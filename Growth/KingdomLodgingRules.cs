using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free arithmetic and prose for cohabitation (Addendum 4, the quality-of-life tag
	/// vocabulary): who is allowed to sleep under which roof, and who is never put beside whom.
	/// <see cref="KingdomLodging"/> is the engine-coupled shell &mdash; it reads real settlers,
	/// real buildings, and the engine's own creed feelings, and calls down into the pure
	/// functions here for every decision that has one right answer given the facts.
	/// <para>
	/// Three hard gates, all placement constraints and never meters (VISION.md, STANDARDS 7b):
	/// a resident is never assigned a home whose <c>Provides</c> does not cover every one of
	/// their <c>Needs</c> (<see cref="MeetsNeeds"/>); a resident is never assigned a home that has
	/// no free bed (<see cref="HasFreeBed"/>); and two residents who refuse each other &mdash;
	/// by creed or by an authored <c>Refuses</c> tag &mdash; are never assigned the same building
	/// (<see cref="Conflicts"/>). Nothing here shades an equilibrium number: <c>Prefers</c> is
	/// out of scope for this file entirely, exactly as Addendum 4 splits it out ("never a penalty
	/// unmet") from the two hard gates and the one hard negative this file owns.
	/// </para>
	/// <para>
	/// <b>Feelings scale with closeness (Addendum 4c).</b> The third gate is not one threshold but
	/// a ladder of four: <see cref="Closeness"/>, derived from a design's beds-per-footprint and
	/// overridable by its <c>Closeness</c> attribute, decides how much of a quarrel these
	/// particular quarters will hold (<see cref="RefusalHostility"/>). An authored
	/// <c>Refuses</c> tag is unscaled and absolute at every rung. The consequence is the ruling's
	/// own: a diverse city must build better housing to exist at all.
	/// </para>
	/// <para>
	/// <b>Refuses, read against what.</b> The brief pairs <c>Refuses</c> with "no cohabitation"
	/// rather than with a building quality, so this file tests it resident-against-resident: a
	/// refusal names a tag, and it fires when the OTHER resident carries that tag among their own
	/// <c>Needs</c> or <c>Prefers</c> &mdash; the only two self-declared surfaces the vocabulary
	/// gives a resident. No third tag category is invented to hold "what a resident presents to
	/// others"; the two that already exist do that job.
	/// </para>
	/// <para>
	/// <b>Where the tags come from.</b> Nothing here parses a creature: every list handed to these
	/// functions is assembled by <see cref="KingdomLodging"/> out of <c>KingdomQol.ProfileOf</c>
	/// and <c>KingdomQol.OfferOf</c> &mdash; the one vocabulary, derived from vanilla truth before
	/// anything is authored, and refined by the blueprint's own <c>r_TAF_*</c> tags. A robot needs
	/// charge here because the engine says it is a robot, not because anybody wrote a tag.
	/// </para>
	/// <para>
	/// <b>Determinism without dice.</b> Which of several eligible homes a resident is assigned to
	/// is decided by <see cref="ChooseIndex"/>: fewest free beds first (fill a household before
	/// opening an empty one), the plot's own id as a stable tiebreak. Nothing here calls the
	/// kernel's counter-random draw, on purpose &mdash; that idiom exists for choices that are
	/// genuinely a matter of taste (which of ten taste categories, which virtue, which pattern-
	/// book design), and dressing a logistics decision in dice would make it LESS honest, not
	/// more. The result is still exactly as reproducible: the same city state always assigns the
	/// same way, and a real decision, once made, is persisted on the resident rather than
	/// re-drawn.
	/// </para>
	/// </summary>
	public static class KingdomLodgingRules
	{
		/// <summary>
		/// Hostility (0-100, from <c>KingdomCreed.HostilityBetween</c>) above which two creeds
		/// will not share the tightest quarters there are. Zero: in one open room any real enmity
		/// at all is enough to refuse cohabitation, because a household is closer quarters than a
		/// shared city. Named rather than inlined so a playtest that wants a softer floor changes
		/// one constant, not a comparison buried in a loop.
		/// <para>
		/// This is the bottom rung of Addendum 4c's ladder and no longer the whole of the rule:
		/// <see cref="PackedRefusalHostility"/> is defined as one past it, and better quarters
		/// hold worse feelings (<see cref="RefusalHostility"/>).
		/// </para>
		/// </summary>
		public const int CreedRefusalHostilityFloor = 0;

		/// <summary>Splits a comma list of namespaced tags (<c>Provides</c>, <c>Needs</c>,
		/// <c>Prefers</c>, <c>Refuses</c>) into trimmed, non-empty tokens, in the order
		/// written.</summary>
		/// <param name="Raw">The raw attribute or tag value. Null and empty both read as no
		/// tags.</param>
		/// <returns>Never null. Empty when <paramref name="Raw"/> named nothing.</returns>
		public static List<string> ParseTags(string Raw)
		{
			List<string> tags = new List<string>();
			if (string.IsNullOrEmpty(Raw))
			{
				return tags;
			}
			string[] parts = Raw.Split(',');
			for (int i = 0; i < parts.Length; i++)
			{
				string token = parts[i].Trim();
				if (token.Length > 0)
				{
					tags.Add(token);
				}
			}
			return tags;
		}

		/// <summary>Whether any tag in <paramref name="A"/> also appears in
		/// <paramref name="B"/>, case-insensitively. Tag order never matters, only membership.
		/// </summary>
		public static bool Intersects(IReadOnlyList<string> A, IReadOnlyList<string> B)
		{
			if (A == null || B == null || A.Count == 0 || B.Count == 0)
			{
				return false;
			}
			for (int i = 0; i < A.Count; i++)
			{
				for (int j = 0; j < B.Count; j++)
				{
					if (string.Equals(A[i], B[j], StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// The hard placement gate: every one of a resident's <c>Needs</c> must appear among a
		/// building's <c>Provides</c>. No needs is trivially met by anything, including a
		/// building that provides nothing &mdash; which is every building in the base catalogue
		/// until a design author writes a <c>Provides</c> string, and is why an unauthored
		/// catalogue behaves exactly as it did before this vocabulary existed.
		/// </summary>
		public static bool MeetsNeeds(IReadOnlyList<string> Needs, IReadOnlyList<string> Provides)
		{
			if (Needs == null || Needs.Count == 0)
			{
				return true;
			}
			if (Provides == null || Provides.Count == 0)
			{
				return false;
			}
			for (int i = 0; i < Needs.Count; i++)
			{
				bool found = false;
				for (int j = 0; j < Provides.Count; j++)
				{
					if (string.Equals(Needs[i], Provides[j], StringComparison.OrdinalIgnoreCase))
					{
						found = true;
						break;
					}
				}
				if (!found)
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>Whether a building has a bed free for one more resident. Strict: a building
		/// at capacity takes nobody else, even the resident who would refuse nobody in it.
		/// </summary>
		public static bool HasFreeBed(int Capacity, int Occupants)
		{
			return Occupants < Capacity;
		}

		// ==================================================================================
		// Addendum 4c -- feelings scale with closeness. How two people feel about each other
		// always bears on whether they can live together; how much it bears is the quarters.
		// You cannot jam five different believers into one bunkhouse and have it be fine, and
		// the same five in a street of stone houses are neighbours who nod. The one
		// CohabitHostility floor the vocabulary shipped with is not deleted but PROMOTED: it is
		// now the Private rung of a four-rung ladder, and the three tighter rungs are what a
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
		/// <param name="FootprintCells">Cells the design's current tier stands on, from
		/// <c>KingdomPlotRules.TryFootprint</c> (which is the whole plot for a tier that declares
		/// no footprint of its own).</param>
		/// <param name="Beds">Beds the design carries, from its <c>Carries</c> roof support.
		/// </param>
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
		/// Hostility at which <see cref="Closeness.Private"/> quarters refuse: a hundred, the
		/// game's own flat -100 fault lines and nothing milder. This is the value
		/// <c>KingdomQolRules.CohabitHostility</c> shipped as the single floor for every roof in
		/// the settlement; under Addendum 4c it is the top rung and applies only where everybody
		/// has a door of their own.
		/// </summary>
		public const int PrivateRefusalHostility = 100;

		/// <summary>
		/// The ladder itself: the creed hostility at or above which these quarters refuse. Strictly
		/// increasing from <see cref="Closeness.Packed"/> to <see cref="Closeness.Private"/>, which
		/// is the whole of the ruling &mdash; better quarters hold worse feelings.
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

		/// <summary>One candidate home, as far as the choice among several eligible ones needs to
		/// know. Everything else (does it meet Needs, does anyone in it conflict) has already
		/// been decided by the time a candidate reaches <see cref="ChooseIndex"/>.</summary>
		public readonly struct LodgingCandidate
		{
			/// <summary>The standing plot's own <c>KingdomPlots.PlotIdProperty</c> &mdash; the
			/// one identity that survives an in-place upgrade, which is why lodging is keyed to
			/// it rather than to the building object itself.</summary>
			public readonly string PlotId;

			public readonly int Capacity;

			public readonly int Occupants;

			public LodgingCandidate(string PlotId, int Capacity, int Occupants)
			{
				this.PlotId = PlotId;
				this.Capacity = Capacity;
				this.Occupants = Occupants;
			}
		}

		/// <summary>
		/// Picks which of several already-eligible homes a resident moves into: fewest free beds
		/// first, so an arrival fills a household that already has people in it before an empty
		/// one is opened, then the plot id itself, ordinal, as a tiebreak that never varies.
		/// </summary>
		/// <param name="Eligible">Candidates that already passed Needs, capacity, and
		/// cohabitation. Order does not matter; every candidate is compared to every other.
		/// </param>
		/// <returns>The winning index, or -1 for an empty list.</returns>
		public static int ChooseIndex(IReadOnlyList<LodgingCandidate> Eligible)
		{
			if (Eligible == null || Eligible.Count == 0)
			{
				return -1;
			}
			int best = 0;
			for (int i = 1; i < Eligible.Count; i++)
			{
				int freeHere = Eligible[i].Capacity - Eligible[i].Occupants;
				int freeBest = Eligible[best].Capacity - Eligible[best].Occupants;
				if (freeHere < freeBest)
				{
					best = i;
					continue;
				}
				if (freeHere == freeBest && string.CompareOrdinal(Eligible[i].PlotId, Eligible[best].PlotId) < 0)
				{
					best = i;
				}
			}
			return best;
		}

		/// <summary>Why a resident who is not housed this pass is not housed, in the order a
		/// founder should hear the reasons: no roof at all outranks a roof nobody fits, which
		/// outranks a roof that fits but is full, which outranks a roof that fits and has room but
		/// holds someone this resident (or who this resident) will not live beside.</summary>
		public enum UnhousedReason
		{
			Housed = 0,
			NoRoofAtAll = 1,
			NeedsUnmet = 2,
			Full = 3,
			Refused = 4
		}

		/// <summary>Reads the four coarse facts a pass over the candidate list already has in
		/// hand and names the single reason nobody was eligible.</summary>
		public static UnhousedReason Diagnose(bool AnyRoofAtAll, bool AnyMeetsNeeds, bool AnyHasCapacity, bool AnyWithoutRefusal)
		{
			if (!AnyRoofAtAll)
			{
				return UnhousedReason.NoRoofAtAll;
			}
			if (!AnyMeetsNeeds)
			{
				return UnhousedReason.NeedsUnmet;
			}
			if (!AnyHasCapacity)
			{
				return UnhousedReason.Full;
			}
			if (!AnyWithoutRefusal)
			{
				return UnhousedReason.Refused;
			}
			return UnhousedReason.Housed;
		}

		/// <summary>
		/// The named, once-announced line STANDARDS 7b requires for an applicable-but-blocked
		/// state: never a complaint, never a countdown, just what is true and why. Repeats the
		/// resident's own name rather than guessing a pronoun &mdash; the roster carries no
		/// gender, and "Vashti will not live beside Vashti" reads honest where a wrong pronoun
		/// would not.
		/// </summary>
		/// <param name="ResidentName">The roll's own name for them.</param>
		/// <param name="Reason">From <see cref="Diagnose"/>.</param>
		public static string UnhousedLine(string ResidentName, UnhousedReason Reason)
		{
			string name = string.IsNullOrEmpty(ResidentName) ? "a settler" : ResidentName;
			switch (Reason)
			{
			case UnhousedReason.NoRoofAtAll:
				return name + " sleeps in the open: there is no roof standing yet.";
			case UnhousedReason.NeedsUnmet:
				return name + " sleeps in the open: nothing built here answers what " + name + " needs.";
			case UnhousedReason.Full:
				return name + " sleeps in the open: every home that would take " + name + " is full.";
			case UnhousedReason.Refused:
				return name + " sleeps in the open: every home that would take " + name + " already holds someone " + name + " will not live beside.";
			default:
				return name + " sleeps in the open.";
			}
		}

		/// <summary>
		/// The same line with the quarters named (Addendum 4c). Only a refusal reads differently:
		/// the founder is told the roomiest quarters that still would not take this person, which
		/// is the fact they can act on &mdash; whatever they build next has to beat it. Every other
		/// reason is word-for-word <see cref="UnhousedLine(string, UnhousedReason)"/>, because
		/// naming the quarters says nothing at all about a settlement with no roof standing.
		/// </summary>
		/// <param name="ResidentName">The roll's own name for them.</param>
		/// <param name="Reason">From <see cref="Diagnose"/>.</param>
		/// <param name="Quarters">The roomiest rung among the homes that had room and refused,
		/// accumulated with <see cref="Roomier"/>.</param>
		public static string UnhousedLine(string ResidentName, UnhousedReason Reason, Closeness Quarters)
		{
			string line = UnhousedLine(ResidentName, Reason);
			if (Reason != UnhousedReason.Refused)
			{
				return line;
			}
			return line + " The roomiest of them is " + QuartersPhrase(Quarters) + ".";
		}

		/// <summary>Of a resident's <c>Needs</c>, the first one their new home's <c>Provides</c>
		/// also names &mdash; the tag <see cref="HomeSuffix"/> colours the line with. Null when
		/// nothing matched, which is the ordinary case for the unauthored base catalogue.
		/// </summary>
		public static string MatchedTag(IReadOnlyList<string> Needs, IReadOnlyList<string> Provides)
		{
			if (Needs == null || Provides == null)
			{
				return null;
			}
			for (int i = 0; i < Needs.Count; i++)
			{
				for (int j = 0; j < Provides.Count; j++)
				{
					if (string.Equals(Needs[i], Provides[j], StringComparison.OrdinalIgnoreCase))
					{
						return Provides[j];
					}
				}
			}
			return null;
		}

		/// <summary>
		/// A small, hand-written table from a namespaced <c>Provides</c> tag to the clause that
		/// names it in prose ("the chrome pilgrim sleeps by the charging post"). Illustrative
		/// rather than exhaustive on purpose: this file owns cohabitation, not the vocabulary's
		/// full derivation, so an unrecognised or absent tag falls back to a plain, honest line
		/// rather than a guess.
		/// </summary>
		private static string FlavorFor(string Tag)
		{
			if (string.IsNullOrEmpty(Tag))
			{
				return null;
			}
			switch (Tag.ToLowerInvariant())
			{
			case "charge":
			case KingdomQolRules.TagCharge:
				return "by the charging post";
			case "water":
			case KingdomQolRules.TagOpenWater:
				return "by the water";
			case "sky":
			case KingdomQolRules.TagSky:
				return "under open sky";
			case "damp":
			case "dark":
			case KingdomQolRules.TagDamp:
			case KingdomQolRules.TagDark:
				return "in the damp dark";
			case "shade":
				return "in the shade";
			case KingdomQolRules.TagQuiet:
				return "in the quiet";
			default:
				return null;
			}
		}

		/// <summary>The line the roll of settlers and the chronicle both read a housed resident's
		/// home as: the building's own name, coloured by the matched tag when the derivation gave
		/// one, plain otherwise.</summary>
		public static string HomeSuffix(string BuildingName, string MatchedProvidesTag)
		{
			string flavor = FlavorFor(MatchedProvidesTag);
			string place = string.IsNullOrEmpty(BuildingName) ? "sleeps under a roof" : ("sleeps in the " + BuildingName);
			return string.IsNullOrEmpty(flavor) ? place : (place + ", " + flavor);
		}

		// ==================================================================================
		// Addendum 4b -- housing binds. A settler joins only if a home exists THEY would take,
		// and a settler who loses every acceptable home is named once, given a short grace of
		// ATTENDED passes for the founder to act, and then leaves through the emigration
		// machinery the settlement already has. Every figure below is a count of attended passes
		// -- never a clock, never a tick, never an age. Absence cannot advance any of it, because
		// nothing here advances except when a pass calls it.
		// ==================================================================================

		/// <summary>
		/// Attended passes a settler who has lost every acceptable home is given before they
		/// leave. Two: long enough for a founder standing there to raise a bunk or stake a plan,
		/// short enough that the answer to "why is nobody moving out" is never "wait longer".
		/// </summary>
		public const int GracePasses = 2;

		/// <summary>The grace of a settler nobody has warned the founder about yet. Negative so
		/// it can never be confused with "warned, and no pass has run since", which is zero.
		/// </summary>
		public const int NoGrace = -1;

		/// <summary>
		/// The grace after one more attended pass has found this settler still unhoused. A
		/// settler at <see cref="NoGrace"/> becomes zero, which is the pass their loss is
		/// announced on; every later attended pass adds one.
		/// <para>
		/// This is the ONLY thing that advances the grace, and it is called only from the
		/// attended lodging pass. An absent founder therefore cannot spend anybody's grace: no
		/// clock is read here, and nothing elapses on its own.
		/// </para>
		/// </summary>
		public static int GraceAfterPass(int Grace)
		{
			return (Grace < 0) ? 0 : (Grace + 1);
		}

		/// <summary>Whether a settler's grace is spent and they leave now: exactly
		/// <see cref="GracePasses"/> attended passes after the one their loss was announced on.
		/// </summary>
		public static bool GraceRunOut(int Grace)
		{
			return Grace >= GracePasses;
		}

		/// <summary>The cause a housing departure is chronicled and noted under, in both
		/// registers. Named here rather than written at the call site so the chronicle and the
		/// ledger cannot drift apart, and so a test can pin it.</summary>
		public const string DepartureCause = "for want of a roof they would live under";

		/// <summary>
		/// The one sentence the founder is owed when a settler's grace has run out and they are
		/// going (STANDARDS 7b). Names the person and the cause and nothing else; the departure
		/// itself is chronicled by the emigration machinery under
		/// <see cref="DepartureCause"/>.
		/// </summary>
		public static string LeavingLine(string ResidentName)
		{
			string name = string.IsNullOrEmpty(ResidentName) ? "a settler" : ResidentName;
			return name + " has waited out the grace with nowhere in the settlement to live, and is leaving.";
		}

		/// <summary>One standing home as the arrival gate sees it. Whether its occupants refuse
		/// the newcomer is decided by <see cref="Conflicts"/> before a home reaches here, because
		/// that answer needs the occupants themselves and this file judges no objects.</summary>
		public readonly struct ArrivalHome
		{
			/// <summary>What the home offers, tags folded &mdash; the design's declared
			/// <c>Provides</c> plus what its roof gives.</summary>
			public readonly IReadOnlyList<string> Provides;

			/// <summary>Beds the home carries.</summary>
			public readonly int Capacity;

			/// <summary>Residents already assigned to it.</summary>
			public readonly int Occupants;

			/// <summary>Whether somebody already in it will not live beside the newcomer, or the
			/// newcomer beside them.</summary>
			public readonly bool OccupantsRefuse;

			public ArrivalHome(IReadOnlyList<string> Provides, int Capacity, int Occupants, bool OccupantsRefuse)
			{
				this.Provides = Provides;
				this.Capacity = Capacity;
				this.Occupants = Occupants;
				this.OccupantsRefuse = OccupantsRefuse;
			}
		}

		/// <summary>
		/// Addendum 4b's arrival gate, which is assignment-level and not a bed tally: whether
		/// SOME standing home would take this arrival &mdash; meets their Needs, has a bed free,
		/// and holds nobody either of them refuses. A settlement with ten empty beds and no
		/// charging post has no room for a robot, and a bed count can never say so.
		/// </summary>
		/// <param name="Homes">Every home standing in the settlement. Null or empty is a
		/// settlement with no roof at all.</param>
		/// <param name="Needs">The arrival's hard requirements. Null or empty asks nothing.
		/// </param>
		/// <param name="Reason">Why nobody would take them, in the order a founder should hear
		/// the reasons. <see cref="UnhousedReason.Housed"/> when one would.</param>
		public static bool AnyWouldTake(IReadOnlyList<ArrivalHome> Homes, IReadOnlyList<string> Needs, out UnhousedReason Reason)
		{
			bool anyRoofAtAll = Homes != null && Homes.Count > 0;
			bool anyMeetsNeeds = false;
			bool anyHasCapacity = false;
			bool anyWithoutRefusal = false;
			if (anyRoofAtAll)
			{
				for (int i = 0; i < Homes.Count; i++)
				{
					if (!MeetsNeeds(Needs, Homes[i].Provides))
					{
						continue;
					}
					anyMeetsNeeds = true;
					if (!HasFreeBed(Homes[i].Capacity, Homes[i].Occupants))
					{
						continue;
					}
					anyHasCapacity = true;
					if (Homes[i].OccupantsRefuse)
					{
						continue;
					}
					anyWithoutRefusal = true;
				}
			}
			Reason = Diagnose(anyRoofAtAll, anyMeetsNeeds, anyHasCapacity, anyWithoutRefusal);
			return Reason == UnhousedReason.Housed;
		}

		/// <summary>The chronicle line for an arrival the settlement had no home for: the real
		/// reason, never a bed count. Addendum 4b's "no home they would take".</summary>
		public static string ArrivalRefusedChronicle(string Settlement, UnhousedReason Reason)
		{
			string where = string.IsNullOrWhiteSpace(Settlement) ? "the settlement" : Settlement.Trim();
			switch (Reason)
			{
			case UnhousedReason.NoRoofAtAll:
				return "a settler reached " + where + " and found no roof standing";
			case UnhousedReason.NeedsUnmet:
				return "a settler reached " + where + " and found no home they would take";
			case UnhousedReason.Full:
				return "a settler reached " + where + " and found every home already full";
			case UnhousedReason.Refused:
				return "a settler reached " + where + " and found no home they would take, for who was already in it";
			default:
				return "a settler reached " + where + " and found nowhere to live";
			}
		}

		/// <summary>The ledger note for the same refusal: what the founder can go and do about
		/// it.</summary>
		public static string ArrivalRefusedNote(UnhousedReason Reason)
		{
			switch (Reason)
			{
			case UnhousedReason.NoRoofAtAll:
				return "A settler came and found no roof standing. Commission housing and they will stay.";
			case UnhousedReason.NeedsUnmet:
				return "A settler came and found no home they would take. Commission housing that answers what they need, and they will stay.";
			case UnhousedReason.Full:
				return "A settler came and found every home full. Commission more housing and they will stay.";
			case UnhousedReason.Refused:
				return "A settler came and found no home they would take, for who was already living in it. Another roof would give them somewhere of their own.";
			default:
				return "A settler came and found nowhere to live.";
			}
		}
	}
}
