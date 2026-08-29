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
		public static bool TryConstructionIdentity(string EventId, out string JobId,
			out string Coordinate)
		{
			JobId = null;
			Coordinate = null;
			const string prefix = "construction:";
			if (string.IsNullOrEmpty(EventId) || EventId.Length > MaxEventIdChars
				|| !EventId.StartsWith(prefix, StringComparison.Ordinal)
				|| EventId.Length <= prefix.Length + 33) return false;
			string job = EventId.Substring(prefix.Length, 32);
			if (!IsLowerHex(job, 32) || EventId[prefix.Length + 32] != ':') return false;
			string coordinate = EventId.Substring(prefix.Length + 33);
			if (string.IsNullOrEmpty(coordinate)
				|| coordinate.Length > MaxConstructionCoordinateChars) return false;
			JobId = job;
			Coordinate = coordinate;
			return string.Equals(EventId, prefix + JobId + ":" + Coordinate,
				StringComparison.Ordinal);
		}

		public static KingdomChronicleReceipt Compact(KingdomChronicleReceipt Receipt)
		{
			if (!IsTerminal(Receipt)) return null;
			KingdomChronicleReceipt copy = Receipt.Copy();
			copy.Compact = true;
			copy.Official = null;
			copy.Outsider = null;
			copy.OfficialBefore = null;
			copy.OfficialAfter = null;
			copy.OutsiderBefore = null;
			copy.OutsiderAfter = null;
			return copy;
		}

		public static bool TryParseRegistry(string Text,
			out List<KingdomChronicleReceipt> Receipts, out bool MigratedLegacy,
			out KingdomChronicleRegistryFault Fault)
		{
			Receipts = new List<KingdomChronicleReceipt>();
			MigratedLegacy = false;
			Fault = KingdomChronicleRegistryFault.None;
			if (string.IsNullOrEmpty(Text)) return true;
			if (Text.Length > MaxRegistryChars)
			{
				Fault = KingdomChronicleRegistryFault.RawTooLong;
				return false;
			}
			try
			{
				if (Text == "v1" || Text.StartsWith("v1\n", StringComparison.Ordinal))
				{
					bool valid = TryParseLegacy(Text, Receipts, out Fault);
					MigratedLegacy = valid;
					return valid;
				}
				if (!(Text == Header || Text.StartsWith(Header + "\n", StringComparison.Ordinal)))
				{
					Fault = Text.StartsWith("taf-chronicle|", StringComparison.Ordinal)
						? KingdomChronicleRegistryFault.UnknownVersion
						: KingdomChronicleRegistryFault.MalformedHeader;
					return false;
				}
				return TryParseV3(Text, Receipts, out Fault);
			}
			catch
			{
				Receipts.Clear();
				MigratedLegacy = false;
				Fault = KingdomChronicleRegistryFault.MalformedRow;
				return false;
			}
		}

		private static bool TryParseV3(string Text, List<KingdomChronicleReceipt> Receipts,
			out KingdomChronicleRegistryFault Fault)
		{
			Fault = KingdomChronicleRegistryFault.None;
			int separators = Count(Text, '\n');
			if (separators > MaxReceipts)
			{
				Fault = KingdomChronicleRegistryFault.TooManyRows;
				return false;
			}
			string[] lines = Text.Split('\n');
			if (lines.Length != separators + 1 || lines[0] != Header)
			{
				Fault = KingdomChronicleRegistryFault.MalformedHeader;
				return false;
			}
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 1; i < lines.Length; i++)
			{
				KingdomChronicleReceipt receipt;
				if (!TryParseV3Row(lines[i], out receipt))
				{
					Fault = KingdomChronicleRegistryFault.MalformedRow;
					Receipts.Clear();
					return false;
				}
				if (!ids.Add(receipt.EventId))
				{
					Fault = KingdomChronicleRegistryFault.DuplicateIdentity;
					Receipts.Clear();
					return false;
				}
				Receipts.Add(receipt);
			}
			return true;
		}

	}
}
