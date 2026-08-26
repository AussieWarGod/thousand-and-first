using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomBountyRules
	{
		/// <summary>
		/// Classifies a persisted exact-water receipt. A row only proves payment when same bound
		/// vessel has exactly its intended post-debit volume. Any other deficit is uncertain and
		/// may not authorize another debit.
		/// </summary>
		public static BountyPaymentObservation ObservePayment(int Requested,
			int[] OriginalVolumes, int[] CurrentVolumes, int[] Allocations,
			bool[] SameVessel, bool[] EmptyOrPureWater, out int ProvedRemoved)
		{
			ProvedRemoved = 0;
			if (Requested <= 0 || OriginalVolumes == null || CurrentVolumes == null
				|| Allocations == null || SameVessel == null || EmptyOrPureWater == null
				|| OriginalVolumes.Length == 0
				|| OriginalVolumes.Length != CurrentVolumes.Length
				|| OriginalVolumes.Length != Allocations.Length
				|| OriginalVolumes.Length != SameVessel.Length
				|| OriginalVolumes.Length != EmptyOrPureWater.Length)
			{
				return BountyPaymentObservation.Malformed;
			}
			bool allOriginal = true;
			bool allDebited = true;
			bool everyRowExact = true;
			long proved = 0L;
			long allocated = 0L;
			for (int i = 0; i < OriginalVolumes.Length; i++)
			{
				int original = OriginalVolumes[i];
				int allocation = Allocations[i];
				if (original <= 0 || allocation <= 0 || allocation > original)
				{
					return BountyPaymentObservation.Malformed;
				}
				allocated += allocation;
				bool identity = SameVessel[i] && EmptyOrPureWater[i];
				bool originalRow = identity && CurrentVolumes[i] == original;
				bool debitedRow = identity && CurrentVolumes[i] == original - allocation;
				allOriginal &= originalRow;
				allDebited &= debitedRow;
				if (debitedRow) proved += allocation;
				if (!originalRow && !debitedRow) everyRowExact = false;
			}
			if (allocated != Requested || proved > int.MaxValue)
			{
				return BountyPaymentObservation.Malformed;
			}
			ProvedRemoved = (int)proved;
			if (allOriginal) return BountyPaymentObservation.Original;
			if (allDebited) return BountyPaymentObservation.Debited;
			return everyRowExact ? BountyPaymentObservation.Mixed : BountyPaymentObservation.Uncertain;
		}

		public static BountyPaymentAction PaymentAction(BountyPaymentPhase Phase,
			BountyPaymentObservation Observation)
		{
			if (Phase == BountyPaymentPhase.Quarantined) return BountyPaymentAction.Wait;
			if (Phase == BountyPaymentPhase.None) return BountyPaymentAction.Bind;
			if (Phase == BountyPaymentPhase.Bound
				&& Observation == BountyPaymentObservation.Original)
			{
				return BountyPaymentAction.Debit;
			}
			if (Phase == BountyPaymentPhase.Credited) return BountyPaymentAction.Wait;
			return BountyPaymentAction.Quarantine;
		}

		/// <summary>Save-facing scalar lifecycle validity. Engine shell additionally validates bindings.</summary>
		public static bool ValidLifecycleScalars(int TaskCode, int Price, int Paid, bool Done,
			string WorkerName, int ScheduleVersion, string EventStreamId, long NextAttemptTick,
			bool ScheduleExhausted, int Passes, int TakePhase, int TransferPhase,
			int PaymentPhase, int TerminalPhase)
		{
			if (TaskCode < 0 || TaskCode >= TaskCount || Price < MinPrice || Price > MaxPrice
				|| Paid < 0 || Paid > Price || Passes < 0 || Passes > MaxPasses)
			{
				return false;
			}
			if (Done && string.IsNullOrEmpty(WorkerName)) return false;
			if (ScheduleVersion != 0 && ScheduleVersion != ScheduledBountyRulesVersion) return false;
			if (ScheduleVersion == ScheduledBountyRulesVersion
				&& (!IsNoticeEventStream(EventStreamId)
					|| (ScheduleExhausted ? NextAttemptTick != 0L : NextAttemptTick <= 0L)))
			{
				return false;
			}
			return TakePhase >= 0 && TakePhase <= (int)BountyTakePhase.Quarantined
				&& TransferPhase >= 0 && TransferPhase <= (int)BountyTransferPhase.Quarantined
				&& PaymentPhase >= 0 && PaymentPhase <= (int)BountyPaymentPhase.Quarantined
				&& TerminalPhase >= 0 && TerminalPhase <= (int)BountyTerminalPhase.CleanupLost;
		}

	}
}
