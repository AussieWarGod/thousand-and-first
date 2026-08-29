using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomChronicleReceiptRules
	{
		public static bool TryWriteRegistry(IList<KingdomChronicleReceipt> Receipts,
			out string Text, out KingdomChronicleRegistryFault Fault)
		{
			Text = null;
			Fault = KingdomChronicleRegistryFault.None;
			if (Receipts == null || Receipts.Count > MaxReceipts)
			{
				Fault = KingdomChronicleRegistryFault.TooManyRows;
				return false;
			}
			try
			{
				StringBuilder result = new StringBuilder(Header);
				HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
				for (int i = 0; i < Receipts.Count; i++)
				{
					KingdomChronicleReceipt receipt = Receipts[i];
					if (!ReceiptValid(receipt) || !ids.Add(receipt.EventId))
					{
						Fault = KingdomChronicleRegistryFault.MalformedRow;
						return false;
					}
					string row = WriteRow(receipt);
					if (row == null || row.Length > MaxActiveRowChars)
					{
						Fault = KingdomChronicleRegistryFault.MalformedRow;
						return false;
					}
					if ((long)result.Length + row.Length + 1L > MaxRegistryChars)
					{
						Fault = KingdomChronicleRegistryFault.RegistryTooLong;
						return false;
					}
					result.Append('\n').Append(row);
				}
				Text = result.ToString();
				return true;
			}
			catch
			{
				Fault = KingdomChronicleRegistryFault.MalformedRow;
				Text = null;
				return false;
			}
		}

		private static string WriteRow(KingdomChronicleReceipt Receipt)
		{
			string fingerprint = Receipt.LegacyBlocked ? "-" : Receipt.Fingerprint;
			if (!Receipt.Compact)
			{
				return "a|" + Encode(Receipt.EventId) + "|" + fingerprint + "|"
					+ Encode(Receipt.Official) + "|" + Encode(Receipt.Outsider) + "|"
					+ Receipt.OfficialBefore + "|" + Receipt.OfficialAfter + "|"
					+ Receipt.OutsiderBefore + "|" + Receipt.OutsiderAfter + "|"
					+ ((int)Receipt.OfficialState).ToString(CultureInfo.InvariantCulture) + "|"
					+ ((int)Receipt.OutsiderState).ToString(CultureInfo.InvariantCulture) + "|"
					+ ((int)Receipt.JournalState).ToString(CultureInfo.InvariantCulture) + "|"
					+ Receipt.Updated.ToString(CultureInfo.InvariantCulture) + "|"
					+ (Receipt.LegacyBlocked ? "1" : "0");
			}
			string job;
			string coordinate;
			string tail = "|" + fingerprint + "|"
				+ ((int)Receipt.OfficialState).ToString(CultureInfo.InvariantCulture) + "|"
				+ ((int)Receipt.OutsiderState).ToString(CultureInfo.InvariantCulture) + "|"
				+ ((int)Receipt.JournalState).ToString(CultureInfo.InvariantCulture) + "|"
				+ Receipt.Updated.ToString(CultureInfo.InvariantCulture) + "|"
				+ (Receipt.LegacyBlocked ? "1" : "0");
			if (TryConstructionIdentity(Receipt.EventId, out job, out coordinate))
				return "tc|" + job + "|" + Encode(coordinate) + tail;
			return "tg|" + Encode(Receipt.EventId) + tail;
		}

		public static bool ReceiptValid(KingdomChronicleReceipt Receipt)
		{
			if (Receipt == null || string.IsNullOrEmpty(Receipt.EventId)
				|| Receipt.EventId.Length > MaxEventIdChars || !EncodedFits(Receipt.EventId,
					MaxEncodedIdChars) || !IsListDisposition(Receipt.OfficialState)
				|| !IsListDisposition(Receipt.OutsiderState)
				|| !IsJournalDisposition(Receipt.JournalState)
				|| Receipt.Updated < 0L) return false;
			if (Receipt.LegacyBlocked)
			{
				return Receipt.Compact && Receipt.Fingerprint == null
					&& Receipt.OfficialState == KingdomChronicleSinkDisposition.Lost
					&& Receipt.OutsiderState == KingdomChronicleSinkDisposition.Lost
					&& Receipt.JournalState == KingdomChronicleSinkDisposition.Lost
					&& PayloadEmpty(Receipt);
			}
			if (!IsSha256(Receipt.Fingerprint)) return false;
			if (Receipt.Compact)
				return IsTerminal(Receipt) && PayloadEmpty(Receipt);
			// A terminal active row is valid recovery input. The engine writer compacts it
			// before persistence, but accepting it keeps a manually interrupted/older v3
			// write from poisoning the whole exact registry.
			return !string.IsNullOrEmpty(Receipt.Official)
				&& Receipt.Official.Length <= MaxEntryChars
				&& EncodedFits(Receipt.Official, MaxEncodedEntryChars)
				&& !string.IsNullOrEmpty(Receipt.Outsider)
				&& Receipt.Outsider.Length <= MaxEntryChars
				&& EncodedFits(Receipt.Outsider, MaxEncodedEntryChars)
				&& IsSha256(Receipt.OfficialBefore) && IsSha256(Receipt.OfficialAfter)
				&& IsSha256(Receipt.OutsiderBefore) && IsSha256(Receipt.OutsiderAfter);
		}

		private static bool PayloadEmpty(KingdomChronicleReceipt Receipt)
		{
			return Receipt.Official == null && Receipt.Outsider == null
				&& Receipt.OfficialBefore == null && Receipt.OfficialAfter == null
				&& Receipt.OutsiderBefore == null && Receipt.OutsiderAfter == null;
		}

		private static string Encode(string Value)
		{
			return Convert.ToBase64String(StrictUtf8.GetBytes(Value ?? ""));
		}

		private static bool Decode(string Value, int MaxEncodedChars, int MaxDecodedChars,
			int MaxDecodedBytes, out string Result)
		{
			Result = null;
			if (Value == null || Value.Length > MaxEncodedChars || (Value.Length & 3) != 0)
				return false;
			try
			{
				byte[] bytes = Convert.FromBase64String(Value);
				if (bytes.Length > MaxDecodedBytes) return false;
				Result = StrictUtf8.GetString(bytes);
				return Result.Length <= MaxDecodedChars;
			}
			catch
			{
				Result = null;
				return false;
			}
		}

		private static bool EncodedFits(string Value, int Maximum)
		{
			if (Value == null) return false;
			try
			{
				long bytes = StrictUtf8.GetByteCount(Value);
				return ((bytes + 2L) / 3L) * 4L <= Maximum;
			}
			catch { return false; }
		}

		private static bool TryState(string Text, out KingdomChronicleSinkDisposition State)
		{
			State = KingdomChronicleSinkDisposition.None;
			int raw;
			if (!TryInt(Text, out raw) || raw < 0 || raw > 5) return false;
			State = (KingdomChronicleSinkDisposition)raw;
			return true;
		}

		private static bool TryBool(string Text, out bool Value)
		{
			Value = false;
			if (Text == "0") return true;
			if (Text == "1")
			{
				Value = true;
				return true;
			}
			return false;
		}

		private static bool TryInt(string Text, out int Value)
		{
			Value = 0;
			return !string.IsNullOrEmpty(Text) && Text.Length <= 10
				&& int.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out Value)
				&& Value >= 0 && Value.ToString(CultureInfo.InvariantCulture) == Text;
		}

		private static bool TryLong(string Text, out long Value)
		{
			Value = 0L;
			return !string.IsNullOrEmpty(Text) && Text.Length <= 19
				&& long.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out Value)
				&& Value >= 0L && Value.ToString(CultureInfo.InvariantCulture) == Text;
		}

		public static bool IsSha256(string Value)
		{
			return IsLowerHex(Value, Sha256HexChars);
		}

		private static bool IsLowerHex(string Value, int Length)
		{
			if (Value == null || Value.Length != Length) return false;
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
			}
			return true;
		}

		private static int Count(string Text, char Character)
		{
			int count = 0;
			for (int i = 0; i < Text.Length; i++) if (Text[i] == Character) count++;
			return count;
		}	}
}
