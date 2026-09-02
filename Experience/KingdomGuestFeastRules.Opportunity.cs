using System;

namespace ThousandAndFirst
{
	public static partial class KingdomGuestFeastRules
	{
		internal static bool ValidOpportunity(string settlementId,
			KingdomGrowthFirstGuestOpportunity x)
		{
			if (!KingdomIdentityRules.IsSettlementId(settlementId) || x == null
				|| x.RulesVersion != 1 && x.RulesVersion != 2 || x.CohortSize != 1
				|| x.FactsState != KingdomGrowthFirstGuestFactsState.Exact
				|| x.CauseTick < 0L || x.OfferedTick < x.CauseTick || x.CadenceTicks <= 0L
				|| x.OpportunityId != KingdomGrowthFirstGuestIdentityRules.OpportunityId(settlementId, 1L)
				|| x.CauseId != KingdomGrowthFirstGuestIdentityRules.CauseId(
					settlementId, 1L, x.CauseTick, x.CadenceTicks)) return false;
			bool decided = x.ChoiceState == KingdomGrowthFirstGuestChoiceState.Admitted
				|| x.ChoiceState == KingdomGrowthFirstGuestChoiceState.Declined;
			if (!(decided ? x.DecisionTick >= x.OfferedTick
				&& GeneratedId(x.DecisionReceiptId,
					"taf:growth-first-guest-receipt:", MaxGuestDecisionIdBytes)
				: (x.ChoiceState == KingdomGrowthFirstGuestChoiceState.AwaitingChoice
					|| x.ChoiceState == KingdomGrowthFirstGuestChoiceState.Deferred)
					&& x.DecisionTick == -1L && x.DecisionReceiptId == null)) return false;
			return x.RulesVersion == 1 ? LegacyGuestAuthority(x)
				: PhysicalGuestAuthority(x);
		}

		private static bool PhysicalGuestAuthority(KingdomGrowthFirstGuestOpportunity x)
		{
			if (!Enum.IsDefined(typeof(KingdomGrowthFirstGuestGuestPhase), x.GuestPhase)
				|| !Enum.IsDefined(typeof(KingdomGrowthFirstGuestTerminalState),
					x.GuestTerminalState)) return false;
			if (x.ChoiceState != KingdomGrowthFirstGuestChoiceState.Admitted)
				return NoPhysicalGuestAuthority(x);
			if (!ValidBodyAuthority(x)) return false;
			bool action = x.GuestActionTick >= x.DecisionTick
				&& GeneratedId(x.GuestActionReceiptId,
					"taf:growth-first-guest-receipt:", MaxGuestDecisionIdBytes);
			bool noAction = x.GuestActionTick == -1L && x.GuestActionReceiptId == null;
			bool terminal = x.GuestTerminalTick >= x.DecisionTick
				&& GeneratedId(x.GuestTerminalReceiptId,
					"taf:growth-first-guest-receipt:", MaxGuestDecisionIdBytes)
				&& x.GuestTerminalState != KingdomGrowthFirstGuestTerminalState.None;
			bool noTerminal = x.GuestTerminalTick == -1L && x.GuestTerminalReceiptId == null
				&& x.GuestTerminalState == KingdomGrowthFirstGuestTerminalState.None;
			if (x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.Preparing
				|| x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.Hosted)
				return noAction && noTerminal && x.BodyLeaseState ==
					KingdomGrowthFirstGuestBodyLeaseState.Reserved;
			if (x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.CitizenshipIntent
				|| x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.CitizenshipPrepared
				|| x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.DepartureIntent)
				return action && noTerminal && x.BodyLeaseState ==
					KingdomGrowthFirstGuestBodyLeaseState.Reserved;
			return x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.Terminal && terminal
				&& x.BodyLeaseState == KingdomGrowthFirstGuestBodyLeaseState.Released
				&& (action || x.GuestTerminalState == KingdomGrowthFirstGuestTerminalState.Died
					&& noAction);
		}
		private static bool NoPhysicalGuestAuthority(KingdomGrowthFirstGuestOpportunity x) =>
			NoBodyAuthority(x) && NoGuestStateAuthority(x);
		private static bool LegacyGuestAuthority(KingdomGrowthFirstGuestOpportunity x)
		{
			return NoGuestStateAuthority(x) && (NoBodyAuthority(x)
				|| x.ChoiceState == KingdomGrowthFirstGuestChoiceState.Admitted
					&& ValidBodyAuthority(x));
		}
		private static bool ValidBodyAuthority(KingdomGrowthFirstGuestOpportunity x)
		{
			return GeneratedId(x.BodyReservationId, "taf:experience-body:first-guest:v1:",
				MaxBodyReservationIdBytes) && KingdomIdentityRules.IsRealmId(x.BodyRealmId)
				&& x.BodyOptionKind == KingdomExperienceOptionKind.CivicStory
				&& x.BodyEnableEpoch > 0L && x.BodyReservedTick >= x.CauseTick
				&& x.BodyReservedTick <= x.DecisionTick
				&& (x.BodyLeaseState == KingdomGrowthFirstGuestBodyLeaseState.Reserved
					|| x.BodyLeaseState == KingdomGrowthFirstGuestBodyLeaseState.Released);
		}
		private static bool NoBodyAuthority(KingdomGrowthFirstGuestOpportunity x) =>
			x.BodyReservationId == null && x.BodyRealmId == null
			&& x.BodyOptionKind == KingdomExperienceOptionKind.None && x.BodyEnableEpoch == 0L
			&& x.BodyReservedTick == -1L
			&& x.BodyLeaseState == KingdomGrowthFirstGuestBodyLeaseState.None;
		private static bool NoGuestStateAuthority(KingdomGrowthFirstGuestOpportunity x)
		{
			return x.GuestPhase == KingdomGrowthFirstGuestGuestPhase.None
				&& x.GuestTerminalState == KingdomGrowthFirstGuestTerminalState.None
				&& x.GuestActionTick == -1L && x.GuestActionReceiptId == null
				&& x.GuestTerminalTick == -1L && x.GuestTerminalReceiptId == null;
		}
	}
}
