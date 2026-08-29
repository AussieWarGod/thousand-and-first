using System;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomGuestFeastRules
	{
		public const int MaxRows = KingdomExperienceRules.MaxSettlements;
		public const int MaxStringBytes = 256;
		public const int MaxRealmIdBytes = 77;
		public const int MaxSettlementIdBytes = 82;
		public const int MaxOpportunityIdBytes = 99;
		public const int MaxCauseIdBytes = 93;
		public const int MaxGuestDecisionIdBytes = 95;
		public const int MaxTerminalReceiptIdBytes = 96;
		public const int MaxBodyReservationIdBytes = 99;
		public const int MaxDeedIdBytes = 96;
		public const int MaxPracticeIdBytes = 100;
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
		public static bool TryValidate(KingdomGuestFeastBook book, out string failure)
		{
			failure = null;
			if (book == null || book.SchemaState != KingdomExperienceSchemaState.Compatible
				|| book.SchemaFault != null || book.Revision < 0L || book.Rows == null
				|| book.Rows.Count > MaxRows || book.OpaqueWireVersion != 0
				|| book.OpaqueFuturePayload != null || book.OpaqueEnvelope != null)
				return Fail("guest-feast header is invalid", out failure);
			if (!book.IdentityBound)
			{
				if (book.RealmId != null || book.Revision != 0L || book.Rows.Count != 0)
					return Fail("unbound guest-feast book carries authority", out failure);
			}
			else if (!RealmId(book.RealmId))
				return Fail("guest-feast realm is invalid", out failure);
			string prior = null;
			for (int i = 0; i < book.Rows.Count; i++)
			{
				KingdomGuestFeastReceipt row = book.Rows[i];
				if (!ValidRow(row) || prior != null
					&& string.CompareOrdinal(prior, row.SettlementId) >= 0)
					return Fail("guest-feast row is invalid or unsorted", out failure);
				prior = row.SettlementId;
			}
			return true;
		}
		public static bool TryBindEmptyIdentity(KingdomGuestFeastBook book, string realmId,
			out string failure)
		{
			failure = null;
			if (!TryValidate(book, out failure) || !RealmId(realmId))
				return Fail(failure ?? "guest-feast realm is invalid", out failure);
			if (book.IdentityBound) return book.RealmId == realmId
				|| Fail("guest-feast realm mismatch", out failure);
			book.IdentityBound = true; book.RealmId = realmId; book.Revision = 1L;
			return true;
		}
		public static bool TryFind(KingdomGuestFeastBook book, string settlementId,
			out KingdomGuestFeastReceipt receipt)
		{
			receipt = null;
			if (!TryValidate(book, out string _)) return false;
			int index = Index(book, settlementId);
			if (index < 0) return true;
			receipt = Copy(book.Rows[index]); return true;
		}
		public static bool IsTerminal(KingdomGuestFeastPhase phase)
		{
			return phase == KingdomGuestFeastPhase.GuestDeclined
				|| phase == KingdomGuestFeastPhase.GuestCouldNotJoin
				|| phase == KingdomGuestFeastPhase.GuestDeparted
				|| phase == KingdomGuestFeastPhase.PracticeRefused
				|| phase == KingdomGuestFeastPhase.PracticeArchived
				|| phase == KingdomGuestFeastPhase.OutOfOrder
				|| phase == KingdomGuestFeastPhase.Exhausted;
		}
		public static bool TryTrace(KingdomGuestFeastBook book, string settlementId,
			KingdomFirstFeastReceipt practice, out string trace)
		{
			trace = null;
			if (!TryFind(book, settlementId, out KingdomGuestFeastReceipt row) || row == null
				|| row.Phase != KingdomGuestFeastPhase.Cycling
					&& row.Phase != KingdomGuestFeastPhase.Exhausted
				|| !ExactPractice(row, practice)) return false;
			trace = KingdomFirstFeastRules.RenderOutcome(practice)
				+ " This record coordinates that independent founding practice with "
				+ row.GuestName + "'s completed arrival; it grants no meal, recipe, or boon.";
			return true;
		}
		internal static bool ExactOpportunity(KingdomGuestFeastReceipt row,
			KingdomGrowthFirstGuestOpportunity opportunity)
		{
			return row != null && ValidOpportunity(row.SettlementId, opportunity)
				&& row.OpportunityId == opportunity.OpportunityId
				&& row.CauseId == opportunity.CauseId && row.CauseTick == opportunity.CauseTick;
		}

		internal static bool ExactGuestReference(KingdomGuestFeastReceipt row,
			KingdomGrowthFirstGuestOpportunity opportunity)
		{
			if (!ExactOpportunity(row, opportunity)
				|| row.GuestDecisionReceiptId != opportunity.DecisionReceiptId
				|| row.GuestDecisionTick != opportunity.DecisionTick) return false;
			return opportunity.ChoiceState == KingdomGrowthFirstGuestChoiceState.AwaitingChoice
				|| opportunity.ChoiceState == KingdomGrowthFirstGuestChoiceState.Deferred
				|| opportunity.ChoiceState == KingdomGrowthFirstGuestChoiceState.Admitted
				|| opportunity.ChoiceState == KingdomGrowthFirstGuestChoiceState.Declined;
		}

		internal static bool ExactTerminal(KingdomGuestFeastReceipt row,
			KingdomGrowthFirstGuestTerminalReceipt terminal)
		{
			return terminal != null && ExactOpportunity(row, terminal.Opportunity)
				&& ExactGuestReference(row, terminal.Opportunity)
				&& row.GrowthTerminalReceiptId == terminal.ReceiptId
				&& row.GuestCandidateId == terminal.CandidateId
				&& row.GuestObjectId == terminal.CandidateObjectId
				&& row.GuestArrivalOperationId == terminal.ArrivalOperationId
				&& row.GuestArrivalOutboxEventId == terminal.ArrivalOutboxEventId
				&& row.GuestName == terminal.PersonName && row.GuestOrigin == terminal.PersonOrigin
				&& row.GuestCreed == terminal.PersonCreed && row.GuestResidentId == terminal.ResidentId
				&& row.GuestResult == terminal.Result && row.GuestTerminalTick == terminal.TerminalTick;
		}

		internal static bool ExactPractice(KingdomGuestFeastReceipt row,
			KingdomFirstFeastReceipt practice)
		{
			return row != null && KingdomFirstFeastRules.IsAffirmative(practice)
				&& row.SettlementId == practice.SettlementId && row.DeedId == practice.DeedId
				&& practice.GuestTerminalReceiptId == row.GrowthTerminalReceiptId
				&& practice.GuestTerminalDigest == TerminalDigest(row)
				&& practice.GuestTerminalTick == row.GuestTerminalTick
				&& practice.DeedTick > row.GuestTerminalTick
				&& row.PracticeId == practice.PracticeId
				&& row.PracticeOutcome == practice.Phase
				&& row.PracticeDecisionTick == practice.DecidedTick;
		}

		private static bool ValidRow(KingdomGuestFeastReceipt row)
		{
			if (row == null || row.Version != KingdomGuestFeastReceipt.CurrentVersion
				|| !Enum.IsDefined(typeof(KingdomGuestFeastPhase), row.Phase)
				|| row.Phase == KingdomGuestFeastPhase.None
				|| !KingdomIdentityRules.IsSettlementId(row.SettlementId)
				|| !GeneratedId(row.OpportunityId,
					"taf:growth-first-guest-opportunity:", MaxOpportunityIdBytes)
				|| !GeneratedId(row.CauseId, "taf:growth-first-guest-cause:",
					MaxCauseIdBytes) || row.CauseTick < 0L
				|| row.HomeCycles < 0 || row.HomeCycles > 3
				|| !Enum.IsDefined(typeof(KingdomGuestFeastPointerKind), row.PointerKind)
				|| !Enum.IsDefined(typeof(KingdomFirstFeastPhase), row.PracticeOutcome)
				|| !PointerShape(row)) return false;
			bool guest = GeneratedId(row.GuestDecisionReceiptId,
				"taf:growth-first-guest-receipt:", MaxGuestDecisionIdBytes)
				&& row.GuestDecisionTick >= row.CauseTick;
			bool noGuest = row.GuestDecisionReceiptId == null && row.GuestDecisionTick == -1L;
			bool affirmative = row.PracticeOutcome == KingdomFirstFeastPhase.Adopted
				|| row.PracticeOutcome == KingdomFirstFeastPhase.Adapted;
			bool practice = FeastId(row.DeedId, KingdomFirstFeastRules.DeedPrefix,
				MaxDeedIdBytes) && row.PracticeDecisionTick >= 0L && (affirmative
					? FeastId(row.PracticeId, KingdomFirstFeastRules.PracticePrefix,
						MaxPracticeIdBytes) : row.PracticeId == null
						&& (row.PracticeOutcome == KingdomFirstFeastPhase.Refused
							|| row.PracticeOutcome == KingdomFirstFeastPhase.Archived));
			bool noPractice = row.DeedId == null && row.PracticeId == null
				&& row.PracticeDecisionTick == -1L
				&& row.PracticeOutcome == KingdomFirstFeastPhase.None;
			bool terminal = TerminalShape(row), noTerminal = NoTerminal(row);
			bool locus = LocusShape(row), noLocus = NoLocus(row);
			if ((!practice && !noPractice) || (!terminal && !noTerminal)) return false;
			switch (row.Phase)
			{
			case KingdomGuestFeastPhase.AwaitingGuestChoice:
				return noGuest && noTerminal && noLocus && row.HomeCycles == 0 && !row.AwayArmed;
			case KingdomGuestFeastPhase.AwaitingGuestResult:
				return guest && noTerminal && noLocus && row.HomeCycles == 0;
			case KingdomGuestFeastPhase.AwaitingPractice:
				return guest && terminal && row.GuestResult == KingdomGrowthArrivalDisposition.Joined
					&& noPractice && noLocus && row.HomeCycles == 0 && !row.AwayArmed;
			case KingdomGuestFeastPhase.AwaitingLocus:
				return guest && terminal && row.GuestResult == KingdomGrowthArrivalDisposition.Joined
					&& affirmative && practice && noLocus && row.HomeCycles == 0;
			case KingdomGuestFeastPhase.Cycling:
				return guest && terminal && row.GuestResult == KingdomGrowthArrivalDisposition.Joined
					&& affirmative && practice && locus && row.HomeCycles < 3;
			case KingdomGuestFeastPhase.Exhausted:
				return guest && terminal && row.GuestResult == KingdomGrowthArrivalDisposition.Joined
					&& affirmative && practice && locus && row.HomeCycles == 3 && !row.AwayArmed;
			case KingdomGuestFeastPhase.GuestDeclined:
				return guest && terminal && row.GuestResult == KingdomGrowthArrivalDisposition.Declined
					&& noLocus && row.HomeCycles == 0 && !row.AwayArmed;
			case KingdomGuestFeastPhase.GuestCouldNotJoin:
				return guest && terminal && row.GuestResult ==
					KingdomGrowthArrivalDisposition.NoAcceptableHome
					&& noLocus && row.HomeCycles == 0 && !row.AwayArmed;
			case KingdomGuestFeastPhase.GuestDeparted:
				return guest && terminal && row.GuestResult == KingdomGrowthArrivalDisposition.Departed
					&& noPractice && noLocus && row.HomeCycles == 0 && !row.AwayArmed;
			case KingdomGuestFeastPhase.PracticeRefused:
				return guest && terminal && row.GuestResult == KingdomGrowthArrivalDisposition.Joined
					&& practice && noLocus && row.PracticeOutcome == KingdomFirstFeastPhase.Refused;
			default:
				return row.Phase == KingdomGuestFeastPhase.PracticeArchived && guest && terminal
					&& row.GuestResult == KingdomGrowthArrivalDisposition.Joined && practice && noLocus
					&& row.PracticeOutcome == KingdomFirstFeastPhase.Archived;
			}
		}

		private static bool TerminalShape(KingdomGuestFeastReceipt r)
		{
			bool joined = r.GuestResult == KingdomGrowthArrivalDisposition.Joined;
			return GeneratedId(r.GrowthTerminalReceiptId, "taf:growth-first-guest-terminal:",
				MaxTerminalReceiptIdBytes) && Text(r.GuestCandidateId)
				&& Text(r.GuestArrivalOperationId) && Text(r.GuestArrivalOutboxEventId)
				&& Text(r.GuestName) && Text(r.GuestOrigin) && Text(r.GuestCreed)
				&& r.GuestTerminalTick >= r.GuestDecisionTick
				&& (joined ? r.GuestResidentId > 0 && Text(r.GuestObjectId)
					: r.GuestResidentId == 0 && (r.GuestResult == KingdomGrowthArrivalDisposition.Declined
						? r.GuestObjectId == null : (r.GuestResult ==
							KingdomGrowthArrivalDisposition.NoAcceptableHome || r.GuestResult ==
							KingdomGrowthArrivalDisposition.Departed) && Text(r.GuestObjectId)))
				&& r.GrowthTerminalReceiptId == KingdomGrowthFirstGuestIdentityRules.TerminalReceiptId(
					r.GuestCandidateId, r.GuestDecisionReceiptId, r.GuestArrivalOperationId,
					r.GuestResult, r.GuestTerminalTick);
		}

		private static bool NoTerminal(KingdomGuestFeastReceipt r) =>
			r.GrowthTerminalReceiptId == null && r.GuestCandidateId == null
			&& r.GuestObjectId == null && r.GuestArrivalOperationId == null
			&& r.GuestArrivalOutboxEventId == null && r.GuestName == null
			&& r.GuestOrigin == null && r.GuestCreed == null && r.GuestResidentId == 0
			&& r.GuestResult == KingdomGrowthArrivalDisposition.None
			&& r.GuestTerminalTick == -1L;

		private static bool PointerShape(KingdomGuestFeastReceipt row)
		{
			if (row.PointerKind == KingdomGuestFeastPointerKind.None)
				return row.PointerSourceId == null && row.PointerTargetId == null
					&& row.PointerTick == -1L;
			return row.PointerSourceId == row.PracticeId
				&& FeastId(row.PointerSourceId, KingdomFirstFeastRules.PracticePrefix,
					MaxPracticeIdBytes) && Text(row.PointerTargetId)
				&& row.PointerTick >= row.PracticeDecisionTick
				&& (row.Phase == KingdomGuestFeastPhase.Cycling
					|| row.Phase == KingdomGuestFeastPhase.Exhausted);
		}

		internal static int Index(KingdomGuestFeastBook book, string settlementId)
		{
			for (int i = 0; i < book.Rows.Count; i++)
				if (book.Rows[i].SettlementId == settlementId) return i;
			return -1;
		}

		internal static bool SemanticId(string value) => KernelSemanticId.IsValid(value);
		private static bool GeneratedId(string value, string prefix, int exactBytes)
		{
			if (!SemanticId(value) || value.Length != exactBytes
				|| !value.StartsWith(prefix, StringComparison.Ordinal)) return false;
			for (int i = prefix.Length; i < value.Length; i++)
			{
				char c = value[i];
				if (!((c >= '0' && c <= '9') || c >= 'a' && c <= 'f')) return false;
			}
			return true;
		}
		private static bool FeastId(string value, string prefix, int exactBytes)
		{
			return GeneratedId(value, prefix, exactBytes);
		}
		private static bool RealmId(string value) => KingdomIdentityRules.IsRealmId(value);
		internal static bool Text(string value)
		{
			try { return value != null && value.Length > 0 && value.Trim() == value
				&& StrictUtf8.GetByteCount(value) <= MaxStringBytes; }
			catch (EncoderFallbackException) { return false; }
		}
		internal static bool Fail(string message, out string failure) { failure = message; return false; }
	}
}
