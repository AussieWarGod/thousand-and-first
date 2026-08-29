namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{
		private static bool PrepareDeclinedFirstGuestOperation(KingdomSystem system,
			KingdomGrowthArrivalCandidate candidate, long tick)
		{
			KingdomGrowthBook growth = system?.LifecycleBook?.Growth;
			if (growth == null || candidate?.FirstGuest?.ChoiceState !=
				KingdomGrowthFirstGuestChoiceState.Declined) return false;
			KingdomGrowthOperation operation = KingdomLifecycleRules.PrepareGrowthOperation(
				growth, KingdomGrowthAction.Arrival, null, tick);
			if (operation == null) return false;
			operation.ArrivalDisposition = KingdomGrowthArrivalDisposition.Declined;
			operation.ArrivalCandidateId = candidate.Id;
			if (!AppendArrivalOutbox(system, operation, "first-guest-declined", null,
				"{{Y|The first guest correspondence was declined without penalty.}}"))
				return false;
			return KingdomLifecycleRules.TryPublishGrowth(growth, operation);
		}

		private static bool PrepareDepartedFirstGuestOperation(KingdomSystem system,
			KingdomGrowthArrivalCandidate candidate, long tick)
		{
			KingdomGrowthBook growth = system?.LifecycleBook?.Growth;
			KingdomGrowthFirstGuestOpportunity x = candidate?.FirstGuest;
			if (growth == null || candidate.Phase !=
				KingdomGrowthArrivalCandidatePhase.GuestTerminal
				|| candidate.Disposition != KingdomGrowthArrivalDisposition.Departed
				|| x?.GuestPhase != KingdomGrowthFirstGuestGuestPhase.Terminal) return false;
			KingdomGrowthOperation operation = KingdomLifecycleRules.PrepareGrowthOperation(
				growth, KingdomGrowthAction.Arrival, null, tick);
			if (operation == null) return false;
			operation.ArrivalDisposition = KingdomGrowthArrivalDisposition.Departed;
			operation.ArrivalCandidateId = candidate.Id;
			string note = x.GuestTerminalState == KingdomGrowthFirstGuestTerminalState.Died
				? "{{K|The first guest died before citizenship.}}"
				: "{{K|The first guest departed before citizenship.}}";
			if (!AppendArrivalOutbox(system, operation, "first-guest-departed", null, note))
				return false;
			return KingdomLifecycleRules.TryPublishGrowth(growth, operation);
		}

		private static bool ReleaseFirstGuestBodyAfterCitizenship(KingdomSystem system,
			KingdomGrowthBook growth, KingdomGrowthArrivalCandidate candidate, long tick,
			out string failure)
		{
			failure = null;
			KingdomGrowthFirstGuestOpportunity x = candidate?.FirstGuest;
			if (x == null) return candidate != null && candidate.LegacyAutomaticRecovery;
			if (x.ChoiceState != KingdomGrowthFirstGuestChoiceState.Admitted)
				return FailFirstGuest("first-guest citizenship has no admitted choice", out failure);
			if (x.BodyLeaseState == KingdomGrowthFirstGuestBodyLeaseState.None) return true;
			if (x.BodyLeaseState == KingdomGrowthFirstGuestBodyLeaseState.Released) return true;
			if (x.BodyLeaseState != KingdomGrowthFirstGuestBodyLeaseState.Reserved)
				return FailFirstGuest("first-guest body lease state is invalid", out failure);
			if (!KingdomExperienceRuntime.TryReleaseBodies(system, x.BodyReservationId,
				x.OpportunityId, out KingdomExperienceCapacityFault _, out failure)) return false;
			return KingdomLifecycleRules.TryMarkGrowthFirstGuestBodyReleased(growth, candidate,
				x.BodyReservationId, tick)
				|| FailFirstGuest("first-guest body release receipt did not publish", out failure);
		}
	}
}
