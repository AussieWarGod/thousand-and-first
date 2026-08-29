using System;
using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		/// <summary>Reconciles each line independently across its own physical boundary.</summary>
		private static bool DriveInputCancellation(KingdomSystem system,
			Zone active, ref KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			out string failure)
		{
			failure = null;
			KingdomConstructionInputChild targetChild;
			if (CancellationTargetPartitionRequired(system, job, receipt, out targetChild,
				out int targetChildOrdinal))
			{
				// A visit to any other touched zone is a pure wait. In particular it must
				// not advance a source line or quarantine the parent.
				if (active == null || active.ZoneID != receipt.TargetZoneId) return false;
				if (!KingdomCentralLogistics.TryRetractConstructionInputTargetCarrier(system,
					job.Id, targetChild.JobId, targetChild.TripId, receipt.Schema,
					receipt.PlanDigest, receipt.Revision, job, receipt, targetChildOrdinal, active,
					out KingdomCityFault targetFault))
					failure = "Cancelled landed carrier could not enter exact transit custody ("
						+ targetFault + ").";
				// Retraction changes physical custody. End this pass even after success.
				return false;
			}
			int requiredSource = NextCancellationSourceOrdinal(receipt);
			for (int i = 0; i < receipt.SourceCount; i++)
			{
				if (i != requiredSource) continue;
				KingdomConstructionInputSourceLine source = receipt.SourceAt(i);
				KingdomConstructionInputCargoLine cargo = receipt.CargoAt(source.CargoOrdinal);
				int childOrdinal = ChildOrdinalForTrip(receipt, cargo.ChildTripId);
				if (CancellationLineComplete(source, cargo)) continue;
				if (childOrdinal < 0 || active == null
					|| active.ZoneID != source.SourceZoneId) return false;
				if (!KingdomCentralLogistics.TryInspectConstructionInputCancellationCarrier(
					system, job.Id, receipt.Schema, receipt.PlanDigest, receipt.Revision,
					cargo.ChildJobId, cargo.ChildTripId, job, receipt, childOrdinal, active,
					out GameObject inspected,
					out KingdomCityFault inspectFault)
					|| !ExactCancellationSourceStanding(active, job, receipt, source,
						cargo, inspected))
				{
					failure = "Cancellation source or carrier standing is stale (" + inspectFault + ").";
					return false;
				}
				if (!KingdomCentralLogistics.TryMaterializeConstructionInputCancellationSource(
					system, job.Id, receipt.Schema, receipt.PlanDigest, receipt.Revision,
					cargo.ChildJobId, cargo.ChildTripId, job, receipt, childOrdinal, active,
					out GameObject carrier,
					out KingdomCityFault sourceFault))
				{
					failure = "The exact cancellation carrier waits for its attended source ("
						+ sourceFault + ").";
					return false;
				}
				if (!ExactCancellationCarrierManifest(system, active, job, receipt, cargo,
					carrier)) return false;
				if (PrepareCancellationCargo(ref job, receipt, cargo, out failure)) return true;
				if (PrepareCancellationSource(ref job, receipt, source, out failure)) return true;
				return source.Kind == KingdomConstructionInputKind.Water
					? RecoverCancelledWater(system, active, carrier, ref job, receipt, source,
						cargo, out failure)
					: RecoverCancelledMaterial(system, active, carrier, ref job, receipt, source,
						cargo, out failure);
			}
			if (!RetireCompletedCancellationCarriers(system, active, job, receipt,
				out failure)) return false;

			KingdomCityFault central;
			int[] expectedTrips = new int[receipt.ChildCount];
			for (int i = 0; i < expectedTrips.Length; i++)
				expectedTrips[i] = receipt.ChildAt(i).TripId;
			if (!KingdomCentralLogistics.TryCloseCancelledConstructionInputOwner(system,
				job.Id, receipt.Schema, receipt.PlanDigest, receipt.Revision, true,
				receipt, expectedTrips, out central))
			{
				failure = "Central logistics could not close cancelled input custody ("
					+ central + ").";
				return false;
			}
			KingdomConstructionInputTxPhase terminal =
				receipt.TxPhase == KingdomConstructionInputTxPhase.RollbackPending
					? KingdomConstructionInputTxPhase.RolledBack
					: receipt.TxPhase == KingdomConstructionInputTxPhase.CompensationPending
						? KingdomConstructionInputTxPhase.Compensated
						: KingdomConstructionInputTxPhase.Cancelled;
			KingdomConstructionInputReceipt closed;
			KingdomConstructionInputFault inputFault;
			if (!KingdomConstructionInputRules.TryTransitionTransaction(receipt,
				receipt.Revision, receipt.TxPhase, terminal, out closed, out inputFault))
			{
				failure = "The recovered cancellation receipt could not close ("
					+ inputFault + ").";
				return false;
			}
			KingdomConstructionPhase jobPhase = terminal
				== KingdomConstructionInputTxPhase.Compensated
					? KingdomConstructionPhase.Compensated
					: KingdomConstructionPhase.Cancelled;
			long now = The.Game == null ? job.UpdatedTick : The.Game.TimeTicks;
			KingdomConstructionJob next = KingdomConstructionRules.Transition(job,
				jobPhase, now, job.Failure);
			if (!KingdomConstructionRules.UpdateInputReceipt(ref next, closed)
				|| !TryUpdate(next, out failure)) return false;
			KingdomConstructionJob routeAuthority = job;
			job = next;
			for (int i = 0; i < closed.ChildCount; i++)
				KingdomCentralLogistics.TryClearConstructionInputRetirement(system, job.Id,
					closed,
					closed.ChildAt(i).TripId);
			ReleaseInputRemainders(routeAuthority, closed, active);
			return true;
		}
		private static bool CancellationLineComplete(KingdomConstructionInputSourceLine source,
			KingdomConstructionInputCargoLine cargo)
		{
			bool sourceDone = source.Phase == KingdomConstructionInputSourcePhase.Reserved
				|| source.Phase == KingdomConstructionInputSourcePhase.Restored
				|| source.Phase == KingdomConstructionInputSourcePhase.Compensated;
			bool cargoDone = cargo.Phase == KingdomConstructionInputCargoPhase.Planned
				|| cargo.Phase == KingdomConstructionInputCargoPhase.Released
				|| cargo.Phase == KingdomConstructionInputCargoPhase.Compensated;
			return sourceDone && cargoDone;
		}
		private static int NextCancellationSourceOrdinal(KingdomConstructionInputReceipt receipt)
		{
			for (int i = 0; receipt != null && i < receipt.SourceCount; i++)
			{
				KingdomConstructionInputSourceLine first = receipt.SourceAt(i);
				if (CancellationLineComplete(first, receipt.CargoAt(first.CargoOrdinal))) continue;
				int required = i;
				for (int j = i + 1; j < receipt.SourceCount; j++)
				{
					KingdomConstructionInputSourceLine later = receipt.SourceAt(j);
					if (later.SourceObjectId == first.SourceObjectId
						&& later.SourceZoneId == first.SourceZoneId
						&& !CancellationLineComplete(later,
							receipt.CargoAt(later.CargoOrdinal))) required = j;
				}
				return required;
			}
			return -1;
		}
		private static int ChildOrdinalForTrip(KingdomConstructionInputReceipt receipt, int tripId)
		{
			int exact = -1;
			for (int i = 0; receipt != null && i < receipt.ChildCount; i++)
				if (receipt.ChildAt(i).TripId == tripId)
				{
					if (exact >= 0) return -1;
					exact = i;
				}
			return exact;
		}
		/// <summary>Returns true only when one durable phase update was published.</summary>
		private static bool PrepareCancellationCargo(ref KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputCargoLine cargo,
			out string failure)
		{
			failure = null;
			KingdomConstructionInputCargoPhase next;
			switch (cargo.Phase)
			{
			case KingdomConstructionInputCargoPhase.CreateIntent:
			case KingdomConstructionInputCargoPhase.AtSource:
			case KingdomConstructionInputCargoPhase.PickupIntent:
				next = KingdomConstructionInputCargoPhase.ReleaseIntent; break;
			case KingdomConstructionInputCargoPhase.InFlight:
			case KingdomConstructionInputCargoPhase.Landed:
			case KingdomConstructionInputCargoPhase.DebitIntent:
				next = KingdomConstructionInputCargoPhase.CompensationIntent; break;
			default:
				return false;
			}
			return InputCargoPhase(ref job, receipt, cargo.Ordinal, next, out failure);
		}
		/// <summary>Returns true only when one durable phase update was published.</summary>
		private static bool PrepareCancellationSource(ref KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputSourceLine source,
			out string failure)
		{
			failure = null;
			KingdomConstructionInputSourcePhase next;
			switch (source.Phase)
			{
			case KingdomConstructionInputSourcePhase.SplitIntent:
			case KingdomConstructionInputSourcePhase.SplitProved:
			case KingdomConstructionInputSourcePhase.TransferIntent:
				next = KingdomConstructionInputSourcePhase.RestoreIntent; break;
			case KingdomConstructionInputSourcePhase.Debited:
				next = KingdomConstructionInputSourcePhase.CompensationIntent; break;
			default:
				return false;
			}
			return InputSourcePhase(ref job, receipt, source.Ordinal, next, out failure);
		}
		private static bool RecoverCancelledMaterial(KingdomSystem system,
			Zone zone, GameObject carrier, ref KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputSourceLine source,
			KingdomConstructionInputCargoLine cargo, out string failure)
		{
			failure = null;
			GameObject holder;
			GameObject item;
			bool graveyard;
			KingdomPhysicalLookupState itemState = FindGlobalInputId(receipt,
				cargo.ObjectId ?? source.SourceObjectId, out item, out graveyard);
			if (zone == null || zone.ZoneID != source.SourceZoneId
				|| KingdomSurvey.ActiveFor(zone) == null
				|| FindExactId(zone, source.HolderId, out holder)
					!= KingdomPhysicalLookupState.Exact || holder.Inventory == null
					|| holder.CurrentCell == null || holder.CurrentCell.X != source.X
					|| holder.CurrentCell.Y != source.Y || itemState != KingdomPhysicalLookupState.Exact
					|| graveyard || !GameObject.Validate(item) || item.Blueprint != source.Blueprint
					|| !RoutedInputItemAuthorized(job, receipt, item)
					|| !GameObject.Validate(carrier) || carrier.CurrentZone != zone
					|| carrier.CurrentCell == null || carrier.Inventory == null)
			{
				failure = "Cancelled material custody is absent, ambiguous, or no longer lawful.";
				return false;
			}
			if (!ExactCancellationCarrierManifest(system, zone, job, receipt, cargo,
				carrier)) return false;
			if (!ReferenceEquals(item.InInventory, holder)
				|| ReferenceCount(holder.Inventory.Objects, item) != 1)
			{
				if (receipt.Paused || !KingdomMaster.NewWorkAllowed(system)) return false;
				if (!ExactCancellationCarrierManifest(system, zone, job, receipt, cargo,
					carrier)) return false;
				if (source.ResidualAfter > 0
					&& !ExactLiveRoutedSplitRemainder(zone, holder, job, receipt, source))
					return false;
				GameObject previous = item.InInventory;
				GameObject accepted = null;
				int routeNeverStack = KingdomPurpose.HasProtectedCargoEvidence(item) ? -1 : 1;
				if (!ReferenceEquals(previous, carrier) || previous.Inventory == null
					|| ReferenceCount(previous.Inventory.Objects, item) != 1
					|| !ExactRoutedMaterialObject(job, receipt, source, cargo, item, source.Take,
						cargo.CargoKey, routeNeverStack)
					|| source.ResidualAfter > 0
						&& !ExactLiveRoutedSplitRemainder(zone, holder, job, receipt, source))
				{
					failure = "Cancelled material is not in its exact receipt carrier.";
					return false;
				}
				try { accepted = holder.Inventory.AddObjectToInventory(item, null,
					Silent: true, NoStack: true); } catch { }
				finally
				{
					KingdomSurvey.ObserveChangedInActive(previous?.CurrentZone, previous);
					KingdomSurvey.ObserveChangedInActive(zone, holder);
					KingdomSurvey.ObserveAddResultInActive(zone, item, accepted);
				}
				if (!ReferenceEquals(accepted, item) || !ExactRoutedMaterialAtHolder(zone,
					holder, item, job, receipt, source, cargo, source.Take,
					cargo.CargoKey, routeNeverStack))
				{
					failure = "The exact cancelled material could not return to its frozen holder.";
					return false;
				}
				if (!ExactCancellationCarrierManifest(system, zone, job, receipt, cargo,
					carrier)) return false;
			}

			if (source.ResidualAfter > 0
				&& !RestoreCancelledSplit(system, zone, holder, carrier, item, ref job,
					receipt, source, cargo, out failure))
				return false;
			if (item.Count != source.Before)
			{
				failure = "The returned material does not prove its frozen whole count.";
				return false;
			}
			return CloseCancelledLine(system, zone, ref job, receipt, source, cargo, holder.ID,
				source.SourceZoneId, source.X, source.Y, out failure);
		}
	}
}
