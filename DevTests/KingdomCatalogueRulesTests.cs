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
			Assert.AreEqual(20, KingdomCatalogueRules.Equilibrium(20, 30, 40, 0, 0));
			Assert.AreEqual(20, KingdomCatalogueRules.Equilibrium(40, 20, 30, 0, 0));
			Assert.AreEqual(20, KingdomCatalogueRules.Equilibrium(30, 40, 20, 0, 0));
		}

		[Test]
		public void Equilibrium_NeverFallsBelowTheFloor()
		{
			Assert.AreEqual(KingdomCatalogueRules.FloorLevel, KingdomCatalogueRules.Equilibrium(0, 0, 0, 0, 0));
			Assert.AreEqual(KingdomCatalogueRules.FloorLevel, KingdomCatalogueRules.Equilibrium(40, 0, 40, 0, 0));
			Assert.AreEqual(KingdomCatalogueRules.FloorLevel, KingdomCatalogueRules.Equilibrium(2, 2, 2, 0, 0));
			// A negative binding total is arithmetic nobody intended, and it still may not push
			// the settlement under its floor.
			Assert.AreEqual(KingdomCatalogueRules.FloorLevel, KingdomCatalogueRules.Equilibrium(-5, 40, 40, 0, 0));
		}

		[Test]
		public void Equilibrium_LiftsTheLevelButOnlyToItsCap()
		{
			// Cap is half the binding level: twenty binding takes at most ten of lift.
			Assert.AreEqual(23, KingdomCatalogueRules.Equilibrium(20, 20, 20, 3, 0));
			Assert.AreEqual(30, KingdomCatalogueRules.Equilibrium(20, 20, 20, 10, 0));
			Assert.AreEqual(30, KingdomCatalogueRules.Equilibrium(20, 20, 20, 11, 0));
			Assert.AreEqual(30, KingdomCatalogueRules.Equilibrium(20, 20, 20, 900, 0));
		}

		[Test]
		public void Equilibrium_CannotShrineItsWayPastNoWaterAtAll()
		{
			// Zero binding means zero cap: comfort is worth nothing when the casks are dry, and
			// the floor is what is left.
			Assert.AreEqual(KingdomCatalogueRules.FloorLevel, KingdomCatalogueRules.Equilibrium(0, 90, 90, 400, 0));
		}

		[Test]
		public void Equilibrium_TreatsANegativeLiftAsNone()
		{
			Assert.AreEqual(20, KingdomCatalogueRules.Equilibrium(20, 20, 20, -50, 0));
		}

		// --- The shade a named notable carries (brief: notable tastes, leader traits, Add. 4) ---

		[Test]
		public void Equilibrium_ReadsTheNotablesShadeAsComfortOfItsOwn()
		{
			// The number the ceremony used to compute and log. Three points of shade with no
			// lifting work standing is three more settlers, exactly as three points of shrine is.
			Assert.AreEqual(23, KingdomCatalogueRules.Equilibrium(20, 20, 20, 0, 3));
			Assert.AreEqual(26, KingdomCatalogueRules.Equilibrium(20, 20, 20, 3, 3));
		}

		[Test]
		public void Equilibrium_BindsTheShadeWithTheSameLiftCap()
		{
			// A notable is texture, not a way past the water: twenty binding takes ten of lift and
			// shade together, and not one more whichever half it came from.
			Assert.AreEqual(30, KingdomCatalogueRules.Equilibrium(20, 20, 20, 8, 8));
			Assert.AreEqual(30, KingdomCatalogueRules.Equilibrium(20, 20, 20, 0, 900));
			Assert.AreEqual(KingdomCatalogueRules.FloorLevel, KingdomCatalogueRules.Equilibrium(0, 90, 90, 0, 40));
		}

		[Test]
		public void Equilibrium_NeverLetsAShadeTakeTheLevelBelowWhatTheWorksCarry()
		{
			// There is no negative shade in the shipped tables, and if one ever arrives it costs
			// the settlement nothing: an unmet taste means their default, never a penalty.
			Assert.AreEqual(20, KingdomCatalogueRules.Equilibrium(20, 20, 20, 0, -50));
			// Neither half may eat the other: a negative shade leaves a standing shrine alone.
			Assert.AreEqual(23, KingdomCatalogueRules.Equilibrium(20, 20, 20, 3, -1));
			Assert.AreEqual(23, KingdomCatalogueRules.Equilibrium(20, 20, 20, -50, 3));
		}

		// --- A household's yard trade (brief: yard trades) --------------------------------------

		[Test]
		public void FoldShade_LandsAYardTradesShadesWithoutCountingASecondThingStanding()
		{
			// The vine lattice's own line: food:1 is a binding good and reaches the pool, and the
			// house it stands behind is still one work rather than two.
			KingdomCatalogueRules.SupportTally house = KingdomCatalogueRules.FoldWork(
				default(KingdomCatalogueRules.SupportTally), Parse("roof:4"), 100);
			Assert.AreEqual(1, house.Works);
			KingdomCatalogueRules.SupportTally worked = KingdomCatalogueRules.FoldShade(house, Parse("food:1"), 100);
			Assert.AreEqual(1, worked.Food, "a vine lattice feeds the settlement it stands in");
			Assert.AreEqual(4, worked.Roof);
			Assert.AreEqual(1, worked.Works, "a yard trade is a household's sideline, not a second work");
		}

		[Test]
		public void FoldShade_SendsATradesLiftToTheLiftingHalf()
		{
			KingdomCatalogueRules.SupportTally tally = KingdomCatalogueRules.FoldShade(
				default(KingdomCatalogueRules.SupportTally), Parse("craft:1,learning:1"), 100);
			Assert.AreEqual(2, tally.Lift);
			Assert.AreEqual(0, tally.Works);
		}

		[Test]
		public void FoldShade_ScalesWithTheConditionOfTheHouseItBelongsTo()
		{
			// Addendum 10(b) reaches a sideline the same way it reaches a work: a half-ruined
			// house's yard is worth half of what it makes.
			Assert.AreEqual(1, KingdomCatalogueRules.FoldShade(
				default(KingdomCatalogueRules.SupportTally), Parse("food:2"), 50).Food);
			Assert.AreEqual(0, KingdomCatalogueRules.FoldShade(
				default(KingdomCatalogueRules.SupportTally), Parse("food:1"), 0).Food);
		}

		private static System.Collections.Generic.List<KindAmount> Parse(string Source)
		{
			System.Collections.Generic.List<KindAmount> tally;
			string error;
			Assert.IsTrue(KingdomCatalogueRules.TryParseTally(Source, out tally, out error), error);
			return tally;
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
				// Mirrors the shipped water lane after Addendum 11(a): the producer declares the
				// water and the vessel declares nothing, and a staffless design carrying nothing
				// is deliberately NOT a finding (only a CREWED one that carries nothing is).
				// `larder` is here because storage now opens above Camp on the water side, and
				// the whole-catalogue check wants every category within a camp's reach - which is
				// exactly the arrangement the shipped file has.
				Entry("larder", KingdomPlotRules.PlotSize.Small, GrowthStage.Camp, "storage", "food:2", 4),
				Entry("catchment", KingdomPlotRules.PlotSize.Small, GrowthStage.Steading, "storage", "water:3", 8),
				Entry("cistern", KingdomPlotRules.PlotSize.Medium, GrowthStage.Steading, "storage", null, 16),
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

		/// <summary>
		/// A MIS-SPELLED REFUSAL is the typo the tag idiom made possible, and it is invisible
		/// without this: `Styles="all,!eatr"` refuses nobody, so the design goes everywhere while
		/// reading to its author as a restriction. Collecting the negated name catches it with the
		/// check that was already there.
		/// </summary>
		[Test]
		public void Validate_NotesAStyleThatIsONLYREFUSEDAndDeclaredByNoStyleElement()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry> { Entry("hut", Styles: "all,!eatr") };
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, new List<string> { "common", "eater" });
			Assert.IsTrue(Has(findings, null, "Styles", CatalogueSeverity.Note));
		}

		/// <summary>A list of nothing but refusals is offered to every style there is, so it
		/// answers the unreferenced-style half of the check for all of them at once — exactly as
		/// <c>Styles="all"</c> does, because that is what it means.</summary>
		[Test]
		public void Validate_TreatsAPureRefusalListAsAnOfferToEveryOtherStyle()
		{
			List<CatalogueEntry> entries = new List<CatalogueEntry> { Entry("hut", Styles: "!eater") };
			List<CatalogueFinding> findings = KingdomCatalogueRules.Validate(entries, new List<string> { "common", "eater", "gyre" });
			Assert.IsFalse(Has(findings, null, "style", CatalogueSeverity.Note));
			Assert.IsFalse(Has(findings, null, "Styles", CatalogueSeverity.Note), "eater IS referred to, by being refused");
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
