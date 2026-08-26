using System;
using System.Collections.Generic;
using ThousandAndFirst.Api;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free rules for live identity facts in Addendum 17. Culture is what a people knows;
	/// species, genotype, and bounded vanilla body conditions are what a body is. All are counts on
	/// one settlement and none is learned knowledge: when the last bearer leaves, the corresponding
	/// roster key leaves too.
	/// </summary>
	public static class KingdomResidentIdentityRules
	{
		/// <summary>Built-in namespaces carried in the same exact body receipt as extension keys.
		/// They extend the existing identity lane; they are not a second resident roster.</summary>
		public const string KindGenotype = "genotype";
		public const string KindBody = "body";

		/// <summary>Bounded body conditions derived from vanilla facts, not handcrafted species
		/// lists. These names are durable architecture selector vocabulary.</summary>
		public const string BodyRobot = "robot";
		public const string BodyWet = "wet-bodied";
		public const string BodyBroad = "broad-bodied";

		/// <summary>More than the settlement population ceiling, while still bounding a corrupt or
		/// third-party vocabulary before it reaches a save or a roster read.</summary>
		public const int MaxFactsPerKind = 128;

		/// <summary>Vanilla names are short. This leaves ample room for a modded name without
		/// allowing one body property to become an unbounded save payload.</summary>
		public const int MaxFactNameLength = 128;

		/// <summary>A defensive count ceiling. Ordinary truth never exceeds the population cap.</summary>
		public const int MaxFactCount = 100000;

		/// <summary>Separator used only inside a citizen body's exact extension-key receipt.
		/// <see cref="KingdomApiRules.IdentityKey"/> refuses this character, so the receipt is
		/// unambiguous without escaping or an unbounded serializer.</summary>
		public const char ReceiptSeparator = '|';

		/// <summary>Worst legal receipt: 128 complete 128-character keys plus separators.</summary>
		public const int MaxReceiptLength =
			(MaxFactsPerKind * KingdomApiRules.MaxIdentityKeyLength) + MaxFactsPerKind - 1;

		/// <summary>
		/// One culture/species name in the exact grammar the keeper roster can round-trip. The
		/// returned value is folded because roster matching is case-insensitive by contract.
		/// </summary>
		public static string CanonicalName(string Kind, string Name)
		{
			string key = KingdomZoningRules.ComposeKey(Kind, Name);
			string value = KingdomZoningRules.NameOf(key);
			return value != null && value.Length <= MaxFactNameLength ? value : null;
		}

		/// <summary>
		/// Canonical, bounded copy of one tally. Invalid/zero rows are dropped, case aliases are
		/// combined, and overflow is clamped. Input is never mutated.
		/// </summary>
		public static Dictionary<string, int> CanonicalTallies(
			IDictionary<string, int> Source, string Kind)
		{
			Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.Ordinal);
			if (Source == null)
			{
				return result;
			}
			List<string> names = new List<string>(Source.Keys);
			names.Sort(StringComparer.Ordinal);
			for (int i = 0; i < names.Count; i++)
			{
				string name = CanonicalName(Kind, names[i]);
				int count = Source[names[i]];
				if (name == null || count <= 0)
				{
					continue;
				}
				result.TryGetValue(name, out int before);
				if (before == 0 && result.Count >= MaxFactsPerKind)
				{
					continue;
				}
				long combined = (long)before + count;
				result[name] = combined > MaxFactCount ? MaxFactCount : (int)combined;
			}
			return result;
		}

		/// <summary>Validates a complete built-in or extension-owned roster key. The owner prefix must
		/// already be the API's canonical kind and re-authoring the key through that owner must
		/// reproduce it exactly. This keeps a corrupt receipt from smuggling a foreign or case-aliased
		/// key into a city tally.</summary>
		public static string CanonicalIdentityKey(string Key)
		{
			if (string.IsNullOrEmpty(Key) || Key.Length > KingdomApiRules.MaxIdentityKeyLength
				|| Key.IndexOf(ReceiptSeparator) >= 0)
			{
				return null;
			}
			int colon = Key.IndexOf(':');
			if (colon <= 0 || colon == Key.Length - 1)
			{
				return null;
			}
			string owner = Key.Substring(0, colon);
			if (!string.Equals(owner, KingdomApiRules.Kind(owner), StringComparison.Ordinal))
			{
				return null;
			}
			string canonical = KingdomApiRules.IdentityKey(owner, Key);
			return string.Equals(canonical, Key, StringComparison.Ordinal) ? canonical : null;
		}

		/// <summary>Canonical, bounded copy of direct built-in/extension-key tallies.</summary>
		public static Dictionary<string, int> CanonicalIdentityTallies(
			IDictionary<string, int> Source)
		{
			Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.Ordinal);
			if (Source == null)
			{
				return result;
			}
			List<string> keys = new List<string>(Source.Keys);
			keys.Sort(StringComparer.Ordinal);
			for (int i = 0; i < keys.Count; i++)
			{
				string key = CanonicalIdentityKey(keys[i]);
				int count = Source[keys[i]];
				if (key == null || count <= 0)
				{
					continue;
				}
				result.TryGetValue(key, out int before);
				if (before == 0 && result.Count >= MaxFactsPerKind)
				{
					continue;
				}
				long combined = (long)before + count;
				result[key] = combined > MaxFactCount ? MaxFactCount : (int)combined;
			}
			return result;
		}

		/// <summary>Canonical, sorted, distinct and bounded identity-key set.</summary>
		public static List<string> CanonicalIdentityKeys(IEnumerable<string> Source)
		{
			List<string> result = new List<string>();
			if (Source == null)
			{
				return result;
			}
			foreach (string candidate in Source)
			{
				string key = CanonicalIdentityKey(candidate);
				if (key != null && !result.Contains(key) && result.Count < MaxFactsPerKind)
				{
					result.Add(key);
				}
			}
			result.Sort(StringComparer.Ordinal);
			return result;
		}

		/// <summary>
		/// Built-in projection for one body. Genotype comes from Qud's <c>GetGenotype()</c>; robot,
		/// water-bound, and broad-body conditions come from vanilla facts read by the engine adapter.
		/// False/blank facts mint nothing. Result uses the same canonical, sorted, bounded receipt as
		/// extension projections.
		/// </summary>
		public static List<string> BuiltInIdentityKeys(string Genotype, bool Robot,
			bool WetBodied, bool BroadBodied)
		{
			List<string> keys = new List<string>();
			string genotype = KingdomApiRules.IdentityKey(KindGenotype, Genotype);
			if (genotype != null) keys.Add(genotype);
			if (Robot) keys.Add(KindBody + ":" + BodyRobot);
			if (WetBodied) keys.Add(KindBody + ":" + BodyWet);
			if (BroadBodied) keys.Add(KindBody + ":" + BodyBroad);
			return CanonicalIdentityKeys(keys);
		}

		/// <summary>Sorted positive names belonging to one identity-key namespace. Invalid rows,
		/// other namespaces, aliases, and zero counts cannot become architecture context.</summary>
		public static List<string> IdentityNames(IDictionary<string, int> Tallies, string Kind)
		{
			List<string> result = new List<string>();
			string kind = KingdomApiRules.Kind(Kind);
			if (kind.Length == 0) return result;
			string prefix = kind + ":";
			Dictionary<string, int> canonical = CanonicalIdentityTallies(Tallies);
			foreach (KeyValuePair<string, int> row in canonical)
			{
				if (row.Value > 0 && row.Key.StartsWith(prefix, StringComparison.Ordinal))
					result.Add(row.Key.Substring(prefix.Length));
			}
			result.Sort(StringComparer.Ordinal);
			return result;
		}

		/// <summary>Sorted positive fact names for a culture/species tally. This is the bounded set
		/// consumed by architecture selection; it deliberately returns names rather than roster
		/// keys because selector XML already supplies the dimension.</summary>
		public static List<string> FactNames(IDictionary<string, int> Tallies, string Kind)
		{
			Dictionary<string, int> canonical = CanonicalTallies(Tallies, Kind);
			List<string> result = new List<string>();
			foreach (KeyValuePair<string, int> row in canonical)
				if (row.Value > 0) result.Add(row.Key);
			result.Sort(StringComparer.Ordinal);
			return result;
		}

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
