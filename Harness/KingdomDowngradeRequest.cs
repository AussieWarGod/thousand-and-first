using System;
using System.Globalization;

namespace ThousandAndFirst.Harness
{
	internal sealed class KingdomDowngradeRequest
	{
		internal const string Header = "taf-downgrade-observe-v1";
		internal const string FileName = "taf-downgrade-request.txt";
		internal const string ReportName = "downgrade-reader-report.txt";
		internal const string OldPin = "a46b5ada5197cc50d5afcfe5d6c1df7836a76b7e";
		internal const int MaximumRequestBytes = 2048;
		internal const int MaximumSlotBytes = 262144;
		internal readonly string Root, Origin, SourcePin;
		private readonly Slot First, Second;

		internal sealed class Slot
		{
			internal readonly char Name;
			internal readonly int Count;
			internal readonly string Sha256;
			internal bool Present => Count > 0;
			internal Slot(char name, int count, string hash)
			{ Name = name; Count = count; Sha256 = hash; }
			internal string Compose() => Name + (Present ? " " + Count.ToString(CultureInfo.InvariantCulture)
				+ " " + Sha256 : " absent");
		}

		private KingdomDowngradeRequest(string root, string origin, string pin, Slot first, Slot second)
		{ Root = root; Origin = origin; SourcePin = pin; First = first; Second = second; }
		internal Slot At(int index)
		{
			if (index != 0 && index != 1) throw new ArgumentOutOfRangeException(nameof(index));
			return index == 0 ? First : Second;
		}
		internal string Compose() => Header + "\n" + Root + "\n" + Origin + "\n" + SourcePin
			+ "\n" + OldPin + "\n" + First.Compose() + "\n" + Second.Compose() + "\n";

		internal static bool TryParse(string text, out KingdomDowngradeRequest request)
		{
			request = null;
			if (text == null || text.Length > MaximumRequestBytes) return false;
			for (int i = 0; i < text.Length; i++)
				if (text[i] != '\n' && (text[i] < 32 || text[i] > 126)) return false;
			string[] rows = text.Split('\n');
			if (rows.Length != 8 || rows[0] != Header || rows[7] != "" || !ValidRoot(rows[1])
				|| !ValidId(rows[2]) || !Hex(rows[3], 40) || rows[4] != OldPin
				|| !TrySlot(rows[5], 'a', out Slot first) || !TrySlot(rows[6], 'b', out Slot second)
				|| !first.Present && !second.Present) return false;
			var result = new KingdomDowngradeRequest(rows[1], rows[2], rows[3], first, second);
			if (result.Compose() != text) return false;
			request = result; return true;
		}

		private static bool TrySlot(string row, char name, out Slot slot)
		{
			slot = null;
			if (row == name + " absent") { slot = new Slot(name, 0, null); return true; }
			string[] fields = row.Split(' ');
			if (fields.Length != 3 || fields[0] != name.ToString()
				|| !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int count)
				|| count <= 0 || count > MaximumSlotBytes
				|| count.ToString(CultureInfo.InvariantCulture) != fields[1] || !Hex(fields[2], 64)) return false;
			slot = new Slot(name, count, fields[2]); return true;
		}

		internal static bool ValidRoot(string root)
		{
			const string prefix = @"C:\taf-scenario.";
			if (root == null || !root.StartsWith(prefix, StringComparison.Ordinal)
				|| root.Length <= prefix.Length || root.Length > prefix.Length + 64) return false;
			for (int i = prefix.Length; i < root.Length; i++) if (!AlphaNumeric(root[i])) return false;
			return true;
		}
		private static bool ValidId(string value)
		{
			if (value.Length == 0 || value.Length > 96) return false;
			for (int i = 0; i < value.Length; i++)
				if (!AlphaNumeric(value[i]) && value[i] != '_' && value[i] != '-') return false;
			return true;
		}
		private static bool AlphaNumeric(char c) => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z'
			|| c >= '0' && c <= '9';
		internal static bool Hex(string value, int count)
		{
			if (value == null || value.Length != count) return false;
			for (int i = 0; i < value.Length; i++)
				if (!(value[i] >= '0' && value[i] <= '9' || value[i] >= 'a' && value[i] <= 'f')) return false;
			return true;
		}
	}
}
