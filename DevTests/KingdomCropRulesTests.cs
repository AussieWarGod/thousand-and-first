#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomCropRulesTests
	{
		// --- CropBlueprintForStyle: the ground decides the crop, with a total fallback --------

		[TestCase("common", "Starapple")]
		[TestCase("verdant", "Vinewafer")]
		[TestCase("fungal", "Plump Mushroom")]
		[TestCase("gyre", "Godshroom Cap")]
		[TestCase("eater", "Dreadroot Tuber")]
		[TestCase("nonesuch", "Starapple")]
		[TestCase("", "Starapple")]
		[TestCase(null, "Starapple")]
		public void CropBlueprintForStyle_MatchesTheDocumentedMapping(string style, string expected)
		{
			Assert.AreEqual(expected, KingdomCropRules.CropBlueprintForStyle(style));
		}

		[Test]
		public void CropBlueprintForStyle_EveryKnownStyleGrowsSomethingDifferent()
		{
			// A mutation that collapses two branches into the same return would leave a style's
			// crop indistinguishable from another's; every known KingdomRules.Styles entry must
			// resolve to its own blueprint.
			string[] known = KingdomRules.Styles;
			for (int i = 0; i < known.Length; i++)
			{
				for (int j = i + 1; j < known.Length; j++)
				{
					Assert.AreNotEqual(
						KingdomCropRules.CropBlueprintForStyle(known[i]),
						KingdomCropRules.CropBlueprintForStyle(known[j]),
						known[i] + " and " + known[j] + " must not grow the same crop");
				}
			}
		}

		// --- CanAffordPlanting: the plot may only spend what upkeep will never need -----------

		// Boundaries derived from the rule rather than copied from a tuning constant, so a
		// retune of upkeep cannot silently invalidate what these claim to prove.

		[Test]
		public void CanAffordPlanting_PlantsWhenNothingIsOwedAndTheCostIsCovered()
		{
			Assert.IsTrue(KingdomCropRules.CanAffordPlanting(KingdomCropRules.PlantWaterCostDrams, 0));
			Assert.IsFalse(KingdomCropRules.CanAffordPlanting(KingdomCropRules.PlantWaterCostDrams - 1, 0));
		}

		[Test]
		public void CanAffordPlanting_WillNotDrinkTheSettlementsReserve()
		{
			foreach (int population in new int[3] { 8, 40, 60 })
			{
				int reserve = KingdomRules.UpkeepDrams(population) * KingdomRules.ReserveDays;
				int enough = reserve + KingdomCropRules.PlantWaterCostDrams;
				Assert.IsTrue(KingdomCropRules.CanAffordPlanting(enough, population),
					"refused to plant with the reserve intact at population " + population);
				Assert.IsFalse(KingdomCropRules.CanAffordPlanting(enough - 1, population),
					"planted into the settlement's own reserve at population " + population);
			}
		}

		[Test]
		public void CanAffordPlanting_NothingStoredNeverPlants()
		{
			Assert.IsFalse(KingdomCropRules.CanAffordPlanting(0, 0));
			Assert.IsFalse(KingdomCropRules.CanAffordPlanting(0, 40));
		}

		[Test]
		public void CanAffordPlanting_HigherPopulationRaisesTheReserve()
		{
			// Ten drams plants fine for an empty settlement; the same ten cannot plant once
			// there are enough settlers that three days of their own upkeep outweighs it. A
			// mutation that drops Population from the calculation would pass both as true.
			Assert.IsTrue(KingdomCropRules.CanAffordPlanting(10, 0));
			Assert.IsFalse(KingdomCropRules.CanAffordPlanting(10, 40));
		}

		[Test]
		public void CanAffordPlanting_ReserveIsExactlyReserveDaysOfCurrentUpkeep()
		{
			// Pins the reserve to KingdomRules' own constants rather than a private guess, so a
			// change to either constant is caught here instead of silently loosening the guard
			// that keeps the plot from ever starting a dry streak. The basis is ReserveDays -- a
			// cushion depth -- and no longer the retired absence cap it used to borrow.
			int population = 20;
			int reserve = KingdomRules.UpkeepDrams(population) * KingdomRules.ReserveDays;
			int exactBoundary = reserve + KingdomCropRules.PlantWaterCostDrams;
			Assert.IsTrue(KingdomCropRules.CanAffordPlanting(exactBoundary, population));
			Assert.IsFalse(KingdomCropRules.CanAffordPlanting(exactBoundary - 1, population));
		}

		// --- HasRipened / RipenTick: the growing clock ------------------------------------------

		[TestCase(100L, 99L, false)]
		[TestCase(100L, 100L, true)]
		[TestCase(100L, 101L, true)]
		[TestCase(0L, 0L, true)]
		public void HasRipened_ComparesAgainstTheStoredTickStamp(long nextStageTick, long timeTicks, bool expected)
		{
			Assert.AreEqual(expected, KingdomCropRules.HasRipened(nextStageTick, timeTicks));
		}

		[TestCase(0L)]
		[TestCase(1000L)]
		[TestCase(48000L)]
		public void RipenTick_IsPlantedTickPlusGrowTicks(long plantedTick)
		{
			Assert.AreEqual(plantedTick + KingdomCropRules.GrowTicks, KingdomCropRules.RipenTick(plantedTick));
		}

		// --- Constants and stage order: shape guards, not values a designer would tune --------

		[Test]
		public void PlotStage_AdvancesInTheDocumentedOrder()
		{
			// A mutation reordering the enum would still compile; this catches it by pinning
			// the numeric order the resolve loop's switch depends on nothing else to enforce.
			Assert.Less((int)KingdomCropRules.PlotStage.Dormant, (int)KingdomCropRules.PlotStage.Growing);
			Assert.Less((int)KingdomCropRules.PlotStage.Growing, (int)KingdomCropRules.PlotStage.Ripe);
		}

		[Test]
		public void EveryCycleQuantityIsPositive()
		{
			Assert.Greater(KingdomCropRules.PlantWaterCostDrams, 0);
			Assert.Greater(KingdomCropRules.GrowTicks, 0L);
			Assert.Greater(KingdomCropRules.YieldPerRow, 0);
			Assert.Greater(KingdomCropRules.CropDays, 0);
			Assert.Greater(KingdomCropRules.GatherDelayTicks, 0L);
			Assert.Greater(KingdomCropRules.MaxCyclesPerVisit, 0);
			Assert.Greater(KingdomCropRules.MaxSeedsPerResolve, 0);
		}

		// --- The derivation: a design's food figure comes off its rows ------------------------

		[Test]
		public void FoodPerDayForRows_IsRowsTimesYieldOverCropDays()
		{
			// The food lane's answer to TicksPerDay / mean(VariableRate). balance-sim.py asserts
			// the same identity against the real catalogue and the real blueprints; this asserts
			// the arithmetic itself, so a retune that breaks one breaks both.
			foreach (int rows in new int[6] { 6, 10, 16, 36, 52, 80 })
			{
				Assert.AreEqual(
					rows * KingdomCropRules.YieldPerRow / KingdomCropRules.CropDays,
					KingdomCropRules.FoodPerDayForRows(rows));
			}
		}

		[Test]
		public void FoodPerDayForRows_AndRowsForFoodPerDay_RoundTrip()
		{
			foreach (int food in new int[6] { 3, 5, 8, 18, 26, 40 })
			{
				int rows = KingdomCropRules.RowsForFoodPerDay(food);
				Assert.AreEqual(food, KingdomCropRules.FoodPerDayForRows(rows),
					"food:" + food + " does not round-trip through " + rows + " rows");
			}
		}

		[TestCase(0)]
		[TestCase(-1)]
		public void FoodPerDayForRows_GrowsNothingFromNoRows(int rows)
		{
			Assert.AreEqual(0, KingdomCropRules.FoodPerDayForRows(rows));
			Assert.AreEqual(0, KingdomCropRules.RowsForFoodPerDay(rows));
		}

		[Test]
		public void CropDaysForStyle_IsFlatAcrossEveryKnownStyle()
		{
			// Deliberately falsifiable. A design's Carries is ONE number and the founder does not
			// choose their ground, so a crop that took longer than another would make the same
			// field carry differently in a marsh than on a flower field. If a later build wants a
			// slow crop, this fails first and the catalogue's food figures have to become
			// per-style with it.
			foreach (string style in KingdomRules.Styles)
			{
				Assert.AreEqual(KingdomCropRules.CropDays, KingdomCropRules.CropDaysForStyle(style),
					style + " no longer ripens on the shared cycle; the catalogue's food figures must move with it");
			}
			Assert.AreEqual(KingdomCropRules.CropDays, KingdomCropRules.CropDaysForStyle("nonesuch"));
			Assert.AreEqual(KingdomCropRules.CropDays, KingdomCropRules.CropDaysForStyle(null));
		}

		// --- HarvestYield: rows, what a row is worth, and what the field is running at ---------

		[Test]
		public void HarvestYield_IsRowsTimesYieldScaledByEffectiveness()
		{
			Assert.AreEqual(16 * KingdomCropRules.YieldPerRow, KingdomCropRules.HarvestYield(16, 100));
			Assert.AreEqual(16 * KingdomCropRules.YieldPerRow / 2, KingdomCropRules.HarvestYield(16, 50));
		}

		[TestCase(0, 100)]
		[TestCase(-4, 100)]
		[TestCase(16, 0)]
		[TestCase(16, -1)]
		public void HarvestYield_GathersNothingFromNothing(int rows, int effectiveness)
		{
			Assert.AreEqual(0, KingdomCropRules.HarvestYield(rows, effectiveness));
		}

		[Test]
		public void HarvestYield_NeverPaysAboveFullEffectiveness()
		{
			// A stamp above 100 is a corrupt reading, not a bonus crop.
			Assert.AreEqual(
				KingdomCropRules.HarvestYield(16, 100),
				KingdomCropRules.HarvestYield(16, 400));
		}

		// --- The cycle, closed form -----------------------------------------------------------

		[Test]
		public void CyclesDue_IsNothingBeforeTheFirstRipening()
		{
			Assert.AreEqual(0, KingdomCropRules.CyclesDue(1000L, 999L));
			Assert.AreEqual(0, KingdomCropRules.CyclesDue(1000L, 0L));
		}

		[Test]
		public void CyclesDue_CountsEveryCompletedCycleOfALongAbsence()
		{
			long next = 10000L;
			Assert.AreEqual(1, KingdomCropRules.CyclesDue(next, next));
			Assert.AreEqual(1, KingdomCropRules.CyclesDue(next, next + KingdomCropRules.GrowTicks - 1L));
			Assert.AreEqual(2, KingdomCropRules.CyclesDue(next, next + KingdomCropRules.GrowTicks));
			Assert.AreEqual(13, KingdomCropRules.CyclesDue(next, next + 12L * KingdomCropRules.GrowTicks));
		}

		[Test]
		public void CyclesDue_ClampsRatherThanOverflowing()
		{
			Assert.AreEqual(
				KingdomCropRules.MaxCyclesPerVisit,
				KingdomCropRules.CyclesDue(0L, long.MaxValue / 2L));
		}

		[Test]
		public void RestampedRipeTick_RestampsFromTheHarvestAndNotFromNow()
		{
			// The part-cycle a field has already grown is KEPT. Restamping from "now" would throw
			// it away and quietly stretch every cycle after a homecoming.
			long next = 10000L;
			long now = next + KingdomCropRules.GrowTicks + 400L;
			int due = KingdomCropRules.CyclesDue(next, now);
			long restamped = KingdomCropRules.RestampedRipeTick(next, due);
			Assert.AreEqual(next + 2L * KingdomCropRules.GrowTicks, restamped);
			Assert.Greater(restamped, now);
			Assert.Less(restamped - now, KingdomCropRules.GrowTicks);
		}

		[Test]
		public void RestampedRipeTick_AlwaysAdvancesAtLeastOneCycle()
		{
			// A zero or negative count must never leave the stamp where it was: the resolve loop
			// would then re-gather the same crop on the next pass, forever.
			Assert.AreEqual(KingdomCropRules.GrowTicks, KingdomCropRules.RestampedRipeTick(0L, 0));
			Assert.AreEqual(KingdomCropRules.GrowTicks, KingdomCropRules.RestampedRipeTick(0L, -3));
		}

		[Test]
		public void LastRipeTick_DatesTheLastOfABatchOfCycles()
		{
			long next = 10000L;
			Assert.AreEqual(next, KingdomCropRules.LastRipeTick(next, 1));
			Assert.AreEqual(next + 3L * KingdomCropRules.GrowTicks, KingdomCropRules.LastRipeTick(next, 4));
			Assert.AreEqual(next, KingdomCropRules.LastRipeTick(next, 0));
		}

		[Test]
		public void MayGather_LeavesTheFounderTheirDay()
		{
			long ripe = 10000L;
			Assert.IsFalse(KingdomCropRules.MayGather(ripe, ripe));
			Assert.IsFalse(KingdomCropRules.MayGather(ripe, ripe + KingdomCropRules.GatherDelayTicks - 1L));
			Assert.IsTrue(KingdomCropRules.MayGather(ripe, ripe + KingdomCropRules.GatherDelayTicks));
		}

		// --- The founder's day, and what a gathering is actually owed --------------------------

		[Test]
		public void GatherableCycles_HoldsACropTheFounderHasNotHadTheirDayWith()
		{
			long next = 10000L;
			bool holds;
			Assert.AreEqual(0, KingdomCropRules.GatherableCycles(next, next, out holds));
			Assert.IsTrue(holds, "a crop that has just come ripe must be held, not gathered");
			Assert.AreEqual(1, KingdomCropRules.GatherableCycles(next, next + KingdomCropRules.GatherDelayTicks, out holds));
			Assert.IsFalse(holds);
		}

		[Test]
		public void GatherableCycles_GathersTheOldOnesAndHoldsTheNewest()
		{
			// Three ripenings due, the newest of them inside the founder's day: two are gathered
			// and the crop standing in front of them is left where it is.
			long next = 10000L;
			bool holds;
			int gather = KingdomCropRules.GatherableCycles(next, next + 2L * KingdomCropRules.GrowTicks, out holds);
			Assert.AreEqual(2, gather);
			Assert.IsTrue(holds);
			// And the restamp lands exactly on the held ripening, so the next pass finds it due.
			Assert.AreEqual(
				KingdomCropRules.LastRipeTick(next, 3),
				KingdomCropRules.RestampedRipeTick(next, gather));
		}

		[Test]
		public void GatherableCycles_IsNothingBeforeAnythingIsDue()
		{
			bool holds;
			Assert.AreEqual(0, KingdomCropRules.GatherableCycles(10000L, 9999L, out holds));
			Assert.IsFalse(holds, "nothing is being held when nothing has ripened");
		}

		[Test]
		public void GatheredYield_CreditsEveryCycleButTheWatchedOneAtWhatStands()
		{
			// Four cycles, sixteen rows standing, ten of them still ripe because the founder
			// walked the rows with a basket. Three cycles at sixteen, one at ten.
			int expected = KingdomCropRules.HarvestYield(16, 100) * 3 + KingdomCropRules.HarvestYield(10, 100);
			Assert.AreEqual(expected, KingdomCropRules.GatheredYield(16, 10, 4, CountsRipeLast: true, EffectivenessPercent: 100));
		}

		[Test]
		public void GatheredYield_CreditsEveryCycleAtWhatStandsWhenNobodyWasThere()
		{
			// An absence: TurnTick never ran, no row was ever made ripe, and nobody could have
			// taken one. A mutation that read the ripe count here would silently lose a season.
			Assert.AreEqual(
				KingdomCropRules.HarvestYield(16, 100) * 4,
				KingdomCropRules.GatheredYield(16, 0, 4, CountsRipeLast: false, EffectivenessPercent: 100));
		}

		[Test]
		public void GatheredYield_GathersNothingFromNoCycles()
		{
			Assert.AreEqual(0, KingdomCropRules.GatheredYield(16, 16, 0, CountsRipeLast: true, EffectivenessPercent: 100));
			Assert.AreEqual(0, KingdomCropRules.GatheredYield(16, 16, -2, CountsRipeLast: false, EffectivenessPercent: 100));
		}

		[Test]
		public void GatheredYield_ScalesTheWholeReckoningByEffectiveness()
		{
			Assert.Greater(
				KingdomCropRules.GatheredYield(16, 16, 4, CountsRipeLast: false, EffectivenessPercent: 100),
				KingdomCropRules.GatheredYield(16, 16, 4, CountsRipeLast: false, EffectivenessPercent: 50));
			Assert.AreEqual(0, KingdomCropRules.GatheredYield(16, 16, 4, CountsRipeLast: false, EffectivenessPercent: 0));
		}

		// --- Irrigation: vanilla's own event, answered on our clock ----------------------------

		[Test]
		public void IrrigatedRipeTick_PullsTheStampForwardByOnePulse()
		{
			Assert.AreEqual(
				10000L - KingdomCropRules.IrrigationTicksPerPulse,
				KingdomCropRules.IrrigatedRipeTick(10000L, 5000L));
		}

		[Test]
		public void IrrigatedRipeTick_NeverPullsAHarvestOutOfThePast()
		{
			// A machine may shorten a wait. It may not hand the settlement a crop that was due
			// before it was switched on, and it may not make the stamp read as overdue by more
			// than the cycle the field has actually stood.
			Assert.AreEqual(9999L, KingdomCropRules.IrrigatedRipeTick(10000L, 9999L));
			Assert.AreEqual(10000L, KingdomCropRules.IrrigatedRipeTick(10000L, 10000L));
			Assert.AreEqual(12000L, KingdomCropRules.IrrigatedRipeTick(10000L, 12000L));
		}

		[Test]
		public void IrrigatedRipeTick_HalvesAWholeCycleInHalfACyclesPulses()
		{
			// The claim the doc comment makes, checked: an irrigator firing once a turn takes a
			// six-day crop to three days. One pulse is one turn's own growing.
			long next = KingdomCropRules.GrowTicks;
			long now = 0L;
			int pulses = 0;
			while (next > now && pulses < 100000)
			{
				now += KingdomCropRules.IrrigationTicksPerPulse;
				next = KingdomCropRules.IrrigatedRipeTick(next, now);
				pulses++;
			}
			Assert.AreEqual(KingdomCropRules.GrowTicks / 2L, now,
				"a continuously irrigated crop should come ripe in half its own days");
		}

		// --- Seeds: the map runs both ways, and every style has one ---------------------------

		[Test]
		public void SeedForCrop_AndCropForSeed_RoundTripForEveryStyle()
		{
			foreach (string style in KingdomRules.Styles)
			{
				string crop = KingdomCropRules.CropBlueprintForStyle(style);
				string seed = KingdomCropRules.SeedForCrop(crop);
				Assert.IsNotNull(seed, style + " grows " + crop + " and no seed sows it");
				Assert.AreEqual(crop, KingdomCropRules.CropForSeed(seed));
				Assert.AreEqual(seed, KingdomCropRules.SeedForStyle(style));
			}
		}

		[Test]
		public void SeedBlueprints_IsExactlyTheSeedsTheStylesName()
		{
			Assert.AreEqual(KingdomRules.Styles.Length, KingdomCropRules.SeedBlueprints.Length);
			foreach (string seed in KingdomCropRules.SeedBlueprints)
			{
				Assert.IsNotNull(KingdomCropRules.CropForSeed(seed), seed + " grows nothing");
			}
			for (int i = 0; i < KingdomCropRules.SeedBlueprints.Length; i++)
			{
				for (int j = i + 1; j < KingdomCropRules.SeedBlueprints.Length; j++)
				{
					Assert.AreNotEqual(KingdomCropRules.SeedBlueprints[i], KingdomCropRules.SeedBlueprints[j]);
				}
			}
		}

		[Test]
		public void EveryCropHasARowToStandIn()
		{
			foreach (string style in KingdomRules.Styles)
			{
				string crop = KingdomCropRules.CropBlueprintForStyle(style);
				Assert.IsNotNull(KingdomCropRules.RowForCrop(crop), crop + " has nothing to stand as");
			}
		}

		[TestCase("")]
		[TestCase(null)]
		[TestCase("Wibble")]
		public void SeedAndRowMapsRefuseWhatTheyDoNotKnow(string unknown)
		{
			Assert.IsNull(KingdomCropRules.SeedForCrop(unknown));
			Assert.IsNull(KingdomCropRules.CropForSeed(unknown));
			Assert.IsNull(KingdomCropRules.RowForCrop(unknown));
		}

		// --- The gate: what refuses a sowing, in order ----------------------------------------

		[Test]
		public void AssessSow_AllowsASoundClaimedUnsownFieldWithWaterInHand()
		{
			Assert.AreEqual(
				KingdomCropRules.SowVerdict.Sown,
				KingdomCropRules.AssessSow(HasField: true, Claimed: true, AlreadySown: false, Condemned: false, HasRow: true, StoredWater: 1000, Population: 4));
		}

		[TestCase(false, true, false, false, true, 1000, KingdomCropRules.SowVerdict.NoField)]
		[TestCase(true, false, false, false, true, 1000, KingdomCropRules.SowVerdict.NotClaimed)]
		[TestCase(true, true, false, true, true, 1000, KingdomCropRules.SowVerdict.Condemned)]
		[TestCase(true, true, true, false, true, 1000, KingdomCropRules.SowVerdict.AlreadySown)]
		[TestCase(true, true, false, false, false, 1000, KingdomCropRules.SowVerdict.NoCrop)]
		[TestCase(true, true, false, false, true, 0, KingdomCropRules.SowVerdict.NoWater)]
		public void AssessSow_NamesTheFirstThingWrong(bool hasField, bool claimed, bool alreadySown, bool condemned, bool hasRow, int water, KingdomCropRules.SowVerdict expected)
		{
			Assert.AreEqual(expected,
				KingdomCropRules.AssessSow(hasField, claimed, alreadySown, condemned, hasRow, water, Population: 4));
		}

		[Test]
		public void AssessSow_RefusesTheGroundBeforeItRefusesTheSeed()
		{
			// Everything wrong at once. The founder is told about the thing they can act on first,
			// which is that they are not standing in a field at all.
			Assert.AreEqual(
				KingdomCropRules.SowVerdict.NoField,
				KingdomCropRules.AssessSow(HasField: false, Claimed: false, AlreadySown: true, Condemned: true, HasRow: false, StoredWater: 0, Population: 40));
		}

		[Test]
		public void SowRefusal_SaysSomethingForEveryVerdict()
		{
			foreach (KingdomCropRules.SowVerdict verdict in Enum.GetValues(typeof(KingdomCropRules.SowVerdict)))
			{
				string line = KingdomCropRules.SowRefusal(verdict);
				Assert.IsFalse(string.IsNullOrEmpty(line), verdict + " refuses in silence");
			}
		}

		[Test]
		public void WantNote_SaysSomethingForEveryRealWant()
		{
			// STANDARDS 7b: applicable-but-blocked must announce, and it cannot announce nothing.
			foreach (KingdomCropRules.FieldWant want in Enum.GetValues(typeof(KingdomCropRules.FieldWant)))
			{
				string line = KingdomCropRules.WantNote(want, "field", "Hearth");
				Assert.IsFalse(string.IsNullOrEmpty(line), want + " stalls in silence");
				Assert.IsTrue(line.Contains("Hearth"), want + " does not say where");
			}
		}

		[Test]
		public void WantNote_TellsTheSeedGateApartFromEveryOtherBlock()
		{
			string seed = KingdomCropRules.WantNote(KingdomCropRules.FieldWant.Seed, "field", "Hearth");
			foreach (KingdomCropRules.FieldWant want in Enum.GetValues(typeof(KingdomCropRules.FieldWant)))
			{
				if (want == KingdomCropRules.FieldWant.Seed)
				{
					continue;
				}
				Assert.AreNotEqual(seed, KingdomCropRules.WantNote(want, "field", "Hearth"));
			}
		}

		[Test]
		public void SowConfirm_NamesTheCropTheRowsTheWaitAndTheWater()
		{
			string text = KingdomCropRules.SowConfirm("vinewafer", "field", 16, KingdomCropRules.PlantWaterCostDrams);
			Assert.IsTrue(text.Contains("vinewafer"));
			Assert.IsTrue(text.Contains("16"));
			Assert.IsTrue(text.Contains(KingdomCropRules.CropDays.ToString()));
			Assert.IsTrue(text.Contains(KingdomCropRules.PlantWaterCostDrams.ToString()));
		}

		// --- Chronicle discipline: a season tells once, with a count ---------------------------

		[Test]
		public void HarvestChronicle_TellsASeasonOfHarvestsWithACount()
		{
			string many = KingdomCropRules.HarvestChronicle(12, 216, "Hearth", 3);
			Assert.IsTrue(many.Contains("12 harvests"), "a season of harvests must carry its count");
			Assert.IsTrue(many.Contains("216"));
			Assert.IsTrue(many.Contains("3 days before you saw it"));
		}

		[Test]
		public void HarvestChronicle_DoesNotCountASingleHarvest()
		{
			string one = KingdomCropRules.HarvestChronicle(1, 18, "Hearth", 0);
			Assert.IsFalse(one.Contains("1 harvests"));
			Assert.IsFalse(one.Contains("before you saw it"), "a harvest gathered today is not dated in the past");
		}

		[Test]
		public void HarvestNote_AccountsForEveryServingItNames()
		{
			string note = KingdomCropRules.HarvestNote(2, 36, 20, 10, 6);
			Assert.IsTrue(note.Contains("36"));
			Assert.IsTrue(note.Contains("20"));
			Assert.IsTrue(note.Contains("10"));
			Assert.IsTrue(note.Contains("6"));
		}

		[Test]
		public void HarvestNote_SaysSoWhenNothingReachedALarderHere()
		{
			string note = KingdomCropRules.HarvestNote(1, 18, 0, 18, 0);
			Assert.IsTrue(note.Contains("None of it reached a larder here"));
			Assert.IsTrue(note.Contains("on the road"));
		}

		// --- The seed-return draw: deterministic, bounded, and never free ----------------------

		[Test]
		public void RollSeedReturn_IsStableForTheSameFieldAndCycle()
		{
			bool first = KingdomCropRules.RollSeedReturn("taf:settlement:hearth", "field-1", 3uL);
			for (int i = 0; i < 8; i++)
			{
				Assert.AreEqual(first, KingdomCropRules.RollSeedReturn("taf:settlement:hearth", "field-1", 3uL),
					"the same question was answered two different ways");
			}
		}

		[Test]
		public void RollSeedReturn_AsksTwoFieldsSeparately()
		{
			// Two fields gathered on the same cycle must not be forced to share one answer, or a
			// city's whole harvest goes to seed together or not at all.
			int agree = 0;
			for (ulong ordinal = 0uL; ordinal < 40uL; ordinal++)
			{
				if (KingdomCropRules.RollSeedReturn("taf:settlement:hearth", "field-1", ordinal)
					== KingdomCropRules.RollSeedReturn("taf:settlement:hearth", "field-2", ordinal))
				{
					agree++;
				}
			}
			Assert.Less(agree, 40, "two fields answer identically on every cycle");
		}

		[Test]
		public void RollSeedReturn_RefusesAMalformedSettlementRatherThanFaulting()
		{
			Assert.IsFalse(KingdomCropRules.RollSeedReturn(null, "field-1", 1uL));
			Assert.IsFalse(KingdomCropRules.RollSeedReturn("", "field-1", 1uL));
		}

		[Test]
		public void RollSeedReturn_LandsNearItsDeclaredChance()
		{
			int hits = 0;
			for (ulong ordinal = 0uL; ordinal < 2000uL; ordinal++)
			{
				if (KingdomCropRules.RollSeedReturn("taf:settlement:hearth", "field-1", ordinal))
				{
					hits++;
				}
			}
			int expected = 2000 * KingdomCropRules.SeedReturnChancePercent / 100;
			Assert.Less(System.Math.Abs(hits - expected), 120,
				"the seed-return draw is " + hits + " in 2000 against a declared " + KingdomCropRules.SeedReturnChancePercent + "%");
		}

		[Test]
		public void SeedReturned_IsCappedHoweverLongTheAbsence()
		{
			Assert.LessOrEqual(
				KingdomCropRules.SeedReturned("taf:settlement:hearth", "field-1", 0uL, KingdomCropRules.MaxCyclesPerVisit, 5000),
				KingdomCropRules.MaxSeedsPerResolve);
		}

		[Test]
		public void SeedReturned_ReturnsNothingFromAHarvestThatYieldedNothing()
		{
			Assert.AreEqual(0, KingdomCropRules.SeedReturned("taf:settlement:hearth", "field-1", 0uL, 40, 0));
			Assert.AreEqual(0, KingdomCropRules.SeedReturned("taf:settlement:hearth", "field-1", 0uL, 0, 500));
		}

		[Test]
		public void FieldStream_FoldsAnyIdIntoTheKernelGrammar()
		{
			// Two different fields must not fold to the same stream, and an unnamed one must still
			// fold to something the kernel will accept rather than faulting the draw away.
			Assert.AreNotEqual(KingdomCropRules.FieldStream("A1"), KingdomCropRules.FieldStream("A2"));
			Assert.AreEqual(KingdomCropRules.FieldStream("A1"), KingdomCropRules.FieldStream("a1"));
			Assert.IsFalse(string.IsNullOrEmpty(KingdomCropRules.FieldStream(null)));
			Assert.IsTrue(KingdomCropRules.RollSeedReturn("taf:settlement:hearth", null, 1uL)
				|| !KingdomCropRules.RollSeedReturn("taf:settlement:hearth", null, 1uL));
		}
	}
}
#endif
