#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;
using Mark = ThousandAndFirst.KingdomLayoutRules.LayoutMark;
using Purpose = ThousandAndFirst.KingdomLayoutRules.LayoutPurpose;
using Size = ThousandAndFirst.KingdomPlotRules.PlotSize;
using Transition = ThousandAndFirst.KingdomOfficeRules.OfficeTransition;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Reach (Addendum 6): what a work's size and tier let it carry, what falls inside that, and
	/// what a great work without a head does instead. Every gate, every tiebreak and every named
	/// line is asserted directly, so dropping a clamp, flipping a comparison or losing a band's
	/// wording fails here rather than turning into a quarter that silently shades the wrong ground.
	/// </summary>
	public class KingdomReachRulesTests
	{
		private static List<Mark> Marks(params int[] Coordinates)
		{
			List<Mark> marks = new List<Mark>();
			for (int i = 0; i + 1 < Coordinates.Length; i += 2)
			{
				marks.Add(new Mark(Coordinates[i], Coordinates[i + 1], Purpose.Housing));
			}
			return marks;
		}

		private static List<KindAmount> Lifts(params object[] Pairs)
		{
			List<KindAmount> lifts = new List<KindAmount>();
			for (int i = 0; i + 1 < Pairs.Length; i += 2)
			{
				lifts.Add(new KindAmount((string)Pairs[i], (int)Pairs[i + 1]));
			}
			return lifts;
		}

		// --- The ladder: size sets the band -----------------------------------------------------

		[TestCase(Size.None, ReachBand.Plot)]
		[TestCase(Size.Small, ReachBand.Plot)]
		[TestCase(Size.Medium, ReachBand.Quarter)]
		[TestCase(Size.Large, ReachBand.Zone)]
		[TestCase(Size.Huge, ReachBand.City)]
		public void BandForSize_IsTheAddendumsOwnLadder(Size size, ReachBand expected)
		{
			Assert.AreEqual(expected, KingdomReachRules.BandForSize(size));
		}

		[TestCase(Size.Small, 0, 3)]
		[TestCase(Size.Small, 2, 3)]
		[TestCase(Size.Medium, 1, 2)]
		[TestCase(Size.Large, 2, 3)]
		public void Derive_TierNeverMovesABandBelowTheGreatWork(Size size, int index, int count)
		{
			Assert.AreEqual(KingdomReachRules.BandForSize(size), KingdomReachRules.Derive(size, index, count));
		}

		[Test]
		public void Derive_TheLastLinkOfAGreatWorksChainReachesTheRealm()
		{
			Assert.AreEqual(ReachBand.Realm, KingdomReachRules.Derive(Size.Huge, 1, 2));
			Assert.AreEqual(ReachBand.Realm, KingdomReachRules.Derive(Size.Huge, 2, 3));
		}

		[TestCase(0, 2)]
		[TestCase(1, 3)]
		[TestCase(0, 1)]
		[TestCase(0, 0)]
		public void Derive_AGreatWorkThatIsNotTheLastLinkReachesTheCity(int index, int count)
		{
			Assert.AreEqual(ReachBand.City, KingdomReachRules.Derive(Size.Huge, index, count));
		}

		[Test]
		public void Derive_ANegativeTierIndexReadsAsTheFirstLink()
		{
			Assert.AreEqual(ReachBand.City, KingdomReachRules.Derive(Size.Huge, -4, 3));
		}

		// --- The Reach attribute ----------------------------------------------------------------

		[TestCase("plot", ReachBand.Plot)]
		[TestCase("QUARTER", ReachBand.Quarter)]
		[TestCase("  zone  ", ReachBand.Zone)]
		[TestCase("City", ReachBand.City)]
		[TestCase("realm", ReachBand.Realm)]
		public void TryParseBand_FoldsCaseAndWhitespace(string raw, ReachBand expected)
		{
			ReachBand band;
			string error;
			Assert.IsTrue(KingdomReachRules.TryParseBand(raw, out band, out error));
			Assert.AreEqual(expected, band);
			Assert.IsNull(error);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		public void TryParseBand_BlankIsDeriveMeAndNotAFault(string raw)
		{
			ReachBand band;
			string error;
			Assert.IsFalse(KingdomReachRules.TryParseBand(raw, out band, out error));
			Assert.IsNull(error);
		}

		[Test]
		public void TryParseBand_AnUnknownWordFailsAndNamesEveryBand()
		{
			ReachBand band;
			string error;
			Assert.IsFalse(KingdomReachRules.TryParseBand("region", out band, out error));
			Assert.IsNotNull(error);
			StringAssert.Contains("region", error);
			for (int i = 0; i < KingdomReachRules.BandNames.Length; i++)
			{
				StringAssert.Contains(KingdomReachRules.BandNames[i], error);
			}
		}

		[Test]
		public void BandNames_LineUpWithTheEnumTheyAreRead()
		{
			Assert.AreEqual(5, KingdomReachRules.BandNames.Length);
			Assert.AreEqual("plot", KingdomReachRules.BandName(ReachBand.Plot));
			Assert.AreEqual("quarter", KingdomReachRules.BandName(ReachBand.Quarter));
			Assert.AreEqual("zone", KingdomReachRules.BandName(ReachBand.Zone));
			Assert.AreEqual("city", KingdomReachRules.BandName(ReachBand.City));
			Assert.AreEqual("realm", KingdomReachRules.BandName(ReachBand.Realm));
		}

		[Test]
		public void Resolve_ADeclaredReachBeatsTheDerivation()
		{
			bool overridden;
			string error;
			Assert.AreEqual(ReachBand.Realm, KingdomReachRules.Resolve("realm", Size.Small, 0, 1, out overridden, out error));
			Assert.IsTrue(overridden);
			Assert.IsNull(error);
		}

		[Test]
		public void Resolve_ABadReachKeepsTheDerivationAndSaysWhy()
		{
			bool overridden;
			string error;
			Assert.AreEqual(ReachBand.Quarter, KingdomReachRules.Resolve("everywhere", Size.Medium, 0, 1, out overridden, out error));
			Assert.IsFalse(overridden);
			Assert.IsNotNull(error);
		}

		[Test]
		public void Resolve_NoAttributeDerivesAndReportsNothing()
		{
			bool overridden;
			string error;
			Assert.AreEqual(ReachBand.Zone, KingdomReachRules.Resolve(null, Size.Large, 0, 1, out overridden, out error));
			Assert.IsFalse(overridden);
			Assert.IsNull(error);
		}

		// --- Covering ----------------------------------------------------------------------------

		[TestCase(ReachBand.Plot, ReachRelation.SamePlot)]
		[TestCase(ReachBand.Quarter, ReachRelation.SameQuarter)]
		[TestCase(ReachBand.Zone, ReachRelation.SameZone)]
		[TestCase(ReachBand.City, ReachRelation.SameCity)]
		[TestCase(ReachBand.Realm, ReachRelation.SameRealm)]
		public void RelationRequired_IsTheBandsOwnEdge(ReachBand band, ReachRelation expected)
		{
			Assert.AreEqual(expected, KingdomReachRules.RelationRequired(band));
			Assert.IsTrue(KingdomReachRules.Covers(band, expected));
		}

		[TestCase(ReachBand.Plot, ReachRelation.SameQuarter)]
		[TestCase(ReachBand.Quarter, ReachRelation.SameZone)]
		[TestCase(ReachBand.Zone, ReachRelation.SameCity)]
		[TestCase(ReachBand.City, ReachRelation.SameRealm)]
		public void Covers_IsFalseOneStepPastTheBandsEdge(ReachBand band, ReachRelation where)
		{
			Assert.IsFalse(KingdomReachRules.Covers(band, where));
		}

		[TestCase(ReachBand.Plot)]
		[TestCase(ReachBand.Quarter)]
		[TestCase(ReachBand.Zone)]
		[TestCase(ReachBand.City)]
		[TestCase(ReachBand.Realm)]
		public void Covers_GroundTheRealmDoesNotHoldIsNeverReached(ReachBand band)
		{
			Assert.IsFalse(KingdomReachRules.Covers(band, ReachRelation.Elsewhere));
		}

		[Test]
		public void Covers_ANearerPlaceIsAlwaysStillCovered()
		{
			Assert.IsTrue(KingdomReachRules.Covers(ReachBand.Quarter, ReachRelation.SamePlot));
			Assert.IsTrue(KingdomReachRules.Covers(ReachBand.City, ReachRelation.SameZone));
			Assert.IsTrue(KingdomReachRules.Covers(ReachBand.Realm, ReachRelation.SamePlot));
		}

		[Test]
		public void RelationAt_NearerFactsWin()
		{
			Assert.AreEqual(ReachRelation.SamePlot, KingdomReachRules.RelationAt(false, false, false, false, OnFootprint: true));
			Assert.AreEqual(ReachRelation.SameQuarter, KingdomReachRules.RelationAt(true, true, true, InQuarter: true, OnFootprint: false));
			Assert.AreEqual(ReachRelation.SameZone, KingdomReachRules.RelationAt(true, true, true, InQuarter: false, OnFootprint: false));
			Assert.AreEqual(ReachRelation.SameCity, KingdomReachRules.RelationAt(true, true, false, false, false));
			Assert.AreEqual(ReachRelation.SameRealm, KingdomReachRules.RelationAt(true, false, false, false, false));
			Assert.AreEqual(ReachRelation.Elsewhere, KingdomReachRules.RelationAt(false, false, false, false, false));
		}

		// --- The quarter, measured ---------------------------------------------------------------

		[TestCase(0, KingdomReachRules.QuarterBaseRadius)]
		[TestCase(1, KingdomReachRules.QuarterBaseRadius + KingdomReachRules.QuarterRadiusPerTier)]
		[TestCase(-3, KingdomReachRules.QuarterBaseRadius)]
		[TestCase(50, KingdomReachRules.QuarterRadiusCap)]
		public void QuarterRadius_GrowsWithTierAndIsClampedBothWays(int tier, int expected)
		{
			Assert.AreEqual(expected, KingdomReachRules.QuarterRadius(tier));
		}

		[Test]
		public void QuarterMarks_LinksTransitivelyAcrossTheGap()
		{
			// Three marks each one link apart: the third belongs to the cluster even though it is
			// twice the link distance from the work.
			List<Mark> marks = Marks(10, 10, 16, 10, 22, 10);
			List<int> cluster = KingdomReachRules.QuarterMarks(marks, 10, 10, KingdomReachRules.QuarterLinkCells);
			CollectionAssert.AreEqual(new[] { 0, 1, 2 }, cluster);
		}

		[Test]
		public void QuarterMarks_LeavesOutGroundPastTheLink()
		{
			List<Mark> marks = Marks(10, 10, 17, 10);
			List<int> cluster = KingdomReachRules.QuarterMarks(marks, 10, 10, KingdomReachRules.QuarterLinkCells);
			CollectionAssert.AreEqual(new[] { 0 }, cluster);
		}

		[Test]
		public void QuarterMarks_AWorkStandingAloneHasNoCluster()
		{
			Assert.AreEqual(0, KingdomReachRules.QuarterMarks(Marks(40, 20), 10, 10, KingdomReachRules.QuarterLinkCells).Count);
			Assert.AreEqual(0, KingdomReachRules.QuarterMarks(null, 10, 10, KingdomReachRules.QuarterLinkCells).Count);
			Assert.AreEqual(0, KingdomReachRules.QuarterMarks(Marks(10, 10), 10, 10, 0).Count);
		}

		[Test]
		public void InQuarter_ReachesPastBuiltGroundByTheRadiusAndNoFurther()
		{
			// One neighbour six cells off; the resident stands four cells past THAT, which is
			// inside a first tier's radius and outside nothing else.
			List<Mark> marks = Marks(10, 10, 16, 10);
			Assert.IsTrue(KingdomReachRules.InQuarter(marks, 10, 10, 20, 10, KingdomReachRules.QuarterLinkCells, KingdomReachRules.QuarterRadius(0)));
			Assert.IsFalse(KingdomReachRules.InQuarter(marks, 10, 10, 21, 10, KingdomReachRules.QuarterLinkCells, KingdomReachRules.QuarterRadius(0)));
		}

		[Test]
		public void InQuarter_ATierFurtherAlongTheChainCarriesFurther()
		{
			List<Mark> marks = Marks(10, 10, 16, 10);
			Assert.IsTrue(KingdomReachRules.InQuarter(marks, 10, 10, 22, 10, KingdomReachRules.QuarterLinkCells, KingdomReachRules.QuarterRadius(1)));
		}

		[Test]
		public void InQuarter_TheWorksOwnRadiusHoldsWithNothingBuiltAroundIt()
		{
			Assert.IsTrue(KingdomReachRules.InQuarter(null, 10, 10, 13, 10, KingdomReachRules.QuarterLinkCells, 4));
			Assert.IsFalse(KingdomReachRules.InQuarter(null, 10, 10, 15, 10, KingdomReachRules.QuarterLinkCells, 4));
		}

		[Test]
		public void InQuarter_ANeighbouringClusterIsNotThisQuarter()
		{
			// Two clusters twenty cells apart. The far one's own ground is not shaded by this work,
			// however much built ground stands there.
			List<Mark> marks = Marks(10, 10, 30, 10, 34, 10);
			Assert.IsFalse(KingdomReachRules.InQuarter(marks, 10, 10, 32, 10, KingdomReachRules.QuarterLinkCells, KingdomReachRules.QuarterRadius(0)));
		}

		[Test]
		public void InQuarter_ANegativeRadiusShadesOnlyTheWorksOwnCell()
		{
			Assert.IsTrue(KingdomReachRules.InQuarter(null, 10, 10, 10, 10, KingdomReachRules.QuarterLinkCells, -5));
			Assert.IsFalse(KingdomReachRules.InQuarter(null, 10, 10, 11, 10, KingdomReachRules.QuarterLinkCells, -5));
		}

		// --- What scopes, and how much of it lands ------------------------------------------------

		[TestCase("water", false)]
		[TestCase("food", false)]
		[TestCase("roof", false)]
		[TestCase("WATER", false)]
		[TestCase("spirit", true)]
		[TestCase("learning", true)]
		[TestCase("craft", true)]
		[TestCase("order", true)]
		[TestCase("luxury", true)]
		[TestCase("someone:elses", true)]
		public void ScopedByReach_BindingGoodsStayCitywideAndEverythingElseScopes(string kind, bool expected)
		{
			Assert.AreEqual(expected, KingdomReachRules.ScopedByReach(kind));
		}

		[TestCase(6, 100, 6)]
		[TestCase(6, 50, 3)]
		[TestCase(6, 0, 0)]
		[TestCase(6, -20, 0)]
		[TestCase(0, 100, 0)]
		[TestCase(-4, 100, 0)]
		[TestCase(2, 150, 3)]
		public void Scaled_FollowsHowWellTheWorkIsRunning(int amount, int percent, int expected)
		{
			Assert.AreEqual(expected, KingdomReachRules.Scaled(amount, percent));
		}

		[Test]
		public void Scaled_AWorkRunningAtAllKeepsAPointOfWhatItDeclares()
		{
			Assert.AreEqual(1, KingdomReachRules.Scaled(1, 25));
		}

		// --- The ground's character ----------------------------------------------------------------

		[Test]
		public void Character_SumsRepeatsAndNamesTheLoudest()
		{
			GroundCharacter character = KingdomReachRules.Character(Lifts("spirit", 2, "learning", 6, "spirit", 3));
			Assert.AreEqual(11, character.Total);
			Assert.AreEqual("learning", character.Dominant);
			Assert.AreEqual(6, character.DominantAmount);
			Assert.AreEqual(2, character.Lifts.Count);
		}

		[Test]
		public void Character_IgnoresTheBindingPoolsEntirely()
		{
			GroundCharacter character = KingdomReachRules.Character(Lifts("water", 40, "food", 12, "roof", 9, "order", 2));
			Assert.AreEqual(2, character.Total);
			Assert.AreEqual("order", character.Dominant);
			Assert.AreEqual(1, character.Lifts.Count);
		}

		[Test]
		public void Character_ListsInTheCataloguesOwnLiftOrder()
		{
			GroundCharacter character = KingdomReachRules.Character(Lifts("luxury", 1, "spirit", 1, "craft", 1));
			Assert.AreEqual("craft", character.Lifts[0].Kind);
			Assert.AreEqual("spirit", character.Lifts[1].Kind);
			Assert.AreEqual("luxury", character.Lifts[2].Kind);
		}

		[Test]
		public void Character_ATieGoesToTheEarlierLiftRatherThanToWhicheverWasSeenFirst()
		{
			GroundCharacter character = KingdomReachRules.Character(Lifts("luxury", 5, "craft", 5));
			Assert.AreEqual("craft", character.Dominant);
		}

		[Test]
		public void Character_AnotherModsGoodIsCountedAndListedAfterTheKnownOnes()
		{
			GroundCharacter character = KingdomReachRules.Character(Lifts("them:song", 9, "craft", 1));
			Assert.AreEqual(10, character.Total);
			Assert.AreEqual("them:song", character.Dominant);
			Assert.AreEqual("craft", character.Lifts[0].Kind);
			Assert.AreEqual("them:song", character.Lifts[1].Kind);
		}

		[Test]
		public void Character_FoldsCaseAndDropsAmountsThatAreNotThere()
		{
			GroundCharacter character = KingdomReachRules.Character(Lifts("Spirit", 3, "spirit", 0, "learning", -2, "  ", 4));
			Assert.AreEqual(1, character.Lifts.Count);
			Assert.AreEqual("spirit", character.Lifts[0].Kind);
			Assert.AreEqual(3, character.Lifts[0].Amount);
		}

		[Test]
		public void Character_GroundNothingReachesNamesNobody()
		{
			GroundCharacter character = KingdomReachRules.Character(null);
			Assert.AreEqual(0, character.Total);
			Assert.IsNull(character.Dominant);
			Assert.AreEqual(0, character.Lifts.Count);
		}

		[TestCase("spirit", "the temple quarter")]
		[TestCase("learning", "the scribes' quarter")]
		[TestCase("craft", "the workers' quarter")]
		[TestCase("order", "the watch's quarter")]
		[TestCase("luxury", "the fine quarter")]
		[TestCase(null, "ordinary ground")]
		[TestCase("them:song", "a quarter of its own")]
		public void QuarterName_NamesTheGroundTheWayThePeopleThereWould(string kind, string expected)
		{
			Assert.AreEqual(expected, KingdomReachRules.QuarterName(kind));
		}

		[Test]
		public void QuarterLine_NamesTheQuarterAndWhatShadesIt()
		{
			string line = KingdomReachRules.QuarterLine(KingdomReachRules.Character(Lifts("spirit", 5, "learning", 2)));
			StringAssert.Contains("the temple quarter", line);
			StringAssert.Contains("faith 5", line);
			StringAssert.Contains("learning 2", line);
		}

		[Test]
		public void QuarterLine_UnshadedGroundStillSaysSomething()
		{
			string line = KingdomReachRules.QuarterLine(KingdomReachRules.Character(null));
			StringAssert.Contains("ordinary ground", line);
			Assert.IsFalse(string.IsNullOrEmpty(KingdomReachRules.QuarterLine(null)));
		}

		[Test]
		public void ReachClause_SaysTheGreatWorkNeedsSomebodyAtItsHead()
		{
			StringAssert.Contains("heads it", KingdomReachRules.ReachClause(ReachBand.City));
			StringAssert.Contains("heads it", KingdomReachRules.ReachClause(ReachBand.Realm));
			Assert.IsFalse(KingdomReachRules.ReachClause(ReachBand.Plot).Contains("heads it"));
		}

		// --- The seat --------------------------------------------------------------------------

		[TestCase(ReachBand.Plot, false)]
		[TestCase(ReachBand.Quarter, false)]
		[TestCase(ReachBand.Zone, false)]
		[TestCase(ReachBand.City, true)]
		[TestCase(ReachBand.Realm, true)]
		public void RequiresSeat_IsTheGreatWorksAloneRule(ReachBand band, bool expected)
		{
			Assert.AreEqual(expected, KingdomReachRules.RequiresSeat(band));
		}

		[TestCase(ReachBand.City, ReachBand.Zone)]
		[TestCase(ReachBand.Realm, ReachBand.Zone)]
		public void Unheaded_AGreatWorkWithNoKeeperKeepsItsOwnZone(ReachBand band, ReachBand expected)
		{
			Assert.AreEqual(expected, KingdomReachRules.Unheaded(band));
		}

		[TestCase(ReachBand.Plot)]
		[TestCase(ReachBand.Quarter)]
		[TestCase(ReachBand.Zone)]
		public void Unheaded_LeavesEverySmallerWorkExactlyAsItWas(ReachBand band)
		{
			Assert.AreEqual(band, KingdomReachRules.Unheaded(band));
		}

		[TestCase("faith", "keeper of rites")]
		[TestCase("knowledge", "archivist")]
		[TestCase("CRAFT", "master of the yard")]
		[TestCase("food", "reeve of the fields")]
		[TestCase("storage", "warden of the stores")]
		[TestCase("defense", "captain of the watch")]
		[TestCase("defence", "captain of the watch")]
		[TestCase("memorial", "keeper of the names")]
		[TestCase("them:hall", "keeper")]
		[TestCase(null, "keeper")]
		public void SeatTitle_NamesTheOfficeWithoutInventingOne(string category, string expected)
		{
			Assert.AreEqual(expected, KingdomReachRules.SeatTitle(category));
		}

		[Test]
		public void SeatFitness_ReadsTheAttributeTheWorkActuallyAsksFor()
		{
			// One candidate strong, one clever: the scriptorium wants the clever one and the
			// mason's yard the strong one, from the same two people.
			int strongAtCraft = KingdomReachRules.SeatFitness("craft", 20, 10, 10, 10, 10, 10);
			int cleverAtCraft = KingdomReachRules.SeatFitness("craft", 10, 10, 10, 20, 10, 10);
			int strongAtKnowledge = KingdomReachRules.SeatFitness("knowledge", 20, 10, 10, 10, 10, 10);
			int cleverAtKnowledge = KingdomReachRules.SeatFitness("knowledge", 10, 10, 10, 20, 10, 10);
			Assert.Greater(strongAtCraft, cleverAtCraft);
			Assert.Greater(cleverAtKnowledge, strongAtKnowledge);
		}

		[Test]
		public void SeatFitness_TheGoverningAttributeCountsTwiceAndTheSecondOnce()
		{
			// Faith: willpower governs, ego seconds. Ten points of willpower are worth twice ten
			// points of ego, and nothing else on the sheet moves the number at all.
			Assert.AreEqual(30, KingdomReachRules.SeatFitness("faith", 0, 0, 0, 0, 10, 10));
			Assert.AreEqual(20, KingdomReachRules.SeatFitness("faith", 0, 0, 0, 0, 10, 0));
			Assert.AreEqual(10, KingdomReachRules.SeatFitness("faith", 0, 0, 0, 0, 0, 10));
			Assert.AreEqual(0, KingdomReachRules.SeatFitness("faith", 99, 99, 99, 99, 0, 0));
		}

		[Test]
		public void SeatFitness_AnUnknownPurposeAsksWhoTheSettlementListensTo()
		{
			Assert.AreEqual(KingdomReachRules.SeatFitness("them:hall", 0, 0, 0, 0, 4, 10),
				(2 * 10) + 4);
		}

		[Test]
		public void SeatFitness_NeverGoesNegative()
		{
			Assert.AreEqual(0, KingdomReachRules.SeatFitness("faith", -9, -9, -9, -9, -9, -9));
		}

		[TestCase(20, 20, false)]
		[TestCase(20, 22, false)]
		[TestCase(20, 23, true)]
		[TestCase(20, 40, true)]
		public void ShouldUnseat_ASeatedNotableIsOnlyReplacedByAPlainlyBetterOne(int seated, int challenger, bool expected)
		{
			Assert.AreEqual(expected, KingdomReachRules.ShouldUnseat(seated, challenger));
		}

		[Test]
		public void ShouldUnseat_AnEmptySeatIsTakenByAnybody()
		{
			Assert.IsTrue(KingdomReachRules.ShouldUnseat(-1, 0));
		}

		[Test]
		public void ShouldUnseat_TheMarginIsTheThingBeingTested()
		{
			Assert.AreEqual(3, KingdomReachRules.SeatUnseatMargin);
		}

		[Test]
		public void UnheadedLine_NamesTheWorkAndTheOfficeThatWouldLiftIt()
		{
			string line = KingdomReachRules.UnheadedLine("the temple", "keeper of rites");
			StringAssert.Contains("the temple", line);
			StringAssert.Contains("keeper of rites", line);
			Assert.IsFalse(string.IsNullOrEmpty(KingdomReachRules.UnheadedLine(null, null)));
		}

		[Test]
		public void SeatChronicle_TellsEachTransitionAndNeverTellsTheOneThatDidNotHappen()
		{
			Assert.AreEqual("", KingdomReachRules.SeatChronicle(Transition.None, "archivist", "Mirrehet", "the great scriptorium"));
			StringAssert.Contains("is named archivist", KingdomReachRules.SeatChronicle(Transition.FirstHolder, "archivist", "Mirrehet", "the great scriptorium"));
			StringAssert.Contains("passes to", KingdomReachRules.SeatChronicle(Transition.Passed, "archivist", "Ulder", "the great scriptorium"));
			StringAssert.Contains("no archivist left", KingdomReachRules.SeatChronicle(Transition.Vacant, "archivist", "Ulder", "the great scriptorium"));
		}

		[Test]
		public void SeatMessage_SaysNothingWhenTheChronicleDoes()
		{
			Assert.AreEqual("", KingdomReachRules.SeatMessage(Transition.None, "archivist", "Mirrehet", "the great scriptorium"));
		}

		[Test]
		public void SeatMessage_OpensWithACapitalAndMarksALostOfficeInRed()
		{
			string named = KingdomReachRules.SeatMessage(Transition.FirstHolder, "archivist", "Mirrehet", "the great scriptorium");
			StringAssert.StartsWith("{{W|M", named);
			StringAssert.EndsWith(".}}", named);
			StringAssert.StartsWith("{{r|", KingdomReachRules.SeatMessage(Transition.Vacant, "archivist", "Ulder", "the great scriptorium"));
		}
	}
}
#endif
