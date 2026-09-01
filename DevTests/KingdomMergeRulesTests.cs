#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomMergeRulesTests
	{
		// These test what layering does to a design, never the shipped catalogue. Nothing here
		// asserts what the tent costs; everything here asserts that whatever the tent costs, a
		// second file that does not mention the cost cannot take it away.

		[Test]
		public void MergeDeclarationsKeepTheirPublicAbiAndDefaults()
		{
			Assert.AreEqual(typeof(int), System.Enum.GetUnderlyingType(typeof(MergeReach)));
			CollectionAssert.AreEqual(new int[3] { 0, 1, 2 }, new int[3]
			{
				(int)MergeReach.Spent,
				(int)MergeReach.Stamped,
				(int)MergeReach.Read
			});

			System.Type[] declarations = new System.Type[4]
			{
				typeof(DraftAttribute), typeof(BuildingDraft), typeof(StandingWork), typeof(MergeOffer)
			};
			for (int i = 0; i < declarations.Length; i++)
			{
				Assert.IsTrue(declarations[i].IsPublic, declarations[i].FullName);
				Assert.IsTrue(declarations[i].IsSealed, declarations[i].FullName);
			}

			CollectionAssert.AreEqual(new string[2] { "Name", "Value" },
				FieldNames(typeof(DraftAttribute)));
			CollectionAssert.AreEqual(new string[6]
			{
				"Key", "Origin", "Declarations", "Attributes", "Skins", "SkinKeysThisPass"
			}, FieldNames(typeof(BuildingDraft)));
			CollectionAssert.AreEqual(new string[3] { "Key", "SkinKey", "Raised" },
				FieldNames(typeof(StandingWork)));
			CollectionAssert.AreEqual(new string[8]
			{
				"Key", "Raised", "DisplayName", "SuccessorKey", "SkinKeys", "WearingSkinKey",
				"WearingSkinWithdrawn", "Diverged"
			}, FieldNames(typeof(MergeOffer)));

			BuildingDraft draft = new BuildingDraft();
			Assert.IsNull(draft.Key);
			Assert.IsNull(draft.Origin);
			Assert.AreEqual(1, draft.Declarations);
			Assert.AreEqual(0, draft.Attributes.Count);
			Assert.IsNull(draft.Skins);
			Assert.AreEqual(0, draft.SkinKeysThisPass.Count);
			MergeOffer offer = new MergeOffer();
			Assert.AreEqual(0, offer.SkinKeys.Count);
			Assert.IsFalse(offer.WearingSkinWithdrawn);
			Assert.AreEqual(0, offer.Diverged.Count);

			System.Reflection.ParameterInfo[] workCtor = typeof(StandingWork).GetConstructor(
				new System.Type[3] { typeof(string), typeof(BuildingDraft), typeof(string) }).GetParameters();
			Assert.IsNull(workCtor[2].DefaultValue);
			System.Reflection.ParameterInfo[] draftCtor = typeof(BuildingDraft).GetConstructor(
				new System.Type[2] { typeof(string), typeof(string) }).GetParameters();
			Assert.IsNull(draftCtor[1].DefaultValue);
		}

		private static string[] FieldNames(System.Type type)
		{
			System.Reflection.FieldInfo[] fields = type.GetFields();
			string[] names = new string[fields.Length];
			for (int i = 0; i < fields.Length; i++) names[i] = fields[i].Name;
			return names;
		}

		private static BuildingDraft Draft(string Key, params string[] Pairs)
		{
			BuildingDraft draft = new BuildingDraft(Key);
			for (int i = 0; i + 1 < Pairs.Length; i += 2)
			{
				draft.Set(Pairs[i], Pairs[i + 1]);
			}
			return draft;
		}

		private static BuildingDraft Base(string Key)
		{
			return Draft(Key,
				KingdomMergeRules.AttrDisplayName, Key,
				KingdomMergeRules.AttrBlueprint, "r_" + Key,
				KingdomMergeRules.AttrCost, "4",
				KingdomMergeRules.AttrTicks, "1200",
				KingdomMergeRules.AttrStyles, "common",
				KingdomMergeRules.AttrCategory, "housing",
				KingdomMergeRules.AttrCarries, "roof:2",
				KingdomMergeRules.AttrPlot, "S");
		}

		private static KingdomDesignRules.SkinEntry Skin(string Key, string Color)
		{
			return new KingdomDesignRules.SkinEntry { Key = Key, ColorString = Color };
		}

		private static CatalogueEntry Entry(string Key, KingdomPlotRules.PlotSize Plot = KingdomPlotRules.PlotSize.Small,
			int FootprintWidth = 0, int FootprintHeight = 0, string Successor = null, int Declarations = 1,
			string Origin = null, string Category = "housing", string Carries = "roof:2")
		{
			return new CatalogueEntry
			{
				Key = Key,
				DisplayName = Key,
				Category = Category,
				Styles = "all",
				MinStage = GrowthStage.Camp,
				Plot = Plot,
				CostDrams = 4,
				Carries = Carries,
				SuccessorKey = Successor,
				FootprintWidth = FootprintWidth,
				FootprintHeight = FootprintHeight,
				Declarations = Declarations,
				Origin = Origin
			};
		}

		private static bool Has(List<CatalogueFinding> Findings, string Key, string Attribute, CatalogueSeverity Severity)
		{
			for (int i = 0; i < Findings.Count; i++)
			{
				if (Findings[i].Key == Key && Findings[i].Attribute == Attribute && Findings[i].Severity == Severity)
				{
					return true;
				}
			}
			return false;
		}

		private static string MessageFor(List<CatalogueFinding> Findings, string Key, string Attribute)
		{
			for (int i = 0; i < Findings.Count; i++)
			{
				if (Findings[i].Key == Key && Findings[i].Attribute == Attribute)
				{
					return Findings[i].Message;
				}
			}
			return "";
		}

		private static int Count(List<CatalogueFinding> Findings, CatalogueSeverity Severity)
		{
			int count = 0;
			for (int i = 0; i < Findings.Count; i++)
			{
				if (Findings[i].Severity == Severity)
				{
					count++;
				}
			}
			return count;
		}

		// --- Override, survive ----------------------------------------------------------------

		[Test]
		public void Merge_OverridesEveryAttributeTheLaterFileNames()
		{
			BuildingDraft later = Draft("tent", KingdomMergeRules.AttrCost, "9", KingdomMergeRules.AttrStyles, "verdant");
			BuildingDraft merged = KingdomMergeRules.Merge(Base("tent"), later, null);
			Assert.AreEqual("9", merged.Get(KingdomMergeRules.AttrCost));
			Assert.AreEqual("verdant", merged.Get(KingdomMergeRules.AttrStyles));
		}

		[Test]
		public void Merge_KeepsEveryAttributeTheLaterFileOmits()
		{
			BuildingDraft later = Draft("tent", KingdomMergeRules.AttrCost, "9");
			BuildingDraft merged = KingdomMergeRules.Merge(Base("tent"), later, null);
			Assert.AreEqual("r_tent", merged.Get(KingdomMergeRules.AttrBlueprint));
			Assert.AreEqual("1200", merged.Get(KingdomMergeRules.AttrTicks));
			Assert.AreEqual("housing", merged.Get(KingdomMergeRules.AttrCategory));
			Assert.AreEqual("roof:2", merged.Get(KingdomMergeRules.AttrCarries));
			Assert.AreEqual("S", merged.Get(KingdomMergeRules.AttrPlot));
		}

		[Test]
		public void Merge_TreatsAnAttributeNamedBlankAsAnErasure()
		{
			// Omitting is not the same as naming empty: the first is "I have no opinion", the
			// second is "no table", and a modder needs both.
			BuildingDraft with = Draft("hut", KingdomMergeRules.AttrContents, "Hut_Contents");
			BuildingDraft merged = KingdomMergeRules.Merge(with, Draft("hut", KingdomMergeRules.AttrContents, ""), null);
			Assert.AreEqual("", merged.Get(KingdomMergeRules.AttrContents));
			BuildingDraft silent = KingdomMergeRules.Merge(with, Draft("hut", KingdomMergeRules.AttrCost, "5"), null);
			Assert.AreEqual("Hut_Contents", silent.Get(KingdomMergeRules.AttrContents));
		}

		[Test]
		public void Merge_LeavesBothDraftsItWasGivenAlone()
		{
			BuildingDraft standing = Base("tent");
			BuildingDraft later = Draft("tent", KingdomMergeRules.AttrCost, "9");
			KingdomMergeRules.Merge(standing, later, null);
			Assert.AreEqual("4", standing.Get(KingdomMergeRules.AttrCost));
			Assert.IsFalse(later.Names(KingdomMergeRules.AttrBlueprint));
		}

		[Test]
		public void Merge_CountsHowManyFilesTheDesignIsMadeOf()
		{
			BuildingDraft once = Base("tent");
			Assert.AreEqual(1, once.Declarations);
			BuildingDraft twice = KingdomMergeRules.Merge(once, Draft("tent", KingdomMergeRules.AttrCost, "5"), null);
			Assert.AreEqual(2, twice.Declarations);
			BuildingDraft thrice = KingdomMergeRules.Merge(twice, Draft("tent", KingdomMergeRules.AttrCost, "6"), null);
			Assert.AreEqual(3, thrice.Declarations);
		}

		[Test]
		public void Merge_ReportsWhichAttributesTheLaterFileSet()
		{
			List<CatalogueFinding> findings = new List<CatalogueFinding>();
			KingdomMergeRules.Merge(Base("tent"), Draft("tent", KingdomMergeRules.AttrCost, "9", KingdomMergeRules.AttrStyles, "verdant"), findings);
			Assert.AreEqual(1, findings.Count);
			Assert.AreEqual(CatalogueSeverity.Note, findings[0].Severity);
			Assert.IsTrue(findings[0].Message.Contains(KingdomMergeRules.AttrCost));
			Assert.IsTrue(findings[0].Message.Contains(KingdomMergeRules.AttrStyles));
			Assert.IsFalse(findings[0].Message.Contains(KingdomMergeRules.AttrBlueprint));
		}

		[Test]
		public void Merge_SaysNothingWhenTheLaterFileRepeatsWhatAlreadyStood()
		{
			List<CatalogueFinding> findings = new List<CatalogueFinding>();
			KingdomMergeRules.Merge(Base("tent"), Draft("tent", KingdomMergeRules.AttrCost, "4"), findings);
			Assert.AreEqual(0, findings.Count);
		}

		// --- A merge into a key nothing declared ----------------------------------------------

		[Test]
		public void Merge_IntoAKeyNothingDeclaredCreatesIt()
		{
			BuildingDraft merged = KingdomMergeRules.Merge(null, Base("newthing"), null);
			Assert.AreEqual("newthing", merged.Key);
			Assert.AreEqual(1, merged.Declarations);
			Assert.AreEqual("r_newthing", merged.Get(KingdomMergeRules.AttrBlueprint));
		}

		[Test]
		public void Merge_NotesAFragmentWithNothingToMergeInto()
		{
			List<CatalogueFinding> findings = new List<CatalogueFinding>();
			BuildingDraft merged = KingdomMergeRules.Merge(null, Draft("tnet", KingdomMergeRules.AttrCost, "9"), findings);
			Assert.AreEqual("9", merged.Get(KingdomMergeRules.AttrCost));
			Assert.AreEqual(1, findings.Count);
			Assert.IsTrue(findings[0].Message.Contains(KingdomMergeRules.AttrBlueprint));
			Assert.IsTrue(findings[0].Message.Contains(KingdomMergeRules.AttrDisplayName));
		}

		[Test]
		public void Merge_SaysNothingWhenAFirstDeclarationIsWholeEnoughToStand()
		{
			List<CatalogueFinding> findings = new List<CatalogueFinding>();
			KingdomMergeRules.Merge(null, Base("tent"), findings);
			Assert.AreEqual(0, findings.Count);
		}

		[TestCase("DisplayName")]
		[TestCase("Blueprint")]
		[TestCase("Cost")]
		[TestCase("Ticks")]
		public void IsFragment_NamesTheOneRequiredAttributeThatIsMissing(string Missing)
		{
			BuildingDraft draft = Base("tent");
			draft.Set(Missing, "");
			List<string> missing;
			Assert.IsTrue(KingdomMergeRules.IsFragment(draft, out missing));
			Assert.AreEqual(1, missing.Count);
			Assert.AreEqual(Missing, missing[0]);
		}

		[Test]
		public void IsFragment_IsFalseForADesignThatNamesAllFour()
		{
			List<string> missing;
			Assert.IsFalse(KingdomMergeRules.IsFragment(Base("tent"), out missing));
			Assert.AreEqual(0, missing.Count);
		}

		// --- Skins: append, and replace by key --------------------------------------------------

		[Test]
		public void MergeSkin_AppendsASkinTheDesignDoesNotHave()
		{
			BuildingDraft draft = Base("hut");
			bool replaced;
			string error;
			Assert.IsTrue(KingdomMergeRules.TryMergeSkin(draft, Skin("verdant", "&g"), out replaced, out error));
			Assert.IsFalse(replaced);
			Assert.AreEqual(1, draft.Skins.Count);
			Assert.AreEqual("verdant", draft.Skins[0].Key);
		}

		[Test]
		public void MergeSkin_ReplacesTheSameKeyWhereItAlreadySits()
		{
			BuildingDraft first = Base("hut");
			bool replaced;
			string error;
			KingdomMergeRules.TryMergeSkin(first, Skin("verdant", "&g"), out replaced, out error);
			KingdomMergeRules.TryMergeSkin(first, Skin("bleached", "&Y"), out replaced, out error);

			BuildingDraft second = KingdomMergeRules.Merge(first, Draft("hut"), null);
			Assert.IsTrue(KingdomMergeRules.TryMergeSkin(second, Skin("verdant", "&W"), out replaced, out error));
			Assert.IsTrue(replaced);
			Assert.AreEqual(2, second.Skins.Count);
			// In place: re-colouring the verdant skin must not move it below the bleached one in
			// the list the founder is offered.
			Assert.AreEqual("verdant", second.Skins[0].Key);
			Assert.AreEqual("&W", second.Skins[0].ColorString);
			Assert.AreEqual("bleached", second.Skins[1].Key);
		}

		[Test]
		public void MergeSkin_RefusesTheSameKeyTwiceInsideOneElement()
		{
			BuildingDraft draft = Base("hut");
			bool replaced;
			string error;
			KingdomMergeRules.TryMergeSkin(draft, Skin("verdant", "&g"), out replaced, out error);
			Assert.IsFalse(KingdomMergeRules.TryMergeSkin(draft, Skin("verdant", "&W"), out replaced, out error));
			Assert.IsNotNull(error);
			Assert.AreEqual(1, draft.Skins.Count);
			Assert.AreEqual("&g", draft.Skins[0].ColorString);
		}

		[Test]
		public void Merge_CarriesTheEarlierFilesSkinsIntoTheLaterOne()
		{
			BuildingDraft first = Base("hut");
			bool replaced;
			string error;
			KingdomMergeRules.TryMergeSkin(first, Skin("verdant", "&g"), out replaced, out error);
			BuildingDraft second = KingdomMergeRules.Merge(first, Draft("hut", KingdomMergeRules.AttrCost, "5"), null);
			Assert.AreEqual(1, second.Skins.Count);
			// And the second file may add its own without the first's list being reopened.
			KingdomMergeRules.TryMergeSkin(second, Skin("marble", "&y"), out replaced, out error);
			Assert.AreEqual(2, second.Skins.Count);
			Assert.AreEqual(1, first.Skins.Count);
		}

		[Test]
		public void Merge_LetsASkinKeyBeRedeclaredAcrossFilesButNotWithinOne()
		{
			BuildingDraft first = Base("hut");
			bool replaced;
			string error;
			KingdomMergeRules.TryMergeSkin(first, Skin("verdant", "&g"), out replaced, out error);
			// Same element, same key: refused.
			Assert.IsFalse(KingdomMergeRules.TryMergeSkin(first, Skin("verdant", "&W"), out replaced, out error));
			// New element, same key: replaces.
			BuildingDraft second = KingdomMergeRules.Merge(first, Draft("hut"), null);
			Assert.IsTrue(KingdomMergeRules.TryMergeSkin(second, Skin("verdant", "&W"), out replaced, out error));
		}

		// --- Load order, and three files piling up ----------------------------------------------

		[Test]
		public void Absorb_PilesThreeFilesUpAttributeByAttribute()
		{
			KingdomMergeRules.ClearDrafts();
			KingdomMergeRules.Absorb(Base("hut"));
			KingdomMergeRules.Absorb(Draft("hut", KingdomMergeRules.AttrCost, "7"));
			BuildingDraft merged = KingdomMergeRules.Absorb(Draft("hut", KingdomMergeRules.AttrCarries, "roof:3"));
			Assert.AreEqual("7", merged.Get(KingdomMergeRules.AttrCost));
			Assert.AreEqual("roof:3", merged.Get(KingdomMergeRules.AttrCarries));
			Assert.AreEqual("r_hut", merged.Get(KingdomMergeRules.AttrBlueprint));
			Assert.AreEqual(3, merged.Declarations);
			Assert.AreEqual(3, KingdomMergeRules.DeclarationsOf("hut"));
		}

		[Test]
		public void Absorb_GivesTheLastFileThatNamesAnAttributeTheLastWord()
		{
			KingdomMergeRules.ClearDrafts();
			KingdomMergeRules.Absorb(Base("hut"));
			KingdomMergeRules.Absorb(Draft("hut", KingdomMergeRules.AttrCost, "7"));
			BuildingDraft merged = KingdomMergeRules.Absorb(Draft("hut", KingdomMergeRules.AttrCost, "2"));
			Assert.AreEqual("2", merged.Get(KingdomMergeRules.AttrCost));
		}

		[Test]
		public void Absorb_LetsALaterFileExtendAChainTheBaseCatalogueEnded()
		{
			KingdomMergeRules.ClearDrafts();
			KingdomMergeRules.Absorb(Base("hut"));
			Assert.IsNull(KingdomMergeRules.Absorb(Draft("hut", KingdomMergeRules.AttrCost, "5")).Get(KingdomMergeRules.AttrUpgradesTo));
			BuildingDraft merged = KingdomMergeRules.Absorb(Draft("hut", KingdomMergeRules.AttrUpgradesTo, "stonehouse"));
			Assert.AreEqual("stonehouse", merged.Get(KingdomMergeRules.AttrUpgradesTo));
			// And the water cost the base catalogue set is still the water cost.
			Assert.AreEqual("5", merged.Get(KingdomMergeRules.AttrCost));
		}

		[Test]
		public void Absorb_KeepsEachKeySeparate()
		{
			KingdomMergeRules.ClearDrafts();
			KingdomMergeRules.Absorb(Base("hut"));
			KingdomMergeRules.Absorb(Base("tent"));
			KingdomMergeRules.Absorb(Draft("hut", KingdomMergeRules.AttrCost, "7"));
			BuildingDraft tent;
			Assert.IsTrue(KingdomMergeRules.TryGetDraft("tent", out tent));
			Assert.AreEqual("4", tent.Get(KingdomMergeRules.AttrCost));
			Assert.AreEqual(1, tent.Declarations);
		}

		[Test]
		public void ClearDrafts_ForgetsEverythingBeforeAReload()
		{
			KingdomMergeRules.ClearDrafts();
			KingdomMergeRules.Absorb(Base("hut"));
			KingdomMergeRules.ClearDrafts();
			BuildingDraft draft;
			Assert.IsFalse(KingdomMergeRules.TryGetDraft("hut", out draft));
			Assert.AreEqual(0, KingdomMergeRules.Findings.Count);
		}

		// --- The guardrail: merges shape future commissions only ---------------------------------

		[Test]
		public void Reconcile_KeepsEverySpentAndStampedAttributeAStandingWorkWasRaisedWith()
		{
			BuildingDraft raised = Draft("hut",
				KingdomMergeRules.AttrCost, "4", KingdomMergeRules.AttrTicks, "1200", KingdomMergeRules.AttrMaterials, "timber:3",
				KingdomMergeRules.AttrBits, "00", KingdomMergeRules.AttrExotics, "ingot:1",
				KingdomMergeRules.AttrPurposeCargoWater, "12", KingdomMergeRules.AttrPurposeCargoCost, "brush:4,workedmetal:1",
				KingdomMergeRules.AttrBlueprint, "r_hut", KingdomMergeRules.AttrPlot, "S", KingdomMergeRules.AttrFootprint, "4x3",
				KingdomMergeRules.AttrRoof, "Walled", KingdomMergeRules.AttrOpen, "No", KingdomMergeRules.AttrContents, "Hut_Contents",
				KingdomMergeRules.AttrPurpose, "flesh", KingdomMergeRules.AttrPurposeSite, "living-surgery",
				KingdomMergeRules.AttrPurposeCargoKey, "graft-stock-casket", KingdomMergeRules.AttrPurposeCargoName, "sealed casket",
				KingdomMergeRules.AttrPurposeCargoMaterial, "workedmetal", KingdomMergeRules.AttrPurposeProducers, "vathouse",
				KingdomMergeRules.AttrPurposeEffect, "procedures");
			BuildingDraft rewritten = Draft("hut",
				KingdomMergeRules.AttrCost, "40", KingdomMergeRules.AttrTicks, "9999", KingdomMergeRules.AttrMaterials, "marble:9",
				KingdomMergeRules.AttrBits, "0034", KingdomMergeRules.AttrExotics, "gold:2,gem:1",
				KingdomMergeRules.AttrPurposeCargoWater, "16", KingdomMergeRules.AttrPurposeCargoCost, "scrap:6,workedmetal:1",
				KingdomMergeRules.AttrBlueprint, "r_palace", KingdomMergeRules.AttrPlot, "XL", KingdomMergeRules.AttrFootprint, "18x12",
				KingdomMergeRules.AttrRoof, "Open", KingdomMergeRules.AttrOpen, "Yes", KingdomMergeRules.AttrContents, "Palace_Contents",
				KingdomMergeRules.AttrPurpose, "chrome", KingdomMergeRules.AttrPurposeSite, "ruin-enrollment",
				KingdomMergeRules.AttrPurposeCargoKey, "arclight-register", KingdomMergeRules.AttrPurposeCargoName, "sealed register",
				KingdomMergeRules.AttrPurposeCargoMaterial, "scrap", KingdomMergeRules.AttrPurposeProducers, "smelter,chargingpost",
				KingdomMergeRules.AttrPurposeEffect, "enrollment");
			MergeOffer offer = KingdomMergeRules.Reconcile(new StandingWork("hut", raised), rewritten);

			// Walking the arrays rather than listing the attributes means adding one to either
			// array without teaching Reconcile about it fails here rather than in somebody's city.
			for (int i = 0; i < KingdomMergeRules.SpentAttributes.Length; i++)
			{
				string attribute = KingdomMergeRules.SpentAttributes[i];
				Assert.AreEqual(raised.Get(attribute), offer.Raised.Get(attribute), attribute);
			}
			for (int i = 0; i < KingdomMergeRules.StampedAttributes.Length; i++)
			{
				string attribute = KingdomMergeRules.StampedAttributes[i];
				Assert.AreEqual(raised.Get(attribute), offer.Raised.Get(attribute), attribute);
			}
			Assert.AreEqual(KingdomMergeRules.SpentAttributes.Length + KingdomMergeRules.StampedAttributes.Length, offer.Diverged.Count);
		}

		[Test]
		public void Reconcile_SeesTheDivergenceItRefusesToApply()
		{
			// The two halves are one guardrail: code that cannot tell the difference would pass a
			// "nothing changed" assertion by doing nothing at all.
			BuildingDraft raised = Draft("hut", KingdomMergeRules.AttrCost, "4", KingdomMergeRules.AttrBlueprint, "r_hut");
			BuildingDraft merged = Draft("hut", KingdomMergeRules.AttrCost, "40", KingdomMergeRules.AttrBlueprint, "r_hut");
			MergeOffer offer = KingdomMergeRules.Reconcile(new StandingWork("hut", raised), merged);
			Assert.AreEqual(1, offer.Diverged.Count);
			Assert.AreEqual(KingdomMergeRules.AttrCost, offer.Diverged[0]);
			Assert.AreEqual("4", offer.Raised.Get(KingdomMergeRules.AttrCost));
			Assert.IsNotNull(KingdomMergeRules.StandingLine("the old hut", offer));
		}

		[Test]
		public void Reconcile_SaysNothingWhenAMergeChangedOnlyWhatIsReadAgain()
		{
			BuildingDraft raised = Draft("hut", KingdomMergeRules.AttrCost, "4", KingdomMergeRules.AttrCarries, "roof:2");
			BuildingDraft merged = Draft("hut", KingdomMergeRules.AttrCost, "4", KingdomMergeRules.AttrCarries, "roof:5");
			MergeOffer offer = KingdomMergeRules.Reconcile(new StandingWork("hut", raised), merged);
			Assert.AreEqual(0, offer.Diverged.Count);
			Assert.IsNull(KingdomMergeRules.StandingLine("the old hut", offer));
		}

		[Test]
		public void Reconcile_HandsBackACopyRatherThanTheWorksOwnDraft()
		{
			BuildingDraft raised = Draft("hut", KingdomMergeRules.AttrCost, "4");
			StandingWork work = new StandingWork("hut", raised);
			MergeOffer offer = KingdomMergeRules.Reconcile(work, Draft("hut", KingdomMergeRules.AttrCost, "40"));
			Assert.IsFalse(ReferenceEquals(offer.Raised, raised));
			offer.Raised.Set(KingdomMergeRules.AttrCost, "1");
			Assert.AreEqual("4", raised.Get(KingdomMergeRules.AttrCost));
		}

		[Test]
		public void Reconcile_OffersASkinALaterFileAddedToSomethingAlreadyStanding()
		{
			BuildingDraft raised = Base("hut");
			BuildingDraft merged = KingdomMergeRules.Merge(raised, Draft("hut"), null);
			bool replaced;
			string error;
			KingdomMergeRules.TryMergeSkin(merged, Skin("marble", "&y"), out replaced, out error);
			MergeOffer offer = KingdomMergeRules.Reconcile(new StandingWork("hut", raised), merged);
			Assert.IsTrue(offer.SkinKeys.Contains("marble"));
			Assert.AreEqual(0, offer.Diverged.Count);
		}

		[Test]
		public void Reconcile_OffersAChainLinkALaterFileAddedToSomethingAlreadyStanding()
		{
			BuildingDraft raised = Base("hut");
			BuildingDraft merged = KingdomMergeRules.Merge(raised, Draft("hut", KingdomMergeRules.AttrUpgradesTo, "stonehouse"), null);
			MergeOffer offer = KingdomMergeRules.Reconcile(new StandingWork("hut", raised), merged);
			Assert.AreEqual("stonehouse", offer.SuccessorKey);
			Assert.AreEqual(0, offer.Diverged.Count);
		}

		[Test]
		public void Reconcile_ReportsASkinWithdrawnFromUnderAWorkThatWearsIt()
		{
			BuildingDraft raised = Base("hut");
			bool replaced;
			string error;
			KingdomMergeRules.TryMergeSkin(raised, Skin("verdant", "&g"), out replaced, out error);
			BuildingDraft merged = Base("hut");
			KingdomMergeRules.TryMergeSkin(merged, Skin("marble", "&y"), out replaced, out error);
			MergeOffer offer = KingdomMergeRules.Reconcile(new StandingWork("hut", raised, "verdant"), merged);
			Assert.IsTrue(offer.WearingSkinWithdrawn);
			Assert.AreEqual("verdant", offer.WearingSkinKey);
		}

		[Test]
		public void Reconcile_FollowsARenameWithoutMovingAnything()
		{
			BuildingDraft raised = Base("hut");
			BuildingDraft merged = KingdomMergeRules.Merge(raised, Draft("hut", KingdomMergeRules.AttrDisplayName, "sod house"), null);
			MergeOffer offer = KingdomMergeRules.Reconcile(new StandingWork("hut", raised), merged);
			Assert.AreEqual("sod house", offer.DisplayName);
			Assert.AreEqual("r_hut", offer.Raised.Get(KingdomMergeRules.AttrBlueprint));
		}

		[Test]
		public void Reconcile_IsNullForNoWorkAndSurvivesAWorkWithNoRaisedDraft()
		{
			Assert.IsNull(KingdomMergeRules.Reconcile(null, Base("hut")));
			MergeOffer offer = KingdomMergeRules.Reconcile(new StandingWork("hut", null), Base("hut"));
			Assert.IsNull(offer.Raised);
			Assert.AreEqual(0, offer.Diverged.Count);
			Assert.AreEqual("hut", offer.DisplayName);
		}

		[TestCase("Cost", MergeReach.Spent)]
		[TestCase("Ticks", MergeReach.Spent)]
		[TestCase("Materials", MergeReach.Spent)]
		[TestCase("Blueprint", MergeReach.Stamped)]
		[TestCase("Plot", MergeReach.Stamped)]
		[TestCase("Footprint", MergeReach.Stamped)]
		[TestCase("Roof", MergeReach.Stamped)]
		[TestCase("Open", MergeReach.Stamped)]
		[TestCase("Contents", MergeReach.Stamped)]
		[TestCase("DisplayName", MergeReach.Read)]
		[TestCase("Carries", MergeReach.Read)]
		[TestCase("Staff", MergeReach.Read)]
		[TestCase("Defence", MergeReach.Read)]
		[TestCase("Manning", MergeReach.Read)]
		[TestCase("Category", MergeReach.Read)]
		[TestCase("Styles", MergeReach.Read)]
		[TestCase("MinStage", MergeReach.Read)]
		[TestCase("Sky", MergeReach.Read)]
		[TestCase("Districts", MergeReach.Read)]
		[TestCase("UpgradesTo", MergeReach.Read)]
		[TestCase("UpgradeCost", MergeReach.Read)]
		[TestCase("Bananas", MergeReach.Read)]
		public void Classify_SaysHowFarAChangeToEachAttributeReaches(string Attribute, MergeReach Expected)
		{
			Assert.AreEqual(Expected, KingdomMergeRules.Classify(Attribute));
			Assert.AreEqual(Expected == MergeReach.Read, KingdomMergeRules.ReachesStandingWork(Attribute));
		}

		// --- Post-merge coherence, in the validator ---------------------------------------------

		[Test]
		public void Validate_FaultsAFootprintThatOutgrewThePlotALaterFileShrank()
		{
			// File one: a large plot with a twelve-by-nine tier on it. File two, wanting a modest
			// version, overrides nothing but the plot. Neither file is wrong by itself.
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("hall", KingdomPlotRules.PlotSize.Small, 12, 9, Declarations: 2)
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "hall", "Footprint", CatalogueSeverity.Fault));
			string message = MessageFor(findings, "hall", "Footprint");
			Assert.IsTrue(message.Contains("12"));
			Assert.IsTrue(message.Contains("6"));
			Assert.IsTrue(message.Contains("merge of 2 declarations"));
		}

		[Test]
		public void Validate_AcceptsAFootprintThatFitsInsideItsPlot()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("hut", KingdomPlotRules.PlotSize.Small, 6, 4),
				Entry("shed", KingdomPlotRules.PlotSize.Medium, 3, 2)
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.AreEqual(0, Count(findings, CatalogueSeverity.Fault));
		}

		[TestCase(6, 0)]
		[TestCase(0, 4)]
		public void Validate_FaultsHalfAFootprint(int Width, int Height)
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry> { Entry("hut", KingdomPlotRules.PlotSize.Medium, Width, Height) };
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "hut", "Footprint", CatalogueSeverity.Fault));
		}

		[Test]
		public void Validate_FaultsAFootprintWithNoPlotUnderIt()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry> { Entry("tower", KingdomPlotRules.PlotSize.None, 3, 3) };
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "tower", "Footprint", CatalogueSeverity.Fault));
		}

		[Test]
		public void Validate_SaysNothingAboutFootprintsForADesignThatDeclaresNone()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry> { Entry("hut", KingdomPlotRules.PlotSize.Small) };
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsFalse(Has(findings, "hut", "Footprint", CatalogueSeverity.Fault));
			Assert.IsFalse(Has(findings, "hut", "Footprint", CatalogueSeverity.Note));
		}

		[Test]
		public void Validate_FaultsARingThreeFilesCloseBetweenThem()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("tent", KingdomPlotRules.PlotSize.Small, Successor: "hut"),
				Entry("hut", KingdomPlotRules.PlotSize.Small, Successor: "house", Declarations: 2),
				Entry("house", KingdomPlotRules.PlotSize.Small, Successor: "tent", Declarations: 3)
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "tent", "UpgradesTo", CatalogueSeverity.Fault));
			string message = MessageFor(findings, "tent", "UpgradesTo");
			Assert.IsTrue(message.Contains("hut from 2 files"));
			Assert.IsTrue(message.Contains("house from 3 files"));
		}

		[Test]
		public void Validate_LeavesTheRingSentenceAloneWhenEveryLinkCameFromOneFile()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("tent", KingdomPlotRules.PlotSize.Small, Successor: "hut"),
				Entry("hut", KingdomPlotRules.PlotSize.Small, Successor: "tent")
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "tent", "UpgradesTo", CatalogueSeverity.Fault));
			Assert.IsFalse(MessageFor(findings, "tent", "UpgradesTo").Contains("files"));
		}

		[Test]
		public void Validate_NamesTheLastFileWhenTheLoaderKnowsIt()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("hall", KingdomPlotRules.PlotSize.Small, 12, 9, Declarations: 2, Origin: "SomeMod")
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(MessageFor(findings, "hall", "Footprint").Contains("SomeMod"));
		}

		[Test]
		public void Validate_FaultsAnUnmergedDuplicateKey()
		{
			// Two entries under one key mean the caller did not merge; the design the settlement
			// would build is half of what the files said.
			List<CatalogueEntry> entries = new List<CatalogueEntry> { Entry("hut"), Entry("hut") };
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "hut", "Key", CatalogueSeverity.Fault));
		}
	}
}
#endif
