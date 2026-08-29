namespace ThousandAndFirst
{
	public static partial class KingdomGuestFeastRules
	{
		public static bool TryAttachCuratorPointer(KingdomGuestFeastBook book,
			long expectedRevision, string settlementId, KingdomCuriosityBook curiosity,
			string sourceId, bool exactForeignNote, out string failure)
		{
			failure = null;
			if (!TryValidate(book, out failure) || !KingdomCuriosityRules.ValidBook(curiosity)
				|| !exactForeignNote) return Fail(failure ?? "curator pointer is invalid", out failure);
			KingdomCuriosityReceipt pointer = null;
			for (int i = 0; i < curiosity.Rows.Count; i++)
				if (curiosity.Rows[i].SourceId == sourceId) pointer = curiosity.Rows[i];
			if (pointer == null || pointer.State != KingdomCuriosityState.Available
				&& pointer.State != KingdomCuriosityState.Viewed)
				return Fail("exact curator pointer is unavailable", out failure);
			return TryAttachPointer(book, expectedRevision, settlementId,
				KingdomGuestFeastPointerKind.Curator, pointer.SourceId, pointer.NoteId,
				pointer.State == KingdomCuriosityState.Viewed ? pointer.ClosedTick
					: pointer.PreparedTick, pointer.SettlementId, out failure);
		}

		public static bool TryAttachCivicLeadPointer(KingdomGuestFeastBook book,
			long expectedRevision, string settlementId, KingdomCivicLeadBook leads,
			string sourceId, bool exactJournalProjection, out string failure)
		{
			failure = null;
			if (!TryValidate(book, out failure) || !KingdomCivicLeadRules.ValidBook(leads)
				|| !exactJournalProjection)
				return Fail(failure ?? "civic-lead pointer is invalid", out failure);
			KingdomCivicLeadReceipt pointer = null;
			for (int i = 0; i < leads.Rows.Count; i++)
				if (leads.Rows[i].SourceId == sourceId) pointer = leads.Rows[i];
			if (pointer == null || pointer.Phase != KingdomCivicLeadPhase.Projected)
				return Fail("exact civic lead is not projected", out failure);
			return TryAttachPointer(book, expectedRevision, settlementId,
				KingdomGuestFeastPointerKind.CivicLead, pointer.SourceId, pointer.LeadId,
				pointer.CompletedTick, pointer.SettlementId, out failure);
		}

		private static bool TryAttachPointer(KingdomGuestFeastBook book,
			long expectedRevision, string settlementId, KingdomGuestFeastPointerKind kind,
			string sourceId, string targetId, long tick, string sourceSettlement,
			out string failure)
		{
			failure = null;
			int index = Index(book, settlementId);
			if (index < 0) return Fail("guest-feast coordination is absent", out failure);
			KingdomGuestFeastReceipt standing = book.Rows[index];
			if (standing.PointerKind != KingdomGuestFeastPointerKind.None)
				return standing.PointerKind == kind && standing.PointerSourceId == sourceId
					&& standing.PointerTargetId == targetId
					|| Fail("guest-feast already references another pointer", out failure);
			if (standing.Phase != KingdomGuestFeastPhase.Cycling
				|| sourceId != standing.PracticeId || sourceSettlement != standing.SettlementId
				|| !SemanticId(sourceId) || !Text(targetId)
				|| tick < standing.PracticeDecisionTick)
				return Fail("pointer does not derive from the exact guest feast", out failure);
			if (expectedRevision != book.Revision || book.Revision == long.MaxValue)
				return Fail("guest-feast pointer CAS refused", out failure);
			KingdomGuestFeastBook next = Clone(book); KingdomGuestFeastReceipt row = next.Rows[index];
			row.PointerKind = kind; row.PointerSourceId = sourceId;
			row.PointerTargetId = targetId; row.PointerTick = tick; next.Revision++;
			if (!TryValidate(next, out failure)) return false;
			Replace(book, next); return true;
		}
	}
}
