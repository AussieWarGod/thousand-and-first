#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// The two-level distance matrix and the roads discount. LIVING-CITY-ARCHITECTURE §3.10(2) and
	/// (3): we never store works², the zone graph is all-pairs at 9³ integer operations, and a road
	/// is a named constant applied identically to the estimate and to any measured length so the
	/// two cannot disagree.
	/// </summary>
	internal class KingdomDistanceRulesTests
	{
		[Test]
		public void CapturedEndpointFreezesEveryMeasuredColumnWithoutReordering()
		{
			KingdomDistanceEndpointState row = KingdomDistanceEndpointState.Capture(
				EndpointId: 17,
				ObjectId: "holder-17",
				X: 3,
				Y: 5,
				DedicationOrdinal: 23,
				WaterAmount: 29L,
				FoodAmount: 31L,
				WaterRoom: 37L,
				FoodRoom: 41L,
				WaterHolderEdges: 1 << (int)KingdomZoneStep.North,
				FoodHolderEdges: 1 << (int)KingdomZoneStep.South,
				WaterTargetEdges: 1 << (int)KingdomZoneStep.East,
				FoodTargetEdges: 1 << (int)KingdomZoneStep.West);

			ClassicAssert.AreEqual(17, row.EndpointId);
			ClassicAssert.AreEqual("holder-17", row.ObjectId);
			ClassicAssert.AreEqual(3, row.X);
			ClassicAssert.AreEqual(5, row.Y);
			ClassicAssert.AreEqual(23, row.DedicationOrdinal);
			ClassicAssert.AreEqual(29L, row.Amount(KingdomStockKind.Water));
			ClassicAssert.AreEqual(31L, row.Amount(KingdomStockKind.Food));
			ClassicAssert.AreEqual(37L, row.Room(KingdomStockKind.Water));
			ClassicAssert.AreEqual(41L, row.Room(KingdomStockKind.Food));
			ClassicAssert.IsTrue(row.WinsHolder(KingdomStockKind.Water, KingdomZoneStep.North));
			ClassicAssert.IsTrue(row.WinsHolder(KingdomStockKind.Food, KingdomZoneStep.South));
			ClassicAssert.IsTrue(row.WinsTarget(KingdomStockKind.Water, KingdomZoneStep.East));
			ClassicAssert.IsTrue(row.WinsTarget(KingdomStockKind.Food, KingdomZoneStep.West));
		}

		[Test]
		public void SplitDeclarationsPreserveEnumValuesAndNodeFieldOrder()
		{
			ClassicAssert.AreEqual(0, (int)KingdomZoneStep.North);
			ClassicAssert.AreEqual(1, (int)KingdomZoneStep.South);
			ClassicAssert.AreEqual(2, (int)KingdomZoneStep.East);
			ClassicAssert.AreEqual(3, (int)KingdomZoneStep.West);
			ClassicAssert.AreEqual(4, (int)KingdomZoneStep.Up);
			ClassicAssert.AreEqual(5, (int)KingdomZoneStep.Down);
			ClassicAssert.AreEqual(6, (int)KingdomZoneStep.None);

			System.Reflection.FieldInfo[] fields = typeof(KingdomZoneNode).GetFields(
				System.Reflection.BindingFlags.Instance
				| System.Reflection.BindingFlags.NonPublic
				| System.Reflection.BindingFlags.Public);
			CollectionAssert.AreEqual(new string[]
			{
				"ZoneId", "GlobalX", "GlobalY", "Stratum", "Shaft"
			}, Array.ConvertAll(fields, field => field.Name));

			KingdomZoneNode node = default(KingdomZoneNode);
			ClassicAssert.IsNull(node.ZoneId);
			ClassicAssert.AreEqual(0, node.GlobalX);
			ClassicAssert.AreEqual(0, node.GlobalY);
			ClassicAssert.AreEqual(0, node.Stratum);
			ClassicAssert.IsFalse(node.Shaft);
		}

		private static KingdomZoneNode Node(string id, int x, int y, int z)
		{
			return new KingdomZoneNode(id, x, y, z);
		}

		/// <summary>Ground with a finished delve going down from it.</summary>
		private static KingdomZoneNode Shafted(string id, int x, int y, int z)
		{
			return new KingdomZoneNode(id, x, y, z, shaft: true);
		}

		/// <summary>
		/// Orthogonal in the same stratum, plus the stratum directly above and below. Deliberately
		/// narrower than KingdomRules.CoordsAdjacent, which admits diagonals because a CLAIM may
		/// border a zone corner-to-corner — a carrier cannot walk through a corner.
		/// </summary>
		[Test]
		public void TheRoutingGraphHasNoDiagonalEdge()
		{
			KingdomZoneNode here = Node("a", 5, 5, 10);
			ClassicAssert.AreEqual(KingdomZoneStep.North, KingdomDistanceRules.StepBetween(here, Node("b", 5, 4, 10)));
			ClassicAssert.AreEqual(KingdomZoneStep.South, KingdomDistanceRules.StepBetween(here, Node("b", 5, 6, 10)));
			ClassicAssert.AreEqual(KingdomZoneStep.East, KingdomDistanceRules.StepBetween(here, Node("b", 6, 5, 10)));
			ClassicAssert.AreEqual(KingdomZoneStep.West, KingdomDistanceRules.StepBetween(here, Node("b", 4, 5, 10)));
			ClassicAssert.AreEqual(KingdomZoneStep.None, KingdomDistanceRules.StepBetween(here, Node("b", 6, 6, 10)),
				"a corner is not an edge a carrier can walk through");
			ClassicAssert.AreEqual(KingdomZoneStep.None, KingdomDistanceRules.StepBetween(here, Node("b", 7, 5, 10)));
			ClassicAssert.AreEqual(KingdomZoneStep.None, KingdomDistanceRules.StepBetween(here, here));
		}

		/// <summary>
		/// The ARITHMETIC of a stratum is free — ZoneID already carries it, so a city three strata
		/// deep sums the same as a flat one (§0.0(f)). The GROUND never was, and that is the whole
		/// of the delve: a direction always exists between a zone and the one under it, and an
		/// EDGE exists only where somebody cut a shaft.
		/// </summary>
		[Test]
		public void AStratumAboveOrBelowIsOneStepAndOnlyAnEdgeWhereAShaftWasCut()
		{
			KingdomZoneNode here = Node("a", 5, 5, 10);
			ClassicAssert.AreEqual(KingdomZoneStep.Down, KingdomDistanceRules.StepBetween(here, Node("b", 5, 5, 11)));
			ClassicAssert.AreEqual(KingdomZoneStep.Up, KingdomDistanceRules.StepBetween(here, Node("b", 5, 5, 9)));
			ClassicAssert.AreEqual(KingdomZoneStep.None, KingdomDistanceRules.StepBetween(here, Node("b", 5, 5, 12)));
			ClassicAssert.AreEqual(KingdomZoneStep.None, KingdomDistanceRules.StepBetween(here, Node("b", 6, 5, 11)),
				"a stairwell goes straight up, never up and across");

			// The step is named and the rock is still shut.
			ClassicAssert.IsFalse(KingdomDistanceRules.Adjacent(here, Node("b", 5, 5, 11)),
				"unbroken rock is not a doorway because the coordinates differ by one");
			ClassicAssert.IsFalse(KingdomDistanceRules.Adjacent(Node("b", 5, 5, 11), here),
				"and it is shut from underneath too");

			KingdomZoneNode cut = Shafted("a", 5, 5, 10);
			ClassicAssert.IsTrue(KingdomDistanceRules.Adjacent(cut, Node("b", 5, 5, 11)));
			ClassicAssert.IsTrue(KingdomDistanceRules.Adjacent(Node("b", 5, 5, 11), cut),
				"a shaft is walked both ways, so the edge is symmetric whichever end asks");
		}

		/// <summary>The flag is read off the SHALLOWER node, because that is the ground the winding
		/// gear stands on. A shaft claimed by the zone underneath opens nothing.</summary>
		[Test]
		public void TheShaftIsReadOffTheGroundTheWindingGearStandsOn()
		{
			ClassicAssert.IsFalse(KingdomDistanceRules.Adjacent(Node("a", 5, 5, 10), Shafted("b", 5, 5, 11)));
		}

		/// <summary>A shaft's foot must be rock. A stair up the inside of a tower is a building in
		/// a set nobody has written, and it does not arrive through this door.</summary>
		[Test]
		public void NothingAboveTheSurfaceIsJoinedByAShaft()
		{
			ClassicAssert.IsFalse(KingdomDistanceRules.Adjacent(Shafted("a", 5, 5, 9), Node("b", 5, 5, 10)));
		}

		/// <summary>Rock with nothing cut down to it is unreachable in the graph — refused, never
		/// estimated — which is what makes it sort LAST in the nearest-first apportionment rather
		/// than looking like the nearest store in the city.</summary>
		[Test]
		public void UndelvedRockHasNoRouteAtAll()
		{
			KingdomZoneNode[] nodes = new KingdomZoneNode[2] { Node("surface", 5, 5, 10), Node("deep", 5, 5, 11) };
			KingdomZoneGraph graph;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomZoneGraph.TryBuild(nodes, 2, KingdomDistanceRules.ZoneTransitCells, out graph, out fault));
			int cells;
			ClassicAssert.IsFalse(graph.TryDistance(0, 1, out cells));
			int[] path = new int[KingdomDistanceRules.MaxNodes];
			int length;
			ClassicAssert.IsFalse(graph.TryPath(0, 1, path, out length, out fault));
		}

		/// <summary>A cut shaft is three ordinary hops: the whole depth of a stratum, climbed, with
		/// the load on your back. The catalogue promises the asymmetry out loud.</summary>
		[Test]
		public void ACutShaftCostsExactlyThreeOrdinaryHops()
		{
			KingdomZoneNode[] nodes = new KingdomZoneNode[2] { Shafted("surface", 5, 5, 10), Node("deep", 5, 5, 11) };
			KingdomZoneGraph graph;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomZoneGraph.TryBuild(nodes, 2, KingdomDistanceRules.ZoneTransitCells, out graph, out fault));
			int down;
			int up;
			ClassicAssert.IsTrue(graph.TryDistance(0, 1, out down));
			ClassicAssert.IsTrue(graph.TryDistance(1, 0, out up));
			ClassicAssert.AreEqual(3 * KingdomDistanceRules.ZoneTransitCells, down);
			ClassicAssert.AreEqual(down, up, "the climb costs the same whichever way the load is going");
			ClassicAssert.AreEqual(KingdomDelveRules.ShaftHopCells(KingdomDistanceRules.ZoneTransitCells), down);
		}

		/// <summary>
		/// The delve changed the deep and changed nothing else. Every surface pair of a parasang
		/// measures exactly what it measured before a shaft existed, with a whole opened stratum
		/// hanging off it — otherwise the wave quietly retuned every route in the game.
		/// </summary>
		[Test]
		public void OpeningTheDeepLeavesEverySurfaceDistanceBitIdentical()
		{
			KingdomZoneNode[] flat = new KingdomZoneNode[9];
			for (int i = 0; i < 9; i++)
			{
				flat[i] = Node("z" + i, i % 3, i / 3, 10);
			}
			KingdomZoneNode[] delved = new KingdomZoneNode[9];
			for (int i = 0; i < 8; i++)
			{
				delved[i] = (i == 0) ? Shafted("z0", 0, 0, 10) : Node("z" + i, i % 3, i / 3, 10);
			}
			delved[8] = Node("under", 0, 0, 11);

			KingdomZoneGraph plain;
			KingdomZoneGraph opened;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomZoneGraph.TryBuild(flat, 9, KingdomDistanceRules.ZoneTransitCells, out plain, out fault));
			ClassicAssert.IsTrue(KingdomZoneGraph.TryBuild(delved, 9, KingdomDistanceRules.ZoneTransitCells, out opened, out fault));
			for (int i = 0; i < 8; i++)
			{
				for (int j = 0; j < 8; j++)
				{
					int was;
					int now;
					bool hadRoute = plain.TryDistance(i, j, out was);
					bool hasRoute = opened.TryDistance(i, j, out now);
					ClassicAssert.AreEqual(hadRoute, hasRoute, "surface route " + i + "->" + j + " changed existence");
					if (hadRoute)
					{
						ClassicAssert.AreEqual(was, now, "surface distance " + i + "->" + j + " changed length");
					}
				}
			}
			int descent;
			ClassicAssert.IsTrue(opened.TryDistance(0, 8, out descent));
			ClassicAssert.AreEqual(3 * KingdomDistanceRules.ZoneTransitCells, descent);
		}

		/// <summary>
		/// §3.10(3): a paved leg costs 0.6 of the same distance unpaved. The constant is named once
		/// and lives with the itinerary rules, because the estimate and the measured length must
		/// scale by the same number or a road makes the two disagree.
		/// </summary>
		[Test]
		public void TheRoadsDiscountIsANamedSixtyPercent()
		{
			ClassicAssert.AreEqual(60, KingdomItineraryRules.RoadDiscountPercent);
			ClassicAssert.AreEqual(100, KingdomItineraryRules.NoRoadDiscountPercent);
			int paved;
			int plain;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomDistanceRules.TryDiscount(100, KingdomItineraryRules.RoadDiscountPercent, out paved, out fault));
			ClassicAssert.IsTrue(KingdomDistanceRules.TryDiscount(100, KingdomItineraryRules.NoRoadDiscountPercent, out plain, out fault));
			ClassicAssert.AreEqual(60, paved);
			ClassicAssert.AreEqual(100, plain);
			ClassicAssert.IsTrue(paved < plain, "laying a road must visibly shorten every itinerary that uses it");
		}

		/// <summary>A road makes a journey shorter and never instantaneous, and zero stays
		/// zero.</summary>
		[Test]
		public void TheDiscountNeverReachesZeroForARealDistance()
		{
			int cells;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomDistanceRules.TryDiscount(1, 60, out cells, out fault));
			ClassicAssert.AreEqual(1, cells);
			ClassicAssert.IsTrue(KingdomDistanceRules.TryDiscount(0, 60, out cells, out fault));
			ClassicAssert.AreEqual(0, cells);
			ClassicAssert.IsFalse(KingdomDistanceRules.TryDiscount(-1, 60, out cells, out fault));
			ClassicAssert.IsFalse(KingdomDistanceRules.TryDiscount(10, 0, out cells, out fault));
			ClassicAssert.IsFalse(KingdomDistanceRules.TryDiscount(10, 101, out cells, out fault),
				"a 'discount' over 100 percent would be a road that lengthens the road");
		}

		/// <summary>The triangular same-zone index is symmetric and collision-free, which is what
		/// keeps one distance from being stored as two answers.</summary>
		[Test]
		public void SameZonePairsAreSymmetricAndCollisionFree()
		{
			const int Works = 8;
			bool[] seen = new bool[KingdomDistanceRules.PairSlots(Works)];
			KingdomCityFault fault;
			for (int a = 0; a < Works; a++)
			{
				for (int b = a + 1; b < Works; b++)
				{
					int forward;
					int backward;
					ClassicAssert.IsTrue(KingdomDistanceRules.TryPairIndex(a, b, Works, out forward, out fault));
					ClassicAssert.IsTrue(KingdomDistanceRules.TryPairIndex(b, a, Works, out backward, out fault));
					ClassicAssert.AreEqual(forward, backward);
					ClassicAssert.IsTrue(forward >= 0 && forward < seen.Length);
					ClassicAssert.IsFalse(seen[forward], "two pairs claimed the same slot");
					seen[forward] = true;
				}
			}
			for (int i = 0; i < seen.Length; i++)
			{
				ClassicAssert.IsTrue(seen[i], "a slot nobody indexes is a slot that should not have been allocated");
			}
			int index;
			ClassicAssert.IsFalse(KingdomDistanceRules.TryPairIndex(3, 3, Works, out index, out fault),
				"a work is not a pair with itself");
		}

		/// <summary>
		/// §3.10(2)'s own figure: all-pairs over nine nodes is 9³ = 729 integer operations, and the
		/// table is at most 81 entries. Counted rather than asserted, so a later change of algorithm
		/// has to answer to the number.
		/// </summary>
		[Test]
		public void TheZoneGraphCostsExactlyNineCubed()
		{
			KingdomZoneNode[] nodes = new KingdomZoneNode[9];
			for (int i = 0; i < 9; i++)
			{
				nodes[i] = Node("z" + i, i % 3, i / 3, 10);
			}
			KingdomZoneGraph graph;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomZoneGraph.TryBuild(nodes, 9, KingdomDistanceRules.ZoneTransitCells, out graph, out fault));
			ClassicAssert.AreEqual(729L, graph.Operations);
			ClassicAssert.AreEqual(9, graph.Count);
		}

		/// <summary>A 3x3 parasang: opposite corners are four hops, and the composed distance is
		/// four transits. The metric never contains the elapsed and never contains a draw.</summary>
		[Test]
		public void OppositeCornersOfAParasangAreFourHops()
		{
			KingdomZoneNode[] nodes = new KingdomZoneNode[9];
			for (int i = 0; i < 9; i++)
			{
				nodes[i] = Node("z" + i, i % 3, i / 3, 10);
			}
			KingdomZoneGraph graph;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomZoneGraph.TryBuild(nodes, 9, KingdomDistanceRules.ZoneTransitCells, out graph, out fault));
			int cells;
			ClassicAssert.IsTrue(graph.TryDistance(0, 8, out cells));
			ClassicAssert.AreEqual(4 * KingdomDistanceRules.ZoneTransitCells, cells);
			int[] path = new int[KingdomDistanceRules.MaxNodes];
			int length;
			ClassicAssert.IsTrue(graph.TryPath(0, 8, path, out length, out fault));
			ClassicAssert.AreEqual(5, length, "four hops is five nodes, both ends included");
			ClassicAssert.AreEqual(0, path[0]);
			ClassicAssert.AreEqual(8, path[length - 1]);
			for (int i = 1; i < length; i++)
			{
				KingdomZoneStep step;
				ClassicAssert.IsTrue(graph.TryStep(path[i - 1], path[i], out step), "every hop of a path is a real edge");
			}
		}

		/// <summary>A road-discounted hop shortens every route that uses it, which is the
		/// consequence the player actually sees (§3.10(3)).</summary>
		[Test]
		public void ADiscountedHopShortensTheWholeRoute()
		{
			KingdomZoneNode[] nodes = new KingdomZoneNode[3]
			{
				Node("a", 0, 0, 10), Node("b", 1, 0, 10), Node("c", 2, 0, 10)
			};
			int paved;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomDistanceRules.TryDiscount(KingdomDistanceRules.ZoneTransitCells, KingdomItineraryRules.RoadDiscountPercent, out paved, out fault));
			KingdomZoneGraph plain;
			KingdomZoneGraph roaded;
			ClassicAssert.IsTrue(KingdomZoneGraph.TryBuild(nodes, 3, KingdomDistanceRules.ZoneTransitCells, out plain, out fault));
			ClassicAssert.IsTrue(KingdomZoneGraph.TryBuild(nodes, 3, paved, out roaded, out fault));
			int longWay;
			int shortWay;
			ClassicAssert.IsTrue(plain.TryDistance(0, 2, out longWay));
			ClassicAssert.IsTrue(roaded.TryDistance(0, 2, out shortWay));
			ClassicAssert.IsTrue(shortWay < longWay);
			ClassicAssert.AreEqual(2 * paved, shortWay);
		}

		/// <summary>A zone with no edge to the rest is unreachable rather than very far away: a
		/// route the planner cannot make is refused, never estimated.</summary>
		[Test]
		public void AnIslandZoneHasNoRoute()
		{
			KingdomZoneNode[] nodes = new KingdomZoneNode[2] { Node("a", 0, 0, 10), Node("b", 40, 40, 10) };
			KingdomZoneGraph graph;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomZoneGraph.TryBuild(nodes, 2, KingdomDistanceRules.ZoneTransitCells, out graph, out fault));
			int cells;
			ClassicAssert.IsFalse(graph.TryDistance(0, 1, out cells));
			int[] path = new int[KingdomDistanceRules.MaxNodes];
			int length;
			ClassicAssert.IsFalse(graph.TryPath(0, 1, path, out length, out fault));
			ClassicAssert.AreEqual(KingdomCityFault.OutsideItinerary, fault);
		}

		/// <summary>
		/// The composition of §3.10(2), in O(1) from three stores that are never works²: the whole
		/// point of the two-level shape.
		/// </summary>
		[Test]
		public void CrossZoneDistanceComposesFromWorkToEdgePlusHopsPlusEdgeToWork()
		{
			KingdomZoneNode[] nodes = new KingdomZoneNode[2] { Node("a", 0, 0, 10), Node("b", 1, 0, 10) };
			KingdomZoneGraph graph;
			KingdomDistanceMatrix matrix;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomZoneGraph.TryBuild(nodes, 2, KingdomDistanceRules.ZoneTransitCells, out graph, out fault));
			ClassicAssert.IsTrue(KingdomDistanceMatrix.TryCreate(graph, out matrix, out fault));
			ClassicAssert.IsTrue(matrix.IsDirty(0), "a new slice is dirty until the ground has been read");

			int[] ids = new int[4] { 101, 102, 103, 104 };
			ushort[] edges = new ushort[4 * KingdomDistanceRules.EdgesPerZone];
			ushort[] pairs = new ushort[KingdomDistanceRules.PairSlots(4)];
			for (int i = 0; i < edges.Length; i++) { edges[i] = 7; }
			for (int i = 0; i < pairs.Length; i++) { pairs[i] = 3; }
			ClassicAssert.IsTrue(matrix.TryWriteZone(0, ids, edges, pairs, out fault));
			ClassicAssert.IsTrue(matrix.TryWriteZone(1, ids, edges, pairs, out fault));
			ClassicAssert.IsFalse(matrix.IsDirty(0));

			int cells;
			ClassicAssert.IsTrue(matrix.TryCompose(0, 101, 1, 103, out cells, out fault));
			ClassicAssert.AreEqual(7 + KingdomDistanceRules.ZoneTransitCells + 7, cells);

			ClassicAssert.IsTrue(matrix.TryCompose(0, 101, 0, 102, out cells, out fault));
			ClassicAssert.AreEqual(3, cells, "a same-zone pair is read straight out of the triangular slice");
			ClassicAssert.IsTrue(matrix.TryCompose(0, 103, 0, 103, out cells, out fault));
			ClassicAssert.AreEqual(0, cells, "a work stands no distance from itself");
		}

		/// <summary>
		/// Invalidation is by structure, never by time or by stock — and a dirty slice REFUSES
		/// rather than answering, because a route planned on a stale slice is a carrier walking past
		/// a nearer holder (I6).
		/// </summary>
		[Test]
		public void ADirtySliceRefusesToAnswer()
		{
			KingdomZoneNode[] nodes = new KingdomZoneNode[2] { Node("a", 0, 0, 10), Node("b", 1, 0, 10) };
			KingdomZoneGraph graph;
			KingdomDistanceMatrix matrix;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomZoneGraph.TryBuild(nodes, 2, KingdomDistanceRules.ZoneTransitCells, out graph, out fault));
			ClassicAssert.IsTrue(KingdomDistanceMatrix.TryCreate(graph, out matrix, out fault));
			int[] ids = new int[2] { 201, 202 };
			ushort[] edges = new ushort[2 * KingdomDistanceRules.EdgesPerZone];
			ushort[] pairs = new ushort[KingdomDistanceRules.PairSlots(2)];
			for (int i = 0; i < edges.Length; i++) { edges[i] = 5; }
			for (int i = 0; i < pairs.Length; i++) { pairs[i] = 5; }
			ClassicAssert.IsTrue(matrix.TryWriteZone(0, ids, edges, pairs, out fault));
			ClassicAssert.IsTrue(matrix.TryWriteZone(1, ids, edges, pairs, out fault));
			int cells;
			ClassicAssert.IsTrue(matrix.TryCompose(0, 201, 1, 202, out cells, out fault));

			matrix.MarkDirty("b");
			ClassicAssert.IsTrue(matrix.IsDirty(1));
			ClassicAssert.IsFalse(matrix.TryCompose(0, 201, 1, 202, out cells, out fault),
				"a work placed or a road laid makes the slice unbelievable until it is read again");
			ClassicAssert.IsTrue(matrix.TryWriteZone(1, ids, edges, pairs, out fault));
			ClassicAssert.IsTrue(matrix.TryCompose(0, 201, 1, 202, out cells, out fault));
		}

		/// <summary>
		/// §0.0(c)'s memory row is the contract. A matrix that allocated past the entry budget would
		/// be the regression the table exists to catch, so it refuses instead.
		/// </summary>
		[Test]
		public void TheSparseMatrixFitsFourLegalTwoHundredAndTwentyWorkZonesWithoutCachingWorksSquared()
		{
			KingdomZoneNode[] nodes = new KingdomZoneNode[4];
			for (int i = 0; i < nodes.Length; i++)
			{
				nodes[i] = Node("z" + i, i, 0, 10);
			}
			KingdomZoneGraph graph;
			KingdomDistanceMatrix matrix;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomZoneGraph.TryBuild(nodes, nodes.Length, KingdomDistanceRules.ZoneTransitCells, out graph, out fault));
			ClassicAssert.IsTrue(KingdomDistanceMatrix.TryCreate(graph, out matrix, out fault));
			ClassicAssert.AreEqual(21, KingdomDistanceMatrix.EndpointShare(4),
				"sparse endpoint share is derived from both entry caps, not the legal work count");
			for (int zone = 0; zone < nodes.Length; zone++)
			{
				// This zone may legally contain 220 works. Only the 21 endpoints named by the
				// frozen logistics snapshot occupy the cache; the other 199 are ordinary model
				// rows, never silently discarded and never multiplied into works-squared.
				int count = KingdomDistanceMatrix.EndpointShare(nodes.Length);
				int[] ids = new int[count];
				ushort[] edges = new ushort[count * KingdomDistanceRules.EdgesPerZone];
				ushort[] pairs = new ushort[KingdomDistanceRules.PairSlots(count)];
				for (int i = 0; i < count; i++) ids[i] = zone * 1000 + i + 1;
				ClassicAssert.IsTrue(matrix.TryWriteZone(zone, ids, edges, pairs, out fault), fault.ToString());
			}
			ClassicAssert.AreEqual(4 * 21 * KingdomDistanceRules.EdgesPerZone, matrix.WorkEdgeEntries);
			ClassicAssert.AreEqual(4 * KingdomDistanceRules.PairSlots(21), matrix.SamePairEntries);
			ClassicAssert.LessOrEqual(matrix.WorkEdgeEntries, KingdomDistanceRules.MaxWorkEdgeEntries);
			ClassicAssert.LessOrEqual(matrix.SamePairEntries, KingdomDistanceRules.MaxSamePairEntries);

			int[] tooMany = new int[91];
			for (int i = 0; i < tooMany.Length; i++) tooMany[i] = i + 1;
			ClassicAssert.IsFalse(matrix.TryWriteZone(0, tooMany,
				new ushort[tooMany.Length * KingdomDistanceRules.EdgesPerZone],
				new ushort[KingdomDistanceRules.PairSlots(tooMany.Length)], out fault));
			ClassicAssert.AreEqual(KingdomCityFault.RowCapExceeded, fault);
		}

		/// <summary>A graph over more nodes than one whole parasang is refused rather than grown:
		/// §1.4's "no dimension of this model grows" applied to the routing table.</summary>
		[Test]
		public void TheGraphRefusesMoreThanOneParasang()
		{
			KingdomZoneNode[] nodes = new KingdomZoneNode[KingdomDistanceRules.MaxNodes + 1];
			for (int i = 0; i < nodes.Length; i++)
			{
				nodes[i] = Node("z" + i, i, 0, 10);
			}
			KingdomZoneGraph graph;
			KingdomCityFault fault;
			ClassicAssert.IsFalse(KingdomZoneGraph.TryBuild(nodes, nodes.Length, KingdomDistanceRules.ZoneTransitCells, out graph, out fault));
			ClassicAssert.AreEqual(KingdomCityFault.InvalidIndex, fault);
		}

		/// <summary>Level-2 values come from live passability, not coordinate distance. A wall
		/// forces the measured same-zone route around its real opening.</summary>
		[Test]
		public void ALevelTwoSliceMeasuresTheWalkAroundRealWalls()
		{
			const int Width = 5;
			const int Height = 5;
			bool[] passable = new bool[Width * Height];
			bool[] paved = new bool[passable.Length];
			for (int i = 0; i < passable.Length; i++) passable[i] = true;
			for (int y = 0; y < Height - 1; y++) passable[y * Width + 2] = false;
			KingdomDistancePoint[] points = new KingdomDistancePoint[2]
			{
				new KingdomDistancePoint(1, 0, 0), new KingdomDistancePoint(2, 4, 0)
			};
			ushort[] edges;
			ushort[] pairs;
			long operations;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomDistanceSliceRules.TryMeasure(passable, paved, Width, Height,
				points, points.Length, -1, -1, -1, -1,
				out edges, out pairs, out operations, out fault), fault.ToString());
			ClassicAssert.AreEqual(1, pairs.Length);
			ClassicAssert.Greater(pairs[0], 4, "Chebyshev would be four; real wall must make it longer");
			ClassicAssert.Greater(operations, 0L);
		}

		/// <summary>Road weight participates in the measured path. Four paved steps cost 2.4 cells
		/// and round up to three; same unpaved walk costs four.</summary>
		[Test]
		public void PavingShortensTheMeasuredSliceWithTheNamedSixtyPercent()
		{
			bool[] passable = new bool[5] { true, true, true, true, true };
			bool[] plain = new bool[5];
			bool[] paved = new bool[5] { true, true, true, true, true };
			KingdomDistancePoint[] points = new KingdomDistancePoint[2]
			{
				new KingdomDistancePoint(11, 0, 0), new KingdomDistancePoint(12, 4, 0)
			};
			ushort[] edgePlain;
			ushort[] pairPlain;
			ushort[] edgePaved;
			ushort[] pairPaved;
			long operations;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomDistanceSliceRules.TryMeasure(passable, plain, 5, 1,
				points, 2, -1, -1, -1, -1, out edgePlain, out pairPlain,
				out operations, out fault));
			ClassicAssert.IsTrue(KingdomDistanceSliceRules.TryMeasure(passable, paved, 5, 1,
				points, 2, -1, -1, -1, -1, out edgePaved, out pairPaved,
				out operations, out fault));
			ClassicAssert.AreEqual(4, pairPlain[0]);
			ClassicAssert.AreEqual(3, pairPaved[0]);
			ClassicAssert.Less(pairPaved[0], pairPlain[0]);
		}

		/// <summary>Vertical entries are exact shaft cells supplied by physical receipts. With no
		/// receipt, up/down remain NoRoute rather than using a zone centre proxy.</summary>
		[Test]
		public void VerticalEdgeUsesOnlyTheExactSuppliedShaftCell()
		{
			bool[] passable = new bool[15];
			bool[] paved = new bool[15];
			for (int i = 0; i < passable.Length; i++) passable[i] = true;
			KingdomDistancePoint[] points = new KingdomDistancePoint[1]
			{
				new KingdomDistancePoint(21, 0, 1)
			};
			ushort[] edges;
			long operations;
			KingdomCityFault fault;
			ClassicAssert.IsTrue(KingdomDistanceSliceRules.TryMeasureEdges(passable, paved, 5, 3,
				points, 1, 4, 1, -1, -1, out edges, out operations, out fault));
			ClassicAssert.AreEqual(4, edges[(int)KingdomZoneStep.Up]);
			ClassicAssert.AreEqual(KingdomDistanceRules.NoRoute, edges[(int)KingdomZoneStep.Down]);
		}

		[Test]
		public void RenderCandidateMeasurementIsBoundedBeforeItAllocates()
		{
			KingdomDistancePoint[] points = new KingdomDistancePoint[
				KingdomDistanceSliceRules.MaxCandidateEndpoints + 1];
			ushort[] edges;
			long operations;
			KingdomCityFault fault;
			ClassicAssert.IsFalse(KingdomDistanceSliceRules.TryMeasureEdges(new bool[1] { true },
				new bool[1], 1, 1, points, points.Length, -1, -1, -1, -1,
				out edges, out operations, out fault));
			ClassicAssert.AreEqual(KingdomCityFault.InvalidIndex, fault);
		}
	}
}
#endif
