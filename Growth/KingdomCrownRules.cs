using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// What one attempt to set the crown down came to.
	/// <para>
	/// The two answers that are not refusals are told apart because they are different events in
	/// the world: a realm getting a capital for the first time and a realm moving one are not the
	/// same sentence, and the second one costs the founder a crossing topology.
	/// </para>
	/// </summary>
	public enum KingdomCrownVerdict : byte
	{
		/// <summary>The realm had no capital. This city is it now.</summary>
		Crowns = 0,

		/// <summary>A crown stood in another city. This act moves it, and the arches re-key.</summary>
		Moves = 1,

		/// <summary>The crown is already set down here. Nothing to do, and the caller says so
		/// rather than asking a question with one answer.</summary>
		AlreadyHere = 2,

		/// <summary>There is no realm yet, so there is nothing for a capital to be the capital
		/// of.</summary>
		RefusedUnfounded = 3,

		/// <summary>This hall does not stand on ground the realm holds.</summary>
		RefusedNotOurGround = 4,

		/// <summary>The settlement never raised this hall.</summary>
		RefusedNotOurWork = 5,

		/// <summary>The city's name could not be written into the realm's own record &mdash; empty,
		/// or carrying the record's separator. Refused rather than escaped, exactly as the arches'
		/// register refuses one (<c>KingdomMirrorGateRules.Storable</c>).</summary>
		RefusedNamed = 6
	}

	/// <summary>
	/// The crown: which of the founder's cities is the capital, how that city came to be it, and
	/// what moving it costs.
	/// <para>
	/// <b>The crown is a BUILDING</b> (Addendum 22 A4), and this file never uses the word
	/// <c>Seat</c> for it. <c>Seat</c> is taken, with a different meaning and a different lifetime:
	/// it is the hot/cold swap role, the settlement the founder is presently standing in, and it
	/// exchanges every time they walk between cities (<c>KingdomSystem.TrySeat</c>). A capital that
	/// moved when the founder walked would not be a capital; it would be a cursor. So the capital
	/// is the city where the crown is set down, the crown is a hall the founder RAISED, and the
	/// only thing that moves it is raising another one and going there.
	/// </para>
	/// <para>
	/// <b>Where "which city is the capital" actually lives, and why.</b> One string in the game's
	/// own state (<see cref="RegisterStateKey"/>), exactly as the arches' register and the keepers'
	/// knowledge roster are carried &mdash; because it is a REALM fact that must be readable while
	/// every city but one is dormant, and a serialized field on the seat could not be, while a
	/// field on the settlement record would be a per-city flag that two cities could both hold.
	/// The string is not trusted, though: <see cref="Resolve"/> checks it against the halls that
	/// actually stand and repairs it when the founder has struck their own crown hall down. That is
	/// the whole of the bargain the megastructure derivation makes one lane over
	/// (<c>KingdomZoning.KeptMegastructure</c>): the world is the record, and the register is only a
	/// tie-break for the one question the world cannot answer &mdash; which of two standing halls
	/// the crown is IN.
	/// </para>
	/// <para>
	/// Engine-free, so the whole of it is tabled.
	/// </para>
	/// </summary>
	public static class KingdomCrownRules
	{
		/// <summary>
		/// The catalogue key of the hall the crown is set down in. Held here as well as in the XML
		/// for the reason <c>KingdomLabRules</c> holds the lab's four: the arithmetic over which
		/// halls stand is testable and the registry is still the authority on what one costs.
		/// </summary>
		public const string CrownKey = "crownhall";

		/// <summary>
		/// Where the realm's crown is recorded. One string in game state, the same way the arches'
		/// register (<c>KingdomMirrorGateRules.RegisterStateKey</c>) and the keepers' roster
		/// (<c>KingdomZoning.RosterState</c>) are carried, and for the same reason: a dormant city
		/// must be able to be the capital.
		/// </summary>
		public const string RegisterStateKey = "r_TAF_Crown";

		/// <summary>Between the record's two columns &mdash; the city, and the hall's own location
		/// key. The arches' register's separator, deliberately: two records the founder will never
		/// see should not be written in two grammars.</summary>
		public const char FieldSeparator = '^';

		/// <summary>Prefix of a crown hall's own location key, so a hall knows whether the crown is
		/// in IT rather than merely in its city.</summary>
		public const string LocationKeyPrefix = "r_TAF_Crown_";

		/// <summary>Whether a name can be written into the record whole. A name that cannot be
		/// stored is refused rather than escaped: a name the record would give back wrong is worse
		/// than a name it refused to take.</summary>
		public static bool Storable(string Text)
		{
			return !string.IsNullOrEmpty(Text) && Text.IndexOf(FieldSeparator) < 0;
		}

		/// <summary>
		/// The location key a crown hall on this ground publishes itself under. Composed from the
		/// zone and the cell and nothing else, so it survives a reload and so a hall rebuilt on the
		/// ground a ruined one stood on inherits the crown rather than orphaning it &mdash; the
		/// arch's own composition rule, for the same reasons.
		/// </summary>
		/// <returns>Null when the ground could not be named.</returns>
		public static string ComposeLocationKey(string ZoneId, int X, int Y)
		{
			if (string.IsNullOrEmpty(ZoneId) || X < 0 || Y < 0 || ZoneId.IndexOf(FieldSeparator) >= 0)
			{
				return null;
			}
			return LocationKeyPrefix + ZoneId + "_" + X + "," + Y;
		}

		/// <summary>The record as one string, ready to be carried in game state.</summary>
		public static string FormatCrown(string City, string Key)
		{
			if (!Storable(City))
			{
				return "";
			}
			return City + FieldSeparator + (Key ?? "");
		}

		/// <summary>
		/// Reads the record. Untrusted, because a save is untrusted and our own older writing is
		/// untrusted with it.
		/// </summary>
		/// <param name="Text">Record text; null and empty both read as a realm with no capital,
		/// which is every realm until the first crown hall is raised.</param>
		/// <param name="City">The city named, or null.</param>
		/// <param name="Key">The hall named, or the empty string when the record predates one.</param>
		/// <returns>False when there was text and it could not be read, so the caller can repair it
		/// and say so once (STANDARDS 7b).</returns>
		public static bool TryParseCrown(string Text, out string City, out string Key)
		{
			City = null;
			Key = "";
			if (string.IsNullOrEmpty(Text))
			{
				return true;
			}
			string[] columns = Text.Split(FieldSeparator);
			if (columns.Length < 1 || columns.Length > 2 || columns[0].Length == 0)
			{
				return false;
			}
			City = columns[0];
			Key = (columns.Length == 2) ? columns[1] : "";
			return true;
		}

		/// <summary>
		/// Which city is the capital, given what the record claims and which cities actually keep a
		/// standing crown hall.
		/// <para>
		/// <b>The world outranks the record.</b> A founder may strike their own crown hall down
		/// &mdash; nothing in the protection law stops them taking apart a thing they built &mdash;
		/// and a realm that went on calling that city its capital would be lying to itself in a
		/// menu. So the record is only believed when the hall it names is still standing somewhere
		/// in that city; otherwise the standing halls decide, and the record is repaired to match.
		/// </para>
		/// <para>
		/// <b>The tie-break never flickers.</b> When the record is no help and several halls stand,
		/// the answer is the first city in the order the caller passed &mdash; which is name order,
		/// never seat order, because seat order changes every time the founder walks through a door
		/// (END-STATE-CITIES-RESEARCH &sect;5.1) and a capital that changed with it would be the
		/// exact bug that section exists to prevent.
		/// </para>
		/// </summary>
		/// <param name="Registered">The city the record names, from <see cref="TryParseCrown"/>.</param>
		/// <param name="CrownCities">Cities keeping a standing crown hall, in a deterministic order
		/// the caller chose and can defend. Null reads as none.</param>
		/// <param name="Capital">The capital, or null when the realm has none.</param>
		/// <returns>True when the record already agreed with the ground and needs no rewrite.</returns>
		public static bool Resolve(string Registered, IList<string> CrownCities, out string Capital)
		{
			int count = (CrownCities == null) ? 0 : CrownCities.Count;
			if (!string.IsNullOrEmpty(Registered))
			{
				for (int i = 0; i < count; i++)
				{
					if (string.Equals(CrownCities[i], Registered, StringComparison.OrdinalIgnoreCase))
					{
						Capital = CrownCities[i];
						return true;
					}
				}
			}
			if (count == 0)
			{
				Capital = null;
				// Nothing to repair only when nothing was claimed. A record naming a city whose hall
				// is gone is a record that must be rubbed out, and the founder is owed the sentence.
				return string.IsNullOrEmpty(Registered);
			}
			Capital = CrownCities[0];
			return false;
		}

		/// <summary>
		/// Whether the crown may be set down in this hall, and what happens if it is.
		/// <para>
		/// Ordered from the fact nothing can change to the one the founder can answer by walking:
		/// there is no realm, then this is not our ground, then we did not build this, then the
		/// name will not store &mdash; and only then the two answers that are events rather than
		/// refusals.
		/// </para>
		/// </summary>
		/// <param name="Founded">Whether there is a realm at all.</param>
		/// <param name="OurGround">Whether the realm holds the zone this hall stands in.</param>
		/// <param name="OurWork">Whether the settlement raised this hall.</param>
		/// <param name="Crowned">The city that presently keeps the crown, or null when none does.</param>
		/// <param name="Here">The city this hall stands in.</param>
		public static KingdomCrownVerdict JudgeTakeUp(bool Founded, bool OurGround, bool OurWork, string Crowned, string Here)
		{
			if (!Founded)
			{
				return KingdomCrownVerdict.RefusedUnfounded;
			}
			if (!OurGround)
			{
				return KingdomCrownVerdict.RefusedNotOurGround;
			}
			if (!OurWork)
			{
				return KingdomCrownVerdict.RefusedNotOurWork;
			}
			if (!Storable(Here))
			{
				return KingdomCrownVerdict.RefusedNamed;
			}
			if (string.IsNullOrEmpty(Crowned))
			{
				return KingdomCrownVerdict.Crowns;
			}
			return string.Equals(Crowned, Here, StringComparison.OrdinalIgnoreCase)
				? KingdomCrownVerdict.AlreadyHere
				: KingdomCrownVerdict.Moves;
		}

		// --- What the founder is told -------------------------------------------------------------
		//
		// Every refusal names the act that lifts it and none of them says "that failed" (STANDARDS
		// 7b). The two events name what they cost before they are committed, which is the Charter's
		// own dedication grammar: disclose the whole price, ask, then act.

		/// <summary>The sentence a refused taking-up is told with. Empty for the three verdicts that
		/// are not refusals, because 7b forbids telling somebody about the absence of a problem.</summary>
		public static string RefusalLine(KingdomCrownVerdict Verdict)
		{
			switch (Verdict)
			{
			case KingdomCrownVerdict.RefusedUnfounded:
				return "You rule nothing yet, and a crown over nothing is a hat.";
			case KingdomCrownVerdict.RefusedNotOurGround:
				return "The crown is set down on the kingdom's own ground, not in other people's houses.";
			case KingdomCrownVerdict.RefusedNotOurWork:
				return "The settlement never raised this hall. Adopt it first, and then the crown can come to it.";
			case KingdomCrownVerdict.RefusedNamed:
				return "This city cannot be written into the realm's own record, so the crown could not honestly be said to be here. Name the city something the record can carry.";
			default:
				return "";
			}
		}

		/// <summary>What the founder is told when the crown is already in this city. Not a refusal:
		/// there is nothing wrong, and the sentence says so rather than opening a question whose
		/// only answer is yes.</summary>
		public static string AlreadyHereLine(string City)
		{
			return Named(City) + " is already the capital. The crown is set down in this hall and there is nothing here to decide.";
		}

		/// <summary>
		/// The whole cost of a first crowning, disclosed before anything is committed. It is
		/// deliberately short, because a first crowning costs nothing that was not already spent
		/// raising the hall.
		/// </summary>
		public static string CrownPrompt(string City)
		{
			return "Set the crown down at " + Named(City) + "?\n\n"
				+ "This city becomes the capital of the realm and stays the capital for as long as this hall stands and the crown is in it. "
				+ "Structures only a capital may raise become buildable here. If your cities keep arches, they will be re-keyed to answer this one.\n\n"
				+ "Nothing is spent. The hall is the price, and it is already paid.";
		}

		/// <summary>
		/// The whole cost of a MOVE, and it is the one prompt in this file that has to be honest
		/// about a loss. The second raising is already sunk by the time this is read; what is not
		/// yet spent is the crossing topology, and a founder who has built a two-city crossing is
		/// owed that sentence before they answer, not after.
		/// </summary>
		public static string MovePrompt(string From, string To)
		{
			return "Move the crown from " + Named(From) + " to " + Named(To) + "?\n\n"
				+ "{{W|" + Named(To) + "}} becomes the capital. {{y|" + Named(From) + "}} stops being one: its hall stands exactly where it stands and nothing is taken down, "
				+ "but the structures only a capital may raise can no longer be begun there, and any that stand there stand as what a former capital keeps.\n\n"
				+ "{{r|Every arch the realm keeps is re-keyed to answer " + Named(To) + ".}} A crossing you built between two other cities will land at the new capital instead. "
				+ "No arch is taken down and no arch goes dark; they simply answer somewhere else from the moment you say yes.";
		}

		/// <summary>What the founder is told the moment a realm gets its first capital.</summary>
		public static string CrownedLine(string City)
		{
			return "{{W|The crown is set down at " + Named(City) + ". The realm has a capital.}}";
		}

		/// <summary>The same moment, dated, for the chronicle.</summary>
		public static string CrownedTelling(string City, string Realm)
		{
			return "the crown was set down at " + Named(City) + ", and " + Named(Realm) + " had a capital for the first time";
		}

		/// <summary>What the founder is told the moment a capital moves.</summary>
		public static string MovedLine(string From, string To)
		{
			return "{{W|The crown is taken up out of " + Named(From) + " and set down at " + Named(To) + ".}}";
		}

		/// <summary>The same moment, dated, for the chronicle.</summary>
		public static string MovedTelling(string From, string To)
		{
			return "the crown left " + Named(From) + " for " + Named(To) + ", and the hall it came out of kept its stone and lost its name";
		}

		/// <summary>
		/// STANDARDS 7b and the protection law in one sentence: the old hall is not destroyed, not
		/// moved, and not taken back &mdash; it is DESIGNATED, and the founder is told what it is
		/// now so they never go looking for the thing it used to do.
		/// </summary>
		public static string FormerCrownLine(string City)
		{
			return "{{y|The hall at " + Named(City) + " is a former crown hall. It stands whole and it is nobody's to pull down; it simply holds nothing now.}}";
		}

		/// <summary>
		/// What the founder is told when the realm's record named a capital whose hall is no longer
		/// standing. Said once, at the moment the realm notices, because there is no other moment at
		/// which they would find out.
		/// </summary>
		public static string StruckLine(string City)
		{
			return "{{r|" + Named(City) + " keeps no crown hall any more, so the realm has no capital. Raise a crown hall and set the crown down in it; the arches go on answering wherever they last answered.}}";
		}

		/// <summary>The same, for a realm that found the crown standing somewhere its own record did
		/// not expect. A repair, told plainly rather than performed in silence.</summary>
		public static string RepairedLine(string City)
		{
			return "{{y|The realm's record of its crown was out of step with its halls. The crown stands at " + Named(City) + ", and that is what the record says now.}}";
		}

		/// <summary>The line a crown hall carries in its own description, so which hall holds the
		/// crown is legible where a founder actually looks for it.</summary>
		/// <param name="Holds">Whether the crown is set down in THIS hall.</param>
		/// <param name="Capital">The city that keeps the crown, or null when none does.</param>
		public static string DescriptionLine(bool Holds, string Capital)
		{
			if (Holds)
			{
				return "\n{{W|The crown is set down here. This city is the capital of the realm.}}";
			}
			if (string.IsNullOrEmpty(Capital))
			{
				return "\n{{rules|Empty. Set the crown down here and this city becomes the capital.}}";
			}
			return "\n{{K|A former crown hall. The crown is at " + Named(Capital) + ".}}";
		}

		/// <summary>What the action reads as in the list. The state is in the label, so a founder
		/// never presses a thing that cannot work and then reads why.</summary>
		public static string TakeUpLabel(bool Holds, string Capital)
		{
			if (Holds)
			{
				return "{{K|set the crown down here}} [already here]";
			}
			return string.IsNullOrEmpty(Capital) ? "set the crown down here" : "move the crown here";
		}

		/// <summary>The line a city's own book carries about the crown. Rendered rather than stored,
		/// so nothing anywhere has to keep it in step.</summary>
		public static string CapitalLine(bool Here, string Capital)
		{
			if (Here)
			{
				return "{{W|The crown is here. This is the capital.}}";
			}
			return string.IsNullOrEmpty(Capital)
				? "{{K|The realm has no capital.}}"
				: ("{{K|The capital is " + Named(Capital) + ".}}");
		}

		/// <summary>A city as a founder would say it, or an honest word when the realm never named
		/// one.</summary>
		public static string Named(string City)
		{
			return string.IsNullOrEmpty(City) ? "the city" : City.Trim();
		}
	}
}
