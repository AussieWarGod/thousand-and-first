#if TAF_TESTS
using System;
using NUnit.Framework;
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
			Assert.AreEqual(KingdomZoneStep.North, KingdomDistanceRules.StepBetween(here, Node("b", 5, 4, 10)));
			Assert.AreEqual(KingdomZoneStep.South, KingdomDistanceRules.StepBetween(here, Node("b", 5, 6, 10)));
			Assert.AreEqual(KingdomZoneStep.East, KingdomDistanceRules.StepBetween(here, Node("b", 6, 5, 10)));
			Assert.AreEqual(KingdomZoneStep.West, KingdomDistanceRules.StepBetween(here, Node("b", 4, 5, 10)));
			Assert.AreEqual(KingdomZoneStep.None, KingdomDistanceRules.StepBetween(here, Node("b", 6, 6, 10)),
				"a corner is not an edge a carrier can walk through");
			Assert.AreEqual(KingdomZoneStep.None, KingdomDistanceRules.StepBetween(here, Node("b", 7, 5, 10)));
			Assert.AreEqual(KingdomZoneStep.None, KingdomDistanceRules.StepBetween(here, here));
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
			Assert.AreEqual(KingdomZoneStep.Down, KingdomDistanceRules.StepBetween(here, Node("b", 5, 5, 11)));
			Assert.AreEqual(KingdomZoneStep.Up, KingdomDistanceRules.StepBetween(here, Node("b", 5, 5, 9)));
			Assert.AreEqual(KingdomZoneStep.None, KingdomDistanceRules.StepBetween(here, Node("b", 5, 5, 12)));
			Assert.AreEqual(KingdomZoneStep.None, KingdomDistanceRules.StepBetween(here, Node("b", 6, 5, 11)),
				"a stairwell goes straight up, never up and across");

			// The step is named and the rock is still shut.
			Assert.IsFalse(KingdomDistanceRules.Adjacent(here, Node("b", 5, 5, 11)),
				"unbroken rock is not a doorway because the coordinates differ by one");
			Assert.IsFalse(KingdomDistanceRules.Adjacent(Node("b", 5, 5, 11), here),
				"and it is shut from underneath too");

			KingdomZoneNode cut = Shafted("a", 5, 5, 10);
			Assert.IsTrue(KingdomDistanceRules.Adjacent(cut, Node("b", 5, 5, 11)));
			Assert.IsTrue(KingdomDistanceRules.Adjacent(Node("b", 5, 5, 11), cut),
				"a shaft is walked both ways, so the edge is symmetric whichever end asks");
		}

		/// <summary>The flag is read off the SHALLOWER node, because that is the ground the winding
		/// gear stands on. A shaft claimed by the zone underneath opens nothing.</summary>
		[Test]
		public void TheShaftIsReadOffTheGroundTheWindingGearStandsOn()
		{
			Assert.IsFalse(KingdomDistanceRules.Adjacent(Node("a", 5, 5, 10), Shafted("b", 5, 5, 11)));
		}

		/// <summary>A shaft's foot must be rock. A stair up the inside of a tower is a building in
		/// a set nobody has written, and it does not arrive through this door.</summary>
		[Test]
		public void NothingAboveTheSurfaceIsJoinedByAShaft()
		{
			Assert.IsFalse(KingdomDistanceRules.Adjacent(Shafted("a", 5, 5, 9), Node("b", 5, 5, 10)));
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
			Assert.IsTrue(KingdomZoneGraph.TryBuild(nodes, 2, KingdomDistanceRules.ZoneTransitCells, out graph, out fault));
			int cells;
			Assert.IsFalse(graph.TryDistance(0, 1, out cells));
			int[] path = new int[KingdomDistanceRules.MaxNodes];
			int length;
			Assert.IsFalse(graph.TryPath(0, 1, path, out length, out fault));
		}

		/// <summary>A cut shaft is three ordinary hops: the whole depth of a stratum, climbed, with
		/// the load on your back. The catalogue promises the asymmetry out loud.</summary>
		[Test]
		public void ACutShaftCostsExactlyThreeOrdinaryHops()
		{
			KingdomZoneNode[] nodes = new KingdomZoneNode[2] { Shafted("surface", 5, 5, 10), Node("deep", 5, 5, 11) };
			KingdomZoneGraph graph;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomZoneGraph.TryBuild(nodes, 2, KingdomDistanceRules.ZoneTransitCells, out graph, out fault));
			int down;
			int up;
			Assert.IsTrue(graph.TryDistance(0, 1, out down));
			Assert.IsTrue(graph.TryDistance(1, 0, out up));
			Assert.AreEqual(3 * KingdomDistanceRules.ZoneTransitCells, down);
			Assert.AreEqual(down, up, "the climb costs the same whichever way the load is going");
			Assert.AreEqual(KingdomDelveRules.ShaftHopCells(KingdomDistanceRules.ZoneTransitCells), down);
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
			Assert.IsTrue(KingdomZoneGraph.TryBuild(flat, 9, KingdomDistanceRules.ZoneTransitCells, out plain, out fault));
			Assert.IsTrue(KingdomZoneGraph.TryBuild(delved, 9, KingdomDistanceRules.ZoneTransitCells, out opened, out fault));
			for (int i = 0; i < 8; i++)
			{
				for (int j = 0; j < 8; j++)
				{
					int was;
					int now;
					bool hadRoute = plain.TryDistance(i, j, out was);
					bool hasRoute = opened.TryDistance(i, j, out now);
					Assert.AreEqual(hadRoute, hasRoute, "surface route " + i + "->" + j + " changed existence");
					if (hadRoute)
					{
						Assert.AreEqual(was, now, "surface distance " + i + "->" + j + " changed length");
					}
				}
			}
			int descent;
			Assert.IsTrue(opened.TryDistance(0, 8, out descent));
			Assert.AreEqual(3 * KingdomDistanceRules.ZoneTransitCells, descent);
		}

		/// <summary>
		/// §3.10(3): a paved leg costs 0.6 of the same distance unpaved. The constant is named once
		/// and lives with the itinerary rules, because the estimate and the measured length must
		/// scale by the same number or a road makes the two disagree.
		/// </summary>
		[Test]
		public void TheRoadsDiscountIsANamedSixtyPercent()
		{
			Assert.AreEqual(60, KingdomItineraryRules.RoadDiscountPercent);
			Assert.AreEqual(100, KingdomItineraryRules.NoRoadDiscountPercent);
			int paved;
			int plain;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomDistanceRules.TryDiscount(100, KingdomItineraryRules.RoadDiscountPercent, out paved, out fault));
			Assert.IsTrue(KingdomDistanceRules.TryDiscount(100, KingdomItineraryRules.NoRoadDiscountPercent, out plain, out fault));
			Assert.AreEqual(60, paved);
			Assert.AreEqual(100, plain);
			Assert.IsTrue(paved < plain, "laying a road must visibly shorten every itinerary that uses it");
		}

		/// <summary>A road makes a journey shorter and never instantaneous, and zero stays
		/// zero.</summary>
		[Test]
		public void TheDiscountNeverReachesZeroForARealDistance()
		{
			int cells;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomDistanceRules.TryDiscount(1, 60, out cells, out fault));
			Assert.AreEqual(1, cells);
			Assert.IsTrue(KingdomDistanceRules.TryDiscount(0, 60, out cells, out fault));
			Assert.AreEqual(0, cells);
			Assert.IsFalse(KingdomDistanceRules.TryDiscount(-1, 60, out cells, out fault));
			Assert.IsFalse(KingdomDistanceRules.TryDiscount(10, 0, out cells, out fault));
			Assert.IsFalse(KingdomDistanceRules.TryDiscount(10, 101, out cells, out fault),
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
					Assert.IsTrue(KingdomDistanceRules.TryPairIndex(a, b, Works, out forward, out fault));
					Assert.IsTrue(KingdomDistanceRules.TryPairIndex(b, a, Works, out backward, out fault));
					Assert.AreEqual(forward, backward);
					Assert.IsTrue(forward >= 0 && forward < seen.Length);
					Assert.IsFalse(seen[forward], "two pairs claimed the same slot");
					seen[forward] = true;
				}
			}
			for (int i = 0; i < seen.Length; i++)
			{
				Assert.IsTrue(seen[i], "a slot nobody indexes is a slot that should not have been allocated");
			}
			int index;
			Assert.IsFalse(KingdomDistanceRules.TryPairIndex(3, 3, Works, out index, out fault),
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
			Assert.IsTrue(KingdomZoneGraph.TryBuild(nodes, 9, KingdomDistanceRules.ZoneTransitCells, out graph, out fault));
			Assert.AreEqual(729L, graph.Operations);
			Assert.AreEqual(9, graph.Count);
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
			Assert.IsTrue(KingdomZoneGraph.TryBuild(nodes, 9, KingdomDistanceRules.ZoneTransitCells, out graph, out fault));
			int cells;
			Assert.IsTrue(graph.TryDistance(0, 8, out cells));
			Assert.AreEqual(4 * KingdomDistanceRules.ZoneTransitCells, cells);
			int[] path = new int[KingdomDistanceRules.MaxNodes];
			int length;
			Assert.IsTrue(graph.TryPath(0, 8, path, out length, out fault));
			Assert.AreEqual(5, length, "four hops is five nodes, both ends included");
			Assert.AreEqual(0, path[0]);
			Assert.AreEqual(8, path[length - 1]);
			for (int i = 1; i < length; i++)
			{
				KingdomZoneStep step;
				Assert.IsTrue(graph.TryStep(path[i - 1], path[i], out step), "every hop of a path is a real edge");
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
			Assert.IsTrue(KingdomDistanceRules.TryDiscount(KingdomDistanceRules.ZoneTransitCells, KingdomItineraryRules.RoadDiscountPercent, out paved, out fault));
			KingdomZoneGraph plain;
			KingdomZoneGraph roaded;
			Assert.IsTrue(KingdomZoneGraph.TryBuild(nodes, 3, KingdomDistanceRules.ZoneTransitCells, out plain, out fault));
			Assert.IsTrue(KingdomZoneGraph.TryBuild(nodes, 3, paved, out roaded, out fault));
			int longWay;
			int shortWay;
			Assert.IsTrue(plain.TryDistance(0, 2, out longWay));
			Assert.IsTrue(roaded.TryDistance(0, 2, out shortWay));
			Assert.IsTrue(shortWay < longWay);
			Assert.AreEqual(2 * paved, shortWay);
		}

		/// <summary>A zone with no edge to the rest is unreachable rather than very far away: a
		/// route the planner cannot make is refused, never estimated.</summary>
		[Test]
		public void AnIslandZoneHasNoRoute()
		{
			KingdomZoneNode[] nodes = new KingdomZoneNode[2] { Node("a", 0, 0, 10), Node("b", 40, 40, 10) };
			KingdomZoneGraph graph;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomZoneGraph.TryBuild(nodes, 2, KingdomDistanceRules.ZoneTransitCells, out graph, out fault));
			int cells;
			Assert.IsFalse(graph.TryDistance(0, 1, out cells));
			int[] path = new int[KingdomDistanceRules.MaxNodes];
			int length;
			Assert.IsFalse(graph.TryPath(0, 1, path, out length, out fault));
			Assert.AreEqual(KingdomCityFault.OutsideItinerary, fault);
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
			Assert.IsTrue(KingdomZoneGraph.TryBuild(nodes, 2, KingdomDistanceRules.ZoneTransitCells, out graph, out fault));
			Assert.IsTrue(KingdomDistanceMatrix.TryCreate(graph, 4, out matrix, out fault));
			Assert.IsTrue(matrix.IsDirty(0), "a new slice is dirty until the ground has been read");

			ushort[] edges = new ushort[4 * KingdomDistanceRules.EdgesPerZone];
			ushort[] pairs = new ushort[KingdomDistanceRules.PairSlots(4)];
			for (int i = 0; i < edges.Length; i++) { edges[i] = 7; }
			for (int i = 0; i < pairs.Length; i++) { pairs[i] = 3; }
			Assert.IsTrue(matrix.TryWriteZone(0, edges, pairs, out fault));
			Assert.IsTrue(matrix.TryWriteZone(1, edges, pairs, out fault));
			Assert.IsFalse(matrix.IsDirty(0));

			int cells;
			Assert.IsTrue(matrix.TryCompose(0, 0, 1, 2, out cells, out fault));
			Assert.AreEqual(7 + KingdomDistanceRules.ZoneTransitCells + 7, cells);

			Assert.IsTrue(matrix.TryCompose(0, 0, 0, 1, out cells, out fault));
			Assert.AreEqual(3, cells, "a same-zone pair is read straight out of the triangular slice");
			Assert.IsTrue(matrix.TryCompose(0, 2, 0, 2, out cells, out fault));
			Assert.AreEqual(0, cells, "a work stands no distance from itself");
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
			Assert.IsTrue(KingdomZoneGraph.TryBuild(nodes, 2, KingdomDistanceRules.ZoneTransitCells, out graph, out fault));
			Assert.IsTrue(KingdomDistanceMatrix.TryCreate(graph, 2, out matrix, out fault));
			ushort[] edges = new ushort[2 * KingdomDistanceRules.EdgesPerZone];
			ushort[] pairs = new ushort[KingdomDistanceRules.PairSlots(2)];
			for (int i = 0; i < edges.Length; i++) { edges[i] = 5; }
			for (int i = 0; i < pairs.Length; i++) { pairs[i] = 5; }
			Assert.IsTrue(matrix.TryWriteZone(0, edges, pairs, out fault));
			Assert.IsTrue(matrix.TryWriteZone(1, edges, pairs, out fault));
			int cells;
			Assert.IsTrue(matrix.TryCompose(0, 0, 1, 1, out cells, out fault));

			matrix.MarkDirty("b");
			Assert.IsTrue(matrix.IsDirty(1));
			Assert.IsFalse(matrix.TryCompose(0, 0, 1, 1, out cells, out fault),
				"a work placed or a road laid makes the slice unbelievable until it is read again");
			Assert.IsTrue(matrix.TryWriteZone(1, edges, pairs, out fault));
			Assert.IsTrue(matrix.TryCompose(0, 0, 1, 1, out cells, out fault));
		}

		/// <summary>
		/// §0.0(c)'s memory row is the contract. A matrix that allocated past the entry budget would
		/// be the regression the table exists to catch, so it refuses instead.
		/// </summary>
		[Test]
		public void TheMatrixRefusesToAllocatePastItsOwnBudget()
		{
			KingdomZoneNode[] nodes = new KingdomZoneNode[9];
			for (int i = 0; i < 9; i++)
			{
				nodes[i] = Node("z" + i, i % 3, i / 3, 10);
			}
			KingdomZoneGraph graph;
			KingdomDistanceMatrix matrix;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomZoneGraph.TryBuild(nodes, 9, KingdomDistanceRules.ZoneTransitCells, out graph, out fault));
			Assert.IsTrue(KingdomDistanceMatrix.TryCreate(graph, 10, out matrix, out fault));
			Assert.IsTrue(matrix.Entries <= KingdomDistanceRules.MaxWorkEdgeEntries + KingdomDistanceRules.MaxSamePairEntries
				+ KingdomDistanceRules.MaxNodes * KingdomDistanceRules.MaxNodes);
			Assert.IsFalse(KingdomDistanceMatrix.TryCreate(graph, 1000, out matrix, out fault));
			Assert.AreEqual(KingdomCityFault.RowCapExceeded, fault);
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
			Assert.IsFalse(KingdomZoneGraph.TryBuild(nodes, nodes.Length, KingdomDistanceRules.ZoneTransitCells, out graph, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidIndex, fault);
		}
	}
}
#endif
