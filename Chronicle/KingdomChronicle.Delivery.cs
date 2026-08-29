using System;
using System.Collections.Generic;
using Qud.API;
using XRL;
using XRL.World;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomChronicle
	{
		/// <summary>
		/// Index of the empty tail — the telling that adds nothing after the deed. Found rather
		/// than hardcoded, so adding a tail to <see cref="KingdomRules.OutsiderTails"/> cannot
		/// silently repoint the scriptorium at an embellishment.
		/// </summary>
		private static int PlainTailIndex()
		{
			for (int i = 0; i < KingdomRules.OutsiderTails.Length; i++)
			{
				if (string.IsNullOrEmpty(KingdomRules.OutsiderTails[i]))
				{
					return i;
				}
			}
			return KingdomRules.OutsiderTails.Length - 1;
		}

		private static bool DeliverList(List<KingdomChronicleReceipt> Rows,
			KingdomChronicleReceipt Receipt, List<string> Values, bool Official)
		{
			if (Rows == null || Receipt == null || Values == null || Values.Count > MaxEntries)
				return false;
			KingdomChronicleSinkDisposition state = Official
				? Receipt.OfficialState : Receipt.OutsiderState;
			if (KingdomChronicleReceiptRules.IsSettled(state)) return true;
			string register = Official ? "official" : "outsider";
			string value = Official ? Receipt.Official : Receipt.Outsider;
			string before = Official ? Receipt.OfficialBefore : Receipt.OutsiderBefore;
			string after = Official ? Receipt.OfficialAfter : Receipt.OutsiderAfter;
			string current;
			if (!KingdomChronicleReceiptRules.TryHashList(register, Values, out current))
				return LoseList(Rows, Receipt, Official, "list-hash");
			KingdomChronicleListAction action = KingdomChronicleReceiptRules.ListAction(
				state, current, before, after);
			if (action == KingdomChronicleListAction.ConfirmDelivered)
			{
				SetListState(Receipt, Official, KingdomChronicleSinkDisposition.Delivered);
				return WriteEventReceipts(Rows, register + "-confirm");
			}
			if (action != KingdomChronicleListAction.Append)
				return LoseList(Rows, Receipt, Official, register + "-interleaved");
			if (state == KingdomChronicleSinkDisposition.Pending)
			{
				SetListState(Receipt, Official, KingdomChronicleSinkDisposition.Attempting);
				if (!WriteEventReceipts(Rows, register + "-intent")) return false;
			}
			// Persistence is an inspectable seam only through exact list state. Recompute
			// after intent: exact after confirms, exact before authorizes one append, and
			// anything else is unrelated interleaving and becomes Lost.
			if (!KingdomChronicleReceiptRules.TryHashList(register, Values, out current))
				return LoseList(Rows, Receipt, Official, register + "-rehash");
			action = KingdomChronicleReceiptRules.ListAction(
				KingdomChronicleSinkDisposition.Attempting, current, before, after);
			if (action == KingdomChronicleListAction.ConfirmDelivered)
			{
				SetListState(Receipt, Official, KingdomChronicleSinkDisposition.Delivered);
				return WriteEventReceipts(Rows, register + "-confirm-after-intent");
			}
			if (action != KingdomChronicleListAction.Append)
				return LoseList(Rows, Receipt, Official, register + "-interleaved-after-intent");
			try { KingdomChronicleReceiptRules.AppendBounded(Values, value); }
			catch { return LoseList(Rows, Receipt, Official, register + "-append"); }
			if (!KingdomChronicleReceiptRules.TryHashList(register, Values, out current)
				|| !string.Equals(current, after, StringComparison.Ordinal))
				return LoseList(Rows, Receipt, Official, register + "-after-mismatch");
			SetListState(Receipt, Official, KingdomChronicleSinkDisposition.Delivered);
			return WriteEventReceipts(Rows, register + "-delivered");
		}

		private static bool LoseList(List<KingdomChronicleReceipt> Rows,
			KingdomChronicleReceipt Receipt, bool Official, string Context)
		{
			SetListState(Receipt, Official, KingdomChronicleSinkDisposition.Lost);
			bool written = WriteEventReceipts(Rows, Context + "-lost");
			if (written) ReportFault(KingdomChronicleRegistryFault.None, Context, true);
			return written;
		}

		private static void SetListState(KingdomChronicleReceipt Receipt, bool Official,
			KingdomChronicleSinkDisposition State)
		{
			if (Official) Receipt.OfficialState = State;
			else Receipt.OutsiderState = State;
			Receipt.Updated = Math.Max(Receipt.Updated, Now());
		}

		private static bool DeliverJournal(List<KingdomChronicleReceipt> Rows,
			KingdomChronicleReceipt Receipt, bool Accomplishment, string Text, string MuralText)
		{
			KingdomChronicleSinkDisposition state = Receipt.JournalState;
			if (KingdomChronicleReceiptRules.IsSettled(state)) return true;
			if (!Accomplishment)
			{
				Receipt.JournalState = state == KingdomChronicleSinkDisposition.Attempting
					? KingdomChronicleSinkDisposition.Lost
					: KingdomChronicleSinkDisposition.Skipped;
				Receipt.Updated = Math.Max(Receipt.Updated, Now());
				return WriteEventReceipts(Rows, "journal-not-requested");
			}
			if (state == KingdomChronicleSinkDisposition.Attempting)
			{
				// Current rows carry their namespaced receipt identity in JournalAccomplishment.ID.
				// Exact one proves the callback landed; zero or duplicate rows stay visibly lost.
				int observed = CountJournalAccomplishments(Receipt.EventId);
				Receipt.JournalState = observed == 1
					? KingdomChronicleSinkDisposition.Delivered
					: KingdomChronicleSinkDisposition.Lost;
				Receipt.Updated = Math.Max(Receipt.Updated, Now());
				bool written = WriteEventReceipts(Rows, observed == 1
					? "journal-reload-confirmed" : "journal-reload-lost");
				if (written && observed != 1) ReportFault(KingdomChronicleRegistryFault.None,
					observed > 1 ? "journal-duplicate-id" : "journal-attempt-uncertain", true);
				return written;
			}
			bool enabled;
			try { enabled = XRL.UI.Options.GetOption("r_TAF_OptionChronicle") != "No"; }
			catch
			{
				ReportFault(KingdomChronicleRegistryFault.None, "journal-option", false);
				return false;
			}
			if (!enabled)
			{
				// Option-off is a frozen terminal choice for this event. Re-enabling does
				// not backlog old journal accomplishments.
				Receipt.JournalState = KingdomChronicleSinkDisposition.Skipped;
				Receipt.Updated = Math.Max(Receipt.Updated, Now());
				return WriteEventReceipts(Rows, "journal-skipped");
			}
			Receipt.JournalState = KingdomChronicleSinkDisposition.Attempting;
			Receipt.Updated = Math.Max(Receipt.Updated, Now());
			if (!WriteEventReceipts(Rows, "journal-intent")) return false;
			if (!TryPrepareJournalProjection(Receipt.EventId, MuralText,
				out string projectedMural, out string gospelText, out MuralWeight weight))
			{
				Receipt.JournalState = KingdomChronicleSinkDisposition.Lost;
				Receipt.Updated = Math.Max(Receipt.Updated, Now());
				return WriteEventReceipts(Rows, "journal-projection-invalid");
			}
			try
			{
				JournalAPI.AddAccomplishment(Text.Capitalize() + ".",
					projectedMural, gospelText, null, "general",
					MuralCategory.CreatesSomething, weight, Receipt.EventId, -1L);
			}
			catch
			{
				if (CountJournalAccomplishments(Receipt.EventId) == 1)
				{
					Receipt.JournalState = KingdomChronicleSinkDisposition.Delivered;
					Receipt.Updated = Math.Max(Receipt.Updated, Now());
					return WriteEventReceipts(Rows, "journal-callback-confirmed");
				}
				Receipt.JournalState = KingdomChronicleSinkDisposition.Lost;
				Receipt.Updated = Math.Max(Receipt.Updated, Now());
				bool written = WriteEventReceipts(Rows, "journal-callback-lost");
				if (written) ReportFault(KingdomChronicleRegistryFault.None,
					"journal-callback-uncertain", true);
				return written;
			}
			if (CountJournalAccomplishments(Receipt.EventId) != 1)
			{
				Receipt.JournalState = KingdomChronicleSinkDisposition.Lost;
				Receipt.Updated = Math.Max(Receipt.Updated, Now());
				return WriteEventReceipts(Rows, "journal-id-mismatch");
			}
			Receipt.JournalState = KingdomChronicleSinkDisposition.Delivered;
			Receipt.Updated = Math.Max(Receipt.Updated, Now());
			return WriteEventReceipts(Rows, "journal-delivered");
		}

		private static bool WriteEventReceipts(List<KingdomChronicleReceipt> Rows,
			string Context)
		{
			if (The.Game == null || Rows == null)
			{
				ReportFault(KingdomChronicleRegistryFault.MalformedRow, Context, false);
				return false;
			}
			for (int i = 0; i < Rows.Count; i++)
			{
				if (!Rows[i].Compact && KingdomChronicleReceiptRules.IsTerminal(Rows[i]))
				{
					KingdomChronicleReceipt compact = KingdomChronicleReceiptRules.Compact(Rows[i]);
					if (compact == null)
					{
						ReportFault(KingdomChronicleRegistryFault.MalformedRow, Context, true);
						return false;
					}
					Rows[i] = compact;
				}
			}
			string value;
			KingdomChronicleRegistryFault fault;
			if (!KingdomChronicleReceiptRules.TryWriteRegistry(Rows, out value, out fault))
			{
				ReportFault(fault, Context,
					fault == KingdomChronicleRegistryFault.TooManyRows
					|| fault == KingdomChronicleRegistryFault.RegistryTooLong);
				return false;
			}
			try
			{
				The.Game.SetStringGameState(EventRegistryState, value);
				if (string.Equals(The.Game.GetStringGameState(EventRegistryState, ""), value,
					StringComparison.Ordinal)) return true;
			}
			catch { }
			ReportFault(KingdomChronicleRegistryFault.MalformedRow, Context + "-write", true);
			return false;
		}

	}
}
