using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Curiosity is a curator recalling a place the founder's journal already holds. It reveals
	/// nothing, rewards nothing, and moves nothing; the whole authority exists so that the same
	/// conversation twice says the same thing twice.
	/// </summary>
	public static partial class KingdomCuriosityRules
	{
		public static bool TryPrepare(KingdomCuriosityBook book, long expectedRevision,
			KingdomCuriosityCause cause, IList<KingdomCuriosityNote> notes,
			out KingdomCuriosityReceipt receipt, out string failure)
		{
			receipt = null; failure = null;
			if (!ValidBook(book) || !ValidCause(cause))
				return Fail("curiosity authority or source is invalid", out failure);
			for (int i = 0; i < book.Rows.Count; i++)
				if (book.Rows[i].SourceId == cause.SourceId)
				{
					if (!SameCause(book.Rows[i], cause)
						|| !FrozenNoteStillStands(book.Rows[i], notes, out failure))
						return Fail(failure ?? "curiosity source replay differs from its frozen cause",
							out failure);
					receipt = book.Rows[i].Copy(); return true;
				}
			if (!TryPreparedReceipt(cause, notes, out KingdomCuriosityReceipt prepared,
				out failure)) return false;
			if (expectedRevision != book.Revision || book.Revision == long.MaxValue)
				return Fail("curiosity revision is unavailable", out failure);
			if (book.Rows.Count >= KingdomCuriosityBook.MaxRows)
				return Fail("curiosity attention is full", out failure);
			book.Rows.Add(prepared.Copy());
			book.Rows.Sort((a, b) => string.CompareOrdinal(a.SourceId, b.SourceId));
			book.Revision++; receipt = prepared.Copy(); return true;
		}

		public static bool TryClose(KingdomCuriosityBook book, long expectedRevision,
			string sourceId, KingdomCuriosityState state, long tick, out string failure)
		{
			failure = null;
			if (!ValidBook(book) || expectedRevision != book.Revision || tick < 0L
				|| state < KingdomCuriosityState.Viewed || state > KingdomCuriosityState.Invalidated)
				return Fail("curiosity closure is invalid", out failure);
			for (int i = 0; i < book.Rows.Count; i++)
			{
				KingdomCuriosityReceipt row = book.Rows[i];
				if (row.SourceId != sourceId) continue;
				if (row.State != KingdomCuriosityState.Available) return row.State == state;
				if (tick < row.PreparedTick) return Fail("closure predates curation", out failure);
				if (book.Revision == long.MaxValue)
					return Fail("curiosity revision is exhausted", out failure);
				KingdomCuriosityReceipt next = row.Copy(); next.State = state; next.ClosedTick = tick;
				book.Rows[i] = next; book.Revision++; return true;
			}
			return Fail("curiosity source is absent", out failure);
		}

		/// <summary>
		/// Whether a live journal note is still the exact one this receipt was cut against.
		/// <para>
		/// Category is compared only when the receipt is able to testify to one. A row migrated
		/// from wire revision 1 carries no category and is not given one here; asking it about a
		/// field it never stored, and taking silence for agreement, is how a mod ends up asserting
		/// history it invented this session.
		/// </para>
		/// </summary>
		public static bool SameForeignNote(KingdomCuriosityReceipt row,
			KingdomCuriosityNote note) => row != null && ValidNote(note)
			&& row.NoteId == note.Id && row.Locator == note.Locator && row.NoteText == note.Text
			&& (row.NoteCategory == null
				|| string.Equals(row.NoteCategory, note.Category, StringComparison.Ordinal));

		public static string AttentionReservationId(string sourceId)
		{
			if (!ValidId(sourceId)) return null;
			using (SHA256 sha = SHA256.Create())
			{
				byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(
					"taf:curiosity-attention:v1\n" + sourceId));
				StringBuilder b = new StringBuilder("taf:experience-audience:curiosity:");
				for (int i = 0; i < hash.Length; i++) b.Append(hash[i].ToString("x2"));
				return b.ToString();
			}
		}

		private static bool TryPreparedReceipt(KingdomCuriosityCause cause,
			IList<KingdomCuriosityNote> notes, out KingdomCuriosityReceipt receipt,
			out string failure)
		{
			receipt = null; failure = null;
			if (!TrySnapshot(notes, out KingdomCuriosityNote[] snapshot, out failure)) return false;

			List<KingdomCuriosityNote> eligible = new List<KingdomCuriosityNote>();
			for (int i = 0; i < snapshot.Length; i++)
				if (ValidNote(snapshot[i]) && string.Equals(snapshot[i].Category,
					cause.RequiredCategory, StringComparison.Ordinal)) eligible.Add(snapshot[i]);
			eligible.Sort(CompareNote);
			if (eligible.Count == 0)
				return Fail("no already-known valid note matches", out failure);
			KingdomCuriosityNote note = eligible[0]; int identityMatches = 0;
			for (int i = 0; i < snapshot.Length; i++)
				if (snapshot[i].Id == note.Id) identityMatches++;
			if (identityMatches != 1)
				return Fail("known-note identity is duplicated", out failure);
			receipt = new KingdomCuriosityReceipt {
				Version = KingdomCuriosityReceipt.CategoryVersion,
				State = KingdomCuriosityState.Available,
				SourceId = cause.SourceId, SourceVersion = cause.SourceVersion,
				SettlementId = cause.SettlementId, CuratorResidentId = cause.CuratorResidentId,
				CuratorName = cause.CuratorName, CuratorObjectId = cause.CuratorObjectId,
				NoteId = note.Id, Locator = note.Locator, NoteText = note.Text,
				NoteCategory = note.Category, Reason = cause.Reason,
				PreparedTick = cause.CompletedTick };
			return ValidReceipt(receipt)
				|| Fail("prepared curiosity snapshot is invalid", out failure);
		}

		private static bool FrozenNoteStillStands(KingdomCuriosityReceipt row,
			IList<KingdomCuriosityNote> notes, out string failure)
		{
			failure = null;
			if (!TrySnapshot(notes, out KingdomCuriosityNote[] snapshot, out failure)) return false;
			int matches = 0; KingdomCuriosityNote found = default(KingdomCuriosityNote);
			for (int i = 0; i < snapshot.Length; i++)
				if (snapshot[i].Id == row.NoteId) { matches++; found = snapshot[i]; }
			if (matches == 1 && SameForeignNote(row, found)) return true;
			return Fail("the frozen journal note is missing, duplicated, or changed", out failure);
		}

		private static bool TrySnapshot(IList<KingdomCuriosityNote> notes,
			out KingdomCuriosityNote[] snapshot, out string failure)
		{
			snapshot = null; failure = null;
			if (notes == null || notes.Count < 0 || notes.Count > MaxKnownNotes)
				return Fail("known-note snapshot is absent or exceeds its bound", out failure);
			int count = notes.Count;
			snapshot = new KingdomCuriosityNote[count];
			try { notes.CopyTo(snapshot, 0); }
			catch
			{
				snapshot = null;
				return Fail("known-note source changed while it was copied", out failure);
			}
			return snapshot.Length == count
				|| Fail("known-note source changed while it was copied", out failure);
		}

		private static int CompareNote(KingdomCuriosityNote a, KingdomCuriosityNote b)
		{
			int c = string.CompareOrdinal(a.Id, b.Id);
			if (c != 0) return c;
			c = string.CompareOrdinal(a.Locator, b.Locator);
			if (c != 0) return c;
			c = string.CompareOrdinal(a.Text, b.Text);
			return c != 0 ? c : string.CompareOrdinal(a.Category, b.Category);
		}

		/// <summary>
		/// Whether a stored row and a freshly prepared one are the same preparation.
		/// <para>
		/// A row read from a wire revision 1 save has no category, and this build cannot honestly
		/// supply the one it would have had. So a v1 row is recognised on everything it does
		/// store and is <b>not</b> upgraded from today's journal: the founder gets their own
		/// receipt back, unchanged, rather than a receipt improved with evidence gathered years
		/// after the fact. A stored row newer than the caller's is never matched at all.
		/// </para>
		/// </summary>
		private static bool SameCause(KingdomCuriosityReceipt stored,
			KingdomCuriosityCause cause)
		{
			return stored != null && cause != null && stored.SourceId == cause.SourceId
				&& stored.SourceVersion == cause.SourceVersion
				&& stored.SettlementId == cause.SettlementId
				&& stored.CuratorResidentId == cause.CuratorResidentId
				&& stored.CuratorName == cause.CuratorName
				&& stored.CuratorObjectId == cause.CuratorObjectId
				&& stored.Reason == cause.Reason && stored.PreparedTick == cause.CompletedTick
				&& (stored.NoteCategory == null
					|| string.Equals(stored.NoteCategory, cause.RequiredCategory,
						StringComparison.Ordinal));
		}
	}
}
