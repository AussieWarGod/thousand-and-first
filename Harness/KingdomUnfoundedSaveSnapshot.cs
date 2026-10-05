using System;
using System.Globalization;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// What the unfounded real save observed (#272, #271): the saved game, the ground the player
	/// stood on, the dormant lifecycle frame and the exact lifecycle and carry bytes. A separate
	/// fresh profile compares the cold-loaded world against it; it is never applied to that world.
	/// </summary>
	internal sealed class KingdomUnfoundedSaveSnapshot
	{
		internal const string Prefix = "taf-unfounded-save-v1:";
		internal const int MaxWireChars = 512;
		private const int MaxZoneChars = 128;

		internal readonly string GameId;
		internal readonly string ZoneId;
		internal readonly int LifecycleFrame;
		internal readonly string LifecycleSha256;
		internal readonly string CarrySha256;
		internal readonly long TimeTicks;

		internal KingdomUnfoundedSaveSnapshot(string GameId, string ZoneId, int LifecycleFrame,
			string LifecycleSha256, string CarrySha256, long TimeTicks)
		{
			this.GameId = GameId;
			this.ZoneId = ZoneId;
			this.LifecycleFrame = LifecycleFrame;
			this.LifecycleSha256 = LifecycleSha256;
			this.CarrySha256 = CarrySha256;
			this.TimeTicks = TimeTicks;
		}

		internal static bool TryEncode(KingdomUnfoundedSaveSnapshot Value, out string Wire)
		{
			Wire = null;
			if (!Valid(Value)) return false;
			string wire = Prefix + Value.GameId + ";" + Value.ZoneId + ";"
				+ Value.LifecycleFrame.ToString(CultureInfo.InvariantCulture) + ";" + Value.LifecycleSha256
				+ ";" + Value.CarrySha256 + ";" + Value.TimeTicks.ToString(CultureInfo.InvariantCulture);
			if (wire.Length > MaxWireChars) return false;
			Wire = wire;
			return true;
		}

		internal static bool TryDecode(string Wire, out KingdomUnfoundedSaveSnapshot Value)
		{
			Value = null;
			if (Wire == null || Wire.Length > MaxWireChars
				|| !Wire.StartsWith(Prefix, StringComparison.Ordinal)) return false;
			string[] fields = Wire.Substring(Prefix.Length).Split(';');
			if (fields.Length != 6 || !Digits(fields[2], 2) || !Digits(fields[5], 19)) return false;
			int frame;
			long ticks;
			if (!int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out frame)
				|| !long.TryParse(fields[5], NumberStyles.None, CultureInfo.InvariantCulture, out ticks))
				return false;
			KingdomUnfoundedSaveSnapshot value = new KingdomUnfoundedSaveSnapshot(fields[0], fields[1],
				frame, fields[3], fields[4], ticks);
			string canonical;
			if (!TryEncode(value, out canonical) || !string.Equals(canonical, Wire, StringComparison.Ordinal))
				return false;
			Value = value;
			return true;
		}

		private static bool Valid(KingdomUnfoundedSaveSnapshot Value)
		{
			Guid id;
			return Value != null && Value.GameId != null && Guid.TryParseExact(Value.GameId, "D", out id)
				&& id.ToString("D") == Value.GameId && Zone(Value.ZoneId)
				&& Value.LifecycleFrame > 0 && Value.LifecycleFrame < 100
				&& Sha(Value.LifecycleSha256) && Sha(Value.CarrySha256) && Value.TimeTicks >= 0L;
		}

		private static bool Zone(string Value)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > MaxZoneChars) return false;
			for (int i = 0; i < Value.Length; i++)
			{
				char c = Value[i];
				if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9'
					|| c == '.' || c == '_' || c == '-')) return false;
			}
			return true;
		}

		private static bool Sha(string Value)
		{
			if (Value == null || Value.Length != 64) return false;
			for (int i = 0; i < Value.Length; i++)
				if (!(Value[i] >= '0' && Value[i] <= '9' || Value[i] >= 'a' && Value[i] <= 'f')) return false;
			return true;
		}

		private static bool Digits(string Value, int MaxDigits)
		{
			if (string.IsNullOrEmpty(Value) || Value.Length > MaxDigits
				|| Value.Length > 1 && Value[0] == '0') return false;
			for (int i = 0; i < Value.Length; i++)
				if (Value[i] < '0' || Value[i] > '9') return false;
			return true;
		}
	}
}
