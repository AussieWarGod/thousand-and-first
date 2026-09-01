using System;
using System.Collections.Generic;
using Qud.API;

namespace ThousandAndFirst
{
	public static partial class KingdomFounderHistory
	{
		/// <summary>Reconstructs the TAF-owned view directly from its durable receipt.</summary>
		public static bool TryGetProjection(KingdomSystem System,
			out KingdomFounderHistoryProjection Projection, out string Failure)
		{
			Projection = null;
			Failure = "";
			KingdomFounderHistoryReceipt receipt = System?.FounderHistory;
			if (receipt == null || receipt.Phase == KingdomFounderHistoryPhase.None
				|| receipt.Phase == KingdomFounderHistoryPhase.Suppressed)
			{
				Failure = "no founder memory has been retained";
				return false;
			}
			if (receipt.Phase == KingdomFounderHistoryPhase.Quarantined)
			{
				Failure = string.IsNullOrEmpty(receipt.Fault)
					? "founder memory is quarantined" : receipt.Fault;
				return false;
			}
			string receiptFailure;
			if (!KingdomFounderHistoryRules.Validate(receipt, out receiptFailure))
			{
				Failure = receiptFailure;
				return false;
			}
			return TryBuildProjection(receipt, out Projection, out Failure);
		}

		private static bool TryBuildProjection(KingdomFounderHistoryReceipt Receipt,
			out KingdomFounderHistoryProjection Projection, out string Failure)
		{
			Projection = null;
			Failure = "";
			if (Receipt == null || string.IsNullOrEmpty(Receipt.ProjectionId)
				|| string.IsNullOrEmpty(Receipt.ProjectionProofId)
				|| string.IsNullOrEmpty(Receipt.Gospel))
			{
				Failure = "founder-memory projection lacks exact local evidence";
				return false;
			}
			Projection = new KingdomFounderHistoryProjection(
				Receipt.ProjectionId, Receipt.ProjectionProofId,
				KingdomFounderHistoryRules.EntityName(Receipt), Receipt.Gospel,
				"the mourning rite in " + Receipt.CityName, Receipt.HistoricYear);
			return true;
		}

		/// <summary>
		/// A schema-2 owner never opens vanilla pools. A migrated schema-1 owner first preflights
		/// both pools, then removes only its exact objects as one rollback-capable operation.
		/// Unknown, duplicated, shared, or altered evidence is quarantined without mutation.
		/// </summary>
		private static bool TryEnsureLegacyIsolation(KingdomFounderHistoryReceipt Receipt,
			out string Failure)
		{
			Failure = "";
			if (Receipt.LegacyCleanupState == KingdomFounderHistoryLegacyCleanupState.None)
				return true;
			if (Receipt.LegacyCleanupState != KingdomFounderHistoryLegacyCleanupState.Required
				&& Receipt.LegacyCleanupState != KingdomFounderHistoryLegacyCleanupState.Complete)
				return Quarantine(Receipt, "unknown schema-1 cleanup state", out Failure);

			LegacyHistoryCleanupPlan historyPlan;
			if (!TryInspectLegacyHistory(Receipt, out historyPlan, out Failure)) return false;
			LegacyJournalCleanupPlan journalPlan;
			if (!TryInspectLegacyJournal(Receipt, out journalPlan, out Failure)) return false;

			bool journalApplied = false;
			bool historyApplied = false;
			try
			{
				if (!journalPlan.Apply(out Failure))
					return Quarantine(Receipt, Failure, out Failure);
				journalApplied = true;
				if (!historyPlan.Apply(out Failure))
				{
					journalPlan.Rollback();
					return Quarantine(Receipt, Failure, out Failure);
				}
				historyApplied = true;
				if (!journalPlan.Absent() || !historyPlan.Absent())
					throw new InvalidOperationException("schema-1 cleanup readback diverged");
				Receipt.LegacyCleanupState = KingdomFounderHistoryLegacyCleanupState.Complete;
				return true;
			}
			catch (Exception ex)
			{
				if (historyApplied) historyPlan.Rollback();
				if (journalApplied) journalPlan.Rollback();
				return Quarantine(Receipt,
					"schema-1 cleanup threw " + ex.GetType().Name, out Failure);
			}
		}

