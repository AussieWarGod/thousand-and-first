using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomZoningRules
	{
		/// <summary>Whether the WEATHER a design wants reaches this ground: it does not, under the
		/// rock. The derived half of the depth gate, and the older one. What SET a design belongs
		/// to is the authored half and is <see cref="StrataAdmits"/>.</summary>
		public static bool StratumAccepts(bool Underground, bool RequiresSky)
		{
			return !(Underground && RequiresSky);
		}

		/// <summary>What the founder calls the stratum, for the sentence that names it.</summary>
		public static string StratumName(bool Underground)
		{
			return Underground ? "under the rock" : "open sky";
		}

		// ==================================================================================
		// The claim: what widens the ground every gate above is measured against.
		// ==================================================================================

		/// <summary>
		/// Why the founder's claim may not proceed, or <see cref="ClaimVerdict.Allowed"/>. Pure,
		/// so the rule is tabled rather than discovered in the field &mdash; the same bargain
		/// <c>KingdomSettlement.JudgeSecondFounding</c> makes for the founding rite.
		/// <para>
		/// Ordered from the fact nothing can change to the one the founder can answer today:
		/// there is no realm, then this ground is already somebody's, then it is out of reach,
		/// then finally the city is not yet large enough to hold more.
		/// </para>
		/// </summary>
		public enum ClaimVerdict
		{
			Allowed = 0,
			NothingFoundedYet = 1,
			GroundIsAlreadyOurs = 2,
			GroundIsAnotherCitys = 3,
			GroundIsAnotherRealms = 4,
			GroundIsForeign = 5,
			GroundIsNotAdjacent = 6,
			CityHoldsAllItCan = 7
		}

		/// <summary>
		/// How many zones a city of this stage holds at most.
		/// <para>
		/// Not a number chosen here: it is read off the catalogue the brief already wrote. The
		/// eight <c>MinZones</c> designs line up exactly with stage &mdash; the two-zone designs
		/// are <c>MinStage="Village"</c>, the three-zone designs are <c>Town</c>, and the
		/// four-zone designs are <c>City</c> &mdash; so a settlement reaches the ground a design
		/// wants at the same moment it reaches the stage that design wants. A camp claiming its
		/// fourth parasang would have made every one of those gates meaningless.
		/// </para>
		/// <para>
		/// A stage this build does not define reads as one zone: the founding claim, and no
		/// expansion out of a bad cast.
		/// </para>
		/// </summary>
		public static int ZonesForStage(GrowthStage Stage)
		{
			switch (Stage)
			{
			case GrowthStage.Village:
				return 2;
			case GrowthStage.Town:
				return 3;
			case GrowthStage.City:
				return 4;
			case GrowthStage.Camp:
			case GrowthStage.Steading:
				return 1;
			default:
				return 1;
			}
		}

		/// <summary>
		/// Judges whether the founder standing on this ground may take it into the seated city.
		/// </summary>
		/// <param name="Founded">Whether the realm exists at all.</param>
		/// <param name="Stage">What the seated city has become.</param>
		/// <param name="ZonesHeld">Zones the seated city already holds.</param>
		/// <param name="GroundIsOurs">Whether the seated city already holds this zone.</param>
		/// <param name="GroundIsAnotherCitys">Whether the realm's other city holds it. Two cities
		/// claiming one zone would break the seat quietly rather than loudly.</param>
		/// <param name="GroundIsAnotherRealms">Whether a realm that put this founder out still
		/// holds it. That city goes on without them.</param>
		/// <param name="GroundIsForeign">Whether some other faction already answers for it.</param>
		/// <param name="GroundIsAdjacent">Whether it borders ground the seated city holds &mdash;
		/// including the stratum directly above or below, which is what a cellar is.</param>
		public static ClaimVerdict JudgeClaim(bool Founded, GrowthStage Stage, int ZonesHeld, bool GroundIsOurs,
			bool GroundIsAnotherCitys, bool GroundIsAnotherRealms, bool GroundIsForeign, bool GroundIsAdjacent)
		{
			if (!Founded)
			{
				return ClaimVerdict.NothingFoundedYet;
			}
			if (GroundIsOurs)
			{
				return ClaimVerdict.GroundIsAlreadyOurs;
			}
			if (GroundIsAnotherCitys)
			{
				return ClaimVerdict.GroundIsAnotherCitys;
			}
			if (GroundIsAnotherRealms)
			{
				return ClaimVerdict.GroundIsAnotherRealms;
			}
			if (GroundIsForeign)
			{
				return ClaimVerdict.GroundIsForeign;
			}
			if (!GroundIsAdjacent)
			{
				return ClaimVerdict.GroundIsNotAdjacent;
			}
			if (ZonesHeld >= ZonesForStage(Stage))
			{
				return ClaimVerdict.CityHoldsAllItCan;
			}
			return ClaimVerdict.Allowed;
		}

		/// <summary>
		/// What the founder is told when the claim will not proceed. Every branch names the lack
		/// AND what lifts it (STANDARDS 7b): a refusal that only says no teaches nothing.
		/// </summary>
		/// <param name="Verdict">The refusal. <see cref="ClaimVerdict.Allowed"/> returns empty.</param>
		/// <param name="SeatName">The seated city's name.</param>
		/// <param name="Stage">What the seated city has become, for the sentence that names the
		/// rung it would have to reach.</param>
		public static string ClaimRefusal(ClaimVerdict Verdict, string SeatName, GrowthStage Stage)
		{
			string seat = string.IsNullOrEmpty(SeatName) ? "the settlement" : ("{{C|" + SeatName + "}}");
			switch (Verdict)
			{
			case ClaimVerdict.NothingFoundedYet:
				return "There is no city yet to take ground for. Pour the first water somewhere first.";
			case ClaimVerdict.GroundIsAlreadyOurs:
				return "This ground is already " + seat + "'s. Walk out to the parasang next door and claim there.";
			case ClaimVerdict.GroundIsAnotherCitys:
				return "Your other city holds this ground, and one parasang answers to one city. Nothing has changed.";
			case ClaimVerdict.GroundIsAnotherRealms:
				return "This ground is not yours to take any more. Ask that realm to have you back, or walk until the ground answers to nobody.";
			case ClaimVerdict.GroundIsForeign:
				return "This ground already answers to someone else. Nothing is annexed by pouring water on it: a living village is asked into a covenant through the charter rite, and anything else foreign simply is not this rite's to take.";
			case ClaimVerdict.GroundIsNotAdjacent:
				return "A city grows outward from what it already holds. Stand on ground that borders " + seat
					+ " — beside it, or the stratum directly above or below it — and claim there.";
			case ClaimVerdict.CityHoldsAllItCan:
				if (Stage >= GrowthStage.City)
				{
					return seat + " holds " + ZoneCount(ZonesForStage(Stage))
						+ ", which is all the ground one city answers for. Pour again out past the horizon of what you hold if you want more.";
				}
				return seat + " is " + StageWord(Stage) + ", and " + StageWord(Stage) + " holds "
					+ ZoneCount(ZonesForStage(Stage)) + ". Grow into " + StageWord(NextStage(Stage))
					+ " and this ground is yours to take.";
			default:
				return "";
			}
		}

		/// <summary>
		/// What a claim did to the wall line. The brief's "walls move outward as the city spans
		/// zones" is real but quiet: nothing standing is moved (the protection law forbids it)
		/// &mdash; the edge simply stops facing the world, so every wall raised from here goes to
		/// the new outer line and the old line becomes an inner wall. A founder who is not told
		/// that has no way to see it happened.
		/// <para>
		/// A claim that frees no edge says so too, and that is not a bug. <c>FrontierEdges</c>
		/// clears an edge only for an orthogonal neighbour in the same stratum, so ground taken
		/// diagonally across a corner, or straight down into the rock, is legal ground that
		/// leaves the wall line exactly where it was. Saying otherwise would be the lie.
		/// </para>
		/// </summary>
		/// <param name="Before">Edges of the ground the city ALREADY held that faced the world
		/// before the claim, summed across those zones.</param>
		/// <param name="After">The same zones' edges facing the world after it.</param>
		/// <param name="SeatName">The seated city's name.</param>
		public static string ClaimedWallClause(int Before, int After, string SeatName)
		{
			string seat = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			int freed = Before - After;
			if (freed <= 0)
			{
				return "The wall line does not move: this ground touches no side " + seat + " was already walling, so every edge it held still faces the world.";
			}
			return "The wall line moves outward. What " + seat + " raises from here stands on the new edge, and "
				+ ((freed == 1) ? "the side you claimed past becomes an inner wall" : ("the " + freed + " sides you claimed past become inner walls"))
				+ " — nothing already built is moved.";
		}

		/// <summary>How many of the four edges are set.</summary>
		public static int EdgeCount(KingdomRules.Frontier Edges)
		{
			int count = 0;
			if ((Edges & KingdomRules.Frontier.North) != 0) { count++; }
			if ((Edges & KingdomRules.Frontier.South) != 0) { count++; }
			if ((Edges & KingdomRules.Frontier.West) != 0) { count++; }
			if ((Edges & KingdomRules.Frontier.East) != 0) { count++; }
			return count;
		}

		/// <summary>
		/// The line that tells the founder how much ground the city holds and how much a city of
		/// its rung may hold. Said after a claim so the next one is never a surprise.
		/// </summary>
		/// <param name="Held">Parasangs the city holds now.</param>
		/// <param name="Ceiling">What <see cref="ZonesForStage"/> allows it.</param>
		public static string ClaimHoldingLine(int Held, int Ceiling)
		{
			int held = (Held < 0) ? 0 : Held;
			int ceiling = (Ceiling < held) ? held : Ceiling;
			if (held >= ceiling)
			{
				return "{{K|" + ZoneCount(held) + " held, which is all this rung answers for.}}";
			}
			int room = ceiling - held;
			return "{{K|" + ZoneCount(held) + " held; room for " + ((room == 1) ? "one more" : (room + " more")) + " at this rung.}}";
		}

		private static string ZoneCount(int Zones)
		{
			return (Zones == 1) ? "one parasang" : (Zones + " parasangs");
		}

		private static string StageWord(GrowthStage Stage)
		{
			return "a " + Stage.ToString().ToLowerInvariant();
		}

		private static GrowthStage NextStage(GrowthStage Stage)
		{
			// Floors at City rather than running off the end of the ladder: a City that somehow
			// holds its four parasangs is told to grow into a City, which is odd but honest, and
			// is unreachable while ZonesForStage tops out with the ladder.
			return (Stage >= GrowthStage.City) ? GrowthStage.City : (GrowthStage)((int)Stage + 1);
		}

	}
}
