using System;
using System.Collections.Generic;
using Qud.API;

namespace ThousandAndFirst
{
	public static partial class KingdomFounderHistory
	{
		private static bool TryEnsureNote(KingdomFounderHistoryReceipt Receipt,
			out string Failure)
		{
			Failure = "";
			r_KingdomFounderHistoryNote found = null;
			int matches = 0;
			for (int i = 0; i < JournalAPI.SultanNotes.Count; i++)
			{
				JournalSultanNote candidate = JournalAPI.SultanNotes[i];
				if (candidate != null && string.Equals(candidate.ID, Receipt.NoteId,
					StringComparison.Ordinal))
				{
					matches++;
					found = candidate as r_KingdomFounderHistoryNote;
				}
			}
			if (matches > 1)
				return Quarantine(Receipt, "duplicate founder-memory journal notes", out Failure);
			if (matches == 1 && found == null)
				return Quarantine(Receipt, "founder-memory note id belongs to another type", out Failure);
			if (found == null)
			{
				if (JournalAPI.NotesByID.TryGetValue(Receipt.NoteId, out IBaseJournalEntry indexed)
					&& indexed != null)
					return Quarantine(Receipt, "journal index carries an unlisted note", out Failure);
				found = NewNote(Receipt);
				JournalAPI.SultanNotes.Add(found);
				JournalAPI.AddedNote(found);
			}
			if (!CoreNoteExact(found, Receipt))
				return Quarantine(Receipt, "founder-memory journal note diverged", out Failure);
			if (JournalAPI.NotesByID.TryGetValue(Receipt.NoteId, out IBaseJournalEntry mapped))
			{
				if (!ReferenceEquals(mapped, found))
					return Quarantine(Receipt, "journal note index diverged", out Failure);
			}
			else JournalAPI.AddedNote(found);
			// Visibility and tradability are projection flags, not semantic authority. Restore them.
			found.Tradable = false;
			found.Revealed = true;
			if (Receipt.Phase < KingdomFounderHistoryPhase.NotePublished)
				Receipt.Phase = KingdomFounderHistoryPhase.NotePublished;
			return true;
		}

		private static r_KingdomFounderHistoryNote NewNote(
			KingdomFounderHistoryReceipt Receipt)
		{
			return new r_KingdomFounderHistoryNote
			{
				ID = Receipt.NoteId,
				Text = Receipt.Gospel,
				History = "",
				LearnedFrom = "the mourning rite in " + Receipt.CityName,
				Weight = 100,
				Revealed = true,
				Tradable = false,
				SultanID = Receipt.EntityId,
				EventID = Receipt.EventId,
				Attributes = new List<string>
				{
					KingdomFounderHistoryRules.JournalAttribute,
					Receipt.ProofId
				}
			};
		}

		private static bool CoreNoteExact(r_KingdomFounderHistoryNote Note,
			KingdomFounderHistoryReceipt Receipt)
		{
			return Note != null && Note.ID == Receipt.NoteId
				&& Note.Text == Receipt.Gospel && string.IsNullOrEmpty(Note.History)
				&& Note.LearnedFrom == "the mourning rite in " + Receipt.CityName
				&& Note.Weight == 100 && Note.SultanID == Receipt.EntityId
				&& Note.EventID == Receipt.EventId && Note.Attributes != null
				&& Note.Attributes.Count == 2
				&& Note.Attributes[0] == KingdomFounderHistoryRules.JournalAttribute
				&& Note.Attributes[1] == Receipt.ProofId;
		}
	}
}
