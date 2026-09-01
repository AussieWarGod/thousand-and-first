#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;
using Frontier = ThousandAndFirst.KingdomRules.Frontier;
using Ground = ThousandAndFirst.KingdomPlotRules.GroundKind;
using Mark = ThousandAndFirst.KingdomLayoutRules.LayoutMark;
using Material = ThousandAndFirst.KingdomPlotRules.Material;
using Outcome = ThousandAndFirst.KingdomLayoutRules.LayoutOutcome;
using Purpose = ThousandAndFirst.KingdomLayoutRules.LayoutPurpose;
using Rect = ThousandAndFirst.KingdomPlotRules.PlotRect;
using Size = ThousandAndFirst.KingdomPlotRules.PlotSize;
using Stage = ThousandAndFirst.KingdomPlotRules.PlotStage;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The plot geometry. Every number is asserted by exact value rather than by ordering, so
	/// deleting a penalty, flipping a gate, widening a tier, or rounding a budget the other way
	/// fails here instead of quietly changing what a settlement looks like.
	/// </summary>
	public class KingdomPlotRulesTests
	{
		// A Qud surface zone.
		private const int W = 80;

		private const int H = 25;

		private static Rect R(int X1, int Y1, int X2, int Y2)
		{
			return new Rect(X1, Y1, X2, Y2);
		}

		private static Rect At(int X, int Y, Size Size)
		{
			Assert.IsTrue(KingdomPlotRules.TryRectAt(X, Y, Size, out var rect), "expected a rect for " + Size);
			return rect;
		}

		private static Mark M(int X, int Y, Purpose Purpose)
		{
			return new Mark(X, Y, Purpose);
		}

		private static List<Mark> Marks(params Mark[] Items)
		{
			return new List<Mark>(Items);
		}

		private static List<Rect> Rects(params Rect[] Items)
		{
			return new List<Rect>(Items);
		}

		private static List<Ground> Cells(int Count, Ground Kind)
		{
			List<Ground> cells = new List<Ground>();
			for (int i = 0; i < Count; i++)
			{
				cells.Add(Kind);
			}
			return cells;
		}

		// --- Tiers: vanilla's own size bands, named ---------------------------------------

		[TestCase(Size.Small, 6, 4)]
		[TestCase(Size.Medium, 8, 6)]
		[TestCase(Size.Large, 12, 10)]
		[TestCase(Size.Huge, 20, 18)]
		public void TierHasItsDimensions(Size Size, int ExpectedWidth, int ExpectedHeight)
		{
			Assert.IsTrue(KingdomPlotRules.TryDimensions(Size, out var width, out var height));
			Assert.AreEqual(ExpectedWidth, width, "width");
			Assert.AreEqual(ExpectedHeight, height, "height");
			Assert.AreEqual(0, width % 2, "every horizontal axis uses the same even seam law");
			Assert.AreEqual(0, height % 2, "every vertical axis uses the same even seam law");
		}

		[Test]
		public void NoneIsNotATier()
		{
			Assert.IsFalse(KingdomPlotRules.TryDimensions(Size.None, out var width, out var height));
			Assert.AreEqual(0, width);
			Assert.AreEqual(0, height);
			Assert.IsFalse(KingdomPlotRules.TryRectAt(4, 4, Size.None, out _));
		}

		[Test]
		public void TiersGrowStrictlyInBothSpans()
		{
			// The overlap band between tiers is deliberate, but a tier that did not grow in both
			// directions would make one of them pointless.
			Size[] order = new Size[4] { Size.Small, Size.Medium, Size.Large, Size.Huge };
			for (int i = 1; i < order.Length; i++)
			{
				KingdomPlotRules.TryDimensions(order[i - 1], out var lastWidth, out var lastHeight);
				KingdomPlotRules.TryDimensions(order[i], out var width, out var height);
				Assert.Greater(width, lastWidth, order[i] + " width");
				Assert.Greater(height, lastHeight, order[i] + " height");
			}
		}

		[Test]
		public void RectAtAnchorRunsFromTheAnchor()
		{
			Rect rect = At(10, 7, Size.Medium);
			Assert.AreEqual(10, rect.X1);
			Assert.AreEqual(7, rect.Y1);
			Assert.AreEqual(17, rect.X2);
			Assert.AreEqual(12, rect.Y2);
			Assert.AreEqual(8, rect.Width);
			Assert.AreEqual(6, rect.Height);
			Assert.AreEqual(48, rect.Area);
		}

		[Test]
		public void CentreIsBiasedToTheLowCornerOnAnEvenSpan()
		{
			Rect rect = At(10, 10, Size.Small);
			Assert.AreEqual(12, rect.CenterX, "6 wide, low centre");
			Assert.AreEqual(11, rect.CenterY, "4 tall");
		}

		[Test]
		public void BordersAndCornersAreTheEdgeCells()
		{
			Rect rect = R(0, 0, 4, 3);
			Assert.IsTrue(rect.IsBorder(0, 0));
			Assert.IsTrue(rect.IsCorner(0, 0));
			Assert.IsTrue(rect.IsBorder(2, 0));
			Assert.IsFalse(rect.IsCorner(2, 0));
			Assert.IsFalse(rect.IsBorder(2, 1));
			Assert.IsFalse(rect.IsBorder(9, 9));
			Assert.IsTrue(rect.Contains(4, 3));
			Assert.IsFalse(rect.Contains(5, 3));
		}

		[Test]
		public void PersistedZoneRectsRejectTornInvertedAndOutOfZoneGeometry()
		{
			Assert.IsTrue(KingdomPlotRules.ValidZoneRect(R(0, 0, W - 1, H - 1), W, H));
			Assert.IsFalse(KingdomPlotRules.ValidZoneRect(R(4, 2, 3, 8), W, H));
			Assert.IsFalse(KingdomPlotRules.ValidZoneRect(R(4, 8, 9, 7), W, H));
			Assert.IsFalse(KingdomPlotRules.ValidZoneRect(R(-1, 0, 4, 3), W, H));
			Assert.IsFalse(KingdomPlotRules.ValidZoneRect(R(0, 0, W, 3), W, H));
			Assert.IsFalse(KingdomPlotRules.ValidZoneRect(R(0, 0, 3, H), W, H));
			Assert.IsFalse(KingdomPlotRules.ValidZoneRect(R(0, 0, 0, 0), 0, H));
			Assert.IsFalse(KingdomPlotRules.Fits(R(4, 2, 3, 8), R(0, 0, 10, 10)));
		}

		// --- Stage gating: the city builds bigger as it grows ------------------------------

		[TestCase(GrowthStage.Camp, Size.Small)]
		[TestCase(GrowthStage.Steading, Size.Medium)]
		[TestCase(GrowthStage.Village, Size.Medium)]
		[TestCase(GrowthStage.Town, Size.Large)]
		[TestCase(GrowthStage.City, Size.Huge)]
		public void StageHasItsCeiling(GrowthStage Stage, Size Expected)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.MaxSizeForStage(Stage));
		}

		[TestCase(GrowthStage.Camp, Size.Small, true)]
		[TestCase(GrowthStage.Camp, Size.Medium, false)]
		[TestCase(GrowthStage.Camp, Size.Large, false)]
		[TestCase(GrowthStage.Camp, Size.Huge, false)]
		[TestCase(GrowthStage.Steading, Size.Medium, true)]
		[TestCase(GrowthStage.Steading, Size.Large, false)]
		[TestCase(GrowthStage.Village, Size.Medium, true)]
		[TestCase(GrowthStage.Village, Size.Large, false)]
		[TestCase(GrowthStage.Town, Size.Large, true)]
		[TestCase(GrowthStage.Town, Size.Huge, false)]
		[TestCase(GrowthStage.City, Size.Huge, true)]
		[TestCase(GrowthStage.City, Size.Small, true)]
		public void StageGatesTheTier(GrowthStage Stage, Size Size, bool Expected)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.Allows(Stage, Size));
		}

		[Test]
		public void NoStageEverLaysANonPlot()
		{
			// Small plots never obsolete, and None is not a plot at all: a stage that allowed it
			// would put single-cell furniture back on the plot path.
			Assert.IsFalse(KingdomPlotRules.Allows(GrowthStage.Camp, Size.None));
			Assert.IsFalse(KingdomPlotRules.Allows(GrowthStage.City, Size.None));
		}

		[TestCase(Size.Small, GrowthStage.Camp)]
		[TestCase(Size.Medium, GrowthStage.Steading)]
		[TestCase(Size.Large, GrowthStage.Town)]
		[TestCase(Size.Huge, GrowthStage.City)]
		public void EveryTierNamesTheStageThatLiftsIt(Size Size, GrowthStage Expected)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.StageForSize(Size));
			Assert.IsTrue(KingdomPlotRules.Allows(Expected, Size), "the stage it names must actually allow it");
		}

		// --- Zone interior and the road budget --------------------------------------------

		[Test]
		public void InteriorIsInsetFromEveryEdge()
		{
			Assert.IsTrue(KingdomPlotRules.TryInterior(W, H, out var interior));
			Assert.AreEqual(2, interior.X1);
			Assert.AreEqual(2, interior.Y1);
			Assert.AreEqual(77, interior.X2);
			Assert.AreEqual(22, interior.Y2);
			Assert.AreEqual(76, interior.Width);
			Assert.AreEqual(21, interior.Height);
		}

		[Test]
		public void AZoneTooSmallToInsetHasNoInterior()
		{
			Assert.IsFalse(KingdomPlotRules.TryInterior(4, 25, out _));
			Assert.IsFalse(KingdomPlotRules.TryInterior(80, 4, out _));
			Assert.AreEqual(0, KingdomPlotRules.PlotAreaAllowance(4, 4));
		}

		[Test]
		public void InsetOriginBoundsKeepTheTrueExteriorLaneInsideASurfaceZone()
		{
			int clearance = KingdomPlotRules.RoadMargin + 1;
			Assert.IsTrue(KingdomPlotRules.TryInsetOriginBounds(W, H, 12, 10,
				clearance, out var large));
			Assert.AreEqual(R(2, 2, 66, 13), large);
			Rect southmost = new Rect(large.X1, large.Y2,
				large.X1 + 11, large.Y2 + 9);
			Assert.AreEqual(22, southmost.Y2);
			Assert.LessOrEqual(southmost.Y2 + clearance, H - 1);

			Assert.IsTrue(KingdomPlotRules.TryInsetOriginBounds(W, H, 20, 18,
				clearance, out var huge));
			Assert.AreEqual(R(2, 2, 58, 5), huge);
			Assert.LessOrEqual(huge.Y2 + 17 + clearance, H - 1);

			Assert.IsTrue(KingdomPlotRules.TryInsetOriginBounds(W, H, 18, 20,
				clearance, out var rotatedHuge));
			Assert.AreEqual(R(2, 2, 60, 3), rotatedHuge);
			Assert.LessOrEqual(rotatedHuge.Y2 + 19 + clearance, H - 1);
		}

		[Test]
		public void InsetOriginBoundsRefuseMalformedOrImpossibleGeometry()
		{
			Assert.IsFalse(KingdomPlotRules.TryInsetOriginBounds(W, H, 0, 10, 2, out _));
			Assert.IsFalse(KingdomPlotRules.TryInsetOriginBounds(W, H, 12, 10, -1, out _));
			Assert.IsFalse(KingdomPlotRules.TryInsetOriginBounds(12, 10, 12, 10, 1, out _));
			Assert.IsFalse(KingdomPlotRules.TryInsetOriginBounds(int.MaxValue, H,
				int.MaxValue, 10, int.MaxValue, out _));
		}

		[Test]
		public void EveryTierFitsASurfaceZonesInterior()
		{
			Assert.IsTrue(KingdomPlotRules.TryInterior(W, H, out var interior));
			Assert.IsTrue(KingdomPlotRules.Fits(At(2, 2, Size.Huge), interior), "XL must fit or it can never be laid");
			Assert.IsFalse(KingdomPlotRules.Fits(At(70, 2, Size.Huge), interior));
		}

		[Test]
		public void ReservedRectIsTheLane()
		{
			Rect reserved = KingdomPlotRules.Reserved(R(10, 10, 14, 13));
			Assert.AreEqual(9, reserved.X1);
			Assert.AreEqual(9, reserved.Y1);
			Assert.AreEqual(15, reserved.X2);
			Assert.AreEqual(14, reserved.Y2);
		}

		[Test]
		public void PlotsMayNotShareTheirLane()
		{
			Rect laid = R(10, 10, 14, 13);
			// Hard against it: overlaps the lane.
			Assert.IsTrue(KingdomPlotRules.CrowdsExisting(R(15, 10, 19, 13), Rects(laid)));
			// One cell of road between them: allowed, and that road is what settlers wear a path
			// into later.
			Assert.IsFalse(KingdomPlotRules.CrowdsExisting(R(16, 10, 20, 13), Rects(laid)));
			// Straight overlap.
			Assert.IsTrue(KingdomPlotRules.CrowdsExisting(R(12, 12, 16, 15), Rects(laid)));
			Assert.IsFalse(KingdomPlotRules.CrowdsExisting(R(0, 0, 4, 3), Rects(laid)));
			Assert.IsFalse(KingdomPlotRules.CrowdsExisting(R(0, 0, 4, 3), null));
		}

		[Test]
		public void OverlapIsSymmetricAndInclusive()
		{
			Assert.IsTrue(KingdomPlotRules.Overlaps(R(0, 0, 4, 4), R(4, 4, 8, 8)));
			Assert.IsTrue(KingdomPlotRules.Overlaps(R(4, 4, 8, 8), R(0, 0, 4, 4)));
			Assert.IsFalse(KingdomPlotRules.Overlaps(R(0, 0, 3, 3), R(4, 4, 8, 8)));
		}

		[Test]
		public void RoadBudgetLeavesFourPartsInTen()
		{
			// 76 x 21 interior is 1596 cells; sixty percent of it may be plot.
			Assert.AreEqual(957, KingdomPlotRules.PlotAreaAllowance(W, H));
		}

		[Test]
		public void TheBriefsMinimumMatureLayoutFitsButFillsThePlotBudget()
		{
			// One XL, two L, four M, six S is the brief's minimum mature mix. It fits under
			// the 957-cell cap, but the 21-cell remainder cannot admit even one 24-cell S lot.
			List<Rect> laid = Rects(
				R(0, 0, 19, 17),
				R(0, 0, 11, 9), R(0, 0, 11, 9),
				R(0, 0, 7, 5), R(0, 0, 7, 5), R(0, 0, 7, 5), R(0, 0, 7, 5),
				R(0, 0, 5, 3), R(0, 0, 5, 3), R(0, 0, 5, 3), R(0, 0, 5, 3), R(0, 0, 5, 3), R(0, 0, 5, 3));
			Assert.AreEqual(936, KingdomPlotRules.LaidArea(laid));
			Assert.IsTrue(KingdomPlotRules.WouldExceedBudget(laid, Size.Small, W, H), "the minimum mix leaves less than one S lot");
			Assert.IsTrue(KingdomPlotRules.WouldExceedBudget(laid, Size.Large, W, H), "another hall does not");
		}

		[Test]
		public void AnEmptyZoneAffordsAnythingAndANonPlotSpendsNothing()
		{
			Assert.IsFalse(KingdomPlotRules.WouldExceedBudget(null, Size.Huge, W, H));
			Assert.IsFalse(KingdomPlotRules.WouldExceedBudget(Rects(R(0, 0, 69, 9)), Size.None, W, H));
			Assert.IsTrue(KingdomPlotRules.WouldExceedBudget(Rects(R(0, 0, 69, 9)), Size.Huge, W, H));
			Assert.AreEqual(0, KingdomPlotRules.LaidArea(null));
		}

		// --- The heart: seeded at the rite, drifting to the built centre --------------------

		[Test]
		public void NoRiteAndNoWorksIsNoHeart()
		{
			Assert.IsFalse(KingdomPlotRules.TryHeart(Marks(), HasRite: false, 0, 0, out var x, out var y));
			Assert.AreEqual(0, x);
			Assert.AreEqual(0, y);
		}

		[Test]
		public void TheRiteGroundIsTheFirstHeart()
		{
			Assert.IsTrue(KingdomPlotRules.TryHeart(Marks(), HasRite: true, 12, 7, out var x, out var y));
			Assert.AreEqual(12, x);
			Assert.AreEqual(7, y);
		}

		[Test]
		public void TheHeartDriftsTowardWhatIsBuilt()
		{
			// Rite at 10,10; three works out at 30,10. The rite counts once, so the heart lands
			// three quarters of the way toward the city and keeps moving as more is raised.
			List<Mark> marks = Marks(M(30, 10, Purpose.Housing), M(30, 10, Purpose.Housing), M(30, 10, Purpose.Civic));
			Assert.IsTrue(KingdomPlotRules.TryHeart(marks, HasRite: true, 10, 10, out var x, out var y));
			Assert.AreEqual(25, x);
			Assert.AreEqual(10, y);
		}

		[Test]
		public void OneWorkMovesTheHeartHalfWay()
		{
			Assert.IsTrue(KingdomPlotRules.TryHeart(Marks(M(20, 10, Purpose.Civic)), HasRite: true, 10, 10, out var x, out _));
			Assert.AreEqual(15, x);
		}

		[Test]
		public void WallsDoNotDragTheHeartToTheEdge()
		{
			List<Mark> marks = Marks(M(0, 0, Purpose.Defence), M(0, 0, Purpose.Defence), M(0, 0, Purpose.Defence));
			Assert.IsTrue(KingdomPlotRules.TryHeart(marks, HasRite: true, 40, 12, out var x, out var y));
			Assert.AreEqual(40, x);
			Assert.AreEqual(12, y);
		}

		[Test]
		public void WithoutARiteTheHeartIsTheGrammarsOwn()
		{
			List<Mark> marks = Marks(M(20, 10, Purpose.Civic), M(30, 12, Purpose.Housing));
			KingdomLayoutRules.TryHeart(marks, out var grammarX, out var grammarY);
			Assert.IsTrue(KingdomPlotRules.TryHeart(marks, HasRite: false, 0, 0, out var x, out var y));
			Assert.AreEqual(grammarX, x);
			Assert.AreEqual(grammarY, y);
		}

		[TestCase(Size.Small, 0)]
		[TestCase(Size.Medium, 0)]
		[TestCase(Size.Large, 1)]
		[TestCase(Size.Huge, 3)]
		public void OnlyTheGreatPlotsWantTheHeart(Size Size, int Expected)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.HeartPull(Size));
		}

		// --- Siting -----------------------------------------------------------------------

		[Test]
		public void ReachIsZeroInsideAndChebyshevOutside()
		{
			Rect rect = R(10, 10, 14, 13);
			Assert.AreEqual(0, KingdomPlotRules.Reach(rect, 12, 12));
			Assert.AreEqual(0, KingdomPlotRules.Reach(rect, 10, 10));
			Assert.AreEqual(1, KingdomPlotRules.Reach(rect, 15, 12));
			Assert.AreEqual(3, KingdomPlotRules.Reach(rect, 17, 9));
			Assert.AreEqual(5, KingdomPlotRules.Reach(rect, 12, 18));
		}

		[Test]
		public void ARectTouchesTheFrontierWhenAnyCellDoes()
		{
			Assert.IsTrue(KingdomPlotRules.TouchesFrontier(R(40, 1, 44, 4), W, H, Frontier.North));
			Assert.IsFalse(KingdomPlotRules.TouchesFrontier(R(40, 2, 44, 5), W, H, Frontier.North));
			Assert.IsFalse(KingdomPlotRules.TouchesFrontier(R(40, 1, 44, 4), W, H, Frontier.None));
			Assert.IsTrue(KingdomPlotRules.TouchesFrontier(R(76, 10, 79, 13), W, H, Frontier.East));
		}

		[Test]
		public void ARectIsScoredWhereItsCentreIs()
		{
			List<Mark> marks = Marks(M(40, 12, Purpose.Housing));
			Assert.AreEqual(-48, KingdomPlotRules.ScoreRect(Purpose.Housing, Size.Small, At(50, 10, Size.Small), W, H, Frontier.None, marks, false, 0, 0));
		}

		[Test]
		public void ACornerOnTheFrontierCostsTheWholeFrontierPenalty()
		{
			// The centre is clear of the band and the corner is not. A house on the wall is a
			// house on the wall whichever cell you measure.
			List<Mark> marks = Marks(M(40, 12, Purpose.Housing));
			Rect clear = At(50, 2, Size.Small);
			Rect touching = At(50, 1, Size.Small);
			int clearScore = KingdomPlotRules.ScoreRect(Purpose.Housing, Size.Small, clear, W, H, Frontier.North, marks, false, 0, 0);
			int touchingScore = KingdomPlotRules.ScoreRect(Purpose.Housing, Size.Small, touching, W, H, Frontier.North, marks, false, 0, 0);
			Assert.AreEqual(-48, clearScore);
			Assert.AreEqual(-108, touchingScore);
			Assert.AreEqual(KingdomLayoutRules.FrontierPenalty, clearScore - touchingScore, "the difference is exactly one frontier penalty");
		}

		[Test]
		public void AGreatPlotIsPulledToTheHeartAndAHutIsNot()
		{
			List<Mark> marks = Marks(M(40, 12, Purpose.Civic));
			Rect huge = At(0, 0, Size.Huge);
			// Centre 9,6 is 31 cells from the heart: two per cell from the grammar, three more
			// per cell because it is a great plot.
			Assert.AreEqual(-155, KingdomPlotRules.ScoreRect(Purpose.Civic, Size.Huge, huge, W, H, Frontier.None, marks, false, 0, 0));
			Assert.AreEqual(-62, KingdomPlotRules.ScoreRect(Purpose.Civic, Size.None, huge, W, H, Frontier.None, marks, false, 0, 0));
		}

		[Test]
		public void TheHeartPullReadsTheRiteGround()
		{
			List<Mark> marks = Marks(M(40, 12, Purpose.Civic));
			Rect huge = At(0, 0, Size.Huge);
			int withoutRite = KingdomPlotRules.ScoreRect(Purpose.Civic, Size.Huge, huge, W, H, Frontier.None, marks, false, 0, 0);
			// A rite poured beside the great plot drags the drifting heart back toward it, so the
			// same ground scores better.
			int withRite = KingdomPlotRules.ScoreRect(Purpose.Civic, Size.Huge, huge, W, H, Frontier.None, marks, true, 0, 0);
			Assert.AreEqual(-155, withoutRite);
			Assert.AreEqual(-95, withRite);
		}

		[Test]
		public void NoCandidatesIsNoneAndNoIndex()
		{
			Assert.AreEqual(Outcome.None, KingdomPlotRules.ChooseRect(Purpose.Housing, Size.Small, W, H, Frontier.None, Marks(M(40, 12, Purpose.Housing)), Rects(), false, 0, 0, false, 0, 0, out var index));
			Assert.AreEqual(-1, index);
			Assert.AreEqual(Outcome.None, KingdomPlotRules.ChooseRect(Purpose.Housing, Size.Small, W, H, Frontier.None, Marks(), null, false, 0, 0, false, 0, 0, out index));
			Assert.AreEqual(-1, index);
		}

		[Test]
		public void EmptyGroundDefersToTheFounder()
		{
			Assert.AreEqual(Outcome.Defer, KingdomPlotRules.ChooseRect(Purpose.Housing, Size.Small, W, H, Frontier.None, Marks(), Rects(At(10, 10, Size.Small)), true, 10, 10, false, 0, 0, out var index));
			Assert.AreEqual(-1, index);
		}

		[Test]
		public void HousingGathersByTheHouses()
		{
			List<Mark> marks = Marks(M(40, 12, Purpose.Housing));
			List<Rect> candidates = Rects(At(42, 11, Size.Small), At(60, 11, Size.Small));
			Assert.AreEqual(Outcome.Grammar, KingdomPlotRules.ChooseRect(Purpose.Housing, Size.Small, W, H, Frontier.None, marks, candidates, false, 0, 0, false, 0, 0, out var index));
			Assert.AreEqual(0, index);
		}

		[Test]
		public void TheFounderPicksTheSpotInsideTheQuarter()
		{
			List<Mark> marks = Marks(M(40, 12, Purpose.Housing));
			List<Rect> candidates = Rects(At(42, 11, Size.Small), At(46, 11, Size.Small));
			// The founder is standing in the second rect, which scores exactly one tolerance
			// worse than the plan's best. Intent wins.
			Assert.AreEqual(Outcome.Founder, KingdomPlotRules.ChooseRect(Purpose.Housing, Size.Small, W, H, Frontier.None, marks, candidates, true, 50, 12, false, 0, 0, out var index));
			Assert.AreEqual(1, index);
		}

		[Test]
		public void TheFounderIsOverruledOnTheWall()
		{
			List<Mark> marks = Marks(M(40, 12, Purpose.Housing));
			List<Rect> candidates = Rects(At(42, 0, Size.Small), At(42, 11, Size.Small));
			// Standing in the frontier band is the one thing the plan feels strongly enough about
			// to refuse the founder's own ground.
			Assert.AreEqual(Outcome.Grammar, KingdomPlotRules.ChooseRect(Purpose.Housing, Size.Small, W, H, Frontier.North, marks, candidates, true, 43, 1, false, 0, 0, out var index));
			Assert.AreEqual(1, index);
		}

		[Test]
		public void TheFounderMustBeWithinReachToCount()
		{
			List<Mark> marks = Marks(M(40, 12, Purpose.Housing));
			List<Rect> candidates = Rects(At(42, 11, Size.Small), At(46, 11, Size.Small));
			// Three cells away from the second rect is outside FounderReachCells, so it is not
			// their ground and the plan's own best wins.
			Assert.AreEqual(Outcome.Grammar, KingdomPlotRules.ChooseRect(Purpose.Housing, Size.Small, W, H, Frontier.None, marks, candidates, true, 49, 18, false, 0, 0, out var index));
			Assert.AreEqual(0, index);
		}

		[TestCase(true)]
		[TestCase(false)]
		public void TiesBreakByPositionAndNotByCallerOrder(bool Reversed)
		{
			List<Mark> marks = Marks(M(40, 12, Purpose.Civic));
			Rect west = At(30, 10, Size.Small);
			Rect east = At(46, 10, Size.Small);
			List<Rect> candidates = Reversed ? Rects(east, west) : Rects(west, east);
			Assert.AreEqual(Outcome.Grammar, KingdomPlotRules.ChooseRect(Purpose.Civic, Size.Small, W, H, Frontier.None, marks, candidates, false, 0, 0, false, 0, 0, out var index));
			Assert.AreEqual(30, candidates[index].X1, "the westward rect wins either way");
		}

		// --- The door faces the settlement -------------------------------------------------

		[Test]
		public void TheDoorIsCutNearestTheHeart()
		{
			Assert.IsTrue(KingdomPlotRules.TryDoor(R(10, 10, 14, 13), 12, 20, out var x, out var y));
			Assert.AreEqual(11, x);
			Assert.AreEqual(13, y);
		}

		[Test]
		public void TheDoorIsNeverACorner()
		{
			Rect rect = R(10, 10, 14, 13);
			Assert.IsTrue(KingdomPlotRules.TryDoor(rect, 0, 0, out var x, out var y));
			Assert.IsFalse(rect.IsCorner(x, y), "a door in a corner is a hole in two walls");
			Assert.IsTrue(rect.IsBorder(x, y));
			Assert.AreEqual(11, x);
			Assert.AreEqual(10, y);
		}

		[Test]
		public void ARectWithNothingButCornersHasNoDoor()
		{
			Assert.IsFalse(KingdomPlotRules.TryDoor(R(0, 0, 1, 1), 5, 5, out var x, out var y));
			Assert.AreEqual(0, x);
			Assert.AreEqual(0, y);
		}

		// --- Clearance is extraction -------------------------------------------------------

		[TestCase(Ground.Bare, 0)]
		[TestCase(Ground.Brush, 1)]
		[TestCase(Ground.Ruins, 3)]
		[TestCase(Ground.Trees, 4)]
		[TestCase(Ground.Rock, 6)]
		[TestCase(Ground.Marble, 8)]
		[TestCase(Ground.Liquid, 0)]
		[TestCase(Ground.Held, 0)]
		public void EffortScalesWithHardness(Ground Kind, int Expected)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.ClearEffort(Kind));
		}

		[TestCase(Ground.Bare, Material.None, 0)]
		[TestCase(Ground.Brush, Material.Timber, 1)]
		[TestCase(Ground.Trees, Material.Timber, 6)]
		[TestCase(Ground.Rock, Material.Stone, 4)]
		[TestCase(Ground.Marble, Material.Marble, 3)]
		[TestCase(Ground.Ruins, Material.Scrap, 2)]
		[TestCase(Ground.Liquid, Material.None, 0)]
		[TestCase(Ground.Held, Material.None, 0)]
		public void RemovalEarns(Ground Kind, Material Expected, int ExpectedAmount)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.YieldOf(Kind, out var amount));
			Assert.AreEqual(ExpectedAmount, amount);
		}

		[Test]
		public void HarderGroundIsAlwaysWorthMorePerCell()
		{
			// Trees over brush, rock over trees: the ladder that makes clearing a jungle or a
			// shale ridge worth the days it costs.
			KingdomPlotRules.YieldOf(Ground.Brush, out var brush);
			KingdomPlotRules.YieldOf(Ground.Trees, out var trees);
			Assert.Greater(trees, brush);
			Assert.Greater(KingdomPlotRules.ClearEffort(Ground.Rock), KingdomPlotRules.ClearEffort(Ground.Trees));
			Assert.Greater(KingdomPlotRules.ClearEffort(Ground.Marble), KingdomPlotRules.ClearEffort(Ground.Rock));
		}

		[TestCase(Ground.Liquid, true)]
		[TestCase(Ground.Held, true)]
		[TestCase(Ground.Bare, false)]
		[TestCase(Ground.Rock, false)]
		[TestCase(Ground.Marble, false)]
		public void WaterAndHeldGroundRefuseThePlot(Ground Kind, bool Expected)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.Refuses(Kind));
		}

		[Test]
		public void ASurveyedRectTotalsItsEffortAndItsYield()
		{
			List<Ground> ground = new List<Ground>();
			ground.AddRange(Cells(4, KingdomPlotRules.GroundKind.Trees));
			ground.AddRange(Cells(2, KingdomPlotRules.GroundKind.Rock));
			ground.AddRange(Cells(14, KingdomPlotRules.GroundKind.Bare));
			Assert.AreEqual(28, KingdomPlotRules.ClearEffort(ground, Underground: false));
			Assert.AreEqual(24, KingdomPlotRules.YieldFor(ground, Material.Timber));
			Assert.AreEqual(8, KingdomPlotRules.YieldFor(ground, Material.Stone));
			Assert.AreEqual(0, KingdomPlotRules.YieldFor(ground, Material.Marble));
			Assert.AreEqual(0, KingdomPlotRules.YieldFor(ground, Material.None));
			Assert.AreEqual(0, KingdomPlotRules.ClearEffort(null, Underground: false));
		}

		[Test]
		public void CarvingCostsDouble()
		{
			List<Ground> ground = Cells(4, KingdomPlotRules.GroundKind.Rock);
			Assert.AreEqual(24, KingdomPlotRules.ClearEffort(ground, Underground: false));
			Assert.AreEqual(48, KingdomPlotRules.ClearEffort(ground, Underground: true));
		}

		// --- Enclosure and the carve bargain -----------------------------------------------

		[Test]
		public void PerimeterIsTheEdgeCells()
		{
			Assert.AreEqual(14, KingdomPlotRules.Perimeter(R(0, 0, 4, 3)));
			Assert.AreEqual(24, KingdomPlotRules.Perimeter(R(0, 0, 7, 5)));
			Assert.AreEqual(5, KingdomPlotRules.Perimeter(R(0, 0, 4, 0)), "a line is all edge");
		}

		[Test]
		public void EnclosureIsFreeUndergroundAndFreeInTheOpen()
		{
			Rect rect = R(0, 0, 4, 3);
			Assert.AreEqual(700L, KingdomPlotRules.EnclosureTicks(rect, Underground: false, Open: false));
			Assert.AreEqual(0L, KingdomPlotRules.EnclosureTicks(rect, Underground: true, Open: false), "the rock is the wall");
			Assert.AreEqual(0L, KingdomPlotRules.EnclosureTicks(rect, Underground: false, Open: true), "a field has no walls");
		}

		[Test]
		public void RaisingCostsTheDesignPlusTheGroundPlusTheWalls()
		{
			Rect rect = R(0, 0, 4, 3);
			List<Ground> ground = new List<Ground>();
			ground.AddRange(Cells(4, KingdomPlotRules.GroundKind.Trees));
			ground.AddRange(Cells(16, KingdomPlotRules.GroundKind.Bare));
			Assert.AreEqual(3500L, KingdomPlotRules.RaiseTicks(1200L, ground, rect, Underground: false, Open: false));
			// Underground: the clearing is twice the work and the enclosure is nothing at all.
			Assert.AreEqual(4400L, KingdomPlotRules.RaiseTicks(1200L, ground, rect, Underground: true, Open: false));
			Assert.AreEqual(2800L, KingdomPlotRules.RaiseTicks(1200L, ground, rect, Underground: false, Open: true));
		}

		[Test]
		public void RaisingNeverFinishesInTheInstantItIsStaked()
		{
			Assert.AreEqual(1L, KingdomPlotRules.RaiseTicks(0L, null, R(0, 0, 0, 0), Underground: false, Open: true));
		}

		[TestCase(9, false)]
		[TestCase(10, false)]
		[TestCase(11, true)]
		[TestCase(40, true)]
		public void TenIsTheSurface(int ZLevel, bool Expected)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.IsUnderground(ZLevel));
		}

		// --- Stages ------------------------------------------------------------------------

		[TestCase(Stage.Staked, 0)]
		[TestCase(Stage.Cleared, 25)]
		[TestCase(Stage.Frame, 50)]
		[TestCase(Stage.Walls, 75)]
		[TestCase(Stage.Done, 100)]
		public void StagesAreEvenlySpaced(Stage Stage, int Expected)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.StagePercent(Stage));
		}

		[TestCase(-5L, Stage.Staked)]
		[TestCase(0L, Stage.Staked)]
		[TestCase(24L, Stage.Staked)]
		[TestCase(25L, Stage.Cleared)]
		[TestCase(49L, Stage.Cleared)]
		[TestCase(50L, Stage.Frame)]
		[TestCase(74L, Stage.Frame)]
		[TestCase(75L, Stage.Walls)]
		[TestCase(99L, Stage.Walls)]
		[TestCase(100L, Stage.Done)]
		[TestCase(100000L, Stage.Done)]
		public void ALongAbsenceLandsOnTheStageItHonestlyBought(long Elapsed, Stage Expected)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.StageAt(Elapsed, 100L));
		}

		[Test]
		public void ARaisingWithNoDurationIsAlreadyDone()
		{
			Assert.AreEqual(Stage.Done, KingdomPlotRules.StageAt(0L, 0L));
			Assert.AreEqual(Stage.Done, KingdomPlotRules.StageAt(0L, -1L));
		}

		[Test]
		public void EveryMiddleStageAnnouncesItselfAndTheEndsDoNot()
		{
			Assert.IsNull(KingdomPlotRules.StageLine(Stage.Staked, "hut"), "the staking says so itself");
			Assert.IsNull(KingdomPlotRules.StageLine(Stage.Done, "hut"), "the raising ceremony tells this one");
			Assert.IsNotNull(KingdomPlotRules.StageLine(Stage.Cleared, "hut"));
			Assert.IsNotNull(KingdomPlotRules.StageLine(Stage.Frame, "hut"));
			Assert.IsNotNull(KingdomPlotRules.StageLine(Stage.Walls, "hut"));
			StringAssert.Contains("hut", KingdomPlotRules.StageLine(Stage.Walls, "hut"));
		}

		[TestCase(Stage.Staked, "staked")]
		[TestCase(Stage.Cleared, "cleared")]
		[TestCase(Stage.Frame, "framed")]
		[TestCase(Stage.Walls, "walled")]
		[TestCase(Stage.Done, "finished")]
		public void EachStageHasItsWord(Stage Stage, string Expected)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.StageLabel(Stage));
		}

		[TestCase(Size.None, 0)]
		[TestCase(Size.Small, 1)]
		[TestCase(Size.Medium, 2)]
		[TestCase(Size.Large, 4)]
		[TestCase(Size.Huge, 6)]
		public void BiggerPlotsAreFurnishedMore(Size Size, int Expected)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.ContentsRolls(Size));
		}

		// --- Wall material is the theme ----------------------------------------------------

		[TestCase("common", "Limestone")]
		[TestCase("verdant", "BrinestalkWall")]
		[TestCase("fungal", "Fulcrete")]
		[TestCase("moonstair", "Black Marble")]
		[TestCase("gyre", "Black Marble")]
		[TestCase("eater", "Verdigris")]
		[TestCase("somebody-elses-style", "Limestone")]
		[TestCase(null, "Limestone")]
		public void AStyleBuildsInItsOwnMaterial(string Style, string Expected)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.WallBlueprintFor(Style, null));
		}

		[Test]
		public void ARuinSiteReusesWhatIsAlreadyLyingAbout()
		{
			Assert.AreEqual("Foamcrete", KingdomPlotRules.WallBlueprintFor("verdant", "Baroque Ruins"));
			Assert.AreEqual("Foamcrete", KingdomPlotRules.WallBlueprintFor("common", "the ruins"));
			Assert.AreEqual("Limestone", KingdomPlotRules.WallBlueprintFor("common", "Saltmarsh"));
		}

		[Test]
		public void EveryMaterialAStyleNamesIsOneVanillaShips()
		{
			// Vanilla's own Village_StructureWall_*Default list. A style that resolved to
			// anything else would raise walls out of a blueprint that does not exist.
			string[] styles = new string[6] { "common", "verdant", "fungal", "moonstair", "eater", "unknown" };
			for (int i = 0; i < styles.Length; i++)
			{
				CollectionAssert.Contains(KingdomPlotRules.WallMaterials, KingdomPlotRules.WallBlueprintFor(styles[i], null));
			}
			CollectionAssert.Contains(KingdomPlotRules.WallMaterials, KingdomPlotRules.WallBlueprintFor("common", "Ruins"));
		}

		// --- Parsing -----------------------------------------------------------------------

		[TestCase("S", Size.Small)]
		[TestCase("s", Size.Small)]
		[TestCase(" small ", Size.Small)]
		[TestCase("M", Size.Medium)]
		[TestCase("medium", Size.Medium)]
		[TestCase("L", Size.Large)]
		[TestCase("large", Size.Large)]
		[TestCase("XL", Size.Huge)]
		[TestCase("xl", Size.Huge)]
		[TestCase("huge", Size.Huge)]
		[TestCase("", Size.None)]
		[TestCase(null, Size.None)]
		public void SizesParse(string Raw, Size Expected)
		{
			Assert.IsTrue(KingdomPlotRules.TryParseSize(Raw, out var size));
			Assert.AreEqual(Expected, size);
		}

		[TestCase("XXL")]
		[TestCase("enormous")]
		[TestCase("4")]
		public void AnUnknownSizeIsAnErrorAndNotSilentlyNoPlot(string Raw)
		{
			Assert.IsFalse(KingdomPlotRules.TryParseSize(Raw, out var size));
			Assert.AreEqual(Size.None, size);
		}

		[TestCase("Yes", true)]
		[TestCase("yes", true)]
		[TestCase("true", true)]
		[TestCase("1", true)]
		[TestCase("No", false)]
		[TestCase("false", false)]
		[TestCase("0", false)]
		[TestCase("", false)]
		[TestCase(null, false)]
		public void FlagsParse(string Raw, bool Expected)
		{
			Assert.IsTrue(KingdomPlotRules.TryParseFlag(Raw, out var value));
			Assert.AreEqual(Expected, value);
		}

		[TestCase("maybe")]
		[TestCase("Y")]
		public void AnUnknownFlagIsAnErrorAndNotSilentlyNo(string Raw)
		{
			Assert.IsFalse(KingdomPlotRules.TryParseFlag(Raw, out var value));
			Assert.IsFalse(value);
		}

		[Test]
		public void AWholePlotSpecParses()
		{
			Assert.IsTrue(KingdomPlotRules.TryParsePlotAttributes("hut", "S", "No", "Yes", " Plot_HutContents ", out var spec, out var error));
			Assert.IsNull(error);
			Assert.AreEqual("hut", spec.Key);
			Assert.AreEqual(Size.Small, spec.Size);
			Assert.IsFalse(spec.Open);
			Assert.IsTrue(spec.RequiresSky);
			Assert.AreEqual("Plot_HutContents", spec.Contents);
		}

		[Test]
		public void ADesignWithNoPlotAttributesIsNotAPlot()
		{
			Assert.IsTrue(KingdomPlotRules.TryParsePlotAttributes("caskrack", null, null, null, null, out var spec, out var error));
			Assert.IsNull(error);
			Assert.AreEqual(Size.None, spec.Size);
			Assert.IsNull(spec.Contents);
		}

		[Test]
		public void ASpecReadTheOldWayFillsItsPlotAndIsWalled()
		{
			// The five-argument call is still supported API, and a design read through it must
			// build exactly what it always built: the whole plot, walled unless it is open.
			Assert.IsTrue(KingdomPlotRules.TryParsePlotAttributes("hut", "M", "No", null, null, out var walled, out _));
			Assert.IsTrue(walled.FillsPlot);
			Assert.IsFalse(walled.RoofDeclared);
			Assert.AreEqual(KingdomPlotRules.RoofState.Walled, walled.Roof);
			Assert.IsTrue(KingdomPlotRules.TryParsePlotAttributes("field", "M", "Yes", null, null, out var open, out _));
			Assert.IsTrue(open.FillsPlot);
			Assert.AreEqual(KingdomPlotRules.RoofState.Open, open.Roof);
		}

		[Test]
		public void TheOldEnclosureAndRaiseSignaturesStillCostWhatTheyCost()
		{
			// Both now answer through the roof table. A settlement mid-raise must not find its
			// walls suddenly free, or suddenly charged for underground.
			Rect rect = At(0, 0, Size.Medium);
			Assert.AreEqual(1200L, KingdomPlotRules.EnclosureTicks(rect, Underground: false, Open: false));
			Assert.AreEqual(0L, KingdomPlotRules.EnclosureTicks(rect, Underground: false, Open: true));
			Assert.AreEqual(0L, KingdomPlotRules.EnclosureTicks(rect, Underground: true, Open: false));
			Assert.AreEqual(1000L + 1600L + 1200L,
				KingdomPlotRules.RaiseTicks(1000L, Cells(4, Ground.Trees), rect, Underground: false, Open: false));
		}

		[Test]
		public void PlotAttributesWithoutASizeAreRefusedRatherThanIgnored()
		{
			// Silently accepting these would ship a design whose Contents table never rolls and
			// whose Sky gate never fires, with nothing anywhere saying so.
			Assert.IsFalse(KingdomPlotRules.TryParsePlotAttributes("x", null, "Yes", null, null, out var spec, out var error));
			Assert.IsNull(spec);
			StringAssert.Contains("without a Plot size", error);
			Assert.IsFalse(KingdomPlotRules.TryParsePlotAttributes("x", null, null, null, "SomeTable", out _, out _));
			Assert.IsFalse(KingdomPlotRules.TryParsePlotAttributes("x", null, null, "Yes", null, out _, out _));
		}

		[Test]
		public void EveryBadAttributeNamesTheDesignAndTheAttribute()
		{
			Assert.IsFalse(KingdomPlotRules.TryParsePlotAttributes("", "S", null, null, null, out _, out var blank));
			StringAssert.Contains("Key", blank);
			Assert.IsFalse(KingdomPlotRules.TryParsePlotAttributes("hall", "XXL", null, null, null, out _, out var size));
			StringAssert.Contains("hall", size);
			StringAssert.Contains("Plot", size);
			Assert.IsFalse(KingdomPlotRules.TryParsePlotAttributes("hall", "L", "sometimes", null, null, out _, out var open));
			StringAssert.Contains("Open", open);
			Assert.IsFalse(KingdomPlotRules.TryParsePlotAttributes("hall", "L", null, "sometimes", null, out _, out var sky));
			StringAssert.Contains("Sky", sky);
		}

		// --- Refusals: nothing stalls in silence (STANDARDS 7b) ----------------------------

		[Test]
		public void AnObstructionRefusalNamesTheThingAndTheGround()
		{
			string refusal = KingdomPlotRules.RefuseObstruction("your waterskin", 41, 12);
			StringAssert.Contains("your waterskin", refusal);
			StringAssert.Contains("41", refusal);
			StringAssert.Contains("12", refusal);
		}

		[Test]
		public void AWaterRefusalNamesTheGroundAndPromisesNotToFillIt()
		{
			string refusal = KingdomPlotRules.RefuseLiquid(30, 8);
			StringAssert.Contains("30", refusal);
			StringAssert.Contains("8", refusal);
			StringAssert.Contains("never filled", refusal);
		}

		[Test]
		public void AStageRefusalNamesTheStageThatWouldLiftIt()
		{
			string refusal = KingdomPlotRules.RefuseStage(Size.Huge, "Ashfall", GrowthStage.Camp);
			StringAssert.Contains("great", refusal);
			StringAssert.Contains("city", refusal);
			StringAssert.Contains("Ashfall", refusal);
			StringAssert.Contains("camp", refusal);
		}

		[Test]
		public void ASkyRefusalNamesTheDesign()
		{
			StringAssert.Contains("sailvane", KingdomPlotRules.RefuseSky("sailvane"));
		}

		[Test]
		public void RoomAndBudgetRefusalsAreDifferentSentences()
		{
			string room = KingdomPlotRules.RefuseRoom(Size.Large);
			string budget = KingdomPlotRules.RefuseBudget("Ashfall");
			StringAssert.Contains("large", room);
			StringAssert.Contains("Ashfall", budget);
			StringAssert.Contains("struck", budget);
			Assert.AreNotEqual(room, budget, "the founder must be able to tell blocked ground from a full plan");
		}

		[TestCase(Size.Small, "small")]
		[TestCase(Size.Medium, "middling")]
		[TestCase(Size.Large, "large")]
		[TestCase(Size.Huge, "great")]
		[TestCase(Size.None, "")]
		public void EveryTierHasAWordForItself(Size Size, string Expected)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.SizeName(Size));
		}
	}
}
#endif
