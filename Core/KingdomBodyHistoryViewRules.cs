using System;
using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Pure, read-only composition of live anatomy and bounded witnessed receipts.</summary>
	public static class KingdomBodyHistoryViewRules
	{
		public const int MaxViewBytes = 16384;

		public static bool TryCompose(KingdomLiveAnatomySnapshot Anatomy,
			KingdomBodyHistoryBook History, out string View, out string Failure)
		{
			View = null;
			Failure = null;
			if (!KingdomBodyHistoryRules.TryView(Anatomy,
				out string anatomyText, out Failure)) return false;
			if (!KingdomBodyHistoryRules.TryValidate(History, out Failure)) return false;
			StringBuilder text = new StringBuilder(anatomyText);
			text.Append("\n\nWitnessed civic body history (oldest first):");
			if (History.Rows.Count == 0) text.Append(" none recorded.");
			List<KingdomBodyHistoryReceipt> chronological =
				new List<KingdomBodyHistoryReceipt>(History.Rows);
			chronological.Sort(delegate(KingdomBodyHistoryReceipt left,
				KingdomBodyHistoryReceipt right)
			{
				int tick = left.WitnessedTick.CompareTo(right.WitnessedTick);
				return tick != 0 ? tick : string.CompareOrdinal(left.ReceiptId, right.ReceiptId);
			});
			for (int i = 0; i < chronological.Count; i++)
			{
				KingdomBodyHistoryReceipt row = chronological[i];
				text.Append("\n- At tick ").Append(row.WitnessedTick).Append(": ")
					.Append(row.Description);
				if (string.Equals(row.ResidentIdentity, Anatomy.ResidentIdentity,
					StringComparison.Ordinal)
					&& string.Equals(row.BodyObjectId, Anatomy.BodyObjectId,
						StringComparison.Ordinal)) text.Append(" [current form]");
				else text.Append(" [former form]");
			}
			return Finish(text, out View, out Failure);
		}

		public static bool TryComposeWithoutHistory(KingdomLiveAnatomySnapshot Anatomy,
			string Reason, out string View, out string Failure)
		{
			View = null;
			Failure = null;
			if (!KingdomBodyHistoryRules.TryView(Anatomy,
				out string anatomyText, out Failure)) return false;
			StringBuilder text = new StringBuilder(anatomyText);
			text.Append("\n\nWitnessed civic body history: unavailable. Current anatomy above remains live.");
			if (SafeReason(Reason)) text.Append("\nReason: ").Append(Reason);
			return Finish(text, out View, out Failure);
		}

		private static bool SafeReason(string Reason)
		{
			if (string.IsNullOrWhiteSpace(Reason) || Reason.IndexOf('\0') >= 0) return false;
			try { return new UTF8Encoding(false, true).GetByteCount(Reason) <= 1024; }
			catch (EncoderFallbackException) { return false; }
		}

		private static bool Finish(StringBuilder Text, out string View, out string Failure)
		{
			View = null;
			Failure = null;
			try
			{
				View = Text.ToString();
				if (new UTF8Encoding(false, true).GetByteCount(View) <= MaxViewBytes) return true;
			}
			catch (EncoderFallbackException) { }
			View = null;
			Failure = "body-history view exceeds its bounded text contract";
			return false;
		}
	}
}
