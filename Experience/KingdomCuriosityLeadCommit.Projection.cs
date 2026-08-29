namespace ThousandAndFirst
{
	/// <summary>Where a civic lead stands in the save, as opposed to in memory.</summary>
	public enum KingdomCivicLeadDurableStanding : byte
	{
		/// <summary>The save has never heard of this lead.</summary>
		Absent = 0,

		/// <summary>Durably prepared: the journal is owed a note.</summary>
		Prepared = 1,

		/// <summary>Durably projected: the note has been made and recorded.</summary>
		Projected = 2
	}

	/// <summary>
	/// The second of the two durable cuts a civic lead needs, and the only place it is made.
	/// <para>
	/// A lead reaches the founder's journal across three separate pieces of state: the record in
	/// the save, the note in the journal, and the book in memory. A crash can land between any two
	/// of them, so the order is fixed and every step is idempotent. The record goes durable first
	/// as <b>Prepared</b>; the note is made; and only then does the record go durable again as
	/// <b>Projected</b>. Nothing here reports success on a row that has only moved in memory.
	/// </para>
	/// <para>
	/// The transition is made on a private decode of the committed section, never on the caller's
	/// book. That is what makes it a lease rather than an assertion: the bytes in the save are
	/// read, the row in them is matched field by field against the receipt, the copy is advanced,
	/// re-encoded, and offered back at the revision it was read at. If anything moved underneath,
	/// the authority refuses the commit and the durable row stays Prepared &mdash; which is
	/// exactly the state a retry knows how to finish, because the note it already made is the same
	/// note it would make again.
	/// </para>
	/// </summary>
	public static partial class KingdomCuriosityLeadCommit
	{
		/// <summary>
		/// Opens the civic-lead section once and reports where this receipt's lead stands in it,
		/// handing back the lease that reading came from.
		/// <para>
		/// The lease is an output because the caller must keep it. A projection reads the record,
		/// writes a note into the founder's journal, and then records that the note was made; if
		/// the section were opened again for that second step, everything decided during the first
		/// reading would have been decided about a save that may since have moved. One lease spans
		/// the whole crossing.
		/// </para>
		/// <para>
		/// Both standings are lawful. Prepared is the ordinary case. Projected means a previous run
		/// already made the note and recorded it, and the only thing left is to confirm the note
		/// still stands; refusing that would turn completed work into a permanent failure.
		/// </para>
		/// </summary>
		public static bool TryReadDurableStanding(IKingdomCivicMemoryAuthority authority,
			long expectedRevision, KingdomCivicLeadReceipt receipt,
			out KingdomCivicMemorySectionLease lease,
			out KingdomCivicLeadDurableStanding standing, out string failure)
		{
			standing = KingdomCivicLeadDurableStanding.Absent;
			lease = null;
			if (authority == null)
				return Fail("there is no civic-memory authority to read", out failure);
			if (!authority.TryReadSection(KingdomCivicMemoryLimits.SectionCivicLeads, out lease,
				out failure)) return false;
			if (lease.ExpectedRevision != expectedRevision)
				return Fail("this lease was read at civic-memory revision " + lease.ExpectedRevision
					+ " and the caller read at " + expectedRevision, out failure);
			if (!TryDecode(lease, out KingdomCivicLeadBook durable, out failure)) return false;
			return TryStandingOf(durable, receipt, out standing, out failure);
		}

		/// <summary>
		/// Records the projection in the save, under the lease the standing was read from, and
		/// returns true only once the authority has taken it.
		/// <para>
		/// This never opens a section. It is given the lease the caller has been holding since
		/// before the journal was touched, decodes that lease's own payload, makes the transition on
		/// that private copy, and offers the result back under the same lease. A lead already
		/// durably projected is a completed commit, not a second one.
		/// </para>
		/// </summary>
		public static bool TryCommitProjectedLead(IKingdomCivicMemoryAuthority authority,
			KingdomCivicMemorySectionLease lease, KingdomCivicLeadReceipt receipt,
			out string failure)
		{
			if (authority == null)
				return Fail("there is no civic-memory authority to commit to", out failure);
			if (lease == null)
				return Fail("there is no civic-lead lease to commit under", out failure);
			if (!TryDecode(lease, out KingdomCivicLeadBook durable, out failure)) return false;
			if (!TryStandingOf(durable, receipt, out KingdomCivicLeadDurableStanding standing,
				out failure)) return false;
			if (standing == KingdomCivicLeadDurableStanding.Projected) { failure = ""; return true; }

			// The transition is made on this private decode of the lease's own bytes, never on any
			// caller's book, and is offered back under the very lease that produced it.
			if (!KingdomCivicLeadRules.TryMarkProjected(durable, durable.Revision,
				receipt.SourceId, receipt.LeadId, receipt.Locator, out failure)) return false;
			if (!KingdomCuriosityLeadCodec.TryEncode(durable, out byte[] bytes, out failure))
				return false;
			return authority.TryCommitSection(lease, bytes, out failure);
		}

		/// <summary>The civic-lead book as this lease's own payload says it stands.</summary>
		private static bool TryDecode(KingdomCivicMemorySectionLease lease,
			out KingdomCivicLeadBook durable, out string failure)
		{
			durable = null;
			if (!lease.Present)
				return Fail("civic memory holds no civic-lead section at all", out failure);
			durable = KingdomCuriosityLeadCodec.DecodeLeads(lease.Payload());
			if (durable.State != KingdomCuriosityBookState.Compatible)
				return Fail("the durable civic-lead section is " + durable.State
					+ " and cannot vouch for anything", out failure);
			failure = "";
			return true;
		}

		private static bool TryStandingOf(KingdomCivicLeadBook durable,
			KingdomCivicLeadReceipt receipt, out KingdomCivicLeadDurableStanding standing,
			out string failure)
		{
			standing = KingdomCivicLeadDurableStanding.Absent;
			if (receipt == null) return Fail("there is no civic lead receipt", out failure);
			for (int i = 0; i < durable.Rows.Count; i++)
			{
				KingdomCivicLeadReceipt row = durable.Rows[i];
				if (row.SourceId != receipt.SourceId) continue;
				if (row.Phase == KingdomCivicLeadPhase.Prepared)
				{
					if (!KingdomCivicLeadRules.TryMatchPreparedRow(durable, durable.Revision,
						receipt, out failure)) return false;
					standing = KingdomCivicLeadDurableStanding.Prepared;
					return true;
				}
				if (row.Phase != KingdomCivicLeadPhase.Projected)
					return Fail("the durable civic lead is " + row.Phase
						+ " and cannot be projected", out failure);
				if (!SameFrozenFields(row, receipt))
					return Fail("the durable civic lead differs from this receipt", out failure);
				standing = KingdomCivicLeadDurableStanding.Projected;
				failure = "";
				return true;
			}
			return Fail("civic memory holds no civic lead for that source", out failure);
		}

		private static bool SameFrozenFields(KingdomCivicLeadReceipt row,
			KingdomCivicLeadReceipt receipt)
		{
			return row.Version == receipt.Version && row.SourceId == receipt.SourceId
				&& row.SourceVersion == receipt.SourceVersion
				&& row.SettlementId == receipt.SettlementId && row.LeadId == receipt.LeadId
				&& row.Locator == receipt.Locator && row.Title == receipt.Title
				&& row.AuthoredReason == receipt.AuthoredReason
				&& row.CompletedTick == receipt.CompletedTick;
		}

		private static bool Fail(string text, out string failure)
		{ failure = text; return false; }
	}
}