		private static bool TryInspectLegacyJournal(KingdomFounderHistoryReceipt Receipt,
			out LegacyJournalCleanupPlan Plan, out string Failure)
		{
			Plan = null;
			Failure = "";
			if (JournalAPI.SultanNotes == null || JournalAPI.NotesByID == null)
			{
				Failure = "Qud journal is not loaded";
				return false;
			}
			JournalSultanNote listed = null;
			int listedIndex = -1;
			int matches = 0;
			for (int i = 0; i < JournalAPI.SultanNotes.Count; i++)
			{
				JournalSultanNote candidate = JournalAPI.SultanNotes[i];
				if (candidate == null) continue;
				bool idMatch = string.Equals(candidate.ID, Receipt.NoteId,
					StringComparison.Ordinal);
				bool proofMatch = HasExactLegacyProof(candidate, Receipt.ProofId);
				if (proofMatch && !idMatch)
					return Quarantine(Receipt,
						"schema-1 journal proof appears under another id", out Failure);
				if (!idMatch) continue;
				matches++;
				listed = candidate;
				listedIndex = i;
			}
			if (matches > 1)
				return Quarantine(Receipt, "duplicate schema-1 journal notes", out Failure);

			bool indexKey = JournalAPI.NotesByID.TryGetValue(Receipt.NoteId,
				out IBaseJournalEntry indexedEntry);
			bool indexed = indexKey && indexedEntry != null;
			JournalSultanNote indexedNote = indexedEntry as JournalSultanNote;
			if (indexKey && !indexed)
				return Quarantine(Receipt, "schema-1 journal index contains null", out Failure);
			if (listed != null && !LegacyNoteExact(listed, Receipt))
				return Quarantine(Receipt, "schema-1 journal note diverged", out Failure);
			if (indexed && (indexedNote == null || !LegacyNoteExact(indexedNote, Receipt)))
				return Quarantine(Receipt, "schema-1 journal index diverged", out Failure);
			if (listed != null && indexed && !ReferenceEquals(listed, indexedNote))
				return Quarantine(Receipt,
					"schema-1 journal list and index disagree", out Failure);

			foreach (KeyValuePair<string, IBaseJournalEntry> row in JournalAPI.NotesByID)
			{
				JournalSultanNote candidate = row.Value as JournalSultanNote;
				if (candidate == null || !HasExactLegacyProof(candidate, Receipt.ProofId)) continue;
				if (!string.Equals(row.Key, Receipt.NoteId, StringComparison.Ordinal)
					|| !string.Equals(candidate.ID, Receipt.NoteId, StringComparison.Ordinal))
					return Quarantine(Receipt,
						"schema-1 journal proof is indexed under another id", out Failure);
			}

			JournalSultanNote owned = listed ?? indexedNote;
			Plan = new LegacyJournalCleanupPlan(owned, listedIndex, indexed);
			return true;
		}

		private static bool LegacyNoteExact(JournalSultanNote Note,
			KingdomFounderHistoryReceipt Receipt)
		{
			Type type = Note?.GetType();
			return Note != null
				&& (type == typeof(r_KingdomFounderHistoryNote)
					|| type == typeof(JournalSultanNote))
				&& Note.ID == Receipt.NoteId && Note.Text == Receipt.Gospel
				&& string.IsNullOrEmpty(Note.History)
				&& Note.LearnedFrom == "the mourning rite in " + Receipt.CityName
				&& Note.Weight == 100 && Note.SultanID == Receipt.EntityId
				&& Note.EventID == Receipt.EventId && Note.Attributes != null
				&& Note.Attributes.Count == 2
				&& Note.Attributes[0] == KingdomFounderHistoryRules.JournalAttribute
				&& Note.Attributes[1] == Receipt.ProofId;
		}

		private static bool HasExactLegacyProof(JournalSultanNote Note, string ProofId)
		{
			return Note?.Attributes != null && Note.Attributes.Count == 2
				&& Note.Attributes[0] == KingdomFounderHistoryRules.JournalAttribute
				&& Note.Attributes[1] == ProofId;
		}

		private sealed class LegacyJournalCleanupPlan
		{
			private readonly JournalSultanNote Note;
			private readonly int ListIndex;
			private readonly bool WasIndexed;
			private bool Applied;

			internal LegacyJournalCleanupPlan(JournalSultanNote Note, int ListIndex,
				bool WasIndexed)
			{
				this.Note = Note;
				this.ListIndex = ListIndex;
				this.WasIndexed = WasIndexed;
			}

			internal bool Apply(out string Failure)
			{
				Failure = "";
				Applied = true;
				try
				{
					if (ListIndex >= 0) JournalAPI.SultanNotes.RemoveAt(ListIndex);
					if (WasIndexed && !JournalAPI.NotesByID.Remove(Note.ID))
						throw new InvalidOperationException("journal index removal refused");
					return true;
				}
				catch (Exception ex)
				{
					Rollback();
					Failure = "schema-1 journal cleanup threw " + ex.GetType().Name;
					return false;
				}
			}

			internal bool Absent()
			{
				if (Note == null) return true;
				return !JournalAPI.SultanNotes.Contains(Note)
					&& !JournalAPI.NotesByID.ContainsKey(Note.ID);
			}

			internal void Rollback()
			{
				if (!Applied || Note == null) return;
				if (ListIndex >= 0 && !JournalAPI.SultanNotes.Contains(Note))
					JournalAPI.SultanNotes.Insert(ListIndex, Note);
				if (WasIndexed) JournalAPI.NotesByID[Note.ID] = Note;
				Applied = false;
			}
		}
	}
}
