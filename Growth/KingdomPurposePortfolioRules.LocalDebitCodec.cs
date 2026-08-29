using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomPurposePortfolioRules
	{
		private const int LocalDebitFields = 13;
		private const int DebitLineFields = 10;

		public static string EncodeLocalDebit(KingdomPurposeLocalDebitReceipt Receipt)
		{
			if (!ValidLocalDebit(Receipt)) return null;
			string lines = EncodeDebitLines(Receipt.Lines);
			if (lines == null) return null;
			return EncodeFields(new string[]
			{
				"1", Receipt.PairId, N(Receipt.PairEpoch), Receipt.OperationId,
				Receipt.SourceSettlementId, Receipt.SourceZoneId, Receipt.SourceWorkId,
				Receipt.SourceInputStoreId, N(Receipt.WaterRequested),
				N(Receipt.FoodRequested), Receipt.MaterialRequested, lines,
				"purpose-local-debit"
			});
		}

		public static bool TryDecodeLocalDebit(string Encoded,
			out KingdomPurposeLocalDebitReceipt Receipt)
		{
			Receipt = null;
			if (!TryDecodeFields(Encoded, LocalDebitFields, out string[] f)
				|| f[0] != "1" || f[12] != "purpose-local-debit"
				|| !Long(f[2], out long epoch) || !Int(f[8], out int water)
				|| !Int(f[9], out int food) || !TryDecodeDebitLines(f[11], out var lines))
				return false;
			Receipt = new KingdomPurposeLocalDebitReceipt
			{
				PairId = f[1], PairEpoch = epoch, OperationId = f[3],
				SourceSettlementId = f[4], SourceZoneId = f[5], SourceWorkId = f[6],
				SourceInputStoreId = f[7], WaterRequested = water,
				FoodRequested = food, MaterialRequested = f[10], Lines = lines
			};
			return ValidLocalDebit(Receipt) && EncodeLocalDebit(Receipt) == Encoded;
		}

		private static string EncodeDebitLines(IList<KingdomPurposeDebitLine> Lines)
		{
			if (Lines == null || Lines.Count < 1 || Lines.Count > MaxDebitLines) return null;
			StringBuilder encoded = new StringBuilder("pdl1");
			for (int i = 0; i < Lines.Count; i++)
			{
				string line = EncodeDebitLine(Lines[i]);
				if (line == null) return null;
				encoded.Append(';').Append(line.Length).Append(':').Append(line);
				if (encoded.Length > MaxReceiptChars) return null;
			}
			return encoded.ToString();
		}

		private static bool TryDecodeDebitLines(string Encoded,
			out List<KingdomPurposeDebitLine> Lines)
		{
			Lines = new List<KingdomPurposeDebitLine>();
			if (string.IsNullOrEmpty(Encoded) || Encoded.Length > MaxReceiptChars
				|| !Encoded.StartsWith("pdl1", System.StringComparison.Ordinal)) return false;
			int at = 4;
			while (at < Encoded.Length)
			{
				if (Lines.Count >= MaxDebitLines || Encoded[at++] != ';') return false;
				int colon = Encoded.IndexOf(':', at);
				if (colon < at || colon - at > 8
					|| !Int(Encoded.Substring(at, colon - at), out int length)
					|| length < 1 || colon + 1 + length > Encoded.Length) return false;
				string row = Encoded.Substring(colon + 1, length);
				if (!TryDecodeDebitLine(row, out var line)) return false;
				Lines.Add(line);
				at = colon + 1 + length;
			}
			return Lines.Count > 0;
		}

		private static string EncodeDebitLine(KingdomPurposeDebitLine Line)
		{
			if (!ValidDebitLine(Line)) return null;
			return EncodeFields(new string[] { "1", N((int)Line.Kind), Line.ContainerId,
				Line.ObjectId, Line.Blueprint, N(Line.Before), N(Line.After), N(Line.TypeIndex),
				N(Line.Capacity), "purpose-debit-line" });
		}

		private static bool TryDecodeDebitLine(string Encoded, out KingdomPurposeDebitLine Line)
		{
			Line = null;
			if (!TryDecodeFields(Encoded, DebitLineFields, out string[] f) || f[0] != "1"
				|| f[9] != "purpose-debit-line" || !Int(f[1], out int kind)
				|| !Int(f[5], out int before) || !Int(f[6], out int after)
				|| !SignedInt(f[7], out int type) || !Int(f[8], out int capacity)) return false;
			Line = new KingdomPurposeDebitLine { Kind = (KingdomPurposeDebitKind)kind,
				ContainerId = f[2], ObjectId = f[3], Blueprint = f[4], Before = before,
				After = after, TypeIndex = type, Capacity = capacity };
			return ValidDebitLine(Line) && EncodeDebitLine(Line) == Encoded;
		}

		private static bool SignedInt(string Value, out int Parsed)
		{
			return int.TryParse(Value, NumberStyles.AllowLeadingSign,
				CultureInfo.InvariantCulture, out Parsed);
		}
	}
}
