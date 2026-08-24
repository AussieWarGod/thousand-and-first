#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Wave G3, Addendum 11(b): food is consumed for favoured MEALS, and by INDUSTRY.
	/// <para>
	/// Written against the specific mistakes this lane invites: a dish derivation that stops
	/// being total (a creed or a crop this build has never heard of must still get dinner), a
	/// meal verdict that lets a settlement with nowhere to cook claim it cooked, a scraps
	/// sentence that nags a camp for doing exactly what a camp does, and a mill whose conversion
	/// stops conserving &mdash; the last being the one that would quietly mint food forever.
	/// </para>
	/// </summary>
	public class KingdomMealRulesTests
	{
		// --- The dish: derived, total, and in Qud's own register ---------------------------

		[Test]
		public void DeriveDish_TakesItsFormFromTheCreedAndItsBodyFromTheGround()
		{
			// The two worked examples the wave is documented with. Joppa's own favourite dish is
			// AppleMatz (B/Factions.xml:154), so a realm whose people hold with Joppa binds its
			// harvest into matz; found that realm in a marsh and the matz is made of vinewafers.
			KingdomRules.FavoredDish marsh = KingdomRules.DeriveDish("Ptoh", "AppleMatz", "Vinewafer");
			Assert.AreEqual("vinewafer matz", marsh.Name);
			Assert.AreEqual("Vinewafer Sheaf", marsh.Staple);

			// The Barathrumites' is ThePorridge (:1179). Found on ordinary ground, the crop is
			// starapple, and the settlement is known for starapple porridge.
			KingdomRules.FavoredDish hill = KingdomRules.DeriveDish("Kesil", "ThePorridge", "Starapple");
			Assert.AreEqual("starapple porridge", hill.Name);
			Assert.AreEqual("Starapple Preserves", hill.Staple);
		}

		[Test]
		public void DeriveDish_IsTotal_BecauseEverySettlementEats()
		{
			// A creed this build has never heard of, a crop it has never heard of, and no realm
			// name at all. None of those is an error: people who hold with nobody still eat, and
			// what they eat is a stew.
			KingdomRules.FavoredDish nobody = KingdomRules.DeriveDish(null, null, null);
			Assert.AreEqual(KingdomRules.DefaultDishForm, nobody.Form);
			Assert.IsFalse(string.IsNullOrEmpty(nobody.Name));
			Assert.IsFalse(string.IsNullOrEmpty(nobody.Text));

			KingdomRules.FavoredDish stranger = KingdomRules.DeriveDish("Ptoh", "NoSuchRecipe", "Grit Gate Ration");
			Assert.AreEqual("grit gate ration stew", stranger.Name);
			Assert.IsNull(stranger.Staple, "a crop this build ships no staple for must say so rather than guess");
		}

		[Test]
		public void DeriveDish_IsDeterministic_SoASaveAndAReloadEatTheSameThing()
		{
			KingdomRules.FavoredDish first = KingdomRules.DeriveDish("Ptoh", "MahLahSoup", "Plump Mushroom");
			KingdomRules.FavoredDish again = KingdomRules.DeriveDish("Ptoh", "MahLahSoup", "Plump Mushroom");
			Assert.AreEqual(first.Name, again.Name);
			Assert.AreEqual(first.Text, again.Text);
			Assert.AreEqual(first.Staple, again.Staple);
			Assert.AreEqual(first.Source, again.Source);
		}

		[TestCase("AppleMatz", "matz")]
		[TestCase("MushroomCider", "compote")]
		[TestCase("GoatAndSweetLeaf", "roast")]
		[TestCase("TongueAndCheek", "brisket")]
		[TestCase("BoneBabka", "pastry")]
		[TestCase("HotandSpiny", "goulash")]
		[TestCase("MahLahSoup", "soup")]
		[TestCase("ThePorridge", "porridge")]
		[TestCase(null, "stew")]
		[TestCase("", "stew")]
		public void DishFormFor_CoversEveryVanillaFavouriteDishAndThenSome(string creedRecipe, string expected)
		{
			Assert.AreEqual(expected, KingdomRules.DishFormFor(creedRecipe));
		}

		[Test]
		public void DishForms_AreAllVanillaRecipeTileWords_WhichIsWhatGetsThemAPicture()
		{
			// CookingRecipe.ingredientTileTypes (D/XRL/World/Skills/Cooking/CookingRecipe.cs:44-51)
			// is what vanilla's own recipe-tile generator matches a name against. Every form this
			// mod can produce is one of those words, so a derived dish is drawn rather than
			// defaulted. A build that invents a prettier word must add it here and check it is
			// still on vanilla's list.
			string[] vanillaTileWords = new string[38]
			{
				"cake", "bread", "loaf", "slaw", "stew", "soup", "brisket", "borscht", "dip", "baklava",
				"compote", "hash", "porridge", "matz", "cookies", "yogurt", "goulash", "rice", "hummus", "knish",
				"broth", "kugel", "latkes", "schnitzel", "pancake", "roast", "shawarma", "flatbread", "meatballs", "pastry",
				"casserole", "dumpling", "doughnut", "tajine", "couscous", "dolma", "kebab", "fillet"
			};
			string[] creeds = new string[9]
			{
				"AppleMatz", "MushroomCider", "GoatAndSweetLeaf", "TongueAndCheek", "BoneBabka",
				"HotandSpiny", "MahLahSoup", "ThePorridge", null
			};
			foreach (string creed in creeds)
			{
				string form = KingdomRules.DishFormFor(creed);
				Assert.Contains(form, vanillaTileWords, "dish form '" + form + "' is not one of vanilla's own recipe tile words");
			}
		}

		[TestCase("Ptoh", "Ptoh's")]
		[TestCase("Ptohs", "Ptohs'")]
		[TestCase("", "")]
		[TestCase(null, "")]
		public void Possessive_WritesItTheWayQudDoes(string name, string expected)
		{
			Assert.AreEqual(expected, KingdomRules.Possessive(name));
		}

		[Test]
		public void DishText_IsVanillasOwnSentenceWithTheRealmInIt()
		{
			KingdomRules.FavoredDish dish = KingdomRules.DeriveDish("Ptoh", "AppleMatz", "Vinewafer");
			Assert.AreEqual("Would you teach me to cook Ptoh's favorite dish?", dish.Text);
		}

		[Test]
		public void PreservedStapleFor_AnswersForEveryCropThisModCanGrow()
		{
			// Every crop KingdomCropRules.CropBlueprintForStyle can return must have a staple, or
			// a settlement founded on that ground would raise a mill that grinds nothing and
			// carry a food number it cannot pay.
			string[] styles = new string[6] { "common", "verdant", "fungal", "gyre", "eater", "not-a-style" };
			foreach (string style in styles)
			{
				string crop = KingdomCropRules.CropBlueprintForStyle(style);
				Assert.IsFalse(string.IsNullOrEmpty(KingdomRules.PreservedStapleFor(crop)),
					"style '" + style + "' grows " + crop + ", which nothing can bind to keep");
			}
		}

		// --- What a day's eating was ------------------------------------------------------

		[Test]
		public void JudgeMeal_SaysNothingWhenNothingWasOwed()
		{
			Assert.AreEqual(KingdomRules.MealVerdict.None, KingdomRules.JudgeMeal(0, 0, 0, true, GrowthStage.City));
			Assert.AreEqual(KingdomRules.MealVerdict.None, KingdomRules.JudgeMeal(-3, 9, 9, true, GrowthStage.City));
		}

		[Test]
		public void JudgeMeal_NeedsAKitchen_BecauseASettlementWithNowhereToCookCannotCook()
		{
			// Same larder, same staple, same everything: the only difference is somewhere to put
			// a pot. A build that drops the kitchen term would let a settlement with no fire at
			// all claim it ate its own dish.
			Assert.AreEqual(KingdomRules.MealVerdict.Favored, KingdomRules.JudgeMeal(10, 10, 10, true, GrowthStage.Town));
			Assert.AreEqual(KingdomRules.MealVerdict.Plain, KingdomRules.JudgeMeal(10, 10, 10, false, GrowthStage.Town));
		}

		[Test]
		public void JudgeMeal_WantsHalfTheDayOffTheStaple()
		{
			Assert.AreEqual(KingdomRules.FavoredMealPercent, 50, "the share below is written against this number");
			Assert.AreEqual(KingdomRules.MealVerdict.Favored, KingdomRules.JudgeMeal(10, 5, 10, true, GrowthStage.Town));
			Assert.AreEqual(KingdomRules.MealVerdict.Plain, KingdomRules.JudgeMeal(10, 4, 10, true, GrowthStage.Town));
			// And a crumb is not a dish.
			Assert.AreEqual(KingdomRules.MealVerdict.Plain, KingdomRules.JudgeMeal(100, 1, 100, true, GrowthStage.Town));
		}

		[Test]
		public void JudgeMeal_DoesNotNagACampForLivingOffTheLand()
		{
			// A camp that eats what its hands find is a camp working exactly as designed
			// (KingdomRules.ForagedRations), and 7b must never nag a founder about a system
			// working. The same reading at a Village is worth saying once.
			Assert.AreEqual(KingdomRules.MealVerdict.Plain, KingdomRules.JudgeMeal(4, 0, 0, true, GrowthStage.Camp));
			Assert.AreEqual(KingdomRules.MealVerdict.Plain, KingdomRules.JudgeMeal(4, 0, 0, true, GrowthStage.Steading));
			Assert.AreEqual(KingdomRules.MealVerdict.Scraps, KingdomRules.JudgeMeal(20, 0, 0, true, GrowthStage.Village));
			Assert.AreEqual(KingdomRules.MealVerdict.Scraps, KingdomRules.JudgeMeal(40, 0, 0, true, GrowthStage.City));
		}

		[Test]
		public void JudgeMeal_IsPlainWhenTheLardersGaveSomethingButNotTheDish()
		{
			Assert.AreEqual(KingdomRules.MealVerdict.Plain, KingdomRules.JudgeMeal(20, 0, 20, true, GrowthStage.City));
		}

		[Test]
		public void MealShade_IsWorthOneSettlerAndOnlyForAFavouredMeal()
		{
			Assert.AreEqual(KingdomRules.FavoredMealShade, KingdomRules.MealShadeFor(KingdomRules.MealVerdict.Favored));
			Assert.AreEqual(0, KingdomRules.MealShadeFor(KingdomRules.MealVerdict.Plain));
			Assert.AreEqual(0, KingdomRules.MealShadeFor(KingdomRules.MealVerdict.Scraps));
			Assert.AreEqual(0, KingdomRules.MealShadeFor(KingdomRules.MealVerdict.None));
			// Never a penalty, at any reading. The brief rejects the penalty half outright, and
			// KingdomCatalogueRules.Equilibrium floors each half before they meet precisely so a
			// shade cannot cancel a shrine that is standing.
			foreach (KingdomRules.MealVerdict verdict in Enum.GetValues(typeof(KingdomRules.MealVerdict)))
			{
				Assert.GreaterOrEqual(KingdomRules.MealShadeFor(verdict), 0);
			}
		}

		[Test]
		public void MealShade_StaysSmallEnoughThatNobodyEatsTheirWayPastTheirOwnWater()
		{
			// The lift cap binds the meal shade with everything else riding the lift term. At the
			// floor the whole cap is two settlers, so a camp cannot dine its way anywhere.
			int cap = KingdomCatalogueRules.FloorLevel * KingdomCatalogueRules.LiftCapPercent / 100;
			Assert.LessOrEqual(KingdomRules.FavoredMealShade, cap,
				"a single meal must never be able to saturate the lift cap on its own");
		}

		[Test]
		public void MealNotes_SayTheDishByNameOrSayNothing()
		{
			Assert.IsNull(KingdomRules.FavoredMealNote("Ptoh", null),
				"a lift with no dish to name is a modifier, and a sentence not worth writing");
			string note = KingdomRules.FavoredMealNote("Ptoh", "vinewafer matz");
			StringAssert.Contains("vinewafer matz", note);
			StringAssert.Contains("Ptoh", note);
			StringAssert.Contains("Ptoh", KingdomRules.ScrapsNote("Ptoh"));
		}

		[Test]
		public void DishStatusLine_MakesTheIngredientKitchenAndOneDayBonusInspectable()
		{
			string line = KingdomRules.DishStatusLine("vinewafer matz", "Vinewafer Sheaf", 7,
				1, KingdomRules.MealVerdict.Favored);
			StringAssert.Contains("vinewafer matz", line);
			StringAssert.Contains("Vinewafer Sheaf: 7 stored", line);
			StringAssert.Contains("kitchen ready", line);
			StringAssert.Contains("at least half", line);
			StringAssert.Contains("carries +1 today", line);
		}

		[Test]
		public void DishStatusLine_DoesNotPromiseABonusBeforeTheChainActuallyPays()
		{
			string plain = KingdomRules.DishStatusLine("starapple porridge",
				"Starapple Preserves", -4, 0, KingdomRules.MealVerdict.Plain);
			StringAssert.Contains("0 stored", plain);
			StringAssert.Contains("no kitchen", plain);
			StringAssert.Contains("no dish bonus", plain);
			Assert.IsNull(KingdomRules.DishStatusLine(null, "Vinewafer Sheaf", 20, 1,
				KingdomRules.MealVerdict.Favored));
		}

		[Test]
		public void DishStatusLine_DistinguishesEveryResolvedTable()
		{
			string none = KingdomRules.DishStatusLine("mushroom soup", "Pickled Mushrooms", 3,
				1, KingdomRules.MealVerdict.None);
			string scraps = KingdomRules.DishStatusLine("mushroom soup", "Pickled Mushrooms", 0,
				1, KingdomRules.MealVerdict.Scraps);
			string plain = KingdomRules.DishStatusLine("mushroom soup", "Pickled Mushrooms", 3,
				1, KingdomRules.MealVerdict.Plain);
			string favored = KingdomRules.DishStatusLine("mushroom soup", "Pickled Mushrooms", 3,
				1, KingdomRules.MealVerdict.Favored);
			CollectionAssert.AllItemsAreUnique(new string[4] { none, scraps, plain, favored });
		}

		// --- Industry: the mill conserves --------------------------------------------------

		[Test]
		public void TheMillConserves_OutIsInTimesTheMultiple()
		{
			// The one invariant that keeps a mill from being a food printer. Whatever the batch,
			// what comes back is exactly what went in times PreserveMultiple, and the GAIN is the
			// difference - never a figure arrived at any other way.
			for (int crops = 0; crops <= 12; crops++)
			{
				int outp = crops * KingdomRules.PreserveMultiple;
				Assert.AreEqual(outp - crops, KingdomRules.MilledGain(crops),
					"the mill's gain must be what came back less what went in");
			}
		}

		[Test]
		public void TheMillsDayIsExactlyWhatTheGrindingMillDeclares()
		{
			// Two crops in, six staples back, a net of four - which is the grinding mill's
			// Carries="food:4". _notes/balance-sim.py asserts the same identity against the
			// catalogue XML itself; this asserts the arithmetic side of it, so a retune of either
			// constant is caught in the suite as well as in the model.
			Assert.AreEqual(4, KingdomRules.MilledGain(KingdomRules.MillCropsPerDay));
			Assert.AreEqual(3, KingdomRules.PreserveMultiple, "vanilla's own Vinewafer -> Vinewafer Sheaf figure");
		}

		[Test]
		public void CropsForGain_IsTheInverseAndNeverQuietlyShort()
		{
			Assert.AreEqual(0, KingdomRules.CropsForGain(0));
			Assert.AreEqual(0, KingdomRules.CropsForGain(-5));
			for (int gain = 1; gain <= 30; gain++)
			{
				int crops = KingdomRules.CropsForGain(gain);
				Assert.GreaterOrEqual(KingdomRules.MilledGain(crops), gain,
					"grinding " + crops + " crops must cover a gain of " + gain);
				Assert.Less(KingdomRules.MilledGain(crops - 1), gain,
					"and one fewer crop must not, or the mill is grinding more than it was asked for");
			}
		}

		[Test]
		public void TheMillNeverEatsTheDayTheResidentsHaveNotEatenYet()
		{
			// The reserve is a whole day's rations for everybody living here, kept back on top of
			// the pass already having drawn the day's rations first. A build that drops it would
			// let a well-stocked settlement wake up hungry because its mill was busy.
			Assert.AreEqual(0, KingdomRules.MillableStock(10, 10));
			Assert.AreEqual(0, KingdomRules.MillableStock(3, 10));
			Assert.AreEqual(5, KingdomRules.MillableStock(15, 10));
			Assert.AreEqual(0, KingdomRules.MillableStock(0, 0));
			for (int stored = 0; stored <= 40; stored++)
			{
				for (int pop = 0; pop <= 20; pop++)
				{
					int free = KingdomRules.MillableStock(stored, pop);
					Assert.GreaterOrEqual(free, 0);
					Assert.LessOrEqual(free + KingdomRules.RationsPerDay(pop), Math.Max(stored, KingdomRules.RationsPerDay(pop)),
						"the mill may never be offered more than the larders hold above tomorrow's bill");
				}
			}
		}
	}
}
#endif
