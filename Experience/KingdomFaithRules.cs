using System;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free arithmetic and prose for two of Addendum 5's five conversion channels: the
	/// shrine (a consecrated, staffed faith building pulls the neutral toward its creed) and
	/// education (a staffed knowledge building softens the ambient grudge, converting nobody).
	/// <see cref="KingdomFaith"/> is the engine-coupled shell &mdash; it reads real buildings, real
	/// settlers, and the engine's own creed feelings, and calls down into the pure functions here
	/// for every decision that has one right answer given the facts.
	/// <para>
	/// <b>The arc.</b> The fault-line ceiling (Addendum 4d) makes creed division permanent
	/// architecture: a hostile pair never shares a roof, at any tier, so a divided city
	/// physically partitions into quarters. Conversion is the long road back. Architecture
	/// manages the difference; practice (this file's shrine and education channels, alongside
	/// osmosis, culture, and diplomacy) converts it; investment heals it. Conversions are RARE,
	/// chronicled by name, counted in the days a shrine actually stood staffed over somebody
	/// (Addendum 8 clause 1 — a consecrated building argues every day, not every visit) and
	/// resolved at a BRINK the founder is warned of and gets world-days to arrest, and never
	/// shown to the player as a meter or a percentage. The counting happens; the watching of it
	/// does not.
	/// </para>
	/// <para>
	/// <b>The guard.</b> A settler may always emigrate rather than convert. A resident whose
	/// creed is OPPOSED to a consecrated shrine's is never pulled toward it &mdash; this file
	/// classifies them out of the pull entirely (<see cref="ClassifyStance"/>) &mdash; because the
	/// covenant is drunk, not administered. What they get instead is pressure they may resent,
	/// which is somebody else's surface to answer (see <see cref="KingdomFaith"/>'s own remarks).
	/// </para>
	/// </summary>
	public static partial class KingdomFaithRules
	{
		private const string FaithCategory = "faith";

		private const string KnowledgeCategory = "knowledge";

		/// <summary>
		/// Whether a design's <c>Category</c> names it a faith building &mdash; the only family
		/// the Charter's consecration ceremony offers, and the only family
		/// <see cref="KingdomFaith"/>'s shrine pass ever looks at. A rule rather than a name list,
		/// so a third-party mod's own faith building is consecratable the moment it declares the
		/// category, with no registration here.
		/// </summary>
		/// <param name="Category">A design's raw <c>Category</c> attribute. Case-insensitive;
		/// null reads as no match.</param>
		public static bool CanConsecrate(string Category)
		{
			return string.Equals(Trimmed(Category), FaithCategory, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// Whether a design's <c>Category</c> names it a knowledge building &mdash; the family
		/// education's softening looks at. Same shape as <see cref="CanConsecrate"/>, for the
		/// same reason: a modded scriptorium works without a line of code here.
		/// </summary>
		public static bool IsEducationCategory(string Category)
		{
			return string.Equals(Trimmed(Category), KnowledgeCategory, StringComparison.OrdinalIgnoreCase);
		}

		private static string Trimmed(string Value)
		{
			return string.IsNullOrEmpty(Value) ? "" : Value.Trim();
		}

		// ==================================================================================
		// Shrine conversion (Addendum 5, channel 2)
		// ==================================================================================

		/// <summary>
		/// What a consecrated shrine reads one resident as, relative to its own creed. Ordered
		/// only for readability; no comparison between members means anything.
		/// </summary>
		public enum ShrineStance
		{
			/// <summary>Holds no creed at all. The one stance a shrine pulls toward its own.
			/// </summary>
			Neutral = 0,

			/// <summary>Already holds the shrine's own creed. Nothing to convert; the shrine is
			/// home ground to them.</summary>
			SameCreed = 1,

			/// <summary>Holds a different creed the engine's own faction table says is at odds
			/// with the shrine's. Never pulled; see the guard in this file's own remarks.</summary>
			Opposed = 2,

			/// <summary>Holds a different creed the table files no opinion between. Neither
			/// pulled nor pressured &mdash; a shrine converts the neutral, not the merely
			/// unaligned.</summary>
			Indifferent = 3
		}

		/// <summary>
		/// Classifies one resident against a consecrated shrine's creed, from facts the caller
		/// already read off the engine: the resident's own creed property and the hostility
		/// between the two creeds (<c>KingdomCreed.HostilityBetween</c>, which is 0 for an empty
		/// creed on either side and for two creeds the table does not set against each other).
		/// </summary>
		/// <param name="ResidentCreed">The resident's <c>KingdomCreed.CreedProperty</c>. Empty
		/// reads as no creed, which is <see cref="ShrineStance.Neutral"/> whatever
		/// <paramref name="Hostility"/> says.</param>
		/// <param name="ShrineCreed">The creed the shrine is consecrated to. Empty is never
		/// passed by a real caller &mdash; an unconsecrated shrine has no pass to run &mdash; and
		/// reads as <see cref="ShrineStance.Indifferent"/> defensively rather than pulling anyone
		/// toward nothing.</param>
		/// <param name="Hostility">0-100, from <c>KingdomCreed.HostilityBetween</c> on the two
		/// creeds. Ignored when either creed is empty or the two are equal.</param>
		public static ShrineStance ClassifyStance(string ResidentCreed, string ShrineCreed, int Hostility)
		{
			if (string.IsNullOrEmpty(ResidentCreed))
			{
				return ShrineStance.Neutral;
			}
			if (string.IsNullOrEmpty(ShrineCreed))
			{
				return ShrineStance.Indifferent;
			}
			if (string.Equals(ResidentCreed, ShrineCreed, StringComparison.Ordinal))
			{
				return ShrineStance.SameCreed;
			}
			return (Hostility > 0) ? ShrineStance.Opposed : ShrineStance.Indifferent;
		}

		/// <summary>
		/// The pull as it was denominated before the clock rework: thirty attended passes under a
		/// staffed, consecrated shrine. Kept as the INPUT to the recalibration rather than
		/// deleted, so <see cref="ConversionPullThreshold"/> shows its own working.
		/// </summary>
		public const int ConversionPullInPasses = 30;

		/// <summary>
		/// Days a neutral resident spends drawn toward a staffed, consecrated shrine before the
		/// road ends. Ninety: <see cref="ConversionPullInPasses"/> restated in days at
		/// <see cref="KingdomBrinkRules.CohabitationDaysPerAttendedPass"/>, so a founder who comes
		/// home at the cadence the design always assumed watches exactly the same arc they always
		/// watched &mdash; large enough that this happens perhaps once or twice a whole game, in
		/// keeping with "conversions are rare". Named so a playtest that wants the arc to land
		/// sooner or later changes one constant.
		/// <para>
		/// A consecrated shrine argues every day, not every visit (Addendum 8 clause 1), so the
		/// days pass whether or not the founder is there. What does NOT happen unwarned is the
		/// conversion: the road ends in a brink, the word is pushed to the founder naming the
		/// settler, the creed, the honest elapsed and what would stop it, and only then does the
		/// shrine's window run &mdash; <see cref="KingdomBrinkRules.CreedBrinkWindowDays"/> of
		/// world time, spent whether or not anybody comes back to watch it (Addendum 10(a)).
		/// </para>
		/// </summary>
		public const int ConversionPullThreshold = ConversionPullInPasses * KingdomBrinkRules.CohabitationDaysPerAttendedPass;

		/// <summary>
		/// The pull count after a stretch of days under a staffed, consecrated shrine has found
		/// this resident still neutral. Held at <see cref="ConversionPullThreshold"/>: the road's
		/// end is a brink, and nothing accrues past a brink, so a founder away a thousand days and
		/// one away ninety come home to a settler standing in the same place.
		/// </summary>
		/// <param name="Pull">Days pulled so far. Negative reads as none.</param>
		/// <param name="Days">Days the shrine argued at them, from
		/// <c>KingdomRules.ActivityDays</c> &mdash; an unstaffed shrine contributes none of them,
		/// which is Addendum 8 clause 2 for this channel. Non-positive changes nothing.</param>
		public static int PullAfterDays(int Pull, int Days)
		{
			int held = (Pull < 0) ? 0 : Pull;
			if (Days <= 0)
			{
				return held;
			}
			long pulled = (long)held + Days;
			return KingdomBrinkRules.HoldAtBrink((pulled > ConversionPullThreshold) ? ConversionPullThreshold : (int)pulled, ConversionPullThreshold);
		}

		/// <summary>Whether a resident's pull has run its course and the shrine's road ends
		/// here.</summary>
		public static bool ConversionReady(int Pull)
		{
			return Pull >= ConversionPullThreshold;
		}

		/// <summary>
		/// Effective start of a warned shrine window after a master-option pause. The warning
		/// remains the same committed record; a later valid option anchor restarts its full future
		/// window. Future/corrupt anchors and clock regression fail safe at <paramref name="NowTick"/>
		/// so they cannot turn into an immediate conversion.
		/// </summary>
		public static long EffectiveWindowStart(long WarnedTick, long OptionAnchorTick,
			long NowTick)
		{
			if (WarnedTick <= 0L) return WarnedTick;
			if (NowTick <= 0L) return WarnedTick;
			long start = WarnedTick;
			if (OptionAnchorTick > start)
				start = (OptionAnchorTick <= NowTick) ? OptionAnchorTick : NowTick;
			if (start > NowTick) start = NowTick;
			return start;
		}

		/// <summary>The chronicle line for a shrine's own consecration or reconsecration. Lower
		/// case, no trailing period, per <c>KingdomChronicle.Record</c>.</summary>
		/// <param name="BuildingName">The shrine's own display name.</param>
		/// <param name="SettlementName">The city it stands in.</param>
		/// <param name="CreedDisplayName">The creed it is consecrated to.</param>
		/// <param name="Reconsecration">True when this shrine already held a creed before this
		/// ceremony.</param>
		public static string ConsecrationChronicle(string BuildingName, string SettlementName, string CreedDisplayName, bool Reconsecration)
		{
			string building = string.IsNullOrEmpty(BuildingName) ? "the shrine" : ("the " + BuildingName);
			string here = string.IsNullOrEmpty(SettlementName) ? "the city" : SettlementName;
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "a creed" : CreedDisplayName;
			if (!Reconsecration)
			{
				return building + " was consecrated to " + creed + " at " + here;
			}
			return building + " was consecrated anew, to " + creed + " this time, and the book still remembers what it was consecrated to before";
		}

		/// <summary>The Yes/No prompt the Charter asks before spending the ceremony. Second
		/// person, full sentences.</summary>
		/// <param name="BuildingName">The shrine's own display name.</param>
		/// <param name="CreedDisplayName">The creed about to be consecrated.</param>
		/// <param name="Reconsecration">True when this overwrites a standing consecration.</param>
		/// <param name="NeverStaffable">True when the design carries no <c>Staff</c> at all, so
		/// it can never crew and so can never actually pull anyone &mdash; told up front rather
		/// than left for the founder to discover from a lapse line that never explains itself.
		/// </param>
		public static string ConsecrationPrompt(string BuildingName, string CreedDisplayName, bool Reconsecration, bool NeverStaffable)
		{
			string building = string.IsNullOrEmpty(BuildingName) ? "it" : BuildingName;
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "a creed" : CreedDisplayName;
			string prompt = "Consecrate " + building + " to " + creed + "?";
			if (Reconsecration)
			{
				prompt += "\n\nIt already answers to another creed. That first consecration stays written in the book; this is a second ceremony, not an undoing of it.";
			}
			if (NeverStaffable)
			{
				prompt += "\n\n" + building + " is never staffed by design. It will hold the creed honestly, and draw nobody toward it until something stands here that hands can actually work.";
			}
			return prompt;
		}

		/// <summary>The modal the founder reads after consecrating. Honest about a shrine that
		/// will never draw anyone while it says so.</summary>
		public static string ConsecrationNotice(string BuildingName, string CreedDisplayName, bool Reconsecration, bool NeverStaffable)
		{
			string building = string.IsNullOrEmpty(BuildingName) ? "The shrine" : ("{{C|" + BuildingName + "}}");
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "the creed" : ("{{C|" + CreedDisplayName + "}}");
			string body = Reconsecration
				? (building + " is consecrated anew, to " + creed + ". The book keeps the first ceremony as it was written.")
				: (building + " is consecrated to " + creed + ".");
			if (NeverStaffable)
			{
				body += " Nothing here can ever be staffed to work it — the stone holds the creed, and holds it quietly.";
			}
			else
			{
				body += " Staffed, and left to its work, it will draw the neutral toward " + creed + " slowly, over a great many visits.";
			}
			return body;
		}

		/// <summary>The chronicle line for a resident's own conversion. Lower case, no trailing
		/// period. Names the person, the creed, and the shrine, because a conversion this rare is
		/// worth all three.</summary>
		public static string ConversionChronicle(string ResidentName, string SettlementName, string CreedDisplayName, string BuildingName)
		{
			string resident = string.IsNullOrEmpty(ResidentName) ? "a settler" : ResidentName;
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "a creed" : CreedDisplayName;
			string building = string.IsNullOrEmpty(BuildingName) ? "the shrine" : ("the " + BuildingName);
			string here = " at " + (string.IsNullOrEmpty(SettlementName) ? "the city" : SettlementName);
			return resident + " came to hold with " + creed + ", drawn slowly by " + building + here;
		}

		/// <summary>The player-facing message for a resident's own conversion.</summary>
		public static string ConversionMessage(string ResidentName, string CreedDisplayName)
		{
			string resident = string.IsNullOrEmpty(ResidentName) ? "A settler" : ResidentName;
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "a creed" : CreedDisplayName;
			return "{{W|" + resident + " has come to hold with " + creed + ".}}";
		}

		/// <summary>
		/// STANDARDS 7b's once-only line for a consecrated shrine with nobody working it: a
		/// stone, and honestly said to be one.
		/// </summary>
		public static string ShrineLapsedLine(string BuildingName, string CreedDisplayName)
		{
			string building = string.IsNullOrEmpty(BuildingName) ? "The shrine" : ("The " + BuildingName);
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "its creed" : CreedDisplayName;
			return "{{K|" + building + " stands consecrated to " + creed + ", but empty of hands: a stone, and nothing more, until somebody tends it.}}";
		}

	}
}
