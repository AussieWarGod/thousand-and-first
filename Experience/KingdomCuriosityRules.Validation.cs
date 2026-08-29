using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// What a curiosity book, row, cause and journal note must be before anything else in this
	/// family will look at them. Every bound here is a primitive one, and every byte cap in
	/// <see cref="KingdomCuriosityLeadCodec"/> is built from these constants rather than beside
	/// them, so a bound cannot be widened in one place and forgotten in the other.
	/// </summary>
	public static partial class KingdomCuriosityRules
	{
		/// <summary>Authored prose: a curator's name, a note's text, a founder's reason.</summary>
		public const int MaxText = 384;

		/// <summary>A semantic identity: a source, a settlement, an object, a journal id.</summary>
		public const int MaxIdChars = 256;

		/// <summary>
		/// A journal category. The engine's own longest shipped category is the twenty-five
		/// characters of "Ruins with Becoming Nooks" (<c>Qud/API/JournalMapNote.cs</c>, the
		/// <c>Reveal</c> category ladder), and an unrecognised category is allowed there rather
		/// than refused &mdash; it simply falls to the support-address branch. This is set well
		/// above the shipped set so a mod's category is carried, and well below
		/// <see cref="MaxText"/> because a category is an identifier, not prose.
		/// </summary>
		public const int MaxCategoryChars = 64;

		/// <summary>How many journal notes this build will read in one snapshot.</summary>
		public const int MaxKnownNotes = 512;

		/// <summary>
		/// A book is usable only while this build understands it. A future book and a quarantined
		/// book both answer false here, which is what makes them read-only: every mutation in this
		/// family passes through this gate first, so neither can be edited, and
		/// <see cref="KingdomCuriosityLeadCodec"/> re-emits their retained bytes untouched.
		/// </summary>
		internal static bool ValidBook(KingdomCuriosityBook b)
		{
			if (b == null || b.State != KingdomCuriosityBookState.Compatible || b.Fault != null
				|| b.OpaquePayload != null || b.OpaqueVersion != 0
				|| b.Revision < 0L || b.Rows.Count > KingdomCuriosityBook.MaxRows)
				return false;
			HashSet<string> sources = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < b.Rows.Count; i++)
				if (!ValidReceipt(b.Rows[i]) || !sources.Add(b.Rows[i].SourceId)
					|| i > 0 && string.CompareOrdinal(b.Rows[i - 1].SourceId,
						b.Rows[i].SourceId) >= 0) return false;
			return true;
		}

		/// <summary>
		/// A row is lawful at wire revision 1 without a category and at revision 2 with one.
		/// Nothing between: a revision 2 row with no category would be a row claiming to know
		/// something it does not, and a revision 1 row carrying one would be a migration that
		/// invented its own evidence.
		/// </summary>
		internal static bool ValidReceipt(KingdomCuriosityReceipt r) => r != null
			&& ValidRowVersionAndCategory(r)
			&& r.State >= KingdomCuriosityState.Available
			&& r.State <= KingdomCuriosityState.Invalidated && ValidId(r.SourceId)
			&& ValidId(r.SettlementId) && r.SourceVersion > 0 && r.CuratorResidentId > 0
			&& Text(r.CuratorName) && ValidId(r.CuratorObjectId) && ValidId(r.NoteId)
			&& StorableLocator(r.Version, r.Locator) && Text(r.NoteText) && Text(r.Reason)
			&& r.PreparedTick >= 0L && (r.State == KingdomCuriosityState.Available
				? r.ClosedTick == -1L : r.ClosedTick >= r.PreparedTick);

		/// <summary>
		/// Which locator grammar a stored row is held to.
		/// <para>
		/// A revision 1 row may predate the canonical grammar, so it is allowed either &mdash; the
		/// wider historical form is what its own build promised it. A revision 2 row could only
		/// have been written by a build that already had the canonical grammar, so nothing but the
		/// canonical form is accepted from it. The two doors never swap: new authorship goes
		/// through <see cref="ValidCause"/> and <see cref="ValidNote"/>, and both of those demand
		/// the canonical form regardless of version.
		/// </para>
		/// </summary>
		internal static bool StorableLocator(int rowVersion, string locator)
		{
			if (TryFullLocator(locator)) return true;
			return rowVersion == KingdomCuriosityReceipt.FirstVersion
				&& LegacyFullLocator(locator);
		}

		private static bool ValidRowVersionAndCategory(KingdomCuriosityReceipt r)
		{
			if (r.Version == KingdomCuriosityReceipt.FirstVersion) return r.NoteCategory == null;
			return r.Version == KingdomCuriosityReceipt.CategoryVersion
				&& Category(r.NoteCategory);
		}

		internal static bool ValidCause(KingdomCuriosityCause c) => c != null
			&& ValidId(c.SourceId) && ValidId(c.SettlementId) && c.SourceVersion > 0
			&& c.CuratorResidentId > 0 && Text(c.CuratorName) && ValidId(c.CuratorObjectId)
			&& Text(c.Reason) && Category(c.RequiredCategory) && c.CompletedTick >= 0L;

		internal static bool ValidNote(KingdomCuriosityNote n) => n.Revealed && ValidId(n.Id)
			&& TryFullLocator(n.Locator) && Text(n.Text) && Category(n.Category);

		internal static bool ValidId(string s) => Bounded(s, MaxIdChars);
		internal static bool Text(string s) => Bounded(s, MaxText);
		internal static bool Category(string s) => Bounded(s, MaxCategoryChars);

		/// <summary>
		/// Every string this family accepts, held to the same three things: present, inside its
		/// field's bound, and not padded.
		/// <para>
		/// The fourth is why this is one helper and not three. A string carrying an unpaired
		/// surrogate is refused <b>here</b>, at ingress, rather than at the moment the book is
		/// written &mdash; because by then the revision has advanced and the founder's records
		/// hold a row that cannot be saved. Strict UTF-8 is the wire's rule; making it the rules'
		/// rule too means a book never reaches a state it cannot be written out of.
		/// </para>
		/// </summary>
		private static bool Bounded(string s, int max) => !string.IsNullOrEmpty(s)
			&& s.Length <= max && s.Trim() == s && Utf8Encodable(s);

		internal static bool Fail(string text, out string failure)
		{ failure = text; return false; }
	}
}
