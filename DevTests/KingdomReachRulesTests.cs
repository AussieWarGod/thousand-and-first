#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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
		[Test]
		public void ReachDeclarationsKeepTheirPublicAbiAndDefaults()
		{
			ClassicAssert.AreEqual(typeof(int), System.Enum.GetUnderlyingType(typeof(ReachBand)));
			CollectionAssert.AreEqual(new int[5] { 0, 1, 2, 3, 4 }, new int[5]
			{
				(int)ReachBand.Plot,
				(int)ReachBand.Quarter,
				(int)ReachBand.Zone,
				(int)ReachBand.City,
				(int)ReachBand.Realm
			});
			ClassicAssert.AreEqual(typeof(int), System.Enum.GetUnderlyingType(typeof(ReachRelation)));
			CollectionAssert.AreEqual(new int[6] { 0, 1, 2, 3, 4, 5 }, new int[6]
			{
				(int)ReachRelation.Elsewhere,
				(int)ReachRelation.SameRealm,
				(int)ReachRelation.SameCity,
				(int)ReachRelation.SameZone,
				(int)ReachRelation.SameQuarter,
				(int)ReachRelation.SamePlot
			});

			ClassicAssert.AreEqual("ThousandAndFirst.GroundCharacter", typeof(GroundCharacter).FullName);
			ClassicAssert.IsTrue(typeof(GroundCharacter).IsPublic);
			ClassicAssert.IsTrue(typeof(GroundCharacter).IsSealed);
			System.Reflection.FieldInfo[] fields = typeof(GroundCharacter).GetFields();
			CollectionAssert.AreEqual(new string[4] { "Lifts", "Total", "Dominant", "DominantAmount" },
				new string[4] { fields[0].Name, fields[1].Name, fields[2].Name, fields[3].Name });
			CollectionAssert.AreEqual(new System.Type[4]
			{
				typeof(List<KindAmount>), typeof(int), typeof(string), typeof(int)
			}, new System.Type[4]
			{
				fields[0].FieldType, fields[1].FieldType, fields[2].FieldType, fields[3].FieldType
			});
			GroundCharacter empty = new GroundCharacter();
			ClassicAssert.AreEqual(0, empty.Lifts.Count);
			ClassicAssert.AreEqual(0, empty.Total);
			ClassicAssert.IsNull(empty.Dominant);
			ClassicAssert.AreEqual(0, empty.DominantAmount);
		}

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

		private static List<KingdomBenefitCell> PlotCells(int Count)
		{
			List<KingdomBenefitCell> cells = new List<KingdomBenefitCell>();
			for (int i = 0; i < Count; i++)
				cells.Add(new KingdomBenefitCell(i, i / 20,
					KingdomBenefitCellUse.Plot));
			return cells;
		}

		// --- The ladder: size sets the band -----------------------------------------------------

		[TestCase(Size.None, ReachBand.Plot)]
		[TestCase(Size.Small, ReachBand.Plot)]
		[TestCase(Size.Medium, ReachBand.Quarter)]
		[TestCase(Size.Large, ReachBand.Zone)]
		[TestCase(Size.Huge, ReachBand.City)]
		public void BandForSize_IsTheAddendumsOwnLadder(Size size, ReachBand expected)
		{
			ClassicAssert.AreEqual(expected, KingdomReachRules.BandForSize(size));
		}

		[TestCase(Size.Small, 0, 3)]
		[TestCase(Size.Small, 2, 3)]
		[TestCase(Size.Medium, 1, 2)]
		[TestCase(Size.Large, 2, 3)]
		public void Derive_TierNeverMovesABandBelowTheGreatWork(Size size, int index, int count)
		{
			ClassicAssert.AreEqual(KingdomReachRules.BandForSize(size), KingdomReachRules.Derive(size, index, count));
		}

		[Test]
		public void Derive_TheLastLinkOfAGreatWorksChainReachesTheRealm()
		{
			ClassicAssert.AreEqual(ReachBand.Realm, KingdomReachRules.Derive(Size.Huge, 1, 2));
			ClassicAssert.AreEqual(ReachBand.Realm, KingdomReachRules.Derive(Size.Huge, 2, 3));
		}

		[TestCase(0, 2)]
		[TestCase(1, 3)]
		[TestCase(0, 1)]
		[TestCase(0, 0)]
		public void Derive_AGreatWorkThatIsNotTheLastLinkReachesTheCity(int index, int count)
		{
			ClassicAssert.AreEqual(ReachBand.City, KingdomReachRules.Derive(Size.Huge, index, count));
		}

		[Test]
		public void Derive_ANegativeTierIndexReadsAsTheFirstLink()
		{
			ClassicAssert.AreEqual(ReachBand.City, KingdomReachRules.Derive(Size.Huge, -4, 3));
		}

		[TestCase(0, Size.None)]
		[TestCase(24, Size.Small)]
		[TestCase(25, Size.Medium)]
		[TestCase(48, Size.Medium)]
		[TestCase(49, Size.Large)]
		[TestCase(120, Size.Large)]
		[TestCase(121, Size.Huge)]
		public void SizeForDesignation_UsesExactPhysicalArea(int cells, Size expected)
		{
			ClassicAssert.AreEqual(expected,
				KingdomReachRules.SizeForDesignation(PlotCells(cells)));
		}

		[Test]
		public void ExactDesignationGeometry_DoesNotFillAnIrregularRoomsGap()
		{
			List<KingdomBenefitCell> cells = new List<KingdomBenefitCell>
			{
				new KingdomBenefitCell(4, 4, KingdomBenefitCellUse.Plot),
				new KingdomBenefitCell(6, 4, KingdomBenefitCellUse.Plot),
				new KingdomBenefitCell(5, 4, KingdomBenefitCellUse.Network, "road")
			};
			ClassicAssert.IsTrue(KingdomReachRules.ContainsPlotCell(cells, 4, 4));
			ClassicAssert.IsFalse(KingdomReachRules.ContainsPlotCell(cells, 5, 4));
			ClassicAssert.IsFalse(KingdomReachRules.ContainsPlotCell(cells, 7, 4));
		}

		[Test]
		public void DesignationAnchor_PrefersAProvedRootAndOtherwiseUsesExactGround()
		{
			List<KingdomBenefitCell> cells = new List<KingdomBenefitCell>
			{
				new KingdomBenefitCell(3, 2, KingdomBenefitCellUse.Plot),
				new KingdomBenefitCell(4, 2, KingdomBenefitCellUse.Plot)
			};
			ClassicAssert.IsTrue(KingdomReachRules.TryDesignationAnchor(cells, 4, 2,
				out int x, out int y));
			ClassicAssert.AreEqual(4, x); ClassicAssert.AreEqual(2, y);
			ClassicAssert.IsTrue(KingdomReachRules.TryDesignationAnchor(cells, 99, 99,
				out x, out y));
			ClassicAssert.AreEqual(3, x); ClassicAssert.AreEqual(2, y);
			ClassicAssert.IsFalse(KingdomReachRules.TryDesignationAnchor(null, 0, 0,
				out _, out _));
		}

		[TestCase("craft", true)]
		[TestCase("wealth", true)]
		[TestCase("third-party-support", true)]
		[TestCase("roof", false)]
		[TestCase("defence", false)]
		[TestCase(" Defence ", false)]
		public void PhysicalLift_ExcludesBindingGoodsAndStructuralDefence(
			string kind, bool expected)
		{
			ClassicAssert.AreEqual(expected, KingdomReachRules.IsPhysicalLift(kind));
		}

		[Test]
		public void RuntimeReachAndSubsidenceConsumePhysicalReadingsWithoutCatalogueFallbacks()
		{
			string subsidence = KingdomSubsidenceLogicalSource.Read();
			string ground = TestMain.ReadRepositoryText(
				"Growth/KingdomReach.GroundCharacter.cs");
			string offices = TestMain.ReadRepositoryText("Growth/KingdomReach.Offices.cs");
			string relations = TestMain.ReadRepositoryText("Growth/KingdomReach.Relations.cs");
			string physical = TestMain.ReadRepositoryText("Growth/KingdomReach.Benefits.cs");

			StringAssert.Contains("TryActiveBenefits", subsidence);
			StringAssert.Contains("IReadOnlyList<KingdomBenefitReading> readings = Benefits.Readings",
				subsidence);
			StringAssert.Contains("KingdomReach.TryRoot(Survey.Ground, reading", subsidence);
			StringAssert.Contains("KingdomObservedBenefitProjection.TryCarries(work, reading",
				subsidence);
			StringAssert.Contains("KingdomObservedBenefitProjectionRules.Amount(carries",
				subsidence);
			StringAssert.Contains("KingdomObservedBenefitProjectionRules.PhysicalLift(carries)",
				subsidence);
			StringAssert.Contains("PhysicalFlowContract", subsidence);
			StringAssert.DoesNotContain("HostedCarries", subsidence);
			StringAssert.DoesNotContain("YardShadesOf", subsidence);
			StringAssert.DoesNotContain("FoldShade", subsidence);

			StringAssert.Contains("benefits.Readings", ground);
			StringAssert.Contains("Gather(lifts, item, reading)", ground);
			StringAssert.Contains("KingdomObservedBenefitProjection.TryCarries(Root, Reading",
				ground);
			StringAssert.Contains("IsPhysicalLift", ground);
			StringAssert.DoesNotContain("Entry.Carries", ground);
			StringAssert.DoesNotContain("TryReadFootprint", ground);
			StringAssert.Contains("TryActiveBenefits", offices);
			StringAssert.Contains("GatherLive(shaded, reading)", offices);
			StringAssert.Contains("GatherLive(realm, reading)", offices);
			StringAssert.Contains("KingdomReachObservationRuntime.TryWrite(System, Z, shaded, realm,",
				offices);
			StringAssert.Contains("ContainsPlotCell", relations);
			StringAssert.DoesNotContain("TryReadFootprint", relations);
			StringAssert.Contains("SizeForDesignation", physical);
			StringAssert.Contains("ProviderId == \"taf.architecture\"", physical);
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
			ClassicAssert.IsTrue(KingdomReachRules.TryParseBand(raw, out band, out error));
			ClassicAssert.AreEqual(expected, band);
			ClassicAssert.IsNull(error);
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		public void TryParseBand_BlankIsDeriveMeAndNotAFault(string raw)
		{
			ReachBand band;
			string error;
			ClassicAssert.IsFalse(KingdomReachRules.TryParseBand(raw, out band, out error));
			ClassicAssert.IsNull(error);
		}

		[Test]
		public void TryParseBand_AnUnknownWordFailsAndNamesEveryBand()
		{
			ReachBand band;
			string error;
			ClassicAssert.IsFalse(KingdomReachRules.TryParseBand("region", out band, out error));
			ClassicAssert.IsNotNull(error);
			StringAssert.Contains("region", error);
			for (int i = 0; i < KingdomReachRules.BandNames.Length; i++)
			{
				StringAssert.Contains(KingdomReachRules.BandNames[i], error);
			}
		}

		[Test]
		public void BandNames_LineUpWithTheEnumTheyAreRead()
		{
			ClassicAssert.AreEqual(5, KingdomReachRules.BandNames.Length);
			ClassicAssert.AreEqual("plot", KingdomReachRules.BandName(ReachBand.Plot));
			ClassicAssert.AreEqual("quarter", KingdomReachRules.BandName(ReachBand.Quarter));
			ClassicAssert.AreEqual("zone", KingdomReachRules.BandName(ReachBand.Zone));
			ClassicAssert.AreEqual("city", KingdomReachRules.BandName(ReachBand.City));
			ClassicAssert.AreEqual("realm", KingdomReachRules.BandName(ReachBand.Realm));
		}

		[Test]
		public void Resolve_ADeclaredReachBeatsTheDerivation()
		{
			bool overridden;
			string error;
			ClassicAssert.AreEqual(ReachBand.Realm, KingdomReachRules.Resolve("realm", Size.Small, 0, 1, out overridden, out error));
			ClassicAssert.IsTrue(overridden);
			ClassicAssert.IsNull(error);
		}

		[Test]
		public void Resolve_ABadReachKeepsTheDerivationAndSaysWhy()
		{
			bool overridden;
			string error;
			ClassicAssert.AreEqual(ReachBand.Quarter, KingdomReachRules.Resolve("everywhere", Size.Medium, 0, 1, out overridden, out error));
			ClassicAssert.IsFalse(overridden);
			ClassicAssert.IsNotNull(error);
		}

		[Test]
		public void Resolve_NoAttributeDerivesAndReportsNothing()
		{
			bool overridden;
			string error;
			ClassicAssert.AreEqual(ReachBand.Zone, KingdomReachRules.Resolve(null, Size.Large, 0, 1, out overridden, out error));
			ClassicAssert.IsFalse(overridden);
			ClassicAssert.IsNull(error);
		}

		// --- Covering ----------------------------------------------------------------------------

		[TestCase(ReachBand.Plot, ReachRelation.SamePlot)]
		[TestCase(ReachBand.Quarter, ReachRelation.SameQuarter)]
		[TestCase(ReachBand.Zone, ReachRelation.SameZone)]
		[TestCase(ReachBand.City, ReachRelation.SameCity)]
		[TestCase(ReachBand.Realm, ReachRelation.SameRealm)]
		public void RelationRequired_IsTheBandsOwnEdge(ReachBand band, ReachRelation expected)
		{
			ClassicAssert.AreEqual(expected, KingdomReachRules.RelationRequired(band));
			ClassicAssert.IsTrue(KingdomReachRules.Covers(band, expected));
		}

		[TestCase(ReachBand.Plot, ReachRelation.SameQuarter)]
		[TestCase(ReachBand.Quarter, ReachRelation.SameZone)]
		[TestCase(ReachBand.Zone, ReachRelation.SameCity)]
		[TestCase(ReachBand.City, ReachRelation.SameRealm)]
		public void Covers_IsFalseOneStepPastTheBandsEdge(ReachBand band, ReachRelation where)
		{
			ClassicAssert.IsFalse(KingdomReachRules.Covers(band, where));
		}

		[TestCase(ReachBand.Plot)]
		[TestCase(ReachBand.Quarter)]
		[TestCase(ReachBand.Zone)]
		[TestCase(ReachBand.City)]
		[TestCase(ReachBand.Realm)]
		public void Covers_GroundTheRealmDoesNotHoldIsNeverReached(ReachBand band)
		{
			ClassicAssert.IsFalse(KingdomReachRules.Covers(band, ReachRelation.Elsewhere));
		}

		[Test]
		public void Covers_ANearerPlaceIsAlwaysStillCovered()
		{
			ClassicAssert.IsTrue(KingdomReachRules.Covers(ReachBand.Quarter, ReachRelation.SamePlot));
			ClassicAssert.IsTrue(KingdomReachRules.Covers(ReachBand.City, ReachRelation.SameZone));
			ClassicAssert.IsTrue(KingdomReachRules.Covers(ReachBand.Realm, ReachRelation.SamePlot));
		}

		[Test]
		public void RelationAt_NearerFactsWin()
		{
			ClassicAssert.AreEqual(ReachRelation.SamePlot, KingdomReachRules.RelationAt(false, false, false, false, OnFootprint: true));
			ClassicAssert.AreEqual(ReachRelation.SameQuarter, KingdomReachRules.RelationAt(true, true, true, InQuarter: true, OnFootprint: false));
			ClassicAssert.AreEqual(ReachRelation.SameZone, KingdomReachRules.RelationAt(true, true, true, InQuarter: false, OnFootprint: false));
			ClassicAssert.AreEqual(ReachRelation.SameCity, KingdomReachRules.RelationAt(true, true, false, false, false));
			ClassicAssert.AreEqual(ReachRelation.SameRealm, KingdomReachRules.RelationAt(true, false, false, false, false));
			ClassicAssert.AreEqual(ReachRelation.Elsewhere, KingdomReachRules.RelationAt(false, false, false, false, false));
		}

		// --- The quarter, measured ---------------------------------------------------------------

		[TestCase(0, KingdomReachRules.QuarterBaseRadius)]
		[TestCase(1, KingdomReachRules.QuarterBaseRadius + KingdomReachRules.QuarterRadiusPerTier)]
		[TestCase(-3, KingdomReachRules.QuarterBaseRadius)]
		[TestCase(50, KingdomReachRules.QuarterRadiusCap)]
		public void QuarterRadius_GrowsWithTierAndIsClampedBothWays(int tier, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomReachRules.QuarterRadius(tier));
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
			ClassicAssert.AreEqual(0, KingdomReachRules.QuarterMarks(Marks(40, 20), 10, 10, KingdomReachRules.QuarterLinkCells).Count);
			ClassicAssert.AreEqual(0, KingdomReachRules.QuarterMarks(null, 10, 10, KingdomReachRules.QuarterLinkCells).Count);
			ClassicAssert.AreEqual(0, KingdomReachRules.QuarterMarks(Marks(10, 10), 10, 10, 0).Count);
		}

		[Test]
		public void InQuarter_ReachesPastBuiltGroundByTheRadiusAndNoFurther()
		{
			// One neighbour six cells off; the resident stands four cells past THAT, which is
			// inside a first tier's radius and outside nothing else.
			List<Mark> marks = Marks(10, 10, 16, 10);
			ClassicAssert.IsTrue(KingdomReachRules.InQuarter(marks, 10, 10, 20, 10, KingdomReachRules.QuarterLinkCells, KingdomReachRules.QuarterRadius(0)));
			ClassicAssert.IsFalse(KingdomReachRules.InQuarter(marks, 10, 10, 21, 10, KingdomReachRules.QuarterLinkCells, KingdomReachRules.QuarterRadius(0)));
		}

		[Test]
		public void InQuarter_ATierFurtherAlongTheChainCarriesFurther()
		{
			List<Mark> marks = Marks(10, 10, 16, 10);
			ClassicAssert.IsTrue(KingdomReachRules.InQuarter(marks, 10, 10, 22, 10, KingdomReachRules.QuarterLinkCells, KingdomReachRules.QuarterRadius(1)));
		}

		[Test]
		public void InQuarter_TheWorksOwnRadiusHoldsWithNothingBuiltAroundIt()
		{
			ClassicAssert.IsTrue(KingdomReachRules.InQuarter(null, 10, 10, 13, 10, KingdomReachRules.QuarterLinkCells, 4));
			ClassicAssert.IsFalse(KingdomReachRules.InQuarter(null, 10, 10, 15, 10, KingdomReachRules.QuarterLinkCells, 4));
		}

		[Test]
		public void InQuarter_ANeighbouringClusterIsNotThisQuarter()
		{
			// Two clusters twenty cells apart. The far one's own ground is not shaded by this work,
			// however much built ground stands there.
			List<Mark> marks = Marks(10, 10, 30, 10, 34, 10);
			ClassicAssert.IsFalse(KingdomReachRules.InQuarter(marks, 10, 10, 32, 10, KingdomReachRules.QuarterLinkCells, KingdomReachRules.QuarterRadius(0)));
		}

		[Test]
		public void InQuarter_ANegativeRadiusShadesOnlyTheWorksOwnCell()
		{
			ClassicAssert.IsTrue(KingdomReachRules.InQuarter(null, 10, 10, 10, 10, KingdomReachRules.QuarterLinkCells, -5));
			ClassicAssert.IsFalse(KingdomReachRules.InQuarter(null, 10, 10, 11, 10, KingdomReachRules.QuarterLinkCells, -5));
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
			ClassicAssert.AreEqual(expected, KingdomReachRules.ScopedByReach(kind));
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
			ClassicAssert.AreEqual(expected, KingdomReachRules.Scaled(amount, percent));
		}

		[Test]
		public void Scaled_AWorkRunningAtAllKeepsAPointOfWhatItDeclares()
		{
			ClassicAssert.AreEqual(1, KingdomReachRules.Scaled(1, 25));
		}

		// --- What a lift lands on the settlement's own level (Addendum 6) -----------------------

		[Test]
		public void Landed_GivesAWorkItsWholeAmountWhereItReachesEveryHome()
		{
			// A zone-band work covers everything built around it, so nothing of it is lost.
			ClassicAssert.AreEqual(6, KingdomReachRules.Landed(6, 20, 20));
		}

		[Test]
		public void Landed_CountsNothingForAWorkThatReachesNobodyWhoLivesHere()
		{
			// The whole point of scoping: a shrine out past the fields lifts the level by nothing,
			// however loudly it shades the ground it stands on.
			ClassicAssert.AreEqual(0, KingdomReachRules.Landed(6, 0, 20));
		}

		[Test]
		public void Landed_ScalesWithTheShareOfTheSettlementItCovers()
		{
			// Half the homes, half the lift. This is what makes one shrine in each quarter worth
			// more to the level than two shrines in one.
			ClassicAssert.AreEqual(3, KingdomReachRules.Landed(6, 10, 20));
			ClassicAssert.AreEqual(1, KingdomReachRules.Landed(6, 5, 20));
		}

		[Test]
		public void Landed_KeepsAPointForAWorkThatReachesAnybodyAtAll()
		{
			// The same floor Scaled keeps, for the same reason: a work that reaches somebody is
			// never silently worth nothing.
			ClassicAssert.AreEqual(1, KingdomReachRules.Landed(2, 1, 40));
		}

		[Test]
		public void Landed_CannotMintALiftFromADoubleCountedHome()
		{
			ClassicAssert.AreEqual(6, KingdomReachRules.Landed(6, 99, 20));
		}

		[Test]
		public void Landed_LandsNothingWhereNobodyLivesAndNothingForANegativeAmount()
		{
			ClassicAssert.AreEqual(0, KingdomReachRules.Landed(6, 4, 0));
			ClassicAssert.AreEqual(0, KingdomReachRules.Landed(-6, 20, 20));
		}

		[Test]
		public void Landed_LeavesTheBindingGoodsAloneByNeverBeingAskedAboutThem()
		{
			// The rule that keeps water, food and roofs citywide pools is ScopedByReach; this test
			// pins the pair together, because a caller that scoped a binding good would be reading
			// the addendum backwards.
			ClassicAssert.IsFalse(KingdomReachRules.ScopedByReach("water"));
			ClassicAssert.IsFalse(KingdomReachRules.ScopedByReach("food"));
			ClassicAssert.IsFalse(KingdomReachRules.ScopedByReach("roof"));
			ClassicAssert.IsTrue(KingdomReachRules.ScopedByReach("spirit"));
			ClassicAssert.IsTrue(KingdomReachRules.ScopedByReach("craft"));
			ClassicAssert.IsTrue(KingdomReachRules.ScopedByReach("order"));
			ClassicAssert.IsTrue(KingdomReachRules.ScopedByReach("luxury"));
			ClassicAssert.IsTrue(KingdomReachRules.ScopedByReach("wealth"));
		}

		// --- The ground's character ----------------------------------------------------------------

		[Test]
		public void Character_SumsRepeatsAndNamesTheLoudest()
		{
			GroundCharacter character = KingdomReachRules.Character(Lifts("spirit", 2, "learning", 6, "spirit", 3));
			ClassicAssert.AreEqual(11, character.Total);
			ClassicAssert.AreEqual("learning", character.Dominant);
			ClassicAssert.AreEqual(6, character.DominantAmount);
			ClassicAssert.AreEqual(2, character.Lifts.Count);
		}

		[Test]
		public void Character_IgnoresTheBindingPoolsEntirely()
		{
			GroundCharacter character = KingdomReachRules.Character(Lifts("water", 40, "food", 12, "roof", 9, "order", 2));
			ClassicAssert.AreEqual(2, character.Total);
			ClassicAssert.AreEqual("order", character.Dominant);
			ClassicAssert.AreEqual(1, character.Lifts.Count);
		}

		[Test]
		public void Character_ListsInTheCataloguesOwnLiftOrder()
		{
			GroundCharacter character = KingdomReachRules.Character(Lifts(
				"wealth", 1, "luxury", 1, "spirit", 1, "craft", 1));
			ClassicAssert.AreEqual("craft", character.Lifts[0].Kind);
			ClassicAssert.AreEqual("spirit", character.Lifts[1].Kind);
			ClassicAssert.AreEqual("luxury", character.Lifts[2].Kind);
			ClassicAssert.AreEqual("wealth", character.Lifts[3].Kind);
		}

		[Test]
		public void Character_ATieGoesToTheEarlierLiftRatherThanToWhicheverWasSeenFirst()
		{
			GroundCharacter character = KingdomReachRules.Character(Lifts("luxury", 5, "craft", 5));
			ClassicAssert.AreEqual("craft", character.Dominant);
		}

		[Test]
		public void Character_AnotherModsGoodIsCountedAndListedAfterTheKnownOnes()
		{
			GroundCharacter character = KingdomReachRules.Character(Lifts("them:song", 9, "craft", 1));
			ClassicAssert.AreEqual(10, character.Total);
			ClassicAssert.AreEqual("them:song", character.Dominant);
			ClassicAssert.AreEqual("craft", character.Lifts[0].Kind);
			ClassicAssert.AreEqual("them:song", character.Lifts[1].Kind);
		}

		[Test]
		public void Character_FoldsCaseAndDropsAmountsThatAreNotThere()
		{
			GroundCharacter character = KingdomReachRules.Character(Lifts("Spirit", 3, "spirit", 0, "learning", -2, "  ", 4));
			ClassicAssert.AreEqual(1, character.Lifts.Count);
			ClassicAssert.AreEqual("spirit", character.Lifts[0].Kind);
			ClassicAssert.AreEqual(3, character.Lifts[0].Amount);
		}

		[Test]
		public void Character_GroundNothingReachesNamesNobody()
		{
			GroundCharacter character = KingdomReachRules.Character(null);
			ClassicAssert.AreEqual(0, character.Total);
			ClassicAssert.IsNull(character.Dominant);
			ClassicAssert.AreEqual(0, character.Lifts.Count);
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
			ClassicAssert.AreEqual(expected, KingdomReachRules.QuarterName(kind));
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
			ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomReachRules.QuarterLine(null)));
		}

		[Test]
		public void ReachClause_SaysTheGreatWorkNeedsSomebodyAtItsHead()
		{
			StringAssert.Contains("heads it", KingdomReachRules.ReachClause(ReachBand.City));
			StringAssert.Contains("heads it", KingdomReachRules.ReachClause(ReachBand.Realm));
			ClassicAssert.IsFalse(KingdomReachRules.ReachClause(ReachBand.Plot).Contains("heads it"));
		}

		// --- The seat --------------------------------------------------------------------------

		[TestCase(ReachBand.Plot, false)]
		[TestCase(ReachBand.Quarter, false)]
		[TestCase(ReachBand.Zone, false)]
		[TestCase(ReachBand.City, true)]
		[TestCase(ReachBand.Realm, true)]
		public void RequiresSeat_IsTheGreatWorksAloneRule(ReachBand band, bool expected)
		{
			ClassicAssert.AreEqual(expected, KingdomReachRules.RequiresSeat(band));
		}

		[TestCase(ReachBand.City, ReachBand.Zone)]
		[TestCase(ReachBand.Realm, ReachBand.Zone)]
		public void Unheaded_AGreatWorkWithNoKeeperKeepsItsOwnZone(ReachBand band, ReachBand expected)
		{
			ClassicAssert.AreEqual(expected, KingdomReachRules.Unheaded(band));
		}

		[TestCase(ReachBand.Plot)]
		[TestCase(ReachBand.Quarter)]
		[TestCase(ReachBand.Zone)]
		public void Unheaded_LeavesEverySmallerWorkExactlyAsItWas(ReachBand band)
		{
			ClassicAssert.AreEqual(band, KingdomReachRules.Unheaded(band));
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
			ClassicAssert.AreEqual(expected, KingdomReachRules.SeatTitle(category));
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
			ClassicAssert.Greater(strongAtCraft, cleverAtCraft);
			ClassicAssert.Greater(cleverAtKnowledge, strongAtKnowledge);
		}

		[Test]
		public void SeatFitness_TheGoverningAttributeCountsTwiceAndTheSecondOnce()
		{
			// Faith: willpower governs, ego seconds. Ten points of willpower are worth twice ten
			// points of ego, and nothing else on the sheet moves the number at all.
			ClassicAssert.AreEqual(30, KingdomReachRules.SeatFitness("faith", 0, 0, 0, 0, 10, 10));
			ClassicAssert.AreEqual(20, KingdomReachRules.SeatFitness("faith", 0, 0, 0, 0, 10, 0));
			ClassicAssert.AreEqual(10, KingdomReachRules.SeatFitness("faith", 0, 0, 0, 0, 0, 10));
			ClassicAssert.AreEqual(0, KingdomReachRules.SeatFitness("faith", 99, 99, 99, 99, 0, 0));
		}

		[Test]
		public void SeatFitness_AnUnknownPurposeAsksWhoTheSettlementListensTo()
		{
			ClassicAssert.AreEqual(KingdomReachRules.SeatFitness("them:hall", 0, 0, 0, 0, 4, 10),
				(2 * 10) + 4);
		}

		[Test]
		public void SeatFitness_NeverGoesNegative()
		{
			ClassicAssert.AreEqual(0, KingdomReachRules.SeatFitness("faith", -9, -9, -9, -9, -9, -9));
		}

		[TestCase(20, 20, false)]
		[TestCase(20, 22, false)]
		[TestCase(20, 23, true)]
		[TestCase(20, 40, true)]
		public void ShouldUnseat_ASeatedNotableIsOnlyReplacedByAPlainlyBetterOne(int seated, int challenger, bool expected)
		{
			ClassicAssert.AreEqual(expected, KingdomReachRules.ShouldUnseat(seated, challenger));
		}

		[Test]
		public void ShouldUnseat_AnEmptySeatIsTakenByAnybody()
		{
			ClassicAssert.IsTrue(KingdomReachRules.ShouldUnseat(-1, 0));
		}

		[Test]
		public void ShouldUnseat_TheMarginIsTheThingBeingTested()
		{
			ClassicAssert.AreEqual(3, KingdomReachRules.SeatUnseatMargin);
		}

		[Test]
		public void UnheadedLine_NamesTheWorkAndTheOfficeThatWouldLiftIt()
		{
			string line = KingdomReachRules.UnheadedLine("the temple", "keeper of rites");
			StringAssert.Contains("the temple", line);
			StringAssert.Contains("keeper of rites", line);
			ClassicAssert.IsFalse(string.IsNullOrEmpty(KingdomReachRules.UnheadedLine(null, null)));
		}

		[Test]
		public void SeatChronicle_TellsEachTransitionAndNeverTellsTheOneThatDidNotHappen()
		{
			ClassicAssert.AreEqual("", KingdomReachRules.SeatChronicle(Transition.None, "archivist", "Mirrehet", "the great scriptorium"));
			StringAssert.Contains("is named archivist", KingdomReachRules.SeatChronicle(Transition.FirstHolder, "archivist", "Mirrehet", "the great scriptorium"));
			StringAssert.Contains("passes to", KingdomReachRules.SeatChronicle(Transition.Passed, "archivist", "Ulder", "the great scriptorium"));
			StringAssert.Contains("no archivist left", KingdomReachRules.SeatChronicle(Transition.Vacant, "archivist", "Ulder", "the great scriptorium"));
		}

		[Test]
		public void SeatMessage_SaysNothingWhenTheChronicleDoes()
		{
			ClassicAssert.AreEqual("", KingdomReachRules.SeatMessage(Transition.None, "archivist", "Mirrehet", "the great scriptorium"));
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
