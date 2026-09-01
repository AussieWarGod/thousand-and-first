using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>
	/// The way down: which of the ground a realm holds its people can actually get to, what
	/// opens the rest of it, and what the descent costs anyone carrying a load.
	/// <para>
	/// Everything under this file was already half-built when it was written, and saying which
	/// half is the whole point of it. A claim ALREADY reaches the stratum directly above or below
	/// (<c>KingdomRules.CoordsAdjacent</c> with <c>IncludeVertical</c>, the one such call in the
	/// mod, from <c>KingdomFounding.ZonesAdjacent</c>); the catalogue ALREADY has a set that lives
	/// under the rock (Addendum 15, <see cref="KingdomZoningRules.StratumDeep"/>); the carve
	/// bargain ALREADY prices it (<see cref="KingdomPlotRules.UndergroundClearPercent"/> against a
	/// free enclosure). What was missing is the WORK. A founder could walk down a cave stair, pour
	/// water on rock, and have the deep be as much the city as the square it was founded on, for
	/// nothing. The rock was owned and never opened.
	/// </para>
	/// <para>
	/// So the claim is left exactly as it is &mdash; ground is cheap to own and always was &mdash;
	/// and what a shaft buys is the ability to WORK the rock: to carry a wall down, to crew a
	/// vault, to bring up what the deep makes. A crew can climb to a cave. It cannot carry a
	/// gallery down a cave stair.
	/// </para>
	/// <para>
	/// Nothing here touches <c>XRL</c>, and nothing here stores anything. A shaft is not a record
	/// in a register: <b>the building is the fact.</b> Callers hand in the zones the realm holds
	/// and the zones a finished delve stands in, and every question below is answered from those
	/// two lists.
	/// </para>
	/// </summary>
	public static partial class KingdomDelveRules
	{
		// ==================================================================================
		// What a delve is
		// ==================================================================================

		/// <summary>
		/// The catalogue key of the design that sinks a shaft.
		/// <para>
		/// A key, not a blueprint and not a footprint &mdash; the same one hardcoded thing, and
		/// for the same reason, as <see cref="KingdomRoadRules.GatehouseKey"/>: an author who
		/// re-keys the design keeps every other property and loses only the shaft-awareness. The
		/// single v1 shaft role is deliberately a named public key, like the gatehouse role: an
		/// extension can merge, retheme, or replace that design without a private blueprint test.
		/// Multiple simultaneous shaft roles would be a different registry contract and require a
		/// versioned schema rather than an undocumented alias list.
		/// </para>
		/// </summary>
		public const string DelveKey = "delve";

		/// <summary>Whether a design is the one that opens the rock below the ground it stands
		/// on. Case-folded, so a third-party file spelling the key differently still lands.</summary>
		public static bool IsDelve(string Key)
		{
			return !string.IsNullOrEmpty(Key) && Key.Trim().ToLowerInvariant() == DelveKey;
		}

		// ==================================================================================
		// The shaft's two ends
		// ==================================================================================

		/// <summary>
		/// Whether these two zones are the head and the foot of one shaft: the same world column,
		/// exactly one stratum apart, with the foot in rock.
		/// <para>
		/// Straight down and nothing else. The claim admits diagonals
		/// (<c>KingdomRules.CoordsAdjacent</c> is Chebyshev in the horizontal), but a shaft is
		/// sunk on a plumb line and there is no such thing as a diagonal one &mdash; which is the
		/// same narrowing the routing graph already makes for its own reason
		/// (<c>KingdomDistanceRules.StepBetween</c>: "a stairwell goes straight up, never up and
		/// across").
		/// </para>
		/// <para>
		/// The foot must be underground. A stair up the inside of a tower is a different building
		/// in a set nobody has written yet, and calling it a delve would put the sky in the deep's
		/// vocabulary a wave early.
		/// </para>
		/// </summary>
		/// <param name="HeadZoneId">The zone the shaft is sunk FROM &mdash; where the winding gear
		/// stands and where the design is raised.</param>
		/// <param name="FootZoneId">The zone the shaft is sunk TO.</param>
		/// <returns>False for either id malformed, which refuses rather than guessing, exactly as
		/// <c>KingdomRules.TryParseZoneID</c> does.</returns>
		public static bool IsShaftPair(string HeadZoneId, string FootZoneId)
		{
			if (!KingdomRules.TryParseZoneID(HeadZoneId, out var headWorld, out var headX, out var headY, out var headZ)
				|| !KingdomRules.TryParseZoneID(FootZoneId, out var footWorld, out var footX, out var footY, out var footZ))
			{
				return false;
			}
			return headWorld == footWorld && headX == footX && headY == footY
				&& ShaftJoinsStrata(headZ, footZ, ShaftStands: true);
		}

		/// <summary>
		/// Derives the sole canonical foot zone id without consulting or loading the world. Qud's
		/// ordinary zone ids have six dot-separated fields; a delve preserves the first five and
		/// advances only Z. Non-canonical numeric spellings refuse so a durable receipt never gains
		/// two names for one zone.
		/// </summary>
		public static bool TryFootZoneId(string HeadZoneId, out string FootZoneId)
		{
			FootZoneId = null;
			if (string.IsNullOrEmpty(HeadZoneId) || HeadZoneId.Length > 256)
			{
				return false;
			}
			string[] fields = HeadZoneId.Split('.');
			if (fields.Length != 6 || string.IsNullOrEmpty(fields[0]))
			{
				return false;
			}
			int value;
			for (int i = 1; i < fields.Length; i++)
			{
				if (!int.TryParse(fields[i], NumberStyles.Integer, CultureInfo.InvariantCulture,
					out value) || value.ToString(CultureInfo.InvariantCulture) != fields[i])
				{
					return false;
				}
			}
			int z;
			if (!int.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out z)
				|| z == int.MaxValue)
			{
				return false;
			}
			fields[5] = (z + 1).ToString(CultureInfo.InvariantCulture);
			string foot = string.Join(".", fields);
			if (foot.Length > 256 || !IsShaftPair(HeadZoneId, foot))
			{
				return false;
			}
			FootZoneId = foot;
			return true;
		}

		/// <summary>
		/// The same rule with the world column already settled by the caller: whether a shaft
		/// joins these two strata, given that somebody cut one.
		/// <para>
		/// Split out because the routing graph has already answered the geometry
		/// (<c>KingdomDistanceRules.StepBetween</c> named the direction from coordinates it
		/// carries) and re-deriving it from a zone id there would be the same judgment made twice,
		/// in two places, from two sources. This is the one sentence both callers share.
		/// </para>
		/// </summary>
		/// <param name="HeadStratum">The shallower zone's Z.</param>
		/// <param name="FootStratum">The deeper zone's Z. Must be exactly one below the head and
		/// must be rock: a stair up the inside of a tower is not this building.</param>
		/// <param name="ShaftStands">Whether a finished delve stands in the HEAD zone.</param>
		public static bool ShaftJoinsStrata(int HeadStratum, int FootStratum, bool ShaftStands)
		{
			return ShaftStands && FootStratum == HeadStratum + 1 && KingdomPlotRules.IsUnderground(FootStratum);
		}

		/// <summary>
		/// Whether a finished shaft joins these two zones: they are a shaft pair, and the head is
		/// one of the zones a delve stands in.
		/// <para>
		/// This is the whole of what the routing graph needs. <c>KingdomDistanceRules.StepBetween</c>
		/// already answers <c>Up</c> and <c>Down</c> as first-class edges &mdash; verticality was
		/// free in that file from the day it was written &mdash; and this is the predicate that
		/// makes the edge conditional on somebody having cut it.
		/// </para>
		/// </summary>
		/// <param name="HeadZoneId">The shallower of the two.</param>
		/// <param name="FootZoneId">The deeper of the two.</param>
		/// <param name="DelvedZones">Zone ids that carry a FINISHED delve. A shaft under
		/// construction opens nothing; the work is the fact and an unfinished work is not one.
		/// Null reads as none.</param>
		public static bool ShaftJoins(string HeadZoneId, string FootZoneId, IEnumerable<string> DelvedZones)
		{
			return IsShaftPair(HeadZoneId, FootZoneId) && Holds(DelvedZones, HeadZoneId);
		}

		// ==================================================================================
		// Reach: the ground the city can work, as against the ground it merely owns
		// ==================================================================================

		/// <summary>
		/// Every zone of the realm's own ground its people can get a load to and from.
		/// <para>
		/// A flood from the shallowest ground the realm holds. The seed is every claimed zone at
		/// the shallowest stratum present &mdash; on any city founded by pouring water on the
		/// world that is exactly the surface set, and stating it as "the shallowest" rather than
		/// "the surface" means a realm whose ground is somehow all underground reaches its own
		/// works instead of refusing everything it owns. Surface zones seed whether or not they
		/// touch each other, because the world between them is walkable and always was; a realm is
		/// never asked to pave the wilderness.
		/// </para>
		/// <para>
		/// It grows two ways. Sideways, to a claimed zone in the same stratum sharing an EDGE
		/// &mdash; orthogonal only, because a carrier cannot walk through a corner, which is the
		/// same call <c>KingdomDistanceRules.StepBetween</c> makes and for the same reason.
		/// Downward, only where a delve stands (<see cref="ShaftJoins"/>).
		/// </para>
		/// </summary>
		/// <param name="ClaimedZones">The realm's own ground. Null and empty answer empty.</param>
		/// <param name="DelvedZones">Zone ids carrying a finished delve. Null reads as none.</param>
		/// <returns>A fresh list in the order <paramref name="ClaimedZones"/> was written, so the
		/// same city answers the same way every time it is asked.</returns>
		public static List<string> ReachedZones(IEnumerable<string> ClaimedZones, IEnumerable<string> DelvedZones)
		{
			List<string> claimed = Parsed(ClaimedZones, out var worlds, out var xs, out var ys, out var zs);
			List<string> reached = new List<string>();
			if (claimed.Count == 0)
			{
				return reached;
			}
			int shallowest = zs[0];
			for (int i = 1; i < claimed.Count; i++)
			{
				if (zs[i] < shallowest)
				{
					shallowest = zs[i];
				}
			}
			bool[] found = new bool[claimed.Count];
			for (int i = 0; i < claimed.Count; i++)
			{
				found[i] = zs[i] == shallowest;
			}
			bool grew = true;
			while (grew)
			{
				grew = false;
				for (int i = 0; i < claimed.Count; i++)
				{
					if (!found[i])
					{
						continue;
					}
					for (int j = 0; j < claimed.Count; j++)
					{
						if (found[j] || worlds[i] != worlds[j])
						{
							continue;
						}
						bool sideways = zs[i] == zs[j] && EdgeApart(xs[i], ys[i], xs[j], ys[j]);
						bool downward = zs[j] == zs[i] + 1 && xs[i] == xs[j] && ys[i] == ys[j]
							&& KingdomPlotRules.IsUnderground(zs[j]) && Holds(DelvedZones, claimed[i]);
						if (sideways || downward)
						{
							found[j] = true;
							grew = true;
						}
					}
				}
			}
			for (int i = 0; i < claimed.Count; i++)
			{
				if (found[i])
				{
					reached.Add(claimed[i]);
				}
			}
			return reached;
		}

		/// <summary>Whether the realm's people can get a load to and from one zone. Ground the
		/// realm does not hold is never reached, however near it lies.</summary>
		public static bool Reaches(string ZoneId, IEnumerable<string> ClaimedZones, IEnumerable<string> DelvedZones)
		{
			return !string.IsNullOrEmpty(ZoneId) && ReachedZones(ClaimedZones, DelvedZones).Contains(ZoneId);
		}

		/// <summary>
		/// The realm's own ground that nothing reaches: rock it poured water on and never opened.
		/// The list a status line reads to say what a claim is still waiting for (STANDARDS 7b).
		/// </summary>
		/// <returns>A fresh list, empty when every claim is worked ground.</returns>
		public static List<string> UnreachedZones(IEnumerable<string> ClaimedZones, IEnumerable<string> DelvedZones)
		{
			List<string> claimed = Parsed(ClaimedZones, out _, out _, out _, out _);
			List<string> reached = ReachedZones(ClaimedZones, DelvedZones);
			List<string> waiting = new List<string>();
			for (int i = 0; i < claimed.Count; i++)
			{
				if (!reached.Contains(claimed[i]))
				{
					waiting.Add(claimed[i]);
				}
			}
			return waiting;
		}

	}
}
