#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomSalvageRulesTests
	{
		// --- ComputeWaterCost: what an inspection of this Complexity costs -------------------

		[TestCase(-5, KingdomSalvageRules.SalvageBaseWaterCost)]
		[TestCase(-1, KingdomSalvageRules.SalvageBaseWaterCost)]
		[TestCase(0, KingdomSalvageRules.SalvageBaseWaterCost)]
		[TestCase(1, 20)]
		[TestCase(5, 40)]
		[TestCase(8, 55)]
		[TestCase(10, 65)]
		public void ComputeWaterCost_MatchesTheFormula(int complexity, int expected)
		{
			Assert.AreEqual(expected, KingdomSalvageRules.ComputeWaterCost(complexity));
		}

		[Test]
		public void ComputeWaterCost_NeverNegativeAndNeverDecreasesWithComplexity()
		{
			// A mutation that lets a higher Complexity ever cost less would let a founder game
			// the price by exaggerating a machine's own difficulty rating.
			int previous = KingdomSalvageRules.ComputeWaterCost(0);
			Assert.GreaterOrEqual(previous, 0);
			for (int complexity = 1; complexity <= 50; complexity++)
			{
				int cost = KingdomSalvageRules.ComputeWaterCost(complexity);
				Assert.GreaterOrEqual(cost, previous, "complexity=" + complexity);
				previous = cost;
			}
		}

		// --- ComputeHandsRequired: settlers an inspection of this Difficulty needs free ------

		[TestCase(-3, KingdomSalvageRules.SalvageBaseHandsRequired)]
		[TestCase(0, KingdomSalvageRules.SalvageBaseHandsRequired)]
		[TestCase(1, 2)]
		[TestCase(2, 3)]
		[TestCase(3, 3)]
		[TestCase(4, 4)]
		[TestCase(10, 7)]
		public void ComputeHandsRequired_MatchesTheFormula(int difficulty, int expected)
		{
			Assert.AreEqual(expected, KingdomSalvageRules.ComputeHandsRequired(difficulty));
		}

		[Test]
		public void ComputeHandsRequired_NeverNegativeAndNeverDecreasesWithDifficulty()
		{
			int previous = KingdomSalvageRules.ComputeHandsRequired(0);
			Assert.GreaterOrEqual(previous, 0);
			for (int difficulty = 1; difficulty <= 50; difficulty++)
			{
				int hands = KingdomSalvageRules.ComputeHandsRequired(difficulty);
				Assert.GreaterOrEqual(hands, previous, "difficulty=" + difficulty);
				previous = hands;
			}
		}

		[Test]
		public void SalvageConstants_AreAllPositive()
		{
			// A zeroed constant here would make certification free, or make every machine
			// certifiable regardless of who is standing in the settlement.
			Assert.Greater(KingdomSalvageRules.SalvageBaseWaterCost, 0);
			Assert.Greater(KingdomSalvageRules.SalvageWaterPerComplexity, 0);
			Assert.Greater(KingdomSalvageRules.SalvageBaseHandsRequired, 0);
			Assert.Greater(KingdomSalvageRules.SalvageDifficultyPerHand, 0);
		}

		// --- Assess: the refusal cases that protect the player's stores and settlers ---------

		[TestCase(true, true, true, false, 0, 0, 0, 0, KingdomSalvageRules.SalvageVerdict.RefusedHazardous)]
		[TestCase(false, true, true, false, 0, 0, 0, 0, KingdomSalvageRules.SalvageVerdict.RefusedBroken)]
		[TestCase(false, false, true, false, 0, 0, 0, 0, KingdomSalvageRules.SalvageVerdict.RefusedRusted)]
		[TestCase(false, false, false, false, 0, 0, 1000, 100, KingdomSalvageRules.SalvageVerdict.RefusedNotUnderstood)]
		[TestCase(false, false, false, true, 0, 0, 0, 0, KingdomSalvageRules.SalvageVerdict.RefusedCannotAfford)]
		[TestCase(false, false, false, true, 0, 0, 15, 0, KingdomSalvageRules.SalvageVerdict.RefusedNoHands)]
		[TestCase(false, false, false, true, 0, 0, 15, 2, KingdomSalvageRules.SalvageVerdict.Certified)]
		public void Assess_ChecksInProtectiveOrder(bool hazardous, bool broken, bool rusted, bool understood, int complexity, int difficulty, int storedWater, int population, KingdomSalvageRules.SalvageVerdict expected)
		{
			KingdomSalvageRules.SalvageVerdict verdict = KingdomSalvageRules.Assess(hazardous, broken, rusted, understood, complexity, difficulty, storedWater, population, out _, out _);
			Assert.AreEqual(expected, verdict);
		}

		[Test]
		public void Assess_NeverCertifiesWhileAnyProtectiveFlagIsSet()
		{
			// Every non-zero combination of hazardous/broken/rusted, with everything else
			// (understanding, water, hands) as favourable as it can be, must still refuse.
			// Catches a dropped check or an inverted condition no single-flag test would.
			for (int mask = 1; mask < 8; mask++)
			{
				bool hazardous = (mask & 1) != 0;
				bool broken = (mask & 2) != 0;
				bool rusted = (mask & 4) != 0;
				KingdomSalvageRules.SalvageVerdict verdict = KingdomSalvageRules.Assess(hazardous, broken, rusted, true, 0, 0, 1000, 1000, out _, out _);
				Assert.AreNotEqual(KingdomSalvageRules.SalvageVerdict.Certified, verdict, "mask=" + mask);
			}
		}

		[Test]
		public void Assess_ExactStoredWaterAndHandsCertify()
		{
			// The boundary CanHoldSharedMeal already pins for food: a tier's offer must be
			// affordable the instant it is reached, not one dram or one settler later.
			int cost = KingdomSalvageRules.ComputeWaterCost(3);
			int hands = KingdomSalvageRules.ComputeHandsRequired(2);
			KingdomSalvageRules.SalvageVerdict verdict = KingdomSalvageRules.Assess(false, false, false, true, 3, 2, cost, hands, out int waterCost, out int handsRequired);
			Assert.AreEqual(KingdomSalvageRules.SalvageVerdict.Certified, verdict);
			Assert.AreEqual(cost, waterCost);
			Assert.AreEqual(hands, handsRequired);
		}

		[Test]
		public void Assess_OneDramShortOfCostRefusesOnAffordability()
		{
			int cost = KingdomSalvageRules.ComputeWaterCost(3);
			int hands = KingdomSalvageRules.ComputeHandsRequired(2);
			KingdomSalvageRules.SalvageVerdict verdict = KingdomSalvageRules.Assess(false, false, false, true, 3, 2, cost - 1, hands, out _, out _);
			Assert.AreEqual(KingdomSalvageRules.SalvageVerdict.RefusedCannotAfford, verdict);
		}

		[Test]
		public void Assess_OneHandShortRefusesOnHands()
		{
			int cost = KingdomSalvageRules.ComputeWaterCost(3);
			int hands = KingdomSalvageRules.ComputeHandsRequired(2);
			KingdomSalvageRules.SalvageVerdict verdict = KingdomSalvageRules.Assess(false, false, false, true, 3, 2, cost, hands - 1, out _, out _);
			Assert.AreEqual(KingdomSalvageRules.SalvageVerdict.RefusedNoHands, verdict);
		}

		[TestCase(5, 3)]
		[TestCase(0, 0)]
		[TestCase(10, 10)]
		public void Assess_AlwaysDisclosesCostEvenWhenRefusedForDanger(int complexity, int difficulty)
		{
			// The founder is told the price even when the machine is refused for being
			// dangerous or unknown - "disclosed before the founder commits" holds for every
			// refusal, not just the affordable-but-declined ones.
			KingdomSalvageRules.Assess(true, false, false, false, complexity, difficulty, 0, 0, out int waterCost, out int handsRequired);
			Assert.AreEqual(KingdomSalvageRules.ComputeWaterCost(complexity), waterCost);
			Assert.AreEqual(KingdomSalvageRules.ComputeHandsRequired(difficulty), handsRequired);
		}

		// --- IsRefusal / IsRetryable: what the Charter uses to decide how to answer ----------

		[Test]
		public void IsRefusal_TrueForEveryVerdictExceptCertified()
		{
			foreach (KingdomSalvageRules.SalvageVerdict verdict in Enum.GetValues(typeof(KingdomSalvageRules.SalvageVerdict)))
			{
				bool expected = verdict != KingdomSalvageRules.SalvageVerdict.Certified;
				Assert.AreEqual(expected, KingdomSalvageRules.IsRefusal(verdict), verdict.ToString());
			}
		}

		[Test]
		public void IsRetryable_TrueOnlyForAffordabilityRefusals()
		{
			// A mutation that marks a hazard, fault, or understanding refusal as retryable
			// would tell a founder "come back later" about a machine that can never pass
			// without the founder actually doing something about it first.
			foreach (KingdomSalvageRules.SalvageVerdict verdict in Enum.GetValues(typeof(KingdomSalvageRules.SalvageVerdict)))
			{
				bool expected = verdict == KingdomSalvageRules.SalvageVerdict.RefusedCannotAfford || verdict == KingdomSalvageRules.SalvageVerdict.RefusedNoHands;
				Assert.AreEqual(expected, KingdomSalvageRules.IsRetryable(verdict), verdict.ToString());
			}
		}
	}
}
#endif
