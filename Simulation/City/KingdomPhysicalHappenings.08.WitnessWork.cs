using System;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomPhysicalHappenings
	{
		/// <summary>Adapts only a fully restored, sink-settled, attended construction raising.
		/// First exact construction-post participant by resident id is the maker; an attendee is
		/// never silently promoted when no builder was present.</summary>
		internal static bool TryClosedWitnessSource(KingdomHappeningOperation Operation,
			long ClosedTick, out KingdomWitnessWorkSource Source, out string Failure)
		{
			Source = null; Failure = null;
			if (Operation == null || ClosedTick < Operation.UpdatedTick
				|| Operation.Kind != KingdomPhysicalHappeningKind.Raising
				|| Operation.Phase != KingdomHappeningLifecyclePhase.Restoring
				|| !Operation.Physical || !Operation.ExternalSemantic || !Operation.Attended
				|| !KingdomHappeningLifecycleRules.SinksSettled(Operation)
				|| !KingdomHappeningLifecycleRules.RestorationSettled(Operation)
				|| Operation.EventTick <= 0L || string.IsNullOrWhiteSpace(Operation.EventId)
				|| string.IsNullOrWhiteSpace(Operation.SettlementId)
				|| string.IsNullOrWhiteSpace(Operation.DisplayName))
			{
				Failure = "physical event is not one exact closed attended construction raising";
				return false;
			}
			KingdomHappeningParticipant? maker = null;
			for (int i = 0; i < Operation.Participants.Length; i++)
			{
				KingdomHappeningParticipant row = Operation.Participants[i];
				if (!row.Restored || row.PostKind != (int)KingdomWorkKind.Construction
					|| row.ResidentId <= 0 || string.IsNullOrWhiteSpace(row.Name)) continue;
				if (!maker.HasValue || row.ResidentId < maker.Value.ResidentId) maker = row;
			}
			if (!maker.HasValue)
			{
				Failure = "closed raising has no exact named construction-post maker";
				return false;
			}
			Source = new KingdomWitnessWorkSource
			{
				EventId = Operation.EventId,
				SettlementId = Operation.SettlementId,
				EventKind = KingdomWitnessWorkRules.RaisingAdapterKind,
				EventText = "the " + Operation.DisplayName + " was raised",
				ClosedTick = Operation.EventTick,
				MakerResidentId = maker.Value.ResidentId,
				MakerName = maker.Value.Name
			};
			Source.SnapshotDigest = KingdomWitnessWorkRules.SnapshotDigest(Source);
			if (!KingdomWitnessWorkRules.TryCapture(new KingdomWitnessWorkBook(), 0L, Source,
				out _, out Failure))
			{
				Source = null; return false;
			}
			return true;
		}

		private static void CaptureClosedWitness(KingdomSystem System,
			KingdomHappeningOperation Operation, long ClosedTick)
		{
			try
			{
				if (!TryClosedWitnessSource(Operation, ClosedTick,
					out KingdomWitnessWorkSource source, out string failure)) return;
				if (!KingdomWitnessWorkLifecycleRuntime.TryCaptureClosed(System, source, ClosedTick,
					out _, out failure)) KingdomLog.Log(
						"witness work: closed raising retained no offer (" + failure + ")");
			}
			catch (Exception error)
			{
				// O5 is downstream of the owning close. It may lose an offer, never reopen or
				// fault the construction event whose physical restoration already committed.
				KingdomLog.Log("witness work: close adapter failed ("
					+ error.GetType().Name + ")");
			}
		}
	}
}
