using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static class KingdomFoundingHeartReservationState
	{
		internal static bool TryExpected(string key, string expected,
			KingdomDurableKeyObservation observed, out bool absent)
		{
			absent = false;
			if (!KingdomFoundingHeartReservationRules.TryRead(key, expected, out _, out _, out _)
				|| !KingdomScenarioStateShape.TryAuthorityText(observed, out string current,
					out bool present, out _)) return false;
			if (present) return string.Equals(current, expected, StringComparison.Ordinal);
			absent = true;
			return true;
		}

		internal static bool TryAudit(
			IEnumerable<KeyValuePair<string, KingdomDurableKeyObservation>> observations,
			int maximumKeys, out Dictionary<string, string> reservations)
		{
			reservations = null;
			if (observations == null || maximumKeys <= 0) return false;
			Dictionary<string, string> complete = new Dictionary<string, string>(StringComparer.Ordinal);
			HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
			int count = 0;
			try
			{
				foreach (KeyValuePair<string, KingdomDurableKeyObservation> row in observations)
				{
					if (count >= maximumKeys || string.IsNullOrEmpty(row.Key) || row.Value == null) return false;
					count++;
					if (!row.Key.StartsWith(KingdomFoundingHeartReservationRules.Prefix,
						StringComparison.Ordinal)) continue;
					if (!keys.Add(row.Key)
						|| !KingdomScenarioStateShape.TryAuthorityText(row.Value, out string raw,
							out bool present, out _) || !present
						|| !KingdomFoundingHeartReservationRules.TryRead(row.Key, raw, out _, out _, out string id)
						|| complete.ContainsKey(id)) return false;
					complete.Add(id, raw);
				}
			}
			catch (Exception) { return false; }
			reservations = complete;
			return true;
		}
	}
}
