namespace ThousandAndFirst
{
	/// <summary>
	/// Terminal cleanup of the Curator audience a curation reserved.
	/// <para>
	/// This half is separated because it is the only part of the family that reaches the
	/// experience ledger. Everything above it decides; this decides nothing and only releases
	/// what a decision already made terminal, which is why a missing lease is success rather than
	/// an error &mdash; a save cut between the release and the write leaves exactly that shape,
	/// and recovery must be able to run twice.
	/// </para>
	/// </summary>
	public static partial class KingdomCuriosityRules
	{
		public static bool TryGetTerminalAttentionRelease(KingdomCuriosityBook book,
			string sourceId, out KingdomCuratorAttentionRelease release, out string failure)
		{
			release = default(KingdomCuratorAttentionRelease); failure = null;
			if (!ValidBook(book) || !ValidId(sourceId))
				return Fail("curiosity terminal authority is invalid", out failure);
			for (int i = 0; i < book.Rows.Count; i++)
			{
				KingdomCuriosityReceipt row = book.Rows[i];
				if (row.SourceId != sourceId) continue;
				if (row.State != KingdomCuriosityState.Viewed
					&& row.State != KingdomCuriosityState.Declined
					&& row.State != KingdomCuriosityState.Invalidated)
					return Fail("curiosity attention is not terminal", out failure);
				string id = AttentionReservationId(row.SourceId);
				if (id == null) return Fail("curiosity attention identity is invalid", out failure);
				release = new KingdomCuratorAttentionRelease(id, row.SourceId,
					row.SettlementId, row.PreparedTick);
				return true;
			}
			return Fail("curiosity source is absent", out failure);
		}

		public static bool TryReleaseTerminalAttention(KingdomExperienceLedger ledger,
			KingdomCuriosityBook book, string sourceId, out KingdomExperienceCapacityFault fault,
			out string failure)
		{
			fault = KingdomExperienceCapacityFault.InvalidRequest;
			if (!TryGetTerminalAttentionRelease(book, sourceId,
				out KingdomCuratorAttentionRelease release, out failure)) return false;
			return TryReleaseTerminalAttention(ledger, release, out fault, out failure);
		}

		internal static bool TryReleaseTerminalAttention(KingdomExperienceLedger ledger,
			KingdomCuratorAttentionRelease release, out KingdomExperienceCapacityFault fault,
			out string failure)
		{
			fault = KingdomExperienceCapacityFault.InvalidRequest; failure = null;
			if (ledger == null || release.ReservationId == null || release.SourceId == null
				|| release.SettlementId == null || release.EarliestCauseTick < 0L)
				return Fail("Curator attention release proof is invalid", out failure);
			if (!KingdomExperienceRules.TryReadAudienceLease(ledger, release.ReservationId,
				out KingdomExperienceAudienceReceipt lease, out KingdomExperienceLeaseState _,
				out failure))
			{
				fault = KingdomExperienceCapacityFault.InvalidLedger; return false;
			}
			if (lease == null) { fault = KingdomExperienceCapacityFault.None; return true; }
			if (lease.ReservationId != release.ReservationId
				|| lease.RealmId != ledger.RealmId
				|| lease.SettlementId != release.SettlementId
				|| lease.SourceId != release.SourceId
				|| lease.Lane != KingdomExperienceLane.Curator
				|| lease.OptionKind != KingdomExperienceOptionKind.CivicKnowledge
				|| lease.CauseTick != lease.ReservedTick
				|| lease.CauseTick < release.EarliestCauseTick || lease.EnableEpoch < 1L)
			{
				fault = KingdomExperienceCapacityFault.OwnershipMismatch;
				return Fail("Curator attention lease differs from terminal source proof",
					out failure);
			}
			return KingdomExperienceRules.TryReleaseAudience(ledger, ledger.Revision,
				release.ReservationId, release.SourceId, out fault, out failure);
		}
	}
}
