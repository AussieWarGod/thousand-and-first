#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomCeremonyRulesTests
	{
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
			Assert.IsTrue(text.Contains(expectedFragment), "expected '" + expectedFragment + "' in: " + text);
			Assert.IsTrue(text.Contains("the granary"));
		}

		[Test]
		public void SurveyorsPlanText_UnknownCategoryFallsBackToPlainStakesNeverFiller()
		{
			string text = KingdomCeremonyRules.SurveyorsPlanText("a-third-party-category-nobody-wrote", "the odd house", GrowthStage.Camp, "marble");
			Assert.AreEqual("The plan for the odd house is staked: plain stakes in the ground, and nothing more written yet.", text);
		}

		[Test]
		public void SurveyorsPlanText_CategoryIsCaseInsensitive()
		{
			string lower = KingdomCeremonyRules.SurveyorsPlanText("food", "hall", GrowthStage.Camp, null);
			string upper = KingdomCeremonyRules.SurveyorsPlanText("FOOD", "hall", GrowthStage.Camp, null);
			Assert.AreEqual(lower, upper);
		}

		[Test]
		public void SurveyorsPlanText_MissingBuildingNameFallsBackToTheWork()
		{
			Assert.IsTrue(KingdomCeremonyRules.SurveyorsPlanText("civic", null, GrowthStage.Camp, null).Contains("the work"));
			Assert.IsTrue(KingdomCeremonyRules.SurveyorsPlanText("civic", "", GrowthStage.Camp, null).Contains("the work"));
		}

		[Test]
		public void SurveyorsPlanText_MissingMaterialFallsBackToPlainStockNeverBlank()
		{
			string text = KingdomCeremonyRules.SurveyorsPlanText("housing", "the hut", GrowthStage.Camp, null);
			Assert.IsTrue(text.Contains("plain stock"));
		}

		[Test]
		public void SurveyorsPlanText_GivenMaterialIsCarriedVerbatim()
		{
			string text = KingdomCeremonyRules.SurveyorsPlanText("housing", "the hut", GrowthStage.Camp, "marble");
			Assert.IsTrue(text.Contains("marble"));
			Assert.IsFalse(text.Contains("plain stock"));
		}

		[TestCase(GrowthStage.Camp, "a camp's")]
		[TestCase(GrowthStage.Steading, "a steading's")]
		[TestCase(GrowthStage.Village, "a village's")]
		[TestCase(GrowthStage.Town, "a town's")]
		[TestCase(GrowthStage.City, "a city's")]
		public void SurveyorsPlanText_TierSlotNamesEachStageDistinctly(GrowthStage tier, string expectedFragment)
		{
			string text = KingdomCeremonyRules.SurveyorsPlanText("civic", "the hall", tier, null);
			Assert.IsTrue(text.Contains(expectedFragment), "expected '" + expectedFragment + "' in: " + text);
		}

		[Test]
		public void SurveyorsPlanText_AllTenKnownFamiliesProduceDistinctText()
		{
			string[] categories = KingdomCeremonyRules.TasteCategories;
			HashSet<string> seen = new HashSet<string>();
			foreach (string category in categories)
			{
				string text = KingdomCeremonyRules.SurveyorsPlanText(category, "the work", GrowthStage.Camp, null);
				Assert.IsTrue(seen.Add(text), "category '" + category + "' duplicated another family's template");
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
			Assert.AreEqual(expected, KingdomCeremonyRules.IsAttended(completeTick, nowTicks));
		}

		// --- Raising ceremony prose ---------------------------------------------------------------

		[Test]
		public void RaisingAttendedChronicle_NoOneFoundStillSharesTheWater()
		{
			string text = KingdomCeremonyRules.RaisingAttendedChronicle("granary", "Nivvun Ut", new List<string>(), null);
			Assert.IsTrue(text.Contains("the water shared"));
			Assert.IsFalse(text.Contains("standing by"));
		}

		[Test]
		public void RaisingAttendedChronicle_OnePresentIsNamedAlone()
		{
			string text = KingdomCeremonyRules.RaisingAttendedChronicle("granary", "Nivvun Ut", new List<string> { "Aeru" }, null);
			Assert.IsTrue(text.Contains("with Aeru standing by"));
		}

		[Test]
		public void RaisingAttendedChronicle_TwoPresentAreJoinedWithAnd()
		{
			string text = KingdomCeremonyRules.RaisingAttendedChronicle("granary", "Nivvun Ut", new List<string> { "Aeru", "Voss" }, null);
			Assert.IsTrue(text.Contains("Aeru and Voss"));
		}

		[Test]
		public void RaisingAttendedChronicle_ThreeOrMorePresentNameTwoAndOthers()
		{
			string text = KingdomCeremonyRules.RaisingAttendedChronicle("granary", "Nivvun Ut", new List<string> { "Aeru", "Voss", "Kest" }, null);
			Assert.IsTrue(text.Contains("Aeru, Voss, and others"));
			Assert.IsFalse(text.Contains("Kest"));
		}

		[Test]
		public void RaisingAttendedChronicle_QuotesThePlanWhenGiven()
		{
			string text = KingdomCeremonyRules.RaisingAttendedChronicle("granary", "Nivvun Ut", null, "The plan for the granary is staked.");
			Assert.IsTrue(text.Contains("true to the plan staked there: \"The plan for the granary is staked.\""));
		}

		[Test]
		public void RaisingAttendedChronicle_OmitsTheQuoteClauseWhenNoPlanWasStaked()
		{
			string text = KingdomCeremonyRules.RaisingAttendedChronicle("granary", "Nivvun Ut", null, null);
			Assert.IsFalse(text.Contains("true to the plan"));
		}

		[Test]
		public void RaisingUnattendedChronicle_NeverNamesCrewAndStillQuotesThePlan()
		{
			string withPlan = KingdomCeremonyRules.RaisingUnattendedChronicle("granary", "Nivvun Ut", "quoted text");
			Assert.IsTrue(withPlan.Contains("before anyone came home to see it"));
			Assert.IsTrue(withPlan.Contains("\"quoted text\""));
			string withoutPlan = KingdomCeremonyRules.RaisingUnattendedChronicle("granary", "Nivvun Ut", null);
			Assert.IsFalse(withoutPlan.Contains("true to the plan"));
		}

		[Test]
		public void RaisingLedgerNote_NamesTheBuildingAndFlagsItAsWhileAway()
		{
			string note = KingdomCeremonyRules.RaisingLedgerNote("granary");
			Assert.IsTrue(note.Contains("granary"));
			Assert.IsTrue(note.Contains("while you were away"));
		}

		[Test]
		public void RaisingAttendedMessage_DiffersWithAndWithoutPresentCrew()
		{
			string alone = KingdomCeremonyRules.RaisingAttendedMessage("granary", new List<string>());
			string withCrew = KingdomCeremonyRules.RaisingAttendedMessage("granary", new List<string> { "Aeru" });
			Assert.AreNotEqual(alone, withCrew);
			Assert.IsTrue(withCrew.Contains("Aeru"));
		}

		// --- Notable tastes -------------------------------------------------------------------

		[Test]
		public void TasteLine_MetAndUnmetReadDifferently()
		{
			string met = KingdomCeremonyRules.TasteLine(0, true);
			string unmet = KingdomCeremonyRules.TasteLine(0, false);
			Assert.AreNotEqual(met, unmet);
			Assert.IsTrue(met.Contains("finds it here already"));
			Assert.IsTrue(unmet.Contains("has not found it here yet"));
		}

		[Test]
		public void TasteLine_UnmetIsNeverPhrasedAsAComplaint()
		{
			// TasteIndex 1 ("storage") is deliberately picked over 0: its own statement text
			// happens to be free of "never"/"fail" words the met/default suffix must also avoid.
			string unmet = KingdomCeremonyRules.TasteLine(1, false).ToLowerInvariant();
			Assert.IsFalse(unmet.Contains("never"));
			Assert.IsFalse(unmet.Contains("fail"));
			Assert.IsFalse(unmet.Contains("penalt"));
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
			Assert.IsTrue(one.Contains("states a taste"));
			Assert.IsTrue(two.Contains("states two tastes"));
		}

		[Test]
		public void TasteChronicle_NamesTheHolder()
		{
			string text = KingdomCeremonyRules.TasteChronicle("Aeru", new List<int> { 0 }, new List<bool> { false });
			Assert.IsTrue(text.StartsWith("Aeru"));
		}

		[Test]
		public void TasteShade_EmptyIsZero()
		{
			Assert.AreEqual(0, KingdomCeremonyRules.TasteShade(new List<bool>()));
		}

		[Test]
		public void TasteShade_OneUnmetIsZero()
		{
			Assert.AreEqual(0, KingdomCeremonyRules.TasteShade(new List<bool> { false }));
		}

		[Test]
		public void TasteShade_OneMetIsOneShadeUnit()
		{
			Assert.AreEqual(KingdomCeremonyRules.TasteShadeAmount, KingdomCeremonyRules.TasteShade(new List<bool> { true }));
		}

		[Test]
		public void TasteShade_TwoMetIsTwoShadeUnits()
		{
			Assert.AreEqual(KingdomCeremonyRules.TasteShadeAmount * 2, KingdomCeremonyRules.TasteShade(new List<bool> { true, true }));
		}

		[Test]
		public void TasteShade_MixedCountsOnlyTheMetOne()
		{
			Assert.AreEqual(KingdomCeremonyRules.TasteShadeAmount, KingdomCeremonyRules.TasteShade(new List<bool> { true, false }));
		}

		[Test]
		public void TasteShade_NullMetIsZeroNotAThrow()
		{
			Assert.AreEqual(0, KingdomCeremonyRules.TasteShade(null));
		}

		// --- Addendum 4 re-basing: a taste is a tag in the shared vocabulary -------------------

		[Test]
		public void TasteTag_IsTheCategoryInTheSharedNamespace()
		{
			Assert.AreEqual(KingdomQolRules.Namespace + "food", KingdomCeremonyRules.TasteTag(0));
		}

		[Test]
		public void TasteTag_OutOfRangeFallsBackToIndexZeroLikeEveryOtherTasteAccessor()
		{
			Assert.AreEqual(KingdomCeremonyRules.TasteTag(0), KingdomCeremonyRules.TasteTag(-1));
			Assert.AreEqual(KingdomCeremonyRules.TasteTag(0), KingdomCeremonyRules.TasteTag(999));
		}

		[Test]
		public void CategoryTag_AndTasteTag_ProduceTheSameStringForTheSameCategory()
		{
			// The whole point of one vocabulary rather than two: what a notable wants and what a
			// building offers are the SAME token, so the shared match engine can compare them.
			for (int i = 0; i < KingdomCeremonyRules.TasteCategories.Length; i++)
			{
				Assert.AreEqual(KingdomCeremonyRules.TasteTag(i),
					KingdomCeremonyRules.CategoryTag(KingdomCeremonyRules.TasteCategories[i]),
					"taste " + KingdomCeremonyRules.TasteCategories[i] + " and its category do not name the same tag");
			}
		}

		[Test]
		public void CategoryTag_FoldsCaseAndWhitespace()
		{
			Assert.AreEqual(KingdomCeremonyRules.CategoryTag("food"), KingdomCeremonyRules.CategoryTag("  FOOD  "));
		}

		[Test]
		public void CategoryTag_NoCategoryAtAllOffersNothing()
		{
			Assert.IsNull(KingdomCeremonyRules.CategoryTag(null));
			Assert.IsNull(KingdomCeremonyRules.CategoryTag(""));
			Assert.IsNull(KingdomCeremonyRules.CategoryTag("   "));
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(7)]
		[TestCase(9)]
		public void TastesMet_EveryTasteIsMetByItsOwnCategoryStandingThere(int tasteIndex)
		{
			string[] offer = new string[1] { KingdomCeremonyRules.CategoryTag(KingdomCeremonyRules.TasteCategories[tasteIndex]) };
			List<bool> met = KingdomCeremonyRules.TastesMet(new List<int> { tasteIndex }, offer);
			Assert.AreEqual(1, met.Count);
			Assert.IsTrue(met[0], "taste " + KingdomCeremonyRules.TasteCategories[tasteIndex] + " was not met by its own category");
		}

		[Test]
		public void TastesMet_ADifferentCategoryStandingThereMeetsNothing()
		{
			string[] offer = new string[1] { KingdomCeremonyRules.CategoryTag("storage") };
			Assert.IsFalse(KingdomCeremonyRules.TastesMet(new List<int> { 0 }, offer)[0], "a granary is not a table");
		}

		[Test]
		public void TastesMet_ASettlementWithNothingStandingMeetsNothing()
		{
			Assert.IsFalse(KingdomCeremonyRules.TastesMet(new List<int> { 0 }, null)[0]);
			Assert.IsFalse(KingdomCeremonyRules.TastesMet(new List<int> { 0 }, new string[0])[0]);
		}

		[Test]
		public void TastesMet_KeepsTheOrderStatedSoTheChronicleAndTheShadeReadTheSameList()
		{
			string[] offer = new string[1] { KingdomCeremonyRules.CategoryTag("housing") };
			List<bool> met = KingdomCeremonyRules.TastesMet(new List<int> { 0, 7 }, offer);
			Assert.AreEqual(2, met.Count);
			Assert.IsFalse(met[0], "food is not met");
			Assert.IsTrue(met[1], "housing is");
		}

		[Test]
		public void TastesMet_NoTastesStatedIsAnEmptyListAndNeverNull()
		{
			Assert.AreEqual(0, KingdomCeremonyRules.TastesMet(null, new string[0]).Count);
			Assert.AreEqual(0, KingdomCeremonyRules.TastesMet(new List<int>(), new string[0]).Count);
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
			Assert.AreEqual(2 * KingdomCeremonyRules.TasteShadeAmount,
				KingdomCeremonyRules.TasteShade(KingdomCeremonyRules.TastesMet(new List<int> { 0, 7 }, offer)));
			Assert.AreEqual(0, KingdomCeremonyRules.TasteShade(KingdomCeremonyRules.TastesMet(new List<int> { 0, 7 }, new string[0])));
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
				Assert.IsTrue(tastes.Count == 1 || tastes.Count == 2, "count was " + tastes.Count);
				foreach (int index in tastes)
				{
					Assert.GreaterOrEqual(index, 0);
					Assert.Less(index, KingdomCeremonyRules.TasteCategories.Length);
				}
				if (tastes.Count == 2)
				{
					Assert.AreNotEqual(tastes[0], tastes[1]);
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
			Assert.AreEqual(virtueA, virtueB);
			Assert.AreEqual(flawA, flawB);
		}

		[Test]
		public void ChooseLeaderTraits_AlwaysInBoundsAcrossASweepOfOrdinals()
		{
			for (ulong ordinal = 0uL; ordinal < 40uL; ordinal++)
			{
				int virtue, flaw;
				KingdomCeremonyRules.ChooseLeaderTraits("taf:settlement:sweep", ordinal, out virtue, out flaw);
				Assert.GreaterOrEqual(virtue, 0);
				Assert.GreaterOrEqual(flaw, 0);
				Assert.IsFalse(string.IsNullOrEmpty(KingdomCeremonyRules.VirtueText(virtue)));
				Assert.IsFalse(string.IsNullOrEmpty(KingdomCeremonyRules.FlawText(flaw)));
			}
		}

		[Test]
		public void ChooseLeaderTraits_InvalidSettlementIdFallsBackToIndexZeroForBoth()
		{
			int virtue, flaw;
			KingdomCeremonyRules.ChooseLeaderTraits("", 0uL, out virtue, out flaw);
			Assert.AreEqual(0, virtue);
			Assert.AreEqual(0, flaw);
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
			Assert.IsTrue(text.Contains("Aeru"));
			Assert.IsTrue(text.Contains("the water-keeper"));
			Assert.IsTrue(text.Contains("Nivvun Ut"));
			Assert.IsTrue(text.Contains(KingdomCeremonyRules.VirtueText(0)));
			Assert.IsTrue(text.Contains(KingdomCeremonyRules.FlawText(0)));
			Assert.IsTrue(text.Contains(" -- but "));
		}

		[Test]
		public void LeaderShade_IsNetPositiveAndSmall()
		{
			int shade = KingdomCeremonyRules.LeaderShade();
			Assert.AreEqual(KingdomCeremonyRules.VirtueShadeAmount - KingdomCeremonyRules.FlawShadeAmount, shade);
			Assert.Greater(shade, 0);
			Assert.LessOrEqual(shade, 3);
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
			Assert.AreEqual(1, found.Count);
			Assert.AreEqual("r_YdRoofline", found[0].BuildingKey);
			Assert.AreEqual("yd-freehold", found[0].LearnName);
		}

		[Test]
		public void ForeignDesigns_ExcludesADesignAlreadyLearnedThroughTheRoster()
		{
			List<KingdomCeremonyRules.BuildingKnowledge> entries = new List<KingdomCeremonyRules.BuildingKnowledge>
			{
				new KingdomCeremonyRules.BuildingKnowledge { Key = "r_YdRoofline", Knowledge = "pattern:yd-freehold" }
			};
			List<string> roster = new List<string> { "pattern:yd-freehold" };
			Assert.AreEqual(0, KingdomCeremonyRules.ForeignDesigns(entries, roster).Count);
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
			Assert.AreEqual(1, KingdomCeremonyRules.ForeignDesigns(entries, roster).Count);
		}

		[Test]
		public void ForeignDesigns_DeduplicatesTheSamePatternNameAcrossTwoEntries()
		{
			List<KingdomCeremonyRules.BuildingKnowledge> entries = new List<KingdomCeremonyRules.BuildingKnowledge>
			{
				new KingdomCeremonyRules.BuildingKnowledge { Key = "r_First", Knowledge = "pattern:hindren-weave-hall" },
				new KingdomCeremonyRules.BuildingKnowledge { Key = "r_Second", Knowledge = "pattern:hindren-weave-hall" }
			};
			Assert.AreEqual(1, KingdomCeremonyRules.ForeignDesigns(entries, new List<string>()).Count);
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
			Assert.AreEqual("apple", found[0].LearnName);
			Assert.AreEqual("zebra", found[1].LearnName);
		}

		[Test]
		public void ForeignDesigns_EmptyOrNullEntriesYieldsNoCandidates()
		{
			Assert.AreEqual(0, KingdomCeremonyRules.ForeignDesigns(null, new List<string>()).Count);
			Assert.AreEqual(0, KingdomCeremonyRules.ForeignDesigns(new List<KingdomCeremonyRules.BuildingKnowledge>(), new List<string>()).Count);
		}

		// --- The pattern-book: the draws themselves --------------------------------------------

		[Test]
		public void ShouldOfferPattern_IsDeterministicForTheSameSettlementAndOrdinal()
		{
			bool first = KingdomCeremonyRules.ShouldOfferPattern("taf:settlement:example", 77uL);
			bool second = KingdomCeremonyRules.ShouldOfferPattern("taf:settlement:example", 77uL);
			Assert.AreEqual(first, second);
		}

		[Test]
		public void ShouldOfferPattern_InvalidSettlementIdFailsClosed()
		{
			Assert.IsFalse(KingdomCeremonyRules.ShouldOfferPattern("", 0uL));
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
			Assert.Greater(offered, 0);
			Assert.Less(offered, trials);
		}

		[Test]
		public void PickPatternIndex_AlwaysWithinBoundsAcrossASweep()
		{
			for (int remaining = 1; remaining <= 5; remaining++)
			{
				for (int step = 0; step < 3; step++)
				{
					int index = KingdomCeremonyRules.PickPatternIndex("taf:settlement:sweep", 12uL, step, remaining);
					Assert.GreaterOrEqual(index, 0);
					Assert.Less(index, remaining);
				}
			}
		}

		[Test]
		public void PickPatternIndex_ZeroOrNegativeRemainingReturnsZeroWithoutThrowing()
		{
			Assert.AreEqual(0, KingdomCeremonyRules.PickPatternIndex("taf:settlement:sweep", 12uL, 0, 0));
			Assert.AreEqual(0, KingdomCeremonyRules.PickPatternIndex("taf:settlement:sweep", 12uL, 0, -3));
		}

		[Test]
		public void PickPatternIndex_InvalidSettlementIdFallsBackToZero()
		{
			Assert.AreEqual(0, KingdomCeremonyRules.PickPatternIndex("", 0uL, 0, 3));
		}
	}
}
#endif
