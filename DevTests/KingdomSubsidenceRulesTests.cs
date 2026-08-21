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
			Assert.AreEqual(KingdomCatalogueRules.Equilibrium(24, 30, 30, 0),
				KingdomSubsidenceRules.SupportedLevel(tally, GrowthStage.Town));
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

		[Test]
		public void RuinedWorksPerBreakpoint_IsNoWorseThanARaidThatGotPastTheWall()
		{
			Assert.AreEqual(KingdomWearRules.MaxWorksDamagedPerRaid, KingdomSubsidenceRules.RuinedWorksPerBreakpoint);
		}

		[Test]
		public void RollRuin_AnswersTheSameQuestionTheSameWayForever()
		{
			for (int i = 0; i < 8; i++)
			{
				Assert.AreEqual(
					KingdomSubsidenceRules.RollRuin("taf:settlement:ashmarch", "work-" + i, 4800uL),
					KingdomSubsidenceRules.RollRuin("taf:settlement:ashmarch", "work-" + i, 4800uL),
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
				if (KingdomSubsidenceRules.RollRuin("taf:settlement:ashmarch", "work-" + i, 4800uL))
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
				if (KingdomSubsidenceRules.RollRuin("taf:settlement:ashmarch", "work-" + i, 4800uL)
					!= KingdomSubsidenceRules.RollRuin("taf:settlement:ashmarch", "work-" + i, 9600uL))
				{
					differed = true;
				}
			}
			Assert.IsTrue(differed, "a second rung must be an independent question");
		}

		[Test]
		public void RollRuin_FailsClosedOnASettlementIdTheKernelWillNotTake()
		{
			Assert.IsFalse(KingdomSubsidenceRules.RollRuin(null, "work-1", 4800uL));
			Assert.IsFalse(KingdomSubsidenceRules.RollRuin("", "work-1", 4800uL));
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
	}
}
#endif
