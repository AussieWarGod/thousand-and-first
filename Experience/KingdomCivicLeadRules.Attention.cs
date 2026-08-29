namespace ThousandAndFirst
{
	/// <summary>
	/// Terminal cleanup for a civic lead's Curator audience.
	/// <para>
	/// A lead becomes terminal when it is projected or invalidated &mdash; not when it is
	/// prepared, because a prepared lead is still owed a journal note and the founder's attention
	/// is still legitimately reserved against it. Both books derive the same reservation identity
	/// from the same source id, so the release proof is
	/// <see cref="KingdomCuriosityRules.AttentionReservationId"/> in both families and there is
	/// exactly one shape of evidence to check.
	/// </para>
	/// </summary>
	public static partial class KingdomCivicLeadRules
	{
		public static bool TryGetTerminalAttentionRelease(KingdomCivicLeadBook book,
			string sourceId, out KingdomCuratorAttentionRelease release, out string failure)
		{
			release = default(KingdomCuratorAttentionRelease); failure = null;
			if (!ValidBook(book) || !KingdomCuriosityRules.ValidId(sourceId))
				return Fail("civic lead terminal authority is invalid", out failure);
			for (int i = 0; i < book.Rows.Count; i++)
			{
				KingdomCivicLeadReceipt row = book.Rows[i];
				if (row.SourceId != sourceId) continue;
				if (row.Phase != KingdomCivicLeadPhase.Projected
					&& row.Phase != KingdomCivicLeadPhase.Invalidated)
					return Fail("civic lead attention is not terminal", out failure);
				string id = KingdomCuriosityRules.AttentionReservationId(row.SourceId);
				if (id == null) return Fail("civic lead attention identity is invalid", out failure);
				release = new KingdomCuratorAttentionRelease(id, row.SourceId,
					row.SettlementId, row.CompletedTick);
				return true;
			}
			return Fail("civic lead source is absent", out failure);
		}

		public static bool TryReleaseTerminalAttention(KingdomExperienceLedger ledger,
			KingdomCivicLeadBook book, string sourceId, out KingdomExperienceCapacityFault fault,
			out string failure)
		{
			fault = KingdomExperienceCapacityFault.InvalidRequest;
			if (!TryGetTerminalAttentionRelease(book, sourceId,
				out KingdomCuratorAttentionRelease release, out failure)) return false;
			return KingdomCuriosityRules.TryReleaseTerminalAttention(ledger, release,
				out fault, out failure);
		}
	}
}
