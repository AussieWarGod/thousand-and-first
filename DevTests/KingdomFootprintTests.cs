#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;
using CellKind = ThousandAndFirst.KingdomAdoptRules.CellKind;
using Ground = ThousandAndFirst.KingdomPlotRules.GroundKind;
using Rect = ThousandAndFirst.KingdomPlotRules.PlotRect;
using Roof = ThousandAndFirst.KingdomPlotRules.RoofState;
using Role = ThousandAndFirst.KingdomAdoptRules.RoleKind;
using Size = ThousandAndFirst.KingdomPlotRules.PlotSize;
using Step = ThousandAndFirst.KingdomPlotRules.ChainStep;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// Footprints and roofs: the ground a design's TIER stands on inside the plot, what stands
	/// over it, and the one invariant the two layers owe each other &mdash; footprint fits plot.
	/// <para>
	/// Every number is asserted by exact value. Widening a tier, flipping a roof predicate,
	/// dropping the fits check, moving the footprint off the heart-facing side, or letting a
	/// design that declares nothing new build differently than it did before all fail here.
	/// </para>
	/// </summary>
	public class KingdomFootprintTests
	{
		private static Rect R(int X1, int Y1, int X2, int Y2)
		{
			return new Rect(X1, Y1, X2, Y2);
		}

		private static Step S(string Key, int Width, int Height)
		{
			return new Step(Key, Key, Width, Height, Roof.Walled);
		}

		private static List<Step> Chain(params Step[] Steps)
		{
			return new List<Step>(Steps);
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

		// --- Roofs -------------------------------------------------------------------------

		[TestCase(Roof.Open, 0)]
		[TestCase(Roof.Soft, 1)]
		[TestCase(Roof.Walled, 2)]
		[TestCase(Roof.Carved, 2)]
		public void ShelterIsRankedAndRockSheltersLikeAWall(Roof Roof, int Expected)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.ShelterRank(Roof));
		}

		[TestCase(Roof.Open, false)]
		[TestCase(Roof.Soft, true)]
		[TestCase(Roof.Walled, true)]
		[TestCase(Roof.Carved, true)]
		public void ABedWantsCanvasAtLeast(Roof Roof, bool Expected)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.HoldsBeds(Roof));
		}

		[TestCase(Roof.Open, true)]
		[TestCase(Roof.Soft, true)]
		[TestCase(Roof.Walled, false)]
		[TestCase(Roof.Carved, false)]
		public void OnlyWallAndRockKeepTheWeatherOut(Roof Roof, bool Expected)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.AdmitsSky(Roof));
		}

		[TestCase(Roof.Open, false)]
		[TestCase(Roof.Soft, false)]
		[TestCase(Roof.Walled, true)]
		[TestCase(Roof.Carved, false)]
		public void TheSettlementOnlyEverRaisesAWalledEnclosure(Roof Roof, bool Expected)
		{
			// Canvas is the design's own object and the rock is the hill's; only a walled tier
			// costs the settlement a perimeter.
			Assert.AreEqual(Expected, KingdomPlotRules.RaisesWalls(Roof));
		}

		[TestCase(Roof.Open, false)]
		[TestCase(Roof.Soft, false)]
		[TestCase(Roof.Walled, true)]
		[TestCase(Roof.Carved, true)]
		public void SomethingStandsRoundAWalledOrCarvedTier(Roof Roof, bool Expected)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.Encloses(Roof));
		}

		[Test]
		public void EveryRoofHasItsOwnWords()
		{
			Assert.AreEqual("open to the sky", KingdomPlotRules.RoofWord(Roof.Open));
			Assert.AreEqual("under canvas", KingdomPlotRules.RoofWord(Roof.Soft));
			Assert.AreEqual("walled", KingdomPlotRules.RoofWord(Roof.Walled));
			Assert.AreEqual("carved from the rock", KingdomPlotRules.RoofWord(Roof.Carved));
		}

		[Test]
		public void ADesignThatDeclaresNoRoofGetsTheOneItAlwaysGot()
		{
			Assert.AreEqual(Roof.Walled, KingdomPlotRules.DefaultRoof(false));
			Assert.AreEqual(Roof.Open, KingdomPlotRules.DefaultRoof(true));
		}

		[TestCase(Roof.Soft)]
		[TestCase(Roof.Walled)]
		[TestCase(Roof.Carved)]
		public void UndergroundEverythingTheSettlementWouldEncloseIsCarved(Roof Declared)
		{
			Assert.AreEqual(Roof.Carved, KingdomPlotRules.RoofOnGround(Declared, Underground: true));
			Assert.AreEqual(Declared, KingdomPlotRules.RoofOnGround(Declared, Underground: false));
		}

		[Test]
		public void AnOpenPlotStaysOpenUnderTheRock()
		{
			// Carving replaces the enclosure a design would have raised; it does not roof ground
			// the design deliberately left unroofed. A field, salt-pan, market square or
			// reservoir cut into the rock is open ground with stone around it.
			Assert.AreEqual(Roof.Open, KingdomPlotRules.RoofOnGround(Roof.Open, Underground: true));
			Assert.AreEqual(Roof.Open, KingdomPlotRules.RoofOnGround(Roof.Open, Underground: false));
		}

		[Test]
		public void TheDeclaredRoofAndTheMeasuredRoofAgreeUnderTheRock()
		{
			// The invariant the Open fix is really about: what a design declares and what walls
			// prove must answer the same question the same way. RoofFromEnclosure has always read
			// unbounded ground underground as open; RoofOnGround used to contradict it.
			KingdomAdoptRules.EnclosureMeasurement field = KingdomAdoptRules.MeasureEnclosure(12, 12, delegate(int x, int y)
			{
				return CellKind.Open;
			});
			Assert.AreEqual(KingdomPlotRules.RoofFromEnclosure(field, Underground: true),
				KingdomPlotRules.RoofOnGround(KingdomPlotRules.DefaultRoof(Open: true), Underground: true));

			KingdomAdoptRules.EnclosureMeasurement room = KingdomAdoptRules.MeasureEnclosure(12, 12, Room(10, 10, 15, 14, 12, 10));
			Assert.AreEqual(KingdomPlotRules.RoofFromEnclosure(room, Underground: true),
				KingdomPlotRules.RoofOnGround(KingdomPlotRules.DefaultRoof(Open: false), Underground: true));
		}

		[Test]
		public void AnUndergroundFieldIsNotShelterAndRaisesNoWall()
		{
			// What the bug actually cost: a carved roof holds beds and encloses, so an
			// underground field became a sealed rock chamber people could be housed in, floored
			// across its whole rect with a door cut into it.
			Roof underground = KingdomPlotRules.RoofOnGround(KingdomPlotRules.DefaultRoof(Open: true), Underground: true);
			Assert.IsFalse(KingdomPlotRules.HoldsBeds(underground), "nobody sleeps in a field, above the rock or under it");
			Assert.IsFalse(KingdomPlotRules.Encloses(underground), "an open plot has no enclosure to be carved out of");
			Assert.IsFalse(KingdomPlotRules.RaisesWalls(underground), "the settlement raises nothing round an open plot");
			Assert.AreEqual(0L, KingdomPlotRules.EnclosureTicks(new KingdomPlotRules.PlotRect(0, 0, 5, 5), underground),
				"an open plot costs no enclosure on any stratum");
		}

		// --- The roofed test IS the adoption enclosure test ---------------------------------

		private static KingdomAdoptRules.CellLookup Room(int X1, int Y1, int X2, int Y2, int DoorX, int DoorY)
		{
			return delegate(int x, int y)
			{
				if (x == DoorX && y == DoorY)
				{
					return CellKind.Door;
				}
				if (x < X1 || y < Y1 || x > X2 || y > Y2)
				{
					return CellKind.Wall;
				}
				if (x == X1 || y == Y1 || x == X2 || y == Y2)
				{
					return CellKind.Wall;
				}
				return CellKind.Open;
			};
		}

		[Test]
		public void AMeasuredRoomReadsWalledAndOpenGroundReadsOpen()
		{
			// Reusing the adoption fill rather than a second roofed test of our own is the whole
			// point: a founder-built house and a commissioned one are judged by one measure.
			KingdomAdoptRules.EnclosureMeasurement room = KingdomAdoptRules.MeasureEnclosure(12, 12, Room(10, 10, 15, 14, 12, 10));
			Assert.IsTrue(room.Bounded, "a walled room is bounded");
			Assert.AreEqual(Roof.Walled, KingdomPlotRules.RoofFromEnclosure(room, Underground: false));
			Assert.AreEqual(Roof.Carved, KingdomPlotRules.RoofFromEnclosure(room, Underground: true));

			KingdomAdoptRules.EnclosureMeasurement field = KingdomAdoptRules.MeasureEnclosure(12, 12, delegate(int x, int y)
			{
				return CellKind.Open;
			});
			Assert.IsFalse(field.Bounded, "open ground never closes");
			Assert.AreEqual(Roof.Open, KingdomPlotRules.RoofFromEnclosure(field, Underground: false));
			Assert.AreEqual(Roof.Open, KingdomPlotRules.RoofFromEnclosure(field, Underground: true),
				"open ground underground is still open ground, not a carved room");
		}

		[TestCase(Role.Housing, Roof.Open, false)]
		[TestCase(Role.Housing, Roof.Soft, true)]
		[TestCase(Role.Housing, Roof.Walled, true)]
		[TestCase(Role.Work, Roof.Soft, false)]
		[TestCase(Role.Work, Roof.Walled, true)]
		[TestCase(Role.Work, Roof.Carved, true)]
		[TestCase(Role.Storage, Roof.Open, true)]
		public void ARoleAsksTheRoofForExactlyWhatItNeeds(Role Role, Roof Roof, bool Expected)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.RoofMeetsRole(Role, Roof));
		}

		// --- The invariant: footprint fits plot ---------------------------------------------

		[TestCase(Size.Small, 6, 4, true)]
		[TestCase(Size.Small, 3, 3, true)]
		[TestCase(Size.Small, 7, 4, false)]
		[TestCase(Size.Small, 6, 5, false)]
		[TestCase(Size.Medium, 8, 6, true)]
		[TestCase(Size.Medium, 9, 6, false)]
		[TestCase(Size.Large, 12, 10, true)]
		[TestCase(Size.Huge, 20, 18, true)]
		[TestCase(Size.Huge, 20, 19, false)]
		[TestCase(Size.None, 1, 1, false)]
		public void AFootprintFitsItsPlotOrItDoesNot(Size Plot, int Width, int Height, bool Expected)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.FootprintFits(Plot, Width, Height));
		}

		[TestCase(0, 3)]
		[TestCase(3, 0)]
		[TestCase(-1, 4)]
		public void AFootprintWithNoGroundInItFitsNothing(int Width, int Height)
		{
			Assert.IsFalse(KingdomPlotRules.FootprintFits(Size.Huge, Width, Height));
		}

		[TestCase(3, 3, Size.Small)]
		[TestCase(6, 4, Size.Small)]
		[TestCase(7, 4, Size.Medium)]
		[TestCase(8, 6, Size.Medium)]
		[TestCase(9, 6, Size.Large)]
		[TestCase(12, 10, Size.Large)]
		[TestCase(13, 10, Size.Huge)]
		[TestCase(20, 18, Size.Huge)]
		[TestCase(21, 18, Size.None)]
		[TestCase(20, 19, Size.None)]
		public void TheSmallestPlotThatHoldsAFootprintIsNamed(int Width, int Height, Size Expected)
		{
			Assert.AreEqual(Expected, KingdomPlotRules.SmallestPlotFor(Width, Height));
		}

		[Test]
		public void ATierThatDeclaresNoFootprintFillsItsPlot()
		{
			Assert.IsTrue(KingdomPlotRules.TryParsePlotAttributes("hut", "M", null, null, null, null, null, out var spec, out var error));
			Assert.IsNull(error);
			Assert.IsTrue(spec.FillsPlot);
			Assert.IsTrue(KingdomPlotRules.TryFootprint(spec, out var width, out var height));
			Assert.AreEqual(8, width, "an M plot is 8 across and a tier that declares nothing takes all of it");
			Assert.AreEqual(6, height);
		}

		[Test]
		public void ATierThatDeclaresAFootprintTakesExactlyThat()
		{
			Assert.IsTrue(KingdomPlotRules.TryParsePlotAttributes("tent", "S", null, null, null, "3x2", "Soft", out var spec, out var error));
			Assert.IsNull(error);
			Assert.IsFalse(spec.FillsPlot);
			Assert.IsTrue(KingdomPlotRules.TryFootprint(spec, out var width, out var height));
			Assert.AreEqual(3, width);
			Assert.AreEqual(2, height);
			Assert.AreEqual(Roof.Soft, spec.Roof);
			Assert.IsTrue(spec.RoofDeclared);
			Assert.IsFalse(spec.Open, "canvas is not an open plot: something is over it");
		}

		// --- Where the building sits inside the plot ----------------------------------------

		[Test]
		public void TheBuildingFrontsTheHeartAndTheYardLiesBehindIt()
		{
			Rect plot = R(10, 10, 17, 15);
			Assert.IsTrue(KingdomPlotRules.TryFootprintWithin(plot, 4, 3, 10, 5, out var north));
			Assert.AreEqual(R(10, 10, 13, 12), north, "a heart to the north pulls the building to the north edge");
			Assert.IsTrue(KingdomPlotRules.TryFootprintWithin(plot, 4, 3, 30, 12, out var east));
			Assert.AreEqual(R(14, 10, 17, 12), east, "a heart to the east pulls it to the east edge");
			Assert.IsTrue(KingdomPlotRules.TryFootprintWithin(plot, 4, 3, 13, 30, out var south));
			Assert.AreEqual(R(10, 13, 13, 15), south, "a heart to the south pulls it to the south edge");
		}

		[Test]
		public void TiesBreakNorthThenWestSoOnePlotAlwaysLaysOutTheSameWay()
		{
			Rect plot = R(0, 0, 7, 5);
			Assert.IsTrue(KingdomPlotRules.TryFootprintWithin(plot, 2, 2, 3, 2, out var first));
			Assert.IsTrue(KingdomPlotRules.TryFootprintWithin(plot, 2, 2, 3, 2, out var again));
			Assert.AreEqual(first, again, "the same question must always give the same ground");
			Assert.LessOrEqual(first.Y1, 2);
		}

		[Test]
		public void AFootprintLargerThanItsPlotIsRefusedRatherThanTrimmed()
		{
			Rect plot = R(10, 10, 14, 13);
			Assert.IsFalse(KingdomPlotRules.TryFootprintWithin(plot, 6, 4, 10, 10, out var wide));
			Assert.AreEqual(R(0, 0, 0, 0), wide);
			Assert.IsFalse(KingdomPlotRules.TryFootprintWithin(plot, 5, 5, 10, 10, out _));
			Assert.IsTrue(KingdomPlotRules.TryFootprintWithin(plot, 5, 4, 10, 10, out var exact));
			Assert.AreEqual(plot, exact, "a footprint the size of its plot is the plot");
		}

		// --- The yard -----------------------------------------------------------------------

		[Test]
		public void TheYardIsThePlotMinusTheFootprintBandByBand()
		{
			Rect plot = R(10, 10, 17, 15);
			Rect footprint = R(10, 10, 13, 12);
			List<Rect> bands = KingdomPlotRules.YardBands(plot, footprint);
			Assert.AreEqual(2, bands.Count, "north and west bands are empty: the building is in that corner");
			Assert.AreEqual(R(10, 13, 17, 15), bands[0], "the south band runs the full width");
			Assert.AreEqual(R(14, 10, 17, 12), bands[1], "the east band only runs beside the building");
			Assert.AreEqual(36, KingdomPlotRules.YardArea(plot, footprint));
			Assert.AreEqual(plot.Area - footprint.Area, KingdomPlotRules.YardArea(plot, footprint),
				"the yard and the building together are the whole plot, exactly once each");
		}

		[Test]
		public void AFootprintInTheMiddleLeavesFourBands()
		{
			Rect plot = R(0, 0, 9, 9);
			Rect footprint = R(3, 3, 6, 6);
			List<Rect> bands = KingdomPlotRules.YardBands(plot, footprint);
			Assert.AreEqual(4, bands.Count);
			Assert.AreEqual(R(0, 0, 9, 2), bands[0]);
			Assert.AreEqual(R(0, 7, 9, 9), bands[1]);
			Assert.AreEqual(R(0, 3, 2, 6), bands[2]);
			Assert.AreEqual(R(7, 3, 9, 6), bands[3]);
			Assert.AreEqual(100 - 16, KingdomPlotRules.YardArea(plot, footprint));
		}

		[Test]
		public void ATierThatFillsItsPlotHasNoYardAtAll()
		{
			Rect plot = R(10, 10, 17, 15);
			Assert.AreEqual(0, KingdomPlotRules.YardBands(plot, plot).Count);
			Assert.AreEqual(0, KingdomPlotRules.YardArea(plot, plot));
			Assert.IsFalse(KingdomPlotRules.InYard(plot, plot, 12, 12));
		}

		[Test]
		public void GroundOutsideThePlotIsNotYardAndNamesNoBands()
		{
			Rect plot = R(10, 10, 17, 15);
			Rect footprint = R(10, 10, 13, 12);
			Assert.IsTrue(KingdomPlotRules.InYard(plot, footprint, 15, 11));
			Assert.IsFalse(KingdomPlotRules.InYard(plot, footprint, 11, 11), "under the building is not yard");
			Assert.IsFalse(KingdomPlotRules.InYard(plot, footprint, 20, 11), "off the plot is not yard");
			Assert.AreEqual(0, KingdomPlotRules.YardBands(plot, R(9, 9, 12, 12)).Count,
				"a footprint hanging off the plot has no yard anybody can name");
		}

		[Test]
		public void GrowingIntoTheSameRectTakesNoNewGround()
		{
			Rect small = R(10, 10, 13, 12);
			Assert.IsFalse(KingdomPlotRules.TakesNewGround(small, small));
			Assert.IsTrue(KingdomPlotRules.TakesNewGround(small, R(10, 10, 14, 12)), "one more column is new ground");
			Assert.IsFalse(KingdomPlotRules.TakesNewGround(R(10, 10, 17, 15), small), "shrinking takes nothing");
		}

		// --- Staking foresight ---------------------------------------------------------------

		[Test]
		public void AChainFitsUntilOneTierDoesNot()
		{
			List<Step> chain = Chain(S("tent", 3, 2), S("tentrow", 5, 4), S("hut", 7, 4));
			Assert.IsFalse(KingdomPlotRules.ChainFits(Size.Small, chain, out var unfit));
			Assert.AreEqual(2, unfit, "the first tier that will not fit is the one the founder is told about");
			Assert.IsTrue(KingdomPlotRules.ChainFits(Size.Medium, chain, out var none));
			Assert.AreEqual(-1, none);
			Assert.AreEqual(Size.Medium, KingdomPlotRules.SmallestPlotForChain(chain));
		}

		[Test]
		public void TheSmallestPlotForAChainTakesTheWidestAndTheTallestSeparately()
		{
			// A chain of a wide-and-short tier and a narrow-and-tall one needs a plot that holds
			// both spans, which is not either tier's own smallest plot.
			List<Step> chain = Chain(S("wide", 11, 3), S("tall", 4, 8));
			Assert.AreEqual(Size.Small, KingdomPlotRules.SmallestPlotFor(4, 4));
			Assert.AreEqual(Size.Large, KingdomPlotRules.SmallestPlotForChain(chain));
		}

		[Test]
		public void AnEmptyChainFitsEverythingAndFitsNoPlot()
		{
			Assert.IsTrue(KingdomPlotRules.ChainFits(Size.Small, null, out var unfit));
			Assert.AreEqual(-1, unfit);
			Assert.AreEqual(Size.None, KingdomPlotRules.SmallestPlotForChain(new List<Step>()));
		}

		[Test]
		public void StakeableSizesRunFromTheDesignsOwnGroundToWhatTheStageAllows()
		{
			List<Step> chain = Chain(S("hut", 5, 4), S("hutyard", 8, 6));
			List<Size> town = KingdomPlotRules.StakeableSizes(Size.Small, GrowthStage.Town, chain);
			CollectionAssert.AreEqual(new List<Size> { Size.Small, Size.Medium, Size.Large }, town,
				"a town lays up to a large plot, and never smaller than the design asks for");
			List<Size> camp = KingdomPlotRules.StakeableSizes(Size.Small, GrowthStage.Camp, chain);
			CollectionAssert.AreEqual(new List<Size> { Size.Small }, camp, "a camp has one choice and no ceiling to buy");
		}

		[Test]
		public void ADesignWhoseFirstTierNeedsMoreGroundNeverOffersTheSmallerStake()
		{
			List<Step> chain = Chain(S("hall", 10, 7));
			List<Size> sizes = KingdomPlotRules.StakeableSizes(Size.Small, GrowthStage.City, chain);
			CollectionAssert.DoesNotContain(sizes, Size.Small);
			Assert.AreEqual(Size.Large, sizes[0], "the floor is the ground the work itself stands on");
		}

		[Test]
		public void ADesignThatIsNotAPlotOffersNoStakeAtAll()
		{
			Assert.AreEqual(0, KingdomPlotRules.StakeableSizes(Size.None, GrowthStage.City, null).Count);
			Assert.AreEqual(0, KingdomPlotRules.StakeableSizes(Size.Small, GrowthStage.City, Chain(S("vast", 40, 40))).Count);
		}

		[Test]
		public void TheForesightNamesTheTierThatOutgrowsThePlotAndThePlotThatWouldHoldIt()
		{
			List<Step> chain = Chain(S("tent", 3, 2), S("tentrow", 5, 4), S("hut", 7, 4));
			string line = KingdomPlotRules.ForesightLine(Size.Small, chain);
			StringAssert.Contains("6 by 4", line, "the ground being staked is named");
			StringAssert.Contains("hut 7 by 4", line, "the tier that outgrows it is named with its own ground");
			StringAssert.Contains("middling", line, "and so is the plot that would hold the whole chain");
			StringAssert.Contains("struck", line, "the ceiling is a choice with a way out, and it says so");
		}

		[Test]
		public void AChainThatFitsSaysSoAndPromisesNoCeiling()
		{
			List<Step> chain = Chain(S("hut", 4, 3), S("hutyard", 5, 4));
			string line = KingdomPlotRules.ForesightLine(Size.Small, chain);
			StringAssert.Contains("Every tier", line);
			Assert.IsFalse(line.Contains("struck"), "nothing has to be struck when everything fits");
		}

		[Test]
		public void ADesignThatNeverGrowsSaysThatInsteadOfPromisingRoom()
		{
			string line = KingdomPlotRules.ForesightLine(Size.Small, Chain(S("caskshed", 5, 4)));
			StringAssert.Contains("never grows", line);
			Assert.IsNull(KingdomPlotRules.ForesightLine(Size.Small, new List<Step>()));
			Assert.IsNull(KingdomPlotRules.ForesightLine(Size.None, Chain(S("hut", 4, 3))));
		}

		[Test]
		public void AStakeOptionSaysHowFarItCarriesAndHowMuchYardItLeaves()
		{
			List<Step> chain = Chain(S("tent", 3, 2), S("tentrow", 5, 4), S("hut", 7, 4));
			string small = KingdomPlotRules.StakeOptionLine(Size.Small, chain);
			StringAssert.Contains("6 by 4", small);
			StringAssert.Contains("tentrow", small, "the last tier this ground carries is named");
			StringAssert.Contains("18 cells of yard", small, "24 cells of plot less the 6 the tent stands on");
			string medium = KingdomPlotRules.StakeOptionLine(Size.Medium, chain);
			StringAssert.Contains("holds every tier", medium);
			StringAssert.Contains("42 cells of yard", medium);
		}

		[Test]
		public void AStakeTooSmallForTheWorkItselfSaysThatAndNotAYardCount()
		{
			string line = KingdomPlotRules.StakeOptionLine(Size.Small, Chain(S("hall", 10, 7)));
			StringAssert.Contains("too little ground", line);
			Assert.IsFalse(line.Contains("yard"));
		}

		[Test]
		public void TheChainLineReadsInTheOrderTheSettlementBuildsIt()
		{
			Assert.AreEqual("tent 3 by 2, then hut 5 by 4",
				KingdomPlotRules.ChainFootprintLine(Chain(S("tent", 3, 2), S("hut", 5, 4))));
			Assert.IsNull(KingdomPlotRules.ChainFootprintLine(null));
		}

		// --- Parsing ---------------------------------------------------------------------------

		[TestCase("6x4", 6, 4)]
		[TestCase("6X4", 6, 4)]
		[TestCase(" 6 x 4 ", 6, 4)]
		[TestCase("20x14", 20, 14)]
		[TestCase("", 0, 0)]
		[TestCase(null, 0, 0)]
		public void FootprintsParse(string Raw, int ExpectedWidth, int ExpectedHeight)
		{
			Assert.IsTrue(KingdomPlotRules.TryParseFootprint(Raw, out var width, out var height));
			Assert.AreEqual(ExpectedWidth, width);
			Assert.AreEqual(ExpectedHeight, height);
		}

		[TestCase("6")]
		[TestCase("6x")]
		[TestCase("6x4x2")]
		[TestCase("wide")]
		[TestCase("0x4")]
		[TestCase("6x0")]
		[TestCase("-6x4")]
		[TestCase("6 by 4")]
		public void ABadFootprintIsAnErrorAndNotSilentlyTheWholePlot(string Raw)
		{
			// Filling the plot on a typo would move a building's walls without saying so.
			Assert.IsFalse(KingdomPlotRules.TryParseFootprint(Raw, out var width, out var height));
			Assert.AreEqual(0, width);
			Assert.AreEqual(0, height);
		}

		[TestCase("Open", Roof.Open)]
		[TestCase("open", Roof.Open)]
		[TestCase("Soft", Roof.Soft)]
		[TestCase("canvas", Roof.Soft)]
		[TestCase("Walled", Roof.Walled)]
		[TestCase(" walls ", Roof.Walled)]
		[TestCase("Carved", Roof.Carved)]
		public void RoofsParse(string Raw, Roof Expected)
		{
			Assert.IsTrue(KingdomPlotRules.TryParseRoof(Raw, out var roof, out var declared));
			Assert.AreEqual(Expected, roof);
			Assert.IsTrue(declared);
		}

		[TestCase("")]
		[TestCase(null)]
		public void AnAbsentRoofIsNoClaimRatherThanAWalledOne(string Raw)
		{
			Assert.IsTrue(KingdomPlotRules.TryParseRoof(Raw, out var roof, out var declared));
			Assert.IsFalse(declared, "a design that said nothing about its roof has not claimed one");
			Assert.AreEqual(Roof.Walled, roof);
		}

		[TestCase("thatched")]
		[TestCase("roofed")]
		[TestCase("2")]
		public void AnUnknownRoofIsAnError(string Raw)
		{
			Assert.IsFalse(KingdomPlotRules.TryParseRoof(Raw, out _, out var declared));
			Assert.IsFalse(declared);
		}

		[Test]
		public void AFootprintLargerThanItsPlotIsRefusedAtLoadWithBothSpansNamed()
		{
			Assert.IsFalse(KingdomPlotRules.TryParsePlotAttributes("hall", "S", null, null, null, "8x6", null, out var spec, out var error));
			Assert.IsNull(spec);
			StringAssert.Contains("hall", error);
			StringAssert.Contains("8 by 6", error);
			StringAssert.Contains("6 by 4", error);
			StringAssert.Contains("never outgrows its plot", error);
		}

		[Test]
		public void AFootprintOrARoofWithoutAPlotSizeIsRefusedRatherThanIgnored()
		{
			Assert.IsFalse(KingdomPlotRules.TryParsePlotAttributes("x", null, null, null, null, "3x3", null, out _, out var footprint));
			StringAssert.Contains("without a Plot size", footprint);
			Assert.IsFalse(KingdomPlotRules.TryParsePlotAttributes("x", null, null, null, null, null, "Soft", out _, out var roof));
			StringAssert.Contains("without a Plot size", roof);
		}

		[Test]
		public void OpenAndARoofThatDisagreeAreRefusedRatherThanOneWinningQuietly()
		{
			Assert.IsFalse(KingdomPlotRules.TryParsePlotAttributes("pan", "S", "Yes", null, null, null, "Walled", out _, out var walled));
			StringAssert.Contains("disagree", walled);
			Assert.IsFalse(KingdomPlotRules.TryParsePlotAttributes("hut", "S", "No", null, null, null, "Open", out _, out var open));
			StringAssert.Contains("disagree", open);
			Assert.IsTrue(KingdomPlotRules.TryParsePlotAttributes("pan", "S", "Yes", null, null, null, "Open", out var agreed, out _));
			Assert.IsTrue(agreed.Open);
			Assert.AreEqual(Roof.Open, agreed.Roof);
		}

		[Test]
		public void ARoofOfOpenIsAnOpenPlotWithoutAlsoSayingSo()
		{
			Assert.IsTrue(KingdomPlotRules.TryParsePlotAttributes("field", "M", null, null, null, null, "Open", out var spec, out var error));
			Assert.IsNull(error);
			Assert.IsTrue(spec.Open, "one answer, not two that can drift apart");
			Assert.AreEqual(Roof.Open, spec.Roof);
		}

		[Test]
		public void OnlyADeclaredRoofCanContradictADesignThatNeedsWeather()
		{
			// The catchment case: Sky with no Roof at all is every entry written before roofs
			// existed, and it must go on building exactly as it did.
			Assert.IsTrue(KingdomPlotRules.TryParsePlotAttributes("catchment", "S", null, "Yes", null, null, null, out var quiet, out _));
			Assert.IsFalse(KingdomPlotRules.RoofRefusesSky(quiet), "a design that claimed no roof has claimed nothing to contradict");
			Assert.IsTrue(KingdomPlotRules.TryParsePlotAttributes("vane", "S", null, "Yes", null, null, "Walled", out var walled, out _));
			Assert.IsTrue(KingdomPlotRules.RoofRefusesSky(walled));
			Assert.IsTrue(KingdomPlotRules.TryParsePlotAttributes("vane", "S", null, "Yes", null, null, "Soft", out var canvas, out _));
			Assert.IsFalse(KingdomPlotRules.RoofRefusesSky(canvas), "canvas rolls back, so the sky still reaches it");
			Assert.IsFalse(KingdomPlotRules.RoofRefusesSky(null));
		}

		[Test]
		public void EveryEntryWrittenBeforeFootprintsExistedReadsExactlyAsItDid()
		{
			// The migration guarantee, asserted rather than asserted-about: the old five-argument
			// call and the new one with both new attributes absent must agree in every field.
			Assert.IsTrue(KingdomPlotRules.TryParsePlotAttributes("hut", "S", "No", "No", "SomeTable", out var before, out _));
			Assert.IsTrue(KingdomPlotRules.TryParsePlotAttributes("hut", "S", "No", "No", "SomeTable", null, null, out var after, out _));
			Assert.AreEqual(before.Size, after.Size);
			Assert.AreEqual(before.Open, after.Open);
			Assert.AreEqual(before.RequiresSky, after.RequiresSky);
			Assert.AreEqual(before.Contents, after.Contents);
			Assert.IsTrue(before.FillsPlot);
			Assert.IsFalse(before.RoofDeclared);
			Assert.AreEqual(Roof.Walled, before.Roof);
		}

		// --- Cost: clearing is the plot, walls are the building -------------------------------

		[TestCase(Roof.Walled, 700L)]
		[TestCase(Roof.Open, 0L)]
		[TestCase(Roof.Soft, 0L)]
		[TestCase(Roof.Carved, 0L)]
		public void OnlyAWalledTierCostsAPerimeter(Roof Roof, long Expected)
		{
			// A 5x4 rect has 14 edge cells at 50 ticks each.
			Assert.AreEqual(Expected, KingdomPlotRules.EnclosureTicks(R(0, 0, 4, 3), Roof));
		}

		[Test]
		public void TheRoofOverloadAgreesWithTheFlagsItReplaced()
		{
			// Three combinations are all the old two flags could say, and each must cost what it
			// always cost or a settlement in flight would find its walls suddenly free.
			Rect rect = R(0, 0, 7, 5);
			Assert.AreEqual(KingdomPlotRules.EnclosureTicks(rect, Underground: false, Open: false),
				KingdomPlotRules.EnclosureTicks(rect, Roof.Walled));
			Assert.AreEqual(KingdomPlotRules.EnclosureTicks(rect, Underground: false, Open: true),
				KingdomPlotRules.EnclosureTicks(rect, Roof.Open));
			Assert.AreEqual(KingdomPlotRules.EnclosureTicks(rect, Underground: true, Open: false),
				KingdomPlotRules.EnclosureTicks(rect, Roof.Carved));
		}

		[Test]
		public void ClearingIsReckonedOnThePlotAndWallsOnTheFootprint()
		{
			// A wide stake costs more to clear and no more to wall: 48 cells of brush at one
			// effort each is 4,800 ticks, and every one of the 3x2 building's six cells is edge,
			// at 50 ticks each.
			Rect footprint = R(0, 0, 2, 1);
			long ticks = KingdomPlotRules.RaiseTicks(1000L, Cells(48, Ground.Brush), footprint, Roof.Walled, Underground: false);
			Assert.AreEqual(1000L + 4800L + 300L, ticks);
			long open = KingdomPlotRules.RaiseTicks(1000L, Cells(48, Ground.Brush), footprint, Roof.Open, Underground: false);
			Assert.AreEqual(1000L + 4800L, open);
		}

		[Test]
		public void ARaisingNeverFinishesInTheInstantItIsStaked()
		{
			Assert.AreEqual(1L, KingdomPlotRules.RaiseTicks(0L, Cells(4, Ground.Bare), R(0, 0, 1, 1), Roof.Open, Underground: false));
		}

		// --- Refusals: nothing stalls in silence (STANDARDS 7b) -------------------------------

		[Test]
		public void AFootprintRefusalNamesTheWorkTheGroundItWantsAndTheGroundItHas()
		{
			string refusal = KingdomPlotRules.RefuseFootprint("stone house", 8, 6, Size.Small);
			StringAssert.Contains("stone house", refusal);
			StringAssert.Contains("8 by 6", refusal);
			StringAssert.Contains("6 by 4", refusal);
			StringAssert.Contains("more ground than this plot holds", refusal);
			StringAssert.Contains("stake larger ground", refusal);
		}

		[Test]
		public void ARoofRefusalNamesTheDesignAndWhatIsOverIt()
		{
			string refusal = KingdomPlotRules.RefuseRoofSky("sailvane", Roof.Walled);
			StringAssert.Contains("sailvane", refusal);
			StringAssert.Contains("walled", refusal);
			Assert.AreNotEqual(KingdomPlotRules.RefuseSky("sailvane"), refusal,
				"a design refused underground and a design refused by its own tier are different problems");
		}

		[Test]
		public void AYardWorkInTheWayIsNamedAndNeverQuietlyRemoved()
		{
			string refusal = KingdomPlotRules.RefuseYardWork("timber hut", "hut and yard", "hide rack");
			StringAssert.Contains("timber hut", refusal);
			StringAssert.Contains("hut and yard", refusal);
			StringAssert.Contains("hide rack", refusal);
			StringAssert.Contains("comes down on its own", refusal);
		}

		[Test]
		public void ABedRefusalSaysWhatWouldLiftIt()
		{
			string refusal = KingdomPlotRules.RefuseBedRoof("field shelter");
			StringAssert.Contains("field shelter", refusal);
			StringAssert.Contains("canvas", refusal);
		}
	}
}
#endif
