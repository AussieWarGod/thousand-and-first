using System;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst.Harness
{
	/// <summary>What the lifecycle save session witnessed, carried to the cold-load session.
	/// It is evidence, never authority: the load session re-reads every one of these from the
	/// loaded game and compares, rather than restoring anything from here.</summary>
	internal sealed class KingdomQuickstartLifecycleSnapshot
	{
		internal readonly string GameId, RealmId, CityId, ZoneId, JobId, PlotId, BuildingId, DesignKey;
		internal readonly int X, Y, Timber, StoredWater;
		internal readonly long Turns;

		internal KingdomQuickstartLifecycleSnapshot(string gameId, string realmId, string cityId,
			string zoneId, string jobId, string plotId, string buildingId, string designKey,
			int x, int y, int timber, int storedWater, long turns)
		{
			GameId = gameId; RealmId = realmId; CityId = cityId; ZoneId = zoneId; JobId = jobId;
			PlotId = plotId; BuildingId = buildingId; DesignKey = designKey;
			X = x; Y = y; Timber = timber; StoredWater = storedWater; Turns = turns;
		}
	}

	/// <summary>
	/// A deliberately plain, strict wire: a prefix, then thirteen pipe-separated fields. No field
	/// may contain a pipe or any control character, so a value that could smuggle a separator is
	/// refused at encode time rather than silently re-parsed into different fields later.
	/// </summary>
	internal static class KingdomQuickstartLifecycleSnapshotCodec
	{
		internal const string Prefix = "taf-lifecycle-save-v1:";
		internal const int MaxWireChars = 8192;
		internal const int MaxFieldChars = 512;
		internal const int Fields = 13;

		internal static bool MatchesPrefix(string Wire)
		{
			return Wire != null && Wire.StartsWith(Prefix, StringComparison.Ordinal);
		}

		internal static bool TryEncode(KingdomQuickstartLifecycleSnapshot Value, out string Wire)
		{
			Wire = null;
			if (Value == null) return false;
			string[] fields =
			{
				Value.GameId, Value.RealmId, Value.CityId, Value.ZoneId, Value.JobId, Value.PlotId,
				Value.BuildingId, Value.DesignKey,
				Value.X.ToString(CultureInfo.InvariantCulture),
				Value.Y.ToString(CultureInfo.InvariantCulture),
				Value.Timber.ToString(CultureInfo.InvariantCulture),
				Value.StoredWater.ToString(CultureInfo.InvariantCulture),
				Value.Turns.ToString(CultureInfo.InvariantCulture)
			};
			StringBuilder wire = new StringBuilder(Prefix);
			for (int i = 0; i < fields.Length; i++)
			{
				if (!Clean(fields[i])) return false;
				if (i > 0) wire.Append('|');
				wire.Append(fields[i]);
			}
			if (wire.Length > MaxWireChars) return false;
			Wire = wire.ToString();
			return true;
		}

		internal static bool TryDecode(string Wire, out KingdomQuickstartLifecycleSnapshot Value)
		{
			Value = null;
			if (!MatchesPrefix(Wire) || Wire.Length > MaxWireChars) return false;
			string[] fields = Wire.Substring(Prefix.Length).Split('|');
			if (fields.Length != Fields) return false;
			foreach (string field in fields) if (!Clean(field)) return false;
			if (!int.TryParse(fields[8], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int x)
				|| !int.TryParse(fields[9], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int y)
				|| !int.TryParse(fields[10], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int timber)
				|| !int.TryParse(fields[11], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int water)
				|| !long.TryParse(fields[12], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long turns))
				return false;
			if (x < 0 || y < 0 || timber < 0 || water < 0 || turns < 0) return false;
			if (!Guid.TryParseExact(fields[0], "D", out Guid id) || id.ToString("D") != fields[0]) return false;
			Value = new KingdomQuickstartLifecycleSnapshot(fields[0], fields[1], fields[2], fields[3],
				fields[4], fields[5], fields[6], fields[7], x, y, timber, water, turns);
			return true;
		}

		/// <summary>Present, bounded, and free of separators and control characters.</summary>
		private static bool Clean(string Field)
		{
			if (string.IsNullOrEmpty(Field) || Field.Length > MaxFieldChars) return false;
			foreach (char value in Field)
				if (value == '|' || char.IsControl(value)) return false;
			return true;
		}
	}
}
