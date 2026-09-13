using System;

namespace ThousandAndFirst.Harness
{
	// External, sealed observer input. Never changes grants or production bootstrap rules.
	internal static class KingdomQuickstartHistoricalGrant
	{
		internal const string FileName = "scenario-historical-quickstart.txt";
		internal const string Header = "taf-quickstart-033-grant-v1";
		internal const string ProductionPin = "e96b50e1c7ce698ed05749963d4fb885ab42d054";
		internal const int MaxBytes = 256;

		internal static bool TryRead(string Text, string GameId, string SnapshotSha256, out int Drams)
		{
			Drams = 0;
			if (Text == null || Text.Length > MaxBytes
				|| !Guid.TryParseExact(GameId, "D", out Guid id) || id.ToString("D") != GameId
				|| SnapshotSha256 == null || SnapshotSha256.Length != 64) return false;
			foreach (char c in SnapshotSha256)
				if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) return false;
			string expected = Header + "\n" + ProductionPin + "\n" + GameId + "\n" + SnapshotSha256 + "\n24\n";
			if (!string.Equals(Text, expected, StringComparison.Ordinal)) return false;
			Drams = 24;
			return true;
		}
	}
}
