#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>Retired passive-food compatibility and live water-only scarcity contract.</summary>
	public class KingdomHungerRulesTests
	{
		[TestCase(-10)]
		[TestCase(0)]
		[TestCase(1)]
		[TestCase(50)]
		[TestCase(int.MaxValue)]
		public void PassiveRationProjection_IsAlwaysZero(int population)
		{
			Assert.AreEqual(0, KingdomRules.RationsPerDay(population));
			Assert.AreEqual(0, KingdomRules.RationsForElapsed(population, long.MaxValue));
			Assert.AreEqual(0, KingdomRules.RationsForElapsed(population,
				KingdomRules.TicksPerDay * 365L));
		}

		[TestCase(-1, -1)]
		[TestCase(0, 10)]
		[TestCase(10, 0)]
		[TestCase(100, 365)]
		public void AbstractForagingProjection_IsAlwaysZero(int hands, int days)
		{
			Assert.AreEqual(0, KingdomRules.ForagedRations(hands, days));
			Assert.AreEqual(0, KingdomRules.ForageRationsPerHand);
			Assert.AreEqual(0, KingdomRules.MaxForagedRationsPerDay);
		}

		[TestCase(-100, GrowthStage.Camp, 0)]
		[TestCase(1, GrowthStage.Village, 20)]
		[TestCase(2, GrowthStage.Town, 40)]
		[TestCase(int.MaxValue, GrowthStage.City, 50)]
		public void LegacyHungerProjection_CannotAdvanceOrDepart(int streak,
			GrowthStage stage, int population)
		{
			Assert.AreEqual(KingdomRules.HungerOutcome.Fed,
				KingdomRules.ResolveHunger(streak, stage, population));
		}

		[Test]
		public void EveryLegacyHungerOrdinal_HasNoBite()
		{
			foreach (KingdomRules.HungerOutcome hunger in
				Enum.GetValues(typeof(KingdomRules.HungerOutcome)))
			{
				Assert.AreEqual(KingdomRules.ScarcityBite.None,
					KingdomRules.BiteOfHunger(hunger), hunger.ToString());
			}
		}

		[Test]
		public void ComposeScarcity_IgnoresEveryLegacyHungerValue()
		{
			foreach (KingdomRules.ThirstOutcome thirst in
				Enum.GetValues(typeof(KingdomRules.ThirstOutcome)))
			{
				foreach (KingdomRules.HungerOutcome hunger in
					Enum.GetValues(typeof(KingdomRules.HungerOutcome)))
				{
					KingdomRules.ScarcityVerdict verdict =
						KingdomRules.ComposeScarcity(thirst, hunger);
					Assert.AreEqual(KingdomRules.BiteOfThirst(thirst), verdict.Bite);
					Assert.AreEqual(thirst != KingdomRules.ThirstOutcome.Sustained,
						verdict.Thirsting);
					Assert.AreEqual(thirst == KingdomRules.ThirstOutcome.Withering,
						verdict.Withering);
					Assert.IsFalse(verdict.Starving, hunger.ToString());
					Assert.IsFalse(verdict.Famishing, hunger.ToString());
					Assert.AreEqual(thirst == KingdomRules.ThirstOutcome.Sustained,
						verdict.Healthy);
				}
			}
		}

		[Test]
		public void FoodAlone_CannotNameOrCauseDeparture()
		{
			Assert.IsNull(KingdomRules.ScarcityDepartureClause(false, true));
			Assert.IsNull(KingdomRules.ScarcityDepartureNote(false, true));
			Assert.AreEqual(KingdomRules.ScarcityBite.None,
				KingdomRules.ComposeScarcity(KingdomRules.ThirstOutcome.Sustained,
					KingdomRules.HungerOutcome.Famine).Bite);
		}

		[Test]
		public void WaterDepartureText_RemainsUnchangedWhenLegacyFoodFlagIsPresent()
		{
			const string clause = "for wetter country, the cisterns having run dry";
			const string note = "for wetter country";
			Assert.AreEqual(clause, KingdomRules.ScarcityDepartureClause(true, false));
			Assert.AreEqual(clause, KingdomRules.ScarcityDepartureClause(true, true));
			Assert.AreEqual(note, KingdomRules.ScarcityDepartureNote(true, false));
			Assert.AreEqual(note, KingdomRules.ScarcityDepartureNote(true, true));
		}

		[TestCase(0, 99, 0)]
		[TestCase(-4, 99, 0)]
		[TestCase(8, 0, 8)]
		[TestCase(8, 50, 8)]
		public void MillableStock_HasNoInvisibleHouseholdReserve(int food, int population,
			int expected)
		{
			Assert.AreEqual(expected, KingdomRules.MillableStock(food, population));
		}

		[TestCase(0)]
		[TestCase(-1)]
		[TestCase(-9999)]
		public void LarderCapacity_FallsBackRatherThanReadingAsZero(int declared)
		{
			Assert.AreEqual(KingdomRules.DefaultLarderCapacity,
				KingdomRules.LarderCapacity(declared));
		}

		[TestCase(1)]
		[TestCase(64)]
		[TestCase(288)]
		public void LarderCapacity_TakesDeclaredSizeAtItsWord(int declared)
		{
			Assert.AreEqual(declared, KingdomRules.LarderCapacity(declared));
		}

		[Test]
		public void CivicLarderList_RemainsExact()
		{
			CollectionAssert.AreEquivalent(new[]
			{
				"r_KingdomLarder", "r_KingdomGranary", "r_KingdomRealmGranary"
			}, KingdomRules.CivicLarderBlueprints);
			Assert.IsFalse(KingdomRules.IsCivicLarderBlueprint("r_KingdomChargingPost"));
			Assert.IsFalse(KingdomRules.IsCivicLarderBlueprint(null));
		}

		[Test]
		public void HeartbeatAndCityCarry_CannotCreateAPassiveFoodDebitOrDeparture()
		{
			string heartbeat = TestMain.ReadRepositoryText(
				"Growth/KingdomGrowth.z02.ScarcityHeartbeat.cs");
			StringAssert.Contains("RetireLegacyFoodState(System);", heartbeat);
			StringAssert.Contains("KingdomRules.HungerOutcome.Fed", heartbeat);
			StringAssert.DoesNotContain("ConsumeFood(", heartbeat);
			StringAssert.DoesNotContain("RationsForElapsed", heartbeat);
			StringAssert.DoesNotContain("ResolveHunger", heartbeat);
			StringAssert.DoesNotContain("HungerStreak++", heartbeat);
			StringAssert.DoesNotContain("verdict.Starving", heartbeat);
			StringAssert.DoesNotContain("verdict.Famishing", heartbeat);

			string carry = TestMain.ReadRepositoryText(
				"Simulation/City/KingdomCity.z08.CarryAndReconcile.cs");
			string carryBody = carry.Substring(0,
				carry.IndexOf("private static KingdomCityState CarryKind", StringComparison.Ordinal));
			StringAssert.DoesNotContain("KingdomStockKind.Food", carryBody);
			StringAssert.DoesNotContain("RationsForElapsed", carryBody);
		}

		[Test]
		public void SharedMeal_ProvesKitchenAndSpendableCustodyBeforeExactDebit()
		{
			string source = TestMain.ReadRepositoryText("Growth/KingdomLarder.cs");
			int kitchen = source.IndexOf("KingdomBenefitCapabilities.Cooking",
				StringComparison.Ordinal);
			int custody = source.IndexOf("KingdomOrdinaryFoodAuthority.TryAvailable",
				StringComparison.Ordinal);
			int gate = source.IndexOf("CanHoldSharedMeal(available, System.Population, kitchens)",
				StringComparison.Ordinal);
			int debit = source.IndexOf("survey.ConsumeFood(cost, System.DishStaple",
				StringComparison.Ordinal);
			int exact = source.IndexOf("spent != cost", StringComparison.Ordinal);
			int benefit = source.IndexOf("KingdomCreed.EaseForMeal(System)",
				StringComparison.Ordinal);
			Assert.That(kitchen, Is.GreaterThanOrEqualTo(0));
			Assert.That(custody, Is.GreaterThan(kitchen));
			Assert.That(gate, Is.GreaterThan(custody));
			Assert.That(debit, Is.GreaterThan(gate));
			Assert.That(exact, Is.GreaterThan(debit));
			Assert.That(benefit, Is.GreaterThan(exact));
			StringAssert.Contains("MealIngredientsSpent", source);
			StringAssert.Contains("no meal benefit was granted", source);
		}
	}
}
#endif
