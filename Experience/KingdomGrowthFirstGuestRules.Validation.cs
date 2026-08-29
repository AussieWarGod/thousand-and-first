using System;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		private static bool GrowthFirstGuestShape(KingdomGrowthArrivalCandidate candidate,
			KingdomGrowthArrivalCandidatePhase phase)
		{
			KingdomGrowthFirstGuestOpportunity x = candidate?.FirstGuest;
			if (candidate == null) return false;
			if (candidate.LegacyAutomaticRecovery)
				return x == null && phase != KingdomGrowthArrivalCandidatePhase.AwaitingChoice
					&& phase != KingdomGrowthArrivalCandidatePhase.Declined;
			if (x == null)
				return phase != KingdomGrowthArrivalCandidatePhase.AwaitingChoice
					&& phase != KingdomGrowthArrivalCandidatePhase.Declined;
			if (candidate.Sequence != 1L || x.RulesVersion != 1 && x.RulesVersion != 2
				|| x.CohortSize != 1
				|| !GrowthFirstGuestBlueprintAllowed(candidate.Blueprint)
				|| !Enum.IsDefined(typeof(KingdomGrowthFirstGuestFactsState), x.FactsState)
				|| !Enum.IsDefined(typeof(KingdomGrowthFirstGuestChoiceState), x.ChoiceState)
				|| !Enum.IsDefined(typeof(KingdomGrowthFirstGuestBodyLeaseState), x.BodyLeaseState)
				|| !Enum.IsDefined(typeof(KingdomGrowthFirstGuestGuestPhase), x.GuestPhase)
				|| !Enum.IsDefined(typeof(KingdomGrowthFirstGuestTerminalState),
					x.GuestTerminalState)
				|| !string.Equals(x.OpportunityId, GrowthFirstGuestOpportunityId(
					candidate.SettlementId, candidate.Sequence), StringComparison.Ordinal)
				|| !string.Equals(x.CauseId, GrowthFirstGuestCauseId(candidate.SettlementId,
					candidate.Sequence, x.CauseTick, x.CadenceTicks), StringComparison.Ordinal)
				|| x.CauseTick < 0L || x.OfferedTick != candidate.CreatedTick
				|| x.OfferedTick < x.CauseTick || x.CadenceTicks <= 0L) return false;
			if (x.FactsState == KingdomGrowthFirstGuestFactsState.Exact)
			{
				if (x.PopulationBefore < 0 || x.PopulationCap <= x.PopulationBefore
					|| x.SupportedLevel < 0 || x.SupportCap <= x.PopulationBefore
					|| x.WaterRequired <= 0 || x.WaterAvailable < x.WaterRequired) return false;
			}
			else if (x.PopulationBefore != -1 || x.PopulationCap != -1
				|| x.SupportedLevel != -1 || x.SupportCap != -1
				|| x.WaterAvailable != -1 || x.WaterRequired != -1) return false;
			return (x.RulesVersion != 1 || GrowthFirstGuestPhysicalDefaults(x))
				&& GrowthFirstGuestChoiceShape(candidate, phase, x);
		}

		private static bool GrowthFirstGuestChoiceShape(KingdomGrowthArrivalCandidate candidate,
			KingdomGrowthArrivalCandidatePhase phase, KingdomGrowthFirstGuestOpportunity x)
		{
			bool deferred = x.DeferredReceiptId != null || x.DeferredTick != -1L;
			bool decided = x.ChoiceState == KingdomGrowthFirstGuestChoiceState.Admitted
				|| x.ChoiceState == KingdomGrowthFirstGuestChoiceState.Declined;
			if (deferred ? x.DeferredTick < x.OfferedTick
				|| !string.Equals(x.DeferredReceiptId, GrowthFirstGuestReceiptId(
					x.OpportunityId, "defer", x.DeferredTick), StringComparison.Ordinal)
				: x.DeferredTick != -1L || x.DeferredReceiptId != null) return false;
			if (x.ChoiceState == KingdomGrowthFirstGuestChoiceState.Deferred && !deferred
				|| x.ChoiceState == KingdomGrowthFirstGuestChoiceState.AwaitingChoice && deferred)
				return false;
			if (decided ? x.DecisionTick < x.OfferedTick
				|| !string.Equals(x.DecisionReceiptId, GrowthFirstGuestReceiptId(x.OpportunityId,
					x.ChoiceState == KingdomGrowthFirstGuestChoiceState.Admitted
						? "admit" : "decline", x.DecisionTick), StringComparison.Ordinal)
				: x.DecisionTick != -1L || x.DecisionReceiptId != null) return false;
			if (x.ChoiceState == KingdomGrowthFirstGuestChoiceState.AwaitingChoice
				|| x.ChoiceState == KingdomGrowthFirstGuestChoiceState.Deferred)
				return phase == KingdomGrowthArrivalCandidatePhase.AwaitingChoice
					&& candidate.Disposition == KingdomGrowthArrivalDisposition.None
					&& GrowthFirstGuestNoBodyProof(x) && GrowthFirstGuestPhysicalDefaults(x);
			if (x.ChoiceState == KingdomGrowthFirstGuestChoiceState.Declined)
				return (phase == KingdomGrowthArrivalCandidatePhase.Declined
					|| phase == KingdomGrowthArrivalCandidatePhase.Settled)
					&& candidate.Disposition == KingdomGrowthArrivalDisposition.Declined
					&& GrowthFirstGuestNoBodyProof(x) && GrowthFirstGuestPhysicalDefaults(x);
			if (x.RulesVersion == 2)
				return GrowthPhysicalFirstGuestChoiceShape(candidate, phase, x);
			return phase != KingdomGrowthArrivalCandidatePhase.AwaitingChoice
				&& phase != KingdomGrowthArrivalCandidatePhase.Declined
				&& candidate.Disposition != KingdomGrowthArrivalDisposition.Declined
				&& (GrowthFirstGuestNoBodyProof(x)
					|| GrowthFirstGuestBodyProof(candidate, x));
		}

		private static bool GrowthPhysicalFirstGuestChoiceShape(
			KingdomGrowthArrivalCandidate candidate, KingdomGrowthArrivalCandidatePhase phase,
			KingdomGrowthFirstGuestOpportunity x)
		{
			if (!GrowthFirstGuestBodyProof(candidate, x)) return false;
			if (phase == KingdomGrowthArrivalCandidatePhase.Prepared
				|| phase == KingdomGrowthArrivalCandidatePhase.CreateIntent)
				return x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.Preparing
					&& candidate.Disposition == KingdomGrowthArrivalDisposition.None
					&& GrowthFirstGuestActionEmpty(x) && GrowthFirstGuestTerminalEmpty(x);
			if (phase == KingdomGrowthArrivalCandidatePhase.Escrowed)
				return (x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.Preparing
					|| x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.CitizenshipPrepared)
					&& candidate.Disposition == KingdomGrowthArrivalDisposition.None
					&& (x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.Preparing
						? GrowthFirstGuestActionEmpty(x) : GrowthFirstGuestAction(x, "welcome"))
					&& GrowthFirstGuestTerminalEmpty(x);
			if (phase == KingdomGrowthArrivalCandidatePhase.GuestHosted)
			{
				bool hosted = x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.Hosted;
				bool welcome = x.GuestPhase ==
					KingdomGrowthFirstGuestGuestPhase.CitizenshipIntent;
				bool depart = x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.DepartureIntent;
				return candidate.Disposition == KingdomGrowthArrivalDisposition.None
					&& (hosted && GrowthFirstGuestActionEmpty(x)
						|| welcome && GrowthFirstGuestAction(x, "welcome")
						|| depart && GrowthFirstGuestAction(x, "depart"))
					&& GrowthFirstGuestTerminalEmpty(x);
			}
			if (phase == KingdomGrowthArrivalCandidatePhase.GuestTerminal
				|| phase == KingdomGrowthArrivalCandidatePhase.Settled
					&& candidate.Disposition == KingdomGrowthArrivalDisposition.Departed)
				return candidate.Disposition == KingdomGrowthArrivalDisposition.Departed
					&& x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.Terminal
					&& GrowthFirstGuestTerminalEvidence(x);
			return x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.CitizenshipPrepared
				&& GrowthFirstGuestAction(x, "welcome") && GrowthFirstGuestTerminalEmpty(x)
				&& candidate.Disposition != KingdomGrowthArrivalDisposition.Declined
				&& candidate.Disposition != KingdomGrowthArrivalDisposition.Departed;
		}

		private static bool GrowthFirstGuestPhysicalDefaults(
			KingdomGrowthFirstGuestOpportunity x)
		{
			return x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.None
				&& GrowthFirstGuestActionEmpty(x) && GrowthFirstGuestTerminalEmpty(x);
		}

		private static bool GrowthFirstGuestActionEmpty(KingdomGrowthFirstGuestOpportunity x)
		{
			return x.GuestActionTick == -1L && x.GuestActionReceiptId == null;
		}

		private static bool GrowthFirstGuestAction(KingdomGrowthFirstGuestOpportunity x,
			string kind)
		{
			return x.GuestActionTick >= x.DecisionTick
				&& x.GuestActionReceiptId == GrowthFirstGuestReceiptId(
					x.OpportunityId, kind, x.GuestActionTick);
		}

		private static bool GrowthFirstGuestTerminalEmpty(KingdomGrowthFirstGuestOpportunity x)
		{
			return x.GuestTerminalState == KingdomGrowthFirstGuestTerminalState.None
				&& x.GuestTerminalTick == -1L && x.GuestTerminalReceiptId == null;
		}

		private static bool GrowthFirstGuestTerminalEvidence(
			KingdomGrowthFirstGuestOpportunity x)
		{
			bool departed = x.GuestTerminalState == KingdomGrowthFirstGuestTerminalState.Departed;
			bool died = x.GuestTerminalState == KingdomGrowthFirstGuestTerminalState.Died;
			if (!departed && !died || x.GuestTerminalTick < x.DecisionTick) return false;
			if (departed ? !GrowthFirstGuestAction(x, "depart")
				: !(GrowthFirstGuestActionEmpty(x) || GrowthFirstGuestAction(x, "welcome")))
				return false;
			return x.GuestTerminalReceiptId == GrowthFirstGuestReceiptId(x.OpportunityId,
				departed ? "departed" : "died", x.GuestTerminalTick);
		}

		private static bool CompleteFirstGuestTerminalOpportunity(
			KingdomGrowthFirstGuestOpportunity x, KingdomGrowthArrivalDisposition result,
			long tick)
		{
			if (x == null || x.RulesVersion == 1) return x != null;
			if (result == KingdomGrowthArrivalDisposition.Declined)
				return GrowthFirstGuestPhysicalDefaults(x);
			if (result == KingdomGrowthArrivalDisposition.Departed)
				return GrowthFirstGuestTerminalResultShape(x, result);
			if (x.GuestPhase != KingdomGrowthFirstGuestGuestPhase.CitizenshipPrepared
				|| !GrowthFirstGuestAction(x, "welcome") || !GrowthFirstGuestTerminalEmpty(x)
				|| tick < x.GuestActionTick) return false;
			x.GuestPhase = KingdomGrowthFirstGuestGuestPhase.Terminal;
			x.GuestTerminalState = result == KingdomGrowthArrivalDisposition.Joined
				? KingdomGrowthFirstGuestTerminalState.Citizen
				: KingdomGrowthFirstGuestTerminalState.CouldNotJoin;
			x.GuestTerminalTick = tick;
			x.GuestTerminalReceiptId = GrowthFirstGuestReceiptId(x.OpportunityId,
				result == KingdomGrowthArrivalDisposition.Joined ? "citizen" : "could-not-join",
				tick);
			return GrowthFirstGuestTerminalResultShape(x, result);
		}

		private static bool GrowthFirstGuestTerminalResultShape(
			KingdomGrowthFirstGuestOpportunity x, KingdomGrowthArrivalDisposition result)
		{
			if (result == KingdomGrowthArrivalDisposition.Declined)
				return GrowthFirstGuestPhysicalDefaults(x);
			if (result == KingdomGrowthArrivalDisposition.Departed)
				return x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.Terminal
					&& GrowthFirstGuestTerminalEvidence(x);
			bool citizen = result == KingdomGrowthArrivalDisposition.Joined;
			if (!citizen && result != KingdomGrowthArrivalDisposition.NoAcceptableHome)
				return false;
			KingdomGrowthFirstGuestTerminalState expected = citizen
				? KingdomGrowthFirstGuestTerminalState.Citizen
				: KingdomGrowthFirstGuestTerminalState.CouldNotJoin;
			return x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.Terminal
				&& x.GuestTerminalState == expected && GrowthFirstGuestAction(x, "welcome")
				&& x.GuestTerminalTick >= x.GuestActionTick
				&& x.GuestTerminalReceiptId == GrowthFirstGuestReceiptId(x.OpportunityId,
					citizen ? "citizen" : "could-not-join", x.GuestTerminalTick);
		}

		private static bool GrowthFirstGuestNoBodyProof(KingdomGrowthFirstGuestOpportunity x)
		{
			return x.BodyReservationId == null && x.BodyRealmId == null
				&& x.BodyOptionKind == KingdomExperienceOptionKind.None
				&& x.BodyEnableEpoch == 0L && x.BodyReservedTick == -1L
				&& x.BodyLeaseState == KingdomGrowthFirstGuestBodyLeaseState.None;
		}

		private static bool GrowthFirstGuestBodyProof(KingdomGrowthArrivalCandidate candidate,
			KingdomGrowthFirstGuestOpportunity x)
		{
			return string.Equals(x.BodyReservationId,
				GrowthFirstGuestBodyReservationId(x.OpportunityId), StringComparison.Ordinal)
				&& ValidRootId(x.BodyRealmId)
				&& x.BodyOptionKind == KingdomExperienceOptionKind.CivicStory
				&& x.BodyEnableEpoch > 0L && x.BodyReservedTick >= x.CauseTick
				&& (x.BodyLeaseState == KingdomGrowthFirstGuestBodyLeaseState.Reserved
					|| x.BodyLeaseState == KingdomGrowthFirstGuestBodyLeaseState.Released);
		}
	}
}
