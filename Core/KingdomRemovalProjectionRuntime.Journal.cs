using System;
using System.Collections.Generic;
using Qud.API;

namespace ThousandAndFirst
{
	internal static partial class KingdomRemovalProjectionRuntime
	{
		private const string FounderNoteType = "r_KingdomFounderHistoryNote";

		internal static bool TryInspectJournal(out List<string> Rows, out string Failure)
		{
			Rows = new List<string>(); Failure = null;
			if (JournalAPI.SultanNotes == null || JournalAPI.NotesByID == null)
				return Fail("the native journal indexes are absent", out Failure);
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < JournalAPI.SultanNotes.Count; i++)
			{
				JournalSultanNote note = JournalAPI.SultanNotes[i];
				if (note == null || note.GetType().Name != FounderNoteType) continue;
				if (string.IsNullOrEmpty(note.ID) || !ids.Add(note.ID))
					return Fail("founder-history notes have empty or duplicate identity", out Failure);
				if (JournalAPI.NotesByID.TryGetValue(note.ID, out IBaseJournalEntry indexed)
					&& !ReferenceEquals(indexed, note)
					&& !(indexed is JournalSultanNote baseNote
						&& baseNote.GetType() == typeof(JournalSultanNote)
						&& SameJournalFields(note, baseNote)))
					return Fail("founder-history list and index diverged", out Failure);
				Rows.Add(i + "\u001f" + note.ID);
			}
			foreach (KeyValuePair<string, IBaseJournalEntry> row in JournalAPI.NotesByID)
				if (row.Value?.GetType().Name == FounderNoteType && !ids.Contains(row.Key))
					return Fail("an unlisted custom founder-history note remains indexed", out Failure);
			return true;
		}

		internal static bool TryConvertJournal(out int Converted, out string Failure)
		{
			Converted = 0;
			if (!TryInspectJournal(out List<string> _, out Failure)) return false;
			for (int i = 0; i < JournalAPI.SultanNotes.Count; i++)
			{
				JournalSultanNote source = JournalAPI.SultanNotes[i];
				if (source == null || source.GetType().Name != FounderNoteType) continue;
				JournalSultanNote replacement;
				if (JournalAPI.NotesByID.TryGetValue(source.ID, out IBaseJournalEntry indexed)
					&& indexed is JournalSultanNote existing
					&& existing.GetType() == typeof(JournalSultanNote)
					&& SameJournalFields(source, existing)) replacement = existing;
				else
				{
					replacement = ToBaseJournalNote(source);
					JournalAPI.NotesByID[source.ID] = replacement;
				}
				JournalAPI.SultanNotes[i] = replacement; Converted++;
			}
			return TryInspectJournal(out List<string> remaining, out Failure)
				&& (remaining.Count == 0 || Fail("a custom founder-history note remains", out Failure));
		}

		private static JournalSultanNote ToBaseJournalNote(JournalSultanNote Source)
		{
			return new JournalSultanNote
			{
				SultanID = Source.SultanID,
				EventID = Source.EventID,
				ID = Source.ID,
				History = Source.History,
				Text = Source.Text,
				LearnedFrom = Source.LearnedFrom,
				Weight = Source.Weight,
				Revealed = Source.Revealed,
				Tradable = Source.Tradable,
				Attributes = Source.Attributes == null
					? new List<string>() : new List<string>(Source.Attributes)
			};
		}

		private static bool SameJournalFields(JournalSultanNote A, JournalSultanNote B)
		{
			if (A == null || B == null || A.SultanID != B.SultanID || A.EventID != B.EventID
				|| A.ID != B.ID || A.History != B.History || A.Text != B.Text
				|| A.LearnedFrom != B.LearnedFrom || A.Weight != B.Weight
				|| A.Revealed != B.Revealed || A.Tradable != B.Tradable
				|| (A.Attributes?.Count ?? 0) != (B.Attributes?.Count ?? 0)) return false;
			for (int i = 0; i < (A.Attributes?.Count ?? 0); i++)
				if (A.Attributes[i] != B.Attributes[i]) return false;
			return true;
		}
	}
}
