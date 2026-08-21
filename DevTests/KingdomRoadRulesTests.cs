#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public class KingdomRoadRulesTests
	{
		// A grid written the way it looks: '#' is something feet turn aside from, anything else
		// is ground. Every routing test below reads one of these, so a case can be read without
		// counting coordinates.
		private static KingdomRoadRules.CellFilter Grid(string[] Rows)
		{
			return delegate(int X, int Y)
			{
				if (Y < 0 || Y >= Rows.Length)
				{
					return false;
				}
				if (X < 0 || X >= Rows[Y].Length)
				{
					return false;
				}
				return Rows[Y][X] != '#';
			};
		}

		// --- The ladder ------------------------------------------------------------------

		[TestCase(-100, KingdomRoadRules.WearState.Untouched)]
		[TestCase(0, KingdomRoadRules.WearState.Untouched)]
		[TestCase(39, KingdomRoadRules.WearState.Untouched)]
		[TestCase(40, KingdomRoadRules.WearState.Worn)]
		[TestCase(119, KingdomRoadRules.WearState.Worn)]
		[TestCase(120, KingdomRoadRules.WearState.Trodden)]
		[TestCase(299, KingdomRoadRules.WearState.Trodden)]
		[TestCase(300, KingdomRoadRules.WearState.Path)]
		[TestCase(4000, KingdomRoadRules.WearState.Path)]
		public void WearAtReadsTheLadder(int traffic, KingdomRoadRules.WearState expected)
		{
			Assert.AreEqual(expected, KingdomRoadRules.WearAt(traffic));
		}

		[TestCase(KingdomRoadRules.WearState.Untouched, 0)]
		[TestCase(KingdomRoadRules.WearState.Worn, KingdomRoadRules.WornTraffic)]
		[TestCase(KingdomRoadRules.WearState.Trodden, KingdomRoadRules.TroddenTraffic)]
		[TestCase(KingdomRoadRules.WearState.Path, KingdomRoadRules.PathTraffic)]
		[TestCase(KingdomRoadRules.WearState.Paved, int.MaxValue)]
		public void ThresholdForNamesEachRung(KingdomRoadRules.WearState state, int expected)
		{
			Assert.AreEqual(expected, KingdomRoadRules.ThresholdFor(state));
		}

		[TestCase(KingdomRoadRules.WearState.Worn)]
		[TestCase(KingdomRoadRules.WearState.Trodden)]
		[TestCase(KingdomRoadRules.WearState.Path)]
		public void ThresholdIsExactlyTheRungItBuys(KingdomRoadRules.WearState state)
		{
			int threshold = KingdomRoadRules.ThresholdFor(state);
			Assert.AreEqual(state, KingdomRoadRules.WearAt(threshold));
			Assert.Less((int)KingdomRoadRules.WearAt(threshold - 1), (int)state);
		}

		[Test]
		public void TheLadderClimbsAndTheCeilingIsAboveIt()
		{
			Assert.Less(KingdomRoadRules.WornTraffic, KingdomRoadRules.TroddenTraffic);
			Assert.Less(KingdomRoadRules.TroddenTraffic, KingdomRoadRules.PathTraffic);
			Assert.LessOrEqual(KingdomRoadRules.PathTraffic, KingdomRoadRules.MaxTraffic);
		}

		[Test]
		public void WalkingNeverReachesPaving()
		{
			Assert.AreNotEqual(KingdomRoadRules.WearState.Paved, KingdomRoadRules.WearAt(int.MaxValue));
		}

		[TestCase(KingdomRoadRules.WearState.Untouched, "untouched ground")]
		[TestCase(KingdomRoadRules.WearState.Worn, "worn grass")]
		[TestCase(KingdomRoadRules.WearState.Trodden, "trodden earth")]
		[TestCase(KingdomRoadRules.WearState.Path, "a path")]
		[TestCase(KingdomRoadRules.WearState.Paved, "paving")]
		public void WearNameSaysEachRungOutLoud(KingdomRoadRules.WearState state, string expected)
		{
			Assert.AreEqual(expected, KingdomRoadRules.WearName(state));
		}

		// --- Traffic ---------------------------------------------------------------------

		[TestCase(KingdomRoadRules.RouteKind.HomeToWork, 100)]
		[TestCase(KingdomRoadRules.RouteKind.DoorToLane, 100)]
		[TestCase(KingdomRoadRules.RouteKind.WorkToHeart, 70)]
		[TestCase(KingdomRoadRules.RouteKind.HeartToGate, 50)]
		[TestCase((KingdomRoadRules.RouteKind)99, 0)]
		public void RouteWeightIsPerErrand(KingdomRoadRules.RouteKind kind, int expected)
		{
			Assert.AreEqual(expected, KingdomRoadRules.RouteWeightPercent(kind));
		}

		[TestCase(KingdomRoadRules.RouteKind.HomeToWork, 0, 0)]
		[TestCase(KingdomRoadRules.RouteKind.HomeToWork, -3, 0)]
		[TestCase(KingdomRoadRules.RouteKind.HomeToWork, 1, 1)]
		[TestCase(KingdomRoadRules.RouteKind.HomeToWork, 5, 2)]
		[TestCase(KingdomRoadRules.RouteKind.DoorToLane, 5, 2)]
		[TestCase(KingdomRoadRules.RouteKind.WorkToHeart, 5, 1)]
		[TestCase(KingdomRoadRules.RouteKind.HeartToGate, 2, 2)]
		[TestCase(KingdomRoadRules.RouteKind.HeartToGate, 40, KingdomRoadRules.MaxWalkersPerRoute)]
		[TestCase((KingdomRoadRules.RouteKind)99, 40, 0)]
		public void WalkersNeverExceedThePlaceOrTheCap(KingdomRoadRules.RouteKind kind, int population, int expected)
		{
			Assert.AreEqual(expected, KingdomRoadRules.WalkersFor(kind, population));
		}

		[TestCase(0, 3, KingdomRoadRules.RouteKind.HomeToWork, 0)]
		[TestCase(2, 0, KingdomRoadRules.RouteKind.HomeToWork, 0)]
		[TestCase(2, -4, KingdomRoadRules.RouteKind.HomeToWork, 0)]
		[TestCase(1, 1, KingdomRoadRules.RouteKind.HomeToWork, 6)]
		[TestCase(2, 3, KingdomRoadRules.RouteKind.HomeToWork, 36)]
		[TestCase(2, 3, KingdomRoadRules.RouteKind.WorkToHeart, 25)]
		[TestCase(2, 3, KingdomRoadRules.RouteKind.HeartToGate, 18)]
		[TestCase(99, 1, KingdomRoadRules.RouteKind.HomeToWork, 24)]
		public void TrafficIsWalkersTimesDaysTimesWeight(int walkers, int days, KingdomRoadRules.RouteKind kind, int expected)
		{
			Assert.AreEqual(expected, KingdomRoadRules.TrafficFor(walkers, days, kind));
		}

		[Test]
		public void AbsenceIsCappedNotBanked()
		{
			// Days come from KingdomRules.HeartbeatDays, so a season away is worth the same as
			// the cap. If that ever stops being true here, wear stops being witnessed-only.
			int capped = KingdomRoadRules.TrafficFor(2, KingdomRules.MaxUpkeepDaysCharged, KingdomRoadRules.RouteKind.HomeToWork);
			Assert.AreEqual(capped, KingdomRoadRules.TrafficFor(2, KingdomRules.HeartbeatDays(KingdomRules.TicksPerDay * 400L), KingdomRoadRules.RouteKind.HomeToWork));
		}

		[Test]
		public void PacingIsWhatTheShippedNumbersSay()
		{
			// One household's daily walk at the absence cap. The pacing that follows is the
			// whole feel of the feature, so it is asserted rather than left to be discovered.
			int perPass = KingdomRoadRules.TrafficFor(2, 3, KingdomRoadRules.RouteKind.HomeToWork);
			Assert.AreEqual(36, perPass);
			Assert.AreEqual(KingdomRoadRules.WearState.Untouched, KingdomRoadRules.WearAt(perPass));
			Assert.AreEqual(KingdomRoadRules.WearState.Worn, KingdomRoadRules.WearAt(perPass * 2));
			Assert.AreEqual(KingdomRoadRules.WearState.Trodden, KingdomRoadRules.WearAt(perPass * 4));
			Assert.AreEqual(KingdomRoadRules.WearState.Trodden, KingdomRoadRules.WearAt(perPass * 8));
			Assert.AreEqual(KingdomRoadRules.WearState.Path, KingdomRoadRules.WearAt(perPass * 9));
		}

		// --- Rotation --------------------------------------------------------------------

		[TestCase(0L, 0, 0)]
		[TestCase(0L, -4, 0)]
		[TestCase(-500L, 5, 0)]
		[TestCase(0L, 5, 0)]
		[TestCase(KingdomRules.TicksPerDay, 5, 1)]
		[TestCase(KingdomRules.TicksPerDay * 5L, 5, 0)]
		[TestCase(KingdomRules.TicksPerDay * 7L, 5, 2)]
		[TestCase(KingdomRules.TicksPerDay - 1L, 5, 0)]
		public void RotationTurnsOnTheDayNotOnADraw(long ticks, int count, int expected)
		{
			Assert.AreEqual(expected, KingdomRoadRules.RotationStart(ticks, count));
		}

		[Test]
		public void RotationWalksEveryErrandEventually()
		{
			bool[] seen = new bool[11];
			for (int day = 0; day < 11; day++)
			{
				seen[KingdomRoadRules.RotationStart(KingdomRules.TicksPerDay * day, 11)] = true;
			}
			for (int i = 0; i < seen.Length; i++)
			{
				Assert.IsTrue(seen[i], "errand " + i + " was never reached by the rotation");
			}
		}

		// --- Packing ---------------------------------------------------------------------

		[TestCase(0, 0, 5, 0)]
		[TestCase(4, 0, 5, 4)]
		[TestCase(0, 1, 5, 5)]
		[TestCase(3, 2, 5, 13)]
		public void PackAndUnpackAgree(int x, int y, int width, int packed)
		{
			Assert.AreEqual(packed, KingdomRoadRules.Pack(x, y, width));
			Assert.AreEqual(x, KingdomRoadRules.UnpackX(packed, width));
			Assert.AreEqual(y, KingdomRoadRules.UnpackY(packed, width));
		}

		[TestCase(0, 0, 5, 5, true)]
		[TestCase(4, 4, 5, 5, true)]
		[TestCase(-1, 0, 5, 5, false)]
		[TestCase(0, -1, 5, 5, false)]
		[TestCase(5, 0, 5, 5, false)]
		[TestCase(0, 5, 5, 5, false)]
		public void InBoundsIsTheZone(int x, int y, int width, int height, bool expected)
		{
			Assert.AreEqual(expected, KingdomRoadRules.InBounds(x, y, width, height));
		}

		// --- Routing ---------------------------------------------------------------------

		[Test]
		public void AStraightWalkIsTheGroundBetweenAndNotTheEnds()
		{
			List<int> route = new List<int>();
			Assert.IsTrue(KingdomRoadRules.TryTrace(Grid(new string[1] { "....." }), 5, 1, 0, 0, 4, 0, 48, 400, route));
			CollectionAssert.AreEqual(new int[3] { 1, 2, 3 }, route);
		}

		[Test]
		public void AdjacentEndsHaveNoGroundBetweenThem()
		{
			List<int> route = new List<int>();
			Assert.IsTrue(KingdomRoadRules.TryTrace(Grid(new string[1] { "....." }), 5, 1, 0, 0, 1, 0, 48, 400, route));
			Assert.AreEqual(0, route.Count);
		}

		[Test]
		public void NobodyWalksToWhereTheyAlreadyAre()
		{
			List<int> route = new List<int>();
			Assert.IsFalse(KingdomRoadRules.TryTrace(Grid(new string[1] { "....." }), 5, 1, 2, 0, 2, 0, 48, 400, route));
			Assert.AreEqual(0, route.Count);
		}

		[Test]
		public void FeetGoRoundWhatTheyCannotGoThrough()
		{
			string[] rows = new string[3] { "..#..", "..#..", "....." };
			KingdomRoadRules.CellFilter grid = Grid(rows);
			List<int> route = new List<int>();
			Assert.IsTrue(KingdomRoadRules.TryTrace(grid, 5, 3, 0, 0, 4, 0, 48, 400, route));
			Assert.Greater(route.Count, 0);
			int previousX = 0;
			int previousY = 0;
			for (int i = 0; i < route.Count; i++)
			{
				int x = KingdomRoadRules.UnpackX(route[i], 5);
				int y = KingdomRoadRules.UnpackY(route[i], 5);
				Assert.IsTrue(grid(x, y), "the walk passed through a cell nobody can walk through");
				Assert.AreEqual(1, KingdomLayoutRules.Chebyshev(x, y, previousX, previousY), "the walk skipped a cell");
				previousX = x;
				previousY = y;
			}
			Assert.AreEqual(1, KingdomLayoutRules.Chebyshev(previousX, previousY, 4, 0), "the walk did not arrive");
		}

		[Test]
		public void WalledOffIsNoWalkAtAll()
		{
			List<int> route = new List<int>();
			Assert.IsFalse(KingdomRoadRules.TryTrace(Grid(new string[3] { "..#..", "..#..", "..#.." }), 5, 3, 0, 1, 4, 1, 48, 400, route));
			Assert.AreEqual(0, route.Count);
		}

		[Test]
		public void ADestinationIsEnterableEvenWhenItIsSolid()
		{
			// A home and a work are solid objects. If the far end had to be walkable, no errand
			// in a real settlement would ever have a route at all.
			List<int> route = new List<int>();
			Assert.IsTrue(KingdomRoadRules.TryTrace(Grid(new string[1] { "....#" }), 5, 1, 0, 0, 4, 0, 48, 400, route));
			CollectionAssert.AreEqual(new int[3] { 1, 2, 3 }, route);
		}

		[Test]
		public void ARouteTooLongToBeAnErrandIsRefusedWhole()
		{
			List<int> route = new List<int>();
			Assert.IsFalse(KingdomRoadRules.TryTrace(Grid(new string[1] { ".........." }), 10, 1, 0, 0, 9, 0, 2, 400, route));
			Assert.AreEqual(0, route.Count);
		}

		[Test]
		public void TheSearchGivesUpRatherThanFloodingTheZone()
		{
			List<int> route = new List<int>();
			Assert.IsFalse(KingdomRoadRules.TryTrace(Grid(new string[1] { ".........." }), 10, 1, 0, 0, 9, 0, 48, 1, route));
		}

		[Test]
		public void TheSameSettlementWearsTheSameGroundEveryTime()
		{
			string[] rows = new string[4] { "..........", "..###..#..", "....#.....", ".........." };
			KingdomRoadRules.CellFilter grid = Grid(rows);
			List<int> first = new List<int>();
			List<int> second = new List<int>();
			Assert.IsTrue(KingdomRoadRules.TryTrace(grid, 10, 4, 0, 0, 9, 3, 48, 400, first));
			Assert.IsTrue(KingdomRoadRules.TryTrace(grid, 10, 4, 0, 0, 9, 3, 48, 400, second));
			CollectionAssert.AreEqual(first, second);
		}

		[TestCase(0, 0, 0, 5)]
		[TestCase(5, 5, -1, 5)]
		[TestCase(5, 5, 5, 5)]
		public void ARouteOffTheGridIsNoRoute(int fromX, int fromY, int toX, int toY)
		{
			List<int> route = new List<int>();
			Assert.IsFalse(KingdomRoadRules.TryTrace(Grid(new string[1] { "....." }), 5, 1, fromX, fromY, toX, toY, 48, 400, route));
		}

		[Test]
		public void NoGridAndNoListAreBothRefusals()
		{
			List<int> route = new List<int>();
			Assert.IsFalse(KingdomRoadRules.TryTrace(null, 5, 1, 0, 0, 4, 0, 48, 400, route));
			Assert.IsFalse(KingdomRoadRules.TryTrace(Grid(new string[1] { "....." }), 5, 1, 0, 0, 4, 0, 48, 400, null));
			Assert.IsFalse(KingdomRoadRules.TryTrace(Grid(new string[1] { "....." }), 0, 1, 0, 0, 4, 0, 48, 400, route));
		}

		// --- The way out -----------------------------------------------------------------

		[Test]
		public void GroundWithNoFrontierHasNoWayOut()
		{
			Assert.IsFalse(KingdomRoadRules.TryGate(10, 10, KingdomRules.Frontier.None, 5, 5, out _, out _));
		}

		[Test]
		public void TheGateIsTheEdgeCellNearestTheHeart()
		{
			Assert.IsTrue(KingdomRoadRules.TryGate(10, 10, KingdomRules.Frontier.North, 5, 5, out var x, out var y));
			Assert.AreEqual(5, x);
			Assert.AreEqual(1, y);
			Assert.IsTrue(KingdomRules.IsOnFrontier(x, y, 10, 10, KingdomRules.Frontier.North));
		}

		[Test]
		public void TheGateIsTheSameGateEveryTime()
		{
			Assert.IsTrue(KingdomRoadRules.TryGate(20, 20, KingdomRules.Frontier.East | KingdomRules.Frontier.West, 10, 10, out var firstX, out var firstY));
			Assert.IsTrue(KingdomRoadRules.TryGate(20, 20, KingdomRules.Frontier.East | KingdomRules.Frontier.West, 10, 10, out var secondX, out var secondY));
			Assert.AreEqual(firstX, secondX);
			Assert.AreEqual(firstY, secondY);
		}

		[Test]
		public void AZoneWithNoSizeHasNoGate()
		{
			Assert.IsFalse(KingdomRoadRules.TryGate(0, 10, KingdomRules.Frontier.North, 0, 0, out _, out _));
			Assert.IsFalse(KingdomRoadRules.TryGate(10, 0, KingdomRules.Frontier.North, 0, 0, out _, out _));
		}

		// --- The lane --------------------------------------------------------------------

		[TestCase(11, 10, 11, 8)]
		[TestCase(11, 13, 11, 15)]
		[TestCase(10, 11, 8, 11)]
		[TestCase(14, 11, 16, 11)]
		public void ADoorOpensOntoTheLaneTheGrammarReserved(int doorX, int doorY, int laneX, int laneY)
		{
			KingdomPlotRules.PlotRect rect = new KingdomPlotRules.PlotRect(10, 10, 14, 13);
			Assert.IsTrue(KingdomRoadRules.TryLane(rect, doorX, doorY, out var x, out var y));
			Assert.AreEqual(laneX, x);
			Assert.AreEqual(laneY, y);
			Assert.IsFalse(KingdomPlotRules.Reserved(rect).Contains(x, y), "the lane cell is inside the plot's own reserved rect");
		}

		[TestCase(10, 10)]
		[TestCase(14, 13)]
		[TestCase(12, 11)]
		[TestCase(0, 0)]
		public void ACornerOrAnInsideCellSaysNothingAboutWhichWayADoorFaces(int doorX, int doorY)
		{
			KingdomPlotRules.PlotRect rect = new KingdomPlotRules.PlotRect(10, 10, 14, 13);
			Assert.IsFalse(KingdomRoadRules.TryLane(rect, doorX, doorY, out _, out _));
		}

		// --- The tally -------------------------------------------------------------------

		[Test]
		public void AFirstWalkAdmitsACellAndLaterOnesAddToIt()
		{
			List<KingdomRoadRules.WornCell> tally = new List<KingdomRoadRules.WornCell>();
			Assert.IsTrue(KingdomRoadRules.Accrue(tally, 3, 4, 30, out var first));
			Assert.AreEqual(30, first);
			Assert.AreEqual(1, tally.Count);
			Assert.IsTrue(KingdomRoadRules.Accrue(tally, 3, 4, 30, out var second));
			Assert.AreEqual(60, second);
			Assert.AreEqual(1, tally.Count);
			Assert.AreEqual(60, KingdomRoadRules.TrafficAt(tally, 3, 4));
			Assert.AreEqual(0, KingdomRoadRules.TrafficAt(tally, 9, 9));
		}

		[Test]
		public void ATallyNeverClimbsPastItsCeiling()
		{
			List<KingdomRoadRules.WornCell> tally = new List<KingdomRoadRules.WornCell>();
			Assert.IsTrue(KingdomRoadRules.Accrue(tally, 0, 0, KingdomRoadRules.MaxTraffic + 5000, out var total));
			Assert.AreEqual(KingdomRoadRules.MaxTraffic, total);
			Assert.IsTrue(KingdomRoadRules.Accrue(tally, 0, 0, 500, out total));
			Assert.AreEqual(KingdomRoadRules.MaxTraffic, total);
		}

		[Test]
		public void WalkingNowhereAdmitsNothing()
		{
			List<KingdomRoadRules.WornCell> tally = new List<KingdomRoadRules.WornCell>();
			Assert.IsTrue(KingdomRoadRules.Accrue(tally, 1, 1, 0, out var total));
			Assert.AreEqual(0, total);
			Assert.AreEqual(0, tally.Count);
		}

		[Test]
		public void AFullTallyRefusesNewGroundButKeepsFeedingTheGroundItHas()
		{
			List<KingdomRoadRules.WornCell> tally = new List<KingdomRoadRules.WornCell>();
			for (int i = 0; i < KingdomRoadRules.MaxTrackedCells; i++)
			{
				tally.Add(new KingdomRoadRules.WornCell(i % 80, i / 80, 10));
			}
			Assert.IsFalse(KingdomRoadRules.Accrue(tally, 79, 79, 10, out var refused));
			Assert.AreEqual(0, refused);
			Assert.AreEqual(KingdomRoadRules.MaxTrackedCells, tally.Count);
			Assert.IsTrue(KingdomRoadRules.Accrue(tally, 0, 0, 10, out var fed));
			Assert.AreEqual(20, fed);
		}

		[Test]
		public void APathLeavesTheTallyBecauseThePathIsTheRecord()
		{
			List<KingdomRoadRules.WornCell> tally = new List<KingdomRoadRules.WornCell>();
			KingdomRoadRules.Accrue(tally, 2, 2, 300, out _);
			Assert.IsTrue(KingdomRoadRules.Retire(tally, 2, 2));
			Assert.AreEqual(0, tally.Count);
			Assert.IsFalse(KingdomRoadRules.Retire(tally, 2, 2));
		}

		[Test]
		public void ANullTallyIsARefusalAndNotACrash()
		{
			Assert.IsFalse(KingdomRoadRules.Accrue(null, 1, 1, 10, out var total));
			Assert.AreEqual(0, total);
			Assert.AreEqual(0, KingdomRoadRules.TrafficAt(null, 1, 1));
			Assert.AreEqual(-1, KingdomRoadRules.IndexOf(null, 1, 1));
			Assert.IsFalse(KingdomRoadRules.Retire(null, 1, 1));
		}

		// --- Writing it down -------------------------------------------------------------

		[Test]
		public void TheTallyRoundTrips()
		{
			List<KingdomRoadRules.WornCell> tally = new List<KingdomRoadRules.WornCell>
			{
				new KingdomRoadRules.WornCell(1, 2, 50),
				new KingdomRoadRules.WornCell(3, 4, 299)
			};
			string written = KingdomRoadRules.Encode(tally);
			Assert.AreEqual("1,2,50;3,4,299", written);
			Assert.IsTrue(KingdomRoadRules.TryDecode(written, out var read, out var error));
			Assert.IsNull(error);
			Assert.AreEqual(2, read.Count);
			Assert.AreEqual(50, KingdomRoadRules.TrafficAt(read, 1, 2));
			Assert.AreEqual(299, KingdomRoadRules.TrafficAt(read, 3, 4));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		public void GroundNobodyHasWalkedWritesNothingAndReadsCleanly(string raw)
		{
			Assert.IsTrue(KingdomRoadRules.TryDecode(raw, out var cells, out var error));
			Assert.IsNull(error);
			Assert.AreEqual(0, cells.Count);
		}

		[Test]
		public void NothingWorthNothingIsWrittenDown()
		{
			List<KingdomRoadRules.WornCell> tally = new List<KingdomRoadRules.WornCell>
			{
				new KingdomRoadRules.WornCell(1, 1, 0),
				new KingdomRoadRules.WornCell(-4, 1, 30),
				new KingdomRoadRules.WornCell(1, KingdomRoadRules.MaxCoordinate + 1, 30),
				new KingdomRoadRules.WornCell(2, 2, 30)
			};
			Assert.AreEqual("2,2,30", KingdomRoadRules.Encode(tally));
			Assert.AreEqual("", KingdomRoadRules.Encode(null));
			Assert.AreEqual("", KingdomRoadRules.Encode(new List<KingdomRoadRules.WornCell>()));
		}

		[Test]
		public void AWrittenTallyIsClampedOnTheWayOut()
		{
			List<KingdomRoadRules.WornCell> tally = new List<KingdomRoadRules.WornCell>
			{
				new KingdomRoadRules.WornCell(1, 1, KingdomRoadRules.MaxTraffic + 900)
			};
			Assert.AreEqual("1,1," + KingdomRoadRules.MaxTraffic, KingdomRoadRules.Encode(tally));
		}

		[TestCase("nonsense")]
		[TestCase("1,2")]
		[TestCase("1,2,3,4")]
		[TestCase("a,2,3")]
		[TestCase("1,b,3")]
		[TestCase("1,2,c")]
		[TestCase("-1,2,3")]
		[TestCase("1,-2,3")]
		[TestCase("1,2,0")]
		[TestCase("1,2,-5")]
		[TestCase("1000,2,3")]
		[TestCase("1,1000,3")]
		public void AMalformedOrImpossibleCellIsDroppedAndSaidSo(string raw)
		{
			Assert.IsFalse(KingdomRoadRules.TryDecode(raw, out var cells, out var error));
			Assert.AreEqual(0, cells.Count);
			Assert.IsNotNull(error);
		}

		[Test]
		public void OneBadCellDoesNotCostTheRestOfTheGround()
		{
			Assert.IsFalse(KingdomRoadRules.TryDecode("1,2,50;garbage;3,4,60", out var cells, out var error));
			Assert.IsNotNull(error);
			Assert.AreEqual(2, cells.Count);
			Assert.AreEqual(50, KingdomRoadRules.TrafficAt(cells, 1, 2));
			Assert.AreEqual(60, KingdomRoadRules.TrafficAt(cells, 3, 4));
		}

		[Test]
		public void ARepeatedCellKeepsTheHeavierReading()
		{
			Assert.IsFalse(KingdomRoadRules.TryDecode("1,2,10;1,2,40", out var cells, out var error));
			Assert.IsNotNull(error);
			Assert.AreEqual(1, cells.Count);
			Assert.AreEqual(40, KingdomRoadRules.TrafficAt(cells, 1, 2));
			Assert.IsFalse(KingdomRoadRules.TryDecode("1,2,40;1,2,10", out cells, out error));
			Assert.AreEqual(40, KingdomRoadRules.TrafficAt(cells, 1, 2));
		}

		[Test]
		public void ATallyReadInFromOutsideIsClampedNotBelieved()
		{
			Assert.IsTrue(KingdomRoadRules.TryDecode("5,5," + (KingdomRoadRules.MaxTraffic + 10000), out var cells, out var error));
			Assert.IsNull(error);
			Assert.AreEqual(KingdomRoadRules.MaxTraffic, KingdomRoadRules.TrafficAt(cells, 5, 5));
		}

		[Test]
		public void AHostileTallyCannotGrowPastWhatTheKeepersCount()
		{
			System.Text.StringBuilder raw = new System.Text.StringBuilder();
			for (int i = 0; i < KingdomRoadRules.MaxTrackedCells + 40; i++)
			{
				if (i > 0)
				{
					raw.Append(KingdomRoadRules.CellSeparator);
				}
				raw.Append(i % 100).Append(KingdomRoadRules.FieldSeparator).Append(i / 100).Append(KingdomRoadRules.FieldSeparator).Append(10);
			}
			Assert.IsFalse(KingdomRoadRules.TryDecode(raw.ToString(), out var cells, out var error));
			Assert.IsNotNull(error);
			Assert.AreEqual(KingdomRoadRules.MaxTrackedCells, cells.Count);
		}

		// --- Paving ----------------------------------------------------------------------

		[TestCase("Marble", "MarbleFloor")]
		[TestCase("Limestone", "SaltPath")]
		[TestCase("BrinestalkWall", "WoodFloor")]
		[TestCase("Verdigris", "GreenTile")]
		[TestCase("Fulcrete", "FoamcreteFloor")]
		[TestCase("Foamcrete", "FoamcreteFloor")]
		[TestCase("SomeoneElsesWall", "DirtPath")]
		[TestCase(null, "DirtPath")]
		public void PavingIsLaidInTheWallTheSettlementBuildsIn(string wall, string expected)
		{
			Assert.AreEqual(expected, KingdomRoadRules.PavedFloorFor(wall));
		}

		[Test]
		public void EveryWallTheModBuildsInHasPavingAndAPriceForIt()
		{
			// The wall list and the paving list are two files apart. If a seventh wall material
			// is added and nobody teaches paving about it, this is the test that says so.
			for (int i = 0; i < KingdomPlotRules.WallMaterials.Length; i++)
			{
				string wall = KingdomPlotRules.WallMaterials[i];
				Assert.AreNotEqual("DirtPath", KingdomRoadRules.PavedFloorFor(wall), "no paving is named for " + wall);
				Assert.IsTrue(KingdomRoadRules.CanPaveIn(KingdomRoadRules.PaveMaterialFor(wall)), "nothing can be spent to pave in " + wall);
			}
		}

		[TestCase("Marble", KingdomMaterial.Marble)]
		[TestCase("Limestone", KingdomMaterial.Stone)]
		[TestCase("Fulcrete", KingdomMaterial.Stone)]
		[TestCase("Foamcrete", KingdomMaterial.Stone)]
		[TestCase("Verdigris", KingdomMaterial.Scrap)]
		[TestCase("BrinestalkWall", KingdomMaterial.Timber)]
		[TestCase("SomeoneElsesWall", KingdomMaterial.Mud)]
		[TestCase(null, KingdomMaterial.Mud)]
		public void PavingIsPaidForInWhatTheWallsAreMadeOf(string wall, KingdomMaterial expected)
		{
			Assert.AreEqual(expected, KingdomRoadRules.PaveMaterialFor(wall));
		}

		[TestCase(KingdomMaterial.Mud, false)]
		[TestCase(KingdomMaterial.Brush, false)]
		[TestCase(KingdomMaterial.Timber, true)]
		[TestCase(KingdomMaterial.Stone, true)]
		[TestCase(KingdomMaterial.Marble, true)]
		[TestCase(KingdomMaterial.Scrap, true)]
		public void YouCannotPaveTheGroundWithTheGround(KingdomMaterial material, bool expected)
		{
			Assert.AreEqual(expected, KingdomRoadRules.CanPaveIn(material));
		}

		[TestCase(-3, 0)]
		[TestCase(0, 0)]
		[TestCase(1, KingdomRoadRules.PaveUnitsPerCell)]
		[TestCase(12, 12 * KingdomRoadRules.PaveUnitsPerCell)]
		public void PavingCostsPerCell(int cells, int expected)
		{
			Assert.AreEqual(expected, KingdomRoadRules.PaveCost(cells));
		}

		[TestCase(-1, 0)]
		[TestCase(0, 0)]
		[TestCase(7, 7)]
		[TestCase(KingdomRoadRules.MaxPaveCellsPerOrder + 30, KingdomRoadRules.MaxPaveCellsPerOrder)]
		public void OneOrderCoversOnlyWhatOneOrderCovers(int available, int expected)
		{
			Assert.AreEqual(expected, KingdomRoadRules.PaveCells(available));
		}

		// --- Prose -----------------------------------------------------------------------

		[TestCase(KingdomRoadRules.WearState.Untouched)]
		[TestCase(KingdomRoadRules.WearState.Worn)]
		[TestCase(KingdomRoadRules.WearState.Paved)]
		public void TheRungsThatSayNothingSayNothing(KingdomRoadRules.WearState state)
		{
			Assert.IsNull(KingdomRoadRules.WearLine(state, "Ezra"));
		}

		[TestCase(KingdomRoadRules.WearState.Trodden)]
		[TestCase(KingdomRoadRules.WearState.Path)]
		public void TheRungsWorthRemarkingOnNameTheSettlement(KingdomRoadRules.WearState state)
		{
			string line = KingdomRoadRules.WearLine(state, "Ezra");
			Assert.IsNotNull(line);
			StringAssert.Contains("Ezra", line);
			Assert.IsNotNull(KingdomRoadRules.WearLine(state, null));
		}

		[Test]
		public void APavingIsReportedAndChronicledWithItsCountAndItsMaterial()
		{
			string line = KingdomRoadRules.PavedLine(7, KingdomMaterial.Stone, "Ezra");
			StringAssert.Contains("7", line);
			StringAssert.Contains("Ezra", line);
			StringAssert.Contains(KingdomMaterialRules.MaterialName(KingdomMaterial.Stone), line);
			string record = KingdomRoadRules.PavedRecord(7, KingdomMaterial.Stone, "Nephilim");
			StringAssert.Contains("7", record);
			StringAssert.Contains("Nephilim", record);
			StringAssert.Contains(KingdomMaterialRules.MaterialName(KingdomMaterial.Stone), record);
		}

		[TestCase(1, "cell")]
		[TestCase(2, "cells")]
		public void OneCellIsACellAndTwoAreCells(int cells, string expected)
		{
			StringAssert.Contains(cells + " " + expected, KingdomRoadRules.PavedLine(cells, KingdomMaterial.Stone, "Ezra"));
			StringAssert.Contains(cells + " " + expected, KingdomRoadRules.PavedRecord(cells, KingdomMaterial.Stone, "Ezra"));
		}

		[Test]
		public void EveryRefusalSaysWhatWouldLiftIt()
		{
			StringAssert.Contains("Ezra", KingdomRoadRules.RefuseNothingWorn("Ezra"));
			StringAssert.Contains(KingdomMaterialRules.MaterialName(KingdomMaterial.Mud), KingdomRoadRules.RefuseMaterialKind(KingdomMaterial.Mud));
			string shortfall = KingdomRoadRules.RefuseMaterial(KingdomMaterial.Stone, 12, 3);
			StringAssert.Contains("12", shortfall);
			StringAssert.Contains("3", shortfall);
			StringAssert.Contains(KingdomMaterialRules.MaterialName(KingdomMaterial.Stone), shortfall);
			StringAssert.Contains("Ezra", KingdomRoadRules.RefuseHands("Ezra"));
			StringAssert.Contains("Ezra", KingdomRoadRules.RefuseTallyFull("Ezra"));
			Assert.IsNotEmpty(KingdomRoadRules.RefuseNotOurGround());
		}

		[Test]
		public void ARefusalNeverLeavesAHoleWhereTheNameGoes()
		{
			Assert.IsNotEmpty(KingdomRoadRules.RefuseNothingWorn(null));
			Assert.IsNotEmpty(KingdomRoadRules.RefuseHands(null));
			Assert.IsNotEmpty(KingdomRoadRules.RefuseTallyFull(null));
			Assert.IsNotEmpty(KingdomRoadRules.PavedLine(1, KingdomMaterial.Stone, null));
			Assert.IsNotEmpty(KingdomRoadRules.PavedRecord(1, KingdomMaterial.Stone, null));
		}
	}
}
#endif
