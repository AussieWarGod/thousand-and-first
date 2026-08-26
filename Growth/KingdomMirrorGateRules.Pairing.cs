using System;

namespace ThousandAndFirst
{
	internal static partial class KingdomMirrorGateRules
	{
		internal static string PartnerOf(KingdomGateRow[] rows, string key)
		{
			int at = IndexOfKey(rows, key);
			return (at < 0) ? "" : rows[at].Partner;
		}

		/// <summary>
		/// Keys an arch: enters it in the register, and joins it to whatever is already waiting.
		/// <para>
		/// <b>One keyed arch to a city.</b> A crossing is between two of the founder's cities
		/// (END-STATE-CITIES-RESEARCH &sect;4.4), so a second arch keyed in a city already keeping
		/// one would answer ground the first one already answers. It is refused, and the refusal
		/// names the arch that is in the way.
		/// </para>
		/// <para>
		/// The waiting arch this one joins is the <b>first unpaired row in another city, in
		/// register order</b>. Deterministic without a sort and without a draw: at v1's two cities
		/// there is at most one, and the rule still reads the same when there are more.
		/// </para>
		/// </summary>
		/// <param name="rows">The register as it stands. Not modified.</param>
		/// <param name="key">This arch's location key.</param>
		/// <param name="city">The city it stands in.</param>
		/// <param name="next">The register as it stands afterwards; the input array on any refusal.</param>
		/// <param name="partner">The key this arch was joined to, or the empty string when it is
		/// only waiting.</param>
		/// <returns>The verdict. Never throws.</returns>
		internal static KingdomGateVerdict TryDedicate(KingdomGateRow[] rows, string key, string city, out KingdomGateRow[] next, out string partner)
		{
			next = rows ?? new KingdomGateRow[0];
			partner = "";
			if (!Storable(key) || !Storable(city))
			{
				return KingdomGateVerdict.RefusedNamed;
			}
			if (IndexOfKey(next, key) >= 0)
			{
				return KingdomGateVerdict.RefusedAlreadyKeyed;
			}
			if (IndexOfCity(next, city) >= 0)
			{
				return KingdomGateVerdict.RefusedCityKeyed;
			}
			if (next.Length >= MaxGates)
			{
				return KingdomGateVerdict.RefusedFull;
			}
			int waiting = -1;
			for (int i = 0; i < next.Length; i++)
			{
				if (next[i].Partner.Length == 0 && !string.Equals(next[i].City, city, StringComparison.OrdinalIgnoreCase))
				{
					waiting = i;
					break;
				}
			}
			KingdomGateRow[] built = new KingdomGateRow[next.Length + 1];
			Array.Copy(next, built, next.Length);
			built[next.Length] = new KingdomGateRow(key, city, (waiting >= 0) ? built[waiting].Key : "");
			if (waiting < 0)
			{
				next = built;
				return KingdomGateVerdict.Offered;
			}
			partner = built[waiting].Key;
			built[waiting] = built[waiting].WithPartner(key);
			next = built;
			return KingdomGateVerdict.Joined;
		}

		/// <summary>
		/// Points two arches at each other, whatever they were pointed at before.
		/// <para>
		/// <b>This is the re-keying seam QB-1 names.</b> Pairwise v1 calls it with the two ends of
		/// one crossing. The capital wave calls it once per arch with the capital's own key, and
		/// the realm becomes a hub without a single arch being visited, rebuilt, or forgotten
		/// &mdash; which is why the register carries the pairing and neither arch does.
		/// </para>
		/// <para>
		/// Whatever either end answered before is released in the same breath, on both sides, so
		/// the register can never hold a row pointing at an arch that is pointing somewhere else.
		/// </para>
		/// </summary>
		/// <param name="rows">The register as it stands. Not modified.</param>
		/// <param name="keyA">One arch.</param>
		/// <param name="keyB">The other. Must be a different arch in the register.</param>
		/// <param name="next">The register afterwards; the input array on any refusal.</param>
		/// <returns><see cref="KingdomGateVerdict.Joined"/>, or a refusal naming what was wrong.</returns>
		internal static KingdomGateVerdict TryPair(KingdomGateRow[] rows, string keyA, string keyB, out KingdomGateRow[] next)
		{
			next = rows ?? new KingdomGateRow[0];
			int a = IndexOfKey(next, keyA);
			int b = IndexOfKey(next, keyB);
			if (a < 0 || b < 0)
			{
				return KingdomGateVerdict.RefusedUnkeyed;
			}
			if (a == b)
			{
				// An arch that answered itself would teleport a founder to where they stand, which
				// is not a crossing and would read as a broken one.
				return KingdomGateVerdict.RefusedNamed;
			}
			if (string.Equals(next[a].City, next[b].City, StringComparison.OrdinalIgnoreCase))
			{
				return KingdomGateVerdict.RefusedCityKeyed;
			}
			KingdomGateRow[] built = new KingdomGateRow[next.Length];
			Array.Copy(next, built, next.Length);
			for (int i = 0; i < built.Length; i++)
			{
				if (i == a || i == b)
				{
					continue;
				}
				// Anything that answered either end is unkeyed by the same act, so no third arch is
				// left holding a key that now answers somebody else.
				if (string.Equals(built[i].Partner, keyA, StringComparison.Ordinal) || string.Equals(built[i].Partner, keyB, StringComparison.Ordinal))
				{
					built[i] = built[i].WithPartner("");
				}
			}
			built[a] = built[a].WithPartner(built[b].Key);
			built[b] = built[b].WithPartner(built[a].Key);
			next = built;
			return KingdomGateVerdict.Joined;
		}

	}
}
