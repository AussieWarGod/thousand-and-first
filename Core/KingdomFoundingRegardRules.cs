using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Frozen first-founding reputation and retry-safe publication. Version one
	/// retains observation-only history; version two also initializes faction-to-realm regard.</summary>
	internal static class KingdomFoundingRegardRules
	{
		internal const int MaxEncodedLength = 262144;
		private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

		internal static bool TryEncode(int version, IList<KeyValuePair<string, int>> rows,
			out string encoded)
		{
			encoded = null;
			if ((version != 1 && version != 2) || rows == null ||
				rows.Count > KingdomStandingRules.MaxRelationships) return false;
			StringBuilder text = new StringBuilder(version == 2 ? "v2" : "v1");
			string previous = null;
			try
			{
				foreach (KeyValuePair<string, int> row in rows)
				{
					if (!KingdomStandingRules.EligibleForeignFaction(row.Key, null) ||
						(previous != null && StringComparer.Ordinal.Compare(previous, row.Key) >= 0))
						return false;
					text.Append(';').Append(Convert.ToBase64String(StrictUtf8.GetBytes(row.Key)))
						.Append(':').Append(row.Value.ToString(CultureInfo.InvariantCulture));
					if (text.Length > MaxEncodedLength) return false;
					previous = row.Key;
				}
			}
			catch (EncoderFallbackException) { return false; }
			encoded = text.ToString();
			return true;
		}

		internal static bool TryDecode(string encoded, out int version,
			out List<KeyValuePair<string, int>> rows)
		{
			version = 0; rows = null;
			if (string.IsNullOrEmpty(encoded) || encoded.Length > MaxEncodedLength) return false;
			string[] parts = encoded.Split(';');
			int parsedVersion = parts[0] == "v2" ? 2 : parts[0] == "v1" ? 1 : 0;
			if (parsedVersion == 0 || parts.Length - 1 > KingdomStandingRules.MaxRelationships)
				return false;
			var decoded = new List<KeyValuePair<string, int>>(parts.Length - 1);
			try
			{
				for (int i = 1; i < parts.Length; i++)
				{
					int split = parts[i].IndexOf(':');
					if (split <= 0 || split != parts[i].LastIndexOf(':') ||
						!int.TryParse(parts[i].Substring(split + 1), NumberStyles.AllowLeadingSign,
							CultureInfo.InvariantCulture, out int value)) return false;
					string name = StrictUtf8.GetString(Convert.FromBase64String(parts[i].Substring(0, split)));
					decoded.Add(new KeyValuePair<string, int>(name, value));
				}
			}
			catch (FormatException) { return false; }
			catch (DecoderFallbackException) { return false; }
			if (!TryEncode(parsedVersion, decoded, out string canonical) || canonical != encoded)
				return false;
			version = parsedVersion; rows = decoded;
			return true;
		}

		internal static bool TryPreparePublication(int version,
			IList<KeyValuePair<string, int>> frozen, Dictionary<string, int> standings,
			Dictionary<string, int> policy, Dictionary<string, int> remainders,
			Dictionary<string, int> observations, out Dictionary<string, int> nextStandings,
			out Dictionary<string, int> nextObservations)
		{
			nextStandings = null; nextObservations = null;
			if (!TryEncode(version, frozen, out string ignored) || standings == null ||
				policy == null || policy.Count != 0 || remainders == null || remainders.Count != 0 ||
				observations == null || (version == 1 && standings.Count != 0)) return false;
			var desired = new Dictionary<string, int>(StringComparer.Ordinal);
			foreach (KeyValuePair<string, int> row in frozen) desired.Add(row.Key, row.Value);
			if (!ExactSubset(standings, desired) || !ExactSubset(observations, desired)) return false;
			nextStandings = version == 2 ? new Dictionary<string, int>(desired, StringComparer.Ordinal)
				: new Dictionary<string, int>(StringComparer.Ordinal);
			nextObservations = desired;
			return true;
		}

		private static bool ExactSubset(Dictionary<string, int> actual, Dictionary<string, int> desired)
		{
			if (actual.Count > desired.Count) return false;
			foreach (KeyValuePair<string, int> row in actual)
				if (!desired.TryGetValue(row.Key, out int value) || value != row.Value) return false;
			return true;
		}
	}
}
