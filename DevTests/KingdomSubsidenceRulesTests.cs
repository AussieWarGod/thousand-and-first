#if TAF_TESTS
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Subsidence, the summation that feeds it, and the stage ladder it moves.
	/// <para>
	/// The clock-rework audit called this the riskiest row in the wave for one reason: it was the
	/// only one with no tests to re-pin, so a wrong answer would fail silently. These are those
	/// tests. They pin the shape of a slide (where it starts, where it stops, what it can never do
	/// on the way), the ladder in both directions, the fact that ruin damages and never deletes,
	/// and that the same question asked twice gets the same answer.
	/// </para>
	/// </summary>
	public class KingdomSubsidenceRulesTests
	{
		private const int Capacity = 1024;

		private static void AssertPublicFields(System.Type type, string[] expectedNames, System.Type[] expectedTypes)
		{
			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
			Assert.AreEqual(expectedNames.Length, fields.Length, type.FullName + " field count changed");
			for (int i = 0; i < fields.Length; i++)
			{
				Assert.AreEqual(expectedNames[i], fields[i].Name, type.FullName + " field order changed");
				Assert.AreEqual(expectedTypes[i], fields[i].FieldType, type.FullName + "." + fields[i].Name + " type changed");
			}
		}

		[Test]
		public void NestedSaveAndPublicShapesKeepTheirExactAbi()
		{
			System.Type rules = typeof(KingdomSubsidenceRules);
			Assert.AreEqual("ThousandAndFirst.KingdomSubsidenceRules", rules.FullName);
			Assert.IsTrue(rules.IsAbstract && rules.IsSealed, "rules authority stopped being static");

			System.Type sighting = typeof(KingdomSubsidenceRules.ZoneSighting);
			System.Type breakpoint = typeof(KingdomSubsidenceRules.Breakpoint);
			System.Type trajectory = typeof(KingdomSubsidenceRules.Trajectory);
			System.Type channel = typeof(KingdomSubsidenceRules.SubsidenceChannel);
			Assert.AreEqual("ThousandAndFirst.KingdomSubsidenceRules+ZoneSighting", sighting.FullName);
			Assert.AreEqual("ThousandAndFirst.KingdomSubsidenceRules+Breakpoint", breakpoint.FullName);
			Assert.AreEqual("ThousandAndFirst.KingdomSubsidenceRules+Trajectory", trajectory.FullName);
			Assert.AreEqual("ThousandAndFirst.KingdomSubsidenceRules+SubsidenceChannel", channel.FullName);
			Assert.IsTrue(sighting.IsNestedPublic && breakpoint.IsNestedPublic && trajectory.IsNestedPublic && channel.IsNestedPublic);

			AssertPublicFields(sighting,
				new[] { "Water", "Food", "Roof", "StorageCapacity", "SeenTick" },
				new[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(long) });
			AssertPublicFields(breakpoint,
				new[] { "Day", "From", "To", "Population" },
				new[] { typeof(int), typeof(GrowthStage), typeof(GrowthStage), typeof(int) });
			AssertPublicFields(trajectory,
				new[] { "Population", "Stage", "Departed", "Steps", "Arrived", "Breakpoints" },
				new[] { typeof(int), typeof(GrowthStage), typeof(int), typeof(int), typeof(bool),
					typeof(List<KingdomSubsidenceRules.Breakpoint>) });
			Assert.AreEqual(typeof(int), System.Enum.GetUnderlyingType(channel));
			Assert.AreEqual("1:Ruin,2:Severity", string.Join(",", System.Array.ConvertAll(
				(KingdomSubsidenceRules.SubsidenceChannel[])System.Enum.GetValues(channel),
				value => ((int)value) + ":" + value)));
		}

		private static KingdomCatalogueRules.SupportTally Tally(int Water, int Food, int Roof, int Lift = 0, int Works = 1)
		{
			KingdomCatalogueRules.SupportTally tally = default(KingdomCatalogueRules.SupportTally);
			tally.Water = Water;
			tally.Food = Food;
			tally.Roof = Roof;
			tally.Lift = Lift;
			tally.Works = Works;
			return tally;
		}

		// ==================================================================================
		// A city is every zone it holds, as each was last seen.
		// ==================================================================================

		private static KingdomSubsidenceRules.ZoneSighting Seen(int Water, int Food, int Roof, int Storage, long Tick)
		{
			return new KingdomSubsidenceRules.ZoneSighting(Water, Food, Roof, Storage, Tick);
		}

		[Test]
		public void ACityIsMeasuredFromEveryZoneItHoldsNotTheOneYouWalkedInThrough()
		{
			// The defect: SupportedLevel was written from the visited zone's survey alone, so a
			// two-zone city entered through the mine forgot the granary zone entirely.
			KingdomCatalogueRules.SupportTally mine = Tally(Water: 6, Food: 0, Roof: 4);
			List<KingdomSubsidenceRules.ZoneSighting> granary = new List<KingdomSubsidenceRules.ZoneSighting>
			{
				Seen(20, 26, 12, 400, 5000L)
			};
			KingdomCatalogueRules.SupportTally city = KingdomSubsidenceRules.CityTally(mine, granary);
			Assert.AreEqual(26, city.Water);
			Assert.AreEqual(26, city.Food);
			Assert.AreEqual(16, city.Roof);
		}

		[Test]
		public void TheCityLevelDoesNotSwingWithWhichZoneTheFounderStandsIn()
		{
			// The property the fix is for, stated directly: the same two zones, entered either
			// way, answer the same level.
			KingdomCatalogueRules.SupportTally mine = Tally(Water: 6, Food: 2, Roof: 4);
			KingdomCatalogueRules.SupportTally granary = Tally(Water: 20, Food: 26, Roof: 12);
			int fromMine = KingdomSubsidenceRules.SupportedLevel(
				KingdomSubsidenceRules.CityTally(mine, new List<KingdomSubsidenceRules.ZoneSighting> { Seen(20, 26, 12, 0, 5000L) }),
				GrowthStage.Town);
			int fromGranary = KingdomSubsidenceRules.SupportedLevel(
				KingdomSubsidenceRules.CityTally(granary, new List<KingdomSubsidenceRules.ZoneSighting> { Seen(6, 2, 4, 0, 5000L) }),
				GrowthStage.Town);
			Assert.AreEqual(fromMine, fromGranary);
			Assert.IsTrue(fromMine > KingdomSubsidenceRules.SupportedLevel(mine, GrowthStage.Town),
				"the city carries more than the zone the founder happened to walk in through");
		}

		[Test]
		public void TheLiftingHalfIsCarriedAcrossUntouchedRatherThanSummedTwice()
		{
			// ScopedSupports has ALREADY summed the city's lifts through
			// KingdomReach.CityShadeExcept (Addendum 6). Adding the other zones' lifts here would
			// count every shrine in the realm twice.
			KingdomCatalogueRules.SupportTally here = Tally(Water: 10, Food: 10, Roof: 10, Lift: 7);
			KingdomCatalogueRules.SupportTally city = KingdomSubsidenceRules.CityTally(here,
				new List<KingdomSubsidenceRules.ZoneSighting> { Seen(5, 5, 5, 0, 900L) });
			Assert.AreEqual(7, city.Lift);
			Assert.AreEqual(here.Works, city.Works, "the works count belongs to the ground that was walked");
		}

		[Test]
		public void AZoneNobodyHasEverStoodInContributesNothing()
		{
			// Knowledge, not truth. An unvisited claim has no sighting, and inventing one would
			// credit the city with works nobody has seen raised.
			KingdomCatalogueRules.SupportTally here = Tally(Water: 10, Food: 10, Roof: 10);
			KingdomCatalogueRules.SupportTally city = KingdomSubsidenceRules.CityTally(here,
				new List<KingdomSubsidenceRules.ZoneSighting> { Seen(99, 99, 99, 999, 0L) });
			Assert.AreEqual(here.Water, city.Water);
			Assert.AreEqual(here.Food, city.Food);
			Assert.AreEqual(here.Roof, city.Roof);
			Assert.AreEqual(0, KingdomSubsidenceRules.SightedZones(new List<KingdomSubsidenceRules.ZoneSighting> { Seen(99, 99, 99, 999, 0L) }));
		}

		[Test]
		public void AOneZoneCityIsMeasuredExactlyAsItAlwaysWas()
		{
			KingdomCatalogueRules.SupportTally here = Tally(Water: 30, Food: 20, Roof: 25, Lift: 3);
			Assert.AreEqual(KingdomSubsidenceRules.SupportedLevel(here, GrowthStage.Town),
				KingdomSubsidenceRules.SupportedLevel(KingdomSubsidenceRules.CityTally(here, null), GrowthStage.Town));
			Assert.AreEqual(KingdomSubsidenceRules.SupportedLevel(here, GrowthStage.Town),
				KingdomSubsidenceRules.SupportedLevel(
					KingdomSubsidenceRules.CityTally(here, new List<KingdomSubsidenceRules.ZoneSighting>()), GrowthStage.Town));
		}

		[Test]
		public void TwoZonesSeenAtDifferentTicksBothCountAndTheOlderDatesTheReading()
		{
			List<KingdomSubsidenceRules.ZoneSighting> others = new List<KingdomSubsidenceRules.ZoneSighting>
			{
				Seen(10, 0, 0, 100, 9000L),
				Seen(0, 12, 0, 50, 3000L)
			};
			KingdomCatalogueRules.SupportTally city = KingdomSubsidenceRules.CityTally(Tally(4, 4, 4), others);
			Assert.AreEqual(14, city.Water, "both sightings count however old either is");
			Assert.AreEqual(16, city.Food);
			Assert.AreEqual(3000L, KingdomSubsidenceRules.OldestSighting(others), "the reading is only as fresh as its oldest part");
			Assert.AreEqual(2, KingdomSubsidenceRules.SightedZones(others));
		}

		[Test]
		public void AnOldSightingNeverAgesIntoSomethingElse()
		{
			// The staleness doctrine: nothing is simulated forward. The same sighting summed
			// twice, however much time has passed between, gives the same number.
			List<KingdomSubsidenceRules.ZoneSighting> others = new List<KingdomSubsidenceRules.ZoneSighting> { Seen(26, 0, 8, 300, 12L) };
			KingdomCatalogueRules.SupportTally first = KingdomSubsidenceRules.CityTally(Tally(2, 2, 2), others);
			KingdomCatalogueRules.SupportTally second = KingdomSubsidenceRules.CityTally(Tally(2, 2, 2), others);
			Assert.AreEqual(first.Water, second.Water);
			Assert.AreEqual(first.Roof, second.Roof);
			Assert.AreEqual(28, first.Water);
		}

		[Test]
		public void TheCityTallyIsOrderIndependentAndDeterministic()
		{
			List<KingdomSubsidenceRules.ZoneSighting> forward = new List<KingdomSubsidenceRules.ZoneSighting>
			{
				Seen(3, 1, 2, 10, 100L), Seen(7, 5, 4, 20, 200L), Seen(1, 9, 6, 30, 300L)
			};
			List<KingdomSubsidenceRules.ZoneSighting> backward = new List<KingdomSubsidenceRules.ZoneSighting>
			{
				Seen(1, 9, 6, 30, 300L), Seen(7, 5, 4, 20, 200L), Seen(3, 1, 2, 10, 100L)
			};
			KingdomCatalogueRules.SupportTally a = KingdomSubsidenceRules.CityTally(Tally(1, 1, 1), forward);
			KingdomCatalogueRules.SupportTally b = KingdomSubsidenceRules.CityTally(Tally(1, 1, 1), backward);
			Assert.AreEqual(a.Water, b.Water);
			Assert.AreEqual(a.Food, b.Food);
			Assert.AreEqual(a.Roof, b.Roof);
			Assert.AreEqual(KingdomSubsidenceRules.OldestSighting(forward), KingdomSubsidenceRules.OldestSighting(backward));
			Assert.AreEqual(KingdomSubsidenceRules.CityStorage(5, forward), KingdomSubsidenceRules.CityStorage(5, backward));
		}

		[Test]
		public void ANegativeOrHostileSightingCannotDragTheCityDown()
		{
			// A corrupted or third-party-written game-state slot must never be able to invent a
			// shortfall out of nothing.
			KingdomCatalogueRules.SupportTally here = Tally(20, 20, 20);
			KingdomCatalogueRules.SupportTally city = KingdomSubsidenceRules.CityTally(here,
				new List<KingdomSubsidenceRules.ZoneSighting> { Seen(-500, -500, -500, -500, 400L) });
			Assert.AreEqual(20, city.Water);
			Assert.AreEqual(20, city.Food);
			Assert.AreEqual(20, city.Roof);
			Assert.AreEqual(20, KingdomSubsidenceRules.CityStorage(20, new List<KingdomSubsidenceRules.ZoneSighting> { Seen(0, 0, 0, -900, 400L) }));
		}

		[Test]
		public void TheStageLadderReadsTheCitysCasksNotOneZonesCasks()
		{
			// StageWithHysteresis reads storage, so a city whose casks stand in the zone next
			// door must be measured against all of them or it demotes itself the moment the
			// founder walks in through the wrong side.
			List<KingdomSubsidenceRules.ZoneSighting> others = new List<KingdomSubsidenceRules.ZoneSighting> { Seen(0, 0, 0, 900, 700L) };
			Assert.AreEqual(1100, KingdomSubsidenceRules.CityStorage(200, others));
			Assert.AreEqual(200, KingdomSubsidenceRules.CityStorage(200, null));
			Assert.IsTrue(KingdomSubsidenceRules.StageWithHysteresis(GrowthStage.Camp, 40, KingdomSubsidenceRules.CityStorage(200, others))
				>= KingdomSubsidenceRules.StageWithHysteresis(GrowthStage.Camp, 40, 200),
				"the whole city's stores never read as less than one zone's");
		}

		[Test]
		public void AReadingWhollyOfThisPassIsNotDated()
		{
			Assert.IsNull(KingdomSubsidenceRules.SightingClause(0, 0), "a one-zone city has nothing to date");
			Assert.IsNull(KingdomSubsidenceRules.SightingClause(0, 40));
		}

		[Test]
		public void AReadingPartlyOutOfMemorySaysHowOldTheMemoryIs()
		{
			Assert.IsTrue(KingdomSubsidenceRules.SightingClause(1, 0).Contains("walked today"));
			Assert.IsTrue(KingdomSubsidenceRules.SightingClause(1, 1).Contains("a day ago"));
			string old = KingdomSubsidenceRules.SightingClause(2, 40);
			Assert.IsTrue(old.Contains("2 parasangs"), "how much of the reading is memory");
			Assert.IsTrue(old.Contains("40 days ago"), "and how old the memory is");
		}

		// ==================================================================================
		// The summation: the piece that was missing between the arithmetic and a consumer.
		// ==================================================================================

		[TestCase(8, 100, 8)]
		[TestCase(8, 50, 4)]
		[TestCase(8, 10, 0)]
		[TestCase(8, 0, 0)]
		[TestCase(8, -20, 0)]
		[TestCase(8, 250, 8)]
		[TestCase(0, 100, 0)]
		public void Carried_ScalesADeclaredAmountByHowWellTheWorkRuns(int amount, int percent, int expected)
		{
			Assert.AreEqual(expected, KingdomCatalogueRules.Carried(amount, percent));
		}

		[Test]
		public void Carried_FloorsHonestlyWhereAReachLiftFloorsAtOne()
		{
			// KingdomReachRules.Scaled deliberately floors a positive contribution at one: a
			// barely-tended shrine still shades the ground it stands on. A binding support may not
			// do that, because "one settler fed" is a claim about a person who eats.
			Assert.AreEqual(1, KingdomReachRules.Scaled(8, 10));
			Assert.AreEqual(0, KingdomCatalogueRules.Carried(8, 10));
		}

		[Test]
		public void FoldWork_SortsEachSupportIntoItsOwnColumn()
		{
			List<KindAmount> carries;
			KingdomCatalogueRules.TryParseTally("water:5,food:3,roof:9,craft:2", out carries, out _);
			KingdomCatalogueRules.SupportTally folded =
				KingdomCatalogueRules.FoldWork(default(KingdomCatalogueRules.SupportTally), carries, 100);
			Assert.AreEqual(5, folded.Water);
			Assert.AreEqual(3, folded.Food);
			Assert.AreEqual(9, folded.Roof);
			Assert.AreEqual(2, folded.Lift);
			Assert.AreEqual(1, folded.Works);
		}

		[Test]
		public void FoldWork_LiftsAKindItHasNeverHeardOf()
		{
			// The catalogue's own rule, applied to the sum: a support this build does not know is
			// somebody else's good, and it lifts rather than faulting.
			List<KindAmount> carries;
			KingdomCatalogueRules.TryParseTally("moonlight:6", out carries, out _);
			KingdomCatalogueRules.SupportTally folded =
				KingdomCatalogueRules.FoldWork(default(KingdomCatalogueRules.SupportTally), carries, 100);
			Assert.AreEqual(6, folded.Lift);
			Assert.AreEqual(0, folded.Water);
		}

		[Test]
		public void FoldWork_CountsAWorkThatCarriesNothing()
		{
			// A palisade carries no support and is still something standing, which is what lets a
			// caller tell "nothing stands here" from "everything here carries nothing".
			KingdomCatalogueRules.SupportTally folded =
				KingdomCatalogueRules.FoldWork(default(KingdomCatalogueRules.SupportTally), null, 100);
			Assert.AreEqual(1, folded.Works);
			Assert.AreEqual(0, folded.Water + folded.Food + folded.Roof + folded.Lift);
		}

		[Test]
		public void FoldWork_LeavesTheTallyItWasHandedAlone()
		{
			KingdomCatalogueRules.SupportTally running = Tally(4, 4, 4);
			List<KindAmount> carries;
			KingdomCatalogueRules.TryParseTally("water:10", out carries, out _);
			KingdomCatalogueRules.FoldWork(running, carries, 100);
			Assert.AreEqual(4, running.Water, "the tally handed in must not be mutated");
		}

		[Test]
		public void FoldWork_ScalesEverySupportByTheWorksEffectiveness()
		{
			List<KindAmount> carries;
			KingdomCatalogueRules.TryParseTally("water:8,food:8,roof:8,craft:8", out carries, out _);
			KingdomCatalogueRules.SupportTally folded =
				KingdomCatalogueRules.FoldWork(default(KingdomCatalogueRules.SupportTally), carries, 50);
			Assert.AreEqual(4, folded.Water);
			Assert.AreEqual(4, folded.Food);
			Assert.AreEqual(4, folded.Roof);
			Assert.AreEqual(4, folded.Lift);
		}

		[Test]
		public void SumCarries_SumsAWholeSettlement()
		{
			KingdomCatalogueRules.SupportTally tally = KingdomCatalogueRules.SumCarries(
				new string[4] { "water:8", "water:5,food:2", "roof:18", "spirit:3" });
			Assert.AreEqual(13, tally.Water);
			Assert.AreEqual(2, tally.Food);
			Assert.AreEqual(18, tally.Roof);
			Assert.AreEqual(3, tally.Lift);
			Assert.AreEqual(4, tally.Works);
		}

		[Test]
		public void SumCarries_ReadsNullAsNoWorksAtAll()
		{
			KingdomCatalogueRules.SupportTally tally = KingdomCatalogueRules.SumCarries(null);
			Assert.AreEqual(0, tally.Works);
			Assert.AreEqual(0, tally.Water);
		}

		[Test]
		public void SumCarries_KeepsWhateverParsedBeforeABadPair()
		{
			// TryParseTally's own contract, carried into the sum: a caller that logs and carries
			// on is never silently credited with nothing.
			KingdomCatalogueRules.SupportTally tally = KingdomCatalogueRules.SumCarries(new string[1] { "water:8,rubbish" });
			Assert.AreEqual(8, tally.Water);
		}

		// ==================================================================================
		// The level: the catalogue read against the upkeep table for the first time.
		// ==================================================================================

		[TestCase(GrowthStage.Camp, 12)]
		[TestCase(GrowthStage.Steading, 10)]
		[TestCase(GrowthStage.Village, 8)]
		[TestCase(GrowthStage.Town, 6)]
		[TestCase(GrowthStage.City, 5)]
		public void LevelFromWater_ConvertsDramsToSettlersAtTheStagesOwnRate(GrowthStage stage, int expected)
		{
			// Twelve drams a day is twelve settlers at camp rates and five in a city, because
			// KingdomRules.StageUpkeepPercent says a city drinks like a city. This conversion is
			// the cross-check the catalogue and the upkeep table had never been put through.
			Assert.AreEqual(expected, KingdomSubsidenceRules.LevelFromWater(12, stage));
		}

		[Test]
		public void LevelFromWater_CarriesNobodyOnNoWater()
		{
			Assert.AreEqual(0, KingdomSubsidenceRules.LevelFromWater(0, GrowthStage.Camp));
			Assert.AreEqual(0, KingdomSubsidenceRules.LevelFromWater(-9, GrowthStage.City));
		}

		[Test]
		public void LevelFromWater_FailsClosedOntoTheCampRateForAStageThisBuildDoesNotDefine()
		{
			Assert.AreEqual(12, KingdomSubsidenceRules.LevelFromWater(12, (GrowthStage)99));
			Assert.AreEqual(12, KingdomSubsidenceRules.LevelFromWater(12, (GrowthStage)(-3)));
		}

		[Test]
		public void SupportedLevel_IsTheFrozenEquilibriumWithTheWaterConverted()
		{
			KingdomCatalogueRules.SupportTally tally = Tally(Water: 44, Food: 30, Roof: 30);
			// 44 drams at Town rates is 24 settlers, which is then the least of the three.
			Assert.AreEqual(24, KingdomSubsidenceRules.LevelFromWater(44, GrowthStage.Town));
			Assert.AreEqual(KingdomCatalogueRules.Equilibrium(24, 30, 30, 0, 0),
				KingdomSubsidenceRules.SupportedLevel(tally, GrowthStage.Town));
		}

		[Test]
		public void SupportedLevel_ReadsWhatTheSettlementsNotableIsWorthToIt()
		{
			// The seam the brief's notable tastes, leader traits and Addendum 4's Prefers all end
			// at. A Village on works for twelve holds fifteen once its notable is worth three.
			KingdomCatalogueRules.SupportTally tally = Tally(18, 99, 99);
			Assert.AreEqual(12, KingdomSubsidenceRules.SupportedLevel(tally, GrowthStage.Village));
			Assert.AreEqual(15, KingdomSubsidenceRules.SupportedLevel(tally, GrowthStage.Village, 3));
		}

		[Test]
		public void SupportedLevel_KeepsTheShadeUnderTheLiftCap()
		{
			// Half the binding level and no more, whichever half the comfort came from.
			KingdomCatalogueRules.SupportTally tally = Tally(18, 99, 99);
			Assert.AreEqual(18, KingdomSubsidenceRules.SupportedLevel(tally, GrowthStage.Village, 400));
			Assert.AreEqual(18, KingdomSubsidenceRules.SupportedLevel(Tally(18, 99, 99, Lift: 4), GrowthStage.Village, 4));
		}

		[Test]
		public void Slide_ConvergesOnTheLevelTheFounderWasActuallyTold()
		{
			// The shade is carried through every step for this reason: a settlement announced at
			// fifteen must settle to fifteen, not to the twelve its works alone would carry.
			KingdomCatalogueRules.SupportTally supports = Tally(18, 99, 99);
			KingdomSubsidenceRules.Trajectory shaded = KingdomSubsidenceRules.Slide(
				20, GrowthStage.Village, 64, supports, 400, AlreadySliding: false, Shade: 3);
			Assert.AreEqual(15, shaded.Population);
			Assert.AreEqual(5, shaded.Departed);
			KingdomSubsidenceRules.Trajectory bare = KingdomSubsidenceRules.Slide(
				20, GrowthStage.Village, 64, supports, 400, AlreadySliding: false);
			Assert.AreEqual(12, bare.Population, "a settlement with nobody named settles to what its works carry");
			Assert.Greater(shaded.Population, bare.Population);
		}

		[Test]
		public void Slide_LeavesASettlementAloneWhenItsNotableLiftsItIntoItsBand()
		{
			// The arrest, reached by the shade rather than by a building: nothing here departs.
			KingdomCatalogueRules.SupportTally supports = Tally(18, 99, 99);
			Assert.AreEqual(0, KingdomSubsidenceRules.Slide(
				15, GrowthStage.Village, 64, supports, 400, AlreadySliding: false, Shade: 3).Departed);
			Assert.Greater(KingdomSubsidenceRules.Slide(
				15, GrowthStage.Village, 64, supports, 400, AlreadySliding: false).Departed, 0);
		}

		[Test]
		public void SupportedLevel_NeverFallsBelowCampsOwnEquilibrium()
		{
			foreach (GrowthStage stage in new GrowthStage[5]
				{ GrowthStage.Camp, GrowthStage.Steading, GrowthStage.Village, GrowthStage.Town, GrowthStage.City })
			{
				Assert.AreEqual(KingdomCatalogueRules.FloorLevel,
					KingdomSubsidenceRules.SupportedLevel(default(KingdomCatalogueRules.SupportTally), stage));
			}
		}

		[Test]
		public void SupportedLevel_RisesAsTheStageFalls()
		{
			// The same cisterns carry more people once the place stops being a city. This is what
			// makes the slide a convergence rather than a countdown to the floor.
			KingdomCatalogueRules.SupportTally tally = Tally(Water: 44, Food: 99, Roof: 99);
			int city = KingdomSubsidenceRules.SupportedLevel(tally, GrowthStage.City);
			int town = KingdomSubsidenceRules.SupportedLevel(tally, GrowthStage.Town);
			int village = KingdomSubsidenceRules.SupportedLevel(tally, GrowthStage.Village);
			Assert.Less(city, town);
			Assert.Less(town, village);
		}

		[Test]
		public void BindingSupportFor_NamesTheWaterOnlyOnceTheCityRateIsApplied()
		{
			// Twenty-six drams is ample at camp rates and thin in a city. Asking the frozen
			// arithmetic without converting would tell a city founder to sow when they should dig.
			KingdomCatalogueRules.SupportTally tally = Tally(Water: 26, Food: 20, Roof: 20);
			Assert.AreEqual(KingdomCatalogueRules.SupportFood,
				KingdomSubsidenceRules.BindingSupportFor(tally, GrowthStage.Camp));
			Assert.AreEqual(KingdomCatalogueRules.SupportWater,
				KingdomSubsidenceRules.BindingSupportFor(tally, GrowthStage.City));
		}

		[TestCase("water", "water")]
		[TestCase("FOOD", "food")]
		[TestCase(" roof ", "roof")]
		[TestCase("luxury", null)]
		[TestCase("moonlight", null)]
		[TestCase("", null)]
		[TestCase(null, null)]
		public void NormalizedBinding_RepairsAStoredNameToACanonicalOneOrToNothing(string stored, string expected)
		{
			Assert.AreEqual(expected, KingdomSubsidenceRules.NormalizedBinding(stored));
		}

		// ==================================================================================
		// Where a slide starts and where it stops.
		// ==================================================================================

		[TestCase(0, 1)]
		[TestCase(4, 5)]
		[TestCase(12, 14)]
		[TestCase(25, 30)]
		[TestCase(42, 50)]
		public void SlideBeginsAbove_KeepsABandThatNeverVanishes(int level, int expected)
		{
			Assert.AreEqual(expected, KingdomSubsidenceRules.SlideBeginsAbove(level));
		}

		[Test]
		public void IsSubsiding_HoldsAtTheBandsEdgeAndSlidesOneAboveIt()
		{
			Assert.IsFalse(KingdomSubsidenceRules.IsSubsiding(14, 12));
			Assert.IsTrue(KingdomSubsidenceRules.IsSubsiding(15, 12));
		}

		[Test]
		public void HasArrived_IsTheLevelItselfAndNotTheBandsEdge()
		{
			// The two thresholds differ on purpose: that difference IS the hysteresis. A slide
			// begins above the band and then settles all the way to the level.
			Assert.IsTrue(KingdomSubsidenceRules.HasArrived(12, 12));
			Assert.IsFalse(KingdomSubsidenceRules.HasArrived(13, 12));
			Assert.IsFalse(KingdomSubsidenceRules.IsSubsiding(13, 12));
		}

		// ==================================================================================
		// The ladder, both ways.
		// ==================================================================================

		[TestCase(4, 1024, GrowthStage.Camp)]
		[TestCase(5, 16, GrowthStage.Steading)]
		[TestCase(12, 64, GrowthStage.Village)]
		[TestCase(25, 256, GrowthStage.Town)]
		[TestCase(50, 1024, GrowthStage.City)]
		public void StageWithHysteresis_RisesExactlyAsTheShippedRatchetDid(int population, int capacity, GrowthStage expected)
		{
			// Raising is unchanged: hauling can still carry a settlement to City. The pillar
			// promises a hauled city SETTLES BACK, not that it could never be raised.
			Assert.AreEqual(expected, KingdomRules.StageFor(population, capacity));
			Assert.AreEqual(expected, KingdomSubsidenceRules.StageWithHysteresis(GrowthStage.Camp, population, capacity));
		}

		[Test]
		public void StageWithHysteresis_FallsOneRungPerReckoning()
		{
			// Ten people in a City is a Camp by StageFor's own reading, and it still only loses
			// one rung: a city that empties has a story with four chapters, and telling all four
			// at once tells none.
			Assert.AreEqual(GrowthStage.Steading, KingdomRules.StageFor(10, Capacity));
			Assert.AreEqual(GrowthStage.Town, KingdomSubsidenceRules.StageWithHysteresis(GrowthStage.City, 10, Capacity));
		}

		[TestCase(20, GrowthStage.Town)]
		[TestCase(19, GrowthStage.Village)]
		public void StageWithHysteresis_GivesTheBenefitOfTheDoubtBeforeDemoting(int population, GrowthStage expected)
		{
			// A Town's own threshold is 25. It keeps the rung down to twenty and loses it at
			// nineteen, which is the fifth named at StageFallMarginPercent.
			Assert.AreEqual(expected, KingdomSubsidenceRules.StageWithHysteresis(GrowthStage.Town, population, Capacity));
		}

		[TestCase(205, GrowthStage.Town)]
		[TestCase(204, GrowthStage.Village)]
		public void StageWithHysteresis_AsksTheSameOfTheCasksAsOfThePeople(int capacity, GrowthStage expected)
		{
			// Undedicating the stores demotes a settlement exactly as losing its people does, and
			// with the same band, so the two readings StageFor takes cannot disagree about it.
			Assert.AreEqual(expected, KingdomSubsidenceRules.StageWithHysteresis(GrowthStage.Town, 100, capacity));
		}

		[Test]
		public void StageWithHysteresis_NeverFallsBelowCamp()
		{
			Assert.AreEqual(GrowthStage.Camp, KingdomSubsidenceRules.StageWithHysteresis(GrowthStage.Camp, 0, 0));
			Assert.AreEqual(GrowthStage.Camp, KingdomSubsidenceRules.StageWithHysteresis(GrowthStage.Camp, -5, -5));
		}

		[Test]
		public void StageWithHysteresis_TreatsCampsOwnEquilibriumAsACamp()
		{
			// Without this the fall margin holds the smallest rung one settler under its own
			// threshold, and a collapsed city would end its slide as a four-person steading -
			// the one outcome the pillar names in so many words.
			Assert.AreEqual(GrowthStage.Camp,
				KingdomSubsidenceRules.StageWithHysteresis(GrowthStage.Steading, KingdomCatalogueRules.FloorLevel, Capacity));
		}

		[TestCase(GrowthStage.Steading, 5, 16)]
		[TestCase(GrowthStage.Village, 12, 64)]
		[TestCase(GrowthStage.Town, 25, 256)]
		[TestCase(GrowthStage.City, 50, 1024)]
		public void StageWithHysteresis_DoesNotFlapAtTheRungItJustCrossed(GrowthStage stage, int population, int capacity)
		{
			// The whole point of a band: a settlement that has just been promoted may lose a cask
			// without losing the rung again on the very next pass.
			Assert.AreEqual(stage, KingdomSubsidenceRules.StageWithHysteresis(GrowthStage.Camp, population, capacity));
			Assert.AreEqual(stage, KingdomSubsidenceRules.StageWithHysteresis(stage, population, capacity - 1));
		}

		[TestCase(GrowthStage.Village, 12, 64)]
		[TestCase(GrowthStage.Town, 25, 256)]
		[TestCase(GrowthStage.City, 50, 1024)]
		public void StageWithHysteresis_DoesNotFlapWhenOneSettlerLeavesTheRungItJustCrossed(GrowthStage stage, int population, int capacity)
		{
			Assert.AreEqual(stage, KingdomSubsidenceRules.StageWithHysteresis(stage, population - 1, capacity));
		}

		[Test]
		public void TheCampFloorOutranksTheBandAtTheSmallestRung()
		{
			// Steading's own threshold is five and the band would forgive four - but four is
			// Camp's own equilibrium, and the floor wins. This is the one rung where the two
			// rules meet, and it is why a collapsed city ends as a camp rather than as a
			// four-person steading.
			Assert.AreEqual(KingdomCatalogueRules.FloorLevel + 1, 5);
			Assert.AreEqual(GrowthStage.Steading, KingdomSubsidenceRules.StageWithHysteresis(GrowthStage.Steading, 5, 16));
			Assert.AreEqual(GrowthStage.Camp, KingdomSubsidenceRules.StageWithHysteresis(GrowthStage.Steading, 4, 16));
		}

		[Test]
		public void SettledStage_FallsEveryRungThePopulationGaveAway()
		{
			// StageWithHysteresis is the pace of a slide being lived through; this is the
			// settling-up after a whole trajectory resolved in one pass.
			Assert.AreEqual(GrowthStage.Camp,
				KingdomSubsidenceRules.SettledStage(GrowthStage.City, KingdomCatalogueRules.FloorLevel, Capacity));
		}

		[Test]
		public void SettledStage_StillRisesWhenTheFiguresRose()
		{
			Assert.AreEqual(GrowthStage.Town, KingdomSubsidenceRules.SettledStage(GrowthStage.Village, 25, 256));
		}

		// ==================================================================================
		// The slide.
		// ==================================================================================

		[Test]
		public void Slide_LeavesASettlementInsideItsBandAlone()
		{
			KingdomSubsidenceRules.Trajectory trajectory = KingdomSubsidenceRules.Slide(
				14, GrowthStage.Village, Capacity, Tally(18, 99, 99), 400, AlreadySliding: false);
			Assert.AreEqual(0, trajectory.Departed);
			Assert.AreEqual(0, trajectory.Steps);
			Assert.AreEqual(14, trajectory.Population);
			Assert.AreEqual(GrowthStage.Village, trajectory.Stage);
		}

		[Test]
		public void Slide_WaitsForAWholeStepOfWorldTime()
		{
			KingdomSubsidenceRules.Trajectory trajectory = KingdomSubsidenceRules.Slide(
				50, GrowthStage.City, Capacity, default(KingdomCatalogueRules.SupportTally),
				KingdomSubsidenceRules.StepDays - 1, AlreadySliding: false);
			Assert.AreEqual(0, trajectory.Steps);
			Assert.AreEqual(50, trajectory.Population);
		}

		[Test]
		public void Slide_TakesOneStepEveryStepDays()
		{
			KingdomSubsidenceRules.Trajectory trajectory = KingdomSubsidenceRules.Slide(
				50, GrowthStage.City, Capacity, default(KingdomCatalogueRules.SupportTally),
				KingdomSubsidenceRules.StepDays * 2, AlreadySliding: false);
			Assert.AreEqual(2, trajectory.Steps);
			Assert.AreEqual(2 * KingdomSubsidenceRules.SettlersPerStep(GrowthStage.City), trajectory.Departed);
		}

		[TestCase(GrowthStage.Camp, 1)]
		[TestCase(GrowthStage.Steading, 2)]
		[TestCase(GrowthStage.Village, 3)]
		[TestCase(GrowthStage.Town, 4)]
		[TestCase(GrowthStage.City, 5)]
		public void SettlersPerStep_ShedsFasterTheGranderThePlaceIs(GrowthStage stage, int expected)
		{
			Assert.AreEqual(expected, KingdomSubsidenceRules.SettlersPerStep(stage));
		}

		[Test]
		public void SettlersPerStep_ClampsAStageThisBuildDoesNotDefine()
		{
			Assert.AreEqual(1, KingdomSubsidenceRules.SettlersPerStep((GrowthStage)(-4)));
			Assert.AreEqual(5, KingdomSubsidenceRules.SettlersPerStep((GrowthStage)99));
		}

		[Test]
		public void Slide_ConvergesOnTheLevelAndStopsExactlyThere()
		{
			// A Village standing at twenty on works for twelve: it settles to twelve, not to
			// fourteen (the band's edge) and not to the floor.
			KingdomSubsidenceRules.Trajectory trajectory = KingdomSubsidenceRules.Slide(
				20, GrowthStage.Village, 64, Tally(18, 99, 99), 400, AlreadySliding: false);
			Assert.AreEqual(12, KingdomSubsidenceRules.SupportedLevel(Tally(18, 99, 99), GrowthStage.Village));
			Assert.AreEqual(12, trajectory.Population);
			Assert.AreEqual(8, trajectory.Departed);
			Assert.IsTrue(trajectory.Arrived);
		}

		[TestCase(1)]
		[TestCase(8)]
		[TestCase(52)]
		[TestCase(400)]
		[TestCase(40000)]
		public void Slide_NeverTakesASettlementBelowItsLevel(int days)
		{
			KingdomCatalogueRules.SupportTally supports = Tally(18, 99, 99);
			KingdomSubsidenceRules.Trajectory trajectory = KingdomSubsidenceRules.Slide(
				40, GrowthStage.Village, 64, supports, days, AlreadySliding: false);
			Assert.GreaterOrEqual(trajectory.Population,
				KingdomSubsidenceRules.SupportedLevel(supports, trajectory.Stage));
		}

		[Test]
		public void Slide_ContinuesOnceUnderWayEvenInsideTheBand()
		{
			// The hysteresis in one test. Thirteen people on works for twelve is inside the band,
			// so nothing STARTS; a slide that has already been announced settles the last one.
			KingdomCatalogueRules.SupportTally supports = Tally(18, 99, 99);
			Assert.AreEqual(0, KingdomSubsidenceRules.Slide(13, GrowthStage.Village, 64, supports, 400, AlreadySliding: false).Departed);
			Assert.AreEqual(1, KingdomSubsidenceRules.Slide(13, GrowthStage.Village, 64, supports, 400, AlreadySliding: true).Departed);
		}

		[Test]
		public void Slide_ArrestsTheMomentTheCauseIsRemoved()
		{
			// The level is re-derived every reckoning and never remembered, so raising the works
			// stops the slide wherever it had got to - even for a settlement mid-slide, which is
			// what "arrestable if caught midway" has to mean.
			KingdomCatalogueRules.SupportTally raised = Tally(60, 99, 99);
			KingdomSubsidenceRules.Trajectory trajectory = KingdomSubsidenceRules.Slide(
				30, GrowthStage.Village, 64, raised, 400, AlreadySliding: true);
			Assert.AreEqual(0, trajectory.Departed);
			Assert.AreEqual(30, trajectory.Population);
			Assert.IsTrue(trajectory.Arrived);
		}

		[Test]
		public void Slide_StopsAtCampsOwnEquilibriumAndNoLower()
		{
			// Nobody subsides out of existence. A city with nothing standing ends as a camp of
			// four, however long the absence.
			KingdomSubsidenceRules.Trajectory trajectory = KingdomSubsidenceRules.Slide(
				50, GrowthStage.City, Capacity, default(KingdomCatalogueRules.SupportTally), 40000, AlreadySliding: false);
			Assert.AreEqual(KingdomCatalogueRules.FloorLevel, trajectory.Population);
			Assert.AreEqual(GrowthStage.Camp, trajectory.Stage);
			Assert.Greater(trajectory.Population, KingdomRules.LoyalCoreSettlers);
		}

		[Test]
		public void Slide_TakesTheWholeCityDownInFiftyTwoDaysAndNotOneStepMore()
		{
			// The headline trajectory, pinned exactly so a tuning change to StepDays or
			// SettlersPerStep cannot move it quietly.
			KingdomSubsidenceRules.Trajectory trajectory = KingdomSubsidenceRules.Slide(
				50, GrowthStage.City, Capacity, default(KingdomCatalogueRules.SupportTally), 400, AlreadySliding: false);
			Assert.AreEqual(13, trajectory.Steps);
			Assert.AreEqual(52, trajectory.Steps * KingdomSubsidenceRules.StepDays);
			Assert.AreEqual(46, trajectory.Departed);
			Assert.AreEqual(4, trajectory.Breakpoints.Count);
		}

		[Test]
		public void Slide_RecordsOneDatedBreakpointForEveryRungLost()
		{
			KingdomSubsidenceRules.Trajectory trajectory = KingdomSubsidenceRules.Slide(
				50, GrowthStage.City, Capacity, default(KingdomCatalogueRules.SupportTally), 400, AlreadySliding: false);
			GrowthStage[] from = new GrowthStage[4]
				{ GrowthStage.City, GrowthStage.Town, GrowthStage.Village, GrowthStage.Steading };
			GrowthStage[] to = new GrowthStage[4]
				{ GrowthStage.Town, GrowthStage.Village, GrowthStage.Steading, GrowthStage.Camp };
			int[] days = new int[4] { 12, 28, 44, 52 };
			for (int i = 0; i < 4; i++)
			{
				Assert.AreEqual(from[i], trajectory.Breakpoints[i].From);
				Assert.AreEqual(to[i], trajectory.Breakpoints[i].To);
				Assert.AreEqual(days[i], trajectory.Breakpoints[i].Day);
			}
		}

		[Test]
		public void Slide_DatesEveryBreakpointOnTheStepLatticeAndInOrder()
		{
			KingdomSubsidenceRules.Trajectory trajectory = KingdomSubsidenceRules.Slide(
				50, GrowthStage.City, Capacity, default(KingdomCatalogueRules.SupportTally), 400, AlreadySliding: false);
			int previous = 0;
			for (int i = 0; i < trajectory.Breakpoints.Count; i++)
			{
				int day = trajectory.Breakpoints[i].Day;
				Assert.AreEqual(0, day % KingdomSubsidenceRules.StepDays, "a breakpoint must sit on the step lattice");
				Assert.Greater(day, previous, "breakpoints must be dated in order");
				previous = day;
				Assert.Greater(trajectory.Breakpoints[i].Population, 0);
			}
		}

		[Test]
		public void Slide_ChargesOnlyTheStepsItActuallyTook()
		{
			// A settlement that arrives early does not bank the rest of the absence: the caller
			// advances its checkpoint by these steps and no more.
			KingdomSubsidenceRules.Trajectory trajectory = KingdomSubsidenceRules.Slide(
				20, GrowthStage.Village, 64, Tally(18, 99, 99), 4000, AlreadySliding: false);
			Assert.Less(trajectory.Steps * KingdomSubsidenceRules.StepDays, 4000);
			Assert.LessOrEqual(trajectory.Steps, KingdomSubsidenceRules.MaxSteps);
		}

		[Test]
		public void Slide_AHundredDaysAndAThousandEndAtTheSameHonestLevel()
		{
			KingdomCatalogueRules.SupportTally supports = Tally(18, 99, 99);
			KingdomSubsidenceRules.Trajectory hundred = KingdomSubsidenceRules.Slide(
				40, GrowthStage.Village, 64, supports, 100, AlreadySliding: false);
			KingdomSubsidenceRules.Trajectory thousand = KingdomSubsidenceRules.Slide(
				40, GrowthStage.Village, 64, supports, 1000, AlreadySliding: false);
			Assert.AreEqual(hundred.Population, thousand.Population);
			Assert.AreEqual(hundred.Stage, thousand.Stage);
			Assert.AreEqual(hundred.Steps, thousand.Steps, "arriving early must not keep charging");
		}

		[Test]
		public void Slide_IsTheSameTrajectoryEveryTimeItIsAsked()
		{
			KingdomCatalogueRules.SupportTally supports = Tally(9, 30, 30);
			KingdomSubsidenceRules.Trajectory first = KingdomSubsidenceRules.Slide(
				44, GrowthStage.City, Capacity, supports, 137, AlreadySliding: false);
			KingdomSubsidenceRules.Trajectory second = KingdomSubsidenceRules.Slide(
				44, GrowthStage.City, Capacity, supports, 137, AlreadySliding: false);
			Assert.AreEqual(first.Population, second.Population);
			Assert.AreEqual(first.Stage, second.Stage);
			Assert.AreEqual(first.Departed, second.Departed);
			Assert.AreEqual(first.Steps, second.Steps);
			Assert.AreEqual(first.Breakpoints.Count, second.Breakpoints.Count);
			for (int i = 0; i < first.Breakpoints.Count; i++)
			{
				Assert.AreEqual(first.Breakpoints[i].Day, second.Breakpoints[i].Day);
				Assert.AreEqual(first.Breakpoints[i].To, second.Breakpoints[i].To);
			}
		}

		[Test]
		public void Slide_NeverReturnsANullBreakpointList()
		{
			Assert.IsNotNull(KingdomSubsidenceRules.Slide(
				4, GrowthStage.Camp, 0, default(KingdomCatalogueRules.SupportTally), 0, AlreadySliding: false).Breakpoints);
		}

		// ==================================================================================
		// Ruin: damage, never deletion (the protection law).
		// ==================================================================================

		[TestCase(0, 12)]
		[TestCase(99, 22)]
		public void RuinIncrement_IsTheComplementOfWhatStandsHalved(int roll, int expected)
		{
			Assert.AreEqual(expected, KingdomSubsidenceRules.RuinIncrement(roll));
		}

		[Test]
		public void RuinIncrement_FallsHarderAsTheRollRises()
		{
			Assert.Less(KingdomSubsidenceRules.RuinIncrement(0), KingdomSubsidenceRules.RuinIncrement(99));
		}

		[TestCase(-50)]
		[TestCase(0)]
		[TestCase(50)]
		[TestCase(99)]
		[TestCase(5000)]
		public void RuinIncrement_IsAlwaysARealButSurvivableAmountOfDamage(int roll)
		{
			int increment = KingdomSubsidenceRules.RuinIncrement(roll);
			Assert.GreaterOrEqual(increment, 1, "a ruin that adds nothing reads like a bug");
			Assert.Less(increment, KingdomMaterialRules.MaxWearPercent, "one rung may never ruin a work outright");
		}

		[TestCase(0)]
		[TestCase(30)]
		[TestCase(59)]
		[TestCase(KingdomMaterialRules.MaxWearPercent)]
		public void ARuinedWorkStillStandsAndIsStillMendable(int before)
		{
			// The protection law: kingdom systems damage what they built and never delete it. Wear
			// is capped, a capped work still runs, and every point of it comes back on a mending.
			int after = KingdomMaterialRules.AddWear(before, KingdomSubsidenceRules.RuinIncrement(99));
			Assert.LessOrEqual(after, KingdomMaterialRules.MaxWearPercent);
			Assert.Greater(KingdomMaterialRules.ConditionPercent(after), 0, "a damaged work stands");
		}

		[TestCase(GrowthStage.Camp, 10)]
		[TestCase(GrowthStage.Steading, 20)]
		[TestCase(GrowthStage.Village, 30)]
		[TestCase(GrowthStage.Town, 40)]
		[TestCase(GrowthStage.City, 50)]
		public void RuinChanceFor_ReachesFurtherTheGranderTheRungThatWent(GrowthStage from, int expected)
		{
			Assert.AreEqual(expected, KingdomSubsidenceRules.RuinChanceFor(from));
		}

		[Test]
		public void RuinChanceFor_RisesWithEveryRungAndNeverPastTheWidest()
		{
			// The reach rule (Addendum 10(c)): each lost rung reaches the works that rung's scale
			// supported. Monotone with no plateau, so no two rungs of the ladder are the same
			// event, and the widest of them is the constant the file names.
			for (int index = 0; index < (int)GrowthStage.City; index++)
			{
				Assert.Less(KingdomSubsidenceRules.RuinChanceFor((GrowthStage)index),
					KingdomSubsidenceRules.RuinChanceFor((GrowthStage)(index + 1)),
					"a grander rung did not reach further than the one below it");
			}
			Assert.AreEqual(KingdomSubsidenceRules.RuinChancePercent,
				KingdomSubsidenceRules.RuinChanceFor(GrowthStage.City));
			Assert.Greater(KingdomSubsidenceRules.RuinChanceFor(GrowthStage.Camp), 0,
				"a rung that reaches nothing is not a rung");
		}

		[TestCase(-4)]
		[TestCase(99)]
		public void RuinChanceFor_ClampsAStageOffTheLadderRatherThanFaulting(int index)
		{
			int chance = KingdomSubsidenceRules.RuinChanceFor((GrowthStage)index);
			Assert.GreaterOrEqual(chance, KingdomSubsidenceRules.RuinChanceFor(GrowthStage.Camp));
			Assert.LessOrEqual(chance, KingdomSubsidenceRules.RuinChanceFor(GrowthStage.City));
		}

		[Test]
		public void ARungReachesFurtherThanASingleRaidEverDid()
		{
			// What the ruling overturned. The reach used to be a flat allowance of two works a
			// rung -- the same figure a raid that got past the wall is allowed -- so a City
			// falling all the way to Camp left eight works scuffed however many dozen were
			// standing. A collapse is not a raid, and now it does not read like four of them.
			int standing = 40;
			int reached = 0;
			for (int i = 0; i < standing; i++)
			{
				if (KingdomSubsidenceRules.RollRuin("taf:settlement:ashmarch", "work-" + i, 4800uL, GrowthStage.City))
				{
					reached++;
				}
			}
			Assert.Greater(reached, KingdomWearRules.MaxWorksDamagedPerRaid,
				"one lost rung of a city still reached no more works than a raid");
		}

		[Test]
		public void RollRuin_AnswersTheSameQuestionTheSameWayForever()
		{
			for (int i = 0; i < 8; i++)
			{
				Assert.AreEqual(
					KingdomSubsidenceRules.RollRuin("taf:settlement:ashmarch", "work-" + i, 4800uL, GrowthStage.Town),
					KingdomSubsidenceRules.RollRuin("taf:settlement:ashmarch", "work-" + i, 4800uL, GrowthStage.Town),
					"a reload must not re-roll a collapse the chronicle already described");
			}
		}

		[Test]
		public void RollRuin_AsksEachWorkIndependently()
		{
			bool sawTrue = false;
			bool sawFalse = false;
			for (int i = 0; i < 60; i++)
			{
				if (KingdomSubsidenceRules.RollRuin("taf:settlement:ashmarch", "work-" + i, 4800uL, GrowthStage.Town))
				{
					sawTrue = true;
				}
				else
				{
					sawFalse = true;
				}
			}
			Assert.IsTrue(sawTrue && sawFalse, "two works at one breakpoint must not share one answer");
		}

		[Test]
		public void RollRuin_AsksEachBreakpointFresh()
		{
			bool differed = false;
			for (int i = 0; i < 60; i++)
			{
				if (KingdomSubsidenceRules.RollRuin("taf:settlement:ashmarch", "work-" + i, 4800uL, GrowthStage.Town)
					!= KingdomSubsidenceRules.RollRuin("taf:settlement:ashmarch", "work-" + i, 9600uL, GrowthStage.Town))
				{
					differed = true;
				}
			}
			Assert.IsTrue(differed, "a second rung must be an independent question");
		}

		[Test]
		public void RollRuin_FailsClosedOnASettlementIdTheKernelWillNotTake()
		{
			Assert.IsFalse(KingdomSubsidenceRules.RollRuin(null, "work-1", 4800uL, GrowthStage.City));
			Assert.IsFalse(KingdomSubsidenceRules.RollRuin("", "work-1", 4800uL, GrowthStage.City));
			Assert.AreEqual(0, KingdomSubsidenceRules.RolledRuinIncrement("", "work-1", 4800uL));
		}

		[Test]
		public void RolledRuinIncrement_IsStableAndAlwaysWithinTheAuthoredBand()
		{
			for (int i = 0; i < 20; i++)
			{
				int first = KingdomSubsidenceRules.RolledRuinIncrement("taf:settlement:ashmarch", "work-" + i, 4800uL);
				int second = KingdomSubsidenceRules.RolledRuinIncrement("taf:settlement:ashmarch", "work-" + i, 4800uL);
				Assert.AreEqual(first, second);
				Assert.GreaterOrEqual(first, KingdomSubsidenceRules.RuinIncrement(0));
				Assert.LessOrEqual(first, KingdomSubsidenceRules.RuinIncrement(99));
			}
		}

		[Test]
		public void WorkStream_FoldsToTheFrozenSemanticIdGrammarAndNamesItsOwnLane()
		{
			string stream = KingdomSubsidenceRules.WorkStream("Work Id/42");
			Assert.IsTrue(stream.StartsWith("taf:subsidence:"), "a subsidence draw must not share the wear file's lane");
			Assert.IsTrue(stream.EndsWith(":v1"));
			StringAssert.Contains("work-id-42", stream);
			Assert.IsTrue(KingdomSubsidenceRules.WorkStream(null).EndsWith("unidentified:v1"));
		}

		// ==================================================================================
		// What it says (STANDARDS 7b).
		// ==================================================================================

		[Test]
		public void BeganNote_NamesThePlaceTheCountAndWhatIsHoldingItThere()
		{
			string line = KingdomSubsidenceRules.BeganNote("Ashmarch", KingdomCatalogueRules.SupportWater, 12, 19);
			StringAssert.Contains("Ashmarch", line);
			StringAssert.Contains("19", line);
			StringAssert.Contains("12", line);
			StringAssert.Contains("water", line);
		}

		[Test]
		public void BeganNote_ReadsDifferentlyForEachBindingGood()
		{
			string water = KingdomSubsidenceRules.BeganNote("Ashmarch", KingdomCatalogueRules.SupportWater, 12, 19);
			string food = KingdomSubsidenceRules.BeganNote("Ashmarch", KingdomCatalogueRules.SupportFood, 12, 19);
			string roof = KingdomSubsidenceRules.BeganNote("Ashmarch", KingdomCatalogueRules.SupportRoof, 12, 19);
			Assert.AreNotEqual(water, food);
			Assert.AreNotEqual(food, roof);
			Assert.AreNotEqual(water, roof);
		}

		[Test]
		public void ArrestedNote_SaysTheOppositeThingFromTheBeginning()
		{
			string began = KingdomSubsidenceRules.BeganNote("Ashmarch", KingdomCatalogueRules.SupportWater, 12, 19);
			string arrested = KingdomSubsidenceRules.ArrestedNote("Ashmarch", 12, 12);
			Assert.AreNotEqual(began, arrested);
			StringAssert.Contains("Ashmarch", arrested);
			StringAssert.Contains("12", arrested);
		}

		[Test]
		public void ArrestedNote_TellsASettlementThatArrivedFromOneThatWasCaught()
		{
			// Both are arrests and both unsay the 7b line, but "it has stopped" and "it has
			// settled" are different pieces of news and the founder is owed the difference.
			Assert.AreNotEqual(
				KingdomSubsidenceRules.ArrestedNote("Ashmarch", 12, 9),
				KingdomSubsidenceRules.ArrestedNote("Ashmarch", 12, 12));
		}

		[Test]
		public void ArrestedChronicle_NamesTheLevelItStoppedAt()
		{
			StringAssert.Contains("12", KingdomSubsidenceRules.ArrestedChronicle("Ashmarch", 12));
		}

		[Test]
		public void BeganChronicle_NamesWhereItIsHeadedAndWhy()
		{
			string line = KingdomSubsidenceRules.BeganChronicle("Ashmarch", KingdomCatalogueRules.SupportFood, 12);
			StringAssert.Contains("Ashmarch", line);
			StringAssert.Contains("12", line);
			StringAssert.Contains("harvest", line);
		}

		[TestCase(0, "today")]
		[TestCase(1, "a day before you saw it")]
		[TestCase(22, "22 days before you saw it")]
		public void BreakpointChronicle_DatesItselfAgainstTheDayItIsToldOn(int daysAgo, string expected)
		{
			// The trajectory is a hundred small departures; what the chronicle keeps is the times
			// the place stopped being one thing, each dated back to when it actually happened.
			string line = KingdomSubsidenceRules.BreakpointChronicle("Ashmarch", GrowthStage.Town, GrowthStage.Village, daysAgo);
			StringAssert.Contains(expected, line);
			StringAssert.Contains("town", line);
			StringAssert.Contains("village", line);
		}

		[Test]
		public void DepartureCause_ReadsDifferentlyForEachBindingGood()
		{
			string water = KingdomSubsidenceRules.DepartureCause(KingdomCatalogueRules.SupportWater);
			string food = KingdomSubsidenceRules.DepartureCause(KingdomCatalogueRules.SupportFood);
			string roof = KingdomSubsidenceRules.DepartureCause(KingdomCatalogueRules.SupportRoof);
			Assert.AreNotEqual(water, food);
			Assert.AreNotEqual(food, roof);
			Assert.AreNotEqual(water, roof);
			foreach (string clause in new string[3] { water, food, roof })
			{
				Assert.IsFalse(clause.EndsWith("."), "a cause clause is spliced into a sentence, not ended");
			}
		}

		[Test]
		public void DepartureCause_BlamesNoGoodItCannotName()
		{
			// A settlement no pass has measured, or a saved name from a build with a different
			// vocabulary, must not have the water blamed for it by default.
			string unknown = KingdomSubsidenceRules.DepartureCause("moonlight");
			Assert.AreEqual(unknown, KingdomSubsidenceRules.DepartureCause(null));
			Assert.AreNotEqual(unknown, KingdomSubsidenceRules.DepartureCause(KingdomCatalogueRules.SupportWater));
			Assert.IsTrue(unknown.Length > 0, "a departure still says why, even when it cannot say which good");
		}

		[Test]
		public void BeganNote_BlamesNoGoodItCannotName()
		{
			Assert.AreNotEqual(
				KingdomSubsidenceRules.BeganChronicle("Ashmarch", null, 12),
				KingdomSubsidenceRules.BeganChronicle("Ashmarch", KingdomCatalogueRules.SupportWater, 12));
			StringAssert.Contains("12", KingdomSubsidenceRules.BeganChronicle("Ashmarch", null, 12));
		}

		// ==================================================================================
		// The seat. A per-city field on one side only is silently lost or loudly refused.
		// ==================================================================================

		[Test]
		public void EverySubsidenceFieldIsOnBothTheSeatAndTheRecord()
		{
			// SettlementSeatTests already reflects over every field; this names these four so a
			// rename cannot quietly drop one and still pass by finding nothing.
			string[] fields = new string[4]
				{ "LastSubsidenceTick", "SubsidenceAnnounced", "SupportedLevel", "SubsidenceBinding" };
			List<string> carried = new List<string>();
			foreach (FieldInfo field in KingdomSettlement.CarriedFields())
			{
				carried.Add(field.Name);
			}
			foreach (string name in fields)
			{
				Assert.IsTrue(carried.Contains(name), name + " is per-city state and must travel with a city");
			}
		}

		[Test]
		public void NormalizeRepairsASubsidenceReadingNothingEverWrote()
		{
			// Subsidence mints nothing, so a stamp or a level below zero is a corrupt reading and
			// not a settlement in debt. Both fail closed to "nothing measured yet".
			KingdomSettlement settlement = new KingdomSettlement();
			settlement.LastSubsidenceTick = -5000L;
			settlement.SupportedLevel = -9;
			settlement.Normalize();
			Assert.AreEqual(0L, settlement.LastSubsidenceTick);
			Assert.AreEqual(0, settlement.SupportedLevel);
		}

		[Test]
		public void NormalizeLeavesTheStoredBindingExactlyAsItFoundIt()
		{
			// The seat swap's contract is a byte-for-byte round trip, so the binding is repaired
			// where it is READ rather than where it is stored - see NormalizedBinding.
			KingdomSettlement settlement = new KingdomSettlement();
			settlement.SubsidenceBinding = "moonlight";
			settlement.Normalize();
			Assert.AreEqual("moonlight", settlement.SubsidenceBinding);
			Assert.IsNull(KingdomSubsidenceRules.NormalizedBinding(settlement.SubsidenceBinding));
		}

		[Test]
		public void NormalizeKeepsAReadingAPassActuallyTook()
		{
			KingdomSettlement settlement = new KingdomSettlement();
			settlement.LastSubsidenceTick = 4800L;
			settlement.SupportedLevel = 12;
			settlement.SubsidenceBinding = KingdomCatalogueRules.SupportRoof;
			settlement.SubsidenceAnnounced = true;
			settlement.Normalize();
			Assert.AreEqual(4800L, settlement.LastSubsidenceTick);
			Assert.AreEqual(12, settlement.SupportedLevel);
			Assert.AreEqual(KingdomCatalogueRules.SupportRoof, settlement.SubsidenceBinding);
			Assert.IsTrue(settlement.SubsidenceAnnounced);
		}

		// ==================================================================================
		// The interlock: the catalogue's denomination against the upkeep table it never met.
		// ==================================================================================

		[TestCase(GrowthStage.Steading, 5)]
		[TestCase(GrowthStage.Village, 12)]
		[TestCase(GrowthStage.Town, 25)]
		[TestCase(GrowthStage.City, 50)]
		public void EachRungIsHoldableOnWaterTheCatalogueCanActuallyDeclare(GrowthStage stage, int threshold)
		{
			// One point of water is one dram a day sustained, and UpkeepDrams(pop, stage) is the
			// bill. The two agree exactly: the water a rung's own population drinks is the water
			// that carries it. This is the cross-check the audit says had never been made, and it
			// is what makes "the works pay for the people" a true sentence rather than a hope.
			int bill = KingdomRules.UpkeepDrams(threshold, stage);
			Assert.GreaterOrEqual(KingdomSubsidenceRules.LevelFromWater(bill, stage), threshold - 1,
				"the drams a rung drinks must carry very nearly the people who drink them");
			Assert.GreaterOrEqual(KingdomSubsidenceRules.LevelFromWater(bill + 1, stage), threshold,
				"one dram of slack must be enough to close the rounding");
		}

		[Test]
		public void ACampCarriesItselfWithNothingStandingAtAll()
		{
			// The doctrine's floor, stated as a test: whatever else subsides, the smallest
			// settlement holds, and it holds without a single work raised.
			KingdomSubsidenceRules.Trajectory trajectory = KingdomSubsidenceRules.Slide(
				KingdomCatalogueRules.FloorLevel, GrowthStage.Camp, 0,
				default(KingdomCatalogueRules.SupportTally), 40000, AlreadySliding: false);
			Assert.AreEqual(KingdomCatalogueRules.FloorLevel, trajectory.Population);
			Assert.AreEqual(GrowthStage.Camp, trajectory.Stage);
			Assert.AreEqual(0, trajectory.Departed);
			Assert.IsTrue(trajectory.Arrived);
		}

		// ==================================================================================
		// The chronicle budget: a collapse is told in rungs, not in fifty departures.
		// ==================================================================================

		[Test]
		public void TellsDeparture_ShortSlidesNameEverybody()
		{
			// A handful of people leaving IS the story at that size, so nothing is sampled away.
			for (int departed = 1; departed <= KingdomSubsidenceRules.NamedDeparturesPerSlide; departed++)
			{
				for (int i = 0; i < departed; i++)
				{
					Assert.IsTrue(KingdomSubsidenceRules.TellsDeparture(i, departed),
						"a short slide stopped naming everybody");
				}
			}
		}

		[Test]
		public void TellsDeparture_KeepsTheFirstAndTheLastAlways()
		{
			// The first is when it started going and the last is who turned the lights off. A
			// sample that dropped either would be a worse record than a shorter one.
			for (int departed = KingdomSubsidenceRules.NamedDeparturesPerSlide + 1; departed <= 60; departed++)
			{
				Assert.IsTrue(KingdomSubsidenceRules.TellsDeparture(0, departed), "the first departure went untold");
				Assert.IsTrue(KingdomSubsidenceRules.TellsDeparture(departed - 1, departed), "the last departure went untold");
			}
		}

		[Test]
		public void TellsDeparture_NamesExactlyTheBudgetHoweverLongTheSlide()
		{
			// The bound. A City emptying to Camp names three and no more, whatever the length.
			for (int departed = 1; departed <= 60; departed++)
			{
				int named = 0;
				for (int i = 0; i < departed; i++)
				{
					if (KingdomSubsidenceRules.TellsDeparture(i, departed))
					{
						named++;
					}
				}
				Assert.AreEqual(KingdomSubsidenceRules.NamedDepartures(departed), named,
					"the sample size drifted from what NamedDepartures promises");
				Assert.LessOrEqual(named, KingdomSubsidenceRules.NamedDeparturesPerSlide);
			}
		}

		[Test]
		public void TellsDeparture_RefusesIndicesOutsideTheSlide()
		{
			Assert.IsFalse(KingdomSubsidenceRules.TellsDeparture(-1, 10));
			Assert.IsFalse(KingdomSubsidenceRules.TellsDeparture(10, 10));
			Assert.IsFalse(KingdomSubsidenceRules.TellsDeparture(0, 0));
		}

		[Test]
		public void SlideDepartureSummary_CarriesEverybodyTheSampleDidNotName()
		{
			// Nobody vanishes from the count. Named plus summarised is exactly what went, which
			// is what lets the ledger's departure tally and the register agree.
			string cause = KingdomSubsidenceRules.DepartureCause(KingdomCatalogueRules.SupportWater);
			string summary = KingdomSubsidenceRules.SlideDepartureSummary("Tamsketh", 46, 3, cause);
			StringAssert.Contains("43 more", summary);
			StringAssert.Contains("Tamsketh", summary);
			StringAssert.Contains(cause, summary);
			StringAssert.Contains("one more", KingdomSubsidenceRules.SlideDepartureSummary("Tamsketh", 4, 3, cause));
		}

		[Test]
		public void SlideDepartureSummary_SaysNothingWhenTheSampleNamedThemAll()
		{
			string cause = KingdomSubsidenceRules.DepartureCause(KingdomCatalogueRules.SupportFood);
			Assert.IsNull(KingdomSubsidenceRules.SlideDepartureSummary("Tamsketh", 3, 3, cause));
			Assert.IsNull(KingdomSubsidenceRules.SlideDepartureSummary("Tamsketh", 0, 0, cause));
			Assert.IsNull(KingdomSubsidenceRules.SlideDepartureSummary("Tamsketh", 2, 5, cause));
		}

		[Test]
		public void SlideDepartureSummary_CountsWhatActuallyWentWhenASlideIsCutShort()
		{
			// A slide loses fewer than the trajectory called for when its people are standing in
			// another claimed zone, and may name fewer than the sample planned. The summary takes
			// both real numbers, so the two always add back up to the departures recorded.
			string cause = KingdomSubsidenceRules.DepartureCause(KingdomCatalogueRules.SupportRoof);
			StringAssert.Contains("3 more", KingdomSubsidenceRules.SlideDepartureSummary("Tamsketh", 5, 2, cause));
		}

		[Test]
		public void ChronicleEntriesFor_AFullCityToCampCollapseSpendsAModestShare()
		{
			// The row. Fifty people falling to Camp's own floor of four, through every rung there
			// is, with every rung ruining its full allowance of works: fifty-eight entries before
			// the coarsening, better than a quarter of the two-hundred-entry register for one
			// event. Now it is the rungs, their ruins, and four lines about the people.
			int rungs = (int)GrowthStage.City;
			int spent = KingdomSubsidenceRules.ChronicleEntriesFor(46, rungs);
			Assert.LessOrEqual(spent, KingdomSubsidenceRules.ChronicleBudgetPerSlide,
				"a full collapse went over the budget this file promises");
			Assert.Less(spent, 58 / 2, "the coarsening did not even halve the old spend");
			Assert.Greater(spent, rungs, "the rungs themselves stopped being told");
		}

		[Test]
		public void ChronicleEntriesFor_GrowsWithTheRungsAndNotWithTheDepartures()
		{
			// What the coarsening actually bought: length stops mattering. Once past the sample,
			// twice as many people leaving costs no more entries at all -- only a longer fall
			// does, because a rung is a real change in what the place is.
			int shortSlide = KingdomSubsidenceRules.ChronicleEntriesFor(10, 1);
			int longSlide = KingdomSubsidenceRules.ChronicleEntriesFor(46, 1);
			Assert.AreEqual(shortSlide, longSlide, "the register still paid by the settler");
			Assert.Greater(KingdomSubsidenceRules.ChronicleEntriesFor(46, 4), longSlide,
				"a four-rung fall cost the same as a one-rung fall");
		}

		[Test]
		public void ChronicleEntriesFor_ASlideThatTookNobodyWritesNothingButItsRungs()
		{
			Assert.AreEqual(0, KingdomSubsidenceRules.ChronicleEntriesFor(0, 0));
			Assert.AreEqual(0, KingdomSubsidenceRules.ChronicleEntriesFor(-4, -1));
		}

		[Test]
		public void ChronicleBudgetPerSlide_IsAModestShareOfTheRegister()
		{
			// KingdomChronicle.MaxEntries is 200 and is the chronicle's own constant; this file
			// folds to fit it rather than reaching into it. One collapse may not spend more than
			// a tenth of the record a settlement keeps of itself.
			Assert.LessOrEqual(KingdomSubsidenceRules.ChronicleBudgetPerSlide, 200 / 10);
			Assert.Greater(KingdomSubsidenceRules.ChronicleBudgetPerSlide, 0);
		}

		[Test]
		public void EveryRungOfARealCollapseIsStillToldFirstAndLast()
		{
			// The rungs are the sample, so they are never thinned: a City sliding to Camp
			// chronicles City-to-Town and Steading-to-Camp and everything between. There are at
			// most four of them, which is why they can all be kept.
			KingdomCatalogueRules.SupportTally supports = Tally(0, 0, 0);
			KingdomSubsidenceRules.Trajectory trajectory = KingdomSubsidenceRules.Slide(
				50, GrowthStage.City, 2048, supports, KingdomSubsidenceRules.StepDays * 200, AlreadySliding: false);
			Assert.Greater(trajectory.Breakpoints.Count, 0, "a City with nothing standing did not fall");
			Assert.AreEqual(GrowthStage.City, trajectory.Breakpoints[0].From, "the first rung was not the one it started on");
			Assert.AreEqual(GrowthStage.Camp, trajectory.Breakpoints[trajectory.Breakpoints.Count - 1].To,
				"the last rung was not the floor it arrived at");
			Assert.LessOrEqual(trajectory.Breakpoints.Count, (int)GrowthStage.City,
				"more rungs were told than the ladder has");
			Assert.LessOrEqual(
				KingdomSubsidenceRules.ChronicleEntriesFor(trajectory.Departed, trajectory.Breakpoints.Count),
				KingdomSubsidenceRules.ChronicleBudgetPerSlide,
				"a real full collapse went over the budget");
		}

		// ==================================================================================
		// Condemnation: which ruined homes strand the people living in them.
		// ==================================================================================

		[Test]
		public void OneRungOfRuinDoesNotCondemnASoundHome()
		{
			// The half of the rule that must do nothing. A single lost rung leaves a home badly
			// used, and a badly used home still keeps the rain off -- so an occupied house that
			// took one rung records no brink and strands nobody.
			for (int roll = 0; roll < 100; roll++)
			{
				int afterOneRung = KingdomMaterialRules.AddWear(0, KingdomSubsidenceRules.RuinIncrement(roll));
				Assert.IsFalse(KingdomLodgingRules.IsCondemned(afterOneRung),
					"one rung of a slide emptied a house");
			}
		}

		[Test]
		public void EnoughRungsOfRuinDoCondemnAHomeAndTheCrossingHappensExactlyOnce()
		{
			// The other half. Two or three rungs bring a work to the wear ceiling, which is past
			// the condemnation line, so a City falling all the way really does leave people
			// without a roof -- and the crossing is a single event, which is what makes
			// "pre-record at the ruin that condemned it" a well-defined moment rather than a
			// thing that fires again on every later rung.
			int wear = 0;
			int crossings = 0;
			for (int rung = 0; rung < (int)GrowthStage.City; rung++)
			{
				int before = wear;
				wear = KingdomMaterialRules.AddWear(wear, KingdomSubsidenceRules.RuinIncrement(50));
				if (KingdomLodgingRules.IsCondemned(wear) && !KingdomLodgingRules.IsCondemned(before))
				{
					crossings++;
				}
			}
			Assert.IsTrue(KingdomLodgingRules.IsCondemned(wear), "a full collapse never condemned anything");
			Assert.AreEqual(1, crossings, "the condemning crossing fired more than once");
		}

		[Test]
		public void AWorkAlreadyAtTheCeilingCrossesNothingAndStrandsNobodyTwice()
		{
			// KingdomBrink.Record is idempotent, and the crossing test in front of it is the
			// other guard: a work already at MaxWearPercent takes no more wear, so it reports no
			// crossing and the people under it keep the honest tick they already had.
			int ceiling = KingdomMaterialRules.MaxWearPercent;
			int after = KingdomMaterialRules.AddWear(ceiling, KingdomSubsidenceRules.RuinIncrement(99));
			Assert.AreEqual(ceiling, after, "the ceiling stopped being a ceiling");
			Assert.IsTrue(KingdomLodgingRules.IsCondemned(ceiling) && KingdomLodgingRules.IsCondemned(after),
				"a work at the ceiling was not condemned");
		}

		[Test]
		public void RuinIsDamageAndACondemnedHomeIsAlwaysMendable()
		{
			// The protection law and the arrest, together: nothing is cleared, the ceiling is
			// above the condemnation line, and every point of the damage goes back through the
			// ordinary mending. A condemnation is answered by acting, never by waiting.
			Assert.Less(KingdomLodgingRules.CondemnedWearPercent, KingdomMaterialRules.MaxWearPercent);
			for (int roll = 0; roll < 100; roll++)
			{
				Assert.Greater(KingdomSubsidenceRules.RuinIncrement(roll), 0, "a ruin was a no-op that read like one");
				Assert.LessOrEqual(KingdomMaterialRules.AddWear(0, KingdomSubsidenceRules.RuinIncrement(roll)),
					KingdomMaterialRules.MaxWearPercent);
			}
		}

		// ==================================================================================
		// The field of ruins (Addendum 10(c)): how far a fall reaches, how deep, and what it
		// costs the register to say so.
		//
		// KingdomSubsidence.Ruin is engine-coupled, so what is reproduced here is its SELECTION
		// rule -- the same four rules functions in the same order over a synthetic settlement of
		// numbered works. Everything these tests assert is therefore a property of the rules the
		// engine calls, not of a re-implementation: change the ladder and these move.
		// ==================================================================================

		private sealed class FieldOfRuins
		{
			/// <summary>Wear each work was left standing at.</summary>
			public int[] Wear;

			/// <summary>Works the fall left the worse for it at all.</summary>
			public int Ruined;

			/// <summary>Homes that crossed the condemnation line, counted at the crossing.</summary>
			public int Crossings;

			/// <summary>Chronicle entries the RUIN half of the telling spent.</summary>
			public int Entries;
		}

		/// <summary>Walks the rungs of a fall exactly as <c>KingdomSubsidence.Ruin</c> does: every
		/// standing work asked once a rung, at that rung's own reach, damaged through the wear
		/// ceiling, with the crossing counted and the telling sampled.</summary>
		private static FieldOfRuins Collapse(string SettlementId, int Works, GrowthStage From, GrowthStage To)
		{
			FieldOfRuins field = new FieldOfRuins();
			field.Wear = new int[Works];
			ulong ordinal = 4800uL;
			for (GrowthStage rung = From; rung > To; rung = (GrowthStage)((int)rung - 1))
			{
				int ruinedThisRung = 0;
				int named = 0;
				int deepest = 0;
				for (int i = 0; i < Works; i++)
				{
					string id = "work-" + i;
					if (!KingdomSubsidenceRules.RollRuin(SettlementId, id, ordinal, rung))
					{
						continue;
					}
					int increment = KingdomSubsidenceRules.RolledRuinIncrement(SettlementId, id, ordinal);
					if (increment <= 0)
					{
						continue;
					}
					int before = field.Wear[i];
					int after = KingdomMaterialRules.AddWear(before, increment);
					if (after == before)
					{
						continue;
					}
					field.Wear[i] = after;
					ruinedThisRung++;
					if (after > deepest)
					{
						deepest = after;
					}
					if (KingdomLodgingRules.IsCondemned(after) && !KingdomLodgingRules.IsCondemned(before))
					{
						field.Crossings++;
					}
					if (KingdomSubsidenceRules.TellsRuin(ruinedThisRung - 1))
					{
						named++;
						field.Entries++;
					}
				}
				if (KingdomSubsidenceRules.RuinSummary("Ashmarch", ruinedThisRung, named, deepest) != null)
				{
					field.Entries++;
				}
				ordinal += 4800uL;
			}
			for (int i = 0; i < Works; i++)
			{
				if (field.Wear[i] > 0)
				{
					field.Ruined++;
				}
			}
			return field;
		}

		[Test]
		public void AFullCollapseLeavesMostOfTheFormerBuildingPlotsInRuins()
		{
			// The ruling itself: "a place that has gone from city back to a few tents should have
			// ruins on the plots that were previously buildings". Under the retired flat
			// allowance this was two works a rung, eight in the whole fall, and every other plot
			// pristine however many dozen were standing.
			const int works = 40;
			FieldOfRuins field = Collapse("taf:settlement:ashmarch", works, GrowthStage.City, GrowthStage.Camp);
			Assert.Greater(field.Ruined, works / 2, "a city fell all the way back and most of it was untouched");
			Assert.Greater(field.Ruined, (int)GrowthStage.City * KingdomWearRules.MaxWorksDamagedPerRaid,
				"the fall reached no more works than the retired flat allowance would have");
			Assert.LessOrEqual(field.Ruined, works, "more works were ruined than were standing");
			// The protection law, across the whole field: every plot still has its work on it,
			// every one of them is still running, and every point of the damage is mendable.
			// A collapse ruins; it never clears.
			for (int i = 0; i < field.Wear.Length; i++)
			{
				Assert.LessOrEqual(field.Wear[i], KingdomMaterialRules.MaxWearPercent,
					"work " + i + " was run past the ceiling a mending has to undo");
				Assert.Greater(KingdomMaterialRules.ConditionPercent(field.Wear[i]), 0,
					"work " + i + " stopped standing");
			}
		}

		[Test]
		public void AFullCollapseLeavesRuinsInEveryStageThereIs()
		{
			// "In appropriate stages of ruin" -- the varied half. Works are asked once a rung at
			// four narrowing chances, so a work taken by one rung is knocked about and one taken
			// by three is a shell, and a founder walking back in reads the difference.
			FieldOfRuins field = Collapse("taf:settlement:ashmarch", 40, GrowthStage.City, GrowthStage.Camp);
			List<string> stages = new List<string>();
			for (int i = 0; i < field.Wear.Length; i++)
			{
				string word = KingdomMaterialRules.ConditionWord(field.Wear[i]);
				if (!stages.Contains(word))
				{
					stages.Add(word);
				}
			}
			Assert.IsTrue(stages.Contains("knocked about"), "nothing was merely knocked about");
			Assert.IsTrue(stages.Contains("badly used"), "nothing was badly used");
			Assert.IsTrue(stages.Contains("half-wrecked"), "nothing was left a ruin");
			Assert.IsTrue(stages.Contains("sound"), "a fall that spared nothing at all is not a field of ruins");
		}

		[Test]
		public void AShallowSlideScuffsACornerAndAFullCollapseTakesTheSettlement()
		{
			// The reach rule's whole point: depth of fall decides breadth of ruin. One rung and
			// four rungs of the same settlement are not the same event.
			FieldOfRuins shallow = Collapse("taf:settlement:ashmarch", 40, GrowthStage.Town, GrowthStage.Village);
			FieldOfRuins full = Collapse("taf:settlement:ashmarch", 40, GrowthStage.City, GrowthStage.Camp);
			Assert.Greater(shallow.Ruined, 0, "a lost rung scuffed nothing at all");
			Assert.Less(shallow.Ruined, full.Ruined, "a one-rung slide cost as much as a whole collapse");
			Assert.AreEqual(0, shallow.Crossings, "one rung emptied a house");
		}

		[Test]
		public void TheSameFallLeavesTheSameFieldOfRuinsEveryTimeItIsAsked()
		{
			// The determinism the chronicle depends on: a reload re-reads a collapse, it does not
			// re-roll one. Same settlement, same works, same rungs, same lattice of ordinals.
			FieldOfRuins first = Collapse("taf:settlement:ashmarch", 40, GrowthStage.City, GrowthStage.Camp);
			FieldOfRuins second = Collapse("taf:settlement:ashmarch", 40, GrowthStage.City, GrowthStage.Camp);
			Assert.AreEqual(first.Ruined, second.Ruined);
			Assert.AreEqual(first.Crossings, second.Crossings);
			for (int i = 0; i < first.Wear.Length; i++)
			{
				Assert.AreEqual(first.Wear[i], second.Wear[i], "work " + i + " was ruined differently the second time");
			}
		}

		[Test]
		public void TwoSettlementsFallingTheSameWayFallDifferently()
		{
			FieldOfRuins here = Collapse("taf:settlement:ashmarch", 40, GrowthStage.City, GrowthStage.Camp);
			FieldOfRuins there = Collapse("taf:settlement:tamsketh", 40, GrowthStage.City, GrowthStage.Camp);
			bool differed = false;
			for (int i = 0; i < here.Wear.Length; i++)
			{
				if (here.Wear[i] != there.Wear[i])
				{
					differed = true;
				}
			}
			Assert.IsTrue(differed, "two settlements were handed one collapse between them");
		}

		[Test]
		public void EveryHomeThatCrossesTheCondemnationLineIsCountedAtItsCrossing()
		{
			// The hook lives inside the damage loop and the damage loop has no count to stop at,
			// so a home that crossed is a home that was recorded -- not just the first couple.
			// Told-by-name is a separate, much smaller number, which is the proof the two are not
			// the same gate.
			FieldOfRuins field = Collapse("taf:settlement:ashmarch", 40, GrowthStage.City, GrowthStage.Camp);
			int condemned = 0;
			for (int i = 0; i < field.Wear.Length; i++)
			{
				if (KingdomLodgingRules.IsCondemned(field.Wear[i]))
				{
					condemned++;
				}
			}
			Assert.Greater(condemned, 0, "a whole collapse condemned nothing");
			Assert.AreEqual(condemned, field.Crossings, "a condemned home crossed the line more or less than once");
			Assert.Greater(field.Crossings, (int)GrowthStage.City * KingdomSubsidenceRules.NamedRuinsPerBreakpoint,
				"no more homes reached their brink than the chronicle happened to name");
		}

		[Test]
		public void TheTellingIsCoarsenedAndTheDamageIsNot()
		{
			// Addendum 10(c) let a rung reach the whole settlement; the register's share of a
			// collapse did not move a line for it. Three times the works, three times the ruins,
			// the same number of entries.
			FieldOfRuins small = Collapse("taf:settlement:ashmarch", 40, GrowthStage.City, GrowthStage.Camp);
			FieldOfRuins large = Collapse("taf:settlement:ashmarch", 120, GrowthStage.City, GrowthStage.Camp);
			Assert.Greater(large.Ruined, small.Ruined, "a bigger settlement lost no more works than a smaller one");
			Assert.AreEqual(small.Entries, large.Entries, "the register still paid by the ruined work");
			Assert.LessOrEqual(large.Entries, (int)GrowthStage.City * (KingdomSubsidenceRules.NamedRuinsPerBreakpoint + 1),
				"a rung spent more entries than its named ruins plus one summary");
		}

		[Test]
		public void AWholeCollapseOfADozensStrongCityStaysInsideTheChronicleBudget()
		{
			// The end-to-end budget, ruins included: forty-six people, four rungs, and a hundred
			// and twenty works standing when it began.
			FieldOfRuins field = Collapse("taf:settlement:ashmarch", 120, GrowthStage.City, GrowthStage.Camp);
			int rungs = (int)GrowthStage.City;
			int departures = KingdomSubsidenceRules.NamedDepartures(46) + 1;
			Assert.LessOrEqual(departures + rungs + field.Entries, KingdomSubsidenceRules.ChronicleBudgetPerSlide,
				"a real full collapse with a real field of ruins went over the budget");
			Assert.AreEqual(departures + rungs + field.Entries, KingdomSubsidenceRules.ChronicleEntriesFor(46, rungs),
				"the promised arithmetic and what a real fall actually writes disagree");
		}

		[Test]
		public void RollRuin_AWiderRungRuinsASupersetOfWhatANarrowerOneWould()
		{
			// The draw did not move when the reach did: same key, same number, only more of the
			// ladder answering yes. This is what makes an existing save's collapse reproducible
			// and what makes "each lost rung reaches the works that rung's scale supported" a
			// statement about one question rather than five.
			bool sawGrowth = false;
			for (int i = 0; i < 60; i++)
			{
				if (KingdomSubsidenceRules.RollRuin("taf:settlement:ashmarch", "work-" + i, 4800uL, GrowthStage.Steading))
				{
					Assert.IsTrue(KingdomSubsidenceRules.RollRuin("taf:settlement:ashmarch", "work-" + i, 4800uL, GrowthStage.City),
						"a work a narrow rung took was spared by a wider one");
				}
				else if (KingdomSubsidenceRules.RollRuin("taf:settlement:ashmarch", "work-" + i, 4800uL, GrowthStage.City))
				{
					sawGrowth = true;
				}
			}
			Assert.IsTrue(sawGrowth, "the widest rung reached nothing the narrowest did not");
		}

		// --- What a rung's ruins say ------------------------------------------------------

		[TestCase(-1, false)]
		[TestCase(0, true)]
		[TestCase(KingdomSubsidenceRules.NamedRuinsPerBreakpoint, false)]
		public void TellsRuin_NamesTheFirstFewAndNoMore(int index, bool expected)
		{
			Assert.AreEqual(expected, KingdomSubsidenceRules.TellsRuin(index));
		}

		[Test]
		public void RuinedWorkLine_NamesTheWorkThePlaceAndWhyNobodyKeptIt()
		{
			string line = KingdomSubsidenceRules.RuinedWorkLine("the tannery", "Ashmarch");
			StringAssert.Contains("the tannery", line);
			StringAssert.Contains("Ashmarch", line);
			StringAssert.Contains("nobody left who kept it", line);
			StringAssert.Contains("a work", KingdomSubsidenceRules.RuinedWorkLine(null, "Ashmarch"));
		}

		[Test]
		public void RuinSummary_CarriesEveryRuinTheSampleDidNotNameAndSaysHowBadTheWorstGot()
		{
			string line = KingdomSubsidenceRules.RuinSummary("Ashmarch", 9, 1, KingdomMaterialRules.MaxWearPercent);
			StringAssert.Contains("8 more works", line);
			StringAssert.Contains("Ashmarch", line);
			StringAssert.Contains(KingdomMaterialRules.ConditionWord(KingdomMaterialRules.MaxWearPercent), line);
			StringAssert.Contains("one more work", KingdomSubsidenceRules.RuinSummary("Ashmarch", 2, 1, 30));
			StringAssert.Contains(KingdomMaterialRules.ConditionWord(30), KingdomSubsidenceRules.RuinSummary("Ashmarch", 2, 1, 30));
		}

		[Test]
		public void RuinSummary_SaysNothingWhenTheSampleNamedThemAll()
		{
			Assert.IsNull(KingdomSubsidenceRules.RuinSummary("Ashmarch", 1, 1, 20));
			Assert.IsNull(KingdomSubsidenceRules.RuinSummary("Ashmarch", 0, 0, 0));
			Assert.IsNull(KingdomSubsidenceRules.RuinSummary("Ashmarch", -3, -1, 0));
		}
	}
}
#endif
