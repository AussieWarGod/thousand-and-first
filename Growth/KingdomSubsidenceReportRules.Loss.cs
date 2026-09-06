using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>The terminal half of the report law: how a telling that can never be delivered is
	/// said so, once, in the plan itself rather than inferred from a later look at the world.
	/// <para>Losing is not a repair and never an effect. Nothing here writes to the ledger or the
	/// Chronicle; it records what was observed, keeps the frozen before as the witness, and closes
	/// the line. A lost line is settled - the frontier moves on and the pass stops stalling - but
	/// it is never delivered, never proved, and never re-armed.</para></summary>
	internal static partial class KingdomSubsidenceReportRules
	{
		/// <summary>Every line finished, delivered, lost or explicitly refused for capacity. This is what the frontier and the step
		/// lane run on; <see cref="Complete"/> is the stronger claim that everything was told.
		/// </summary>
		internal static bool Settled(KingdomSubsidenceReportPlan plan)
		{
			if (!Valid(plan)) return false;
			foreach (KingdomSubsidenceReportEntry entry in plan.Entries)
				if (!EntrySettled(entry)) return false;
			return true;
		}

		/// <summary>Whether any line has a loss or explicit capacity refusal requiring retention. It
		/// answers for a malformed plan too, so a caller can name the loss before refusing.
		/// </summary>
		internal static bool HasLoss(KingdomSubsidenceReportPlan plan)
		{
			if (plan == null || plan.Entries == null) return false;
			foreach (KingdomSubsidenceReportEntry entry in plan.Entries)
				if (entry != null
					&& (entry.LedgerLoss != LedgerLossKind.None || entry.ChronicleLost || entry.CapacityRefused))
					return true;
			return false;
		}

		/// <summary>Latches a pending intent as lost against the ledger's exact current notes.
		/// <para>Ordinarily only a stranger's list loses a line: the frozen before still
		/// authorizes the write and the exact append still authorizes the proof, so neither is a
		/// loss. The homecoming barrier is the one exception - it calls this immediately BEFORE
		/// clearing the notes, while the frozen before is still standing, precisely because after
		/// the clear the same list would falsely re-authorize the append. It may therefore lose a
		/// line that still matches its before; it may never lose one that already holds the exact
		/// append, because that line was delivered.</para>
		/// <para>The observed count and hash are recorded as the after and the frozen before is
		/// carried through untouched. Nothing is appended, assumed or repaired. Persist the
		/// returned plan before doing anything further to the world.</para></summary>
		internal static bool TryLoseLedger(KingdomSubsidenceReportPlan plan, int index,
			IList<string> notes, bool homecomingReset, out KingdomSubsidenceReportPlan next)
		{
			next = null;
			if (!AtFrontier(plan, index) || !TryHash(notes, out string hash)) return false;
			KingdomSubsidenceReportEntry entry = plan.Entries[index];
			LedgerLossKind kind = homecomingReset
				? LedgerLossKind.HomecomingReset : LedgerLossKind.ThirdState;
			if (entry.LedgerPhase == ReportLedgerPhase.Lost)
			{
				// The same loss seen again: the latch holds, the witness stands, nothing is
				// re-armed. Any other observation is a different fact and is refused outright.
				if (entry.LedgerLoss != kind || entry.AfterCount != notes.Count
					|| !string.Equals(entry.AfterHash, hash, StringComparison.Ordinal))
					return false;
				next = plan; return true;
			}
			if (entry.LedgerPhase != ReportLedgerPhase.Intent) return false;
			KingdomSubsidenceEffectAction action = LedgerAction(plan, index, notes);
			if (action == KingdomSubsidenceEffectAction.Confirm
				|| !homecomingReset && action != KingdomSubsidenceEffectAction.Refuse) return false;
			KingdomSubsidenceReportPlan value
				= plan.With(index, entry.WithLoss(kind, notes.Count, hash));
			if (!Valid(value)) return false;
			next = value; return true;
		}

		/// <summary>Latches the chronicle half as lost. Told last, like its proof, and only once
		/// the ledger half has settled - so a cut cannot leave a chronicle loss standing over a
		/// ledger question still open. Idempotent; refused outright once the line was delivered.
		/// </summary>
		internal static bool TryLoseChronicle(KingdomSubsidenceReportPlan plan, int index,
			out KingdomSubsidenceReportPlan next)
		{
			next = null;
			if (!AtFrontier(plan, index)) return false;
			KingdomSubsidenceReportEntry entry = plan.Entries[index];
			if (!LedgerSettled(entry.LedgerPhase) || entry.ChronicleProved || entry.CapacityRefused) return false;
			if (entry.ChronicleLost) { next = plan; return true; }
			KingdomSubsidenceReportPlan value = plan.With(index, entry.WithChronicleLost());
			if (!Valid(value)) return false;
			next = value; return true;
		}
	}
}
