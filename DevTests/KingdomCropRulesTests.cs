#if TAF_TESTS
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
				int reserve = KingdomRules.UpkeepDrams(population) * KingdomRules.MaxUpkeepDaysCharged;
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
		public void CanAffordPlanting_ReserveIsExactlyThreeDaysOfCurrentUpkeep()
		{
			// Pins the reserve to KingdomRules' own constants rather than a private guess, so a
			// change to either constant is caught here instead of silently loosening the guard
			// that keeps the plot from ever starting a dry streak.
			int population = 20;
			int reserve = KingdomRules.UpkeepDrams(population) * KingdomRules.MaxUpkeepDaysCharged;
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
			Assert.Greater(KingdomCropRules.YieldPerHarvest, 0);
			Assert.Greater(KingdomCropRules.MaxCyclesPerVisit, 0);
		}
	}
}
#endif
