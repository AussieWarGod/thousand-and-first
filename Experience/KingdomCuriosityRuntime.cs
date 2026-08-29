#if !TAF_TESTS
using System;
using System.Collections.Generic;
using Qud.API;
using XRL.World;

namespace ThousandAndFirst
{
	internal enum KingdomCuriosityNoteStanding : byte
	{
		Unavailable = 0,
		Exact = 1,
		MissingOrChanged = 2
	}

	internal static class KingdomCuriosityRuntime
	{
		internal static bool TryReserveAttention(KingdomExperienceLedger ledger, string realmId,
			string settlementId, string sourceId, long tick, long epoch, out string failure)
		{
			string reservationId = KingdomCuriosityRules.AttentionReservationId(sourceId);
			if (ledger == null || reservationId == null)
			{ failure = "curiosity attention identity is invalid"; return false; }
			KingdomExperienceAudienceReceipt request = new KingdomExperienceAudienceReceipt
			{
				ReservationId = reservationId,
				RealmId = realmId, SettlementId = settlementId, SourceId = sourceId,
				Lane = KingdomExperienceLane.Curator,
				OptionKind = KingdomExperienceOptionKind.CivicKnowledge,
				CauseTick = tick, ReservedTick = tick, EnableEpoch = epoch
			};
			return KingdomExperienceRules.TryReserveAudience(ledger, ledger.Revision, request,
				out _, out failure);
		}

		/// <summary>Idempotent save-cut recovery. Terminal Curiosity authority derives one exact
		/// source-owned audience identity; a missing row is already clean.</summary>
		internal static bool TryReleaseTerminalAttention(KingdomExperienceLedger ledger,
			KingdomCuriosityBook book, string sourceId, out string failure)
		{
			return KingdomCuriosityRules.TryReleaseTerminalAttention(ledger, book, sourceId,
				out KingdomExperienceCapacityFault _, out failure);
		}

		internal static bool TryReadKnownNotes(out List<KingdomCuriosityNote> rows,
			out string failure)
		{
			rows = null; failure = null;
			List<JournalMapNote> live = JournalAPI.MapNotes;
			if (live == null)
			{ failure = "the journal map-note list is unavailable"; return false; }
			int count = live.Count;
			if (count < 0 || count > KingdomCuriosityRules.MaxKnownNotes)
			{ failure = "the journal map-note snapshot exceeds its bound"; return false; }
			JournalMapNote[] snapshot;
			try { snapshot = live.ToArray(); }
			catch { failure = "the journal changed while its notes were copied"; return false; }
			if (snapshot.Length != count)
			{ failure = "the journal changed while its notes were copied"; return false; }
			rows = new List<KingdomCuriosityNote>(snapshot.Length);
			for (int i = 0; i < snapshot.Length; i++)
			{
				JournalMapNote note = snapshot[i];
				if (note == null) continue;
				rows.Add(new KingdomCuriosityNote(note.ID, note.ZoneID, note.Text,
					note.Category, note.Revealed));
			}
			return true;
		}

		internal static bool TryReadMapNoteCount(out int count, out string failure)
		{
			count = -1; failure = null;
			List<JournalMapNote> live = JournalAPI.MapNotes;
			if (live == null)
			{ failure = "the journal map-note list is unavailable"; return false; }
			int observed = live.Count;
			if (observed < 0 || observed > KingdomCivicLeadRules.MaxJournalMapNotes)
			{ failure = "the journal map-note snapshot exceeds its bound"; return false; }
			JournalMapNote[] snapshot;
			try { snapshot = live.ToArray(); }
			catch { failure = "the journal changed while its notes were copied"; return false; }
			if (snapshot.Length != observed)
			{ failure = "the journal changed while its notes were copied"; return false; }
			count = observed; return true;
		}

		/// <summary>
		/// Whether the journal still holds exactly the note this receipt was cut from.
		/// <para>
		/// A wire revision 2 receipt records the category the curation matched, so a note whose
		/// category has since changed no longer answers for it. A receipt migrated from revision 1
		/// carries no category and is not asked about one &mdash; see
		/// <c>KingdomCuriosityRules.SameForeignNote</c> for why silence must not be read as
		/// agreement.
		/// </para>
		/// </summary>
		internal static bool StillExact(KingdomCuriosityReceipt receipt, out string failure)
		{
			return TryStanding(receipt, out KingdomCuriosityNoteStanding standing, out failure)
				&& standing == KingdomCuriosityNoteStanding.Exact;
		}

		internal static bool TryStanding(KingdomCuriosityReceipt receipt,
			out KingdomCuriosityNoteStanding standing, out string failure)
		{
			standing = KingdomCuriosityNoteStanding.Unavailable; failure = null;
			if (receipt == null || receipt.NoteId == null)
			{ failure = "the curiosity receipt is absent"; return false; }
			List<JournalMapNote> live = JournalAPI.MapNotes;
			if (live == null)
			{ failure = "the journal map-note snapshot is unavailable or over its bound"; return false; }
			int count = live.Count;
			if (count < 0 || count > KingdomCuriosityRules.MaxKnownNotes)
			{ failure = "the journal map-note snapshot is unavailable or over its bound"; return false; }
			JournalMapNote[] snapshot;
			try { snapshot = live.ToArray(); }
			catch { failure = "the journal changed while its notes were copied"; return false; }
			if (snapshot.Length != count || snapshot.Length > KingdomCuriosityRules.MaxKnownNotes)
			{ failure = "the journal changed while its notes were copied"; return false; }
			int matches = 0; JournalMapNote found = null;
			for (int i = 0; i < snapshot.Length; i++)
			{
				JournalMapNote note = snapshot[i];
				if (note != null && note.ID == receipt.NoteId) { matches++; found = note; }
			}
			bool exact = matches == 1 && KingdomCuriosityRules.SameForeignNote(receipt,
				new KingdomCuriosityNote(found.ID, found.ZoneID, found.Text,
					found.Category, found.Revealed));
			standing = exact ? KingdomCuriosityNoteStanding.Exact
				: KingdomCuriosityNoteStanding.MissingOrChanged;
			if (!exact) failure = "the exact journal note this curation named no longer stands";
			return true;
		}

		internal static string Rendering(KingdomCuriosityReceipt receipt)
		{
			if (receipt == null || receipt.State != KingdomCuriosityState.Available) return "";
			return receipt.CuratorName + " recalls " + receipt.NoteText + ".\n\n"
				+ receipt.Reason + "\n\nThe journal already marks it at " + receipt.Locator
				+ ". This curation reveals nothing new and offers no reward.";
		}
	}
}
#endif
