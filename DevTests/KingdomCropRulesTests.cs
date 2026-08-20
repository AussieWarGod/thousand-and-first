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

		[TestCase(3, 0, true)]
		[TestCase(2, 0, false)]
		[TestCase(9, 8, true)]
		[TestCase(8, 8, false)]
		[TestCase(33, 40, true)]
		[TestCase(32, 40, false)]
		[TestCase(0, 0, false)]
		public void CanAffordPlanting_ReservesUpkeepBeforeSpending(int storedWater, int population, bool expected)
		{
			Assert.AreEqual(expected, KingdomCropRules.CanAffordPlanting(storedWater, population));
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
