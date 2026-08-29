using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// A civic lead is one journal map note the city earned by finishing a delve, and the durable
	/// record of whether that note has actually reached the journal yet. The record moves ahead of
	/// the journal, never behind it, so a save cut anywhere in between leaves a row that can be
	/// finished rather than a note nobody owns.
	/// </summary>
	public static partial class KingdomCivicLeadRules
	{
		/// <summary>How many map notes this build will let a journal hold before it declines to
		/// add another of its own. Preflight, not a repair: nothing here removes anyone's note.</summary>
		public const int MaxJournalMapNotes = 512;

		/// <summary>The stable prefix and total length of a derived lead identity.</summary>
		public const string LeadIdPrefix = "taf:civic-lead:v1:";
		public const int LeadIdChars = 18 + 64;

		public static bool TryPrepare(KingdomCivicLeadBook book, long expectedRevision,
			KingdomCivicLeadCause cause, int journalCount, bool attentionReserved,
			out KingdomCivicLeadReceipt receipt, out string failure)
		{
			receipt = null; failure = null;
			if (!ValidBook(book) || !ValidCause(cause))
				return Fail("civic lead source or authority is invalid", out failure);
			KingdomCivicLeadReceipt prepared = PreparedReceipt(cause);
			if (prepared == null) return Fail("civic lead identity is invalid", out failure);
			for (int i = 0; i < book.Rows.Count; i++)
				if (book.Rows[i].SourceId == cause.SourceId)
				{
					if (!ExactPreparation(book.Rows[i], prepared))
						return Fail("civic lead source replay differs from its frozen cause",
							out failure);
					receipt = book.Rows[i].Copy(); return true;
				}
			if (expectedRevision != book.Revision || book.Revision == long.MaxValue)
				return Fail("civic lead revision is unavailable", out failure);
			if (!attentionReserved || journalCount < 0 || journalCount >= MaxJournalMapNotes
				|| book.Rows.Count >= KingdomCivicLeadBook.MaxRows)
				return Fail("civic lead capacity is unavailable", out failure);
			book.Rows.Add(prepared.Copy());
			book.Rows.Sort((a, b) => string.CompareOrdinal(a.SourceId, b.SourceId));
			book.Revision++; receipt = prepared.Copy(); return true;
		}

		/// <summary>
		/// Records that the journal now holds this exact note. A row already projected says so
		/// again without spending a revision; a row whose identity or place disagrees with the
		/// caller is refused outright and left exactly as it stands.
		/// </summary>
		public static bool TryMarkProjected(KingdomCivicLeadBook book, long expectedRevision,
			string sourceId, string leadId, string locator, out string failure)
		{
			failure = null;
			if (!ValidBook(book) || expectedRevision != book.Revision)
				return Fail("civic lead authority changed", out failure);
			for (int i = 0; i < book.Rows.Count; i++)
			{
				KingdomCivicLeadReceipt row = book.Rows[i];
				if (row.SourceId != sourceId) continue;
				if (row.LeadId != leadId || row.Locator != locator)
					return Fail("foreign lead projection conflicts", out failure);
				if (row.Phase == KingdomCivicLeadPhase.Projected) return true;
				if (row.Phase != KingdomCivicLeadPhase.Prepared)
					return Fail("civic lead is not projectable", out failure);
				if (book.Revision == long.MaxValue)
					return Fail("civic lead revision is exhausted", out failure);
				KingdomCivicLeadReceipt next = row.Copy(); next.Phase = KingdomCivicLeadPhase.Projected;
				book.Rows[i] = next; book.Revision++; return true;
			}
			return Fail("civic lead source is absent", out failure);
		}

		public static bool TryInvalidate(KingdomCivicLeadBook book, long expectedRevision,
			string sourceId, out string failure)
		{
			failure = null;
			if (!ValidBook(book) || expectedRevision != book.Revision)
				return Fail("civic lead authority changed", out failure);
			for (int i = 0; i < book.Rows.Count; i++)
			{
				KingdomCivicLeadReceipt row = book.Rows[i];
				if (row.SourceId != sourceId) continue;
				if (row.Phase == KingdomCivicLeadPhase.Invalidated) return true;
				if (book.Revision == long.MaxValue)
					return Fail("civic lead revision is exhausted", out failure);
				KingdomCivicLeadReceipt next = row.Copy();
				next.Phase = KingdomCivicLeadPhase.Invalidated;
				book.Rows[i] = next; book.Revision++; return true;
			}
			return Fail("civic lead source is absent", out failure);
		}

		/// <summary>The derived identity for new authorship: canonical locators only.</summary>
		public static string LeadId(string sourceId, string locator)
		{
			if (!KingdomCuriosityRules.ValidId(sourceId)
				|| !KingdomCuriosityRules.TryFullLocator(locator)) return null;
			return Derive(sourceId, locator);
		}

		private static string Derive(string sourceId, string locator)
		{
			using (SHA256 sha = SHA256.Create())
			{
				byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes("taf:civic-lead:v1\n"
					+ sourceId + "\n" + locator));
				StringBuilder b = new StringBuilder(LeadIdPrefix);
				for (int i = 0; i < hash.Length; i++) b.Append(hash[i].ToString("x2"));
				return b.ToString();
			}
		}

		/// <summary>See <see cref="KingdomCuriosityRules.ValidBook"/>; the same gate, and the same
		/// reason a future or quarantined book cannot be edited by anyone here.</summary>
		internal static bool ValidBook(KingdomCivicLeadBook b)
		{
			if (b == null || b.State != KingdomCuriosityBookState.Compatible || b.Fault != null
				|| b.OpaquePayload != null || b.OpaqueVersion != 0
				|| b.Revision < 0 || b.Rows.Count > KingdomCivicLeadBook.MaxRows) return false;
			HashSet<string> sources = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < b.Rows.Count; i++)
				if (!ValidReceipt(b.Rows[i]) || !sources.Add(b.Rows[i].SourceId)
					|| i > 0 && string.CompareOrdinal(b.Rows[i - 1].SourceId,
						b.Rows[i].SourceId) >= 0) return false;
			return true;
		}

		/// <summary>
		/// A stored row, held to the grammar its own build promised it.
		/// <para>
		/// A lead's identity is derived from its locator, so a row written against the historical
		/// grammar has an identity this build can still reproduce &mdash; <see cref="LeadId"/> is
		/// asked for the same grammar the row is judged by, or the row's own name would stop
		/// matching itself on the day the grammar tightened.
		/// </para>
		/// </summary>
		internal static bool ValidReceipt(KingdomCivicLeadReceipt r) => r != null
			&& r.Version == KingdomCivicLeadReceipt.CurrentVersion
			&& r.Phase >= KingdomCivicLeadPhase.Prepared
			&& r.Phase <= KingdomCivicLeadPhase.Invalidated && r.Fault == null
			&& KingdomCuriosityRules.ValidId(r.SourceId)
			&& KingdomCuriosityRules.ValidId(r.SettlementId)
			&& r.SourceVersion > 0 && r.CompletedTick >= 0
			&& KingdomCuriosityRules.StorableLocator(KingdomCuriosityReceipt.FirstVersion,
				r.Locator)
			&& KingdomCuriosityRules.Text(r.Title)
			&& KingdomCuriosityRules.Text(r.AuthoredReason)
			&& StoredLeadId(r.SourceId, r.Locator) == r.LeadId;

		/// <summary>The derived identity for a stored row, whose locator may be historical.</summary>
		internal static string StoredLeadId(string sourceId, string locator)
		{
			if (!KingdomCuriosityRules.ValidId(sourceId)
				|| !KingdomCuriosityRules.StorableLocator(KingdomCuriosityReceipt.FirstVersion,
					locator)) return null;
			return Derive(sourceId, locator);
		}

		/// <summary>
		/// Whether a caller's receipt is the exact prepared row this book holds.
		/// <para>
		/// This is the gate in front of the journal. A caller arrives with a receipt it was handed
		/// some time ago, and between then and now the book may have moved on, been reloaded from
		/// a different save, or never have held this row at all. Every frozen field is compared
		/// and the revision the caller read is named, so a fabricated or stale receipt can never
		/// become the reason a note is written into a founder's journal.
		/// </para>
		/// </summary>
		public static bool TryMatchPreparedRow(KingdomCivicLeadBook book, long expectedRevision,
			KingdomCivicLeadReceipt receipt, out string failure)
		{
			failure = null;
			if (!ValidBook(book)) return Fail("civic lead authority is not usable", out failure);
			if (receipt == null) return Fail("there is no civic lead receipt", out failure);
			if (expectedRevision != book.Revision)
				return Fail("the civic lead receipt was read at revision " + expectedRevision
					+ " and the authority now stands at " + book.Revision, out failure);
			for (int i = 0; i < book.Rows.Count; i++)
			{
				KingdomCivicLeadReceipt row = book.Rows[i];
				if (row.SourceId != receipt.SourceId) continue;
				if (row.Phase != KingdomCivicLeadPhase.Prepared)
					return Fail("the civic lead is " + row.Phase + " and not prepared",
						out failure);
				if (!ExactPreparation(row, receipt) || row.Phase != receipt.Phase)
					return Fail("the civic lead receipt differs from the row this authority holds",
						out failure);
				return true;
			}
			return Fail("this authority holds no civic lead for that source", out failure);
		}

		private static KingdomCivicLeadCause Restate(KingdomCivicLeadReceipt r)
			=> new KingdomCivicLeadCause { SourceId = r.SourceId,
				SourceVersion = r.SourceVersion, SettlementId = r.SettlementId,
				Locator = r.Locator, Title = r.Title, AuthoredReason = r.AuthoredReason,
				CompletedTick = r.CompletedTick };

		private static KingdomCivicLeadReceipt PreparedReceipt(KingdomCivicLeadCause cause)
		{
			string id = LeadId(cause.SourceId, cause.Locator);
			return id == null ? null : new KingdomCivicLeadReceipt
			{
				Phase = KingdomCivicLeadPhase.Prepared,
				SourceId = cause.SourceId, SourceVersion = cause.SourceVersion,
				SettlementId = cause.SettlementId, LeadId = id, Locator = cause.Locator,
				Title = cause.Title, AuthoredReason = cause.AuthoredReason,
				CompletedTick = cause.CompletedTick
			};
		}

		private static bool ExactPreparation(KingdomCivicLeadReceipt a,
			KingdomCivicLeadReceipt b)
		{
			return a != null && b != null && a.Version == b.Version
				&& a.SourceId == b.SourceId && a.SourceVersion == b.SourceVersion
				&& a.SettlementId == b.SettlementId && a.LeadId == b.LeadId
				&& a.Locator == b.Locator && a.Title == b.Title
				&& a.AuthoredReason == b.AuthoredReason
				&& a.CompletedTick == b.CompletedTick;
		}

		internal static bool ValidCause(KingdomCivicLeadCause c) => c != null
			&& KingdomCuriosityRules.ValidId(c.SourceId)
			&& KingdomCuriosityRules.ValidId(c.SettlementId)
			&& c.SourceVersion > 0 && c.CompletedTick >= 0
			&& KingdomCuriosityRules.TryFullLocator(c.Locator)
			&& KingdomCuriosityRules.Text(c.Title)
			&& KingdomCuriosityRules.Text(c.AuthoredReason);

		private static bool Fail(string text, out string failure)
		{ failure = text; return false; }
	}
}
