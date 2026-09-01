#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomCatalogueArithmeticTests
	{
		[TestCase(1, 21474836)]
		[TestCase(25, 536870911)]
		[TestCase(50, 1073741823)]
		[TestCase(99, 2126008810)]
		[TestCase(100, int.MaxValue)]
		public void Carried_WidensBeforeScalingMaximumAmount(int percent, int expected)
		{
			Assert.AreEqual(expected, KingdomCatalogueRules.Carried(int.MaxValue, percent));
		}

		[Test]
		public void Carried_BoundsInputMatrixAndMatchesLongArithmetic()
		{
			int[] amounts = { int.MinValue, -1, 0, 1, 99, 100, int.MaxValue };
			int[] percents = { int.MinValue, -1, 0, 1, 25, 50, 99, 100, int.MaxValue };
			for (int a = 0; a < amounts.Length; a++)
			{
				for (int p = 0; p < percents.Length; p++)
				{
					int expected = amounts[a] <= 0 || percents[p] <= 0 ? 0
						: percents[p] >= 100 ? amounts[a]
						: (int)((long)amounts[a] * percents[p] / 100L);
					int actual = KingdomCatalogueRules.Carried(amounts[a], percents[p]);
					Assert.AreEqual(expected, actual, "amount " + amounts[a] + ", percent " + percents[p]);
					Assert.That(actual, Is.InRange(0, int.MaxValue));
				}
			}
		}

		[Test]
		public void FoldShade_RepeatedMaximumRowsSaturateEverySupportLane()
		{
			List<KindAmount> rows = Parse(
				"water:2147483647,water:2147483647,food:2147483647,food:2147483647,"
				+ "roof:2147483647,roof:2147483647,craft:2147483647,learning:2147483647");
			KingdomCatalogueRules.SupportTally tally = KingdomCatalogueRules.FoldShade(
				default(KingdomCatalogueRules.SupportTally), rows, 100);
			Assert.AreEqual(int.MaxValue, tally.Water);
			Assert.AreEqual(int.MaxValue, tally.Food);
			Assert.AreEqual(int.MaxValue, tally.Roof);
			Assert.AreEqual(int.MaxValue, tally.Lift);
			Assert.AreEqual(0, tally.Works);
		}

		[Test]
		public void FoldWork_RepeatedProvidersAndMaximumWorkCounterNeverWrap()
		{
			List<KindAmount> rows = Parse(
				"water:2147483647,food:2147483647,roof:2147483647,craft:2147483647");
			KingdomCatalogueRules.SupportTally tally = KingdomCatalogueRules.FoldWork(
				default(KingdomCatalogueRules.SupportTally), rows, 100);
			tally = KingdomCatalogueRules.FoldWork(tally, rows, 100);
			Assert.AreEqual(int.MaxValue, tally.Water);
			Assert.AreEqual(int.MaxValue, tally.Food);
			Assert.AreEqual(int.MaxValue, tally.Roof);
			Assert.AreEqual(int.MaxValue, tally.Lift);
			Assert.AreEqual(2, tally.Works);

			tally.Works = int.MaxValue;
			Assert.AreEqual(int.MaxValue, KingdomCatalogueRules.FoldWork(tally, null, 100).Works);
		}

		[Test]
		public void FoldingMalformedNegativeRunningStateClampsEveryCounter()
		{
			KingdomCatalogueRules.SupportTally tally = new KingdomCatalogueRules.SupportTally
			{
				Water = int.MinValue,
				Food = -1,
				Roof = int.MinValue,
				Lift = -1,
				Works = int.MinValue
			};
			tally = KingdomCatalogueRules.FoldShade(tally, null, 100);
			Assert.AreEqual(0, tally.Water);
			Assert.AreEqual(0, tally.Food);
			Assert.AreEqual(0, tally.Roof);
			Assert.AreEqual(0, tally.Lift);
			Assert.AreEqual(0, tally.Works);
		}

		[Test]
		public void DuplicateQueriesSaturateAndIgnoreMalformedNegativeRows()
		{
			List<KindAmount> rows = new List<KindAmount>
			{
				new KindAmount("water", int.MaxValue),
				new KindAmount("water", int.MaxValue),
				new KindAmount("water", -10),
				new KindAmount("craft", int.MaxValue),
				new KindAmount("moonlight", int.MaxValue),
				new KindAmount("learning", -10)
			};
			Assert.AreEqual(int.MaxValue, KingdomCatalogueRules.AmountOf(rows, "water"));
			Assert.AreEqual(int.MaxValue, KingdomCatalogueRules.LiftOf(rows));
			Assert.That(KingdomCatalogueRules.AmountOf(rows, "water"), Is.InRange(0, int.MaxValue));
			Assert.That(KingdomCatalogueRules.LiftOf(rows), Is.InRange(0, int.MaxValue));
		}

		[Test]
		public void SumCarries_RepeatedMaximumProvidersSaturateAllTotals()
		{
			string provider = "water:2147483647,food:2147483647,roof:2147483647,craft:2147483647";
			KingdomCatalogueRules.SupportTally tally = KingdomCatalogueRules.SumCarries(
				new[] { provider, provider });
			Assert.AreEqual(int.MaxValue, tally.Water);
			Assert.AreEqual(int.MaxValue, tally.Food);
			Assert.AreEqual(int.MaxValue, tally.Roof);
			Assert.AreEqual(int.MaxValue, tally.Lift);
			Assert.AreEqual(2, tally.Works);
		}

		[Test]
		public void Equilibrium_MaximumSupportsAndLiftSaturateWithoutWrap()
		{
			Assert.AreEqual(int.MaxValue, KingdomCatalogueRules.Equilibrium(
				int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue));
			Assert.AreEqual(int.MaxValue, KingdomCatalogueRules.Equilibrium(
				int.MaxValue, int.MaxValue, int.MaxValue, 1, 0));
			Assert.AreEqual(int.MaxValue, KingdomCatalogueRules.Equilibrium(
				int.MaxValue, int.MaxValue, int.MaxValue, 0, 0));
		}

		[Test]
		public void Equilibrium_WidensLiftCapBeforeTakingItsMinimum()
		{
			int binding = 1000000000;
			Assert.AreEqual(1500000000, KingdomCatalogueRules.Equilibrium(
				binding, binding, binding, int.MaxValue, int.MaxValue));
		}

		[Test]
		public void CounterPrimitivesClampMalformedInputsAndBothOverflowDirections()
		{
			Assert.AreEqual(int.MaxValue,
				KingdomCatalogueRules.SaturatingCounterAdd(int.MaxValue, int.MaxValue));
			Assert.AreEqual(7, KingdomCatalogueRules.SaturatingCounterAdd(-5, 7));
			Assert.AreEqual(0, KingdomCatalogueRules.SaturatingCounterAdd(-5, -7));
			Assert.AreEqual(0,
				KingdomCatalogueRules.SaturatingCounterSubtract(0, int.MaxValue));
			Assert.AreEqual(0,
				KingdomCatalogueRules.SaturatingCounterSubtract(7, int.MaxValue));
			Assert.AreEqual(int.MaxValue,
				KingdomCatalogueRules.SaturatingCounterSubtract(int.MaxValue, -1));
			Assert.AreEqual(int.MaxValue,
				KingdomCatalogueRules.SaturatingCounterMultiply(int.MaxValue, int.MaxValue));
			Assert.AreEqual(0,
				KingdomCatalogueRules.SaturatingCounterMultiply(int.MaxValue, -1));
		}

		[Test]
		public void CityTallyRepeatedMaximumSightingsSaturateAndClampEveryCounter()
		{
			KingdomCatalogueRules.SupportTally here = new KingdomCatalogueRules.SupportTally
			{
				Water = -1, Food = -1, Roof = -1, Lift = -1, Works = -1
			};
			List<KingdomSubsidenceRules.ZoneSighting> sightings =
				new List<KingdomSubsidenceRules.ZoneSighting>
			{
				new KingdomSubsidenceRules.ZoneSighting(int.MaxValue, int.MaxValue,
					int.MaxValue, int.MaxValue, 1L),
				new KingdomSubsidenceRules.ZoneSighting(int.MaxValue, int.MaxValue,
					int.MaxValue, int.MaxValue, 2L)
			};
			KingdomCatalogueRules.SupportTally city = KingdomSubsidenceRules.CityTally(
				here, sightings);
			Assert.AreEqual(int.MaxValue, city.Water);
			Assert.AreEqual(int.MaxValue, city.Food);
			Assert.AreEqual(int.MaxValue, city.Roof);
			Assert.AreEqual(0, city.Lift);
			Assert.AreEqual(0, city.Works);
			Assert.AreEqual(int.MaxValue, KingdomSubsidenceRules.CityStorage(
				int.MaxValue, sightings));
		}

		[Test]
		public void LevelFromWaterMaximumMatchesWidenedArithmeticAtEveryStage()
		{
			for (int i = 0; i < KingdomRules.StageUpkeepPercent.Length; i++)
			{
				int percent = KingdomRules.StageUpkeepPercent[i];
				int expected = percent <= 100 ? int.MaxValue
					: (int)((long)int.MaxValue * 100L / percent);
				int actual = KingdomSubsidenceRules.LevelFromWater(
					int.MaxValue, (GrowthStage)i);
				Assert.AreEqual(expected, actual, "stage " + (GrowthStage)i);
				Assert.That(actual, Is.InRange(0, int.MaxValue));
			}
		}

		[Test]
		public void RuntimeConsumersUseBoundedCounterArithmetic()
		{
			string fields = TestMain.ReadRepositoryText(
				"Growth/KingdomCrops.00.FieldStateAndFoodCredit.cs");
			string mills = TestMain.ReadRepositoryText("Growth/KingdomCrops.01.Milling.cs");
			string sightings = TestMain.ReadRepositoryText(
				"Growth/KingdomSubsidenceRules.Sightings.cs");
			string scope = TestMain.ReadRepositoryText(
				"Growth/KingdomSubsidence.ScopeAndSightings.cs");
			string growth = TestMain.ReadRepositoryText(
				"Growth/KingdomGrowth.z03.FoodAndHarvest.cs");
			string city = TestMain.ReadRepositoryText(
				"Simulation/City/KingdomCity.z09.WorksAndAudit.cs");
			string reach = TestMain.ReadRepositoryText(
				"Growth/KingdomReach.GroundCharacter.cs");
			StringAssert.Contains("cycled = KingdomCatalogueRules.SaturatingCounterAdd(", fields);
			StringAssert.Contains("milled = KingdomCatalogueRules.SaturatingCounterAdd(", mills);
			StringAssert.DoesNotContain("cycled +=", fields);
			StringAssert.DoesNotContain("milled +=", mills);
			StringAssert.DoesNotContain("tally.Water +=", sightings);
			StringAssert.DoesNotContain("tally.Food +=", sightings);
			StringAssert.DoesNotContain("tally.Roof +=", sightings);
			StringAssert.Contains("int food = 0;", scope);
			StringAssert.DoesNotContain("Supports.Food - KingdomCrops", scope);
			StringAssert.Contains("SaturatingCounterAdd(scoped,", scope);
			StringAssert.DoesNotContain("scoped +=", scope);
			StringAssert.Contains("KingdomCatalogueRules.SaturatingCounterMultiply(", growth);
			StringAssert.Contains("public static int FoodMadePerDay(KingdomSurvey Survey)", growth);
			StringAssert.Contains("return 0;", growth);
			StringAssert.DoesNotContain("MilledFoodPerDay(Survey) * Days", growth);
			StringAssert.DoesNotContain("ground * KingdomRules.PreserveMultiple", growth);
			StringAssert.Contains("private static int FoodMadePerDay(KingdomSurvey Survey)", city);
			StringAssert.Contains("return 0;", city);
			StringAssert.DoesNotContain("OrdinarySupports(Survey).Food\n\t\t\t\t-", city);
			StringAssert.Contains("KingdomCatalogueRules.SaturatingCounterAdd(total,", reach);
			StringAssert.DoesNotContain("total += KingdomHostedArcology.ReachOverlay", reach);
		}

		[Test]
		public void MillConversionArithmeticSaturatesAtMaximumInputs()
		{
			Assert.AreEqual(int.MaxValue, KingdomRules.MilledGain(int.MaxValue));
			Assert.AreEqual(1073741824, KingdomRules.CropsForGain(int.MaxValue));
			Assert.AreEqual(0, KingdomRules.MillableStock(int.MinValue, int.MaxValue));
			Assert.AreEqual(int.MaxValue, KingdomRules.MillableStock(int.MaxValue, 0));
		}

		[Test]
		public void ReachScalingAndLandingWidenBeforeApplyingPercentages()
		{
			Assert.AreEqual(21474836, KingdomReachRules.Scaled(int.MaxValue, 1));
			Assert.AreEqual(2126008810, KingdomReachRules.Scaled(int.MaxValue, 99));
			Assert.AreEqual(int.MaxValue, KingdomReachRules.Scaled(int.MaxValue, 100));
			Assert.AreEqual(int.MaxValue,
				KingdomReachRules.Scaled(int.MaxValue, int.MaxValue));
			Assert.AreEqual(int.MaxValue,
				KingdomReachRules.Landed(int.MaxValue, int.MaxValue, int.MaxValue));
			Assert.AreEqual(KingdomReachRules.QuarterRadiusCap,
				KingdomReachRules.QuarterRadius(int.MaxValue));
		}

		[Test]
		public void GroundCharacterRepeatedMaximumLiftsSaturatePerKindAndOverall()
		{
			GroundCharacter character = KingdomReachRules.Character(new[]
			{
				new KindAmount("craft", int.MaxValue),
				new KindAmount("craft", int.MaxValue),
				new KindAmount("spirit", int.MaxValue)
			});
			Assert.AreEqual(2, character.Lifts.Count);
			Assert.AreEqual(int.MaxValue, character.Lifts[0].Amount);
			Assert.AreEqual(int.MaxValue, character.Lifts[1].Amount);
			Assert.AreEqual(int.MaxValue, character.Total);
			Assert.AreEqual("craft", character.Dominant);
			Assert.AreEqual(int.MaxValue, character.DominantAmount);
		}

		[Test]
		public void YardShadeValidationCannotBeBypassedByRepeatedMaximumRows()
		{
			Assert.IsFalse(KingdomYardRules.TryParseYardWorkAttributes(
				"yard", "Yard", "Yard Fixture", "trade",
				"food:2147483647,food:2147483647", "No",
				out var spec, out var error));
			Assert.IsNull(spec);
			StringAssert.Contains(int.MaxValue.ToString(), error);
		}

		private static List<KindAmount> Parse(string source)
		{
			Assert.IsTrue(KingdomCatalogueRules.TryParseTally(source, out var tally, out var error), error);
			return tally;
		}
	}
}
#endif
