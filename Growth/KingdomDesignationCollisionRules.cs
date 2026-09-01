using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Pure provenance-aware identity arbitration for normalized designations.</summary>
	public static class KingdomDesignationCollisionRules
	{
		public static bool TryRefused(IReadOnlyList<string> Identities,
			IReadOnlyList<string> Roots, IReadOnlyList<bool> Trusted,
			out HashSet<int> Refused)
		{
			Refused = null;
			if (Identities == null || Roots == null || Trusted == null
				|| Identities.Count != Roots.Count || Identities.Count != Trusted.Count
				|| Identities.Count > KingdomDesignationRules.MaxDesignationsPerZone) return false;
			Dictionary<string, int> identities = new Dictionary<string, int>(StringComparer.Ordinal);
			Dictionary<string, int> roots = new Dictionary<string, int>(StringComparer.Ordinal);
			HashSet<int> refused = new HashSet<int>();
			for (int i = 0; i < Identities.Count; i++)
			{
				if (string.IsNullOrEmpty(Identities[i]) || string.IsNullOrEmpty(Roots[i])) return false;
				Resolve(identities, Identities[i], i, Trusted, refused);
				Resolve(roots, Roots[i], i, Trusted, refused);
			}
			Refused = refused; return true;
		}

		private static void Resolve(Dictionary<string, int> Seen, string Key, int Current,
			IReadOnlyList<bool> Trusted, HashSet<int> Refused)
		{
			if (!Seen.TryGetValue(Key, out int prior)) { Seen.Add(Key, Current); return; }
			if (Trusted[prior] && !Trusted[Current]) { Refused.Add(Current); return; }
			if (!Trusted[prior] && Trusted[Current])
			{
				Refused.Add(prior); Seen[Key] = Current; return;
			}
			Refused.Add(prior); Refused.Add(Current);
		}
	}
}
