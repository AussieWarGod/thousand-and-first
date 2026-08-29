namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		private static bool GrowthFirstGuestBodyLeasePhaseShape(KingdomGrowthBook book,
			KingdomGrowthArrivalCandidate candidate, KingdomGrowthArrivalCandidatePhase phase)
		{
			KingdomGrowthFirstGuestOpportunity x = candidate?.FirstGuest;
			if (x == null || x.ChoiceState != KingdomGrowthFirstGuestChoiceState.Admitted
				|| x.BodyLeaseState != KingdomGrowthFirstGuestBodyLeaseState.Released) return true;
			return GrowthFirstGuestBodyReleasePhaseShape(book, candidate, phase);
		}

		private static bool GrowthFirstGuestBodyReleasePhaseShape(KingdomGrowthBook book,
			KingdomGrowthArrivalCandidate candidate, KingdomGrowthArrivalCandidatePhase phase)
		{
			if (phase == KingdomGrowthArrivalCandidatePhase.GuestTerminal
				&& candidate?.Disposition == KingdomGrowthArrivalDisposition.Departed)
				return true;
			if (phase != KingdomGrowthArrivalCandidatePhase.Settled) return false;
			KingdomGrowthOperation op = book?.ArrivalOp;
			if (op == null) return GrowthRetiredArrivalBarrierExists(book, candidate);
			if (op.ArrivalDisposition == KingdomGrowthArrivalDisposition.NoAcceptableHome)
				return op.Phase == KingdomGrowthPhase.Prepared
					|| op.Phase == KingdomGrowthPhase.ClockIntent
					|| op.Phase == KingdomGrowthPhase.Sinks
					|| op.Phase == KingdomGrowthPhase.Terminal
					|| op.Phase == KingdomGrowthPhase.Quarantined;
			if (op.ArrivalDisposition == KingdomGrowthArrivalDisposition.Departed)
				return op.Phase == KingdomGrowthPhase.Prepared
					|| op.Phase == KingdomGrowthPhase.ClockIntent
					|| op.Phase == KingdomGrowthPhase.Sinks
					|| op.Phase == KingdomGrowthPhase.Terminal
					|| op.Phase == KingdomGrowthPhase.Quarantined;
			return op.ArrivalDisposition == KingdomGrowthArrivalDisposition.Joined
				&& (op.Phase == KingdomGrowthPhase.DomainSettled
					|| op.Phase == KingdomGrowthPhase.ClockIntent
					|| op.Phase == KingdomGrowthPhase.Sinks
					|| op.Phase == KingdomGrowthPhase.Terminal
					|| op.Phase == KingdomGrowthPhase.Quarantined
						&& op.DomainSteps != null && op.DomainSteps.Count > 0
						&& op.DomainCursor == op.DomainSteps.Count);
		}
	}
}
