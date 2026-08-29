using System;
using System.Collections.Generic;

using XRL;
using XRL.World;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomConstruction
	{
		internal const string InputMarkerProperty = "TAFConstructionInputMarker";
		private const int MaxRoutedInputStepsPerPass = 192;

		private static KingdomConstructionStartResult TryBeginRoutedFunding(
			KingdomConstructionJob job, string requiredObjectId, bool newJob,
			out KingdomConstructionJob published, out string failure)
		{
			return TryBeginRoutedFundingWithRequiredObjects(job,
				string.IsNullOrEmpty(requiredObjectId) ? new string[0]
					: new[] { requiredObjectId }, newJob, out published, out failure);
		}

		private static KingdomConstructionStartResult TryBeginRoutedFundingWithRequiredObjects(
			KingdomConstructionJob job, IList<string> requiredObjectIds, bool newJob,
			out KingdomConstructionJob published, out string failure)
		{
			published = job;
			failure = null;
			KingdomSystem system = The.Game == null ? null : The.Game.GetSystem<KingdomSystem>();
			long now = The.Game == null ? 0L : The.Game.TimeTicks;
			KingdomConstructionInputReceipt receipt;
			if (system == null || !KingdomPurpose.RequiredFundingObjectsMatch(job,
				requiredObjectIds) || !TryPrepareRoutedInputReceiptWithRequiredObjects(system, job,
				requiredObjectIds, now, out receipt, out failure))
			{
				failure = failure ?? "The routed required object does not match its frozen commitment.";
				return KingdomConstructionStartResult.Refused;
			}

			KingdomConstructionJob adopted = job.Copy();
			if (!KingdomConstructionRules.UpdateInputReceipt(ref adopted, receipt)
				|| !(newJob ? TryPublish(adopted, out failure)
					: PublishInputReceipt(job, receipt, out adopted, out failure)))
			{
				TryCancelPreparedRoutedInput(system, receipt, out _);
				return KingdomConstructionStartResult.Refused;
			}
			published = adopted;
			if (!TryActivatePreparedRoutedInput(system, receipt, out failure))
				return KingdomConstructionStartResult.Outstanding;
			KingdomConstructionInputReceipt reserved;
			KingdomConstructionInputFault fault;
			if (KingdomConstructionInputRules.TryTransitionTransaction(receipt,
				receipt.Revision, KingdomConstructionInputTxPhase.ReservationPrepared,
				KingdomConstructionInputTxPhase.Reserved, out reserved, out fault))
				PublishInputReceipt(adopted, reserved, out published, out _);
			return KingdomConstructionStartResult.Outstanding;
		}

		internal static KingdomConstructionStartResult TryFundNewRouted(
			KingdomConstructionJob Job, IList<string> RequiredObjectIds,
			out KingdomConstructionJob Published, out string Failure)
		{
			return TryBeginRoutedFundingWithRequiredObjects(Job, RequiredObjectIds, true,
				out Published, out Failure);
		}

		internal static KingdomConstructionStartResult TryResumeRoutedFunding(
			KingdomConstructionJob Job, IList<string> RequiredObjectIds,
			out KingdomConstructionJob Published, out string Failure)
		{
			return TryBeginRoutedFundingWithRequiredObjects(Job, RequiredObjectIds, false,
				out Published, out Failure);
		}

		private static bool PublishInputReceipt(KingdomConstructionJob current,
			KingdomConstructionInputReceipt receipt, out KingdomConstructionJob published,
			out string failure)
		{
			published = current;
			failure = null;
			if (current == null || receipt == null || !IsCurrent(current))
			{
				failure = "The routed-input owner changed before its receipt could publish.";
				return false;
			}
			long now = The.Game == null ? current.UpdatedTick : The.Game.TimeTicks;
			KingdomConstructionJob next = KingdomConstructionRules.Transition(current,
				current.Phase, now);
			if (!KingdomConstructionRules.UpdateInputReceipt(ref next, receipt)
				|| !TryUpdate(next, out failure)) return false;
			published = next;
			return true;
		}

		private static bool DriveRoutedInput(KingdomSystem system, Zone target,
			ref KingdomConstructionJob job, out string failure)
		{
			failure = null;
			KingdomSurvey activeSurvey = KingdomSurvey.ActiveFor(target);
			if (!ActiveInputGround(target, activeSurvey))
			{
				failure = "Routed construction input waits for exact active ground custody.";
				return false;
			}
			for (int step = 0; step < MaxRoutedInputStepsPerPass; step++)
			{
				if (!TryFind(job.Id, out job))
				{
					failure = "The routed-input owner disappeared during recovery.";
					return false;
				}
				KingdomConstructionInputReceipt receipt;
				if (!KingdomConstructionRules.TryGetInputReceipt(job, out receipt))
				{
					failure = "The routed-input receipt cannot be decoded.";
					return false;
				}
				if (KingdomConstructionInputRules.IsTerminal(receipt))
					return receipt.TxPhase == KingdomConstructionInputTxPhase.Committed;
				bool progressed;
				switch (receipt.TxPhase)
				{
				case KingdomConstructionInputTxPhase.ReservationPrepared:
					progressed = ActivateRoutedInput(system, ref job, receipt, out failure); break;
				case KingdomConstructionInputTxPhase.Reserved:
					progressed = TransitionInputTx(ref job, receipt,
						KingdomConstructionInputTxPhase.SourcePending, out failure); break;
				case KingdomConstructionInputTxPhase.SourcePending:
					if (!InputSourcePendingHere(receipt, target.ZoneID)) return false;
					progressed = DriveInputSources(system, ref job, receipt, out failure); break;
				case KingdomConstructionInputTxPhase.Routing:
					if (receipt.Schema == 2 && target.ZoneID != receipt.TargetZoneId)
					{
						for (int i = 0; i < receipt.ChildCount; i++)
						{
							KingdomConstructionInputChild legacy = receipt.ChildAt(i);
							if (legacy.SourceZoneId != target.ZoneID
								|| KingdomCentralLogistics.ConstructionInputTransitRootExists(
									job.Id, legacy.TripId)) continue;
							if (!KingdomCentralLogistics.TryAdoptSchemaTwoConstructionInputTransit(
								system, job.Id, receipt.PlanDigest, receipt.Revision,
								legacy.JobId, legacy.TripId, job, receipt, i, target,
								out KingdomCityFault legacyFault))
								failure = "Legacy routed custody could not be adopted ("
									+ legacyFault + ").";
							return false;
						}
						return false;
					}
					if (receipt.TargetZoneId != target.ZoneID) return false;
					progressed = DriveInputArrivals(system, target, ref job, receipt, out failure); break;
				case KingdomConstructionInputTxPhase.LandedAwaitingOwner:
					if (receipt.TargetZoneId != target.ZoneID) return false;
					if (!KingdomMaster.NewWorkAllowed(system)) return false;
					progressed = TransitionInputTx(ref job, receipt,
						KingdomConstructionInputTxPhase.DebitPending, out failure); break;
				case KingdomConstructionInputTxPhase.DebitPending:
					if (receipt.TargetZoneId != target.ZoneID) return false;
					if (!KingdomMaster.NewWorkAllowed(system)) return false;
					progressed = DriveInputDebit(system, target, ref job, receipt, out failure); break;
				case KingdomConstructionInputTxPhase.Closing:
					if (receipt.TargetZoneId != target.ZoneID) return false;
					progressed = CloseAndCommitInput(system, ref job, receipt, out failure); break;
				case KingdomConstructionInputTxPhase.RollbackPending:
				case KingdomConstructionInputTxPhase.CompensationPending:
				case KingdomConstructionInputTxPhase.CancellationPending:
					progressed = DriveInputCancellation(system, target, ref job, receipt,
						out failure); break;
				default:
					failure = "The routed-input transaction entered an unsupported phase.";
					return false;
				}
				if (!progressed) return false;
			}
			failure = "The routed-input pass reached its bounded work slice; recovery will continue.";
			return false;
		}

		private static bool InputReceiptTouchesZone(KingdomConstructionInputReceipt receipt,
			string zoneId)
		{
			if (receipt == null || string.IsNullOrEmpty(zoneId)) return false;
			if (receipt.TargetZoneId == zoneId) return true;
			for (int i = 0; i < receipt.SourceCount; i++)
				if (receipt.SourceAt(i).SourceZoneId == zoneId) return true;
			return false;
		}

		private static bool InputSourcePendingHere(KingdomConstructionInputReceipt receipt,
			string zoneId)
		{
			for (int i = 0; receipt != null && i < receipt.SourceCount; i++)
				if (receipt.SourceAt(i).Phase != KingdomConstructionInputSourcePhase.Debited)
					return receipt.SourceAt(i).SourceZoneId == zoneId;
			return true;
		}

		private static bool ActivateRoutedInput(KingdomSystem system,
			ref KingdomConstructionJob job, KingdomConstructionInputReceipt receipt,
			out string failure)
		{
			if (!TryActivatePreparedRoutedInput(system, receipt, out failure)) return false;
			return TransitionInputTx(ref job, receipt,
				KingdomConstructionInputTxPhase.Reserved, out failure);
		}

		private static bool TransitionInputTx(ref KingdomConstructionJob job,
			KingdomConstructionInputReceipt receipt, KingdomConstructionInputTxPhase next,
			out string failure)
		{
			KingdomConstructionInputReceipt updated;
			KingdomConstructionInputFault fault;
			if (!KingdomConstructionInputRules.TryTransitionTransaction(receipt,
				receipt.Revision, receipt.TxPhase, next, out updated, out fault))
			{
				failure = "The routed-input transaction transition was refused (" + fault + ").";
				return false;
			}
			return PublishInputReceipt(job, updated, out job, out failure);
		}
	}
}
