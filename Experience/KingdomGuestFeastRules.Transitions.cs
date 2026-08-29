using System;

namespace ThousandAndFirst
{
	public static partial class KingdomGuestFeastRules
	{
		public static bool TryBegin(KingdomGuestFeastBook book, long expectedRevision,
			string settlementId, KingdomGrowthFirstGuestOpportunity opportunity,
			out KingdomGuestFeastReceipt receipt, out string failure)
		{
			receipt = null; failure = null;
			if (!TryValidate(book, out failure) || !book.IdentityBound
				|| !ValidOpportunity(settlementId, opportunity))
				return Fail(failure ?? "guest-feast opportunity is invalid", out failure);
			int standing = Index(book, settlementId);
			if (standing >= 0)
			{
				receipt = Copy(book.Rows[standing]);
				return ExactGuestReference(receipt, opportunity)
					|| Fail("settlement already coordinates another guest feast", out failure);
			}
			if (expectedRevision != book.Revision || book.Rows.Count >= MaxRows
				|| book.Revision == long.MaxValue)
				return Fail("guest-feast revision or capacity refused", out failure);
			KingdomGuestFeastReceipt row = new KingdomGuestFeastReceipt
			{
				Phase = KingdomGuestFeastPhase.AwaitingGuestChoice,
				SettlementId = settlementId, OpportunityId = opportunity.OpportunityId,
				CauseId = opportunity.CauseId, CauseTick = opportunity.CauseTick
			};
			if (opportunity.ChoiceState == KingdomGrowthFirstGuestChoiceState.Admitted
				|| opportunity.ChoiceState == KingdomGrowthFirstGuestChoiceState.Declined)
			{
				row.GuestDecisionReceiptId = opportunity.DecisionReceiptId;
				row.GuestDecisionTick = opportunity.DecisionTick;
				row.Phase = KingdomGuestFeastPhase.AwaitingGuestResult;
			}
			KingdomGuestFeastBook next = Clone(book); next.Rows.Add(row);
			next.Rows.Sort((a, b) => string.CompareOrdinal(a.SettlementId, b.SettlementId));
			next.Revision++;
			if (!TryValidate(next, out failure)) return false;
			Replace(book, next); receipt = Copy(row); return true;
		}

		public static bool TryObserveGuestDecision(KingdomGuestFeastBook book,
			long expectedRevision, string settlementId,
			KingdomGrowthFirstGuestOpportunity opportunity,
			out KingdomGuestFeastReceipt receipt, out string failure)
		{
			receipt = null; failure = null;
			if (!TryValidate(book, out failure)) return false;
			int index = Index(book, settlementId);
			if (index < 0 || !ExactOpportunity(book.Rows[index], opportunity))
				return Fail("exact guest-feast opportunity is absent", out failure);
			KingdomGuestFeastReceipt row = book.Rows[index];
			if (row.Phase != KingdomGuestFeastPhase.AwaitingGuestChoice)
			{
				receipt = Copy(row);
				return ExactGuestReference(row, opportunity)
					|| Fail("guest decision replay differs from frozen owner evidence", out failure);
			}
			if (opportunity.ChoiceState == KingdomGrowthFirstGuestChoiceState.AwaitingChoice
				|| opportunity.ChoiceState == KingdomGrowthFirstGuestChoiceState.Deferred)
			{
				receipt = Copy(row); return true;
			}
			if (expectedRevision != book.Revision || book.Revision == long.MaxValue)
				return Fail("guest decision CAS refused", out failure);
			KingdomGuestFeastBook next = Clone(book); row = next.Rows[index];
			row.GuestDecisionReceiptId = opportunity.DecisionReceiptId;
			row.GuestDecisionTick = opportunity.DecisionTick;
			row.Phase = KingdomGuestFeastPhase.AwaitingGuestResult;
			next.Revision++;
			if (!TryValidate(next, out failure)) return false;
			Replace(book, next); receipt = Copy(row); return true;
		}

