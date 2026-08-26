#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public class KingdomYardGoodsRulesTests
	{
		private static KingdomYardGoodsRules.HouseholdEvidence House(string Plot = "lot-1",
			string Key = "dyevat")
		{
			return new KingdomYardGoodsRules.HouseholdEvidence
			{
				PlotId = Plot, YardKey = Key, Built = true, Eligible = true,
				FeedsGoods = true, Working = true
			};
		}

		private static KingdomYardGoodsRules.FixtureEvidence Fixture(string Plot = "lot-1",
			string Key = "dyevat")
		{
			return new KingdomYardGoodsRules.FixtureEvidence
			{
				PlotId = Plot, YardKey = Key, Standing = true, InYard = true,
				FeedsGoods = true
			};
		}

		[Test]
		public void OneExactHouseAndItsPhysicalFixtureProduceOneGood()
		{
			Assert.AreEqual(1, KingdomYardGoodsRules.ExactStandingHouseholds(
				new List<KingdomYardGoodsRules.HouseholdEvidence> { House() },
				new List<KingdomYardGoodsRules.FixtureEvidence> { Fixture() }));
		}

		[Test]
		public void MissingMovedMismatchedAndNonGoodsFixturesProduceNothing()
		{
			List<KingdomYardGoodsRules.HouseholdEvidence> houses =
				new List<KingdomYardGoodsRules.HouseholdEvidence> { House() };
			Assert.AreEqual(0, KingdomYardGoodsRules.ExactStandingHouseholds(houses,
				new List<KingdomYardGoodsRules.FixtureEvidence>()));
			KingdomYardGoodsRules.FixtureEvidence moved = Fixture(); moved.InYard = false;
			Assert.AreEqual(0, KingdomYardGoodsRules.ExactStandingHouseholds(houses,
				new List<KingdomYardGoodsRules.FixtureEvidence> { moved }));
			Assert.AreEqual(0, KingdomYardGoodsRules.ExactStandingHouseholds(houses,
				new List<KingdomYardGoodsRules.FixtureEvidence> { Fixture("lot-2") }));
			KingdomYardGoodsRules.FixtureEvidence support = Fixture(); support.FeedsGoods = false;
			Assert.AreEqual(0, KingdomYardGoodsRules.ExactStandingHouseholds(houses,
				new List<KingdomYardGoodsRules.FixtureEvidence> { support }));
		}

		[Test]
		public void DuplicateHouseOrFixtureAuthorityFailsClosed()
		{
			Assert.AreEqual(0, KingdomYardGoodsRules.ExactStandingHouseholds(
				new List<KingdomYardGoodsRules.HouseholdEvidence> { House(), House() },
				new List<KingdomYardGoodsRules.FixtureEvidence> { Fixture() }));
			Assert.AreEqual(0, KingdomYardGoodsRules.ExactStandingHouseholds(
				new List<KingdomYardGoodsRules.HouseholdEvidence> { House() },
				new List<KingdomYardGoodsRules.FixtureEvidence> { Fixture(), Fixture() }));
		}

		[Test]
		public void HouseholdMustBeBuiltEligibleWorkingAndDeclaredAsGoods()
		{
			Action<KingdomYardGoodsRules.HouseholdEvidence> rejects = delegate(
				KingdomYardGoodsRules.HouseholdEvidence house)
			{
				Assert.AreEqual(0, KingdomYardGoodsRules.ExactStandingHouseholds(
					new List<KingdomYardGoodsRules.HouseholdEvidence> { house },
					new List<KingdomYardGoodsRules.FixtureEvidence> { Fixture() }));
			};
			KingdomYardGoodsRules.HouseholdEvidence row = House(); row.Built = false; rejects(row);
			row = House(); row.Eligible = false; rejects(row);
			row = House(); row.Working = false; rejects(row);
			row = House(); row.FeedsGoods = false; rejects(row);
		}

		[Test]
		public void MatchingIsExactOrdinalAndResultIsHardCapped()
		{
			List<KingdomYardGoodsRules.HouseholdEvidence> houses =
				new List<KingdomYardGoodsRules.HouseholdEvidence>();
			List<KingdomYardGoodsRules.FixtureEvidence> fixtures =
				new List<KingdomYardGoodsRules.FixtureEvidence>();
			for (int i = 0; i < 9; i++)
			{
				houses.Add(House("lot-" + i));
				fixtures.Add(Fixture("lot-" + i));
			}
			Assert.AreEqual(KingdomYardGoodsRules.MaxHouseholdsPerCaravan,
				KingdomYardGoodsRules.ExactStandingHouseholds(houses, fixtures));
			Assert.AreEqual(0, KingdomYardGoodsRules.ExactStandingHouseholds(
				new List<KingdomYardGoodsRules.HouseholdEvidence> { House("LOT-1") },
				new List<KingdomYardGoodsRules.FixtureEvidence> { Fixture("lot-1") }));
		}

		[TestCase(6, 0, 6)]
		[TestCase(6, 1, 7)]
		[TestCase(6, 4, 10)]
		[TestCase(6, 99, 10)]
		[TestCase(6, -1, 6)]
		[TestCase(-1, 1, -1)]
		public void IncomeFreezesOneDramPerExactHouseholdWithinCap(int Base, int Houses,
			int Expected)
		{
			Assert.AreEqual(Expected, KingdomYardGoodsRules.IncomePerCycle(Base, Houses));
		}

		[Test]
		public void IncomeSaturatesInsteadOfOverflowing()
		{
			Assert.AreEqual(int.MaxValue,
				KingdomYardGoodsRules.IncomePerCycle(int.MaxValue, 4));
		}

		[Test]
		public void FounderFacingSummaryDisclosesRateAndCap()
		{
			string summary = KingdomYardGoodsRules.EffectSummary();
			StringAssert.Contains("1 dram", summary);
			StringAssert.Contains("4 drams", summary);
			StringAssert.Contains("charter", summary);
		}
	}

	[TestFixture]
	public class KingdomYardGoodsSourceTests
	{
		[Test]
		public void DeliveryConsumesSharedSurveyAndFreezesAdjustedIncomeBeforeMutation()
		{
			string trade = TestMain.ReadRepositoryText("Trade/KingdomTrade.cs");
			int start = trade.IndexOf("private static bool PrepareCharterDelivery(",
				StringComparison.Ordinal);
			int end = trade.IndexOf("private static bool TryProjectionRow(", start,
				StringComparison.Ordinal);
			Assert.GreaterOrEqual(start, 0);
			Assert.Greater(end, start);
			string prepare = trade.Substring(start, end - start);
			StringAssert.Contains("KingdomSurvey Survey", prepare);
			StringAssert.Contains("KingdomYardGoods.ExactStandingHouseholds(Survey)", prepare);
			StringAssert.Contains("KingdomYardGoodsRules.IncomePerCycle(", prepare);
			StringAssert.Contains("operation.IncomePerCycle = incomePerCycle", prepare);
			StringAssert.Contains("operation.RequestedWater = water", prepare);
			Assert.Less(prepare.IndexOf("KingdomYardGoods.ExactStandingHouseholds(Survey)",
				StringComparison.Ordinal), prepare.IndexOf("KingdomTradeRules.NewOperation(",
				StringComparison.Ordinal));
			StringAssert.Contains("Z,\n\t\t\t\tsurvey, now", trade);
		}

		[Test]
		public void GoodsProjectionUsesOnlyTheMaintainedSurveyAndExactPhysicalPairs()
		{
			string runtime = TestMain.ReadRepositoryText("Growth/KingdomYardGoods.cs");
			StringAssert.Contains("Survey.Built", runtime);
			StringAssert.Contains("Survey.Objects", runtime);
			StringAssert.Contains("KingdomYards.YardWorkProperty", runtime);
			StringAssert.Contains("KingdomPlots.PlotIdProperty", runtime);
			StringAssert.Contains("StandsInMatchingYard", runtime);
			Assert.IsFalse(runtime.Contains("GetObjects("));
			Assert.IsFalse(runtime.Contains("KingdomSurvey.Take("));
		}
	}
}
#endif
