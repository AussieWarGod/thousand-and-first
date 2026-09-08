#if TAF_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomCeremonyRulesTests
	{
		[Test]
		public void CeremonyAbiKeepsAuthorityAndNestedPatternDtosExact()
		{
			Type authority = typeof(KingdomCeremonyRules);
			ClassicAssert.AreEqual("ThousandAndFirst.KingdomCeremonyRules", authority.FullName);
			ClassicAssert.IsTrue(authority.IsPublic && authority.IsAbstract && authority.IsSealed);

			AssertNestedFields(typeof(KingdomCeremonyRules.BuildingKnowledge),
				"ThousandAndFirst.KingdomCeremonyRules+BuildingKnowledge",
				new[] { "Key", "Knowledge", "Label" });
			AssertNestedFields(typeof(KingdomCeremonyRules.ForeignDesign),
				"ThousandAndFirst.KingdomCeremonyRules+ForeignDesign",
				new[] { "BuildingKey", "LearnName", "Label" });

			KingdomCeremonyRules.BuildingKnowledge knowledge = new KingdomCeremonyRules.BuildingKnowledge();
			ClassicAssert.IsNull(knowledge.Key);
			ClassicAssert.IsNull(knowledge.Knowledge);
			ClassicAssert.IsNull(knowledge.Label);
			KingdomCeremonyRules.ForeignDesign design = new KingdomCeremonyRules.ForeignDesign();
			ClassicAssert.IsNull(design.BuildingKey);
			ClassicAssert.IsNull(design.LearnName);
			ClassicAssert.IsNull(design.Label);

			FieldInfo categories = authority.GetField("TasteCategories",
				BindingFlags.Public | BindingFlags.Static);
			ClassicAssert.IsNotNull(categories);
			ClassicAssert.AreEqual(typeof(string[]), categories.FieldType);
			ClassicAssert.IsTrue(categories.IsInitOnly);
			CollectionAssert.AreEqual(new[]
			{
				"food", "storage", "civic", "craft", "power", "faith", "memorial",
				"housing", "defense", "knowledge"
			}, KingdomCeremonyRules.TasteCategories);
		}

		[Test]
		public void LogicalSourceKeepsOneOrderedPartialAuthorityAndNestedDtos()
		{
			string source = LogicalSource();
			ClassicAssert.AreEqual(5, Count(source, "public static partial class KingdomCeremonyRules"));
			ClassicAssert.AreEqual(1, Count(source, "public sealed class BuildingKnowledge"));
			ClassicAssert.AreEqual(1, Count(source, "public sealed class ForeignDesign"));
			ClassicAssert.Less(source.IndexOf("public static string SurveyorsPlanText", StringComparison.Ordinal),
				source.IndexOf("public static bool IsAttended", StringComparison.Ordinal));
			ClassicAssert.Less(source.IndexOf("public static bool IsAttended", StringComparison.Ordinal),
				source.IndexOf("public static List<int> ChooseTastes", StringComparison.Ordinal));
			ClassicAssert.Less(source.IndexOf("public static List<int> ChooseTastes", StringComparison.Ordinal),
				source.IndexOf("public static void ChooseLeaderTraits", StringComparison.Ordinal));
			ClassicAssert.Less(source.IndexOf("public static void ChooseLeaderTraits", StringComparison.Ordinal),
				source.IndexOf("public static List<ForeignDesign> ForeignDesigns", StringComparison.Ordinal));
		}

		// --- SurveyorsPlanText: one template per family, tier and material slots, honest fallback

		[TestCase("food", "table")]
		[TestCase("storage", "keeping-place")]
		[TestCase("civic", "gathering-ground")]
		[TestCase("craft", "working-floor")]
		[TestCase("power", "engine-house")]
		[TestCase("faith", "quiet room")]
		[TestCase("memorial", "remembering-place")]
		[TestCase("housing", "roof")]
		[TestCase("defense", "standing wall")]
		[TestCase("knowledge", "keeping of what is known")]
		public void SurveyorsPlanText_EachKnownFamilyHasItsOwnDistinctTemplate(string category, string expectedFragment)
		{
			string text = KingdomCeremonyRules.SurveyorsPlanText(category, "the granary", GrowthStage.Steading, null);
			ClassicAssert.IsTrue(text.Contains(expectedFragment), "expected '" + expectedFragment + "' in: " + text);
			ClassicAssert.IsTrue(text.Contains("the granary"));
		}

		[Test]
		public void SurveyorsPlanText_UnknownCategoryFallsBackToPlainStakesNeverFiller()
		{
			string text = KingdomCeremonyRules.SurveyorsPlanText("a-third-party-category-nobody-wrote", "the odd house", GrowthStage.Camp, "marble");
			ClassicAssert.AreEqual("The plan for the odd house is staked: plain stakes in the ground, and nothing more written yet.", text);
		}

		[Test]
		public void SurveyorsPlanText_CategoryIsCaseInsensitive()
		{
			string lower = KingdomCeremonyRules.SurveyorsPlanText("food", "hall", GrowthStage.Camp, null);
			string upper = KingdomCeremonyRules.SurveyorsPlanText("FOOD", "hall", GrowthStage.Camp, null);
			ClassicAssert.AreEqual(lower, upper);
		}

		[Test]
		public void SurveyorsPlanText_MissingBuildingNameFallsBackToTheWork()
		{
			ClassicAssert.IsTrue(KingdomCeremonyRules.SurveyorsPlanText("civic", null, GrowthStage.Camp, null).Contains("the work"));
			ClassicAssert.IsTrue(KingdomCeremonyRules.SurveyorsPlanText("civic", "", GrowthStage.Camp, null).Contains("the work"));
		}

		[Test]
		public void SurveyorsPlanText_MissingMaterialFallsBackToPlainStockNeverBlank()
		{
			string text = KingdomCeremonyRules.SurveyorsPlanText("housing", "the hut", GrowthStage.Camp, null);
			ClassicAssert.IsTrue(text.Contains("plain stock"));
		}

		[Test]
		public void SurveyorsPlanText_GivenMaterialIsCarriedVerbatim()
		{
			string text = KingdomCeremonyRules.SurveyorsPlanText("housing", "the hut", GrowthStage.Camp, "marble");
			ClassicAssert.IsTrue(text.Contains("marble"));
			ClassicAssert.IsFalse(text.Contains("plain stock"));
		}

		[TestCase(GrowthStage.Camp, "a camp's")]
		[TestCase(GrowthStage.Steading, "a steading's")]
		[TestCase(GrowthStage.Village, "a village's")]
		[TestCase(GrowthStage.Town, "a town's")]
		[TestCase(GrowthStage.City, "a city's")]
		public void SurveyorsPlanText_TierSlotNamesEachStageDistinctly(GrowthStage tier, string expectedFragment)
		{
			string text = KingdomCeremonyRules.SurveyorsPlanText("civic", "the hall", tier, null);
			ClassicAssert.IsTrue(text.Contains(expectedFragment), "expected '" + expectedFragment + "' in: " + text);
		}

		[Test]
		public void SurveyorsPlanText_AllTenKnownFamiliesProduceDistinctText()
		{
			string[] categories = KingdomCeremonyRules.TasteCategories;
			HashSet<string> seen = new HashSet<string>();
			foreach (string category in categories)
			{
				string text = KingdomCeremonyRules.SurveyorsPlanText(category, "the work", GrowthStage.Camp, null);
				ClassicAssert.IsTrue(seen.Add(text), "category '" + category + "' duplicated another family's template");
			}
		}

		// --- IsAttended: the exact day-grace boundary --------------------------------------------

		[TestCase(1000L, 1000L, true)]
		[TestCase(1000L, 1000L + KingdomRules.TicksPerDay - 1L, true)]
		[TestCase(1000L, 1000L + KingdomRules.TicksPerDay, false)]
		[TestCase(1000L, 1000L + KingdomRules.TicksPerDay * 10L, false)]
		[TestCase(1000L, 900L, true)]
		public void IsAttended_FollowsTheOneDayGraceBoundaryExactly(long completeTick, long nowTicks, bool expected)
		{
			ClassicAssert.AreEqual(expected, KingdomCeremonyRules.IsAttended(completeTick, nowTicks));
		}

		// --- Raising ceremony prose ---------------------------------------------------------------

		[Test]
		public void RaisingAttendedChronicle_NoOneFoundStillSharesTheWater()
		{
			string text = KingdomCeremonyRules.RaisingAttendedChronicle("granary", "Nivvun Ut", new List<string>(), null);
			ClassicAssert.IsTrue(text.Contains("the water shared"));
			ClassicAssert.IsFalse(text.Contains("standing by"));
		}

		[Test]
		public void RaisingAttendedChronicle_OnePresentIsNamedAlone()
		{
			string text = KingdomCeremonyRules.RaisingAttendedChronicle("granary", "Nivvun Ut", new List<string> { "Aeru" }, null);
			ClassicAssert.IsTrue(text.Contains("with Aeru standing by"));
		}

		[Test]
		public void RaisingAttendedChronicle_TwoPresentAreJoinedWithAnd()
		{
			string text = KingdomCeremonyRules.RaisingAttendedChronicle("granary", "Nivvun Ut", new List<string> { "Aeru", "Voss" }, null);
			ClassicAssert.IsTrue(text.Contains("Aeru and Voss"));
		}

		[Test]
		public void RaisingAttendedChronicle_ThreeOrMorePresentNameTwoAndOthers()
		{
			string text = KingdomCeremonyRules.RaisingAttendedChronicle("granary", "Nivvun Ut", new List<string> { "Aeru", "Voss", "Kest" }, null);
			ClassicAssert.IsTrue(text.Contains("Aeru, Voss, and others"));
			ClassicAssert.IsFalse(text.Contains("Kest"));
		}

		[Test]
		public void RaisingAttendedChronicle_QuotesThePlanWhenGiven()
		{
			string text = KingdomCeremonyRules.RaisingAttendedChronicle("granary", "Nivvun Ut", null, "The plan for the granary is staked.");
			ClassicAssert.IsTrue(text.Contains("true to the plan staked there: \"The plan for the granary is staked.\""));
		}

		[Test]
		public void RaisingAttendedChronicle_OmitsTheQuoteClauseWhenNoPlanWasStaked()
		{
			string text = KingdomCeremonyRules.RaisingAttendedChronicle("granary", "Nivvun Ut", null, null);
			ClassicAssert.IsFalse(text.Contains("true to the plan"));
		}

		[Test]
		public void RaisingUnattendedChronicle_NeverNamesCrewAndStillQuotesThePlan()
		{
			string withPlan = KingdomCeremonyRules.RaisingUnattendedChronicle("granary", "Nivvun Ut", "quoted text");
			ClassicAssert.IsTrue(withPlan.Contains("before anyone came home to see it"));
			ClassicAssert.IsTrue(withPlan.Contains("\"quoted text\""));
			string withoutPlan = KingdomCeremonyRules.RaisingUnattendedChronicle("granary", "Nivvun Ut", null);
			ClassicAssert.IsFalse(withoutPlan.Contains("true to the plan"));
		}

		[Test]
		public void RaisingLedgerNote_NamesTheBuildingAndFlagsItAsWhileAway()
		{
			string note = KingdomCeremonyRules.RaisingLedgerNote("granary");
			ClassicAssert.IsTrue(note.Contains("granary"));
			ClassicAssert.IsTrue(note.Contains("while you were away"));
		}

		[Test]
		public void RaisingAttendedMessage_DiffersWithAndWithoutPresentCrew()
		{
			string alone = KingdomCeremonyRules.RaisingAttendedMessage("granary", new List<string>());
			string withCrew = KingdomCeremonyRules.RaisingAttendedMessage("granary", new List<string> { "Aeru" });
			ClassicAssert.AreNotEqual(alone, withCrew);
			ClassicAssert.IsTrue(withCrew.Contains("Aeru"));
		}

		// --- Notable tastes -------------------------------------------------------------------

		[Test]
		public void TasteLine_MetAndUnmetReadDifferently()
		{
			string met = KingdomCeremonyRules.TasteLine(0, true);
			string unmet = KingdomCeremonyRules.TasteLine(0, false);
			ClassicAssert.AreNotEqual(met, unmet);
			ClassicAssert.IsTrue(met.Contains("finds it here already"));
			ClassicAssert.IsTrue(unmet.Contains("has not found it here yet"));
		}

		[Test]
		public void TasteLine_UnmetIsNeverPhrasedAsAComplaint()
		{
			// TasteIndex 1 ("storage") is deliberately picked over 0: its own statement text
			// happens to be free of "never"/"fail" words the met/default suffix must also avoid.
			string unmet = KingdomCeremonyRules.TasteLine(1, false).ToLowerInvariant();
			ClassicAssert.IsFalse(unmet.Contains("never"));
			ClassicAssert.IsFalse(unmet.Contains("fail"));
			ClassicAssert.IsFalse(unmet.Contains("penalt"));
		}

		[Test]
		public void TasteLine_OutOfRangeIndexClampsRatherThanThrowing()
		{
			Assert.DoesNotThrow(delegate { KingdomCeremonyRules.TasteLine(-1, true); });
			Assert.DoesNotThrow(delegate { KingdomCeremonyRules.TasteLine(999, true); });
		}

		[Test]
		public void TasteChronicle_OneTasteReadsSingularAndTwoReadsPlural()
		{
			string one = KingdomCeremonyRules.TasteChronicle("Aeru", new List<int> { 0 }, new List<bool> { true });
			string two = KingdomCeremonyRules.TasteChronicle("Aeru", new List<int> { 0, 1 }, new List<bool> { true, false });
			ClassicAssert.IsTrue(one.Contains("states a taste"));
			ClassicAssert.IsTrue(two.Contains("states two tastes"));
		}

		[Test]
		public void TasteChronicle_NamesTheHolder()
		{
			string text = KingdomCeremonyRules.TasteChronicle("Aeru", new List<int> { 0 }, new List<bool> { false });
			ClassicAssert.IsTrue(text.StartsWith("Aeru"));
		}

		[Test]
		public void TasteShade_EmptyIsZero()
		{
			ClassicAssert.AreEqual(0, KingdomCeremonyRules.TasteShade(new List<bool>()));
		}

		[Test]
		public void TasteShade_OneUnmetIsZero()
		{
			ClassicAssert.AreEqual(0, KingdomCeremonyRules.TasteShade(new List<bool> { false }));
		}

		[Test]
		public void TasteShade_OneMetIsOneShadeUnit()
		{
			ClassicAssert.AreEqual(KingdomCeremonyRules.TasteShadeAmount, KingdomCeremonyRules.TasteShade(new List<bool> { true }));
		}

		[Test]
		public void TasteShade_TwoMetIsTwoShadeUnits()
		{
			ClassicAssert.AreEqual(KingdomCeremonyRules.TasteShadeAmount * 2, KingdomCeremonyRules.TasteShade(new List<bool> { true, true }));
		}

		[Test]
		public void TasteShade_MixedCountsOnlyTheMetOne()
		{
			ClassicAssert.AreEqual(KingdomCeremonyRules.TasteShadeAmount, KingdomCeremonyRules.TasteShade(new List<bool> { true, false }));
		}

		[Test]
		public void TasteShade_NullMetIsZeroNotAThrow()
		{
			ClassicAssert.AreEqual(0, KingdomCeremonyRules.TasteShade(null));
		}

		// --- Addendum 4 re-basing: a taste is a tag in the shared vocabulary -------------------

		[Test]
		public void TasteTag_IsTheCategoryInTheSharedNamespace()
		{
			ClassicAssert.AreEqual(KingdomQolRules.Namespace + "food", KingdomCeremonyRules.TasteTag(0));
		}

		[Test]
		public void TasteTag_OutOfRangeFallsBackToIndexZeroLikeEveryOtherTasteAccessor()
		{
			ClassicAssert.AreEqual(KingdomCeremonyRules.TasteTag(0), KingdomCeremonyRules.TasteTag(-1));
			ClassicAssert.AreEqual(KingdomCeremonyRules.TasteTag(0), KingdomCeremonyRules.TasteTag(999));
		}

		[Test]
		public void CategoryTag_AndTasteTag_ProduceTheSameStringForTheSameCategory()
		{
			// The whole point of one vocabulary rather than two: what a notable wants and what a
			// building offers are the SAME token, so the shared match engine can compare them.
			for (int i = 0; i < KingdomCeremonyRules.TasteCategories.Length; i++)
			{
				ClassicAssert.AreEqual(KingdomCeremonyRules.TasteTag(i),
					KingdomCeremonyRules.CategoryTag(KingdomCeremonyRules.TasteCategories[i]),
					"taste " + KingdomCeremonyRules.TasteCategories[i] + " and its category do not name the same tag");
			}
		}

		[Test]
		public void CategoryTag_FoldsCaseAndWhitespace()
		{
			ClassicAssert.AreEqual(KingdomCeremonyRules.CategoryTag("food"), KingdomCeremonyRules.CategoryTag("  FOOD  "));
		}

		[Test]
		public void CategoryTag_NoCategoryAtAllOffersNothing()
		{
			ClassicAssert.IsNull(KingdomCeremonyRules.CategoryTag(null));
			ClassicAssert.IsNull(KingdomCeremonyRules.CategoryTag(""));
			ClassicAssert.IsNull(KingdomCeremonyRules.CategoryTag("   "));
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(7)]
		[TestCase(9)]
		public void TastesMet_EveryTasteIsMetByItsOwnCategoryStandingThere(int tasteIndex)
		{
			string[] offer = new string[1] { KingdomCeremonyRules.CategoryTag(KingdomCeremonyRules.TasteCategories[tasteIndex]) };
			List<bool> met = KingdomCeremonyRules.TastesMet(new List<int> { tasteIndex }, offer);
			ClassicAssert.AreEqual(1, met.Count);
			ClassicAssert.IsTrue(met[0], "taste " + KingdomCeremonyRules.TasteCategories[tasteIndex] + " was not met by its own category");
		}

		[Test]
		public void TastesMet_ADifferentCategoryStandingThereMeetsNothing()
		{
			string[] offer = new string[1] { KingdomCeremonyRules.CategoryTag("storage") };
			ClassicAssert.IsFalse(KingdomCeremonyRules.TastesMet(new List<int> { 0 }, offer)[0], "a granary is not a table");
		}

		[Test]
		public void TastesMet_ASettlementWithNothingStandingMeetsNothing()
		{
			ClassicAssert.IsFalse(KingdomCeremonyRules.TastesMet(new List<int> { 0 }, null)[0]);
			ClassicAssert.IsFalse(KingdomCeremonyRules.TastesMet(new List<int> { 0 }, new string[0])[0]);
		}

		[Test]
		public void TastesMet_KeepsTheOrderStatedSoTheChronicleAndTheShadeReadTheSameList()
		{
			string[] offer = new string[1] { KingdomCeremonyRules.CategoryTag("housing") };
			List<bool> met = KingdomCeremonyRules.TastesMet(new List<int> { 0, 7 }, offer);
			ClassicAssert.AreEqual(2, met.Count);
			ClassicAssert.IsFalse(met[0], "food is not met");
			ClassicAssert.IsTrue(met[1], "housing is");
		}

		[Test]
		public void TastesMet_NoTastesStatedIsAnEmptyListAndNeverNull()
		{
			ClassicAssert.AreEqual(0, KingdomCeremonyRules.TastesMet(null, new string[0]).Count);
			ClassicAssert.AreEqual(0, KingdomCeremonyRules.TastesMet(new List<int>(), new string[0]).Count);
		}

		[Test]
		public void TheRebasingLeavesTheShadeExactlyWhereItWas()
		{
			// The re-basing renames how "is this met" is asked and nothing else: a met taste is
			// still worth TasteShadeAmount, and two of them still worth two.
			string[] offer = new string[2]
			{
				KingdomCeremonyRules.CategoryTag("food"),
				KingdomCeremonyRules.CategoryTag("housing")
			};
			ClassicAssert.AreEqual(2 * KingdomCeremonyRules.TasteShadeAmount,
				KingdomCeremonyRules.TasteShade(KingdomCeremonyRules.TastesMet(new List<int> { 0, 7 }, offer)));
			ClassicAssert.AreEqual(0, KingdomCeremonyRules.TasteShade(KingdomCeremonyRules.TastesMet(new List<int> { 0, 7 }, new string[0])));
		}

		[Test]
		public void ChooseTastes_IsDeterministicForTheSameSettlementAndOrdinal()
		{
			List<int> first = KingdomCeremonyRules.ChooseTastes("taf:settlement:example", 4200uL);
			List<int> second = KingdomCeremonyRules.ChooseTastes("taf:settlement:example", 4200uL);
			CollectionAssert.AreEqual(first, second);
		}

		[Test]
		public void ChooseTastes_AlwaysReturnsOneOrTwoDistinctInBoundsIndices()
		{
			for (ulong ordinal = 0uL; ordinal < 40uL; ordinal++)
			{
				List<int> tastes = KingdomCeremonyRules.ChooseTastes("taf:settlement:sweep", ordinal);
				ClassicAssert.IsTrue(tastes.Count == 1 || tastes.Count == 2, "count was " + tastes.Count);
				foreach (int index in tastes)
				{
					ClassicAssert.GreaterOrEqual(index, 0);
					ClassicAssert.Less(index, KingdomCeremonyRules.TasteCategories.Length);
				}
				if (tastes.Count == 2)
				{
					ClassicAssert.AreNotEqual(tastes[0], tastes[1]);
				}
			}
		}

		[Test]
		public void ChooseTastes_InvalidSettlementIdFallsBackToASingleFixedTaste()
		{
			List<int> tastes = KingdomCeremonyRules.ChooseTastes("", 0uL);
			CollectionAssert.AreEqual(new List<int> { 0 }, tastes);
		}

		// --- Leader traits -------------------------------------------------------------------

		[Test]
		public void ChooseLeaderTraits_IsDeterministicForTheSameSettlementAndOrdinal()
		{
			int virtueA, flawA, virtueB, flawB;
			KingdomCeremonyRules.ChooseLeaderTraits("taf:settlement:example", 900uL, out virtueA, out flawA);
			KingdomCeremonyRules.ChooseLeaderTraits("taf:settlement:example", 900uL, out virtueB, out flawB);
			ClassicAssert.AreEqual(virtueA, virtueB);
			ClassicAssert.AreEqual(flawA, flawB);
		}

		[Test]
		public void ChooseLeaderTraits_AlwaysInBoundsAcrossASweepOfOrdinals()
		{
			for (ulong ordinal = 0uL; ordinal < 40uL; ordinal++)
			{
				int virtue, flaw;
				KingdomCeremonyRules.ChooseLeaderTraits("taf:settlement:sweep", ordinal, out virtue, out flaw);
				ClassicAssert.GreaterOrEqual(virtue, 0);
				ClassicAssert.GreaterOrEqual(flaw, 0);
				ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomCeremonyRules.VirtueText(virtue)));
				ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomCeremonyRules.FlawText(flaw)));
			}
		}

		[Test]
		public void ChooseLeaderTraits_InvalidSettlementIdFallsBackToIndexZeroForBoth()
		{
			int virtue, flaw;
			KingdomCeremonyRules.ChooseLeaderTraits("", 0uL, out virtue, out flaw);
			ClassicAssert.AreEqual(0, virtue);
			ClassicAssert.AreEqual(0, flaw);
		}

		[Test]
		public void VirtueText_AndFlawText_ClampOutOfRangeRatherThanThrow()
		{
			Assert.DoesNotThrow(delegate { KingdomCeremonyRules.VirtueText(-1); });
			Assert.DoesNotThrow(delegate { KingdomCeremonyRules.VirtueText(999); });
			Assert.DoesNotThrow(delegate { KingdomCeremonyRules.FlawText(-1); });
			Assert.DoesNotThrow(delegate { KingdomCeremonyRules.FlawText(999); });
		}

		[Test]
		public void LeaderTraitChronicle_NeverOmitsTheFlawEvenThoughItIsNamedAfterTheVirtue()
		{
			string text = KingdomCeremonyRules.LeaderTraitChronicle("the water-keeper", "Aeru", "Nivvun Ut", 0, 0);
			ClassicAssert.IsTrue(text.Contains("Aeru"));
			ClassicAssert.IsTrue(text.Contains("the water-keeper"));
			ClassicAssert.IsTrue(text.Contains("Nivvun Ut"));
			ClassicAssert.IsTrue(text.Contains(KingdomCeremonyRules.VirtueText(0)));
			ClassicAssert.IsTrue(text.Contains(KingdomCeremonyRules.FlawText(0)));
			ClassicAssert.IsTrue(text.Contains(" -- but "));
		}

		[Test]
		public void LeaderShade_IsNetPositiveAndSmall()
		{
			int shade = KingdomCeremonyRules.LeaderShade();
			ClassicAssert.AreEqual(KingdomCeremonyRules.VirtueShadeAmount - KingdomCeremonyRules.FlawShadeAmount, shade);
			ClassicAssert.Greater(shade, 0);
			ClassicAssert.LessOrEqual(shade, 3);
		}

		// --- The pattern-book: candidate filtering ---------------------------------------------

		[Test]
		public void ForeignDesigns_OnlyOffersEntriesGatedOnAnUnsatisfiedPatternToken()
		{
			List<KingdomCeremonyRules.BuildingKnowledge> entries = new List<KingdomCeremonyRules.BuildingKnowledge>
			{
				new KingdomCeremonyRules.BuildingKnowledge { Key = "r_YdRoofline", Knowledge = "pattern:yd-freehold" },
				new KingdomCeremonyRules.BuildingKnowledge { Key = "r_OrdinaryHut", Knowledge = null },
				new KingdomCeremonyRules.BuildingKnowledge { Key = "r_GatedByDistrict", Knowledge = "districts:market" },
				new KingdomCeremonyRules.BuildingKnowledge { Key = "r_GatedByMachine", Knowledge = "machine:solar condenser" }
			};
			List<KingdomCeremonyRules.ForeignDesign> found = KingdomCeremonyRules.ForeignDesigns(entries, new List<string>());
			ClassicAssert.AreEqual(1, found.Count);
			ClassicAssert.AreEqual("r_YdRoofline", found[0].BuildingKey);
			ClassicAssert.AreEqual("yd-freehold", found[0].LearnName);
		}

		[Test]
		public void ForeignDesigns_ExcludesADesignAlreadyLearnedThroughTheRoster()
		{
			List<KingdomCeremonyRules.BuildingKnowledge> entries = new List<KingdomCeremonyRules.BuildingKnowledge>
			{
				new KingdomCeremonyRules.BuildingKnowledge { Key = "r_YdRoofline", Knowledge = "pattern:yd-freehold" }
			};
			List<string> roster = new List<string> { "pattern:yd-freehold" };
			ClassicAssert.AreEqual(0, KingdomCeremonyRules.ForeignDesigns(entries, roster).Count);
		}

		[Test]
		public void ForeignDesigns_ADiskCannotSatisfyAPatternRequirement()
		{
			// Knows() only lets an unqualified requirement match any kind; a "pattern:" token is
			// qualified, so only a roster entry of kind "pattern" may ever satisfy it.
			List<KingdomCeremonyRules.BuildingKnowledge> entries = new List<KingdomCeremonyRules.BuildingKnowledge>
			{
				new KingdomCeremonyRules.BuildingKnowledge { Key = "r_YdRoofline", Knowledge = "pattern:yd-freehold" }
			};
			List<string> roster = new List<string> { "disk:yd-freehold" };
			ClassicAssert.AreEqual(1, KingdomCeremonyRules.ForeignDesigns(entries, roster).Count);
		}

		[Test]
		public void ForeignDesigns_DeduplicatesTheSamePatternNameAcrossTwoEntries()
		{
			List<KingdomCeremonyRules.BuildingKnowledge> entries = new List<KingdomCeremonyRules.BuildingKnowledge>
			{
				new KingdomCeremonyRules.BuildingKnowledge { Key = "r_First", Knowledge = "pattern:hindren-weave-hall" },
				new KingdomCeremonyRules.BuildingKnowledge { Key = "r_Second", Knowledge = "pattern:hindren-weave-hall" }
			};
			ClassicAssert.AreEqual(1, KingdomCeremonyRules.ForeignDesigns(entries, new List<string>()).Count);
		}

		[Test]
		public void ForeignDesigns_SortsDeterministicallyByLearnName()
		{
			List<KingdomCeremonyRules.BuildingKnowledge> entries = new List<KingdomCeremonyRules.BuildingKnowledge>
			{
				new KingdomCeremonyRules.BuildingKnowledge { Key = "r_Z", Knowledge = "pattern:zebra" },
				new KingdomCeremonyRules.BuildingKnowledge { Key = "r_A", Knowledge = "pattern:apple" }
			};
			List<KingdomCeremonyRules.ForeignDesign> found = KingdomCeremonyRules.ForeignDesigns(entries, new List<string>());
			ClassicAssert.AreEqual("apple", found[0].LearnName);
			ClassicAssert.AreEqual("zebra", found[1].LearnName);
		}

		[Test]
		public void ForeignDesigns_EmptyOrNullEntriesYieldsNoCandidates()
		{
			ClassicAssert.AreEqual(0, KingdomCeremonyRules.ForeignDesigns(null, new List<string>()).Count);
			ClassicAssert.AreEqual(0, KingdomCeremonyRules.ForeignDesigns(new List<KingdomCeremonyRules.BuildingKnowledge>(), new List<string>()).Count);
		}

		// --- The pattern-book: the draws themselves --------------------------------------------

		[Test]
		public void ShouldOfferPattern_IsDeterministicForTheSameSettlementAndOrdinal()
		{
			bool first = KingdomCeremonyRules.ShouldOfferPattern("taf:settlement:example", 77uL);
			bool second = KingdomCeremonyRules.ShouldOfferPattern("taf:settlement:example", 77uL);
			ClassicAssert.AreEqual(first, second);
		}

		[Test]
		public void ShouldOfferPattern_InvalidSettlementIdFailsClosed()
		{
			ClassicAssert.IsFalse(KingdomCeremonyRules.ShouldOfferPattern("", 0uL));
		}

		[Test]
		public void ShouldOfferPattern_RollsBelowTheChanceOverASweepOfOrdinals()
		{
			int offered = 0;
			const int trials = 500;
			for (ulong ordinal = 0uL; ordinal < trials; ordinal++)
			{
				if (KingdomCeremonyRules.ShouldOfferPattern("taf:settlement:sweep", ordinal))
				{
					offered++;
				}
			}
			// Not an exact binomial check (that would be flaky); just confirms the draw is neither
			// always-on nor always-off, which a mutated "return true"/"return false" would produce.
			ClassicAssert.Greater(offered, 0);
			ClassicAssert.Less(offered, trials);
		}

		[Test]
		public void PickPatternIndex_AlwaysWithinBoundsAcrossASweep()
		{
			for (int remaining = 1; remaining <= 5; remaining++)
			{
				for (int step = 0; step < 3; step++)
				{
					int index = KingdomCeremonyRules.PickPatternIndex("taf:settlement:sweep", 12uL, step, remaining);
					ClassicAssert.GreaterOrEqual(index, 0);
					ClassicAssert.Less(index, remaining);
				}
			}
		}

		[Test]
		public void PickPatternIndex_ZeroOrNegativeRemainingReturnsZeroWithoutThrowing()
		{
			ClassicAssert.AreEqual(0, KingdomCeremonyRules.PickPatternIndex("taf:settlement:sweep", 12uL, 0, 0));
			ClassicAssert.AreEqual(0, KingdomCeremonyRules.PickPatternIndex("taf:settlement:sweep", 12uL, 0, -3));
		}

		[Test]
		public void PickPatternIndex_InvalidSettlementIdFallsBackToZero()
		{
			ClassicAssert.AreEqual(0, KingdomCeremonyRules.PickPatternIndex("", 0uL, 0, 3));
		}

		// ==================================================================================
		// Historical notable scoring helpers retained for narrative/tool compatibility.
		// ==================================================================================

		[Test]
		public void HistoricalNotableScoreCombinesTastesLeaderAndMetPrefers()
		{
			// This proves old chronicle/tool vocabulary remains deterministic. Live capacity never
			// reads this score; civic-office authority is title-only.
			ClassicAssert.AreEqual((2 * KingdomCeremonyRules.TasteShadeAmount) + KingdomCeremonyRules.LeaderShade() + 2,
				KingdomCeremonyRules.NotableShade(new List<bool> { true, true }, 2));
		}

		[Test]
		public void HistoricalNotableScoreStillIncludesNarrativeVirtue()
		{
			// Unmet tastes are not a penalty (the brief rejects the penalty half outright), so a
			// notable who found nothing here is still worth their virtue net of their flaw.
			ClassicAssert.AreEqual(KingdomCeremonyRules.LeaderShade(),
				KingdomCeremonyRules.NotableShade(new List<bool> { false, false }, 0));
			ClassicAssert.AreEqual(KingdomCeremonyRules.LeaderShade(), KingdomCeremonyRules.NotableShade(null, 0));
		}

		[Test]
		public void HistoricalNotableScoreTreatsNegativePreferenceAsNone()
		{
			ClassicAssert.AreEqual(KingdomCeremonyRules.LeaderShade(), KingdomCeremonyRules.NotableShade(null, -9));
		}

		[Test]
		public void HistoricalNotableScoreNeverExceedsItsVocabularyCeiling()
		{
			ClassicAssert.AreEqual(KingdomCeremonyRules.MaxNotableShade,
				KingdomCeremonyRules.NotableShade(new List<bool> { true, true, true, true }, 99));
			ClassicAssert.AreEqual(5, KingdomCeremonyRules.MaxNotableShade,
				"two tastes, a virtue net of a flaw, and two Prefers: texture, not a lever");
		}

		[Test]
		public void HistoricalScoreCeilingAgreesWithTheWidestTasteDraw()
		{
			// A ceiling that disagreed with the draw would be a ceiling nothing reached.
			List<int> widest = new List<int>();
			for (ulong ordinal = 1uL; ordinal < 40uL && widest.Count < KingdomCeremonyRules.MaxTastesStated; ordinal++)
			{
				List<int> drawn = KingdomCeremonyRules.ChooseTastes("taf:settlement:sweep", ordinal);
				ClassicAssert.LessOrEqual(drawn.Count, KingdomCeremonyRules.MaxTastesStated,
					"no notable may state more tastes than the ceiling counts");
				if (drawn.Count > widest.Count)
				{
					widest = drawn;
				}
			}
			ClassicAssert.AreEqual(KingdomCeremonyRules.MaxTastesStated, widest.Count,
				"the draw must be able to reach the ceiling, or the ceiling is fiction");
		}

		[Test]
		public void HistoricalShadeClauseSaysNothingForZero()
		{
			ClassicAssert.AreEqual("", KingdomCeremonyRules.ShadeClause(0));
			ClassicAssert.AreEqual("", KingdomCeremonyRules.ShadeClause(-2));
		}

		[Test]
		public void HistoricalShadeClauseNamesItsRetiredScoreForTooling()
		{
			string clause = KingdomCeremonyRules.ShadeClause(3);
			StringAssert.Contains("+3", clause);
			StringAssert.Contains("notable", clause);
		}

		private static void AssertNestedFields(Type type, string fullName, string[] names)
		{
			ClassicAssert.AreEqual(fullName, type.FullName);
			ClassicAssert.IsTrue(type.IsNestedPublic);
			ClassicAssert.AreEqual(typeof(KingdomCeremonyRules), type.DeclaringType);
			FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
			Array.Sort(fields, (a, b) => a.MetadataToken.CompareTo(b.MetadataToken));
			CollectionAssert.AreEqual(names, Array.ConvertAll(fields, field => field.Name));
			CollectionAssert.AreEqual(new[] { typeof(string), typeof(string), typeof(string) },
				Array.ConvertAll(fields, field => field.FieldType));
			foreach (FieldInfo field in fields) ClassicAssert.IsFalse(field.IsInitOnly, field.Name);
		}

		private static string LogicalSource()
		{
			return string.Join("\n", new[]
			{
				TestMain.ReadRepositoryText(Path.Combine("Experience", "KingdomCeremonyRules.cs")),
				TestMain.ReadRepositoryText(Path.Combine("Experience", "KingdomCeremonyRules.Raising.cs")),
				TestMain.ReadRepositoryText(Path.Combine("Experience", "KingdomCeremonyRules.Tastes.cs")),
				TestMain.ReadRepositoryText(Path.Combine("Experience", "KingdomCeremonyRules.LeaderAndShade.cs")),
				TestMain.ReadRepositoryText(Path.Combine("Experience", "KingdomCeremonyRules.PatternBook.cs"))
			});
		}

		private static int Count(string source, string term)
		{
			int count = 0;
			int at = 0;
			while ((at = source.IndexOf(term, at, StringComparison.Ordinal)) >= 0)
			{
				count++;
				at += term.Length;
			}
			return count;
		}
	}
}
#endif
