#if !TAF_TESTS
using Qud.API;

namespace ThousandAndFirst
{
	/// <summary>
	/// Everything this family does to the founder's journal, and nothing it does anywhere else.
	/// <para>
	/// One note is added, or one half-finished add is completed, and both are read back by
	/// identity before anyone is told they worked. Nothing here deletes a note, reveals one,
	/// un-reveals one, makes one tradable, or writes the journal's index by hand; the only
	/// mutations are the engine's own public <c>AddMapNote</c>, <c>AddedNote</c> and <c>Init</c>.
	/// </para>
	/// </summary>
	internal static partial class KingdomCivicLeadRuntime
	{
		/// <summary>
		/// Adds the note, and invalidates the category cache whether or not that succeeded.
		/// <para>
		/// The invalidation is in a <c>finally</c> because <c>AddMapNote</c> is not atomic: it
		/// appends to <c>MapNotes</c> first and only then registers the note and files it by zone
		/// (<c>Qud/API/JournalAPI.cs:1257-1269</c>). A throw anywhere after that first append
		/// leaves the note in the list with the category cache still standing, and a return that
		/// skipped the invalidation would strand a category the journal can no longer list.
		/// Clearing it costs one rebuild and is correct in every case, including the ones where
		/// nothing was added at all.
		/// </para>
		/// </summary>
		private static bool TryAdd(JournalMapNote exact, out string failure)
		{
			failure = null;
			try { JournalAPI.AddMapNote(exact); }
			catch { failure = "the journal rejected the civic lead"; return false; }
			finally { InvalidateJournalCaches(); }
			return true;
		}

		/// <summary>
		/// Finishes an add that was cut in half.
		/// <para>
		/// <c>AddMapNote</c> appends to <c>MapNotes</c> before it registers the note in
		/// <c>NotesByID</c> and files it by zone (<c>Qud/API/JournalAPI.cs:1257-1269</c>). A
		/// throw in between leaves the exact note in the list and unknown to the index, and a
		/// retry would then find the note, add nothing, and fail the readback for ever. So when
		/// the durable record says this lead is ours and every field of the standing note
		/// matches, the missing registration is made through the engine's own public
		/// <c>AddedNote</c>.
		/// </para>
		/// <para>
		/// Only when the identity is absent. <c>AddedNote</c> registers by <c>TryAdd</c> and
		/// merely logs on a collision, so calling it against an id someone else holds would
		/// change nothing and hide the conflict; that case is refused instead. Nothing here
		/// writes to the index directly, and nothing here removes a note.
		/// </para>
		/// </summary>
		private static bool TryRepairIndex(JournalMapNote standing, out string failure)
		{
			failure = null;
			if (JournalAPI.NotesByID.TryGetValue(standing.ID, out IBaseJournalEntry indexed))
			{
				if (ReferenceEquals(indexed, standing)) return true;
				failure = "the journal index holds another entry under this identity";
				return false;
			}
			try { JournalAPI.AddedNote(standing); }
			catch
			{ failure = "the journal refused to register the standing civic lead"; return false; }
			finally { InvalidateJournalCaches(); }
			return true;
		}

		/// <summary>
		/// Drops both journal map caches, through the engine's own public reset.
		/// <para>
		/// <c>JournalAPI.Init()</c> nulls <c>_mapNotesByZone</c> and <c>_mapNoteCategories</c>
		/// (<c>Qud/API/JournalAPI.cs:645-650</c>), and both need dropping: the object overload
		/// of <c>AddMapNote</c> files a note by zone only after the append that may have thrown,
		/// and it never clears the category cache at all. The explicit category line stays
		/// because it is the specific promise this family makes, and a later engine whose
		/// <c>Init</c> stopped covering it would otherwise take the promise with it.
		/// </para>
		/// </summary>
		private static void InvalidateJournalCaches()
		{
			JournalAPI._mapNoteCategories = null;
			JournalAPI.Init();
		}