		public static bool TryObserveLocus(KingdomGuestFeastBook book, long expectedRevision,
			string settlementId, KingdomGuestFeastLocusReceipt locus, out string failure)
		{
			failure = null;
			if (!TryValidate(book, out failure) || !ValidLocus(locus)) return false;
			int index = Index(book, settlementId);
			if (index < 0) return Fail("guest-feast coordination is absent", out failure);
			KingdomGuestFeastReceipt row = book.Rows[index];
			if (row.LocusProjectionId != null)
				return ExactLocus(row, locus)
					|| Fail("locus replay differs from frozen projection", out failure);
			if (row.Phase != KingdomGuestFeastPhase.AwaitingLocus
				|| locus.SettlementId != settlementId || locus.RealmId != book.RealmId
				|| locus.ObservedTick <= row.PracticeDecisionTick)
				return Fail("locus must follow the joined guest and later practice", out failure);
			if (expectedRevision != book.Revision || book.Revision == long.MaxValue)
				return Fail("guest-feast locus CAS refused", out failure);
			KingdomGuestFeastBook next = Clone(book); row = next.Rows[index];
			SetLocus(row, locus); row.Phase = KingdomGuestFeastPhase.Cycling;
			next.Revision++;
			if (!TryValidate(next, out failure)) return false;
			Replace(book, next); return true;
		}

		public static bool TryLoseLocus(KingdomGuestFeastBook book, long expectedRevision,
			string settlementId, KingdomGuestFeastLocusReceipt locus, out string failure)
		{
			failure = null;
			if (!TryValidate(book, out failure) || !ValidLocus(locus)) return false;
			int index = Index(book, settlementId);
			if (index < 0 || !ExactLocus(book.Rows[index], locus))
				return Fail("exact locus projection is absent", out failure);
			KingdomGuestFeastReceipt standing = book.Rows[index];
			if (standing.Phase != KingdomGuestFeastPhase.Cycling
				&& standing.Phase != KingdomGuestFeastPhase.Exhausted)
				return Fail("only a live locus projection can be recovered", out failure);
			if (expectedRevision != book.Revision || book.Revision == long.MaxValue)
				return Fail("guest-feast locus-loss CAS refused", out failure);
			KingdomGuestFeastBook next = Clone(book); ClearLocus(next.Rows[index]);
			next.Rows[index].AwayArmed = false; next.Rows[index].HomeCycles = 0;
			next.Rows[index].Phase = KingdomGuestFeastPhase.AwaitingLocus; next.Revision++;
			if (!TryValidate(next, out failure)) return false;
			Replace(book, next); return true;
		}

		public static bool TryObservePractice(KingdomGuestFeastBook book,
			long expectedRevision, string settlementId, KingdomFirstFeastReceipt practice,
			out KingdomGuestFeastReceipt receipt, out string failure)
		{
			receipt = null; failure = null;
			if (!TryValidate(book, out failure) || !KingdomFirstFeastRules.Valid(practice)
				|| practice.SettlementId != settlementId)
				return Fail(failure ?? "guest-feast practice evidence is invalid", out failure);
			int index = Index(book, settlementId);
			if (index < 0) return Fail("guest-feast coordination is absent", out failure);
			KingdomGuestFeastReceipt standing = book.Rows[index];
			if (practice.Phase == KingdomFirstFeastPhase.Offered)
			{
				receipt = Copy(standing); return true;
			}
			if (standing.DeedId != null)
			{
				receipt = Copy(standing);
				return PracticeReferenceMatches(standing, practice)
					&& practice.GuestTerminalReceiptId == standing.GrowthTerminalReceiptId
					&& practice.GuestTerminalDigest == TerminalDigest(standing)
					&& practice.GuestTerminalTick == standing.GuestTerminalTick
					&& practice.DeedTick > standing.GuestTerminalTick
					|| Fail("practice replay differs from frozen owner evidence", out failure);
			}
			if (standing.Phase != KingdomGuestFeastPhase.AwaitingPractice
				|| standing.GuestResult != KingdomGrowthArrivalDisposition.Joined
				|| standing.GrowthTerminalReceiptId == null
				|| practice.GuestTerminalReceiptId != standing.GrowthTerminalReceiptId
				|| practice.GuestTerminalDigest != TerminalDigest(standing)
				|| practice.GuestTerminalTick != standing.GuestTerminalTick
				|| practice.DeedTick <= standing.GuestTerminalTick)
				return Fail("practice must follow the exact joined guest terminal", out failure);
			if (expectedRevision != book.Revision || book.Revision == long.MaxValue)
				return Fail("guest-feast practice CAS refused", out failure);
			KingdomGuestFeastBook next = Clone(book); KingdomGuestFeastReceipt row = next.Rows[index];
			row.DeedId = practice.DeedId; row.PracticeDecisionTick = practice.DecidedTick;
			row.PracticeOutcome = practice.Phase;
			if (KingdomFirstFeastRules.IsAffirmative(practice)) row.PracticeId = practice.PracticeId;
			row.Phase = Resolve(row);
			next.Revision++;
			if (!TryValidate(next, out failure)) return false;
			Replace(book, next); receipt = Copy(row); return true;
		}

