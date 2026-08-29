#if !TAF_TESTS
using Qud.API;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The one place this mod puts a map note into the founder's journal, and the exact order it
	/// does it in.
	/// <para>
	/// <b>Prepared durable authority, then <c>AddMapNote</c>, then an exact readback, then
	/// projected durable authority.</b> The record leads the journal and never trails it, so every
	/// place a save can be cut leaves a state that finishes cleanly: cut before the add and the
	/// prepared row simply re-projects; cut after it and preflight finds the note already there,
	/// matches it exactly, adds nothing and marks the row projected.
	/// </para>
	/// <para>
	/// Two engine facts shape the middle of that order. The first is that
	/// <c>JournalAPI.AddMapNote(JournalMapNote)</c> &mdash; the object overload,
	/// <c>Qud/API/JournalAPI.cs:1255-1270</c> &mdash; does <b>not</b> clear
	/// <c>_mapNoteCategories</c>, unlike the string overload (<c>:1289</c>),
	/// <c>DeleteMapNote</c> (<c>:1184</c>), <c>Init</c> (<c>:646</c>) and
	/// <c>JournalMapNote.Reveal</c>. Because a lead is built already revealed, <c>Reveal</c> never
	/// runs and nothing else clears it either, so a founder whose journal had no Settlements note
	/// before would never see the category appear. Assigning null to that public field is the
	/// engine's own idiom at all four of those sites, and this is the only place in this mod that
	/// does it.
	/// </para>
	/// <para>
	/// The second is that <c>AddedNote</c> (<c>:165-171</c>) registers by
	/// <c>NotesByID.TryAdd</c> and, on collision, <b>logs an error and carries on</b>. The list
	/// would then hold our note while the index still pointed at someone else's, and every
	/// <c>TryRevealNote</c>, <c>HasNote</c> and journal lookup would find theirs. So the readback
	/// is by identity against <c>NotesByID</c>, not by presence in <c>MapNotes</c>: the only proof
	/// that the journal accepted our note is that the object it hands back is the object we made.
	/// </para>
	/// </summary>
	internal static partial class KingdomCivicLeadRuntime
	{
		internal const string LeadCategory = "Settlements";

		internal static bool TryReserveAttention(KingdomExperienceLedger ledger, string realmId,
			string settlementId, string sourceId, long tick, long epoch, out string failure)
		{
			return KingdomCuriosityRuntime.TryReserveAttention(ledger, realmId, settlementId,
				sourceId, tick, epoch, out failure);
		}

		/// <summary>Projected or invalidated leads own only terminal cleanup. Repeated recovery
		/// accepts an already-missing audience and never recreates one.</summary>
		internal static bool TryReleaseTerminalAttention(KingdomExperienceLedger ledger,
			KingdomCivicLeadBook book, string sourceId, out string failure)
		{
			return KingdomCivicLeadRules.TryReleaseTerminalAttention(ledger, book, sourceId,
				out KingdomExperienceCapacityFault _, out failure);
		}

		internal static bool TryCauseFromCompletedDelve(string settlementId,
			string headZoneId, long completedTick, out KingdomCivicLeadCause cause)
		{
			cause = null;
			if (string.IsNullOrEmpty(settlementId) || completedTick < 0L
				|| !KingdomDelveLink.TryReadLoadedCompletion(headZoneId,
					out KingdomDelveLinkReceipt link, out long exactTick, out string _)
				|| exactTick != completedTick
				|| !KingdomCuriosityRules.TryFullLocator(link.FootZoneId)) return false;
			cause = new KingdomCivicLeadCause
			{
				SourceId = "taf:civic-lead:delve:" + link.Token,
				SourceVersion = 1, SettlementId = settlementId,
				Locator = link.FootZoneId, CompletedTick = completedTick,
				Title = "The civic delve below " + link.HeadZoneId,
				AuthoredReason = "A completed city delve opened this exact lower landing."
			};
			return true;
		}

		/// <summary>Proves the physical-link check above cannot thaw either endpoint.</summary>
		internal static bool LinkZonesLoaded(KingdomDelveLinkReceipt link)
		{
			if (link == null || The.ZoneManager?.CachedZones == null) return false;
			return The.ZoneManager.CachedZones.TryGetValue(link.HeadZoneId, out Zone head)
				&& The.ZoneManager.CachedZones.TryGetValue(link.FootZoneId, out Zone foot)
				&& head != null && foot != null && head.Built && foot.Built;
		}

		/// <summary>
		/// Projects one prepared civic lead into the journal.
		/// <para>
		/// The caller must supply the authority the prepared row was committed to and the revision
		/// it read, because a receipt on its own proves nothing: it may have been fabricated, or
		/// read before a reload, or belong to a book this session no longer holds. Nothing touches
		/// the journal until the row is shown to be the exact prepared row <i>and</i> to be
		/// durable, so a fabricated or stale receipt causes zero <c>AddMapNote</c> calls.
		/// </para>
		/// <para>
		/// Success means <b>durable</b>. The projection is recorded in the save through
		/// <see cref="KingdomCuriosityLeadCommit.TryCommitProjectedLead"/> before this returns
		/// true. A failure at that commit leaves the durable row prepared on purpose: the note
		/// standing in the journal is the exact note a retry would make, so the next run
		/// recognises it, adds nothing, and finishes the record.
		/// </para>
		/// <para>
		/// Once that commit is taken, the save and the journal agree and the projection has
		/// happened. Updating the caller's own copy of the book afterwards is a courtesy and is
		/// explicitly best-effort: a refusal there is reported in <c>failure</c> but does not turn
		/// a completed projection into a failure, because a caller told this failed would retry
		/// work that is already durably done.
		/// </para>
		/// </summary>
		internal static bool TryProject(IKingdomCivicMemoryAuthority authority,
			long expectedMemoryRevision, KingdomCivicLeadBook book,
			KingdomCivicLeadReceipt receipt, out string failure)
		{
			failure = null;
			if (book == null || receipt == null)
			{ failure = "there is no prepared civic lead to project"; return false; }
			if (!KingdomCuriosityRules.TryFullLocator(receipt.Locator))
			{ failure = "the civic lead does not name a canonical zone"; return false; }
			if (!KingdomCivicLeadRules.TryMatchPreparedRow(book, book.Revision, receipt,
				out failure)) return false;
			// One lease, held across the whole crossing: read now, committed after the journal.
			if (!KingdomCuriosityLeadCommit.TryReadDurableStanding(authority,
				expectedMemoryRevision, receipt, out KingdomCivicMemorySectionLease lease,
				out KingdomCivicLeadDurableStanding _, out failure)) return false;
			if (!TryPreflight(receipt, out JournalMapNote standing, out failure)) return false;
			JournalMapNote exact = standing ?? Compose(receipt);
			if (standing == null ? !TryAdd(exact, out failure)
				: !TryRepairIndex(exact, out failure)) return false;
			if (!Readback(exact, receipt, out failure)) return false;
			if (!KingdomCuriosityLeadCommit.TryCommitProjectedLead(authority, lease, receipt,
				out failure)) return false;
			// Past this line the save and the journal agree, and that is the whole of the truth.
			// Bringing the caller's convenience copy into line is a courtesy: if it refuses, the
			// projection has still happened, and reporting failure would invite a caller to retry
			// work that is already done. The refusal is named and the answer stays true.
			KingdomCivicLeadRules.TryMarkProjected(book, book.Revision, receipt.SourceId,
				receipt.LeadId, receipt.Locator, out string sync);
			failure = sync ?? "";
			return true;
		}
	}
}
#endif