		/// <summary>
		/// Duplicate and capacity first, before the journal or the record is touched at all. A
		/// standing note that is not exactly ours is a conflict and is left alone: the prepared row
		/// stays prepared and a founder can be told, which is the only honest outcome when someone
		/// else's knowledge already occupies our identity.
		/// <para>
		/// <c>JournalAPI.MapNotes</c> is a public mutable list that anything in the game may add
		/// to, so it is measured once and copied once, and every question below is put to the
		/// copy. Counting the live list for capacity and then walking it again for duplicates
		/// would be two readings of two different journals, and the decision would belong to
		/// neither.
		/// </para>
		/// </summary>
		private static bool TryPreflight(KingdomCivicLeadReceipt receipt,
			out JournalMapNote standing, out string failure)
		{
			standing = null; failure = null;
			// Bound once. Every question below is put to this reference and to the one array it
			// yields; naming the property again would be asking a second, later journal.
			System.Collections.Generic.List<JournalMapNote> live = JournalAPI.MapNotes;
			if (live == null || JournalAPI.NotesByID == null)
			{ failure = "the journal is not available"; return false; }
			if (live.Count > KingdomCivicLeadRules.MaxJournalMapNotes)
			{ failure = "the journal holds more map notes than this build will read"; return false; }
			JournalMapNote[] notes = live.ToArray();
			if (notes.Length > KingdomCivicLeadRules.MaxJournalMapNotes)
			{ failure = "the journal grew past its bound while it was being read"; return false; }
			int matches = 0;
			for (int i = 0; i < notes.Length; i++)
			{
				JournalMapNote note = notes[i];
				if (note != null && note.ID == receipt.LeadId) { matches++; standing = note; }
			}
			if (matches > 1)
			{
				standing = null;
				failure = "the journal already holds " + matches
					+ " map notes under this civic lead's identity";
				return false;
			}
			if (matches == 1)
			{
				if (Same(standing, receipt)) return true;
				standing = null;
				failure = "a map note under this civic lead's identity records different "
					+ "knowledge; the lead stays prepared rather than replacing it";
				return false;
			}
			if (JournalAPI.NotesByID.ContainsKey(receipt.LeadId))
			{
				failure = "the journal index already holds this identity for another kind of note";
				return false;
			}
			if (notes.Length >= KingdomCivicLeadRules.MaxJournalMapNotes)
			{ failure = "the journal's map-note capacity is full"; return false; }
			return true;
		}

		/// <summary>
		/// The journal accepted our note only if its index hands back the very object we made.
		/// Reference identity, not equality: a foreign entry that merely looks the same would
		/// still be the one every later lookup resolves to.
		/// </summary>
		private static bool Readback(JournalMapNote exact, KingdomCivicLeadReceipt receipt,
			out string failure)
		{
			failure = null;
			if (!JournalAPI.NotesByID.TryGetValue(receipt.LeadId, out IBaseJournalEntry indexed)
				|| !ReferenceEquals(indexed, exact))
			{
				failure = "the journal index does not resolve this civic lead to the note "
					+ "this build added; the lead stays prepared";
				return false;
			}
			if (!Same(exact, receipt))
			{ failure = "the journal changed the civic lead as it was added"; return false; }
			return true;
		}

		private static JournalMapNote Compose(KingdomCivicLeadReceipt receipt)
		{
			return new JournalMapNote
			{
				ID = receipt.LeadId, ZoneID = receipt.Locator, Text = receipt.Title,
				Category = LeadCategory, Revealed = true, Tradable = false,
				LearnedFrom = receipt.AuthoredReason
			};
		}

		/// <summary>Every field this build authored, compared exactly. <c>ZoneID</c> is read back
		/// through the engine's own getter, so a locator that was not canonical would already have
		/// failed here rather than after the founder's next save.</summary>
		private static bool Same(JournalMapNote note, KingdomCivicLeadReceipt receipt)
		{
			return note != null && note.ID == receipt.LeadId && note.ZoneID == receipt.Locator
				&& note.Text == receipt.Title && note.Category == LeadCategory
				&& note.LearnedFrom == receipt.AuthoredReason
				&& note.Revealed && !note.Tradable;
		}
	}
}
#endif
