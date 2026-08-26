using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// What a city believes, where that belief came from, and what happens when a realm holds two
	/// cities that do not believe the same thing.
	/// <para>
	/// A creed is a <b>vanilla faction name</b> — never a name this mod invents. Qud already knows
	/// which creeds hate each other: <c>Factions.xml</c> gives every faction its
	/// <c>&lt;feeling About="X" Value="N"/&gt;</c> entries, and the engine reads them back at
	/// runtime. So the fault lines between cities are the game's own fault lines, they are correct
	/// for any faction another mod ships, and no grudge table lives here. That also satisfies the
	/// extensibility law without a new registry: the registry is <c>Factions</c> itself.
	/// </para>
	/// <para>
	/// Which factions can walk into your city is decided by your realm's own standings ledger,
	/// not by a catalogue. A young realm that has met nobody receives only ordinary settlers; a
	/// creed enters the roll only once the founder's deeds have put that faction on the realm's
	/// books. Belief arrives through what you did, which is the only door this mod wants it to
	/// come through.
	/// </para>
	/// <para>
	/// Elapsed time enters here through <see cref="KingdomRules.ElapsedDays"/>, uncapped: a
	/// season of quarrelling is a season (Addendum 8 clause 1). What stops a realm coming apart
	/// because nobody was playing is not a ceiling on the calendar but the brink &mdash; dissent
	/// stops accruing at the breaking point and the founder still gets
	/// <see cref="SecessionWindowDays"/> world-days from the day the word reaches them, so an
	/// absence of ninety days and one of a thousand arrive at exactly the same realm, warned in
	/// exactly the same words.
	/// Engine-free, so the whole ladder is tabled rather than discovered in the field.
	/// </para>
	/// </summary>
	public static partial class KingdomCreedRules
	{
		/// <summary>
		/// The weight of "this settler believes nothing in particular", against which every creed
		/// competes. Deliberately larger than any single creed's opening weight: a creed is a
		/// minority colour that accumulates, never a badge every arrival wears.
		/// </summary>
		public const int OrdinaryWeight = 100;

		/// <summary>
		/// Weight added per resident of a city who already holds a creed. This is the whole of the
		/// accumulation: a city with five templars in it is known as a templar city, and templars
		/// walk there.
		/// </summary>
		public const int AffinityPerResident = 8;

		/// <summary>
		/// Weight added to the creed the founder declared the realm's own. Decisive without being
		/// absolute — a declaration bends the road, it does not close it.
		/// </summary>
		public const int DeclaredBonus = 60;

		/// <summary>
		/// Residents of one creed below which a city has no creed at all, whatever the
		/// proportions. Three, so a camp of two cannot be doctrinaire and a single zealot is a
		/// person rather than a faction.
		/// </summary>
		public const int MinBelievers = 3;

		/// <summary>
		/// Share of a city's residents one creed must hold before the city is said to have that
		/// creed. A third, and no rival as large: enough that a stranger arriving would notice,
		/// short of a majority nobody in Qud ever actually holds.
		/// </summary>
		public const int DominantSharePercent = 33;

		// ==================================================================================
		// Creed HISTORY (Addendum 16): what a settler has held and left.
		// ==================================================================================

		/// <summary>
		/// Creeds one settler's history remembers. Three, and the bound is the point: a life is
		/// not a ledger, and the record has to be a fixed width or the resident row it rides in
		/// stops being budgetable (LIVING-CITY-ARCHITECTURE &sect;0.0(c)).
		/// <para>
		/// Three because it is the smallest number that can hold a story: what they were born to,
		/// what they took here, and what they took after that. A fourth conversion is a person the
		/// city has already learned everything it is going to learn about.
		/// </para>
		/// </summary>
		public const int MaxKeptCreeds = 3;

		/// <summary>
		/// Separates creeds in a stored history. A pipe rather than the comma an author writes,
		/// for the reason <c>KingdomZoningRules.RosterSeparator</c> gives: a stored key is a name
		/// the GAME chose, and a comma is likelier to appear in one. A creed carrying this
		/// character is refused at <see cref="RememberKept"/> rather than corrupting the record.
		/// </summary>
		public const char KeptSeparator = '|';

		/// <summary>
		/// Reads a stored creed history. Order is preserved &mdash; oldest first, which is the
		/// order it was written in &mdash; duplicates and unusable names are dropped, and anything
		/// past <see cref="MaxKeptCreeds"/> is left where a corrupted store put it: outside the
		/// record. A store that is null, empty, or nonsense reads as an empty history rather than
		/// throwing, because an unreadable history must never be able to cost a founder a
		/// building.
		/// </summary>
		public static List<string> DecodeKept(string Encoded)
		{
			List<string> kept = new List<string>();
			if (string.IsNullOrEmpty(Encoded))
			{
				return kept;
			}
			string[] parts = Encoded.Split(KeptSeparator);
			for (int i = 0; i < parts.Length && kept.Count < MaxKeptCreeds; i++)
			{
				string name = (parts[i] == null) ? null : parts[i].Trim();
				if (string.IsNullOrEmpty(name) || Holds(kept, name))
				{
					continue;
				}
				kept.Add(name);
			}
			return kept;
		}

		/// <summary>Writes a history back to its stored form. Round-trips
		/// <see cref="DecodeKept"/> exactly, bound and de-duplication included.</summary>
		public static string EncodeKept(IEnumerable<string> Kept)
		{
			List<string> names = new List<string>();
			if (Kept != null)
			{
				foreach (string entry in Kept)
				{
					string name = (entry == null) ? null : entry.Trim();
					if (string.IsNullOrEmpty(name) || name.IndexOf(KeptSeparator) >= 0 || Holds(names, name))
					{
						continue;
					}
					names.Add(name);
					if (names.Count >= MaxKeptCreeds)
					{
						break;
					}
				}
			}
			return string.Join(KeptSeparator.ToString(), names.ToArray());
		}

		/// <summary>
		/// One creed added to a stored history: the record a settler carries out of the creed they
		/// have just left.
		/// <para>
		/// <b>First in wins, and the record never rewrites itself.</b> A full history takes no
		/// more, rather than dropping its oldest entry to make room. That is the choice the
		/// visibility law forces (Addendum 14): a creed-work a city could see yesterday must not
		/// vanish from the menu today because somebody across town converted a fourth time. A
		/// record that only ever grows is one a founder can plan against; one that rotates is a
		/// door that closes for reasons nothing on the screen can name.
		/// </para>
		/// </summary>
		/// <param name="Encoded">The history as stored. Null and empty are an empty history.</param>
		/// <param name="Creed">The creed being left. Null, blank, one already remembered, and one
		/// carrying <see cref="KeptSeparator"/> all change nothing.</param>
		/// <param name="Added">True only when the record actually grew.</param>
		/// <returns>The history to store. Unchanged when <paramref name="Added"/> is false.</returns>
		public static string RememberKept(string Encoded, string Creed, out bool Added)
		{
			Added = false;
			List<string> kept = DecodeKept(Encoded);
			string name = (Creed == null) ? null : Creed.Trim();
			if (string.IsNullOrEmpty(name) || name.IndexOf(KeptSeparator) >= 0 || Holds(kept, name) || kept.Count >= MaxKeptCreeds)
			{
				return EncodeKept(kept);
			}
			kept.Add(name);
			Added = true;
			return EncodeKept(kept);
		}

		/// <summary>Whether a history remembers a creed. Case-folded, because the name is written
		/// once by the game and again by a catalogue author.</summary>
		public static bool KeptHolds(string Encoded, string Creed)
		{
			return !string.IsNullOrEmpty(Creed) && Holds(DecodeKept(Encoded), Creed);
		}

		private static bool Holds(List<string> Names, string Name)
		{
			for (int i = 0; i < Names.Count; i++)
			{
				if (string.Equals(Names[i], Name, StringComparison.OrdinalIgnoreCase))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// How much hostility between two creeds buys one point of dissent a day. At the game's
		/// own fault line — the flat -100 the Templar hold and are held at — that is four points a
		/// day; a mere dislike of twenty buys none at all, which is the floor that keeps ordinary
		/// friction from ever breaking a realm.
		/// <para>
		/// <b>Polity is not proximity.</b> This constant and
		/// <c>KingdomLodgingRules.RefusalHostility</c>'s ladder read the same faction feelings and
		/// are deliberately different lenses on them, and neither may be collapsed into the other.
		/// Dissent asks whether two CITIES can be one realm: the parties are a day's walk apart and
		/// never in the same room, so distance is the whole of the relationship, the feeling is
		/// spent slowly as points a day the founder can watch accumulating, ordinary dislike buys
		/// none at all, and the answer arrives with a long fuse that a rite of shared water can
		/// still put out. Cohabitation (Addendum 4c) asks whether two PEOPLE sleep in one room
		/// tonight: no accrual, no countdown, nothing to put out — a placement constraint answered
		/// yes or no at the door, scaled not by time but by ARCHITECTURE, because a wall between
		/// two beds is a real object the founder can pay stone for and a border between two cities
		/// is not. The same -50 that buys a realm two points of dissent a day is refused outright
		/// by a hut and carried without comment by a stone house.
		/// </para>
		/// </summary>
		public const int HostilityPerDissentPoint = 25;

		/// <summary>Dissent at which the unhappier city leaves. Also the ceiling: dissent never
		/// climbs past the point where it has already done its worst.</summary>
		public const int DissentBreaking = 100;

		/// <summary>Dissent at which the two cities are openly at odds and the founder is told so
		/// in the plainest words the mod has.</summary>
		public const int DissentRupture = 70;

		/// <summary>Dissent at which the quarrel is out in the open.</summary>
		public const int DissentQuarrel = 45;

		/// <summary>Dissent at which the first muttering is heard. Deliberately early: the founder
		/// must be able to watch this coming from a very long way off.</summary>
		public const int DissentMuttering = 20;

		/// <summary>Days between rites of shared water. Three, which was the absence cap when it
		/// was written and is now <see cref="KingdomBrinkRules.CohabitationDaysPerAttendedPass"/>
		/// &mdash; the same number wearing its honest name, the design's own model of how often a
		/// present founder comes home. The cap it used to match is retired; the CADENCE it was
		/// really about is not, and this is still the rate a founder who is here can hold.
		/// </summary>
		public const int RiteCooldownDays = KingdomBrinkRules.CohabitationDaysPerAttendedPass;

		/// <summary>
		/// World-days the realm has between the founder being told it stands at the breaking point
		/// and the unhappier city walking. Nine, from
		/// <see cref="KingdomBrinkRules.CityBrinkWindowDays"/>.
		/// <para>
		/// This window did not exist. Secession fired on the same pass dissent reached
		/// <see cref="DissentBreaking"/>, which was survivable only because dissent could not
		/// accrue faster than the absence cap allowed. It now stops at the brink, the word is
		/// pushed to the founder wherever they are, and nine days of world time later the city
		/// goes &mdash; whether or not they came back to watch (Addendum 10(a)). What still cannot
		/// happen is a realm losing a city it was never warned about.
		/// </para>
		/// </summary>
		public const int SecessionWindowDays = KingdomBrinkRules.CityBrinkWindowDays;

		/// <summary>Dissent eased by holding a shared meal while the cities are at odds. Smaller
		/// than a rite because the meal is not asked to be a policy — it is a good evening.</summary>
		public const int MealEase = 12;

		/// <summary>
		/// Dissent added the moment the founder declares one creed the realm's own. The slighted
		/// city hears about it the same night. Paid up front so the declaration is a decision with
		/// a price rather than a free fix.
		/// </summary>
		public const int DeclarationShock = 20;

		/// <summary>
		/// What the realm's standing with the slighted faction falls by on a declaration. Applied
		/// through <c>KingdomSystem.AdjustStanding</c>, so it is visible in the world: the faction
		/// you passed over likes your realm less, everywhere, not only here.
		/// </summary>
		public const int DeclarationStandingCost = -150;
	}
}
