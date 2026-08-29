using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		private static bool DriveInputSources(KingdomSystem system,
			ref KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			out string failure)
		{
			failure = null;
			for (int i = 0; i < receipt.ChildCount; i++)
			{
				KingdomConstructionInputChild child = receipt.ChildAt(i);
				for (int j = child.CargoStart; j < child.CargoStart + child.CargoCount; j++)
				{
					KingdomConstructionInputCargoLine cargo = receipt.CargoAt(j);
					KingdomConstructionInputSourceLine source =
						receipt.SourceAt(cargo.SourceLineOrdinal);
					if (source.Phase == KingdomConstructionInputSourcePhase.Debited) continue;
					Zone zone; GameObject carrier;
					if (!TryInputSourceAuthority(system, job, receipt, cargo, source,
						out zone, out carrier, out failure)) return false;
					return source.Kind == KingdomConstructionInputKind.Water
						? DriveInputWater(system, zone, carrier, ref job, receipt, source,
							cargo, out failure)
						: DriveInputMaterial(system, zone, carrier, ref job, receipt, source,
							cargo, out failure);
				}
				for (int j = child.CargoStart; j < child.CargoStart + child.CargoCount; j++)
				{
					KingdomConstructionInputCargoLine cargo = receipt.CargoAt(j);
					if (cargo.Phase == KingdomConstructionInputCargoPhase.PickupIntent)
						return InputCargoPhase(ref job, receipt, j,
							KingdomConstructionInputCargoPhase.InFlight, out failure);
					if (cargo.Phase != KingdomConstructionInputCargoPhase.InFlight)
					{
						failure = "Routed cargo did not reach exact carrier custody.";
						return false;
					}
				}
				if (child.CentralPhase == (int)KingdomDeliveryPhase.InFlight) continue;
				KingdomCityFault central = KingdomCityFault.OutsideItinerary;
				Zone active = The.ZoneManager == null ? null : The.ZoneManager.ActiveZone;
				GameObject wholeCarrier;
				if (active != null && active.ZoneID == child.SourceZoneId
					&& KingdomCentralLogistics.TryResolveConstructionInputRootedPickup(system,
						job.Id, child.JobId, child.TripId, receipt.Schema, receipt.PlanDigest,
						receipt.Revision, job, receipt, i, active, out wholeCarrier, out central))
				{
					if (!ExactAuthorizedChildCargo(wholeCarrier, job, receipt, child)
						|| !KingdomCentralLogistics.TryAcknowledgeConstructionInputPickup(system,
							job.Id, child.JobId, child.TripId, receipt.Schema, receipt.PlanDigest,
							receipt.Revision, out central)) return false;
					return InputChildEvidence(ref job, receipt, i,
						(int)KingdomDeliveryPhase.InFlight, receipt.Revision, out failure);
				}
				if (central != KingdomCityFault.UnknownBinding
					&& central != KingdomCityFault.OutsideItinerary) return false;
				if (active == null || active.ZoneID != child.SourceZoneId
					|| !KingdomCentralLogistics.TryResolveConstructionInputSourceCarrier(system,
						job.Id, child.JobId, child.TripId, receipt.Schema, receipt.PlanDigest,
						receipt.Revision, out wholeCarrier, out central)
					|| !KingdomCentralLogistics.ExactConstructionInputTransitManifest(
						wholeCarrier, job, receipt, i)
					|| !ExactAuthorizedChildCargo(wholeCarrier, job, receipt, child)
					|| !KingdomCentralLogistics.TryProveConstructionInputTransitRootable(system,
						job.Id, child.JobId, child.TripId, wholeCarrier, out central)
					|| !ReleaseDebitedInputRemaindersOnActiveSource(job, receipt, active,
						out failure)
					|| !KingdomCentralLogistics.TryRootConstructionInputTransitCarrier(system,
						job.Id, child.JobId, child.TripId, active, out central))
				{
					failure = failure ?? "The exact construction carrier could not enter semantic transit custody ("
						+ central + ").";
					return false;
				}
				if (!KingdomCentralLogistics.TryAcknowledgeConstructionInputPickup(system,
					job.Id, child.JobId, child.TripId, receipt.Schema, receipt.PlanDigest,
					receipt.Revision, out central))
				{
					failure = "Central logistics could not acknowledge exact pickup ("
						+ central + ").";
					return false;
				}
				return InputChildEvidence(ref job, receipt, i,
					(int)KingdomDeliveryPhase.InFlight, receipt.Revision, out failure);
			}
			return TransitionInputTx(ref job, receipt,
				KingdomConstructionInputTxPhase.Routing, out failure);
		}

		private static bool ExactAuthorizedChildCargo(GameObject carrier,
			KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputChild child)
		{
			for (int i = child.CargoStart; i < child.CargoStart + child.CargoCount; i++)
			{
				KingdomConstructionInputCargoLine cargo = receipt.CargoAt(i);
				GameObject item = null; int matches = 0;
				for (int j = 0; carrier?.Inventory != null
					&& j < carrier.Inventory.Objects.Count; j++)
					if (carrier.Inventory.Objects[j]?.IDIfAssigned == cargo.ObjectId)
					{ item = carrier.Inventory.Objects[j]; matches++; }
				if (matches != 1 || !ReferenceEquals(item.InInventory, carrier)
					|| !RoutedInputItemAuthorized(job, receipt, item)) return false;
			}
			return true;
		}

		private static bool TryInputSourceAuthority(KingdomSystem system,
			KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			KingdomConstructionInputCargoLine cargo, KingdomConstructionInputSourceLine source,
			out Zone zone, out GameObject carrier, out string failure)
		{
			zone = null;
			carrier = null;
			failure = null;
			zone = The.ZoneManager == null ? null : The.ZoneManager.ActiveZone;
			KingdomSurvey activeSurvey = KingdomSurvey.ActiveFor(zone);
			if (zone == null || zone.ZoneID != source.SourceZoneId
				|| !ActiveInputGround(zone, activeSurvey))
			{
				failure = "The frozen routed-input source waits for its exact attended ground.";
				return false;
			}
			// Prove the durable endpoint before Render is allowed to mint or bind a
			// SourceDebitPrepared projection.
			GameObject preflightHolder;
			if (!system.OwnedZone(source.SourceZoneId)
				|| system.SettlementIdForOwnedZone(source.SourceZoneId)
					!= source.SourceSettlementId
				|| FindExactId(zone, source.HolderId, out preflightHolder)
					!= KingdomPhysicalLookupState.Exact
				|| !GameObject.Validate(preflightHolder) || preflightHolder.CurrentZone != zone
				|| preflightHolder.CurrentCell != zone.GetCell(source.X, source.Y)
				|| !ExactInputSourcePreflight(system, zone, preflightHolder, job, receipt,
					cargo, source))
			{
				failure = "The frozen routed-input endpoint is stale or no longer claimed.";
				return false;
			}
			if (!KingdomCentralLogistics.TryProveConstructionInputSourceRow(system, job.Id,
				cargo.ChildJobId, cargo.ChildTripId, receipt.Schema, receipt.PlanDigest,
				receipt.Revision, source.SourceZoneId, out KingdomCityFault rowFault))
			{
				failure = "The routed-input source row is stale (" + rowFault + ").";
				return false;
			}
			if (!KingdomPorters.RenderConstructionInputSource(system, zone, job.Id,
				cargo.ChildJobId, cargo.ChildTripId, receipt.Schema, receipt.PlanDigest,
				receipt.Revision, The.Game == null ? 0L : The.Game.TimeTicks))
			{
				failure = "The exact empty construction carrier could not project at source.";
				return false;
			}
			KingdomCityFault central;
			if (!KingdomCentralLogistics.TryResolveConstructionInputSourceCarrier(system,
				job.Id, cargo.ChildJobId, cargo.ChildTripId, receipt.Schema,
				receipt.PlanDigest, receipt.Revision, out carrier, out central))
			{
				failure = "The exact routed-input source carrier is unavailable ("
					+ central + ").";
				return false;
			}
			if (!GameObject.Validate(carrier) || !carrier.IsAlive || carrier.Inventory == null
				|| carrier.IsPlayer() || carrier.IsPlayerLed()
				|| carrier.GetIntProperty(KingdomResidents.JobIdProperty) != cargo.ChildJobId
				|| carrier.CurrentCell == null || carrier.CurrentZone != zone
				|| !ExactSourcePickupManifest(zone, carrier, job, receipt, cargo, source))
			{
				failure = "The routed-input source carrier lost lawful autonomous custody.";
				return false;
			}
			GameObject holder;
			GameObject exact;
			if (FindExactId(zone, source.HolderId, out holder) != KingdomPhysicalLookupState.Exact
				|| FindExactId(zone, source.SourceObjectId, out exact)
					!= KingdomPhysicalLookupState.Exact
				|| holder.CurrentCell == null || holder.CurrentZone != zone
				|| holder.CurrentCell.X != source.X || holder.CurrentCell.Y != source.Y
				|| !ExactInputDedication(zone, source, holder, exact)
					&& !ExactRoutedMaterialAtCarrier(zone, carrier, exact, job, receipt,
						source, cargo, cargo.CargoKey,
						KingdomPurpose.HasProtectedCargoEvidence(exact) ? -1 : 1))
			{
				failure = "The frozen source holder, dedication, or exact inventory reference changed.";
				return false;
			}
			return true;
		}

		private static bool ExactInputDedication(Zone zone,
			KingdomConstructionInputSourceLine source, GameObject holder, GameObject exact)
		{
			if (source.Kind == KingdomConstructionInputKind.Water)
			{
				KingdomSurvey survey = KingdomSurvey.ActiveFor(zone);
				return ReferenceEquals(holder, exact) && source.DedicationOrdinal >= 0
					&& source.DedicationOrdinal < survey.Stores.Count
					&& ReferenceEquals(survey.Stores[source.DedicationOrdinal]?.ParentObject,
						holder);
			}
			KingdomSurvey active = KingdomSurvey.ActiveFor(zone);
			return holder.Inventory != null && ReferenceEquals(exact.InInventory, holder)
				&& (exact.Count == source.Before || exact.Count == source.Take)
				&& source.DedicationOrdinal >= 0
				&& active != null
				&& source.DedicationOrdinal < active.MaterialStockpiles.Count
				&& ReferenceEquals(active.MaterialStockpiles[source.DedicationOrdinal], holder);
		}

		private static bool DriveInputMaterial(KingdomSystem system, Zone zone,
			GameObject carrier, ref KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputSourceLine source,
			KingdomConstructionInputCargoLine cargo, out string failure)
		{
			failure = null;
			GameObject holder;
			GameObject item;
			KingdomConstructionInputKind liveKind;
			string liveClassification;
			if (FindExactId(zone, source.HolderId, out holder) != KingdomPhysicalLookupState.Exact
				|| holder.Inventory == null
				|| FindExactId(zone, source.SourceObjectId, out item)
					!= KingdomPhysicalLookupState.Exact
				|| item.Blueprint != source.Blueprint
				|| !TryInputClassification(item, out liveKind, out liveClassification)
				|| liveKind != source.Kind || liveClassification != source.Classification
				|| !RoutedInputItemAuthorized(job, receipt, item) || item.IsImportant()
				|| item.Equipped != null || !item.IsTakeable() || item.HasTag("AlwaysStack"))
			{
				failure = "The frozen construction material source changed or became unsafe.";
				return false;
			}
			bool protectedCargo = KingdomPurpose.HasProtectedCargoEvidence(item);
			bool markerAbsent = !item.HasStringProperty(InputMarkerProperty)
				&& !item.HasIntProperty(InputMarkerProperty);
			bool cargoMarker = item.HasStringProperty(InputMarkerProperty)
				&& !item.HasIntProperty(InputMarkerProperty)
				&& item.GetStringProperty(InputMarkerProperty) == cargo.CargoKey;
			bool splitMarker = !protectedCargo
				&& source.Phase == KingdomConstructionInputSourcePhase.SplitIntent
				&& item.HasStringProperty(InputMarkerProperty)
				&& !item.HasIntProperty(InputMarkerProperty)
				&& item.GetStringProperty(InputMarkerProperty) == source.RemainderMarker;
			if (!markerAbsent && !cargoMarker && !splitMarker
				|| !protectedCargo && item.GetIntProperty("NeverStack")
					!= (markerAbsent ? 0 : 1))
			{
				failure = "The frozen construction source acquired foreign route or stack policy.";
				return false;
			}
			if (string.IsNullOrEmpty(cargo.ObjectId))
				return InputCargoEvidence(ref job, receipt, cargo.Ordinal,
					source.SourceObjectId, cargo.CustodyTopology, cargo.CustodyOwnerId,
					cargo.CustodyZoneId, cargo.CustodyX, cargo.CustodyY,
					cargo.BeforeWitnessHash, cargo.AfterWitnessHash, cargo.Spent,
					cargo.Lost, out failure);
			if (cargo.Phase == KingdomConstructionInputCargoPhase.Planned)
				return InputCargoPhaseEvidence(ref job, receipt, cargo.Ordinal,
					KingdomConstructionInputCargoPhase.AtSource, cargo.ObjectId,
					KingdomConstructionInputTopology.ContainerInventory, source.HolderId,
					source.SourceZoneId, source.X, source.Y, cargo.BeforeWitnessHash,
					cargo.AfterWitnessHash, cargo.Spent, cargo.Lost, out failure);
			if (source.Phase == KingdomConstructionInputSourcePhase.Reserved)
				return InputSourcePhase(ref job, receipt, source.Ordinal,
					source.ResidualAfter > 0 ? KingdomConstructionInputSourcePhase.SplitIntent
						: KingdomConstructionInputSourcePhase.TransferIntent, out failure);
			if (source.Phase == KingdomConstructionInputSourcePhase.SplitIntent)
				return DriveInputSplit(system, zone, holder, carrier, item, ref job, receipt,
					source, out failure);
			if (source.Phase == KingdomConstructionInputSourcePhase.SplitProved)
				return InputSourcePhase(ref job, receipt, source.Ordinal,
					KingdomConstructionInputSourcePhase.TransferIntent, out failure);
			if (source.Phase != KingdomConstructionInputSourcePhase.TransferIntent
				|| cargo.Phase != KingdomConstructionInputCargoPhase.AtSource
					&& cargo.Phase != KingdomConstructionInputCargoPhase.PickupIntent)
			{
				failure = "The construction material transfer phases disagree.";
				return false;
			}
			if (cargo.Phase == KingdomConstructionInputCargoPhase.AtSource)
				return InputCargoPhase(ref job, receipt, cargo.Ordinal,
					KingdomConstructionInputCargoPhase.PickupIntent, out failure);
			return DriveInputMaterialMove(system, zone, holder, carrier, item,
				ref job, receipt, source, cargo, out failure);
		}
	}
}
