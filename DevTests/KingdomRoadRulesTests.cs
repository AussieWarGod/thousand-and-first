#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;
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

		[Test]
		public void NestedRoadDeclarationsKeepTheirPersistedAbi()
		{
			ClassicAssert.AreEqual(typeof(int), System.Enum.GetUnderlyingType(typeof(KingdomRoadRules.WearState)));
			CollectionAssert.AreEqual(new int[5] { 0, 1, 2, 3, 4 }, new int[5]
			{
				(int)KingdomRoadRules.WearState.Untouched,
				(int)KingdomRoadRules.WearState.Worn,
				(int)KingdomRoadRules.WearState.Trodden,
				(int)KingdomRoadRules.WearState.Path,
				(int)KingdomRoadRules.WearState.Paved
			});
			ClassicAssert.AreEqual(typeof(int), System.Enum.GetUnderlyingType(typeof(KingdomRoadRules.RouteKind)));
			CollectionAssert.AreEqual(new int[4] { 0, 1, 2, 3 }, new int[4]
			{
				(int)KingdomRoadRules.RouteKind.HomeToWork,
				(int)KingdomRoadRules.RouteKind.WorkToHeart,
				(int)KingdomRoadRules.RouteKind.HeartToGate,
				(int)KingdomRoadRules.RouteKind.DoorToLane
			});

			System.Reflection.FieldInfo[] fields = typeof(KingdomRoadRules.WornCell).GetFields();
			ClassicAssert.AreEqual(3, fields.Length);
			ClassicAssert.AreEqual("X", fields[0].Name);
			ClassicAssert.AreEqual("Y", fields[1].Name);
			ClassicAssert.AreEqual("Traffic", fields[2].Name);
			for (int i = 0; i < fields.Length; i++)
			{
				ClassicAssert.AreEqual(typeof(int), fields[i].FieldType);
			}

			KingdomRoadRules.WornCell empty = default(KingdomRoadRules.WornCell);
			ClassicAssert.AreEqual(0, empty.X);
			ClassicAssert.AreEqual(0, empty.Y);
			ClassicAssert.AreEqual(0, empty.Traffic);
			System.Reflection.MethodInfo invoke = typeof(KingdomRoadRules.CellFilter).GetMethod("Invoke");
			ClassicAssert.AreEqual(typeof(bool), invoke.ReturnType);
			ClassicAssert.AreEqual(2, invoke.GetParameters().Length);
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
			ClassicAssert.AreEqual(expected, KingdomRoadRules.WearAt(traffic));
		}

		[TestCase(KingdomRoadRules.WearState.Untouched, 0)]
		[TestCase(KingdomRoadRules.WearState.Worn, KingdomRoadRules.WornTraffic)]
		[TestCase(KingdomRoadRules.WearState.Trodden, KingdomRoadRules.TroddenTraffic)]
		[TestCase(KingdomRoadRules.WearState.Path, KingdomRoadRules.PathTraffic)]
		[TestCase(KingdomRoadRules.WearState.Paved, int.MaxValue)]
		public void ThresholdForNamesEachRung(KingdomRoadRules.WearState state, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomRoadRules.ThresholdFor(state));
		}

		[TestCase(KingdomRoadRules.WearState.Worn)]
		[TestCase(KingdomRoadRules.WearState.Trodden)]
		[TestCase(KingdomRoadRules.WearState.Path)]
		public void ThresholdIsExactlyTheRungItBuys(KingdomRoadRules.WearState state)
		{
			int threshold = KingdomRoadRules.ThresholdFor(state);
			ClassicAssert.AreEqual(state, KingdomRoadRules.WearAt(threshold));
			ClassicAssert.Less((int)KingdomRoadRules.WearAt(threshold - 1), (int)state);
		}

		[Test]
		public void TheLadderClimbsAndTheCeilingIsAboveIt()
		{
			ClassicAssert.Less(KingdomRoadRules.WornTraffic, KingdomRoadRules.TroddenTraffic);
			ClassicAssert.Less(KingdomRoadRules.TroddenTraffic, KingdomRoadRules.PathTraffic);
			ClassicAssert.LessOrEqual(KingdomRoadRules.PathTraffic, KingdomRoadRules.MaxTraffic);
		}

		[Test]
		public void WalkingNeverReachesPaving()
		{
			ClassicAssert.AreNotEqual(KingdomRoadRules.WearState.Paved, KingdomRoadRules.WearAt(int.MaxValue));
		}

		[TestCase(KingdomRoadRules.WearState.Untouched, "untouched ground")]
		[TestCase(KingdomRoadRules.WearState.Worn, "worn grass")]
		[TestCase(KingdomRoadRules.WearState.Trodden, "trodden earth")]
		[TestCase(KingdomRoadRules.WearState.Path, "a path")]
		[TestCase(KingdomRoadRules.WearState.Paved, "paving")]
		public void WearNameSaysEachRungOutLoud(KingdomRoadRules.WearState state, string expected)
		{
			ClassicAssert.AreEqual(expected, KingdomRoadRules.WearName(state));
		}

		// --- Traffic ---------------------------------------------------------------------

		[TestCase(KingdomRoadRules.RouteKind.HomeToWork, 100)]
		[TestCase(KingdomRoadRules.RouteKind.DoorToLane, 100)]
		[TestCase(KingdomRoadRules.RouteKind.WorkToHeart, 70)]
		[TestCase(KingdomRoadRules.RouteKind.HeartToGate, 50)]
		[TestCase((KingdomRoadRules.RouteKind)99, 0)]
		public void RouteWeightIsPerErrand(KingdomRoadRules.RouteKind kind, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomRoadRules.RouteWeightPercent(kind));
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
			ClassicAssert.AreEqual(expected, KingdomRoadRules.WalkersFor(kind, population));
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
			ClassicAssert.AreEqual(expected, KingdomRoadRules.TrafficFor(walkers, days, kind));
		}

		[Test]
		public void TrafficRunsTheWholeAbsenceBecausePeopleWalkedItWhileNobodyLooked()
		{
			// The uncapping. A stamp four hundred days stale used to be handed three days of
			// walking; it is handed four hundred, because the errands were walked. Linear in the
			// days, which is the whole of the claim -- there is no ceiling in here, only the
			// walkers term and the tally's own saturation.
			int oneDay = KingdomRoadRules.TrafficFor(2, 1, KingdomRoadRules.RouteKind.HomeToWork);
			int tenDays = KingdomRoadRules.TrafficFor(2, 10, KingdomRoadRules.RouteKind.HomeToWork);
			ClassicAssert.AreEqual(oneDay * 10, tenDays, "ten days did not lay ten days of walking");
			ClassicAssert.Greater(KingdomRoadRules.TrafficFor(2, KingdomRules.ElapsedDays(KingdomRules.TicksPerDay * 400L), KingdomRoadRules.RouteKind.HomeToWork),
				tenDays, "a four-hundred-day stretch laid no more than ten days");
		}

		[Test]
		public void NobodyWalkingLaysNothingHoweverLongTheStretch()
		{
			// Clause 2, and the reason uncapping needed nothing else attached: traffic is
			// WALKERS times days, so an empty settlement over four hundred days lays exactly what
			// it lays over an afternoon. Idleness wears nothing.
			ClassicAssert.AreEqual(0, KingdomRoadRules.TrafficFor(0, 400, KingdomRoadRules.RouteKind.HomeToWork));
			ClassicAssert.AreEqual(0, KingdomRoadRules.TrafficFor(-3, 400, KingdomRoadRules.RouteKind.HomeToWork));
		}

		[Test]
		public void TrafficSaturatesRatherThanWrappingOnANonsenseStretch()
		{
			// An unplanted stamp reads as the age of the world (KingdomRules.ElapsedDays does not
			// special-case it and callers must). A wrapped negative would read as ground that had
			// been UNwalked, so the widened multiply saturates at the tally's own ceiling.
			int enormous = KingdomRoadRules.TrafficFor(KingdomRoadRules.MaxWalkersPerRoute, int.MaxValue, KingdomRoadRules.RouteKind.HomeToWork);
			ClassicAssert.AreEqual(KingdomRoadRules.MaxTraffic, enormous);
		}

		[Test]
		public void PacingIsWhatTheShippedNumbersSay()
		{
			// One household's daily walk at the absence cap. The pacing that follows is the
			// whole feel of the feature, so it is asserted rather than left to be discovered.
			int perPass = KingdomRoadRules.TrafficFor(2, 3, KingdomRoadRules.RouteKind.HomeToWork);
			ClassicAssert.AreEqual(36, perPass);
			ClassicAssert.AreEqual(KingdomRoadRules.WearState.Untouched, KingdomRoadRules.WearAt(perPass));
			ClassicAssert.AreEqual(KingdomRoadRules.WearState.Worn, KingdomRoadRules.WearAt(perPass * 2));
			ClassicAssert.AreEqual(KingdomRoadRules.WearState.Trodden, KingdomRoadRules.WearAt(perPass * 4));
			ClassicAssert.AreEqual(KingdomRoadRules.WearState.Trodden, KingdomRoadRules.WearAt(perPass * 8));
			ClassicAssert.AreEqual(KingdomRoadRules.WearState.Path, KingdomRoadRules.WearAt(perPass * 9));
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
			ClassicAssert.AreEqual(expected, KingdomRoadRules.RotationStart(ticks, count));
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
				ClassicAssert.IsTrue(seen[i], "errand " + i + " was never reached by the rotation");
			}
		}

		// --- Packing ---------------------------------------------------------------------

		[TestCase(0, 0, 5, 0)]
		[TestCase(4, 0, 5, 4)]
		[TestCase(0, 1, 5, 5)]
		[TestCase(3, 2, 5, 13)]
		public void PackAndUnpackAgree(int x, int y, int width, int packed)
		{
			ClassicAssert.AreEqual(packed, KingdomRoadRules.Pack(x, y, width));
			ClassicAssert.AreEqual(x, KingdomRoadRules.UnpackX(packed, width));
			ClassicAssert.AreEqual(y, KingdomRoadRules.UnpackY(packed, width));
		}

		[TestCase(0, 0, 5, 5, true)]
		[TestCase(4, 4, 5, 5, true)]
		[TestCase(-1, 0, 5, 5, false)]
		[TestCase(0, -1, 5, 5, false)]
		[TestCase(5, 0, 5, 5, false)]
		[TestCase(0, 5, 5, 5, false)]
		public void InBoundsIsTheZone(int x, int y, int width, int height, bool expected)
		{
			ClassicAssert.AreEqual(expected, KingdomRoadRules.InBounds(x, y, width, height));
		}

		// --- Routing ---------------------------------------------------------------------

		[Test]
		public void AStraightWalkIsTheGroundBetweenAndNotTheEnds()
		{
			List<int> route = new List<int>();
			ClassicAssert.IsTrue(KingdomRoadRules.TryTrace(Grid(new string[1] { "....." }), 5, 1, 0, 0, 4, 0, 48, 400, route));
			CollectionAssert.AreEqual(new int[3] { 1, 2, 3 }, route);
		}

		[Test]
		public void AdjacentEndsHaveNoGroundBetweenThem()
		{
			List<int> route = new List<int>();
			ClassicAssert.IsTrue(KingdomRoadRules.TryTrace(Grid(new string[1] { "....." }), 5, 1, 0, 0, 1, 0, 48, 400, route));
			ClassicAssert.AreEqual(0, route.Count);
		}

		[Test]
		public void NobodyWalksToWhereTheyAlreadyAre()
		{
			List<int> route = new List<int>();
			ClassicAssert.IsFalse(KingdomRoadRules.TryTrace(Grid(new string[1] { "....." }), 5, 1, 2, 0, 2, 0, 48, 400, route));
			ClassicAssert.AreEqual(0, route.Count);
		}

		[Test]
		public void FeetGoRoundWhatTheyCannotGoThrough()
		{
			string[] rows = new string[3] { "..#..", "..#..", "....." };
			KingdomRoadRules.CellFilter grid = Grid(rows);
			List<int> route = new List<int>();
			ClassicAssert.IsTrue(KingdomRoadRules.TryTrace(grid, 5, 3, 0, 0, 4, 0, 48, 400, route));
			ClassicAssert.Greater(route.Count, 0);
			int previousX = 0;
			int previousY = 0;
			for (int i = 0; i < route.Count; i++)
			{
				int x = KingdomRoadRules.UnpackX(route[i], 5);
				int y = KingdomRoadRules.UnpackY(route[i], 5);
				ClassicAssert.IsTrue(grid(x, y), "the walk passed through a cell nobody can walk through");
				ClassicAssert.AreEqual(1, KingdomLayoutRules.Chebyshev(x, y, previousX, previousY), "the walk skipped a cell");
				previousX = x;
				previousY = y;
			}
			ClassicAssert.AreEqual(1, KingdomLayoutRules.Chebyshev(previousX, previousY, 4, 0), "the walk did not arrive");
		}

		[Test]
		public void WalledOffIsNoWalkAtAll()
		{
			List<int> route = new List<int>();
			ClassicAssert.IsFalse(KingdomRoadRules.TryTrace(Grid(new string[3] { "..#..", "..#..", "..#.." }), 5, 3, 0, 1, 4, 1, 48, 400, route));
			ClassicAssert.AreEqual(0, route.Count);
		}

		[Test]
		public void ADestinationIsEnterableEvenWhenItIsSolid()
		{
			// A home and a work are solid objects. If the far end had to be walkable, no errand
			// in a real settlement would ever have a route at all.
			List<int> route = new List<int>();
			ClassicAssert.IsTrue(KingdomRoadRules.TryTrace(Grid(new string[1] { "....#" }), 5, 1, 0, 0, 4, 0, 48, 400, route));
			CollectionAssert.AreEqual(new int[3] { 1, 2, 3 }, route);
		}

		[Test]
		public void ARouteTooLongToBeAnErrandIsRefusedWhole()
		{
			List<int> route = new List<int>();
			ClassicAssert.IsFalse(KingdomRoadRules.TryTrace(Grid(new string[1] { ".........." }), 10, 1, 0, 0, 9, 0, 2, 400, route));
			ClassicAssert.AreEqual(0, route.Count);
		}

		[Test]
		public void TheSearchGivesUpRatherThanFloodingTheZone()
		{
			List<int> route = new List<int>();
			ClassicAssert.IsFalse(KingdomRoadRules.TryTrace(Grid(new string[1] { ".........." }), 10, 1, 0, 0, 9, 0, 48, 1, route));
		}

		[Test]
		public void TheSameSettlementWearsTheSameGroundEveryTime()
		{
			string[] rows = new string[4] { "..........", "..###..#..", "....#.....", ".........." };
			KingdomRoadRules.CellFilter grid = Grid(rows);
			List<int> first = new List<int>();
			List<int> second = new List<int>();
			ClassicAssert.IsTrue(KingdomRoadRules.TryTrace(grid, 10, 4, 0, 0, 9, 3, 48, 400, first));
			ClassicAssert.IsTrue(KingdomRoadRules.TryTrace(grid, 10, 4, 0, 0, 9, 3, 48, 400, second));
			CollectionAssert.AreEqual(first, second);
		}

		[TestCase(0, 0, 0, 5)]
		[TestCase(5, 5, -1, 5)]
		[TestCase(5, 5, 5, 5)]
		public void ARouteOffTheGridIsNoRoute(int fromX, int fromY, int toX, int toY)
		{
			List<int> route = new List<int>();
			ClassicAssert.IsFalse(KingdomRoadRules.TryTrace(Grid(new string[1] { "....." }), 5, 1, fromX, fromY, toX, toY, 48, 400, route));
		}

		[Test]
		public void NoGridAndNoListAreBothRefusals()
		{
			List<int> route = new List<int>();
			ClassicAssert.IsFalse(KingdomRoadRules.TryTrace(null, 5, 1, 0, 0, 4, 0, 48, 400, route));
			ClassicAssert.IsFalse(KingdomRoadRules.TryTrace(Grid(new string[1] { "....." }), 5, 1, 0, 0, 4, 0, 48, 400, null));
			ClassicAssert.IsFalse(KingdomRoadRules.TryTrace(Grid(new string[1] { "....." }), 0, 1, 0, 0, 4, 0, 48, 400, route));
		}

		// --- The way out -----------------------------------------------------------------

		[Test]
		public void GroundWithNoFrontierHasNoWayOut()
		{
			ClassicAssert.IsFalse(KingdomRoadRules.TryGate(10, 10, KingdomRules.Frontier.None, 5, 5, out _, out _));
		}

		[Test]
		public void TheGateIsTheEdgeCellNearestTheHeart()
		{
			ClassicAssert.IsTrue(KingdomRoadRules.TryGate(10, 10, KingdomRules.Frontier.North, 5, 5, out var x, out var y));
			ClassicAssert.AreEqual(5, x);
			ClassicAssert.AreEqual(1, y);
			ClassicAssert.IsTrue(KingdomRules.IsOnFrontier(x, y, 10, 10, KingdomRules.Frontier.North));
		}

		[Test]
		public void TheGateIsTheSameGateEveryTime()
		{
			ClassicAssert.IsTrue(KingdomRoadRules.TryGate(20, 20, KingdomRules.Frontier.East | KingdomRules.Frontier.West, 10, 10, out var firstX, out var firstY));
			ClassicAssert.IsTrue(KingdomRoadRules.TryGate(20, 20, KingdomRules.Frontier.East | KingdomRules.Frontier.West, 10, 10, out var secondX, out var secondY));
			ClassicAssert.AreEqual(firstX, secondX);
			ClassicAssert.AreEqual(firstY, secondY);
		}

		[Test]
		public void AZoneWithNoSizeHasNoGate()
		{
			ClassicAssert.IsFalse(KingdomRoadRules.TryGate(0, 10, KingdomRules.Frontier.North, 0, 0, out _, out _));
			ClassicAssert.IsFalse(KingdomRoadRules.TryGate(10, 0, KingdomRules.Frontier.North, 0, 0, out _, out _));
		}

		// --- The gatehouse: a placement rule, not a size ---------------------------------

		[TestCase("gatehouse", true)]
		[TestCase("GATEHOUSE", true)]
		[TestCase("  gatehouse ", true)]
		[TestCase("palisade", false)]
		[TestCase("rampart", false)]
		[TestCase("watchtower", false)]
		[TestCase("barracks", false)]
		[TestCase(null, false)]
		[TestCase("", false)]
		public void OnlyTheGatehouseIsSitedAtTheGate(string key, bool expected)
		{
			ClassicAssert.AreEqual(expected, KingdomRoadRules.SitesAtGate(key));
		}

		[Test]
		public void TheGatehouseTakesTheFrontierCellNearestTheWayOut()
		{
			// The whole rule: on the frontier wall, astride the road. TryGate names the cell the
			// settlement's own HeartToGate route is walked to, and the gatehouse goes there.
			ClassicAssert.IsTrue(KingdomRoadRules.TryGate(20, 20, KingdomRules.Frontier.North, 10, 10, out var gateX, out var gateY));
			int[] xs = new int[4] { 2, 10, 18, 6 };
			int[] ys = new int[4] { 0, 1, 0, 1 };
			int index = KingdomRoadRules.NearestToGate(xs, ys, gateX, gateY);
			ClassicAssert.AreEqual(1, index);
			ClassicAssert.AreEqual(gateX, xs[index]);
			ClassicAssert.AreEqual(gateY, ys[index]);
		}

		[Test]
		public void AnOccupiedGateCellSettlesForTheGroundBesideIt()
		{
			// The protection law forbids taking ground that holds anything, so the rule aims at
			// the way out and settles for the nearest offered cell - which is still the wall
			// astride the road.
			ClassicAssert.IsTrue(KingdomRoadRules.TryGate(20, 20, KingdomRules.Frontier.North, 10, 10, out var gateX, out var gateY));
			int[] xs = new int[3] { 2, 12, 18 };
			int[] ys = new int[3] { 1, 1, 1 };
			int index = KingdomRoadRules.NearestToGate(xs, ys, gateX, gateY);
			ClassicAssert.AreEqual(1, index, "the nearest offered cell, not the first one enumerated");
			ClassicAssert.IsTrue(KingdomLayoutRules.Chebyshev(xs[index], ys[index], gateX, gateY) <= 2);
		}

		[Test]
		public void TheGatehouseIsSitedTheSameWayEveryTimeItIsAsked()
		{
			// Ties are broken by the ground rather than by the order the engine enumerated cells,
			// so a reload puts the gatehouse back where it was.
			int[] xs = new int[4] { 12, 8, 10, 10 };
			int[] ys = new int[4] { 1, 1, 0, 2 };
			int[] shuffled = new int[4] { 10, 10, 8, 12 };
			int[] shuffledYs = new int[4] { 2, 0, 1, 1 };
			int first = KingdomRoadRules.NearestToGate(xs, ys, 10, 1);
			int second = KingdomRoadRules.NearestToGate(shuffled, shuffledYs, 10, 1);
			ClassicAssert.AreEqual(xs[first], shuffled[second]);
			ClassicAssert.AreEqual(ys[first], shuffledYs[second]);
		}

		[Test]
		public void ATieAtEqualDistanceGoesNorthmostThenWestmost()
		{
			int[] xs = new int[3] { 11, 9, 10 };
			int[] ys = new int[3] { 0, 0, 1 };
			int index = KingdomRoadRules.NearestToGate(xs, ys, 10, 0);
			ClassicAssert.AreEqual(1, index, "same distance, so the northmost then westmost cell wins");
		}

		[Test]
		public void NoGroundOfferedMeansNoGateSiting()
		{
			ClassicAssert.AreEqual(-1, KingdomRoadRules.NearestToGate(new int[0], new int[0], 5, 5));
			ClassicAssert.AreEqual(-1, KingdomRoadRules.NearestToGate(null, null, 5, 5));
		}

		[Test]
		public void ClaimingTheNeighbourMovesTheGateWithTheWall()
		{
			// The brief's "walls move outward as the city spans zones", read at the gate: a zone
			// whose north edge stops being frontier stops having its way out through the north.
			ClassicAssert.IsTrue(KingdomRoadRules.TryGate(20, 20, KingdomRules.Frontier.North | KingdomRules.Frontier.South, 10, 5,
				out var beforeX, out var beforeY));
			ClassicAssert.AreEqual(1, beforeY, "the heart sits near the north edge, so the way out is north");
			ClassicAssert.IsTrue(KingdomRoadRules.TryGate(20, 20, KingdomRules.Frontier.South, 10, 5, out var afterX, out var afterY));
			ClassicAssert.AreNotEqual(beforeY, afterY, "the way out moved to the edge that still faces the world");
			ClassicAssert.IsTrue(KingdomRules.IsOnFrontier(afterX, afterY, 20, 20, KingdomRules.Frontier.South));
			ClassicAssert.IsFalse(KingdomRules.IsOnFrontier(afterX, afterY, 20, 20, KingdomRules.Frontier.North));
		}

		[Test]
		public void AZoneTheRealmSurroundsHasNoGateToSiteAGatehouseAt()
		{
			ClassicAssert.IsFalse(KingdomRoadRules.TryGate(20, 20, KingdomRules.Frontier.None, 10, 10, out _, out _));
		}

		// --- The lane --------------------------------------------------------------------

		[TestCase(11, 10, 11, 8)]
		[TestCase(11, 13, 11, 15)]
		[TestCase(10, 11, 8, 11)]
		[TestCase(14, 11, 16, 11)]
		public void ADoorOpensOntoTheLaneTheGrammarReserved(int doorX, int doorY, int laneX, int laneY)
		{
			KingdomPlotRules.PlotRect rect = new KingdomPlotRules.PlotRect(10, 10, 14, 13);
			ClassicAssert.IsTrue(KingdomRoadRules.TryLane(rect, doorX, doorY, out var x, out var y));
			ClassicAssert.AreEqual(laneX, x);
			ClassicAssert.AreEqual(laneY, y);
			ClassicAssert.IsFalse(KingdomPlotRules.Reserved(rect).Contains(x, y), "the lane cell is inside the plot's own reserved rect");
		}

		[TestCase(10, 10)]
		[TestCase(14, 13)]
		[TestCase(12, 11)]
		[TestCase(0, 0)]
		public void ACornerOrAnInsideCellSaysNothingAboutWhichWayADoorFaces(int doorX, int doorY)
		{
			KingdomPlotRules.PlotRect rect = new KingdomPlotRules.PlotRect(10, 10, 14, 13);
			ClassicAssert.IsFalse(KingdomRoadRules.TryLane(rect, doorX, doorY, out _, out _));
		}

		[Test]
		public void AnInteriorAuthoredEntranceUsesOnlyUnclaimedGroundAndRotatesItsExactWay()
		{
			ArchitectureLayoutSnapshot snapshot = InteriorEntranceSnapshot();
			foreach (ArchitectureFacing facing in System.Enum.GetValues(typeof(ArchitectureFacing)))
			{
				snapshot.Facing = facing;
				ClassicAssert.IsTrue(KingdomArchitectureRules.TryWorldDimensions(snapshot.Width,
					snapshot.Height, facing, out int width, out int height));
				KingdomPlotRules.PlotRect rect = new KingdomPlotRules.PlotRect(
					10, 10, 9 + width, 9 + height);
				List<ArchitecturePoint> route = new List<ArchitecturePoint>();
				ClassicAssert.IsTrue(KingdomRoadRules.TryAuthoredLane(snapshot, rect,
					snapshot.Anchors[0], route, out int doorX, out int doorY,
					out int laneX, out int laneY), facing.ToString());
				ClassicAssert.AreEqual(2, route.Count, "one unclaimed edge cell plus one road margin");
				ClassicAssert.IsTrue(KingdomArchitectureRules.TryToWorld(rect.X1, rect.Y1,
					snapshot.Width, snapshot.Height, facing, 4, 2,
					out int edgeX, out int edgeY));
				ClassicAssert.AreEqual(edgeX, route[0].X, facing.ToString());
				ClassicAssert.AreEqual(edgeY, route[0].Y, facing.ToString());
				ClassicAssert.IsFalse(KingdomPlotRules.Reserved(rect).Contains(laneX, laneY));
				ClassicAssert.IsTrue(KingdomRoadRules.TryExactTrace(
					delegate(int x, int y) { return x != doorX || y != doorY; },
					30, 30, doorX, doorY, laneX, laneY,
					KingdomRoadRules.MaxRouteCells, route, new List<int>()));
			}
		}

		[Test]
		public void ACornerEntranceTakesCanonicalNorthThenRotatesWithTheBuilding()
		{
			ArchitectureLayoutSnapshot snapshot = InteriorEntranceSnapshot();
			snapshot.Anchors[0].Key = "entrance:public@0,0";
			snapshot.Anchors[0].X = 0;
			snapshot.Anchors[0].Y = 0;
			ArchitectureCellState oldEntrance = snapshot.Cells.Find(cell => cell.X == 3 && cell.Y == 2);
			oldEntrance.Passability = ArchitecturePassability.Blocked;
			ArchitectureCellState corner = snapshot.Cells.Find(cell => cell.X == 0 && cell.Y == 0);
			corner.Passability = ArchitecturePassability.Walkable;
			snapshot.Facing = ArchitectureFacing.East;
			KingdomPlotRules.PlotRect rect = new KingdomPlotRules.PlotRect(10, 10, 13, 14);
			List<ArchitecturePoint> route = new List<ArchitecturePoint>();
			ClassicAssert.IsTrue(KingdomRoadRules.TryAuthoredLane(snapshot, rect, snapshot.Anchors[0],
				route, out int doorX, out int doorY, out int laneX, out int laneY));
			ClassicAssert.AreEqual(1, route.Count, "a border entrance contributes only the exterior margin");
			ClassicAssert.AreEqual(doorX + 2, laneX, "canonical north rotates east");
			ClassicAssert.AreEqual(doorY, laneY);
		}

		[Test]
		public void ABorderThresholdNamesMarginThenTheTrueLaneTwoCellsBeyondTheLot()
		{
			ArchitectureLayoutSnapshot snapshot = InteriorEntranceSnapshot();
			snapshot.Anchors[0].Key = "entrance:public@2,3";
			snapshot.Anchors[0].X = 2;
			snapshot.Anchors[0].Y = 3;
			snapshot.Cells.Find(cell => cell.X == 3 && cell.Y == 2).Passability =
				ArchitecturePassability.Blocked;
			ArchitectureCellState threshold = snapshot.Cells.Find(
				cell => cell.X == 2 && cell.Y == 3);
			threshold.Claim = ArchitectureClaim.Building;
			threshold.Passability = ArchitecturePassability.Walkable;
			snapshot.Facing = ArchitectureFacing.North;
			KingdomPlotRules.PlotRect rect = new KingdomPlotRules.PlotRect(10, 10, 14, 13);
			List<ArchitecturePoint> route = new List<ArchitecturePoint>();

			ClassicAssert.IsTrue(KingdomRoadRules.TryAuthoredLane(snapshot, rect,
				snapshot.Anchors[0], route, out int doorX, out int doorY,
				out int laneX, out int laneY));
			ClassicAssert.AreEqual(12, doorX);
			ClassicAssert.AreEqual(13, doorY);
			ClassicAssert.AreEqual(1, route.Count);
			ClassicAssert.AreEqual(12, route[0].X);
			ClassicAssert.AreEqual(14, route[0].Y, "the first exterior cell is reserved margin");
			ClassicAssert.AreEqual(12, laneX);
			ClassicAssert.AreEqual(15, laneY, "road evidence belongs at the lane, not its margin");
			ClassicAssert.IsFalse(KingdomPlotRules.Reserved(rect).Contains(laneX, laneY));
		}

		[Test]
		public void InteriorEgressRefusesClaimedBlockedAndMalformedApproaches()
		{
			ArchitectureLayoutSnapshot snapshot = InteriorEntranceSnapshot();
			List<ArchitecturePoint> route = new List<ArchitecturePoint>();
			ArchitectureCellState approach = snapshot.Cells.Find(
				cell => cell.X == 4 && cell.Y == 2);

			approach.Claim = ArchitectureClaim.Building;
			ClassicAssert.IsFalse(KingdomRoadRules.TryAuthoredLane(snapshot,
				new KingdomPlotRules.PlotRect(10, 10, 14, 13), snapshot.Anchors[0],
				route, out _, out _, out _, out _));
			approach.Claim = ArchitectureClaim.Unclaimed;
			approach.Passability = ArchitecturePassability.Blocked;
			ClassicAssert.IsFalse(KingdomRoadRules.TryAuthoredLane(snapshot,
				new KingdomPlotRules.PlotRect(10, 10, 14, 13), snapshot.Anchors[0],
				route, out _, out _, out _, out _));
			approach.Passability = ArchitecturePassability.Walkable;
			snapshot.Cells.Add(new ArchitectureCellState
			{
				X = 4, Y = 2, Claim = ArchitectureClaim.Unclaimed,
				Passability = ArchitecturePassability.Walkable
			});
			ClassicAssert.IsFalse(KingdomRoadRules.TryAuthoredLane(snapshot,
				new KingdomPlotRules.PlotRect(10, 10, 14, 13), snapshot.Anchors[0],
				route, out _, out _, out _, out _));
		}

		[Test]
		public void ExactAuthoredTraceRefusesBlockedDuplicateAndDiagonalIntermediatesWhole()
		{
			List<int> packed = new List<int> { 99 };
			List<ArchitecturePoint> route = new List<ArchitecturePoint>
			{
				new ArchitecturePoint(2, 1), new ArchitecturePoint(3, 1)
			};
			ClassicAssert.IsFalse(KingdomRoadRules.TryExactTrace(
				delegate(int x, int y) { return x != 2 || y != 1; },
				8, 6, 1, 1, 4, 1, 8, route, packed));
			ClassicAssert.IsEmpty(packed, "a refused exact route publishes no prefix");

			route[1] = new ArchitecturePoint(2, 1);
			ClassicAssert.IsFalse(KingdomRoadRules.TryExactTrace(delegate(int x, int y) { return true; },
				8, 6, 1, 1, 3, 1, 8, route, packed));
			ClassicAssert.IsEmpty(packed);

			route[0] = new ArchitecturePoint(2, 2);
			route.RemoveAt(1);
			ClassicAssert.IsFalse(KingdomRoadRules.TryExactTrace(delegate(int x, int y) { return true; },
				8, 6, 1, 1, 3, 2, 8, route, packed));
			ClassicAssert.IsEmpty(packed);
		}

		private static ArchitectureLayoutSnapshot InteriorEntranceSnapshot()
		{
			ArchitectureLayoutSnapshot snapshot = new ArchitectureLayoutSnapshot
			{
				Width = 5, Height = 4, Facing = ArchitectureFacing.North
			};
			for (int y = 0; y < snapshot.Height; y++)
				for (int x = 0; x < snapshot.Width; x++)
					snapshot.Cells.Add(new ArchitectureCellState
					{
						X = x, Y = y, Claim = ArchitectureClaim.Building,
						Passability = ArchitecturePassability.Blocked,
						Cover = ArchitectureCover.Walled
					});
			ArchitectureCellState entrance = snapshot.Cells.Find(cell => cell.X == 3 && cell.Y == 2);
			entrance.Passability = ArchitecturePassability.Walkable;
			ArchitectureCellState exterior = snapshot.Cells.Find(cell => cell.X == 4 && cell.Y == 2);
			exterior.Claim = ArchitectureClaim.Unclaimed;
			exterior.Passability = ArchitecturePassability.Walkable;
			exterior.Cover = ArchitectureCover.Open;
			snapshot.Anchors.Add(new ArchitectureAnchor
			{
				Key = "entrance:public@3,2", X = 3, Y = 2,
				Access = ArchitectureAnchorAccess.OnCell
			});
			return snapshot;
		}

		// --- The tally -------------------------------------------------------------------

		[Test]
		public void AFirstWalkAdmitsACellAndLaterOnesAddToIt()
		{
			List<KingdomRoadRules.WornCell> tally = new List<KingdomRoadRules.WornCell>();
			ClassicAssert.IsTrue(KingdomRoadRules.Accrue(tally, 3, 4, 30, out var first));
			ClassicAssert.AreEqual(30, first);
			ClassicAssert.AreEqual(1, tally.Count);
			ClassicAssert.IsTrue(KingdomRoadRules.Accrue(tally, 3, 4, 30, out var second));
			ClassicAssert.AreEqual(60, second);
			ClassicAssert.AreEqual(1, tally.Count);
			ClassicAssert.AreEqual(60, KingdomRoadRules.TrafficAt(tally, 3, 4));
			ClassicAssert.AreEqual(0, KingdomRoadRules.TrafficAt(tally, 9, 9));
		}

		[Test]
		public void ATallyNeverClimbsPastItsCeiling()
		{
			List<KingdomRoadRules.WornCell> tally = new List<KingdomRoadRules.WornCell>();
			ClassicAssert.IsTrue(KingdomRoadRules.Accrue(tally, 0, 0, KingdomRoadRules.MaxTraffic + 5000, out var total));
			ClassicAssert.AreEqual(KingdomRoadRules.MaxTraffic, total);
			ClassicAssert.IsTrue(KingdomRoadRules.Accrue(tally, 0, 0, 500, out total));
			ClassicAssert.AreEqual(KingdomRoadRules.MaxTraffic, total);
		}

		[Test]
		public void WalkingNowhereAdmitsNothing()
		{
			List<KingdomRoadRules.WornCell> tally = new List<KingdomRoadRules.WornCell>();
			ClassicAssert.IsTrue(KingdomRoadRules.Accrue(tally, 1, 1, 0, out var total));
			ClassicAssert.AreEqual(0, total);
			ClassicAssert.AreEqual(0, tally.Count);
		}

		[Test]
		public void AFullTallyRefusesNewGroundButKeepsFeedingTheGroundItHas()
		{
			List<KingdomRoadRules.WornCell> tally = new List<KingdomRoadRules.WornCell>();
			for (int i = 0; i < KingdomRoadRules.MaxTrackedCells; i++)
			{
				tally.Add(new KingdomRoadRules.WornCell(i % 80, i / 80, 10));
			}
			ClassicAssert.IsFalse(KingdomRoadRules.Accrue(tally, 79, 79, 10, out var refused));
			ClassicAssert.AreEqual(0, refused);
			ClassicAssert.AreEqual(KingdomRoadRules.MaxTrackedCells, tally.Count);
			ClassicAssert.IsTrue(KingdomRoadRules.Accrue(tally, 0, 0, 10, out var fed));
			ClassicAssert.AreEqual(20, fed);
		}

		[Test]
		public void APathLeavesTheTallyBecauseThePathIsTheRecord()
		{
			List<KingdomRoadRules.WornCell> tally = new List<KingdomRoadRules.WornCell>();
			KingdomRoadRules.Accrue(tally, 2, 2, 300, out _);
			ClassicAssert.IsTrue(KingdomRoadRules.Retire(tally, 2, 2));
			ClassicAssert.AreEqual(0, tally.Count);
			ClassicAssert.IsFalse(KingdomRoadRules.Retire(tally, 2, 2));
		}

		[Test]
		public void ANullTallyIsARefusalAndNotACrash()
		{
			ClassicAssert.IsFalse(KingdomRoadRules.Accrue(null, 1, 1, 10, out var total));
			ClassicAssert.AreEqual(0, total);
			ClassicAssert.AreEqual(0, KingdomRoadRules.TrafficAt(null, 1, 1));
			ClassicAssert.AreEqual(-1, KingdomRoadRules.IndexOf(null, 1, 1));
			ClassicAssert.IsFalse(KingdomRoadRules.Retire(null, 1, 1));
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
			ClassicAssert.AreEqual("1,2,50;3,4,299", written);
			ClassicAssert.IsTrue(KingdomRoadRules.TryDecode(written, out var read, out var error));
			ClassicAssert.IsNull(error);
			ClassicAssert.AreEqual(2, read.Count);
			ClassicAssert.AreEqual(1, read[0].X);
			ClassicAssert.AreEqual(2, read[0].Y);
			ClassicAssert.AreEqual(3, read[1].X);
			ClassicAssert.AreEqual(4, read[1].Y);
			ClassicAssert.AreEqual(50, KingdomRoadRules.TrafficAt(read, 1, 2));
			ClassicAssert.AreEqual(299, KingdomRoadRules.TrafficAt(read, 3, 4));
		}

		[TestCase(null)]
		[TestCase("")]
		[TestCase("   ")]
		public void GroundNobodyHasWalkedWritesNothingAndReadsCleanly(string raw)
		{
			ClassicAssert.IsTrue(KingdomRoadRules.TryDecode(raw, out var cells, out var error));
			ClassicAssert.IsNull(error);
			ClassicAssert.AreEqual(0, cells.Count);
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
			ClassicAssert.AreEqual("2,2,30", KingdomRoadRules.Encode(tally));
			ClassicAssert.AreEqual("", KingdomRoadRules.Encode(null));
			ClassicAssert.AreEqual("", KingdomRoadRules.Encode(new List<KingdomRoadRules.WornCell>()));
		}

		[Test]
		public void AWrittenTallyIsClampedOnTheWayOut()
		{
			List<KingdomRoadRules.WornCell> tally = new List<KingdomRoadRules.WornCell>
			{
				new KingdomRoadRules.WornCell(1, 1, KingdomRoadRules.MaxTraffic + 900)
			};
			ClassicAssert.AreEqual("1,1," + KingdomRoadRules.MaxTraffic, KingdomRoadRules.Encode(tally));
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
			ClassicAssert.IsFalse(KingdomRoadRules.TryDecode(raw, out var cells, out var error));
			ClassicAssert.AreEqual(0, cells.Count);
			ClassicAssert.IsNotNull(error);
		}

		[Test]
		public void OneBadCellDoesNotCostTheRestOfTheGround()
		{
			ClassicAssert.IsFalse(KingdomRoadRules.TryDecode("1,2,50;garbage;3,4,60", out var cells, out var error));
			ClassicAssert.IsNotNull(error);
			ClassicAssert.AreEqual(2, cells.Count);
			ClassicAssert.AreEqual(50, KingdomRoadRules.TrafficAt(cells, 1, 2));
			ClassicAssert.AreEqual(60, KingdomRoadRules.TrafficAt(cells, 3, 4));
		}

		[Test]
		public void ARepeatedCellKeepsTheHeavierReading()
		{
			ClassicAssert.IsFalse(KingdomRoadRules.TryDecode("1,2,10;1,2,40", out var cells, out var error));
			ClassicAssert.IsNotNull(error);
			ClassicAssert.AreEqual(1, cells.Count);
			ClassicAssert.AreEqual(40, KingdomRoadRules.TrafficAt(cells, 1, 2));
			ClassicAssert.IsFalse(KingdomRoadRules.TryDecode("1,2,40;1,2,10", out cells, out error));
			ClassicAssert.AreEqual(40, KingdomRoadRules.TrafficAt(cells, 1, 2));
		}

		[Test]
		public void ATallyReadInFromOutsideIsClampedNotBelieved()
		{
			ClassicAssert.IsTrue(KingdomRoadRules.TryDecode("5,5," + (KingdomRoadRules.MaxTraffic + 10000), out var cells, out var error));
			ClassicAssert.IsNull(error);
			ClassicAssert.AreEqual(KingdomRoadRules.MaxTraffic, KingdomRoadRules.TrafficAt(cells, 5, 5));
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
			ClassicAssert.IsFalse(KingdomRoadRules.TryDecode(raw.ToString(), out var cells, out var error));
			ClassicAssert.IsNotNull(error);
			ClassicAssert.AreEqual(KingdomRoadRules.MaxTrackedCells, cells.Count);
		}

		// --- Paving ----------------------------------------------------------------------

		[TestCase("Marble", "MarbleFloor")]
		[TestCase("Black Marble", "BlackMarbleWalkway")]
		[TestCase("Limestone", "SaltPath")]
		[TestCase("BrinestalkWall", "WoodFloor")]
		[TestCase("Verdigris", "GreenTile")]
		[TestCase("Fulcrete", "FoamcreteFloor")]
		[TestCase("Foamcrete", "FoamcreteFloor")]
		[TestCase("MetalWall", "SmallHexFloor")]
		[TestCase("WoodWall", "WoodFloor")]
		[TestCase("SomeoneElsesWall", "DirtPath")]
		[TestCase(null, "DirtPath")]
		public void PavingIsLaidInTheWallTheSettlementBuildsIn(string wall, string expected)
		{
			ClassicAssert.AreEqual(expected, KingdomRoadRules.PavedFloorFor(wall));
		}

		[Test]
		public void EveryWallTheModBuildsInHasPavingAndAPriceForIt()
		{
			// The wall list and the paving list are two files apart. If a seventh wall material
			// is added and nobody teaches paving about it, this is the test that says so.
			for (int i = 0; i < KingdomPlotRules.WallMaterials.Length; i++)
			{
				string wall = KingdomPlotRules.WallMaterials[i];
				ClassicAssert.AreNotEqual("DirtPath", KingdomRoadRules.PavedFloorFor(wall), "no paving is named for " + wall);
				ClassicAssert.IsTrue(KingdomRoadRules.CanPaveIn(KingdomRoadRules.PaveMaterialFor(wall)), "nothing can be spent to pave in " + wall);
			}
		}

		[TestCase("Marble", KingdomMaterial.Marble)]
		[TestCase("Black Marble", KingdomMaterial.Marble)]
		[TestCase("Limestone", KingdomMaterial.Stone)]
		// Fulcrete is what KingdomMaterials.WallBlueprint raises out of ShapedStone and nothing
		// else, so paving beside a Fulcrete wall is priced in dressed stone, not raw (Addendum 7).
		[TestCase("Fulcrete", KingdomMaterial.ShapedStone)]
		[TestCase("Foamcrete", KingdomMaterial.Stone)]
		[TestCase("MetalWall", KingdomMaterial.WorkedMetal)]
		[TestCase("WoodWall", KingdomMaterial.ShapedTimber)]
		[TestCase("Verdigris", KingdomMaterial.Scrap)]
		[TestCase("BrinestalkWall", KingdomMaterial.Timber)]
		[TestCase("SomeoneElsesWall", KingdomMaterial.Mud)]
		[TestCase(null, KingdomMaterial.Mud)]
		public void PavingIsPaidForInWhatTheWallsAreMadeOf(string wall, KingdomMaterial expected)
		{
			ClassicAssert.AreEqual(expected, KingdomRoadRules.PaveMaterialFor(wall));
		}

		[TestCase(KingdomMaterial.Mud, false)]
		[TestCase(KingdomMaterial.Brush, false)]
		[TestCase(KingdomMaterial.Timber, true)]
		[TestCase(KingdomMaterial.Stone, true)]
		[TestCase(KingdomMaterial.Marble, true)]
		[TestCase(KingdomMaterial.Scrap, true)]
		public void YouCannotPaveTheGroundWithTheGround(KingdomMaterial material, bool expected)
		{
			ClassicAssert.AreEqual(expected, KingdomRoadRules.CanPaveIn(material));
		}

		[TestCase(-3, 0)]
		[TestCase(0, 0)]
		[TestCase(1, KingdomRoadRules.PaveUnitsPerCell)]
		[TestCase(12, 12 * KingdomRoadRules.PaveUnitsPerCell)]
		public void PavingCostsPerCell(int cells, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomRoadRules.PaveCost(cells));
		}

		[TestCase(-1, 0)]
		[TestCase(0, 0)]
		[TestCase(7, 7)]
		[TestCase(KingdomRoadRules.MaxPaveCellsPerOrder + 30, KingdomRoadRules.MaxPaveCellsPerOrder)]
		public void OneOrderCoversOnlyWhatOneOrderCovers(int available, int expected)
		{
			ClassicAssert.AreEqual(expected, KingdomRoadRules.PaveCells(available));
		}

		// --- Prose -----------------------------------------------------------------------

		[TestCase(KingdomRoadRules.WearState.Untouched)]
		[TestCase(KingdomRoadRules.WearState.Worn)]
		[TestCase(KingdomRoadRules.WearState.Paved)]
		public void TheRungsThatSayNothingSayNothing(KingdomRoadRules.WearState state)
		{
			ClassicAssert.IsNull(KingdomRoadRules.WearLine(state, "Ezra"));
		}

		[TestCase(KingdomRoadRules.WearState.Trodden)]
		[TestCase(KingdomRoadRules.WearState.Path)]
		public void TheRungsWorthRemarkingOnNameTheSettlement(KingdomRoadRules.WearState state)
		{
			string line = KingdomRoadRules.WearLine(state, "Ezra");
			ClassicAssert.IsNotNull(line);
			StringAssert.Contains("Ezra", line);
			ClassicAssert.IsNotNull(KingdomRoadRules.WearLine(state, null));
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
			ClassicAssert.IsNotEmpty(KingdomRoadRules.RefuseNotOurGround());
		}

		[Test]
		public void ARefusalNeverLeavesAHoleWhereTheNameGoes()
		{
			ClassicAssert.IsNotEmpty(KingdomRoadRules.RefuseNothingWorn(null));
			ClassicAssert.IsNotEmpty(KingdomRoadRules.RefuseHands(null));
			ClassicAssert.IsNotEmpty(KingdomRoadRules.RefuseTallyFull(null));
			ClassicAssert.IsNotEmpty(KingdomRoadRules.PavedLine(1, KingdomMaterial.Stone, null));
			ClassicAssert.IsNotEmpty(KingdomRoadRules.PavedRecord(1, KingdomMaterial.Stone, null));
		}
	}
}
#endif
