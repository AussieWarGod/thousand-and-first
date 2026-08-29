namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		private static bool GrowthArrivalCreateProvedPhase(
			KingdomGrowthArrivalCandidatePhase phase)
		{
			return phase == KingdomGrowthArrivalCandidatePhase.Escrowed
				|| phase == KingdomGrowthArrivalCandidatePhase.GuestHosted
				|| phase == KingdomGrowthArrivalCandidatePhase.GuestTerminal
				|| phase == KingdomGrowthArrivalCandidatePhase.LodgingIntent
				|| phase == KingdomGrowthArrivalCandidatePhase.Observed
				|| phase == KingdomGrowthArrivalCandidatePhase.ConsumeIntent
				|| phase == KingdomGrowthArrivalCandidatePhase.RefusalIntent
				|| phase == KingdomGrowthArrivalCandidatePhase.Settled;
		}

		private static bool GrowthArrivalLodgingProvedPhase(
			KingdomGrowthArrivalCandidatePhase phase)
		{
			return phase == KingdomGrowthArrivalCandidatePhase.Observed
				|| phase == KingdomGrowthArrivalCandidatePhase.ConsumeIntent
				|| phase == KingdomGrowthArrivalCandidatePhase.RefusalIntent
				|| phase == KingdomGrowthArrivalCandidatePhase.Settled;
		}

		private static bool GrowthFirstGuestDeclinedSettled(
			KingdomGrowthArrivalCandidate candidate, KingdomGrowthArrivalCandidatePhase phase)
		{
			return phase == KingdomGrowthArrivalCandidatePhase.Settled
				&& candidate?.FirstGuest?.ChoiceState ==
					KingdomGrowthFirstGuestChoiceState.Declined;
		}

		private static bool GrowthFirstGuestPhysicalTerminalSettled(
			KingdomGrowthArrivalCandidate candidate, KingdomGrowthArrivalCandidatePhase phase)
		{
			return phase == KingdomGrowthArrivalCandidatePhase.Settled
				&& candidate?.Disposition == KingdomGrowthArrivalDisposition.Departed
				&& candidate.FirstGuest?.RulesVersion == 2
				&& candidate.FirstGuest.GuestPhase ==
					KingdomGrowthFirstGuestGuestPhase.Terminal;
		}
	}
}
