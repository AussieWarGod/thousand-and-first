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
		/// will not share a roof. Zero: any real enmity at all is enough to refuse cohabitation,
		/// because a household is closer quarters than a shared city. Named rather than inlined
		/// so a playtest that wants a softer floor changes one constant, not a comparison buried
		/// in a loop.
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
		public static bool Conflicts(IReadOnlyList<string> ARefuses, IReadOnlyList<string> ASelfTags, IReadOnlyList<string> BRefuses, IReadOnlyList<string> BSelfTags, int CreedHostility)
		{
			if (CreedHostility > CreedRefusalHostilityFloor)
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
