using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Fail-closed law over one report plan. Nothing here touches the world: the caller
	/// hands in the ledger's exact current note list and is handed back the next plan to persist
	/// BEFORE it writes. Every refusal returns false with a null next, leaving the caller holding
	/// the original plan unchanged; plans are immutable, so no refusal can half-apply.</summary>
	internal static partial class KingdomSubsidenceReportRules
	{
		/// <summary>One step's telling is bounded so a cut never leaves an unbounded resume queue.
		/// </summary>
		internal const int MaxEntries = 4;

		/// <summary>The ordinary-note bound KingdomLedger.Note itself enforces (Core/KingdomLedger.cs).
		/// A list already at the bound would drop the append silently, so the ledger half is skipped
		/// rather than stalled: the Chronicle obligation remains independently owed.</summary>
		internal const int MaxNotes = 12;

		internal const string LedgerHashDomain = "taf-subsidence-ledger-v1";
		/// <summary>The one batch-id grammar, taken from the batch law itself rather than restated.
		/// </summary>
		internal const string BatchPrefix = KingdomSubsidenceBatchRules.Prefix;
		private const string EventInfix = ":report:";

		/// <summary>A report belongs to a step, a batch, or one saved begin/arrest transition.
		/// Each uses its exact prefixed lower-hex identity; no other owner shape is admitted.
		/// </summary>
		internal static bool IsOwnerId(string value)
		{
			if (KingdomSubsidenceAnnouncementRules.IsId(value)) return true;
			if (KingdomSubsidenceStepRules.IsStepId(value)) return true;
			if (value == null || !value.StartsWith(BatchPrefix, StringComparison.Ordinal)
				|| value.Length != BatchPrefix.Length
					+ KingdomChronicleReceiptRules.Sha256HexChars) return false;
			return KingdomChronicleReceiptRules.IsSha256(value.Substring(BatchPrefix.Length));
		}

		internal static bool Valid(KingdomSubsidenceReportPlan plan)
		{
			if (plan == null || !IsOwnerId(plan.OwnerId)
				|| !KingdomIdentityRules.IsRealmId(plan.RealmId)
				|| !KingdomIdentityRules.IsSettlementId(plan.SettlementId)
				|| plan.Entries == null || plan.Entries.Count > MaxEntries) return false;
			bool priorDone = true;
			for (int i = 0; i < plan.Entries.Count; i++)
			{
				KingdomSubsidenceReportEntry entry = plan.Entries[i];
				if (!ValidEntry(entry) || !ValidCapacity(plan, i, entry)) return false;
				// Sequential frontier: nothing may be started while an earlier line is unfinished.
				if (!priorDone && (entry.LedgerPhase != ReportLedgerPhase.Prepared
					|| entry.ChronicleProved || entry.ChronicleLost || entry.CapacityRefused)) return false;
				priorDone = EntrySettled(entry);
			}
			return true;
		}

		/// <summary>Wholly delivered, both halves, every line. A plan with no entries is complete:
		/// an owner may owe no telling at all. A lost line is never complete however it is
		/// settled - see <see cref="Settled"/> for the weaker fact the frontier runs on.</summary>
		internal static bool Complete(KingdomSubsidenceReportPlan plan)
		{
			if (!Valid(plan)) return false;
			foreach (KingdomSubsidenceReportEntry entry in plan.Entries)
				if (!EntryComplete(entry)) return false;
			return true;
		}

		/// <summary>The chronicle identity of one line, derived from the owner and the ordinal so a
		/// replay after a cut re-derives exactly the same id. Null for an invalid plan or index.
		/// </summary>
		internal static string EventId(KingdomSubsidenceReportPlan plan, int index)
		{
			if (!Valid(plan) || index < 0 || index >= plan.Entries.Count) return null;
			return plan.OwnerId + EventInfix + index.ToString(CultureInfo.InvariantCulture);
		}

		/// <summary>Freezes the exact ledger before-state, or settles the line as explicitly
		/// skipped. Persist the returned plan before touching the ledger.</summary>
		internal static bool TryArmLedger(KingdomSubsidenceReportPlan plan, int index,
			IList<string> notes, out KingdomSubsidenceReportPlan next)
		{
			next = null;
			if (!AtFrontier(plan, index) || !TryHash(notes, out string hash)) return false;
			KingdomSubsidenceReportEntry entry = plan.Entries[index];
			if (entry.LedgerPhase != ReportLedgerPhase.Prepared) return false;
			// An empty ledger line was never owed; a full note list cannot take the append and must
			// not stall the world for it. Both settle here with before equal to after.
			bool skip = entry.LedgerText.Length == 0 || notes.Count >= MaxNotes;
			KingdomSubsidenceReportPlan value = plan.With(index, skip
				? entry.WithLedger(ReportLedgerPhase.Skipped, notes.Count, hash, notes.Count, hash)
				: entry.WithLedger(ReportLedgerPhase.Intent, notes.Count, hash, 0, ""));
			if (!Valid(value)) return false;
			next = value; return true;
		}

		/// <summary>What a pending intent authorizes against the ledger's exact current notes. The
		/// frozen before authorizes the write; the exact append authorizes the confirmation; any
		/// third state is a stranger's list and authorizes nothing.</summary>
		internal static KingdomSubsidenceEffectAction LedgerAction(KingdomSubsidenceReportPlan plan,
			int index, IList<string> notes)
		{
			if (!AtFrontier(plan, index) || notes == null || notes.Count > MaxNotes)
				return KingdomSubsidenceEffectAction.Refuse;
			KingdomSubsidenceReportEntry entry = plan.Entries[index];
			if (entry.LedgerPhase != ReportLedgerPhase.Intent)
				return KingdomSubsidenceEffectAction.Refuse;
			if (MatchesBefore(entry, notes)) return KingdomSubsidenceEffectAction.Apply;
			if (TryAfter(entry, notes, out string _)) return KingdomSubsidenceEffectAction.Confirm;
			return KingdomSubsidenceEffectAction.Refuse;
		}

		/// <summary>Settles a pending intent against the exact append and nothing else. A skipped
		/// line answers true to its own exact repeat and never changes.</summary>
		internal static bool TryProveLedger(KingdomSubsidenceReportPlan plan, int index,
			IList<string> notes, out KingdomSubsidenceReportPlan next)
		{
			next = null;
			if (!AtFrontier(plan, index) || notes == null || notes.Count > MaxNotes) return false;
			KingdomSubsidenceReportEntry entry = plan.Entries[index];
			// Lost is terminal: the one thing it can never become is proved.
			if (entry.LedgerPhase == ReportLedgerPhase.Lost) return false;
			if (entry.LedgerPhase == ReportLedgerPhase.Skipped)
			{
				if (notes.Count != entry.AfterCount || !TryHash(notes, out string repeat)
					|| !string.Equals(repeat, entry.AfterHash, StringComparison.Ordinal))
					return false;
				next = plan; return true;
			}
			if (LedgerAction(plan, index, notes) != KingdomSubsidenceEffectAction.Confirm
				|| !TryAfter(entry, notes, out string hash)) return false;
			KingdomSubsidenceReportPlan value = plan.With(index, entry.WithLedger(
				ReportLedgerPhase.Proved, entry.BeforeCount, entry.BeforeHash, notes.Count, hash));
			if (!Valid(value)) return false;
			next = value; return true;
		}

		/// <summary>The chronicle is told last, so a cut between the two halves re-tells nothing
		/// the ledger has not already settled. Idempotent once told.</summary>
		internal static bool TryProveChronicle(KingdomSubsidenceReportPlan plan, int index,
			out KingdomSubsidenceReportPlan next)
		{
			next = null;
			if (!AtFrontier(plan, index)) return false;
			KingdomSubsidenceReportEntry entry = plan.Entries[index];
			// A lost telling is never delivered afterwards; the two halves are exclusive.
			if (!LedgerSettled(entry.LedgerPhase) || entry.ChronicleLost || entry.CapacityRefused) return false;
			if (entry.ChronicleProved) { next = plan; return true; }
			KingdomSubsidenceReportPlan value = plan.With(index, entry.WithChronicle(true));
			if (!Valid(value)) return false;
			next = value; return true;
		}

		/// <summary>Settled is not delivered. A lost line is finished with the ledger and lets
		/// the frontier move on, but nothing was ever written for it.</summary>
		internal static bool LedgerSettled(ReportLedgerPhase phase)
		{
			return LedgerDelivered(phase) || phase == ReportLedgerPhase.Lost;
		}

		internal static bool LedgerDelivered(ReportLedgerPhase phase)
		{
			return phase == ReportLedgerPhase.Proved || phase == ReportLedgerPhase.Skipped;
		}

		/// <summary>SHA-256 over the exact note list under this report's own versioned domain.
		/// Null is distinct from empty and field boundaries cannot alias, so no two different
		/// lists share a hash by rearrangement.</summary>
		internal static bool TryHash(IList<string> notes, out string hash)
		{
			hash = null;
			if (notes == null || notes.Count > MaxNotes) return false;
			for (int i = 0; i < notes.Count; i++)
				if (notes[i] == null
					|| notes[i].Length > KingdomChronicleReceiptRules.MaxEntryChars) return false;
			return KingdomChronicleReceiptRules.TryCanonicalHash(LedgerHashDomain, notes, out hash);
		}

		/// <summary>Wholly told: both halves delivered. A lost half is never complete.</summary>
		private static bool EntryComplete(KingdomSubsidenceReportEntry entry)
		{
			return LedgerDelivered(entry.LedgerPhase) && entry.ChronicleProved;
		}

		/// <summary>Finished either way: nothing further will ever be attempted on this line, so
		/// the next one may begin.</summary>
		private static bool EntrySettled(KingdomSubsidenceReportEntry entry)
		{
			return LedgerSettled(entry.LedgerPhase)
				&& (entry.ChronicleProved || entry.ChronicleLost || entry.CapacityRefused);
		}

		private static bool AtFrontier(KingdomSubsidenceReportPlan plan, int index)
		{
			if (!Valid(plan) || index < 0 || index >= plan.Entries.Count) return false;
			for (int i = 0; i < index; i++) if (!EntrySettled(plan.Entries[i])) return false;
			return true;
		}

		private static bool ValidEntry(KingdomSubsidenceReportEntry entry)
		{
			if (entry == null || entry.AtTick < 0
				|| !KingdomSubsidenceRungRules.Text(entry.Text,
					KingdomChronicleReceiptRules.MaxEventTextChars, false)
				|| !KingdomSubsidenceRungRules.Text(entry.LedgerText,
					KingdomChronicleReceiptRules.MaxEntryChars, true)
				|| entry.BeforeCount < 0 || entry.BeforeCount > MaxNotes
				|| entry.AfterCount < 0 || entry.AfterCount > MaxNotes
				|| !Hash(entry.BeforeHash) || !Hash(entry.AfterHash)
				|| entry.LedgerLoss > LedgerLossKind.HomecomingReset
				|| entry.LedgerLoss != LedgerLossKind.None
					&& entry.LedgerPhase != ReportLedgerPhase.Lost
				|| entry.ChronicleProved && entry.ChronicleLost
				|| (entry.ChronicleProved || entry.ChronicleLost)
					&& !LedgerSettled(entry.LedgerPhase)) return false;
			switch (entry.LedgerPhase)
			{
				case ReportLedgerPhase.Prepared:
					return entry.BeforeCount == 0 && entry.AfterCount == 0
						&& entry.BeforeHash.Length == 0 && entry.AfterHash.Length == 0;
				case ReportLedgerPhase.Intent:
					// Only an owed line under the bound is ever left pending.
					return entry.LedgerText.Length != 0 && entry.BeforeCount < MaxNotes
						&& entry.BeforeHash.Length != 0 && entry.AfterCount == 0
						&& entry.AfterHash.Length == 0;
				case ReportLedgerPhase.Proved:
					return entry.LedgerText.Length != 0 && entry.BeforeHash.Length != 0
						&& entry.AfterHash.Length != 0 && entry.AfterCount == entry.BeforeCount + 1;
				case ReportLedgerPhase.Skipped:
					return (entry.LedgerText.Length == 0 || entry.BeforeCount == MaxNotes)
						&& entry.BeforeHash.Length != 0 && entry.AfterCount == entry.BeforeCount
						&& string.Equals(entry.BeforeHash, entry.AfterHash, StringComparison.Ordinal);
				case ReportLedgerPhase.Lost:
					// The before is exactly what the intent froze; the after is exactly what was
					// seen when the line was lost. No append is guessed, so no relation between the
					// two pairs is asserted - except that a third state is, by its own definition,
					// not the frozen before.
					return entry.LedgerLoss != LedgerLossKind.None && entry.LedgerText.Length != 0
						&& entry.BeforeCount < MaxNotes && entry.BeforeHash.Length != 0
						&& entry.AfterHash.Length != 0
						&& (entry.LedgerLoss != LedgerLossKind.ThirdState
							|| entry.AfterCount != entry.BeforeCount
							|| !string.Equals(entry.BeforeHash, entry.AfterHash,
								StringComparison.Ordinal));
				default:
					return false;
			}
		}

		private static bool Hash(string value)
		{
			return value != null
				&& (value.Length == 0 || KingdomChronicleReceiptRules.IsSha256(value));
		}

		private static bool MatchesBefore(KingdomSubsidenceReportEntry entry, IList<string> notes)
		{
			return notes.Count == entry.BeforeCount && TryHash(notes, out string hash)
				&& string.Equals(hash, entry.BeforeHash, StringComparison.Ordinal);
		}

		/// <summary>The exact append and no other list: one longer than the frozen before, its own
		/// tail is this entry's ledger line, and its head hashes to the frozen before hash.</summary>
		private static bool TryAfter(KingdomSubsidenceReportEntry entry, IList<string> notes,
			out string hash)
		{
			hash = null;
			if (notes.Count != entry.BeforeCount + 1 || !string.Equals(notes[notes.Count - 1],
				entry.LedgerText, StringComparison.Ordinal)) return false;
			string[] head = new string[entry.BeforeCount];
			for (int i = 0; i < head.Length; i++) head[i] = notes[i];
			if (!TryHash(head, out string before)
				|| !string.Equals(before, entry.BeforeHash, StringComparison.Ordinal)) return false;
			return TryHash(notes, out hash);
		}
	}
}
