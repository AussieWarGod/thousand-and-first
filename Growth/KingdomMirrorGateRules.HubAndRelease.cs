using System;

namespace ThousandAndFirst
{
	internal static partial class KingdomMirrorGateRules
	{
		/// <summary>
		/// Re-keys the whole realm onto one city's arch: every other arch answers the capital, and
		/// the capital answers back.
		/// <para>
		/// <b>This is QB-1 cashed in, and it is one pass over one column.</b> The provisional this
		/// register was built under said the hub constraint would be "retrofitted as a re-keying
		/// when the capital exists (a gate re-dedication, not a data loss)". It is: no arch is
		/// visited, no zone is loaded, no row is added and no row is dropped &mdash;
		/// <c>next.Length</c> is always <c>rows.Length</c>, and every row keeps the key and the city
		/// it came in with, in the order it came in. Only <see cref="KingdomGateRow.Partner"/> moves.
		/// </para>
		/// <para>
		/// <b>Cities without arches are untouched, and so is a realm whose capital has none.</b> A
		/// capital that keeps no arch is not a hub, and forcing one would mean either inventing a
		/// row for a building that does not exist or tearing down crossings the founder built and
		/// is still using. So the register is handed back exactly as it stands, and the caller says
		/// so once (STANDARDS 7b) rather than leaving a founder to notice that a political act did
		/// nothing.
		/// </para>
		/// <para>
		/// <b>Why the hub answers ONE spoke rather than all of them.</b> A row carries a single
		/// partner because vanilla's own <c>DestinationKey</c> is a single game-state key: an arch
		/// goes one place. Every spoke landing at the capital is therefore exact. A new hub initially
		/// answers the first spoke in register order, without a draw; a founder may then choose another
		/// lawful spoke through <see cref="TrySelectHubDestination"/>. Re-running this reconciliation
		/// preserves that choice while it remains a registered spoke.
		/// </para>
		/// <para>
		/// <b>Nothing goes dark.</b> Darkness in this file means the works could not pay
		/// (<see cref="JudgeHold"/>), and inventing a dark span for a political act would be a timer
		/// of our own wearing a costume, which Addendum 8 forbids. The re-key's cost is real and is
		/// paid at once: a crossing the founder built between two other cities now lands somewhere
		/// else, and they are told that before they consent.
		/// </para>
		/// </summary>
		/// <param name="rows">The register as it stands. Not modified.</param>
		/// <param name="CapitalCity">The city keeping the crown.</param>
		/// <param name="next">The register afterwards; the input array on any refusal.</param>
		/// <param name="rekeyed">How many rows changed what they answer. Zero when the realm was
		/// already hubbed here, which is the ordinary case for a re-run.</param>
		/// <param name="hubKey">The capital's own arch, or the empty string on a refusal.</param>
		/// <returns><see cref="KingdomGateVerdict.Joined"/> when the realm is hubbed;
		/// <see cref="KingdomGateVerdict.Offered"/> when the capital's arch is the only one there is;
		/// <see cref="KingdomGateVerdict.RefusedUnkeyed"/> when the capital keeps no arch;
		/// <see cref="KingdomGateVerdict.RefusedNamed"/> when the city could not be read.</returns>
		internal static KingdomGateVerdict TryHub(KingdomGateRow[] rows, string CapitalCity, out KingdomGateRow[] next, out int rekeyed, out string hubKey)
		{
			next = rows ?? new KingdomGateRow[0];
			rekeyed = 0;
			hubKey = "";
			if (!Storable(CapitalCity))
			{
				return KingdomGateVerdict.RefusedNamed;
			}
			int hub = IndexOfCity(next, CapitalCity);
			if (hub < 0)
			{
				return KingdomGateVerdict.RefusedUnkeyed;
			}
			hubKey = next[hub].Key;
			KingdomGateRow[] built = new KingdomGateRow[next.Length];
			Array.Copy(next, built, next.Length);
			string selectedSpoke = built[hub].Partner;
			string firstSpoke = "";
			bool selectedStillLawful = false;
			for (int i = 0; i < built.Length; i++)
			{
				if (i == hub)
				{
					continue;
				}
				if (firstSpoke.Length == 0)
				{
					firstSpoke = built[i].Key;
				}
				if (string.Equals(built[i].Key, selectedSpoke, StringComparison.Ordinal))
					selectedStillLawful = true;
				if (!string.Equals(built[i].Partner, hubKey, StringComparison.Ordinal))
				{
					built[i] = built[i].WithPartner(hubKey);
					rekeyed++;
				}
			}
			string outward = selectedStillLawful ? selectedSpoke : firstSpoke;
			if (!string.Equals(built[hub].Partner, outward, StringComparison.Ordinal))
			{
				built[hub] = built[hub].WithPartner(outward);
				rekeyed++;
			}
			next = built;
			return (firstSpoke.Length == 0) ? KingdomGateVerdict.Offered : KingdomGateVerdict.Joined;
		}

