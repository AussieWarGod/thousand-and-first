#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomCatalogueRulesTests
	{
		// These test the validator and the arithmetic, never the shipped catalogue. A test that
		// asserted "the tent costs three drams" would fail the next time somebody balanced the
		// tent, which is not a defect and is not what any of this is for.

		private static CatalogueEntry Entry(string Key,
			KingdomPlotRules.PlotSize Plot = KingdomPlotRules.PlotSize.Small,
			GrowthStage MinStage = GrowthStage.Camp, string Category = "housing", string Carries = "roof:2",
			int Cost = 4, int Staff = 0, int Defence = 0, string Successor = null, string Materials = null,
			string Styles = "all", string Manning = "scaled", bool Open = false, string Contents = null)
		{
			return new CatalogueEntry
			{
				Key = Key,
				DisplayName = Key,
				Category = Category,
				Styles = Styles,
				MinStage = MinStage,
				Plot = Plot,
				Open = Open,
				Contents = Contents,
				CostDrams = Cost,
				Staff = Staff,
				Manning = Manning,
				Defence = Defence,
				Materials = Materials,
				Carries = Carries,
				SuccessorKey = Successor
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

		private static string FirstMessage(List<CatalogueFinding> Findings)
		{
			return (Findings.Count == 0) ? "" : Findings[0].Message;
		}

		// --- Stage against the plot the design stands on -------------------------------------

		[TestCase(GrowthStage.Camp, KingdomPlotRules.PlotSize.Large, GrowthStage.Town)]
		[TestCase(GrowthStage.Steading, KingdomPlotRules.PlotSize.Huge, GrowthStage.City)]
		[TestCase(GrowthStage.City, KingdomPlotRules.PlotSize.Small, GrowthStage.City)]
		[TestCase(GrowthStage.Village, KingdomPlotRules.PlotSize.Medium, GrowthStage.Village)]
		[TestCase(GrowthStage.Camp, KingdomPlotRules.PlotSize.Medium, GrowthStage.Steading)]
		public void EffectiveMinStage_TakesTheLaterOfTheTwoGates(GrowthStage authored, KingdomPlotRules.PlotSize plot, GrowthStage expected)
		{
			Assert.AreEqual(expected, KingdomCatalogueRules.EffectiveMinStage(authored, plot));
		}

		[Test]
		public void EffectiveMinStage_LeavesASingleCellWorkExactlyWhereItsAuthorPutIt()
		{
			// A wall segment is not a plot, so nothing about plot size may push its stage around.
			Assert.AreEqual(GrowthStage.Camp,
				KingdomCatalogueRules.EffectiveMinStage(GrowthStage.Camp, KingdomPlotRules.PlotSize.None));
			Assert.AreEqual(GrowthStage.Village,
				KingdomCatalogueRules.EffectiveMinStage(GrowthStage.Village, KingdomPlotRules.PlotSize.None));
		}

		// --- Supports and the level ---------------------------------------------------------

		[TestCase("water", true)]
		[TestCase("food", true)]
		[TestCase("roof", true)]
		[TestCase(" ROOF ", true)]
		[TestCase("craft", false)]
		[TestCase("spirit", false)]
		[TestCase("learning", false)]
		[TestCase("order", false)]
		[TestCase("luxury", false)]
		[TestCase("moonlight", false)]
		[TestCase(null, false)]
		public void IsBindingSupport_IsOnlyTheThree(string kind, bool expected)
		{
			Assert.AreEqual(expected, KingdomCatalogueRules.IsBindingSupport(kind));
		}

		[TestCase("water", true)]
		[TestCase("luxury", true)]
		[TestCase("moonlight", false)]
		[TestCase("", false)]
		public void IsKnownSupport_CoversBothHalvesAndNothingElse(string kind, bool expected)
		{
			Assert.AreEqual(expected, KingdomCatalogueRules.IsKnownSupport(kind));
		}

		[Test]
		public void Equilibrium_IsTheLeastOfTheThreeBindingSupports()
		{
			Assert.AreEqual(20, KingdomCatalogueRules.Equilibrium(20, 30, 40, 0));
			Assert.AreEqual(20, KingdomCatalogueRules.Equilibrium(40, 20, 30, 0));
			Assert.AreEqual(20, KingdomCatalogueRules.Equilibrium(30, 40, 20, 0));
		}

		[Test]
		public void Equilibrium_NeverFallsBelowTheFloor()
		{
			Assert.AreEqual(KingdomCatalogueRules.FloorLevel, KingdomCatalogueRules.Equilibrium(0, 0, 0, 0));
			Assert.AreEqual(KingdomCatalogueRules.FloorLevel, KingdomCatalogueRules.Equilibrium(40, 0, 40, 0));
			Assert.AreEqual(KingdomCatalogueRules.FloorLevel, KingdomCatalogueRules.Equilibrium(2, 2, 2, 0));
			// A negative binding total is arithmetic nobody intended, and it still may not push
			// the settlement under its floor.
			Assert.AreEqual(KingdomCatalogueRules.FloorLevel, KingdomCatalogueRules.Equilibrium(-5, 40, 40, 0));
		}

		[Test]
		public void Equilibrium_LiftsTheLevelButOnlyToItsCap()
		{
			// Cap is half the binding level: twenty binding takes at most ten of lift.
			Assert.AreEqual(23, KingdomCatalogueRules.Equilibrium(20, 20, 20, 3));
			Assert.AreEqual(30, KingdomCatalogueRules.Equilibrium(20, 20, 20, 10));
			Assert.AreEqual(30, KingdomCatalogueRules.Equilibrium(20, 20, 20, 11));
			Assert.AreEqual(30, KingdomCatalogueRules.Equilibrium(20, 20, 20, 900));
		}

		[Test]
		public void Equilibrium_CannotShrineItsWayPastNoWaterAtAll()
		{
			// Zero binding means zero cap: comfort is worth nothing when the casks are dry, and
			// the floor is what is left.
			Assert.AreEqual(KingdomCatalogueRules.FloorLevel, KingdomCatalogueRules.Equilibrium(0, 90, 90, 400));
		}

		[Test]
		public void Equilibrium_TreatsANegativeLiftAsNone()
		{
			Assert.AreEqual(20, KingdomCatalogueRules.Equilibrium(20, 20, 20, -50));
		}

		[TestCase(5, 9, 9, "water")]
		[TestCase(9, 5, 9, "food")]
		[TestCase(9, 9, 5, "roof")]
		[TestCase(5, 5, 5, "water")]
		[TestCase(9, 5, 5, "food")]
		public void BindingSupport_NamesWhatIsHoldingTheSettlementBack(int water, int food, int roof, string expected)
		{
			Assert.AreEqual(expected, KingdomCatalogueRules.BindingSupport(water, food, roof));
		}

		[Test]
		public void LimitLine_SaysTheLevelAndReadsDifferentlyForEachSupport()
		{
			string water = KingdomCatalogueRules.LimitLine("water", 12);
			string food = KingdomCatalogueRules.LimitLine("food", 12);
			string roof = KingdomCatalogueRules.LimitLine("roof", 12);
			Assert.IsTrue(water.Contains("12"));
			Assert.IsTrue(food.Contains("12"));
			Assert.IsTrue(roof.Contains("12"));
			Assert.AreNotEqual(water, food);
			Assert.AreNotEqual(food, roof);
			Assert.AreNotEqual(water, roof);
			// An unknown support still gets a line: 7b says a stall explains itself, and a kind
			// this file has never heard of is exactly when that matters most.
			string unknown = KingdomCatalogueRules.LimitLine("moonlight", 12);
			Assert.IsTrue(unknown.Contains("12"));
		}

		// --- The Carries list ---------------------------------------------------------------

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		public void TryParseTally_ReadsAnAbsentListAsEmptyRatherThanBroken(string written)
		{
			bool ok = KingdomCatalogueRules.TryParseTally(written, out var tally, out var error);
			Assert.IsTrue(ok);
			Assert.IsNull(error);
			Assert.AreEqual(0, tally.Count);
		}

		[Test]
		public void TryParseTally_FoldsKindsAndKeepsOrder()
		{
			bool ok = KingdomCatalogueRules.TryParseTally(" WATER : 6 , food:2 ,, spirit:1 ", out var tally, out var error);
			Assert.IsTrue(ok);
			Assert.IsNull(error);
			Assert.AreEqual(3, tally.Count);
			Assert.AreEqual("water", tally[0].Kind);
			Assert.AreEqual(6, tally[0].Amount);
			Assert.AreEqual("food", tally[1].Kind);
			Assert.AreEqual(2, tally[1].Amount);
			Assert.AreEqual("spirit", tally[2].Kind);
			Assert.AreEqual(1, tally[2].Amount);
		}

		[Test]
		public void TryParseTally_AcceptsZero()
		{
			bool ok = KingdomCatalogueRules.TryParseTally("craft:0", out var tally, out _);
			Assert.IsTrue(ok);
			Assert.AreEqual(1, tally.Count);
			Assert.AreEqual(0, tally[0].Amount);
		}

		[TestCase("water")]
		[TestCase("water:")]
		[TestCase(":6")]
		[TestCase("water:x")]
		[TestCase("water:-1")]
		[TestCase("water:2:3")]
		public void TryParseTally_RefusesAPairItCannotRead(string written)
		{
			bool ok = KingdomCatalogueRules.TryParseTally(written, out var tally, out var error);
			Assert.IsFalse(ok);
			Assert.IsNotNull(error);
			Assert.IsNotNull(tally);
		}

		[Test]
		public void TryParseTally_KeepsWhatItAlreadyReadWhenItGivesUp()
		{
			// The caller logs and carries on; it must not be credited with nothing for a list
			// whose first two thirds were perfectly good.
			bool ok = KingdomCatalogueRules.TryParseTally("water:6,food:2,rubbish", out var tally, out _);
			Assert.IsFalse(ok);
			Assert.AreEqual(2, tally.Count);
		}

		[Test]
		public void AmountOf_AddsRepeatsAndAnswersZeroForAnythingAbsent()
		{
			KingdomCatalogueRules.TryParseTally("water:2,water:3,food:4", out var tally, out _);
			Assert.AreEqual(5, KingdomCatalogueRules.AmountOf(tally, "water"));
			Assert.AreEqual(4, KingdomCatalogueRules.AmountOf(tally, "FOOD"));
			Assert.AreEqual(0, KingdomCatalogueRules.AmountOf(tally, "roof"));
			Assert.AreEqual(0, KingdomCatalogueRules.AmountOf(null, "water"));
		}

		[Test]
		public void LiftOf_IsEverythingThatIsNotABindingSupport()
		{
			KingdomCatalogueRules.TryParseTally("water:9,food:9,roof:9,craft:2,spirit:3,moonlight:5", out var tally, out _);
			// The three binding kinds contribute nothing to lift; the unknown kind does, because a
			// third party inventing a new binding good would make every older catalogue unbuildable.
			Assert.AreEqual(10, KingdomCatalogueRules.LiftOf(tally));
			Assert.AreEqual(0, KingdomCatalogueRules.LiftOf(null));
		}

		// --- Validate -----------------------------------------------------------------------

		[Test]
		public void Validate_SaysNothingAboutNothing()
		{
			Assert.AreEqual(0, KingdomCatalogueRules.Validate(null, null).Count);
			Assert.AreEqual(0, KingdomCatalogueRules.Validate(new List<CatalogueEntry>(), null).Count);
		}

		[Test]
		public void Validate_IsSilentOnACatalogueWithNothingWrongWithIt()
		{
			// The anchor for every other case below: if this ever starts producing findings, one
			// of the checks has begun firing on correct data.
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("tent", KingdomPlotRules.PlotSize.Small, GrowthStage.Camp, "housing", "roof:2", 3, Successor: "hut"),
				Entry("hut", KingdomPlotRules.PlotSize.Small, GrowthStage.Camp, "housing", "roof:3", 6, Materials: "timber:6,mud:2"),
				Entry("catchment", KingdomPlotRules.PlotSize.Small, GrowthStage.Camp, "storage", "water:3", 8),
				Entry("cistern", KingdomPlotRules.PlotSize.Medium, GrowthStage.Steading, "storage", "water:8", 20),
				Entry("palisade", KingdomPlotRules.PlotSize.None, GrowthStage.Camp, "defense", null, 6, Defence: 3)
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, new List<string> { "common" });
			Assert.AreEqual(0, findings.Count, FirstMessage(findings));
			Assert.IsFalse(KingdomCatalogueRules.AnyFault(findings));
		}

		[Test]
		public void Validate_SkipsEntriesWithNoKeyAtAllRatherThanReportingThemForever()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry> { null, new CatalogueEntry(), Entry("hut") };
			Assert.AreEqual(0, KingdomCatalogueRules.Validate(entries, null).Count);
		}

		[Test]
		public void Validate_FaultsAKeyDeclaredTwiceInTheSamePass()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry> { Entry("hut"), Entry("hut") };
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "hut", "Key", CatalogueSeverity.Fault));
			Assert.IsTrue(KingdomCatalogueRules.AnyFault(findings));
		}

		[Test]
		public void Validate_FaultsAnImprovementIntoSomethingNoBuildingDeclares()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry> { Entry("hut", Successor: "palace") };
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "hut", "UpgradesTo", CatalogueSeverity.Fault));
			Assert.AreEqual(1, Count(findings, CatalogueSeverity.Fault));
		}

		[Test]
		public void Validate_FaultsAChainThatComesBackToItself()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("a", Successor: "b"),
				Entry("b", Successor: "c"),
				Entry("c", Successor: "a")
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "a", "UpgradesTo", CatalogueSeverity.Fault));
			Assert.IsTrue(Has(findings, "b", "UpgradesTo", CatalogueSeverity.Fault));
			Assert.IsTrue(Has(findings, "c", "UpgradesTo", CatalogueSeverity.Fault));
		}

		[Test]
		public void Validate_FaultsADesignThatImprovesIntoItself()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry> { Entry("hut", Successor: "hut") };
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "hut", "UpgradesTo", CatalogueSeverity.Fault));
		}

		[Test]
		public void Validate_FaultsAnImprovementOntoALargerPlot()
		{
			// Upgrades climb within a plot; sizes compete across plots. An S design that became an
			// M one in place would be standing on ground nobody ever cleared.
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("hut", KingdomPlotRules.PlotSize.Small, GrowthStage.Camp, "housing", "roof:3", 6, Successor: "house"),
				Entry("house", KingdomPlotRules.PlotSize.Medium, GrowthStage.Steading, "housing", "roof:8", 16)
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "hut", "UpgradesTo", CatalogueSeverity.Fault));
		}

		[Test]
		public void Validate_AcceptsAnImprovementThatStaysOnItsOwnPlot()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("hut", KingdomPlotRules.PlotSize.Small, GrowthStage.Camp, "housing", "roof:3", 6, Successor: "hutyard"),
				Entry("hutyard", KingdomPlotRules.PlotSize.Small, GrowthStage.Camp, "housing", "roof:5", 10)
			};
			Assert.AreEqual(0, KingdomCatalogueRules.Validate(entries, null).Count);
		}

		[Test]
		public void Validate_FaultsAnImprovementTheSettlementCouldHaveRaisedEarlier()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("late", KingdomPlotRules.PlotSize.Medium, GrowthStage.Village, "storage", "water:8", 20, Successor: "early"),
				Entry("early", KingdomPlotRules.PlotSize.Medium, GrowthStage.Steading, "storage", "water:9", 22)
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "late", "UpgradesTo", CatalogueSeverity.Fault));
		}

		[Test]
		public void Validate_NotesAnImprovementThatIsCheaperToRaiseFromNothing()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("dear", KingdomPlotRules.PlotSize.Small, GrowthStage.Camp, "housing", "roof:3", 20, Successor: "cheap"),
				Entry("cheap", KingdomPlotRules.PlotSize.Small, GrowthStage.Camp, "housing", "roof:5", 6)
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "dear", "UpgradesTo", CatalogueSeverity.Note));
			Assert.AreEqual(0, Count(findings, CatalogueSeverity.Fault));
		}

		[Test]
		public void Validate_NotesAnImprovementThatChangesWhatTheBuildingIsFor()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("shed", KingdomPlotRules.PlotSize.Small, GrowthStage.Camp, "housing", "roof:3", 6, Successor: "vat"),
				Entry("vat", KingdomPlotRules.PlotSize.Small, GrowthStage.Camp, "storage", "water:3", 8)
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "shed", "UpgradesTo", CatalogueSeverity.Note));
		}

		[Test]
		public void Validate_FaultsADesignThatWantsBothAPlotAndAPlaceOnTheWall()
		{
			// A Defence rating overrides the category at siting time, so this design goes on the
			// frontier line and the large plot it asked for is never laid.
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("keep", KingdomPlotRules.PlotSize.Large, GrowthStage.Town, "defense", "order:4", 40, Defence: 8)
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "keep", "Defence", CatalogueSeverity.Fault));
		}

		[Test]
		public void Validate_NotesFurnishingsOnAPlotWithNoInterior()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("yard", KingdomPlotRules.PlotSize.Small, GrowthStage.Camp, "civic", "spirit:1", 4,
					Open: true, Contents: "r_KingdomFurnishings_Civic")
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "yard", "Contents", CatalogueSeverity.Note));
		}

		[Test]
		public void Validate_LetsARoofedPlotNameItsFurnishings()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("hut", KingdomPlotRules.PlotSize.Small, GrowthStage.Camp, "housing", "roof:3", 6,
					Contents: "r_KingdomFurnishings_Dwelling")
			};
			Assert.AreEqual(0, KingdomCatalogueRules.Validate(entries, null).Count);
		}

		[Test]
		public void Validate_NotesAStageGateItsOwnPlotSizeWillRaiseAnyway()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("hall", KingdomPlotRules.PlotSize.Large, GrowthStage.Camp, "civic", "spirit:3", 40)
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "hall", "MinStage", CatalogueSeverity.Note));
		}

		[Test]
		public void Validate_FaultsACarriesItCannotRead()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry> { Entry("hut", Carries: "roof") };
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "hut", "Carries", CatalogueSeverity.Fault));
		}

		[Test]
		public void Validate_FaultsAMaterialsTheMaterialRulesRefuse()
		{
			// The vocabulary is KingdomMaterialRules'; this only reports its verdict, so a seventh
			// material never has to be added in two places.
			List<CatalogueEntry> entries = new List<CatalogueEntry> { Entry("hut", Materials: "chrome:4") };
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "hut", "Materials", CatalogueSeverity.Fault));
		}

		[Test]
		public void Validate_AcceptsEveryMaterialTheMaterialRulesName()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("hut", Materials: "mud:1,canvas:2,timber:3,stone:4,marble:5,scrap:6")
			};
			Assert.AreEqual(0, KingdomCatalogueRules.Validate(entries, null).Count);
		}

		[Test]
		public void Validate_NotesASupportNothingBindsOnWithoutRefusingIt()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry> { Entry("hut", Carries: "roof:3,moonlight:2") };
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "hut", "Carries", CatalogueSeverity.Note));
			Assert.AreEqual(0, Count(findings, CatalogueSeverity.Fault));
		}

		[Test]
		public void Validate_NotesAWorkThatTakesACrewAndAddsNothing()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("folly", KingdomPlotRules.PlotSize.Small, GrowthStage.Camp, "craft", null, 8, Staff: 3)
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "folly", "Carries", CatalogueSeverity.Note));
		}

		[Test]
		public void Validate_LetsAWallTakeACrewWithoutCarryingAnything()
		{
			// A watchtower's whole output is that somebody is standing on it looking outward.
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("tower", KingdomPlotRules.PlotSize.None, GrowthStage.Steading, "defense", null, 12, Staff: 2, Defence: 6)
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsFalse(Has(findings, "tower", "Carries", CatalogueSeverity.Note));
			Assert.AreEqual(0, Count(findings, CatalogueSeverity.Fault));
		}

		[Test]
		public void Validate_NotesAManningItDoesNotKnow()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("mill", KingdomPlotRules.PlotSize.Small, GrowthStage.Camp, "power", "craft:1", 8, Staff: 3, Manning: "rota")
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "mill", "Manning", CatalogueSeverity.Note));
		}

		[Test]
		public void Validate_NotesAManningSettingOnADesignThatWantsNoCrew()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("cairn", KingdomPlotRules.PlotSize.Small, GrowthStage.Camp, "memorial", "spirit:1", 5, Manning: "threshold")
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "cairn", "Manning", CatalogueSeverity.Note));
		}

		[Test]
		public void Validate_NotesACategoryNoDistrictClaims()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("odd", KingdomPlotRules.PlotSize.Small, GrowthStage.Camp, "menagerie", "spirit:1", 5)
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, "odd", "Category", CatalogueSeverity.Note));
		}

		[Test]
		public void Validate_NotesAStyleBuiltForButDeclaredByNoStyleElement()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry> { Entry("hut", Styles: "common,brackish") };
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, new List<string> { "common" });
			Assert.IsTrue(Has(findings, null, "Styles", CatalogueSeverity.Note));
		}

		[Test]
		public void Validate_NotesAStyleNoDesignIsOfferedTo()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry> { Entry("hut", Styles: "common") };
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, new List<string> { "common", "gyre" });
			Assert.IsTrue(Has(findings, null, "style", CatalogueSeverity.Note));
		}

		[Test]
		public void Validate_TreatsOneAllDesignAsAnOfferToEveryStyle()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("hut", Styles: "common"),
				Entry("fire", KingdomPlotRules.PlotSize.Small, GrowthStage.Camp, "civic", "spirit:1", 2, Styles: "all")
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, new List<string> { "common", "gyre" });
			Assert.IsFalse(Has(findings, null, "style", CatalogueSeverity.Note));
		}

		[Test]
		public void Validate_SaysNothingAboutStylesWhenItWasHandedNoStyleList()
		{
			// Null is "I did not look them up", not "none are declared". Reporting every style as
			// unknown there would bury the findings that matter.
			List<CatalogueEntry> entries = new List<CatalogueEntry> { Entry("hut", Styles: "common,brackish") };
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsFalse(Has(findings, null, "Styles", CatalogueSeverity.Note));
			Assert.IsFalse(Has(findings, null, "style", CatalogueSeverity.Note));
		}

		[Test]
		public void Validate_NotesAFamilyACampCannotReachAtAll()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("tent", KingdomPlotRules.PlotSize.Small, GrowthStage.Camp, "housing", "roof:2", 3),
				Entry("scriptorium", KingdomPlotRules.PlotSize.Medium, GrowthStage.Village, "knowledge", "learning:6", 26)
			};
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, null);
			Assert.IsTrue(Has(findings, null, "MinStage", CatalogueSeverity.Note));
			Assert.AreEqual(1, Count(findings, CatalogueSeverity.Note));
		}

		[Test]
		public void Validate_SaysNothingAboutAFamilyThatDoesOpenAtCamp()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry>
			{
				Entry("shelf", KingdomPlotRules.PlotSize.Small, GrowthStage.Camp, "knowledge", "learning:1", 10),
				Entry("scriptorium", KingdomPlotRules.PlotSize.Medium, GrowthStage.Village, "knowledge", "learning:6", 26)
			};
			Assert.AreEqual(0, KingdomCatalogueRules.Validate(entries, null).Count);
		}

		[Test]
		public void AnyFault_SeparatesTheTwoSeverities()
		{
			Assert.IsFalse(KingdomCatalogueRules.AnyFault(null));
			Assert.IsFalse(KingdomCatalogueRules.AnyFault(new List<CatalogueFinding>()));
			Assert.IsFalse(KingdomCatalogueRules.AnyFault(new List<CatalogueFinding>
			{
				new CatalogueFinding("a", "Plot", CatalogueSeverity.Note, "x")
			}));
			Assert.IsTrue(KingdomCatalogueRules.AnyFault(new List<CatalogueFinding>
			{
				new CatalogueFinding("a", "Plot", CatalogueSeverity.Note, "x"),
				new CatalogueFinding("b", "Plot", CatalogueSeverity.Fault, "y")
			}));
		}
	}
}
#endif
