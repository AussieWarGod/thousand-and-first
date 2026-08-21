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
	/// chronicled by name, counted only in attended passes (never calendar time — a founder away
	/// a season spends no more of anybody's patience than a founder away three days), and never
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
	public static class KingdomFaithRules
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
		/// Attended passes a neutral resident spends drawn toward a staffed, consecrated shrine
		/// before they take up its creed. Thirty: large enough that a founder sees this happen
		/// perhaps once or twice a whole game, in keeping with "conversions are rare" &mdash;
		/// and, because it counts only passes the founder is present for standing on this ground,
		/// a season away spends none of it. Named so a playtest that wants the arc to land sooner
		/// or later changes one constant.
		/// </summary>
		public const int ConversionPullThreshold = 30;

		/// <summary>
		/// The pull count after one more attended pass under a staffed, consecrated shrine finds
		/// this resident still neutral. Zero (a resident never pulled before, or one just reset
		/// by a stance that is not <see cref="ShrineStance.Neutral"/>) steps to one, the same
		/// "one pass, one step" shape <c>KingdomLodgingRules.GraceAfterPass</c> uses for its own,
		/// unrelated counter &mdash; the shape Addendum 5 asks every channel to share, not the
		/// state.
		/// </summary>
		public static int PullAfterPass(int Pull)
		{
			return (Pull < 0) ? 1 : (Pull + 1);
		}

		/// <summary>Whether a resident's pull has run its course and they take up the shrine's
		/// creed this pass.</summary>
		public static bool ConversionReady(int Pull)
		{
			return Pull >= ConversionPullThreshold;
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

		// ==================================================================================
		// Education (Addendum 5, channel 3) -- softens, converts nobody.
		// ==================================================================================

		/// <summary>
		/// One band gentler: the closeness a staffed knowledge building lets its zone's residents
		/// read the ambient grudge as, for cohabitation and osmosis alike. Education never
		/// changes what quarters a home actually has &mdash; it changes how forgiving the
		/// quarters' own rung is read as being, exactly one rung roomier, capped at
		/// <see cref="KingdomLodgingRules.Closeness.Private"/> because there is nothing gentler
		/// than a house of one's own to soften toward.
		/// </summary>
		public static KingdomLodgingRules.Closeness SoftenedCloseness(KingdomLodgingRules.Closeness Quarters)
		{
			return (Quarters >= KingdomLodgingRules.Closeness.Private) ? Quarters : (KingdomLodgingRules.Closeness)((int)Quarters + 1);
		}

		/// <summary>
		/// STANDARDS 7b's once-only line for a knowledge building built to be staffed &mdash;
		/// carries a <c>Staff</c> requirement of its own &mdash; that presently has nobody at it:
		/// a room of vellum, and honestly said to be one.
		/// </summary>
		public static string EducationLapsedLine(string BuildingName)
		{
			string building = string.IsNullOrEmpty(BuildingName) ? "The scriptorium" : ("The " + BuildingName);
			return "{{K|" + building + " stands empty of hands: a room of vellum, and nothing more, until somebody keeps it.}}";
		}
	}
}
