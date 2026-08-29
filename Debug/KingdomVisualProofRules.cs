using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Pure, bounded checkpoint and evidence-row rules shared by native visual galleries.</summary>
	public static class KingdomVisualProofRules
	{
		public const int Schema = 1;
		public const int MaxCases = 4096;
		public const byte Unreviewed = 0;
		public const byte Pass = 1;
		public const byte Fail = 2;

		public static byte[] Empty(int Total)
		{
			return Total < 1 || Total > MaxCases ? null : new byte[Total];
		}

		public static string EncodeCheckpoint(string CatalogueDigest, byte[] States)
		{
			if (!ValidDigest(CatalogueDigest) || States == null || States.Length < 1
				|| States.Length > MaxCases) return null;
			byte[] packed = new byte[(States.Length + 3) / 4];
			for (int i = 0; i < States.Length; i++)
			{
				if (States[i] > Fail) return null;
				packed[i / 4] |= (byte)(States[i] << ((i % 4) * 2));
			}
			return "vp1|" + States.Length.ToString(CultureInfo.InvariantCulture) + "|"
				+ CatalogueDigest + "|" + Convert.ToBase64String(packed);
		}

		public static bool TryDecodeCheckpoint(string Raw, int Total, string CatalogueDigest,
			out byte[] States, out string Failure)
		{
			States = null;
			Failure = null;
			if (Total < 1 || Total > MaxCases || !ValidDigest(CatalogueDigest))
				return Refuse("The visual-proof catalogue bounds are invalid.", out Failure);
			if (string.IsNullOrEmpty(Raw))
			{
				States = Empty(Total);
				return true;
			}
			if (Raw.Length > 8192)
				return Refuse("The visual-proof checkpoint exceeds its bounded wire size.", out Failure);
			string[] fields = Raw.Split('|');
			int encodedTotal;
			if (fields.Length != 4 || fields[0] != "vp1"
				|| !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture,
					out encodedTotal) || encodedTotal != Total
				|| fields[1] != Total.ToString(CultureInfo.InvariantCulture)
				|| fields[2] != CatalogueDigest)
				return Refuse("The visual-proof checkpoint belongs to a different catalogue.", out Failure);
			byte[] packed;
			try { packed = Convert.FromBase64String(fields[3]); }
			catch (FormatException)
			{
				return Refuse("The visual-proof checkpoint payload is malformed.", out Failure);
			}
			if (packed.Length != (Total + 3) / 4)
				return Refuse("The visual-proof checkpoint payload has the wrong length.", out Failure);
			byte[] decoded = new byte[Total];
			for (int i = 0; i < Total; i++)
			{
				decoded[i] = (byte)((packed[i / 4] >> ((i % 4) * 2)) & 3);
				if (decoded[i] > Fail)
					return Refuse("The visual-proof checkpoint contains an unknown verdict.", out Failure);
			}
			int unused = Total % 4;
			if (unused != 0 && (packed[packed.Length - 1] >> (unused * 2)) != 0)
				return Refuse("The visual-proof checkpoint has non-canonical trailing bits.", out Failure);
			States = decoded;
			return true;
		}

		public static int Next(byte[] States)
		{
			if (States == null) return -1;
			for (int i = 0; i < States.Length; i++) if (States[i] == Unreviewed) return i;
			return -1;
		}

		public static void Counts(byte[] States, out int Passed, out int Failed, out int Open)
		{
			Passed = 0;
			Failed = 0;
			Open = 0;
			if (States == null) return;
			for (int i = 0; i < States.Length; i++)
				if (States[i] == Pass) Passed++;
				else if (States[i] == Fail) Failed++;
				else Open++;
		}

		public static string ExpectedScreenshot(string Suite, int Number, int Total)
		{
			if (!SafeToken(Suite) || Number < 1 || Number > Total || Total > MaxCases) return null;
			int width = Math.Max(4, Total.ToString(CultureInfo.InvariantCulture).Length);
			return "taf-" + Suite + "-" + Number.ToString(new string('0', width),
				CultureInfo.InvariantCulture) + ".png";
		}

		public static bool ScreenshotMatches(string Path, string Expected)
		{
			if (string.IsNullOrEmpty(Path) || string.IsNullOrEmpty(Expected)) return false;
			string normalized = Path.Replace('\\', '/');
			int slash = normalized.LastIndexOf('/');
			string name = slash < 0 ? normalized : normalized.Substring(slash + 1);
			return string.Equals(name, Expected, StringComparison.Ordinal);
		}

		public static string EvidenceRow(string Suite, int Number, int Total, string CaseId,
			string Receipt, string Digest, string Verdict, string ScreenshotPath, string Note)
		{
			if (!SafeToken(Suite) || Number < 1 || Number > Total || string.IsNullOrEmpty(CaseId)
				|| !SafeToken(Receipt) || !ValidDigest(Digest)
				|| (Verdict != "pass" && Verdict != "fail") || string.IsNullOrEmpty(ScreenshotPath))
				return null;
			return "[TAF visual-evidence]\tschema=1\tsuite=" + Suite + "\tindex="
				+ Number.ToString(CultureInfo.InvariantCulture) + "/"
				+ Total.ToString(CultureInfo.InvariantCulture) + "\tcase64=" + Base64(CaseId)
				+ "\treceipt=" + Receipt + "\tdigest=" + Digest + "\tverdict=" + Verdict
				+ "\tscreenshot64=" + Base64(ScreenshotPath) + "\tnote64=" + Base64(Note ?? "")
				+ "\tcapture=human-asserted";
		}

		private static string Base64(string Value)
		{
			return Convert.ToBase64String(Encoding.UTF8.GetBytes(Value ?? ""));
		}

		private static bool ValidDigest(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= '0' && Value[i] <= '9')
					|| (Value[i] >= 'a' && Value[i] <= 'f'))) return false;
			return true;
		}

		private static bool SafeToken(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > 96) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!((Value[i] >= 'a' && Value[i] <= 'z')
					|| (Value[i] >= '0' && Value[i] <= '9') || Value[i] == '-')) return false;
			return true;
		}

		private static bool Refuse(string Message, out string Failure)
		{
			Failure = Message;
			return false;
		}
	}
}