		/// <summary>
		/// What the founder is told when the realm's arches have been re-keyed onto the capital.
		/// Says the NUMBER, because a founder who has just moved a crown wants to know how much of
		/// their road network moved with it, and says it only when something actually moved.
		/// </summary>
		/// <param name="Capital">The city the arches now answer.</param>
		/// <param name="Rekeyed">How many arches changed what they answer.</param>
		internal static string HubbedLine(string Capital, int Rekeyed)
		{
			if (Rekeyed <= 0)
			{
				return "";
			}
			return "{{C|" + Rekeyed + ((Rekeyed == 1) ? " arch is" : " arches are") + " re-keyed. The realm's crossings answer "
				+ Named(Capital) + " now, and not one of them was touched to do it.}}";
		}

		/// <summary>The same moment, dated, for the chronicle.</summary>
		internal static string HubbedTelling(string Capital)
		{
			return "the realm's arches turned to face " + Named(Capital) + ", and every road in the kingdom became a road to one place";
		}

		/// <summary>
		/// STANDARDS 7b for the one way a re-key can do nothing: the capital keeps no arch, so
		/// there is no hub to hang the realm on. Named rather than passed over, because the founder
		/// asked for a thing and would otherwise be left to work out that it did not happen.
		/// </summary>
		internal static string NoArchAtCapitalLine(string Capital)
		{
			return "{{y|" + Named(Capital) + " keeps no arch, so the realm's crossings are left exactly as they were. "
				+ "Raise a mirror-gate in the capital and key it, and every other arch will answer it.}}";
		}

		/// <summary>
		/// Takes an arch back out of the register, and unkeys whatever was answering it.
		/// <para>
		/// The arch itself stands exactly where it stands &mdash; nothing player-placed is destroyed
		/// or moved by any of this (the protection law); the crossing simply stops answering.
		/// </para>
		/// </summary>
		/// <param name="rows">The register as it stands. Not modified.</param>
		/// <param name="key">The arch to unkey.</param>
		/// <param name="next">The register afterwards; the input array on any refusal.</param>
		/// <param name="orphan">The key of the arch left waiting, or the empty string when none
		/// was answering. The caller tells that city, once.</param>
		internal static KingdomGateVerdict TryRelease(KingdomGateRow[] rows, string key, out KingdomGateRow[] next, out string orphan)
		{
			next = rows ?? new KingdomGateRow[0];
			orphan = "";
			int at = IndexOfKey(next, key);
			if (at < 0)
			{
				return KingdomGateVerdict.RefusedUnkeyed;
			}
			KingdomGateRow[] built = new KingdomGateRow[next.Length - 1];
			int kept = 0;
			for (int i = 0; i < next.Length; i++)
			{
				if (i == at)
				{
					continue;
				}
				KingdomGateRow row = next[i];
				if (string.Equals(row.Partner, key, StringComparison.Ordinal))
				{
					orphan = row.Key;
					row = row.WithPartner("");
				}
				built[kept++] = row;
			}
			next = built;
			return KingdomGateVerdict.Released;
		}
	}
}
