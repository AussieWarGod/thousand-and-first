using System;
using System.Globalization;

namespace ThousandAndFirst.Harness
{
	// Strict LF transport. Graphs remain observations, not deserialized game entities.
	internal static class KingdomUpgradeSnapshotCodec
	{
		internal const string Prefix = "taf-upgrade-save-v1\n";
		internal const string OldPin = "a46b5ada5197cc50d5afcfe5d6c1df7836a76b7e";
		internal const int MaxWireChars = 4194304;
		internal const string RequestFile = "upgrade-save-request.txt";
		internal const string SnapshotFile = "upgrade-save-snapshot.txt";
		internal const string ReceiptFile = "upgrade-save-receipt.txt";
		internal const string FailureFile = "upgrade-save-failure.txt";

		internal static bool ValidCase(string value)
		{ return value == "inheritance" || value == "detached-transition"; }

		internal static bool GameId(string value)
		{ return value != null && Guid.TryParseExact(value, "D", out Guid id) && id.ToString("D") == value; }

		internal static bool Hash(string value)
		{
			if (value == null || value.Length != 64) return false;
			foreach (char c in value) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) return false;
			return true;
		}

		internal static bool TryRequest(string wire, out string scenario)
		{
			scenario = null;
			if (wire == null || wire.Length > 160 || wire.IndexOf('\r') >= 0) return false;
			string[] rows = wire.Split('\n');
			if (rows.Length != 4 || rows[0] != "taf-upgrade-save-request-v1" || rows[1] != OldPin
				|| !ValidCase(rows[2]) || rows[3] != "") return false;
			scenario = rows[2]; return true;
		}

		internal static bool TryEncode(KingdomUpgradeSnapshot value, out string wire)
		{
			wire = null;
			if (value == null || !GameId(value.GameId) || !ValidCase(value.Case) || value.OldPin != OldPin
				|| value.Turns < 0 || value.TimeTicks < 0 || value.ActionTicks < 0 || value.PlayerActionTicks < 0
				|| !KingdomUpgradeGraph.Valid(value.Inheritance) || !KingdomUpgradeGraph.Valid(value.Transition)
				|| !KingdomUpgradeGraph.Valid(value.Legacy)) return false;
			string text = Prefix + value.GameId + "\n" + value.Case + "\n" + value.OldPin + "\n"
				+ Number(value.Turns) + "\n" + Number(value.TimeTicks) + "\n" + Number(value.ActionTicks) + "\n"
				+ Number(value.PlayerActionTicks) + "\n" + value.Inheritance + "\n" + value.Transition + "\n"
				+ value.Legacy + "\n";
			if (text.Length > MaxWireChars) return false;
			wire = text; return true;
		}

		internal static bool TryDecode(string wire, out KingdomUpgradeSnapshot value)
		{
			value = null;
			if (wire == null || wire.Length > MaxWireChars || !wire.StartsWith(Prefix, StringComparison.Ordinal)
				|| wire.IndexOf('\r') >= 0) return false;
			string[] rows = wire.Split('\n');
			if (rows.Length != 12 || rows[11] != "" || !Clock(rows[4], out long turns)
				|| !Clock(rows[5], out long time) || !Clock(rows[6], out long action)
				|| !Clock(rows[7], out long playerAction)) return false;
			KingdomUpgradeSnapshot candidate = new KingdomUpgradeSnapshot(rows[1], rows[2], rows[3],
				turns, time, action, playerAction, rows[8], rows[9], rows[10]);
			if (!TryEncode(candidate, out string canonical) || canonical != wire) return false;
			value = candidate; return true;
		}

		internal static string Receipt(KingdomUpgradeSnapshot snapshot, string primary, string info, string snapshotHash)
		{
			if (!TryEncode(snapshot, out _) || !Hash(primary) || !Hash(info) || !Hash(snapshotHash))
				throw new ArgumentException("invalid upgrade receipt fields");
			return "taf-upgrade-save-receipt-v1\n" + OldPin + "\n" + snapshot.GameId + "\n" + snapshot.Case
				+ "\n" + primary + "\n" + info + "\ncache-bind-after-quit\n" + snapshotHash + "\n";
		}

		private static bool Clock(string value, out long number)
		{
			return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out number)
				&& number >= 0 && Number(number) == value;
		}

		private static string Number(long value) { return value.ToString(CultureInfo.InvariantCulture); }
	}
}
