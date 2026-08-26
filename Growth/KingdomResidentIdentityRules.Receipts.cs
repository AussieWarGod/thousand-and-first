using System;
using System.Collections.Generic;
using ThousandAndFirst.Api;

namespace ThousandAndFirst
{
	public static partial class KingdomResidentIdentityRules
	{
		/// <summary>Encodes the exact keys counted for one body. Invalid, duplicate, or excess
		/// entries never reach the save property.</summary>
		public static string EncodeIdentityKeys(IEnumerable<string> Keys)
		{
			List<string> keys = CanonicalIdentityKeys(Keys);
			return keys.Count == 0 ? null : string.Join(ReceiptSeparator.ToString(), keys);
		}

		/// <summary>Decodes a body receipt without trusting its length or grammar.</summary>
		public static List<string> DecodeIdentityKeys(string Receipt)
		{
			if (string.IsNullOrEmpty(Receipt) || Receipt.Length > MaxReceiptLength)
			{
				return new List<string>();
			}
			return CanonicalIdentityKeys(Receipt.Split(ReceiptSeparator));
		}

		/// <summary>Replaces all built-in/extension keys formerly counted for one body with the body's
		/// fresh frozen projection. Set comparison makes retries idempotent and decrements the last
		/// bearer exactly.</summary>
		public static bool TransitionIdentityKeys(Dictionary<string, int> Tallies,
			IEnumerable<string> Former, IEnumerable<string> Current)
		{
			if (Tallies == null)
			{
				return false;
			}
			List<string> former = CanonicalIdentityKeys(Former);
			List<string> current = CanonicalIdentityKeys(Current);
			bool changed = false;
			for (int i = 0; i < former.Count; i++)
			{
				string key = former[i];
				if (current.Contains(key)) continue;
				if (Tallies.TryGetValue(key, out int count) && count > 0)
				{
					if (count == 1) Tallies.Remove(key);
					else Tallies[key] = count - 1;
					changed = true;
				}
			}
			for (int i = 0; i < current.Count; i++)
			{
				string key = current[i];
				if (former.Contains(key)) continue;
				Tallies.TryGetValue(key, out int count);
				if (count > 0 || Tallies.Count < MaxFactsPerKind)
				{
					Tallies[key] = count >= MaxFactCount ? MaxFactCount : count + 1;
					changed = true;
				}
			}
			return changed;
		}

		/// <summary>Sorted direct roster keys whose live tally remains positive.</summary>
		public static List<string> IdentityRosterKeys(IDictionary<string, int> Tallies)
		{
			Dictionary<string, int> canonical = CanonicalIdentityTallies(Tallies);
			List<string> result = new List<string>();
			foreach (KeyValuePair<string, int> row in canonical)
				if (row.Value > 0) result.Add(row.Key);
			result.Sort(StringComparer.Ordinal);
			return result;
		}

		/// <summary>
		/// Replaces one body's former fact with its current one. Idempotence comes from the body
		/// carrying the former fact: applying the same transition twice changes nothing the second
		/// time. Returns whether the tally changed.
		/// </summary>
		public static bool Transition(Dictionary<string, int> Tallies, string Kind,
			string Former, string Current)
		{
			if (Tallies == null)
			{
				return false;
			}
			string former = CanonicalName(Kind, Former);
			string current = CanonicalName(Kind, Current);
			if (string.Equals(former, current, StringComparison.Ordinal))
			{
				return false;
			}
			bool changed = false;
			if (former != null && Tallies.TryGetValue(former, out int old) && old > 0)
			{
				if (old == 1)
				{
					Tallies.Remove(former);
				}
				else
				{
					Tallies[former] = old - 1;
				}
				changed = true;
			}
			if (current != null)
			{
				Tallies.TryGetValue(current, out int count);
				if (count > 0 || Tallies.Count < MaxFactsPerKind)
				{
					Tallies[current] = count >= MaxFactCount ? MaxFactCount : count + 1;
					changed = true;
				}
			}
			return changed;
		}

		/// <summary>Sorted live roster keys for a tally. Invalid rows never reach the roster.</summary>
		public static List<string> RosterKeys(IDictionary<string, int> Tallies, string Kind)
		{
			Dictionary<string, int> canonical = CanonicalTallies(Tallies, Kind);
			List<string> result = new List<string>(canonical.Count);
			foreach (KeyValuePair<string, int> row in canonical)
			{
				if (row.Value <= 0)
				{
					continue;
				}
				string key = KingdomZoningRules.ComposeKey(Kind, row.Key);
				if (key != null)
				{
					result.Add(key);
				}
			}
			result.Sort(StringComparer.Ordinal);
			return result;
		}
	}
}
