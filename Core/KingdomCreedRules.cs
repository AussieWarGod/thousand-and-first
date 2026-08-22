using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// How far apart two cities of one realm have grown. Ordered mildest-first, so a larger value
	/// is a worse temper and every comparison in this file reads that way.
	/// </summary>
	public enum CityTemper
	{
		Concord = 0,
		Muttering = 1,
		Quarrel = 2,
		Rupture = 3,
		Secession = 4
	}

	/// <summary>Why a city may not leave the realm, or that its leaving is warranted.</summary>
	public enum SecessionVerdict
	{
		Warranted = 0,
		OneCity = 1,
		NoClash = 2,
		DissentHolds = 3
	}

	/// <summary>Why a seceded city may not be taken back, or that it may.</summary>
	public enum RejoinVerdict
	{
		Allowed = 0,
		NothingSeceded = 1,
		RealmIsFull = 2,
		NotOnTheirGround = 3,
		ClashStillLive = 4,
		StandingTooLow = 5
	}

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
	public static class KingdomCreedRules
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

		/// <summary>
		/// Weight one candidate creed draws from the realm's standing with its faction. Vanilla's
		/// own attitude tiers, mirrored from <see cref="KingdomExileRules"/> so a creed's pull
		/// steps exactly where the reputation screen says it should.
		/// </summary>
		/// <param name="Standing">The realm's standing with that faction, on the vanilla scale.</param>
		/// <returns>Zero for a faction that dislikes the realm — its people do not walk here.</returns>
		public static int StandingWeight(int Standing)
		{
			if (Standing <= KingdomExileRules.RegardDisliked)
			{
				return 0;
			}
			if (Standing < KingdomExileRules.RegardLiked)
			{
				return 10;
			}
			if (Standing < KingdomExileRules.RegardLoved)
			{
				return 25;
			}
			return 40;
		}

		/// <summary>
		/// How strongly one creed pulls at an arriving settler: what the realm's books say about
		/// that faction, how many of its people are already here, and whether the founder named it.
		/// </summary>
		/// <param name="Standing">The realm's standing with the creed's faction.</param>
		/// <param name="AlreadyHere">Residents of this city already holding the creed. Negative
		/// reads as none.</param>
		/// <param name="Declared">True if the founder declared this creed the realm's own.</param>
		/// <returns>A weight to compete against <see cref="OrdinaryWeight"/>; never negative.</returns>
		public static int CreedWeight(int Standing, int AlreadyHere, bool Declared)
		{
			int weight = StandingWeight(Standing);
			if (AlreadyHere > 0)
			{
				weight += AlreadyHere * AffinityPerResident;
			}
			if (Declared)
			{
				weight += DeclaredBonus;
			}
			return weight;
		}

		/// <summary>
		/// The whole draw an arriving settler is rolled against: every candidate's weight plus the
		/// ordinary settler's.
		/// </summary>
		/// <param name="Weights">Candidate weights, as from <see cref="CreedWeight"/>. Null reads
		/// as no candidates.</param>
		/// <returns>At least <see cref="OrdinaryWeight"/>, so the roll is never over an empty
		/// range.</returns>
		public static int TotalWeight(int[] Weights)
		{
			int total = OrdinaryWeight;
			if (Weights != null)
			{
				for (int i = 0; i < Weights.Length; i++)
				{
					if (Weights[i] > 0)
					{
						total += Weights[i];
					}
				}
			}
			return total;
		}

		/// <summary>
		/// Draws one arriving settler's creed.
		/// <para>
		/// The ordinary settler sits at the bottom of the range, so a roll below
		/// <see cref="OrdinaryWeight"/> always means "believes nothing in particular" whatever the
		/// candidates are. Non-positive weights are skipped rather than treated as tiny: a faction
		/// that dislikes the realm sends nobody, not somebody rarely.
		/// </para>
		/// </summary>
		/// <param name="Weights">Candidate weights, parallel to the caller's candidate names.</param>
		/// <param name="Roll">A roll in <c>[0, TotalWeight(Weights))</c>. Out-of-range rolls read
		/// as ordinary rather than throwing, because the caller's roll comes from the engine.</param>
		/// <returns>The index of the drawn creed, or -1 for an ordinary settler.</returns>
		public static int DrawCreed(int[] Weights, int Roll)
		{
			if (Weights == null || Roll < OrdinaryWeight)
			{
				return -1;
			}
			int cursor = OrdinaryWeight;
			for (int i = 0; i < Weights.Length; i++)
			{
				if (Weights[i] <= 0)
				{
					continue;
				}
				cursor += Weights[i];
				if (Roll < cursor)
				{
					return i;
				}
			}
			return -1;
		}

		/// <summary>
		/// The creed of a city, read off its roll of residents.
		/// <para>
		/// A city has a creed when one is held by at least <see cref="MinBelievers"/> people, by at
		/// least <see cref="DominantSharePercent"/> of everyone living there, and by strictly more
		/// people than any rival. Anything else is a city of mixed people, which is not a failure
		/// state — most cities are that, and nothing in this file happens to them.
		/// </para>
		/// <para>
		/// A tie has no winner on purpose. It is the honest answer, and it is also the only answer
		/// that does not depend on the order a dictionary happens to enumerate in.
		/// </para>
		/// </summary>
		/// <param name="Counts">Creed name to residents holding it. Null, empty, and non-positive
		/// entries all read as nobody.</param>
		/// <param name="Population">Everyone living in the city, believers and ordinary alike.</param>
		/// <returns>The dominant creed's name, or null for a city of mixed people.</returns>
		public static string DominantCreed(IDictionary<string, int> Counts, int Population)
		{
			if (Counts == null || Counts.Count == 0 || Population <= 0)
			{
				return null;
			}
			string leader = null;
			int most = 0;
			int second = 0;
			foreach (KeyValuePair<string, int> entry in Counts)
			{
				if (string.IsNullOrEmpty(entry.Key) || entry.Value <= 0)
				{
					continue;
				}
				if (entry.Value > most)
				{
					second = most;
					most = entry.Value;
					leader = entry.Key;
				}
				else if (entry.Value > second)
				{
					second = entry.Value;
				}
			}
			if (leader == null || most < MinBelievers || most <= second)
			{
				return null;
			}
			return (most * 100 >= Population * DominantSharePercent) ? leader : null;
		}

		/// <summary>
		/// How badly two creeds are at odds, from the engine's own faction feelings.
		/// <para>
		/// The worse of the two directions wins, because a grudge only one side holds is still a
		/// grudge, and in this data most grudges are one-sided: over half of all faction pairs in
		/// <c>Factions.xml</c> disagree about each other. The Templar hold the Girsh at -100 while
		/// the Girsh file no opinion of the Templar at all and fall back on a general -50; the
		/// Barathrumites hold the Templar at -100 while the Templar return only -50. A templar city
		/// beside either is not at peace on the strength of the gentler half.
		/// </para>
		/// </summary>
		/// <param name="FeelingAboutTheOther">How the first city's creed feels about the second's.</param>
		/// <param name="TheirFeelingBack">How the second city's creed feels about the first's.</param>
		/// <param name="SameCreed">True when both cities hold the same creed. Short-circuits to
		/// nothing rather than relying on the engine's own self-feeling, which is a hardcoded +100
		/// and would read here as warmth the cities have not earned.</param>
		/// <returns>0 to 100. Zero for creeds that get on, are indifferent, or that the caller
		/// could not resolve at all.</returns>
		public static int Hostility(int FeelingAboutTheOther, int TheirFeelingBack, bool SameCreed)
		{
			if (SameCreed)
			{
				return 0;
			}
			int worst = (FeelingAboutTheOther < TheirFeelingBack) ? FeelingAboutTheOther : TheirFeelingBack;
			if (worst >= 0)
			{
				return 0;
			}
			return (worst <= -100) ? 100 : -worst;
		}

		/// <summary>
		/// Points of dissent one day of the founder's attention adds, at a given hostility. Integer
		/// division on purpose: everything below <see cref="HostilityPerDissentPoint"/> buys
		/// nothing at all, so ordinary dislike between neighbours never becomes a countdown.
		/// </summary>
		public static int DissentPerDay(int Hostility)
		{
			return (Hostility <= 0) ? 0 : (Hostility / HostilityPerDissentPoint);
		}

		/// <summary>
		/// Dissent after a stretch of days.
		/// <para>
		/// <paramref name="Days"/> is UNCAPPED (Addendum 8 clause 1): two cities that cannot
		/// stand each other go on not standing each other whether or not the founder is watching.
		/// What bounds the outcome is no longer a forgiveness ceiling on the clock but
		/// <see cref="DissentBreaking"/> itself &mdash; the value clamps there, records a brink,
		/// and the unhappier city then waits <see cref="SecessionWindowDays"/> WORLD-DAYS
		/// for the founder to do something about it. Uncapping this without that window would have
		/// made an absence lose a city faster than presence does, which is clause 3 exactly
		/// inverted; the two moved together, in one package, which is why this doc no longer
		/// promises what it used to.
		/// </para>
		/// </summary>
		/// <param name="Dissent">Dissent standing now.</param>
		/// <param name="Hostility">From <see cref="Hostility"/>.</param>
		/// <param name="Days">Days to resolve. Non-positive changes nothing.</param>
		/// <returns>The new dissent, clamped to <c>[0, DissentBreaking]</c>. Arithmetic in long,
		/// because an uncapped day count times four points a day is a number an int cannot always
		/// hold, and a dissent that wrapped negative would read as concord.</returns>
		public static int AccrueDissent(int Dissent, int Hostility, int Days)
		{
			if (Days <= 0)
			{
				return Clamp(Dissent);
			}
			long accrued = (long)Dissent + (long)DissentPerDay(Hostility) * Days;
			if (accrued >= DissentBreaking)
			{
				return DissentBreaking;
			}
			return Clamp((int)accrued);
		}

		/// <summary>Dissent after something the founder did about it. Clamped, so a lever can
		/// never drive dissent below nothing or a shock above the breaking point.</summary>
		/// <param name="Dissent">Dissent standing now.</param>
		/// <param name="Delta">Signed change; negative eases.</param>
		public static int ApplyDissent(int Dissent, int Delta)
		{
			return Clamp(Dissent + Delta);
		}

		/// <summary>How the realm reads its own dissent. Boundaries are the named constants, so
		/// the tier the Charter shows is the tier the secession fires on.</summary>
		public static CityTemper ClassifyTemper(int Dissent)
		{
			if (Dissent >= DissentBreaking)
			{
				return CityTemper.Secession;
			}
			if (Dissent >= DissentRupture)
			{
				return CityTemper.Rupture;
			}
			if (Dissent >= DissentQuarrel)
			{
				return CityTemper.Quarrel;
			}
			if (Dissent >= DissentMuttering)
			{
				return CityTemper.Muttering;
			}
			return CityTemper.Concord;
		}

		/// <summary>
		/// The temper the realm remembers having said out loud, after seeing
		/// <paramref name="Current"/>. The hysteresis: a worsening speaks once, jitter back and
		/// forth across one threshold says nothing further, and only easing the thing all the way
		/// back to <see cref="CityTemper.Concord"/> re-arms the ladder. Mirrors
		/// <c>KingdomExileRules.RememberedRegard</c>, deliberately.
		/// </summary>
		public static CityTemper RememberedTemper(CityTemper Current, CityTemper Spoken)
		{
			if (Current == CityTemper.Concord)
			{
				return CityTemper.Concord;
			}
			return (Current > Spoken) ? Current : Spoken;
		}

		/// <summary>Whether this pass has something new to say about the two cities. False for a
		/// realm in concord and for a temper already spoken of.</summary>
		public static bool ShouldSpeak(CityTemper Current, CityTemper Spoken)
		{
			return Current > CityTemper.Concord && Current > Spoken;
		}

		/// <summary>
		/// Drams of fresh water a rite of shared water asks for at a given temper. Nothing at
		/// concord, because there is nothing to mend and the founder should not be sold a cure for
		/// it.
		/// </summary>
		public static int RiteCost(CityTemper Temper)
		{
			switch (Temper)
			{
			case CityTemper.Muttering:
				return 20;
			case CityTemper.Quarrel:
				return 40;
			case CityTemper.Rupture:
			case CityTemper.Secession:
				return 80;
			default:
				return 0;
			}
		}

		/// <summary>
		/// Dissent a rite eases at a given temper. Rises with the cost but not as fast, so a
		/// founder who lets it run pays more per point — and at the fault line's four points a day
		/// a rite every <see cref="RiteCooldownDays"/> days still wins ground, slowly, at a price.
		/// </summary>
		public static int RiteEase(CityTemper Temper)
		{
			switch (Temper)
			{
			case CityTemper.Muttering:
				return 15;
			case CityTemper.Quarrel:
				return 20;
			case CityTemper.Rupture:
			case CityTemper.Secession:
				return 25;
			default:
				return 0;
			}
		}

		/// <summary>Whether the rite may be held again yet.</summary>
		/// <param name="LastRiteTick">Tick of the last rite, or 0 if never.</param>
		/// <param name="TimeTicks">Now.</param>
		public static bool RiteReady(long LastRiteTick, long TimeTicks)
		{
			return LastRiteTick <= 0 || TimeTicks - LastRiteTick >= RiteCooldownDays * KingdomRules.TicksPerDay;
		}

		/// <summary>
		/// Whether the unhappier city leaves the realm.
		/// </summary>
		/// <param name="Cities">Cities the realm holds. Below two there is nobody to fall out
		/// with, which is how a founder who only ever holds one city never meets any of this.</param>
		/// <param name="Hostility">From <see cref="Hostility"/>. Zero means the creeds do not
		/// clash, whatever dissent was accrued before they stopped clashing.</param>
		/// <param name="Dissent">Dissent standing now.</param>
		/// <param name="Forced">True for the debug path, which skips the dissent requirement and
		/// nothing else.</param>
		public static SecessionVerdict JudgeSecession(int Cities, int Hostility, int Dissent, bool Forced)
		{
			if (Cities < KingdomSettlement.MaxSettlements)
			{
				return SecessionVerdict.OneCity;
			}
			if (Forced)
			{
				return SecessionVerdict.Warranted;
			}
			if (Hostility <= 0)
			{
				return SecessionVerdict.NoClash;
			}
			return (Dissent >= DissentBreaking) ? SecessionVerdict.Warranted : SecessionVerdict.DissentHolds;
		}

		/// <summary>
		/// Which of the two cities walks: the one whose creed holds the other's in worse regard.
		/// <para>
		/// On a tie of feeling the smaller city goes, because it is the minority that leaves. On a
		/// tie of both, the city the founder is not standing in goes — arbitrary, but fixed, so the
		/// same realm always breaks the same way.
		/// </para>
		/// </summary>
		/// <param name="SeatFeelingAboutAway">How the seated city's creed feels about the other's.</param>
		/// <param name="AwayFeelingAboutSeat">How the other city's creed feels about the seat's.</param>
		/// <param name="SeatPopulation">Residents of the seated city.</param>
		/// <param name="AwayPopulation">Residents of the other city.</param>
		/// <returns>True if the city the founder is not standing in is the one that leaves.</returns>
		public static bool AwayIsTheLeaver(int SeatFeelingAboutAway, int AwayFeelingAboutSeat, int SeatPopulation, int AwayPopulation)
		{
			if (AwayFeelingAboutSeat != SeatFeelingAboutAway)
			{
				return AwayFeelingAboutSeat < SeatFeelingAboutAway;
			}
			if (AwayPopulation != SeatPopulation)
			{
				return AwayPopulation < SeatPopulation;
			}
			return true;
		}

		/// <summary>
		/// Whether a seceded city may be taken back.
		/// <para>
		/// The cause must be gone before the city is: a founder who walks back with the same clash
		/// still live is told exactly that. Winning it back means having actually changed something
		/// — declared a creed and let the rolls drift, or watched one city's creed lapse to mixed —
		/// not waiting the quarrel out.
		/// </para>
		/// </summary>
		/// <param name="Seceded">Whether a city left at all.</param>
		/// <param name="Cities">Cities the realm holds now. A founder who founded another second
		/// city in the meantime has no room, and shut that door themselves.</param>
		/// <param name="OnTheirGround">Whether the founder is standing on the seceded city's own
		/// ground. It is asked where it can hear you.</param>
		/// <param name="Hostility">Hostility between the seceded city's creed and the realm's
		/// remaining one, now.</param>
		/// <param name="Standing">The realm's standing with the seceded city's creed's faction.</param>
		public static RejoinVerdict JudgeRejoin(bool Seceded, int Cities, bool OnTheirGround, int Hostility, int Standing)
		{
			if (!Seceded)
			{
				return RejoinVerdict.NothingSeceded;
			}
			if (Cities >= KingdomSettlement.MaxSettlements)
			{
				return RejoinVerdict.RealmIsFull;
			}
			if (!OnTheirGround)
			{
				return RejoinVerdict.NotOnTheirGround;
			}
			if (DissentPerDay(Hostility) > 0)
			{
				return RejoinVerdict.ClashStillLive;
			}
			return (Standing <= KingdomExileRules.RegardDisliked) ? RejoinVerdict.StandingTooLow : RejoinVerdict.Allowed;
		}

		/// <summary>Lower-case name for a temper, Qud style, for reports and the dev log.</summary>
		public static string TemperName(CityTemper Temper)
		{
			switch (Temper)
			{
			case CityTemper.Muttering:
				return "muttering";
			case CityTemper.Quarrel:
				return "quarrelling";
			case CityTemper.Rupture:
				return "ruptured";
			case CityTemper.Secession:
				return "past mending";
			default:
				return "at peace";
			}
		}

		/// <summary>
		/// Founder-facing clause naming what a city believes. Lower-case, fit to follow a comma.
		/// </summary>
		/// <param name="CreedDisplayName">The creed faction's display name, or null for a city of
		/// mixed people.</param>
		public static string CreedClause(string CreedDisplayName)
		{
			return string.IsNullOrEmpty(CreedDisplayName)
				? "a city of mixed people, holding to nothing in particular"
				: ("a city that holds with " + CreedDisplayName);
		}

		/// <summary>
		/// The standing line the Charter shows about the two cities. Always available, whatever the
		/// temper, so the founder can watch this coming rather than being told about it once.
		/// </summary>
		/// <param name="Temper">The temper now.</param>
		/// <param name="Dissent">Dissent standing now, shown plainly against its breaking point.</param>
		/// <param name="HereName">The seated city's name.</param>
		/// <param name="ThereName">The other city's name.</param>
		/// <param name="HereCreed">The seated city's creed display name, or null.</param>
		/// <param name="ThereCreed">The other city's creed display name, or null.</param>
		public static string TemperReport(CityTemper Temper, int Dissent, string HereName, string ThereName, string HereCreed, string ThereCreed)
		{
			string here = string.IsNullOrEmpty(HereName) ? "this city" : ("{{C|" + HereName + "}}");
			string there = string.IsNullOrEmpty(ThereName) ? "the other city" : ("{{C|" + ThereName + "}}");
			string body = here + " is " + CreedClause(HereCreed) + ".\n" + there + " is " + CreedClause(ThereCreed) + ".\n\n";
			switch (Temper)
			{
			case CityTemper.Muttering:
				return body + "They are {{Y|muttering}} about each other. Nothing has been said to anyone's face. {{K|(" + Dissent + " of " + DissentBreaking + ")}}";
			case CityTemper.Quarrel:
				return body + "They are {{W|quarrelling}} openly, and each has begun keeping its own account of who started it. {{K|(" + Dissent + " of " + DissentBreaking + ")}}";
			case CityTemper.Rupture:
				return body + "This is a {{R|rupture}}. One of them is deciding whether it needs you. {{K|(" + Dissent + " of " + DissentBreaking + ")}}";
			case CityTemper.Secession:
				return body + "It is {{R|past mending}}. {{K|(" + Dissent + " of " + DissentBreaking + ")}}";
			default:
				return body + "They are at peace with one another. {{K|(" + Dissent + " of " + DissentBreaking + ")}}";
			}
		}

		/// <summary>
		/// What the realm says on the pass a temper worsens. One line, non-modal, in the
		/// water-keepers' voice.
		/// </summary>
		/// <param name="Temper">The temper now. <see cref="CityTemper.Concord"/> returns empty;
		/// <see cref="CityTemper.Secession"/> has its own telling.</param>
		/// <param name="HereName">The seated city's name.</param>
		/// <param name="ThereName">The other city's name.</param>
		public static string TemperSpeech(CityTemper Temper, string HereName, string ThereName)
		{
			string here = string.IsNullOrEmpty(HereName) ? "this city" : ("{{C|" + HereName + "}}");
			string there = string.IsNullOrEmpty(ThereName) ? "the other city" : ("{{C|" + ThereName + "}}");
			switch (Temper)
			{
			case CityTemper.Muttering:
				return "{{Y|In " + here + " they have started saying " + there + " as though it were a place they had heard about.}}";
			case CityTemper.Quarrel:
				return "{{W|A carter came in from " + there + " and was served last. Nobody in " + here + " thought that worth remarking on.}}";
			case CityTemper.Rupture:
				return "{{R|" + here + " read its own charter aloud tonight and left out the part that names " + there + ".}}";
			default:
				return "";
			}
		}

		/// <summary>
		/// The same worsening as the founder's own book records it: lower-case clause, no trailing
		/// period, because the chronicle dates it and closes it. Kept apart from
		/// <see cref="TemperSpeech"/> because a chronicle entry is not a message with the colour
		/// stripped out &mdash; it is written a year later, by someone with an opinion.
		/// </summary>
		public static string TemperChronicle(CityTemper Temper, string HereName, string ThereName)
		{
			string here = string.IsNullOrEmpty(HereName) ? "the city" : HereName;
			string there = string.IsNullOrEmpty(ThereName) ? "the other city" : ThereName;
			switch (Temper)
			{
			case CityTemper.Muttering:
				return here + " began to speak of " + there + " as somewhere else";
			case CityTemper.Quarrel:
				return here + " and " + there + " fell to open quarrelling, and each kept its own account of who began it";
			case CityTemper.Rupture:
				return "the charter was read aloud in " + here + " with " + there + " left out of it";
			default:
				return "";
			}
		}

		/// <summary>
		/// The loudest thing the realm ever says, and the one the ladder never had a rung for:
		/// the two cities have reached the breaking point and the founder has a named, countable
		/// number of visits to stop the split.
		/// <para>
		/// <see cref="TemperSpeech"/> deliberately returns nothing at
		/// <see cref="CityTemper.Secession"/>, because until the brink there was nothing to say
		/// at that tier &mdash; the city was already gone by the time the tier was reached. This
		/// is what it says now.
		/// </para>
		/// </summary>
		/// <param name="LeaverName">The city that will walk if nothing changes.</param>
		/// <param name="KeptName">The city the realm would keep.</param>
		/// <param name="Days">Whole days the realm has stood at the breaking point, from
		/// <c>KingdomBrinkRules.DaysStood</c>.</param>
		/// <param name="DaysLeft">World-days left before the city goes, from
		/// <c>KingdomBrinkRules.DaysLeft</c>. Days rather than visits: the window runs on the
		/// world's clock now, so staying away does not hold it open.</param>
		public static string SecessionBrinkSpeech(string LeaverName, string KeptName, int Days, int DaysLeft)
		{
			string leaver = string.IsNullOrEmpty(LeaverName) ? "the other city" : ("{{C|" + LeaverName + "}}");
			string kept = string.IsNullOrEmpty(KeptName) ? "this one" : ("{{C|" + KeptName + "}}");
			string stood = (Days <= 0)
				? "tonight"
				: ((Days == 1) ? "since yesterday" : ("for " + Days + " days"));
			string window = (DaysLeft <= 0)
				? "There is no more time in it."
				: ((DaysLeft == 1)
					? "One day, and it is done."
					: (DaysLeft + " days, and it is done."));
			return "{{R|" + leaver + " has been drawing up its own charter " + stood + ", and it does not name "
				+ kept + ". Pour the rite, or settle what the two of them believe, and it holds. " + window
				+ "}} {{K|(Charter: how your cities hold each other)}}";
		}

		/// <summary>
		/// The day a city left, as the founder's own book records it. Lower-case clause, no
		/// trailing period.
		/// </summary>
		/// <param name="LeaverName">The city that left.</param>
		/// <param name="KeptName">The city the realm kept.</param>
		/// <param name="LeaverCreed">The leaving city's creed display name, or null.</param>
		public static string SecessionTelling(string LeaverName, string KeptName, string LeaverCreed)
		{
			string leaver = string.IsNullOrEmpty(LeaverName) ? "the second city" : LeaverName;
			string kept = string.IsNullOrEmpty(KeptName) ? "the city you were standing in" : KeptName;
			string creed = string.IsNullOrEmpty(LeaverCreed) ? "what it had come to believe" : LeaverCreed;
			return leaver + " stopped answering to the realm, holding that it owed " + kept
				+ " nothing and " + creed + " everything, and no water was spilled over it";
		}

		/// <summary>The same day as the roads tell it. Third person already, because the rumour
		/// register is not a translation of the founder's account but a rival to it.</summary>
		/// <param name="LeaverName">The city that left.</param>
		/// <param name="FounderName">The founder's name as strangers would use it.</param>
		public static string SecessionRumour(string LeaverName, string FounderName)
		{
			string leaver = string.IsNullOrEmpty(LeaverName) ? "the second city" : LeaverName;
			string founder = string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName;
			return leaver + " threw off " + founder + " and lost nothing by it — not a wall, not a well, not a soul — which is the part " + founder + " tells differently";
		}

		/// <summary>
		/// The modal the founder reads when a city leaves. States what is gone and, plainly, what
		/// is not — because everything still standing is the reason this is a wound and not an
		/// ending.
		/// </summary>
		/// <param name="LeaverName">The city that left.</param>
		/// <param name="KeptName">The city the realm kept.</param>
		/// <param name="LeaverCreed">The leaving city's creed display name, or null.</param>
		/// <param name="LeaverPopulation">Residents who went with it.</param>
		public static string SecessionNotice(string LeaverName, string KeptName, string LeaverCreed, int LeaverPopulation)
		{
			string leaver = string.IsNullOrEmpty(LeaverName) ? "Your second city" : ("{{C|" + LeaverName + "}}");
			string kept = string.IsNullOrEmpty(KeptName) ? "the city you are standing in" : ("{{C|" + KeptName + "}}");
			string because = string.IsNullOrEmpty(LeaverCreed)
				? "because it could no longer hear itself over " + kept
				: "because it holds with {{C|" + LeaverCreed + "}}, and " + kept + " does not";
			string people = (LeaverPopulation == 1) ? "The one person living there stays" : (LeaverPopulation + " people stay");
			return leaver + " has left the realm, " + because + "."
				+ "\n\nNothing was burned and nobody was driven out. " + people + " where they are. The walls stand, the wells are theirs, the stores are theirs, and the book they kept goes on being kept — in " + leaver + "'s own hand now, not yours."
				+ "\n\nYou still hold " + kept + ", and " + kept + " still holds you."
				+ "\n\nGo and stand on their ground when the thing that split you is no longer true, and ask. They will hear it out. They may even say yes.";
		}

		/// <summary>The day a seceded city came back, as the founder's own book records it.</summary>
		public static string RejoinTelling(string LeaverName)
		{
			string leaver = string.IsNullOrEmpty(LeaverName) ? "the city that left" : LeaverName;
			return "you stood in " + leaver + " with nothing in your hands and asked, and " + leaver + " came back into the realm of its own accord";
		}

		/// <summary>The same day as the roads tell it. See <see cref="SecessionRumour"/>.</summary>
		public static string RejoinRumour(string LeaverName, string FounderName)
		{
			string leaver = string.IsNullOrEmpty(LeaverName) ? "the city that left" : LeaverName;
			string founder = string.IsNullOrEmpty(FounderName) ? "the founder" : FounderName;
			return leaver + " took " + founder + " back on its own terms, and there are those who say it never truly left";
		}

		/// <summary>The modal the founder reads when a seceded city rejoins.</summary>
		/// <param name="LeaverName">The city that came back.</param>
		/// <param name="RealmName">The realm's display name.</param>
		public static string RejoinNotice(string LeaverName, string RealmName)
		{
			string leaver = string.IsNullOrEmpty(LeaverName) ? "the city" : ("{{C|" + LeaverName + "}}");
			string realm = string.IsNullOrEmpty(RealmName) ? "the realm" : ("{{C|" + RealmName + "}}");
			return "You ask on their own ground, and " + leaver + " hears it out."
				+ "\n\nIt is " + realm + "'s again, with everything it did while it was not — the stores as they stand, the roll as it stands, the book with its going in it and its coming back written underneath."
				+ "\n\nNobody apologises. Live and drink.";
		}

		/// <summary>
		/// What the founder is told when a seceded city will not come back. Written as its
		/// water-keepers would say it, not as a rule.
		/// </summary>
		/// <param name="Verdict">The refusal. <see cref="RejoinVerdict.Allowed"/> returns empty.</param>
		/// <param name="LeaverName">The seceded city's name.</param>
		/// <param name="LeaverCreed">The seceded city's creed display name, or null.</param>
		public static string RejoinRefusal(RejoinVerdict Verdict, string LeaverName, string LeaverCreed)
		{
			string leaver = string.IsNullOrEmpty(LeaverName) ? "the city that left" : ("{{C|" + LeaverName + "}}");
			string creed = string.IsNullOrEmpty(LeaverCreed) ? "what they believe" : ("{{C|" + LeaverCreed + "}}");
			switch (Verdict)
			{
			case RejoinVerdict.NothingSeceded:
				return "No city has left you. There is nothing to ask for.";
			case RejoinVerdict.RealmIsFull:
				return "You poured again while " + leaver + " was gone, and a realm holds two cities. There is no room at your table for the one that walked away from it.";
			case RejoinVerdict.NotOnTheirGround:
				return "Ask it where it can hear you. Stand on " + leaver + "'s own ground, and say it there.";
			case RejoinVerdict.ClashStillLive:
				return "You would be heard out in " + leaver + ", and then asked what had changed. Nothing has: your other city still cannot say " + creed + " without spitting after it. Come back when that is no longer true.";
			case RejoinVerdict.StandingTooLow:
				return "They hold with " + creed + " in " + leaver + ", and " + creed + " holds your realm in contempt. They will not put their necks back under that.";
			default:
				return "";
			}
		}

		/// <summary>The rite of shared water as the founder's own book records it.</summary>
		/// <param name="HereName">The city the rite was held in.</param>
		/// <param name="ThereName">The city its envoys came from.</param>
		/// <param name="Drams">Drams poured.</param>
		public static string RiteTelling(string HereName, string ThereName, int Drams)
		{
			string here = string.IsNullOrEmpty(HereName) ? "the city" : HereName;
			string there = string.IsNullOrEmpty(ThereName) ? "the other city" : ThereName;
			return here + " poured " + Drams + " drams for " + there + "'s people and drank with them, and for one evening nobody counted whose water it had been";
		}

		/// <summary>The modal the founder reads after a rite. Honest about how far it went.</summary>
		/// <param name="Temper">The temper after the rite.</param>
		/// <param name="ThereName">The city whose people were drunk with.</param>
		public static string RiteNotice(CityTemper Temper, string ThereName)
		{
			string there = string.IsNullOrEmpty(ThereName) ? "the other city" : ("{{C|" + ThereName + "}}");
			return (Temper == CityTemper.Concord)
				? ("The basin goes round twice and comes back empty. Whatever was between you and " + there + " is not between you any more.")
				: ("The basin goes round. It does not fix it — you can hear that it does not — but they came, and they drank, and they will come again.");
		}

		/// <summary>The declaration as the founder's own book records it.</summary>
		/// <param name="RealmName">The realm's display name.</param>
		/// <param name="CreedDisplayName">The creed declared the realm's own.</param>
		public static string DeclarationTelling(string RealmName, string CreedDisplayName)
		{
			string realm = string.IsNullOrEmpty(RealmName) ? "the realm" : RealmName;
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "one creed above the rest" : CreedDisplayName;
			return realm + " declared itself for " + creed + ", and every road that leads here now leads here knowing it";
		}

		/// <summary>The day the founder took the declaration back.</summary>
		/// <param name="RealmName">The realm's display name.</param>
		public static string RecantTelling(string RealmName)
		{
			string realm = string.IsNullOrEmpty(RealmName) ? "the realm" : RealmName;
			return realm + " unsaid what it had said about itself, and went back to being a place where water is shared and nothing else is asked";
		}

		/// <summary>
		/// The modal the founder reads on declaring a creed. States the price before it is paid
		/// elsewhere &mdash; the standing falls in the world, not only here.
		/// </summary>
		/// <param name="CreedDisplayName">The creed declared.</param>
		/// <param name="SlightedDisplayName">The creed passed over, or null when there is none.</param>
		public static string DeclarationNotice(string CreedDisplayName, string SlightedDisplayName)
		{
			string creed = string.IsNullOrEmpty(CreedDisplayName) ? "the creed" : ("{{C|" + CreedDisplayName + "}}");
			string slighted = string.IsNullOrEmpty(SlightedDisplayName) ? "" : ("{{C|" + SlightedDisplayName + "}}");
			string cost = string.IsNullOrEmpty(slighted)
				? ""
				: ("\n\n" + slighted + " hears of it before the week is out, and thinks less of your realm for it — everywhere, not only here. The city that holds with them takes it harder still, tonight.");
			return "You say it out loud, where the water is poured: this realm is for " + creed + "."
				+ cost
				+ "\n\nFrom now on the people who walk here are people who wanted to walk toward that. Give it time and one of your two cities will stop being what it was.";
		}

		/// <summary>Dissent clamped to the range it is allowed to occupy.</summary>
		private static int Clamp(int Dissent)
		{
			if (Dissent < 0)
			{
				return 0;
			}
			return (Dissent > DissentBreaking) ? DissentBreaking : Dissent;
		}
	}
}
