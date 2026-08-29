#if !TAF_TESTS
using System;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomGuestFeastRuntime
	{
		/// <summary>Best-effort observation only. Growth never waits for this optional section.</summary>
		internal static bool TryObserveAndProveGrowthDecision(KingdomSystem system, Zone zone,
			KingdomSurvey survey, KingdomGrowthArrivalCandidate candidate, long tick,
			out string failure)
		{
			failure = null;
			try { return TryObserveAndProveGrowthDecisionCore(system, zone, survey,
				candidate, tick, out failure); }
			catch (Exception error)
			{
				return Fail("guest-feast decision adapter failed before Growth mutation ("
					+ error.GetType().Name + ")", out failure);
			}
		}

		private static bool TryObserveAndProveGrowthDecisionCore(KingdomSystem system,
			Zone zone, KingdomSurvey survey, KingdomGrowthArrivalCandidate candidate, long tick,
			out string failure)
		{
			failure = null;
			KingdomGrowthBook growth = system?.LifecycleBook?.Growth;
			KingdomGrowthFirstGuestOpportunity opportunity = candidate?.FirstGuest;
			if (opportunity == null) return true;
			if (!ReferenceEquals(growth?.ArrivalCandidate, candidate)
				|| !KingdomLifecycleRules.CanOwnGrowthAuthority(growth, candidate.SettlementId)
				|| !KingdomGuestFeastRules.ValidOpportunity(candidate.SettlementId, opportunity)
				|| opportunity.ChoiceState != KingdomGrowthFirstGuestChoiceState.Admitted
					&& opportunity.ChoiceState != KingdomGrowthFirstGuestChoiceState.Declined
				|| tick < opportunity.DecisionTick)
				return Fail("Growth decision is not exact current first-guest authority", out failure);
			if (!TryRead(system, out KingdomGuestFeastBook book, out failure)) return false;
			if (!book.IdentityBound) return true;
			if (!KingdomGuestFeastRules.TryFind(book, candidate.SettlementId,
				out KingdomGuestFeastReceipt row)) return false;
			if (row == null) return true;
			if (!TryWriteDecision(system, candidate.SettlementId, opportunity, out failure))
				return false;
			if (!TryReconcileOwners(system, zone, survey, candidate.SettlementId, out string fanout))
				KingdomLog.Log("guest feast: optional owner observation retained (" + fanout + ")");
			return true;
		}

		internal static bool TryBeginPresentedOpportunity(KingdomSystem system,
			KingdomGrowthArrivalCandidate candidate, out string failure)
		{
			failure = null;
			KingdomGrowthBook growth = system?.LifecycleBook?.Growth;
			KingdomGrowthFirstGuestOpportunity opportunity = candidate?.FirstGuest;
			if (!ReferenceEquals(growth?.ArrivalCandidate, candidate)
				|| !KingdomLifecycleRules.CanOwnGrowthAuthority(growth, candidate.SettlementId)
				|| !KingdomGuestFeastRules.ValidOpportunity(candidate.SettlementId, opportunity))
				return Fail("presented first-guest authority is invalid", out failure);
			if (!TryEnsureBound(system, out failure)
				|| !TryRead(system, out KingdomGuestFeastBook book, out failure)
				|| !KingdomGuestFeastRules.TryFind(book, candidate.SettlementId,
					out KingdomGuestFeastReceipt row)) return false;
			if (row != null) return KingdomGuestFeastRules.ExactGuestReference(row, opportunity)
				|| Fail("another guest-feast coordination already occupies this city", out failure);
			KingdomGuestFeastBook next = KingdomGuestFeastRules.Clone(book);
			if (!KingdomGuestFeastRules.TryBegin(next, next.Revision, candidate.SettlementId,
				opportunity, out _, out failure) || !TryPublish(system, next, out failure)) return false;
			KingdomExperienceRuntime.TryRecord(system, KingdomExperienceExperiment.GuestsFeast,
				KingdomExperienceTrialArm.Integrated,
				KingdomExperienceObservationKind.Exposed, 1);
			return true;
		}

		internal static bool TryObserveGrowthTerminalBestEffort(KingdomSystem system,
			KingdomGrowthFirstGuestTerminalReceipt terminal, out string failure)
		{
			failure = null;
			try
			{
				if (terminal == null || !TryRead(system, out KingdomGuestFeastBook book,
					out failure)) return terminal == null;
				if (!book.IdentityBound) return true;
				if (!KingdomGuestFeastRules.TryFind(book, terminal.SettlementId,
					out KingdomGuestFeastReceipt row)) return false;
				if (row == null) return true;
				KingdomGuestFeastBook next = KingdomGuestFeastRules.Clone(book);
				if (!KingdomGuestFeastRules.TryObserveGuestTerminal(next, next.Revision,
					terminal.SettlementId, terminal, out _, out failure)) return false;
				bool changed = next.Revision != book.Revision;
				if (changed && !TryPublish(system, next, out failure)) return false;
				if (terminal.Result == KingdomGrowthArrivalDisposition.Joined)
					KingdomExperienceRuntime.TryRecord(system,
						KingdomExperienceExperiment.GuestsFeast,
						KingdomExperienceTrialArm.Integrated,
						KingdomExperienceObservationKind.Committed, changed ? 1 : 0);
				return true;
			}
			catch (Exception error)
			{
				return Fail("guest-feast terminal adapter retained ("
					+ error.GetType().Name + ")", out failure);
			}
		}

		internal static bool TryReconcileGrowthTerminalBestEffort(KingdomSystem system,
			string settlementId, out string failure)
		{
			failure = null;
			KingdomGrowthFirstGuestTerminalReceipt terminal =
				system?.LifecycleBook?.Growth?.FirstGuestTerminal;
			return terminal == null || terminal.SettlementId != settlementId
				|| TryObserveGrowthTerminalBestEffort(system, terminal, out failure);
		}

		internal static bool ExactGrowthDecisionObserved(KingdomSystem system,
			KingdomGrowthArrivalCandidate candidate, out string failure)
		{
			failure = null;
			try
			{
				KingdomGrowthFirstGuestOpportunity opportunity = candidate?.FirstGuest;
				if (opportunity == null) return true;
				if (!TryRead(system, out KingdomGuestFeastBook book, out failure)
					|| !book.IdentityBound || !string.Equals(book.RealmId, system.RealmId,
						StringComparison.Ordinal)
					|| !KingdomGuestFeastRules.TryFind(book, candidate.SettlementId,
						out KingdomGuestFeastReceipt row) || row == null
					|| !KingdomGuestFeastRules.ExactGuestReference(row, opportunity))
					return Fail(failure ?? "C18 has not observed the exact Growth decision",
						out failure);
				return true;
			}
			catch (Exception error)
			{
				return Fail("C18 Growth-decision proof failed closed ("
					+ error.GetType().Name + ")", out failure);
			}
		}

		private static bool TryWriteDecision(KingdomSystem system, string settlementId,
			KingdomGrowthFirstGuestOpportunity opportunity, out string failure)
		{
			failure = null;
			if (!TryRead(system, out KingdomGuestFeastBook book, out failure)
				|| !KingdomGuestFeastRules.TryFind(book, settlementId,
					out KingdomGuestFeastReceipt row)) return false;
			KingdomGuestFeastBook next = KingdomGuestFeastRules.Clone(book);
			if (row == null) return true;
			bool ok = KingdomGuestFeastRules.TryObserveGuestDecision(next, next.Revision,
				settlementId, opportunity, out _, out failure);
			if (!ok) return false;
			if (next.Revision != book.Revision && !TryPublish(system, next, out failure))
				return false;
			if (!TryRead(system, out book, out failure)
				|| !KingdomGuestFeastRules.TryFind(book, settlementId, out row) || row == null
				|| !KingdomGuestFeastRules.ExactGuestReference(row, opportunity))
				return Fail(failure ?? "exact Growth decision was not reproved in C18", out failure);
			return true;
		}
	}
}
#endif
