using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// What the people of a city believe, and what a realm does about holding two cities that
	/// believe different things.
	/// <para>
	/// A settler's creed is a <b>vanilla faction name</b>. A city's creed is whatever enough of
	/// its residents hold; most cities are mixed and have none. When a realm's two cities hold
	/// creeds the engine's own <c>Factions.xml</c> says are at odds, dissent accrues in real time
	/// — two cities that cannot stand each other go on not standing each other whether or not
	/// anyone is watching — and the founder is told about it four tiers before it can cost them
	/// anything. When it runs to the top the realm stands at a BRINK
	/// (<see cref="KingdomBrinkRules"/>) rather than losing a city on the spot: accrual halts, the
	/// founder is named the city and the honest elapsed, and
	/// <see cref="KingdomCreedRules.SecessionWindowDays"/> world-days stand between the
	/// warning and the split. If those are spent with the quarrel still live, the unhappier city
	/// leaves, keeping its ground, its people and its buildings; nothing is destroyed and nobody
	/// is driven out.
	/// </para>
	/// <para>
	/// The arithmetic all lives in <see cref="KingdomCreedRules"/>. This file is the wiring: what
	/// counts as a creed, what the engine says two creeds think of each other, and the four
	/// moments where the founder can act.
	/// </para>
	/// <para>
	/// A realm of one city never encounters any of this. Every entry point below returns before
	/// doing anything when <c>SettlementCount</c> is under two, so the founder who never founds a
	/// second city cannot be penalised by a system they never opted into.
	/// </para>
	/// </summary>
	public static class KingdomCreed
	{
		public static bool Enabled => Options.GetOption("r_TAF_OptionCreed") != "No";

		/// <summary>The string property a settler carries their creed on, alongside
		/// <c>KingdomOrigin</c>. Empty or absent means an ordinary settler, which is most of
		/// them.</summary>
		public const string CreedProperty = "KingdomCreed";

		/// <summary>
		/// The string property a settler carries the creeds they have HELD AND LEFT on, bounded to
		/// <c>KingdomCreedRules.MaxKeptCreeds</c> and joined by
		/// <c>KingdomCreedRules.KeptSeparator</c>. Empty or absent means somebody who has believed
		/// one thing all their life, which is nearly everybody.
		/// <para>
		/// Stamped on the settler rather than kept in a map, for exactly the reason
		/// <c>KingdomConversion.CohabitTickProperty</c> gives: what a person has believed is a
		/// fact about them, and one carried on them survives a seat swap, a secession and a save
		/// without any per-city map having to remember to carry it. The city's own
		/// <c>KingdomSystem.CreedPastCounts</c> is a tally OF this, kept so that a gate can answer
		/// without the people being loaded.
		/// </para>
		/// </summary>
		public const string CreedPastProperty = "KingdomCreedPast";

		/// <summary>
		/// <c>HistoricalSignificance</c> at or above which an ancient faction still reads as a
		/// creed a living person could hold. Three: the tier the Putus Templar, the Mechanimists
		/// and the water barons sit at, which is where "a power the world turns on" begins.
		/// </summary>
		public const int CreedSignificance = 3;

		/// <summary>
		/// Whether a faction is something a settler could plausibly believe in.
		/// <para>
		/// A rule rather than a catalogue, so a faction another mod ships is judged on the same
		/// terms and needs no registration here. A creed is a faction the world names
		/// (<c>Visible</c>), that does not exist purely to hate the player
		/// (<c>HatesPlayer</c>), whose identity is not minted fresh each game (the procedural
		/// <c>SultanCult*</c> factions, whose display names come from game state), and that is
		/// either a people of present-day Qud (<c>Old="false"</c>), a power its history turns on
		/// (<see cref="CreedSignificance"/>), or something the language itself gives an article to
		/// (<c>FormatWithArticle</c> — "the villagers of Ezra", never "birds").
		/// </para>
		/// <para>
		/// Against the shipped data that admits thirty-three factions, every one of them a people:
		/// Joppa and Kyakukya and Ezra, the Barathrumites and the Consortium and the Templar, the
		/// snapjaws and the svardym and the goatfolk. It rejects fifty — every animal and plant
		/// bucket, every internal marker faction, and the sultan cults.
		/// </para>
		/// </summary>
		/// <param name="Candidate">A faction. Null reads as unsuitable.</param>
		public static bool CanBeCreed(Faction Candidate)
		{
			if (Candidate == null || !Candidate.Visible || Candidate.HatesPlayer || string.IsNullOrEmpty(Candidate.Name))
			{
				return false;
			}
			if (Candidate.Name.StartsWith("SultanCult"))
			{
				return false;
			}
			return !Candidate.Old || Candidate.HistoricalSignificance >= CreedSignificance || Candidate.FormatWithArticle;
		}

		/// <summary>
		/// A creed as the founder should read it: the faction's formatted display name, article and
		/// all.
		/// </summary>
		/// <param name="CreedFactionName">A faction name, or null.</param>
		/// <returns>Empty for no creed. For a faction this build cannot resolve — a creed recorded
		/// by a save whose mods have since changed — the raw name, because naming it wrongly is
		/// better than silently dropping a city's whole character.</returns>
		public static string CreedName(string CreedFactionName)
		{
			if (string.IsNullOrEmpty(CreedFactionName))
			{
				return "";
			}
			Faction faction = Factions.GetIfExists(CreedFactionName);
			return (faction != null) ? faction.GetFormattedName() : CreedFactionName;
		}

		/// <summary>
		/// What one creed's faction thinks of another's, straight out of the engine.
		/// <para>
		/// <c>GetIfExists</c> rather than <c>Get</c>, for the reason
		/// <c>KingdomSystem.MirrorFeeling</c> gives: <c>Factions.Get</c> throws a bare exception on
		/// an unknown name, and a creed recorded in a save can outlive the faction that named it.
		/// (<c>Factions.GetFeelingFactionToFaction</c> would swallow that into a logged exception
		/// and a zero, which is the same answer with a stack trace in the player's log every pass.)
		/// </para>
		/// <para>
		/// An absent entry is <b>not</b> zero: the engine falls through to the faction's
		/// <c>"*"</c> general feeling, which for the Templar, the Girsh and the Children of Mamon
		/// is a standing -50 toward everyone they have not troubled to name. That is deliberate
		/// and is left alone — a faction that hates strangers by default should be hard to live
		/// beside.
		/// </para>
		/// </summary>
		/// <param name="From">The faction holding the opinion.</param>
		/// <param name="About">The faction it is held about.</param>
		/// <returns>The feeling, or 0 when either name is empty or unresolvable.</returns>
		public static int Feeling(string From, string About)
		{
			if (string.IsNullOrEmpty(From) || string.IsNullOrEmpty(About))
			{
				return 0;
			}
			Faction faction = Factions.GetIfExists(From);
			return (faction == null) ? 0 : faction.GetFeelingTowardsFaction(About);
		}

		/// <summary>
		/// How badly two creeds are at odds, read both ways because Qud's feelings are not
		/// symmetric — over half of all faction pairs in the shipped data disagree about each
		/// other.
		/// </summary>
		/// <returns>0 to 100; 0 when either city is mixed or both hold the same creed.</returns>
		public static int HostilityBetween(string CreedA, string CreedB)
		{
			if (string.IsNullOrEmpty(CreedA) || string.IsNullOrEmpty(CreedB))
			{
				return 0;
			}
			return KingdomCreedRules.Hostility(Feeling(CreedA, CreedB), Feeling(CreedB, CreedA), CreedA == CreedB);
		}

		/// <summary>The creed of one city, or null for a city of mixed people.</summary>
		/// <param name="City">A settlement record. Null reads as mixed.</param>
		public static string CreedOf(KingdomSettlement City)
		{
			return (City == null) ? null : KingdomCreedRules.DominantCreed(City.CreedCounts, City.Population);
		}

		/// <summary>The seated city's creed, or null.</summary>
		public static string SeatCreed(KingdomSystem System)
		{
			if (System == null || !System.Founded)
			{
				return null;
			}
			return KingdomCreedRules.DominantCreed(System.CreedCounts, System.Population);
		}

		/// <summary>The realm's other city's creed, or null when there is no other city.</summary>
		public static string AwayCreed(KingdomSystem System)
		{
			return (System == null) ? null : CreedOf(System.Away);
		}

		/// <summary>
		/// Draws the creed of a settler arriving at the seated city.
		/// <para>
		/// Candidates are the factions the realm already has dealings with — its own standings
		/// ledger — plus whatever its cities already hold and whatever the founder declared. A
		/// young realm that has met nobody has no candidates and receives only ordinary settlers,
		/// which is the intent: belief walks in through what the founder did, not out of a table.
		/// </para>
		/// <para>
		/// Weighting is <see cref="KingdomCreedRules.CreedWeight"/>: the realm's standing with the
		/// faction, how many of its people already live here, and whether the founder named it.
		/// The ordinary settler always outweighs any single creed at the outset.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom. Unfounded returns empty.</param>
		/// <returns>A faction name, or empty for an ordinary settler.</returns>
		public static string Draw(KingdomSystem System)
		{
			if (!Enabled || System == null || !System.Founded)
			{
				return "";
			}
			List<string> candidates = Candidates(System);
			if (candidates.Count == 0)
			{
				return "";
			}
			int[] weights = new int[candidates.Count];
			for (int i = 0; i < candidates.Count; i++)
			{
				System.CreedCounts.TryGetValue(candidates[i], out var here);
				weights[i] = KingdomCreedRules.CreedWeight(System.GetStanding(candidates[i]), here, candidates[i] == System.DeclaredCreed);
			}
			int drawn = KingdomCreedRules.DrawCreed(weights, Stat.Random(0, KingdomCreedRules.TotalWeight(weights) - 1));
			return (drawn < 0) ? "" : candidates[drawn];
		}

		/// <summary>
		/// Records an arriving settler's creed on the settler and in the seated city's tally.
		/// <para>
		/// Side effects: writes <see cref="CreedProperty"/> on <paramref name="Settler"/> and
		/// increments the city's count. Safe to call with an empty creed, which is the common case
		/// and does nothing.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Settler">The arriving settler. Null is tolerated; only the tally moves.</param>
		/// <param name="Creed">A faction name from <see cref="Draw"/>, or empty.</param>
		public static void Record(KingdomSystem System, GameObject Settler, string Creed)
		{
			if (System == null || string.IsNullOrEmpty(Creed))
			{
				return;
			}
			Settler?.SetStringProperty(CreedProperty, Creed);
			System.CreedCounts.TryGetValue(Creed, out var count);
			System.CreedCounts[Creed] = count + 1;
			KingdomLog.Log("creed: " + (Settler?.ShortDisplayName ?? "settler") + " holds with " + Creed + " (" + (count + 1) + " here)");
		}

		/// <summary>
		/// Takes a settler who has left or died out of the seated city's creed tally. Reads the
		/// creed off the settler, so a settler who carries none costs nothing. Never drives a
		/// count below zero.
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Leaver">The departing settler. Null does nothing.</param>
		public static void Forget(KingdomSystem System, GameObject Leaver)
		{
			if (System == null || Leaver == null)
			{
				return;
			}
			// The whole person comes out of both tallies: what they hold, and what they held before
			// that. A history is a fact about somebody, so it leaves with them exactly as their
			// present creed does -- the alternative is a city that goes on being able to raise a
			// creed-work because of a believer who died a year ago.
			//
			// KingdomConversion.Convert is the one caller for whom the person has NOT left, and it
			// puts both halves back on the far side: Record for the present, RememberPast for the
			// history it has just added to.
			DropPast(System, Leaver);
			string creed = Leaver.GetStringProperty(CreedProperty);
			if (string.IsNullOrEmpty(creed))
			{
				return;
			}
			System.CreedCounts.TryGetValue(creed, out var count);
			if (count > 1)
			{
				System.CreedCounts[creed] = count - 1;
			}
			else
			{
				System.CreedCounts.Remove(creed);
			}
		}

		/// <summary>
		/// The creeds one settler has held and left, oldest first. Never null.
		/// </summary>
		/// <param name="Settler">A settler. Null reads as a history of nothing.</param>
		public static List<string> PastOf(GameObject Settler)
		{
			return KingdomCreedRules.DecodeKept(Settler?.GetStringProperty(CreedPastProperty));
		}

		/// <summary>
		/// Whether this settler ALIGNS with a creed: they hold it now, or they have held it and
		/// left it. Addendum 16 clause (4), asked of one person.
		/// </summary>
		public static bool Aligns(GameObject Settler, string Creed)
		{
			if (Settler == null || string.IsNullOrEmpty(Creed))
			{
				return false;
			}
			if (string.Equals(Settler.GetStringProperty(CreedProperty), Creed, System.StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			return KingdomCreedRules.KeptHolds(Settler.GetStringProperty(CreedPastProperty), Creed);
		}

		/// <summary>
		/// Writes a creed into a settler's history and puts that whole history into the city's
		/// tally.
		/// <para>
		/// Called from <c>KingdomConversion.Convert</c> and nowhere else: the one conversion path
		/// is the one place a creed is ever LEFT, and a creed nobody left is not history, it is
		/// what they still believe. Side effects: the settler's
		/// <see cref="CreedPastProperty"/> may grow by one name, and every name in it is counted
		/// into <c>KingdomSystem.CreedPastCounts</c> &mdash; the whole history rather than the one
		/// name, because <see cref="Forget"/> ran a line earlier and took the rest of it out.
		/// </para>
		/// </summary>
		/// <param name="System">The realm.</param>
		/// <param name="Settler">The settler who has just left a creed.</param>
		/// <param name="Creed">The creed they left. Empty is an ordinary settler taking their
		/// first creed, which leaves nothing behind and records nothing.</param>
		/// <returns>True when the settler's own record actually grew.</returns>
		public static bool RememberPast(KingdomSystem System, GameObject Settler, string Creed)
		{
			if (System == null || Settler == null)
			{
				return false;
			}
			bool added;
			string kept = KingdomCreedRules.RememberKept(Settler.GetStringProperty(CreedPastProperty), Creed, out added);
			Settler.SetStringProperty(CreedPastProperty, string.IsNullOrEmpty(kept) ? null : kept);
			List<string> names = KingdomCreedRules.DecodeKept(kept);
			for (int i = 0; i < names.Count; i++)
			{
				System.CreedPastCounts.TryGetValue(names[i], out var count);
				System.CreedPastCounts[names[i]] = count + 1;
			}
			if (added)
			{
				KingdomLog.Log("creed: " + (Settler.ShortDisplayName ?? "settler") + " has now held with " + Creed + " and left it ("
					+ names.Count + "/" + KingdomCreedRules.MaxKeptCreeds + " remembered)");
			}
			return added;
		}

		// Takes one settler's whole history out of the city's tally. Never drives a count below
		// zero, and removes the entry outright at zero so the tally never grows a tail of creeds
		// nobody here has ever held -- which is the exact fact the visibility law reads.
		private static void DropPast(KingdomSystem System, GameObject Leaver)
		{
			List<string> names = PastOf(Leaver);
			for (int i = 0; i < names.Count; i++)
			{
				if (!System.CreedPastCounts.TryGetValue(names[i], out var count))
				{
					continue;
				}
				if (count > 1)
				{
					System.CreedPastCounts[names[i]] = count - 1;
				}
				else
				{
					System.CreedPastCounts.Remove(names[i]);
				}
			}
		}

		/// <summary>
		/// The kingdom's one attended pass over the two cities' tempers.
		/// <para>
		/// Preconditions: called from <c>ZoneActivatedEvent</c> on the realm's own ground, inside a
		/// <c>KingdomSystem.Guard</c>. Side effects: accrues dissent for the attended days since
		/// the last pass, may speak one line and write one chronicle entry, and may carry out a
		/// secession. Failure mode: returns having done nothing.
		/// </para>
		/// <para>
		/// Dissent runs the full elapsed (<see cref="KingdomRules.ElapsedDays"/>,
		/// <see cref="KingdomRules.AdvanceCheckpoint"/>), which is Addendum 8 clause 1: a quarrel
		/// between two cities is not something the founder's attention causes. The absence
		/// guarantee moved rather than went away, and it is now clause 3's: dissent CLAMPS at
		/// <see cref="KingdomCreedRules.DissentBreaking"/> and records a brink there, so a founder
		/// away a season and a founder away a thousand days come home to a realm standing in
		/// exactly the same place, told about it once with the real number of days, and holding
		/// <see cref="KingdomCreedRules.SecessionWindowDays"/> world-days in which pouring the
		/// rite or settling what the two cities believe still stops the split. Addendum 10(a): the
		/// window is the world's, so a founder who hears the word and stays away loses the city on
		/// the day it said they would &mdash; but nobody ever loses one unwarned.
		/// </para>
		/// </summary>
		public static void OnZoneActivated(KingdomSystem System, Zone Z)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || System.SettlementCount < KingdomSettlement.MaxSettlements || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			Reconcile(System);
			// A realm that has only just acquired its second city has no checkpoint yet. Starting
			// one costs nothing rather than charging the whole absence cap on the first pass.
			if (System.LastDissentTick <= 0)
			{
				System.LastDissentTick = timeTicks;
				return;
			}
			string here = SeatCreed(System);
			string there = AwayCreed(System);
			int hostility = HostilityBetween(here, there);
			if (KingdomBrink.CityStands())
			{
				// Nothing accrues past a brink, and the checkpoint is deliberately NOT advanced:
				// while the brink stands, LastDissentTick is the day the realm reached it, which
				// is what the announcement quotes and what the arrest resets.
				RunSecessionWindow(System, Z, hostility, timeTicks);
				return;
			}
			int days = KingdomRules.ElapsedDays(timeTicks - System.LastDissentTick);
			System.LastDissentTick = KingdomRules.AdvanceCheckpoint(System.LastDissentTick, timeTicks);
			if (days <= 0)
			{
				return;
			}
			int before = System.Dissent;
			System.Dissent = KingdomCreedRules.AccrueDissent(before, hostility, days);
			Announce(System, here, there);
			if (KingdomCreedRules.ClassifyTemper(System.Dissent) != CityTemper.Secession)
			{
				return;
			}
			// The breaking point was crossed somewhere inside the stretch, and the founder is owed
			// the day it was actually crossed on rather than the day they walked back in.
			long reached = KingdomBrinkRules.CrossingTick(
				timeTicks - (long)days * KingdomRules.TicksPerDay, timeTicks, before,
				KingdomCreedRules.DissentBreaking, KingdomCreedRules.DissentPerDay(hostility));
			RecordSecessionBrink(System, Z, here, there, reached, timeTicks);
		}

		// The realm reaches the breaking point: recorded, warned once by name with the honest
		// elapsed, and NOT acted on. This is the whole of what the clock rework owed secession --
		// the four-tier warning ladder used to end in a tier that had nothing to say, because by
		// the time it was reached the city was already gone.
		//
		// The warning is PUSHED (Addendum 10(a)). It goes to the founder wherever they stand, and
		// it names the arrest, because from here the window is the world's: nine days of it, and
		// then the city goes whether anybody came back or not.
		private static void RecordSecessionBrink(KingdomSystem System, Zone Z, string HereCreed, string ThereCreed, long ReachedTick, long NowTick)
		{
			if (!KingdomBrink.RecordCity(System, ReachedTick))
			{
				return;
			}
			KingdomBrink.MarkCityWarned(NowTick);
			SayTheCityBrink(System, Z, HereCreed, ThereCreed, NowTick);
		}

		// The realm's window, judged against the world's clock, and the arrest that ends it. Rule
		// 2: the quarrel is a fact re-derived every pass, so a realm whose creeds stopped clashing
		// -- or whose founder poured enough water to ease dissent back off the breaking point --
		// steps back from the edge and is told so, whenever they did it.
		//
		// What absence cannot do is start the clock. A realm carrying a brink nobody was ever
		// warned of (a save from before the word went out, a record made by a path that could not
		// speak) is warned here and gets the whole window from here.
		private static void RunSecessionWindow(KingdomSystem System, Zone Z, int Hostility, long NowTick)
		{
			string here = SeatCreed(System);
			string there = AwayCreed(System);
			string leaver;
			string kept;
			NameTheLeaver(System, here, there, out leaver, out kept);
			if (Hostility <= 0 || System.Dissent < KingdomCreedRules.DissentBreaking)
			{
				bool wasWarned = KingdomBrink.OfCity(System).Warned;
				if (KingdomBrink.LiftCity(System, NowTick))
				{
					if (wasWarned)
					{
						// Only what was actually said is unsaid.
						KingdomBrink.Unsay(System, BrinkKind.City, leaver, StandsInLeaver(System, Z, leaver), leaver);
					}
					Rearm(System);
				}
				return;
			}
			if (KingdomBrink.MarkCityWarned(NowTick))
			{
				SayTheCityBrink(System, Z, here, there, NowTick);
				return;
			}
			if (!KingdomBrink.CityWindowSpent(NowTick))
			{
				return;
			}
			long went = KingdomBrinkRules.ExpiryTick(BrinkKind.City, KingdomBrink.OfCity(System).WarnedTick);
			int ago = KingdomBrinkRules.DaysStood(went, NowTick);
			if (Secede(System, Forced: false, out var _))
			{
				KingdomWord.Aftermath(System, leaver, StandsInLeaver(System, Z, leaver),
					KingdomBrinkRules.FiredNote(BrinkKind.City, leaver, ago));
				KingdomBrink.LiftCity(System, NowTick);
				return;
			}
			// The realm would not let it go -- the second city was already lost some other way, or
			// the verdict refused. The window stays spent and is tried again on the next resolve
			// rather than being reset, so nothing is lost and nobody is warned twice.
		}

		// The city brink's own voice. KingdomWord PUSHES the loud four-tier speech this ladder has
		// always ended in -- to wherever the founder is standing, framed as word out of the city
		// it is about when that is not the one they are in -- and FILES the shared brink note in
		// the report and the chronicle. Two registers of one warning, said once each, exactly as
		// this tier has always been told; what changed is that the speech now travels.
		private static void SayTheCityBrink(KingdomSystem System, Zone Z, string HereCreed, string ThereCreed, long NowTick)
		{
			string leaver;
			string kept;
			NameTheLeaver(System, HereCreed, ThereCreed, out leaver, out kept);
			BrinkRecord brink = KingdomBrink.OfCity(System);
			KingdomBrink.Announce(System, BrinkKind.City, leaver, kept, brink, NowTick,
				StandsInLeaver(System, Z, leaver), leaver,
				KingdomCreedRules.SecessionBrinkSpeech(leaver, kept,
					KingdomBrinkRules.DaysStood(brink.ReachedTick, NowTick),
					KingdomBrinkRules.DaysLeft(BrinkKind.City, brink.WarnedTick, NowTick)));
		}

		// Whether the founder is standing in the city the news is about. A realm's brink is news
		// about ONE of its two cities, and when that city is the one they are not in, the word
		// reaches them from it rather than around them.
		private static bool StandsInLeaver(KingdomSystem System, Zone Z, string Leaver)
		{
			return KingdomWord.StandsIn(Z) && !string.IsNullOrEmpty(Leaver) && Leaver == System.SeatName;
		}

		// Which city the prose should name as the one that walks. A prediction rather than a
		// decision -- KingdomCreedRules.AwayIsTheLeaver decides it again, from the same facts, on
		// the day itself -- but it is deterministic in exactly those facts, so a founder told
		// which city is drawing up its own charter is told the truth.
		private static void NameTheLeaver(KingdomSystem System, string HereCreed, string ThereCreed, out string Leaver, out string Kept)
		{
			string awayName = (System.Away != null) ? System.Away.SettlementName : null;
			bool awayLeaves = KingdomCreedRules.AwayIsTheLeaver(
				Feeling(HereCreed, ThereCreed), Feeling(ThereCreed, HereCreed),
				System.Population, (System.Away != null) ? System.Away.Population : 0);
			Leaver = awayLeaves ? awayName : System.SeatName;
			Kept = awayLeaves ? System.SeatName : awayName;
		}

		/// <summary>Whether the founder may hold a rite of shared water here and now.</summary>
		public static bool RiteAvailable(KingdomSystem System)
		{
			return Enabled && System != null && System.Founded
				&& System.SettlementCount >= KingdomSettlement.MaxSettlements
				&& KingdomCreedRules.RiteCost(Temper(System)) > 0;
		}

		/// <summary>
		/// The rite of shared water: the seated city pours for the other city's people and drinks
		/// with them.
		/// <para>
		/// Preconditions: the realm holds two cities at odds, the founder is on the seated city's
		/// ground, its dedicated stores can bear
		/// <see cref="KingdomCreedRules.RiteCost"/>, and the rite is off cooldown. Side effects:
		/// drams are drained from the dedicated stores, dissent eases, and the day is chronicled.
		/// Failure mode: returns false with a founder-facing reason and changes nothing.
		/// </para>
		/// <para>
		/// Water is what founds this realm and water is what holds it together; this is the lever
		/// the mod is actually about. It cannot outrun the quarrel on its own at the worst
		/// hostility the game's data holds — it holds the line and gains slowly, at eighty drams
		/// every three days, which is the price of not choosing.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Z">The zone the founder is standing in.</param>
		/// <param name="Failure">Founder-facing reason, or empty on success.</param>
		public static bool HoldRite(KingdomSystem System, Zone Z, out string Failure)
		{
			Failure = "";
			if (!Enabled || System == null || !System.Founded || System.SettlementCount < KingdomSettlement.MaxSettlements)
			{
				Failure = "A rite of shared water is held between two cities. This realm has one.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "The rite is held on the realm's own ground.";
				return false;
			}
			CityTemper temper = Temper(System);
			int cost = KingdomCreedRules.RiteCost(temper);
			if (cost <= 0)
			{
				Failure = "There is nothing between your two cities that a basin of water would mend. Do not go looking for one.";
				return false;
			}
			if (!KingdomCreedRules.RiteReady(System.LastRiteTick, The.Game.TimeTicks))
			{
				Failure = "You poured for them too recently. A rite held every day is a habit, and a habit mends nothing.";
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.Take(Z);
			KingdomWaterDebit debit;
			if (!survey.TryReserveExactWater(cost, out debit))
			{
				Failure = "The rite requires exactly {{C|" + cost
					+ " drams}} from the dedicated stores, and they cannot provide it.";
				return false;
			}
			// Last safe point before the realm's cadence, dissent or history changes. This receipt
			// either drains the whole stated cost or restores every vessel it touched.
			if (!debit.Commit())
			{
				Failure = "The dedicated stores could not yield exactly {{C|" + cost
					+ " drams}}. No rite was held.";
				return false;
			}
			System.LastRiteTick = The.Game.TimeTicks;
			KingdomGovernanceScope.Commit("hold shared rite");
			System.Dissent = KingdomCreedRules.ApplyDissent(System.Dissent, -KingdomCreedRules.RiteEase(temper));
			string awayName = (System.Away != null) ? System.Away.SettlementName : null;
			KingdomChronicle.Record(System, KingdomCreedRules.RiteTelling(System.SeatName, awayName, cost));
			Popup.Show(KingdomCreedRules.RiteNotice(Temper(System), awayName));
			Rearm(System);
			return true;
		}

		/// <summary>
		/// The creeds the founder may declare the realm's own: whatever its two cities hold. Never
		/// null; empty when neither city has a creed, in which case there is nothing to choose
		/// between and the Charter should not offer the choice.
		/// </summary>
		public static List<string> DeclarableCreeds(KingdomSystem System)
		{
			List<string> creeds = new List<string>();
			if (!Enabled || System == null || !System.Founded)
			{
				return creeds;
			}
			string here = SeatCreed(System);
			string there = AwayCreed(System);
			if (!string.IsNullOrEmpty(here))
			{
				creeds.Add(here);
			}
			if (!string.IsNullOrEmpty(there) && there != here)
			{
				creeds.Add(there);
			}
			return creeds;
		}

		/// <summary>
		/// Declares one creed the realm's own, or takes the declaration back.
		/// <para>
		/// Side effects: sets <c>KingdomSystem.DeclaredCreed</c>, which raises that creed's weight
		/// in every future arrival to both cities. When the other city holds a creed, that faction
		/// is slighted: the realm's standing with it falls by
		/// <see cref="KingdomCreedRules.DeclarationStandingCost"/> — mirrored into the world, so
		/// its people think less of the realm everywhere — and dissent rises immediately by
		/// <see cref="KingdomCreedRules.DeclarationShock"/>. Failure mode: returns false with a
		/// founder-facing reason and changes nothing.
		/// </para>
		/// <para>
		/// This is the fast lever and the expensive one. It does not ease the quarrel; it decides
		/// it, by making one city's creed the one arriving settlers carry until the other city's
		/// creed thins below dominance and the clash simply stops being true.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="CreedFactionName">A creed from <see cref="DeclarableCreeds"/>, or null/empty
		/// to recant.</param>
		/// <param name="Failure">Founder-facing reason, or empty on success.</param>
		public static bool Declare(KingdomSystem System, string CreedFactionName, out string Failure)
		{
			Failure = "";
			if (!Enabled || System == null || !System.Founded)
			{
				Failure = "There is no realm to declare anything about.";
				return false;
			}
			if (string.IsNullOrEmpty(CreedFactionName))
			{
				if (string.IsNullOrEmpty(System.DeclaredCreed))
				{
					Failure = "The realm has said nothing about itself. There is nothing to unsay.";
					return false;
				}
				System.DeclaredCreed = null;
				KingdomGovernanceScope.Commit("recant creed");
				KingdomChronicle.Record(System, KingdomCreedRules.RecantTelling(System.KingdomDisplayName));
				Popup.Show("You unsay it. The roads go back to carrying whoever they were carrying before, and nobody is owed an explanation.");
				return true;
			}
			List<string> declarable = DeclarableCreeds(System);
			if (!declarable.Contains(CreedFactionName))
			{
				Failure = "A realm declares for what its own people already hold. Nobody here holds with " + CreedName(CreedFactionName) + ".";
				return false;
			}
			if (CreedFactionName == System.DeclaredCreed)
			{
				Failure = "You have said it already, and saying it twice is not louder.";
				return false;
			}
			string slighted = null;
			for (int i = 0; i < declarable.Count; i++)
			{
				if (declarable[i] != CreedFactionName)
				{
					slighted = declarable[i];
				}
			}
			System.DeclaredCreed = CreedFactionName;
			KingdomGovernanceScope.Commit("declare creed");
			if (!string.IsNullOrEmpty(slighted))
			{
				System.AdjustStanding(slighted, KingdomCreedRules.DeclarationStandingCost);
				System.Dissent = KingdomCreedRules.ApplyDissent(System.Dissent, KingdomCreedRules.DeclarationShock);
			}
			KingdomChronicle.Record(System, KingdomCreedRules.DeclarationTelling(System.KingdomDisplayName, CreedName(CreedFactionName)));
			Popup.Show(KingdomCreedRules.DeclarationNotice(CreedName(CreedFactionName), CreedName(slighted)));
			return true;
		}

		/// <summary>
		/// Eases the quarrel because the founder called a shared meal while it was running. Called
		/// from the meal itself rather than being a lever of its own: the founder already paid for
		/// the food, and this is the meal being worth more than its calories when there is
		/// something to mend.
		/// </summary>
		/// <param name="System">The kingdom. Does nothing for a one-city realm or a realm at
		/// peace, so a founder who holds one city never learns this exists.</param>
		public static void EaseForMeal(KingdomSystem System)
		{
			if (!Enabled || System == null || !System.Founded || System.SettlementCount < KingdomSettlement.MaxSettlements || System.Dissent <= 0)
			{
				return;
			}
			System.Dissent = KingdomCreedRules.ApplyDissent(System.Dissent, -KingdomCreedRules.MealEase);
			MessageQueue.AddPlayerMessage("{{G|Word of the meal reaches " + OtherCityName(System) + " before the plates are cold.}}");
			Rearm(System);
		}

		/// <summary>Whether the ground belongs to a city that left the realm.</summary>
		/// <param name="ZoneID">A zone id. Null and empty read as false.</param>
		public static bool SecededHolds(KingdomSystem System, string ZoneID)
		{
			return System != null && System.Seceded != null && !string.IsNullOrEmpty(ZoneID) && System.Seceded.ClaimedZones.Contains(ZoneID);
		}

		/// <summary>
		/// The unhappier of the realm's two cities leaves it.
		/// <para>
		/// Preconditions: the realm holds two cities whose creeds clash, and dissent has reached
		/// <see cref="KingdomCreedRules.DissentBreaking"/> — or <paramref name="Forced"/> is set,
		/// which is the debug path and skips the dissent requirement and nothing else. Side
		/// effects: the leaving city moves whole into <c>KingdomSystem.Seceded</c>, the realm keeps
		/// the other as its seat, dissent is cleared, both chronicle registers record the day in
		/// their own words, and a modal states what has changed. Failure mode: returns false with a
		/// founder-facing refusal and changes nothing.
		/// </para>
		/// <para>
		/// Nothing physical is touched, for the same reasons exile does not touch anything: no
		/// citizen's allegiance key moves, no faction is minted or unmade, no zone is stripped, no
		/// vessel loses its dedication. The seceded city's people still carry the realm's faction
		/// property and are not hostile. They simply are not yours, and the ground they stand on
		/// stops being in <c>ClaimedZones</c>, so every kingdom pass skips it.
		/// </para>
		/// </summary>
		public static bool Secede(KingdomSystem System, bool Forced, out string Refusal)
		{
			Refusal = "";
			if (System == null || !System.Founded)
			{
				Refusal = "There is no realm to leave.";
				return false;
			}
			string here = SeatCreed(System);
			string there = AwayCreed(System);
			SecessionVerdict verdict = KingdomCreedRules.JudgeSecession(System.SettlementCount, HostilityBetween(here, there), System.Dissent, Forced);
			if (verdict != SecessionVerdict.Warranted)
			{
				Refusal = SecessionRefusal(verdict);
				return false;
			}
			bool awayLeaves = KingdomCreedRules.AwayIsTheLeaver(
				Feeling(here, there), Feeling(there, here),
				System.Population, (System.Away != null) ? System.Away.Population : 0);
			// Read before anything moves: once the seat is exchanged, "the seated city" names the
			// other one and every string below would be about the wrong place.
			string keptName = awayLeaves ? System.SeatName : ((System.Away != null) ? System.Away.SettlementName : null);
			string leaverName = awayLeaves ? ((System.Away != null) ? System.Away.SettlementName : null) : System.SeatName;
			string leaverCreed = CreedName(awayLeaves ? there : here);
			int leaverPopulation = awayLeaves ? ((System.Away != null) ? System.Away.Population : 0) : System.Population;
			if (awayLeaves)
			{
				System.Seceded = System.Away;
			}
			else
			{
				// Capture-then-Restore is the sanctioned exchange (see KingdomSystem.TrySeat): the
				// two share their containers for exactly as long as it takes to write the survivor
				// over the flat fields.
				KingdomSettlement leaving = System.Capture();
				System.Restore(System.Away);
				System.Seceded = leaving;
			}
			System.Away = null;
			System.SecededTick = The.Game.TimeTicks;
			System.Dissent = 0;
			System.DissentSpoken = 0;
			KingdomBrink.LiftCity(System, The.Game.TimeTicks);
			System.LastDissentTick = The.Game.TimeTicks;
			KingdomChronicle.RecordDisputed(System,
				KingdomCreedRules.SecessionTelling(leaverName, keptName, leaverCreed),
				KingdomCreedRules.SecessionRumour(leaverName, KingdomChronicle.FounderName()));
			Popup.Show(KingdomCreedRules.SecessionNotice(leaverName, keptName, leaverCreed, leaverPopulation));
			KingdomLog.Log("secession: " + (leaverName ?? "-") + " left; realm keeps " + (keptName ?? "-"));
			return true;
		}

		/// <summary>
		/// Takes a seceded city back into the realm.
		/// <para>
		/// Preconditions: a city seceded, the realm has room for it, the founder is standing on its
		/// own ground, the clash that split them is no longer live, and the realm's standing with
		/// the city's creed is not contemptible — see
		/// <see cref="KingdomCreedRules.JudgeRejoin"/>. Side effects: the city returns as the
		/// realm's second, the seat moves into it because the founder is standing there, dissent is
		/// cleared, both registers record the day, and a modal states it. Failure mode: returns
		/// false with a founder-facing refusal and changes nothing.
		/// </para>
		/// <para>
		/// The cause has to be gone before the city is. A founder who walks back with the same
		/// clash still live is told exactly that and sent away — waiting it out is not a route,
		/// because a quarrel nobody did anything about has not ended.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Site">The zone the founder is standing in.</param>
		/// <param name="Refusal">Founder-facing reason, or empty on success.</param>
		public static bool TryRejoin(KingdomSystem System, Zone Site, out string Refusal)
		{
			Refusal = "";
			if (System == null || !System.Founded)
			{
				Refusal = "There is no realm for anyone to come back to.";
				return false;
			}
			string leaverCreed = CreedOf(System.Seceded);
			RejoinVerdict verdict = KingdomCreedRules.JudgeRejoin(
				System.Seceded != null,
				System.SettlementCount,
				Site != null && SecededHolds(System, Site.ZoneID),
				HostilityBetween(leaverCreed, SeatCreed(System)),
				System.GetStanding(leaverCreed));
			string leaverName = (System.Seceded != null) ? System.Seceded.SettlementName : null;
			if (verdict != RejoinVerdict.Allowed)
			{
				Refusal = KingdomCreedRules.RejoinRefusal(verdict, leaverName, CreedName(leaverCreed));
				return false;
			}
			System.Away = System.Seceded;
			KingdomGovernanceScope.Commit("rejoin city");
			System.Seceded = null;
			System.SecededTick = 0;
			System.Dissent = 0;
			System.DissentSpoken = 0;
			// A realm that has taken a city back is not standing at a brink, whatever the state
			// slot was left holding by a secession that has since been undone.
			KingdomBrink.LiftCity(System, The.Game.TimeTicks);
			System.LastDissentTick = The.Game.TimeTicks;
			// The founder is standing in it, so it becomes the seat by the ordinary route rather
			// than a second one invented here.
			System.TrySeat(Site);
			KingdomChronicle.RecordDisputed(System,
				KingdomCreedRules.RejoinTelling(leaverName),
				KingdomCreedRules.RejoinRumour(leaverName, KingdomChronicle.FounderName()));
			Popup.Show(KingdomCreedRules.RejoinNotice(leaverName, System.KingdomDisplayName));
			KingdomLog.Log("rejoin: " + (leaverName ?? "-") + " came back");
			return true;
		}

		/// <summary>The realm's temper between its two cities. Concord for anything else.</summary>
		public static CityTemper Temper(KingdomSystem System)
		{
			if (System == null || !System.Founded || System.SettlementCount < KingdomSettlement.MaxSettlements)
			{
				return CityTemper.Concord;
			}
			return KingdomCreedRules.ClassifyTemper(System.Dissent);
		}

		/// <summary>
		/// What the Charter shows about the two cities. Always available while the realm holds two,
		/// whatever the temper, so the founder can watch this coming from the first muttering
		/// rather than being told about it once.
		/// </summary>
		/// <returns>The report, or a line explaining why there is nothing to report.</returns>
		public static string Report(KingdomSystem System)
		{
			if (!Enabled)
			{
				return "You are not keeping account of what your cities believe.";
			}
			if (System == null || !System.Founded)
			{
				return "There is no realm to take the temper of.";
			}
			if (System.Seceded != null)
			{
				return "{{C|" + (System.Seceded.SettlementName ?? "One of your cities") + "}} left the realm and has not come back. It is "
					+ KingdomCreedRules.CreedClause(CreedName(CreedOf(System.Seceded))) + ", and it still keeps everything it kept.\n\n"
					+ "Stand on their ground and ask, when what split you is no longer true.";
			}
			if (System.SettlementCount < KingdomSettlement.MaxSettlements)
			{
				return "{{C|" + System.SeatName + "}} is " + KingdomCreedRules.CreedClause(CreedName(SeatCreed(System)))
					+ ".\n\nOne city cannot fall out with itself. Nothing here needs watching.";
			}
			string report = KingdomCreedRules.TemperReport(Temper(System), System.Dissent, System.SeatName,
				(System.Away != null) ? System.Away.SettlementName : null,
				CreedName(SeatCreed(System)), CreedName(AwayCreed(System)));
			if (!string.IsNullOrEmpty(System.DeclaredCreed))
			{
				report += "\n\nThe realm has declared for {{C|" + CreedName(System.DeclaredCreed) + "}}. Whoever walks the roads here knows it.";
			}
			return report;
		}

		/// <summary>
		/// The factions a settler arriving here could plausibly hold with: everyone on the realm's
		/// standings ledger, plus whatever its cities already hold and whatever it declared, minus
		/// the realm's own faction and anything <see cref="CanBeCreed"/> rejects.
		/// </summary>
		public static List<string> Candidates(KingdomSystem System)
		{
			List<string> candidates = new List<string>();
			foreach (KeyValuePair<string, int> standing in System.Standings)
			{
				Consider(System, candidates, standing.Key);
			}
			foreach (KeyValuePair<string, int> held in System.CreedCounts)
			{
				Consider(System, candidates, held.Key);
			}
			if (System.Away != null)
			{
				foreach (KeyValuePair<string, int> held in System.Away.CreedCounts)
				{
					Consider(System, candidates, held.Key);
				}
			}
			Consider(System, candidates, System.DeclaredCreed);
			return candidates;
		}

		private static void Consider(KingdomSystem System, List<string> Candidates, string FactionName)
		{
			if (string.IsNullOrEmpty(FactionName) || FactionName == System.KingdomFactionName || FactionName == "Player" || Candidates.Contains(FactionName))
			{
				return;
			}
			if (CanBeCreed(Factions.GetIfExists(FactionName)))
			{
				Candidates.Add(FactionName);
			}
		}

		/// <summary>
		/// Speaks and chronicles a worsening, once per tier. The hysteresis is
		/// <see cref="KingdomCreedRules.RememberedTemper"/>: jitter across one threshold says
		/// nothing further, and only easing all the way back to concord re-arms the ladder.
		/// </summary>
		private static void Announce(KingdomSystem System, string HereCreed, string ThereCreed)
		{
			CityTemper temper = KingdomCreedRules.ClassifyTemper(System.Dissent);
			CityTemper spoken = (CityTemper)System.DissentSpoken;
			bool speak = KingdomCreedRules.ShouldSpeak(temper, spoken);
			System.DissentSpoken = (int)KingdomCreedRules.RememberedTemper(temper, spoken);
			if (!speak)
			{
				return;
			}
			string awayName = (System.Away != null) ? System.Away.SettlementName : null;
			string speech = KingdomCreedRules.TemperSpeech(temper, System.SeatName, awayName);
			if (!string.IsNullOrEmpty(speech))
			{
				MessageQueue.AddPlayerMessage(speech + " {{K|(Charter: how your cities hold each other)}}");
			}
			string entry = KingdomCreedRules.TemperChronicle(temper, System.SeatName, awayName);
			if (!string.IsNullOrEmpty(entry))
			{
				KingdomChronicle.Record(System, entry);
			}
			KingdomLog.Log("dissent: " + System.Dissent + " temper=" + temper + " here=" + (HereCreed ?? "-") + " there=" + (ThereCreed ?? "-"));
		}

		/// <summary>Re-arms the spoken ladder after a lever eased the quarrel, so a founder who
		/// mends it and then lets it slip is warned again rather than silently.</summary>
		private static void Rearm(KingdomSystem System)
		{
			System.DissentSpoken = (int)KingdomCreedRules.RememberedTemper(KingdomCreedRules.ClassifyTemper(System.Dissent), (CityTemper)System.DissentSpoken);
			if (System.Dissent >= KingdomCreedRules.DissentBreaking || !KingdomBrink.CityStands())
			{
				return;
			}
			// A lever eased it back off the breaking point. The brink goes on the spot rather than
			// on the next pass -- the founder poured the water, and they are owed the answer in
			// the same breath -- and the clock restarts from now, so the days the realm spent
			// standing at the edge are not billed again the moment it steps back.
			string leaver;
			string kept;
			NameTheLeaver(System, SeatCreed(System), AwayCreed(System), out leaver, out kept);
			bool wasWarned = KingdomBrink.OfCity(System).Warned;
			KingdomBrink.LiftCity(System, The.Game.TimeTicks);
			if (wasWarned)
			{
				// Only what was actually said is unsaid.
				KingdomBrink.Unsay(System, BrinkKind.City, leaver, leaver == System.SeatName, leaver);
			}
		}

		/// <summary>
		/// Trims a city's creed tally back to something its population can support. Deaths that
		/// nothing attributed — a raid, a fall, a save from before this build — leave counts above
		/// the roll they were counted from, and a count that outlives its believers would make a
		/// city's creed permanent. Self-healing rather than a migration, because the tally is
		/// knowledge and not a ledger.
		/// </summary>
		private static void Reconcile(KingdomSystem System)
		{
			Trim(System.CreedCounts, System.Population);
			// And the history, on exactly the same terms and for a sharper reason. A past tally
			// standing above the roll it was counted from would keep a creed-work VISIBLE in a city
			// where nobody who ever held that creed is still alive -- and the visibility law is the
			// one gate that shows nothing rather than refusing out loud, so the error would read as
			// a design that simply exists rather than as one the city has no path to. Each entry
			// counts PEOPLE, so the population is its ceiling exactly as it is the present tally's,
			// even though the sum across creeds may legitimately exceed it (one person can be
			// remembered under MaxKeptCreeds names).
			Trim(System.CreedPastCounts, System.Population);
			if (System.Away != null)
			{
				Trim(System.Away.CreedCounts, System.Away.Population);
				Trim(System.Away.CreedPastCounts, System.Away.Population);
			}
		}

		private static void Trim(Dictionary<string, int> Counts, int Population)
		{
			if (Counts == null || Counts.Count == 0)
			{
				return;
			}
			List<string> held = new List<string>(Counts.Keys);
			int room = (Population > 0) ? Population : 0;
			foreach (string creed in held)
			{
				int count = Counts[creed];
				if (count <= 0 || room <= 0)
				{
					Counts.Remove(creed);
					continue;
				}
				if (count > room)
				{
					Counts[creed] = room;
				}
			}
		}

		/// <summary>
		/// The creed line <c>kingdom:dump</c> appends: what the seated city holds with, and what it
		/// has HELD AND LEFT. The second half is the one the alignment gate reads and the
		/// visibility law hides designs on, so it has to be visible somewhere a tester can look
		/// (STANDARDS 7b: a thing that decides what a founder may see says so out loud somewhere).
		/// </summary>
		/// <param name="System">The realm. Null or unfounded reports an empty string.</param>
		public static string DumpLine(KingdomSystem System)
		{
			if (System == null || !System.Founded)
			{
				return "";
			}
			System.Text.StringBuilder line = new System.Text.StringBuilder("\nCreeds held: ");
			line.Append(Tally(System.CreedCounts));
			line.Append("   once held: ").Append(Tally(System.CreedPastCounts));
			return line.ToString();
		}

		// Sorted by name so two runs of the same state print the same line: a dump nobody can diff
		// is a dump nobody reads twice.
		private static string Tally(Dictionary<string, int> Counts)
		{
			if (Counts == null || Counts.Count == 0)
			{
				return "(none)";
			}
			List<string> names = new List<string>(Counts.Keys);
			names.Sort(System.StringComparer.Ordinal);
			List<string> parts = new List<string>();
			for (int i = 0; i < names.Count; i++)
			{
				parts.Add(names[i] + " x" + Counts[names[i]]);
			}
			return string.Join(", ", parts.ToArray());
		}

		private static string OtherCityName(KingdomSystem System)
		{
			string name = (System.Away != null) ? System.Away.SettlementName : null;
			return string.IsNullOrEmpty(name) ? "your other city" : ("{{C|" + name + "}}");
		}

		private static string SecessionRefusal(SecessionVerdict Verdict)
		{
			switch (Verdict)
			{
			case SecessionVerdict.OneCity:
				return "A realm of one city has nobody to fall out with.";
			case SecessionVerdict.NoClash:
				return "Your two cities hold nothing against each other worth leaving over.";
			default:
				return "It has not come to that, and it is not going to today.";
			}
		}
	}
}
