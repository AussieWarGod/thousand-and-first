using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomLodgingRules
	{
		// ==================================================================================
		// Addendum 4c -- feelings scale with closeness. How two people feel about each other
		// always bears on whether they can live together; how much it bears is the quarters.
		// You cannot jam five different believers into one bunkhouse and have it be fine, and
		// the same five in a street of stone houses are neighbours who nod. The one
		// old flat floor's VALUE is promoted into the ladder while its public name is retired:
		// the live Private rung and the three tighter rungs are what a
		// tent, a hut and a house always should have asked for.
		//
		// WHY THIS READS THE SAME FEELINGS AS CITY DISSENT AND ANSWERS DIFFERENTLY.
		// KingdomCreedRules.HostilityPerDissentPoint is the other lens on this one table, and
		// the two must never be collapsed into each other: POLITY IS NOT PROXIMITY.
		//   * Dissent asks whether two CITIES can be one realm. The parties are a day's walk
		//     apart and never in the same room; distance is the whole of the relationship. So
		//     the feeling is spent slowly, as points a day the founder can watch accumulating,
		//     ordinary dislike buys none at all, and the answer arrives as a decision with a
		//     long fuse that a rite of shared water can still put out.
		//   * Cohabitation asks whether two PEOPLE sleep in one room tonight. There is no
		//     accrual, no countdown and nothing to put out: it is a placement constraint,
		//     answered yes or no at the door (VISION.md's pillar guards, never meters). What it
		//     scales on is not time but ARCHITECTURE, because a wall between two beds is a real
		//     object the founder can pay stone for, and a border between two cities is not.
		// The consequence is the ruling's own, and intended: a diverse city must build better
		// housing to exist at all. Belief diversity is a thing you build for, in stone.
		//
		// WHAT QUD'S OWN TABLE ACTUALLY HOLDS, since the rungs are only as sharp as the data.
		// Factions.xml files exactly five negative feelings: -25 once, -50 fifty-three times,
		// -100 nineteen times, and -200/-500 three times between them, which
		// KingdomCreedRules.Hostility clamps to 100. So on the shipped table the rungs bite at
		// three distinct places -- Packed refuses everything filed, Close refuses the ambient
		// -50 grudge, and Roomed and Private both refuse only the flat fault lines. Roomed and
		// Private part company at 75, a feeling somebody had to sit down and file on purpose;
		// vanilla files none, a mod may, and the rung is a rule about quarters rather than a
		// lookup table for one game's factions.
		// ==================================================================================

		/// <summary>
		/// How close the quarters are, which is how much of a quarrel they will hold. Ordered
		/// tightest first, so a plain <c>&gt;</c> between two of these reads as "roomier than".
		/// </summary>
		public enum Closeness
		{
			/// <summary>One open room. A tent, a staked tent-row, a bunk row: every bed in
			/// earshot of every other and nothing at all between them.</summary>
			Packed = 0,

			/// <summary>A hut. One household's worth of walls, and a door that shuts.</summary>
			Close = 1,

			/// <summary>A stone house. Walls between the beds, and rooms that are somebody's.
			/// </summary>
			Roomed = 2,

			/// <summary>A fine house, a manor. Quarters of one's own, and a household that meets
			/// at dinner because it chose to.</summary>
			Private = 3
		}

		/// <summary>
		/// Cells of the tier's own footprint per bed below which the quarters are
		/// <see cref="Closeness.Packed"/>. Four: the tent is three cells a bed and the tent-row
		/// three and a third, and neither has a wall in it anywhere.
		/// </summary>
		public const int PackedCellsPerBed = 4;

		/// <summary>Cells per bed below which the quarters are <see cref="Closeness.Close"/>.
		/// Six: the timber hut and the hut-and-yard are four cells a bed apiece.</summary>
		public const int CloseCellsPerBed = 6;

		/// <summary>Cells per bed below which the quarters are <see cref="Closeness.Roomed"/>,
		/// and at or above which they are <see cref="Closeness.Private"/>. Ten: the stone house
		/// is six cells a bed and the housing court seven, against the fine house's twelve and
		/// the manor's eighteen.</summary>
		public const int RoomedCellsPerBed = 10;

		/// <summary>
		/// The rung a design's own arithmetic puts it on, before any <c>Closeness</c> attribute
		/// is consulted: beds against the ground the TIER stands on (not the plot &mdash; the
		/// plot is an envelope, and the yard is not somewhere anybody sleeps).
		/// <para>
		/// Integer arithmetic throughout, by multiplying the threshold out rather than dividing
		/// the density down, so no rung boundary lands on a rounding direction.
		/// </para>
		/// <para>
		/// A degenerate reading &mdash; no footprint the registry could measure, or no beds
		/// &mdash; is <see cref="Closeness.Packed"/>. The tightest rung is the honest answer to
		/// a roof with no arithmetic behind it: a work with no plot spec is one cell with a bunk
		/// in it, which is exactly one open room. It is never actually consulted for a home with
		/// no beds, because a home with no beds takes nobody.
		/// </para>
		/// </summary>
		/// <param name="FootprintCells">Exact designated plot cells occupied by the physical
		/// provider root.</param>
		/// <param name="Beds">Current physical <c>roof</c> capacity supplied by that root.</param>
		public static Closeness ClosenessFromDensity(int FootprintCells, int Beds)
		{
			if (FootprintCells < 1 || Beds < 1)
			{
				return Closeness.Packed;
			}
			if (FootprintCells < Beds * PackedCellsPerBed)
			{
				return Closeness.Packed;
			}
			if (FootprintCells < Beds * CloseCellsPerBed)
			{
				return Closeness.Close;
			}
			return (FootprintCells < Beds * RoomedCellsPerBed) ? Closeness.Roomed : Closeness.Private;
		}

		/// <summary>The names the <c>Closeness</c> attribute accepts, in rung order &mdash; the
		/// override a design writes when the arithmetic reads its ground wrong.</summary>
		public static readonly string[] ClosenessNames = new string[4] { "Packed", "Close", "Roomed", "Private" };

		/// <summary>
		/// Reads a design's declared <c>Closeness</c>. Case and surrounding whitespace are folded,
		/// exactly as every other catalogue attribute folds them.
		/// </summary>
		/// <param name="Raw">The raw attribute. Null and blank both mean "declared nothing", which
		/// is every design in the catalogue that is content to be measured.</param>
		/// <param name="Quarters">The rung named. <see cref="Closeness.Packed"/> and meaningless
		/// when this returns false.</param>
		/// <returns>False for null, blank, and any word that is not one of
		/// <see cref="ClosenessNames"/> &mdash; the caller falls back to
		/// <see cref="ClosenessFromDensity"/> and says so, rather than refusing the design.
		/// </returns>
		public static bool TryParseCloseness(string Raw, out Closeness Quarters)
		{
			Quarters = Closeness.Packed;
			if (string.IsNullOrEmpty(Raw))
			{
				return false;
			}
			string token = Raw.Trim();
			for (int i = 0; i < ClosenessNames.Length; i++)
			{
				if (string.Equals(token, ClosenessNames[i], StringComparison.OrdinalIgnoreCase))
				{
					Quarters = (Closeness)i;
					return true;
				}
			}
			return false;
		}

		/// <summary>The roomier of two rungs. Used to name the best quarters that still refused
		/// somebody, which is the fact a founder can act on: whatever they build next has to beat
		/// it.</summary>
		public static Closeness Roomier(Closeness A, Closeness B)
		{
			return (A > B) ? A : B;
		}

		/// <summary>
		/// Hostility at which <see cref="Closeness.Packed"/> quarters refuse: one, which is one
		/// past <see cref="CreedRefusalHostilityFloor"/> and so is the same rule the floor has
		/// always stated. In one open room a household shares without quarrel or not at all
		/// &mdash; same creed, or two creeds the table has filed no opinion between.
		/// </summary>
		public const int PackedRefusalHostility = CreedRefusalHostilityFloor + 1;

		/// <summary>
		/// Hostility at which <see cref="Closeness.Close"/> quarters refuse: fifty, the ambient
		/// grudge fifty-three faction pairs hold toward everyone they have not troubled to name.
		/// A hut holds a mild dislike and will not hold that one.
		/// </summary>
		public const int CloseRefusalHostility = 50;

		/// <summary>
		/// Hostility at which <see cref="Closeness.Roomed"/> quarters refuse: seventy-five. A
		/// house with walls between the beds carries the ambient grudge and refuses open
		/// hostility &mdash; a feeling somebody filed on purpose rather than fell back on.
		/// </summary>
		public const int RoomedRefusalHostility = 75;

		/// <summary>
		/// Hostility at which <see cref="Closeness.Private"/> quarters refuse. Equal to Roomed's,
		/// by Addendum 4d: a fault line refuses any shared roof, and marble is
		/// never a tool for housing enemies. Private's worth is quality and notables, not
		/// tolerance. This was 100 — which let a pair refused a stone house share a fine one, the
		/// exact gap the ruling closes; on vanilla's feeling table (−25/−50/−100) the difference
		/// was unobservable, but a modded feeling between 75 and 99 would have found it.
		/// </summary>
		public const int PrivateRefusalHostility = RoomedRefusalHostility;

		/// <summary>
		/// The ladder itself: the creed hostility at or above which these quarters refuse. Never
		/// decreasing from <see cref="Closeness.Packed"/> to <see cref="Closeness.Private"/>, which
		/// is the whole of the ruling &mdash; better quarters hold worse feelings. It stops rising
		/// at the top: Addendum 4d ties Private's tolerance to Roomed's, so marble buys quality and
		/// notables, never permission to house an enemy a stone house refused.
		/// </summary>
		/// <param name="Quarters">The rung, from the design's <c>Closeness</c> or from
		/// <see cref="ClosenessFromDensity"/>.</param>
		public static int RefusalHostility(Closeness Quarters)
		{
			switch (Quarters)
			{
			case Closeness.Close:
				return CloseRefusalHostility;
			case Closeness.Roomed:
				return RoomedRefusalHostility;
			case Closeness.Private:
				return PrivateRefusalHostility;
			default:
				return PackedRefusalHostility;
			}
		}

		/// <summary>How the mod says a rung out loud, as the tail of "the roomiest of them is
		/// &#8230;". Names the architecture rather than the rung, because a founder acts on walls
		/// and not on a word this file made up.</summary>
		public static string QuartersPhrase(Closeness Quarters)
		{
			switch (Quarters)
			{
			case Closeness.Close:
				return "a hut's close quarters";
			case Closeness.Roomed:
				return "a house with walls between the beds";
			case Closeness.Private:
				return "a house of their own";
			default:
				return "one open room";
			}
		}

		/// <summary>
		/// Whether two residents cannot share a building: the ideological case (creed feelings,
		/// read off the engine's own faction table by the caller and handed in as a hostility
		/// score) or the rest (an authored <c>Refuses</c> tag matching something the other
		/// resident's own <c>Needs</c> or <c>Prefers</c> names). Tested both directions, because
		/// a refusal only one side states is still a refusal &mdash; the same asymmetry
		/// <c>KingdomCreedRules.Hostility</c> already reads faction feelings with.
		/// </summary>
		/// <param name="ARefuses">The first resident's <c>Refuses</c> tags.</param>
		/// <param name="ASelfTags">The first resident's own <c>Needs</c> ∪ <c>Prefers</c> tags
		/// &mdash; what the second resident's <c>Refuses</c> is tested against.</param>
		/// <param name="BRefuses">The second resident's <c>Refuses</c> tags.</param>
		/// <param name="BSelfTags">The second resident's own <c>Needs</c> ∪ <c>Prefers</c> tags.
		/// </param>
		/// <param name="CreedHostility">0-100, from <c>KingdomCreed.HostilityBetween</c> on the
		/// pair's own creeds. Zero for a mixed pair, an agreeing pair, or a pair the engine has no
		/// opinion about.</param>
		/// <remarks>Judges the tightest quarters there are, which is the only safe reading of a
		/// caller that has not said what the quarters were. Callers that know pass them:
		/// <see cref="Conflicts(IReadOnlyList{string}, IReadOnlyList{string}, IReadOnlyList{string}, IReadOnlyList{string}, int, Closeness)"/>.
		/// </remarks>
		public static bool Conflicts(IReadOnlyList<string> ARefuses, IReadOnlyList<string> ASelfTags, IReadOnlyList<string> BRefuses, IReadOnlyList<string> BSelfTags, int CreedHostility)
		{
			return Conflicts(ARefuses, ASelfTags, BRefuses, BSelfTags, CreedHostility, Closeness.Packed);
		}

		/// <summary>
		/// The same question, asked of specific quarters (Addendum 4c). The creed half scales:
		/// what refuses in one open room is carried by a house with walls between the beds. The
		/// tag half does not &mdash; an authored <c>Refuses</c> is an absolute refusal at every
		/// closeness, because it names a thing about the other person that a wall does not fix
		/// and no amount of marble will.
		/// </summary>
		/// <param name="ARefuses">The first resident's <c>Refuses</c> tags.</param>
		/// <param name="ASelfTags">The first resident's own <c>Needs</c> &#8746; <c>Prefers</c>
		/// tags.</param>
		/// <param name="BRefuses">The second resident's <c>Refuses</c> tags.</param>
		/// <param name="BSelfTags">The second resident's own <c>Needs</c> &#8746; <c>Prefers</c>
		/// tags.</param>
		/// <param name="CreedHostility">0-100, from <c>KingdomCreed.HostilityBetween</c>, handed in
		/// raw: this function owns the comparison, so no caller has to know the ladder to use it.
		/// </param>
		/// <param name="Quarters">The home's rung, from its declared <c>Closeness</c> or from
		/// <see cref="ClosenessFromDensity"/>.</param>
		public static bool Conflicts(IReadOnlyList<string> ARefuses, IReadOnlyList<string> ASelfTags, IReadOnlyList<string> BRefuses, IReadOnlyList<string> BSelfTags, int CreedHostility, Closeness Quarters)
		{
			if (CreedHostility >= RefusalHostility(Quarters))
			{
				return true;
			}
			if (Intersects(ARefuses, BSelfTags))
			{
				return true;
			}
			return Intersects(BRefuses, ASelfTags);
		}
	}
}