		public static bool TryObserveGuestTerminal(KingdomGuestFeastBook book,
			long expectedRevision, string settlementId,
			KingdomGrowthFirstGuestTerminalReceipt terminal,
			out KingdomGuestFeastReceipt receipt, out string failure)
		{
			receipt = null; failure = null;
			if (!TryValidate(book, out failure) || !ValidTerminal(settlementId, terminal))
				return Fail(failure ?? "Growth terminal receipt is invalid", out failure);
			int index = Index(book, settlementId);
			if (index < 0 || !ExactOpportunity(book.Rows[index], terminal.Opportunity))
				return Fail("presented guest-feast coordination is absent", out failure);
			KingdomGuestFeastReceipt standing = book.Rows[index];
			if (standing.GrowthTerminalReceiptId != null)
			{
				receipt = Copy(standing);
				return ExactTerminal(standing, terminal)
					|| Fail("Growth terminal replay differs from frozen evidence", out failure);
			}
			if (expectedRevision != book.Revision || book.Revision == long.MaxValue)
				return Fail("Growth terminal CAS refused", out failure);
			KingdomGuestFeastBook next = Clone(book); KingdomGuestFeastReceipt row = next.Rows[index];
			row.GuestDecisionReceiptId = terminal.Opportunity.DecisionReceiptId;
			row.GuestDecisionTick = terminal.Opportunity.DecisionTick;
			row.GrowthTerminalReceiptId = terminal.ReceiptId;
			row.GuestCandidateId = terminal.CandidateId;
			row.GuestObjectId = terminal.CandidateObjectId;
			row.GuestArrivalOperationId = terminal.ArrivalOperationId;
			row.GuestArrivalOutboxEventId = terminal.ArrivalOutboxEventId;
			row.GuestName = terminal.PersonName; row.GuestOrigin = terminal.PersonOrigin;
			row.GuestCreed = terminal.PersonCreed; row.GuestResidentId = terminal.ResidentId;
			row.GuestResult = terminal.Result; row.GuestTerminalTick = terminal.TerminalTick;
			row.Phase = Resolve(row); next.Revision++;
			if (!TryValidate(next, out failure)) return false;
			Replace(book, next); receipt = Copy(row); return true;
		}

		public static bool TryObserveZoneCycle(KingdomGuestFeastBook book,
			long expectedRevision, string settlementId, bool atHome, bool storyEnabled,
			out bool changed, out string failure)
		{
			changed = false; failure = null;
			if (!TryValidate(book, out failure)) return false;
			int index = Index(book, settlementId);
			if (index < 0 || book.Rows[index].Phase != KingdomGuestFeastPhase.Cycling) return true;
			KingdomGuestFeastReceipt standing = book.Rows[index];
			bool arm = storyEnabled && !atHome && !standing.AwayArmed;
			bool disarm = !storyEnabled && standing.AwayArmed;
			bool returnHome = storyEnabled && atHome && standing.AwayArmed;
			if (!arm && !disarm && !returnHome) return true;
			if (expectedRevision != book.Revision || book.Revision == long.MaxValue)
				return Fail("guest-feast cycle CAS refused", out failure);
			KingdomGuestFeastBook next = Clone(book); KingdomGuestFeastReceipt row = next.Rows[index];
			if (arm) row.AwayArmed = true;
			else
			{
				row.AwayArmed = false;
				if (returnHome && ++row.HomeCycles == 3)
					row.Phase = KingdomGuestFeastPhase.Exhausted;
			}
			next.Revision++;
			if (!TryValidate(next, out failure)) return false;
			Replace(book, next); changed = true; return true;
		}

		private static bool PracticeReferenceMatches(KingdomGuestFeastReceipt row,
			KingdomFirstFeastReceipt practice)
		{
			return row.DeedId == practice.DeedId && row.PracticeDecisionTick == practice.DecidedTick
				&& row.PracticeOutcome == practice.Phase
				&& (row.PracticeId == practice.PracticeId || row.PracticeId == null
					&& (practice.Phase == KingdomFirstFeastPhase.Refused
						|| practice.Phase == KingdomFirstFeastPhase.Archived));
		}

