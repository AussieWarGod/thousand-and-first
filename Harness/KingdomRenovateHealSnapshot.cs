using System;
using System.Globalization;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// #283 heal route: what session one saw when it saved the stuck renovation, which session two
	/// compares before activation and binds its verdict to. Engine-free, exact and fail-closed:
	/// nothing is trimmed or repaired on decode.
	/// </summary>
	internal sealed class KingdomRenovateHealSnapshot
	{
		internal const string Prefix = "taf-renovate-heal-v1:";
		internal const int MaxWireChars = 1024;
		private const int Fields = 9;

		internal readonly string GameId, ZoneId, JobId, PredecessorId, SuccessorId, Defect,
			ScaffoldIntentId;
		internal readonly int UpgradePhase;
		internal readonly long TimeTicks;

		internal KingdomRenovateHealSnapshot(string GameId, string ZoneId, string JobId,
			string PredecessorId, string SuccessorId, string Defect, int UpgradePhase,
			string ScaffoldIntentId, long TimeTicks)
		{
			this.GameId = GameId; this.ZoneId = ZoneId; this.JobId = JobId;
			this.PredecessorId = PredecessorId; this.SuccessorId = SuccessorId; this.Defect = Defect;
			this.UpgradePhase = UpgradePhase; this.ScaffoldIntentId = ScaffoldIntentId;
			this.TimeTicks = TimeTicks;
		}

		internal static bool TryEncode(KingdomRenovateHealSnapshot Snapshot, out string Wire)
		{
			Wire = null;
			if (!Valid(Snapshot)) return false;
			string wire = Prefix + string.Join(";", new[] { Snapshot.GameId, Snapshot.ZoneId,
				Snapshot.JobId, Snapshot.PredecessorId, Snapshot.SuccessorId, Snapshot.Defect,
				Snapshot.UpgradePhase.ToString(CultureInfo.InvariantCulture), Snapshot.ScaffoldIntentId,
				Snapshot.TimeTicks.ToString(CultureInfo.InvariantCulture) });
			if (wire.Length > MaxWireChars) return false;
			Wire = wire;
			return true;
		}

		internal static bool TryDecode(string Wire, out KingdomRenovateHealSnapshot Snapshot)
		{
			Snapshot = null;
			if (Wire == null || Wire.Length > MaxWireChars
				|| !Wire.StartsWith(Prefix, StringComparison.Ordinal)) return false;
			string[] f = Wire.Substring(Prefix.Length).Split(';');
			int phase;
			long ticks;
			if (f.Length != Fields || !Canonical(f[6], out phase) || !Canonical(f[8], out ticks))
				return false;
			KingdomRenovateHealSnapshot candidate = new KingdomRenovateHealSnapshot(f[0], f[1], f[2],
				f[3], f[4], f[5], phase, f[7], ticks);
			string again;
			if (!TryEncode(candidate, out again) || again != Wire) return false;
			Snapshot = candidate;
			return true;
		}

		private static bool Valid(KingdomRenovateHealSnapshot S)
		{
			return S != null && GuidText(S.GameId) && Token(S.ZoneId) && Hex32(S.JobId)
				&& Token(S.PredecessorId) && Token(S.SuccessorId) && S.PredecessorId != S.SuccessorId
				&& (S.Defect == "A" || S.Defect == "B") && S.UpgradePhase >= 0 && S.UpgradePhase <= 5
				&& Token(S.ScaffoldIntentId) && S.TimeTicks >= 0L;
		}

		private static bool Canonical(string Text, out int Value)
		{
			long wide;
			Value = 0;
			if (!Canonical(Text, out wide) || wide > int.MaxValue) return false;
			Value = (int)wide;
			return true;
		}

		private static bool Canonical(string Text, out long Value)
		{
			Value = 0L;
			return !string.IsNullOrEmpty(Text) && Text.Length <= 19 && (Text == "0" || Text[0] != '0')
				&& long.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out Value)
				&& Value.ToString(CultureInfo.InvariantCulture) == Text;
		}

		private static bool GuidText(string Text)
		{
			if (Text == null || Text.Length != 36) return false;
			for (int i = 0; i < 36; i++)
			{
				char c = Text[i];
				bool dash = i == 8 || i == 13 || i == 18 || i == 23;
				if (dash ? c != '-' : !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) return false;
			}
			return true;
		}

		private static bool Hex32(string Text)
		{
			if (Text == null || Text.Length != 32) return false;
			foreach (char c in Text) if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) return false;
			return true;
		}

		private static bool Token(string Text)
		{
			if (string.IsNullOrEmpty(Text) || Text.Length > 128) return false;
			foreach (char c in Text)
				if (!(c >= '0' && c <= '9' || c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z'
					|| c == '.' || c == '_' || c == '-')) return false;
			return true;
		}
	}
}
