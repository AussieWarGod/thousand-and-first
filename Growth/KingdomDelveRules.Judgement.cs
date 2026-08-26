using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomDelveRules
	{
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
