#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class LarderRulesTests
	{
		// --- ClassifyPantry: the ladder Status and the shared meal both read from -----------

		[TestCase(0, KingdomRules.PantryTier.Empty)]
		[TestCase(-5, KingdomRules.PantryTier.Empty)]
		[TestCase(1, KingdomRules.PantryTier.Scant)]
		[TestCase(9, KingdomRules.PantryTier.Scant)]
		[TestCase(10, KingdomRules.PantryTier.Modest)]
		[TestCase(29, KingdomRules.PantryTier.Modest)]
		[TestCase(30, KingdomRules.PantryTier.Ample)]
		[TestCase(1000, KingdomRules.PantryTier.Ample)]
		public void ClassifyPantry_MatchesTheDocumentedLadder(int foodCount, KingdomRules.PantryTier expected)
		{
			Assert.AreEqual(expected, KingdomRules.ClassifyPantry(foodCount));
		}

		[Test]
		public void ClassifyPantry_ThresholdsAreStrictlyIncreasing()
		{
			// A mutation that lets Modest's floor sink to or below Scant's, or Ample's to or
			// below Modest's, collapses the ladder into fewer than four reachable tiers.
			Assert.Less(KingdomRules.PantryScantThreshold, KingdomRules.PantryModestThreshold);
			Assert.Less(KingdomRules.PantryModestThreshold, KingdomRules.PantryAmpleThreshold);
		}

		[Test]
		public void PantryTierNames_HasOneLowercaseNamePerTier()
		{
			Assert.AreEqual(4, KingdomRules.PantryTierNames.Length);
			Assert.AreEqual("empty", KingdomRules.PantryTierNames[(int)KingdomRules.PantryTier.Empty]);
			Assert.AreEqual("scant", KingdomRules.PantryTierNames[(int)KingdomRules.PantryTier.Scant]);
			Assert.AreEqual("modest", KingdomRules.PantryTierNames[(int)KingdomRules.PantryTier.Modest]);
			Assert.AreEqual("ample", KingdomRules.PantryTierNames[(int)KingdomRules.PantryTier.Ample]);
			for (int i = 0; i < KingdomRules.PantryTierNames.Length; i++)
			{
				Assert.AreEqual(KingdomRules.PantryTierNames[i], KingdomRules.PantryTierNames[i].ToLowerInvariant(), "Qud style is lower-case object and state names");
			}
		}

		// --- MealCost: what a shared meal spends, per tier -----------------------------------

		[TestCase(KingdomRules.PantryTier.Empty, 0)]
		[TestCase(KingdomRules.PantryTier.Scant, KingdomRules.MealCostScant)]
		[TestCase(KingdomRules.PantryTier.Modest, KingdomRules.MealCostModest)]
		[TestCase(KingdomRules.PantryTier.Ample, KingdomRules.MealCostAmple)]
		public void MealCost_MatchesItsTier(KingdomRules.PantryTier tier, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.MealCost(tier));
		}

		[Test]
		public void MealCost_NeverAsksForMoreThanItsTierGuarantees()
		{
			// The invariant a shared meal depends on: every tier's cost fits inside the least
			// stock that tier can ever report, so a meal offered at a tier is always affordable
			// the instant that tier is reached, with no separate stock check required at the
			// call site.
			Assert.LessOrEqual(KingdomRules.MealCostScant, KingdomRules.PantryScantThreshold);
			Assert.LessOrEqual(KingdomRules.MealCostModest, KingdomRules.PantryModestThreshold);
			Assert.LessOrEqual(KingdomRules.MealCostAmple, KingdomRules.PantryAmpleThreshold);
		}

		[Test]
		public void MealCost_GrowsWithTheTier()
		{
			// A richer larder must never offer a cheaper meal - otherwise "choose the tier
			// honestly" inverts into a reason to keep the pantry thin.
			Assert.Less(KingdomRules.MealCost(KingdomRules.PantryTier.Scant), KingdomRules.MealCost(KingdomRules.PantryTier.Modest));
			Assert.Less(KingdomRules.MealCost(KingdomRules.PantryTier.Modest), KingdomRules.MealCost(KingdomRules.PantryTier.Ample));
		}

		// --- CanHoldSharedMeal: the one gate the Charter checks before offering the action ---

		[TestCase(0, 5, false)]
		[TestCase(1, 5, true)]
		[TestCase(9, 5, true)]
		[TestCase(30, 5, true)]
		[TestCase(30, 0, false)]
		[TestCase(0, 0, false)]
		[TestCase(-3, 5, false)]
		[TestCase(5, -1, false)]
		public void CanHoldSharedMeal_RequiresBothFoodAndSomeoneToFeed(int foodStored, int population, bool expected)
		{
			Assert.AreEqual(expected, KingdomRules.CanHoldSharedMeal(foodStored, population));
		}

		[TestCase(1, 1, 0, false)]
		[TestCase(1, 1, 1, true)]
		[TestCase(30, 5, 2, true)]
		[TestCase(0, 5, 1, false)]
		[TestCase(5, 0, 1, false)]
		public void CanHoldSharedMeal_RuntimeGateAlsoRequiresACapableKitchen(
			int foodStored, int population, int cookingProviders, bool expected)
		{
			Assert.AreEqual(expected,
				KingdomRules.CanHoldSharedMeal(foodStored, population, cookingProviders));
		}

		// --- MealServingsSpent: never more than what the larders actually hold ---------------

		[TestCase(0, 0)]
		[TestCase(1, 1)]
		[TestCase(9, 1)]
		[TestCase(10, 8)]
		[TestCase(17, 8)]
		[TestCase(29, 8)]
		[TestCase(30, 20)]
		[TestCase(1000, 20)]
		public void MealServingsSpent_MatchesTheTierCostExactly(int foodStored, int expected)
		{
			Assert.AreEqual(expected, KingdomRules.MealServingsSpent(foodStored));
		}

		[Test]
		public void MealServingsSpent_NeverExceedsWhatIsStored()
		{
			for (int food = 0; food <= 200; food++)
			{
				int spent = KingdomRules.MealServingsSpent(food);
				Assert.LessOrEqual(spent, food, "food=" + food + " spent more than the larders held");
				Assert.GreaterOrEqual(spent, 0, "food=" + food + " spent a negative amount");
			}
		}

		// --- MealSizeName / MealSpeech: the words never oversell the tier --------------------

		[Test]
		public void NothingIsNamedOrSpokenWhenThereIsNothingToServe()
		{
			Assert.IsNull(KingdomRules.MealSizeName(KingdomRules.PantryTier.Empty));
			Assert.IsNull(KingdomRules.MealSpeech(KingdomRules.PantryTier.Empty));
		}

		[TestCase(KingdomRules.PantryTier.Scant)]
		[TestCase(KingdomRules.PantryTier.Modest)]
		[TestCase(KingdomRules.PantryTier.Ample)]
		public void MealSizeNameAndSpeech_AreWordsWhenThereIsSomethingToServe(KingdomRules.PantryTier tier)
		{
			Assert.IsFalse(string.IsNullOrWhiteSpace(KingdomRules.MealSizeName(tier)), tier + " must name what is being served");
			Assert.IsFalse(string.IsNullOrWhiteSpace(KingdomRules.MealSpeech(tier)), tier + " must let the settler say something");
		}

		[Test]
		public void MealSizeName_IsDistinctPerTier()
		{
			// Every servable tier must read as its own size; two tiers sharing a name would make
			// "choose the tier honestly" a lie for whichever one silently borrowed the other's.
			string scant = KingdomRules.MealSizeName(KingdomRules.PantryTier.Scant);
			string modest = KingdomRules.MealSizeName(KingdomRules.PantryTier.Modest);
			string ample = KingdomRules.MealSizeName(KingdomRules.PantryTier.Ample);
			Assert.AreNotEqual(scant, modest);
			Assert.AreNotEqual(modest, ample);
			Assert.AreNotEqual(scant, ample);
		}

		[Test]
		public void MealSpeech_IsDistinctPerTier()
		{
			string scant = KingdomRules.MealSpeech(KingdomRules.PantryTier.Scant);
			string modest = KingdomRules.MealSpeech(KingdomRules.PantryTier.Modest);
			string ample = KingdomRules.MealSpeech(KingdomRules.PantryTier.Ample);
			Assert.AreNotEqual(scant, modest);
			Assert.AreNotEqual(modest, ample);
			Assert.AreNotEqual(scant, ample);
		}

		[Test]
		public void EveryServableTierHasBothANameAndACost()
		{
			// Ties MealCost, MealSizeName, and MealSpeech to the same definition of "servable"
			// (CanHoldSharedMeal_RequiresBothFoodAndSomeoneToFeed above pins that definition
			// against real stock numbers) so a new tier, or a re-ordered one, cannot silently
			// drift out of sync with the others.
			KingdomRules.PantryTier[] all = new KingdomRules.PantryTier[4]
			{
				KingdomRules.PantryTier.Empty,
				KingdomRules.PantryTier.Scant,
				KingdomRules.PantryTier.Modest,
				KingdomRules.PantryTier.Ample
			};
			for (int i = 0; i < all.Length; i++)
			{
				bool servable = all[i] != KingdomRules.PantryTier.Empty;
				Assert.AreEqual(servable, KingdomRules.MealCost(all[i]) > 0, all[i] + ": cost and servability disagree");
				Assert.AreEqual(servable, KingdomRules.MealSizeName(all[i]) != null, all[i] + ": name and servability disagree");
				Assert.AreEqual(servable, KingdomRules.MealSpeech(all[i]) != null, all[i] + ": speech and servability disagree");
			}
		}
	}
}
#endif
