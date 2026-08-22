using System.Collections.Generic;

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
	public static class KingdomDelveRules
	{
		// ==================================================================================
		// What a delve is
		// ==================================================================================

		/// <summary>
		/// The catalogue key of the design that sinks a shaft.
		/// <para>
		/// A key, not a blueprint and not a footprint &mdash; the same one hardcoded thing, and
		/// for the same reason, as <see cref="KingdomRoadRules.GatehouseKey"/>: an author who
		/// re-keys the design keeps every other property and loses only the shaft-awareness, and
		/// swapping this for an authored attribute is a one-line change to <see cref="IsDelve"/>
		/// when the schema grows one. QB-16 ruled that attribute's own wave not yet earned; a
		/// second design that opens ground is what earns it.
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

		// ==================================================================================
		// Judging a shaft, and the sentences the founder is owed
		// ==================================================================================

		/// <summary>Whether the settlement may sink a shaft from the ground the founder is
		/// standing on, and if not, which lack is the reason.</summary>
		public enum DelveVerdict
		{
			Allowed,

			/// <summary>No realm to cut for.</summary>
			NothingFoundedYet,

			/// <summary>Somebody else's ground, or nobody's.</summary>
			GroundIsNotOurs,

			/// <summary>The realm holds this ground and cannot get to it. A shaft is sunk from
			/// somewhere the crew can already stand with their tools.</summary>
			GroundIsUnreached,

			/// <summary>Nothing of the realm's lies directly beneath. A shaft has to be sunk
			/// TO somewhere.</summary>
			NoGroundBelow,

			/// <summary>A shaft already goes down from here, and one hole in one floor is one
			/// hole in one floor.</summary>
			AlreadyDelved
		}

		/// <param name="Founded">Whether the realm exists at all.</param>
		/// <param name="HeadZoneId">The ground the founder is standing on, which is where the
		/// winding gear would go.</param>
		/// <param name="ClaimedZones">The realm's own ground.</param>
		/// <param name="DelvedZones">Zone ids carrying a finished delve.</param>
		public static DelveVerdict JudgeDelve(bool Founded, string HeadZoneId, IEnumerable<string> ClaimedZones,
			IEnumerable<string> DelvedZones)
		{
			if (!Founded)
			{
				return DelveVerdict.NothingFoundedYet;
			}
			List<string> claimed = Parsed(ClaimedZones, out _, out _, out _, out _);
			if (string.IsNullOrEmpty(HeadZoneId) || !claimed.Contains(HeadZoneId))
			{
				return DelveVerdict.GroundIsNotOurs;
			}
			if (Holds(DelvedZones, HeadZoneId))
			{
				return DelveVerdict.AlreadyDelved;
			}
			if (!ReachedZones(ClaimedZones, DelvedZones).Contains(HeadZoneId))
			{
				return DelveVerdict.GroundIsUnreached;
			}
			for (int i = 0; i < claimed.Count; i++)
			{
				if (IsShaftPair(HeadZoneId, claimed[i]))
				{
					return DelveVerdict.Allowed;
				}
			}
			return DelveVerdict.NoGroundBelow;
		}

		/// <summary>
		/// What the founder is told when the shaft will not be sunk. Every branch names the lack
		/// AND what lifts it (STANDARDS 7b).
		/// </summary>
		/// <param name="Verdict">The refusal. <see cref="DelveVerdict.Allowed"/> returns
		/// empty.</param>
		/// <param name="SeatName">The settlement's name.</param>
		public static string DelveRefusal(DelveVerdict Verdict, string SeatName)
		{
			string seat = Seat(SeatName);
			switch (Verdict)
			{
			case DelveVerdict.NothingFoundedYet:
				return "There is no city yet to sink a shaft for. Pour the first water somewhere first.";
			case DelveVerdict.GroundIsNotOurs:
				return "A shaft goes down through " + seat + "'s own floor. Claim this ground first, and then cut.";
			case DelveVerdict.GroundIsUnreached:
				return seat + " owns this ground and has no way to it. Open the stratum above it before you ask for a way below it.";
			case DelveVerdict.NoGroundBelow:
				return "There is nothing of " + seat + "'s under this ground to cut down to. Walk down, claim the rock below, and come back up to sink the shaft to it.";
			case DelveVerdict.AlreadyDelved:
				return "A shaft already goes down from here, and it goes down to the only place under it. Claim further out and sink the next one there.";
			default:
				return "";
			}
		}

		/// <summary>
		/// What the founder is told when a design is asked for on rock the settlement holds and
		/// cannot work. The sentence that makes the delve mean something: the refusal names the
		/// building that lifts it (STANDARDS 7b).
		/// </summary>
		/// <param name="SeatName">The settlement's name.</param>
		/// <param name="DesignName">The design that was asked for, in the catalogue's own
		/// lowercase register.</param>
		public static string RefuseUnreached(string SeatName, string DesignName)
		{
			string design = string.IsNullOrEmpty(DesignName) ? "it" : ("the " + DesignName);
			return Seat(SeatName) + " holds this rock and has no way down to it. A crew can climb to a cave; it cannot carry "
				+ design + " down a cave stair. Sink a delve in the ground above and the deep is the city's to build in.";
		}

		/// <summary>
		/// What a finished shaft says it opened. The other half of 7b: a work that changes what a
		/// settlement can do says so once, in the founder's own words, rather than leaving them to
		/// notice a menu stopped refusing.
		/// </summary>
		/// <param name="SeatName">The settlement's name.</param>
		public static string ShaftOpens(string SeatName)
		{
			return "The delve is cut. " + Seat(SeatName)
				+ " reaches the rock under this ground now: the crews go down by the shaft, and what the deep makes comes up the same way.";
		}

		/// <summary>
		/// What the realm's unopened claims are waiting for, for the status report and the ledger.
		/// One line however many parasangs are waiting, because the answer is the same building
		/// every time.
		/// </summary>
		/// <param name="SeatName">The settlement's name.</param>
		/// <param name="Waiting">How many claimed zones nothing reaches, from
		/// <see cref="UnreachedZones"/>.</param>
		/// <returns>Null when nothing is waiting, so a caller can drop the whole line.</returns>
		public static string UnreachedNote(string SeatName, int Waiting)
		{
			if (Waiting <= 0)
			{
				return null;
			}
			string ground = (Waiting == 1) ? "one parasang of rock" : (Waiting + " parasangs of rock");
			return Seat(SeatName) + " owns " + ground + " it has never opened. Nothing can be raised down there and nothing can be carried out of it until a delve is sunk from the ground above.";
		}

		// ==================================================================================
		// The connection: what the descent costs anyone carrying a load
		// ==================================================================================

		/// <summary>
		/// What a shaft costs the routing metric, as a multiple of an ordinary hop across a zone
		/// boundary.
		/// <para>
		/// Three, and the reason is the ground rather than the arithmetic. A level hop is priced
		/// at half a zone's width because a carrier enters one edge and leaves by another
		/// (<c>KingdomDistanceRules.ZoneTransitCells</c>); a shaft is the whole depth of a stratum,
		/// climbed, with the load on your back and one at a time. The catalogue's own deep section
		/// promises the asymmetry out loud &mdash; "a deep city is hand-cheap and ceilinged" &mdash;
		/// and this is the half of it the haul pays. Named rather than inlined for the same reason
		/// every other metric constant is: a number nobody can find is a number nobody can retune.
		/// </para>
		/// </summary>
		public const int ShaftHopMultiplier = 3;

		/// <summary>What one crossing of a shaft costs, in the same cells
		/// <c>KingdomDistanceRules.ZoneTransitCells</c> is written in.</summary>
		/// <param name="LevelHopCells">What an ordinary hop across a zone boundary costs the
		/// caller's own metric. Zero and below answer zero rather than a negative distance.</param>
		public static int ShaftHopCells(int LevelHopCells)
		{
			return (LevelHopCells <= 0) ? 0 : LevelHopCells * ShaftHopMultiplier;
		}

		// ==================================================================================
		// Shared reads
		// ==================================================================================

		private static string Seat(string SeatName)
		{
			return string.IsNullOrEmpty(SeatName) ? "the settlement" : ("{{C|" + SeatName + "}}");
		}

		private static bool Holds(IEnumerable<string> Zones, string ZoneId)
		{
			if (Zones == null || string.IsNullOrEmpty(ZoneId))
			{
				return false;
			}
			foreach (string zone in Zones)
			{
				if (zone == ZoneId)
				{
					return true;
				}
			}
			return false;
		}

		// Orthogonal neighbours in one stratum: a shared EDGE, never a shared corner. A claim may
		// be taken across a corner (KingdomRules.CoordsAdjacent is Chebyshev) and the ground taken
		// that way is legal ground; nobody can walk a load through the corner to reach it, which is
		// why it has to be reached some other way or not at all.
		private static bool EdgeApart(int XA, int YA, int XB, int YB)
		{
			int dx = (XA > XB) ? (XA - XB) : (XB - XA);
			int dy = (YA > YB) ? (YA - YB) : (YB - YA);
			return dx + dy == 1;
		}

		// Every id that survives a parse, with its coordinates alongside. A malformed id is
		// dropped rather than guessed at: a third party's instanced zone name disables one row of
		// this and never the whole reckoning (STANDARDS 9).
		private static List<string> Parsed(IEnumerable<string> Zones, out List<string> Worlds,
			out List<int> Xs, out List<int> Ys, out List<int> Zs)
		{
			List<string> kept = new List<string>();
			Worlds = new List<string>();
			Xs = new List<int>();
			Ys = new List<int>();
			Zs = new List<int>();
			if (Zones == null)
			{
				return kept;
			}
			foreach (string zone in Zones)
			{
				if (kept.Contains(zone) || !KingdomRules.TryParseZoneID(zone, out var world, out var x, out var y, out var z))
				{
					continue;
				}
				kept.Add(zone);
				Worlds.Add(world);
				Xs.Add(x);
				Ys.Add(y);
				Zs.Add(z);
			}
			return kept;
		}
	}
}