		private static KingdomGuestFeastPhase Resolve(KingdomGuestFeastReceipt row)
		{
			if (row.GrowthTerminalReceiptId == null) return row.GuestDecisionReceiptId == null
				? KingdomGuestFeastPhase.AwaitingGuestChoice
				: KingdomGuestFeastPhase.AwaitingGuestResult;
			if (row.GuestResult == KingdomGrowthArrivalDisposition.Declined)
				return KingdomGuestFeastPhase.GuestDeclined;
			if (row.GuestResult == KingdomGrowthArrivalDisposition.NoAcceptableHome)
				return KingdomGuestFeastPhase.GuestCouldNotJoin;
			if (row.GuestResult == KingdomGrowthArrivalDisposition.Departed)
				return KingdomGuestFeastPhase.GuestDeparted;
			if (row.DeedId == null) return KingdomGuestFeastPhase.AwaitingPractice;
			if (row.PracticeOutcome == KingdomFirstFeastPhase.Refused)
				return KingdomGuestFeastPhase.PracticeRefused;
			if (row.PracticeOutcome == KingdomFirstFeastPhase.Archived)
				return KingdomGuestFeastPhase.PracticeArchived;
			if (row.LocusProjectionId == null) return KingdomGuestFeastPhase.AwaitingLocus;
			return row.HomeCycles == 3 ? KingdomGuestFeastPhase.Exhausted
				: KingdomGuestFeastPhase.Cycling;
		}

		private static bool ValidTerminal(string settlementId,
			KingdomGrowthFirstGuestTerminalReceipt t)
		{
			if (t == null || t.Version != KingdomGrowthFirstGuestTerminalReceipt.CurrentVersion
				|| t.SettlementId != settlementId || !ValidOpportunity(settlementId, t.Opportunity)
				|| t.Opportunity.ChoiceState != KingdomGrowthFirstGuestChoiceState.Admitted
					&& t.Opportunity.ChoiceState != KingdomGrowthFirstGuestChoiceState.Declined
				|| !Text(t.CandidateId) || !Text(t.ArrivalOperationId)
				|| !Text(t.ArrivalOutboxEventId) || !Text(t.PersonName) || !Text(t.PersonOrigin)
				|| !Text(t.PersonCreed) || t.TerminalTick < t.Opportunity.DecisionTick
				|| t.ReceiptId != KingdomGrowthFirstGuestIdentityRules.TerminalReceiptId(
					t.CandidateId, t.Opportunity.DecisionReceiptId, t.ArrivalOperationId,
					t.Result, t.TerminalTick)) return false;
			bool physical = t.Opportunity.RulesVersion == 2;
			KingdomGrowthFirstGuestTerminalState state = t.Opportunity.GuestTerminalState;
			if (t.Result == KingdomGrowthArrivalDisposition.Joined)
				return t.Opportunity.ChoiceState == KingdomGrowthFirstGuestChoiceState.Admitted
					&& t.ResidentId > 0 && Text(t.CandidateObjectId)
					&& (!physical || state == KingdomGrowthFirstGuestTerminalState.Citizen);
			if (t.Result == KingdomGrowthArrivalDisposition.NoAcceptableHome)
				return t.Opportunity.ChoiceState == KingdomGrowthFirstGuestChoiceState.Admitted
					&& t.ResidentId == 0 && Text(t.CandidateObjectId)
					&& (!physical || state == KingdomGrowthFirstGuestTerminalState.CouldNotJoin);
			if (t.Result == KingdomGrowthArrivalDisposition.Departed)
				return physical && t.Opportunity.ChoiceState ==
					KingdomGrowthFirstGuestChoiceState.Admitted && t.ResidentId == 0
					&& Text(t.CandidateObjectId) && (state ==
						KingdomGrowthFirstGuestTerminalState.Departed
						|| state == KingdomGrowthFirstGuestTerminalState.Died);
			return t.Result == KingdomGrowthArrivalDisposition.Declined
				&& t.Opportunity.ChoiceState == KingdomGrowthFirstGuestChoiceState.Declined
				&& t.ResidentId == 0 && t.CandidateObjectId == null
				&& state == KingdomGrowthFirstGuestTerminalState.None;
		}
	}
}
