#if TAF_TESTS
using NUnit.Framework;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Tests
{
	/// <summary>
	/// W7's graph rows, the declared-topology law, and the traversal the flow solve runs on.
	/// LIVING-CITY-ARCHITECTURE &sect;3.11; BUILDING-CATALOGUE-BRIEF Addendum 12(g) and the LIQUID
	/// LAW.
	/// </summary>
	public class KingdomNetworkRulesTests
	{
		private static KingdomNetworkNode Source(int id, int rate)
		{
			return new KingdomNetworkNode(id, KingdomNetworkRole.Source, KingdomWorkTier.Water, 0, rate);
		}

		private static KingdomNetworkNode Sink(int id, int rate, KingdomWorkTier tier)
		{
			return new KingdomNetworkNode(id, KingdomNetworkRole.Sink, tier, 0, rate);
		}

		private static KingdomNetworkNode Store(int id, int capacity, int rate)
		{
			return new KingdomNetworkNode(id, KingdomNetworkRole.Store, KingdomWorkTier.Water, capacity, rate);
		}

		private static KingdomNetworkGraph Build(KingdomNetworkNode[] nodes, KingdomNetworkEdge[] edges)
		{
			KingdomNetworkGraph graph;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomNetworkGraph.TryBuild(1, KingdomNetworkKind.Electrical, null, 7L,
				nodes, nodes.Length, edges, edges.Length, out graph, out fault), fault.ToString());
			return graph;
		}

		private static int[] Bottleneck(KingdomNetworkGraph graph, out int visits)
		{
			int[] reach = new int[graph.NodeCount];
			KingdomCityFault fault;
			Assert.IsTrue(graph.TryBottleneck(reach, out visits, out fault), fault.ToString());
			return reach;
		}

		// ---- The LIQUID LAW: declared, never inferred -----------------------------------------

		/// <summary>
		/// The law's first clause. Two lines carrying different liquids do not merge, do not
		/// average, and do not pick a winner — they REFUSE, and the refusal names both liquids so
		/// the founder knows which two mains would not meet.
		/// </summary>
		[Test]
		public void ACrossLiquidJoinRefusesAndNamesBothLiquids()
		{
			KingdomJoinVerdict verdict = KingdomNetworkRules.JudgeJoin(true,
				KingdomNetworkKind.Liquid, "water", KingdomNetworkKind.Liquid, "salt");
			Assert.AreEqual(KingdomJoinVerdict.RefusedLiquid, verdict);
			string line = KingdomNetworkRules.RefusalLine(verdict, "water", "salt");
			StringAssert.Contains("water", line);
			StringAssert.Contains("salt", line);
			Assert.IsFalse(line.Contains("merge"), "a refusal must not describe the thing it refused to do as having happened");
		}

		/// <summary>Same liquid, both declaring: one line. Case and stray whitespace are the
		/// founder's XML, not a different liquid.</summary>
		[TestCase("water", "water")]
		[TestCase("Water", "water")]
		[TestCase(" water ", "water")]
		public void ASameLiquidDeclaredJoinIsOneLine(string mine, string theirs)
		{
			Assert.AreEqual(KingdomJoinVerdict.Joined,
				KingdomNetworkRules.JudgeJoin(true, KingdomNetworkKind.Liquid, mine, KingdomNetworkKind.Liquid, theirs));
		}

		/// <summary>
		/// The law's second clause, and the crossover's whole reason to exist. Two lines that only
		/// share ground CROSS: not a join, and not a refusal either, because nobody asked for one.
		/// A refusal here would fire on every tile two mains ran past each other and 7b's
		/// announce-once would become announce-constantly.
		/// </summary>
		[Test]
		public void TwoLinesThatNeverDeclaredMerelyCrossAndSayNothing()
		{
			KingdomJoinVerdict verdict = KingdomNetworkRules.JudgeJoin(false,
				KingdomNetworkKind.Liquid, "water", KingdomNetworkKind.Liquid, "salt");
			Assert.AreEqual(KingdomJoinVerdict.Crossed, verdict);
			Assert.AreEqual("", KingdomNetworkRules.RefusalLine(verdict, "water", "salt"));
		}

		/// <summary>An untyped line joins nothing at all. Two blanks are not an agreement:
		/// <i>declared, never inferred</i> means a blank declaration is not a declaration.</summary>
		[Test]
		public void AnUntypedLineJoinsNothingIncludingAnotherUntypedLine()
		{
			Assert.AreEqual(KingdomJoinVerdict.RefusedUntyped,
				KingdomNetworkRules.JudgeJoin(true, KingdomNetworkKind.Liquid, "", KingdomNetworkKind.Liquid, ""));
			Assert.IsFalse(KingdomNetworkRules.LiquidsMatch("", ""));
			Assert.IsFalse(KingdomNetworkRules.LiquidsMatch(null, "water"));
		}

		/// <summary>Two families never join, which is vanilla's own rule
		/// (<c>IPowerTransmission.GetCorrespondingPart</c> matches on the type string).</summary>
		[Test]
		public void TwoFamiliesNeverJoinEvenWhenBothDeclare()
		{
			Assert.AreEqual(KingdomJoinVerdict.RefusedKind,
				KingdomNetworkRules.JudgeJoin(true, KingdomNetworkKind.Mechanical, null, KingdomNetworkKind.Electrical, null));
		}

		/// <summary>A join needs BOTH declarations. One segment offering is an offer, not a
		/// connection — which is the entire difference between declared and inferred.</summary>
		[Test]
		public void OneSidedDeclarationJoinsNothing()
		{
			Assert.IsTrue(KingdomNetworkRules.DeclaredToward(KingdomNetworkRules.JoinNorth, KingdomNetworkRules.JoinSouth, KingdomNetworkRules.JoinNorth));
			Assert.IsFalse(KingdomNetworkRules.DeclaredToward(KingdomNetworkRules.JoinNorth, KingdomNetworkRules.JoinEast, KingdomNetworkRules.JoinNorth));
			Assert.IsFalse(KingdomNetworkRules.DeclaredToward(KingdomNetworkRules.JoinEast, KingdomNetworkRules.JoinSouth, KingdomNetworkRules.JoinNorth));
		}

		/// <summary>A misspelt declaration joins NOTHING rather than everything. The dangerous
		/// default is the permissive one: a silent merge is the single thing the law forbids
		/// outright, so an unreadable value fails closed.</summary>
		[TestCase("NS", true, KingdomNetworkRules.JoinNorth | KingdomNetworkRules.JoinSouth)]
		[TestCase("nsew", true, KingdomNetworkRules.JoinAll)]
		[TestCase("", true, 0)]
		[TestCase("NX", false, 0)]
		[TestCase("north", false, 0)]
		public void AMisspeltDeclarationCapsTheSegmentRatherThanOpeningIt(string text, bool ok, int expected)
		{
			int mask;
			Assert.AreEqual(ok, KingdomNetworkRules.TryParseJoins(text, out mask));
			Assert.AreEqual(expected, mask);
		}

		/// <summary>A crossover carries a run straight through and pairs nothing else. North in,
		/// south out; and never north in, east out — which is what stops it being a tee.</summary>
		[Test]
		public void ACrossoverCarriesThroughAndNeverAround()
		{
			int cross = KingdomNetworkRules.JoinAll;
			Assert.AreEqual(KingdomNetworkRules.JoinSouth, KingdomNetworkRules.CrossoverExit(cross, KingdomNetworkRules.JoinNorth));
			Assert.AreEqual(KingdomNetworkRules.JoinWest, KingdomNetworkRules.CrossoverExit(cross, KingdomNetworkRules.JoinEast));
			int halfLaid = KingdomNetworkRules.JoinNorth | KingdomNetworkRules.JoinEast;
			Assert.AreEqual(0, KingdomNetworkRules.CrossoverExit(halfLaid, KingdomNetworkRules.JoinNorth),
				"half a crossover is a dead end, not a corner");
		}

		// ---- The graph rows -------------------------------------------------------------------

		/// <summary>A liquid network must say what it carries and no other kind may claim to. The
		/// check is at BUILD, because a network that could not name its liquid could never refuse a
		/// join by name.</summary>
		[Test]
		public void ALiquidNetworkWithoutALiquidIsRefusedAndSoIsTheReverse()
		{
			KingdomNetworkNode[] nodes = new KingdomNetworkNode[1] { Source(1, 10) };
			KingdomNetworkEdge[] edges = new KingdomNetworkEdge[0];
			KingdomNetworkGraph graph;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomNetworkGraph.TryBuild(1, KingdomNetworkKind.Liquid, null, 1L, nodes, 1, edges, 0, out graph, out fault));
			Assert.AreEqual(KingdomCityFault.NullArgument, fault);
			Assert.IsFalse(KingdomNetworkGraph.TryBuild(1, KingdomNetworkKind.Electrical, "water", 1L, nodes, 1, edges, 0, out graph, out fault));
			Assert.AreEqual(KingdomCityFault.NullArgument, fault);
		}

		/// <summary>An edge naming a node that is not there is a refusal, never a dropped edge.
		/// Quietly discarding it would publish a graph whose topology is not the one the founder
		/// laid.</summary>
		[Test]
		public void AnEdgeNamingAMissingNodeRefusesTheWholeGraph()
		{
			KingdomNetworkNode[] nodes = new KingdomNetworkNode[2] { Source(1, 10), Sink(2, 10, KingdomWorkTier.Industry) };
			KingdomNetworkEdge[] edges = new KingdomNetworkEdge[1] { new KingdomNetworkEdge(0, 5, 100, 100) };
			KingdomNetworkGraph graph;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomNetworkGraph.TryBuild(1, KingdomNetworkKind.Electrical, null, 1L, nodes, 2, edges, 1, out graph, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidIndex, fault);
		}

		/// <summary>Over the caps is a refusal, not a truncation. §0.0(c) budgets 32 nodes and 48
		/// edges of RAM; a graph that quietly kept the first 32 would be a network the founder
		/// cannot see the rest of.</summary>
		[Test]
		public void OverTheCapIsRefusedRatherThanTruncated()
		{
			KingdomNetworkNode[] nodes = new KingdomNetworkNode[KingdomNetworkRules.MaxNodes + 1];
			for (int i = 0; i < nodes.Length; i++)
			{
				nodes[i] = Source(i + 1, 1);
			}
			KingdomNetworkGraph graph;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomNetworkGraph.TryBuild(1, KingdomNetworkKind.Electrical, null, 1L,
				nodes, nodes.Length, new KingdomNetworkEdge[0], 0, out graph, out fault));
			Assert.AreEqual(KingdomCityFault.RowCapExceeded, fault);
		}

		// ---- The traversal and its op bound ---------------------------------------------------

		/// <summary>
		/// The bottleneck relaxation: what reaches a node is the narrowest segment on the way to
		/// it. Conservative by construction — it can understate throughput and never overstate it,
		/// so it cannot manufacture supply, which is the right direction for an error to point.
		/// Vanilla reasons the same way, reducing <c>GridCapacity</c> to the weakest link on the
		/// grid.
		/// </summary>
		[Test]
		public void TheNarrowestSegmentIsWhatReachesTheFarEnd()
		{
			KingdomNetworkNode[] nodes = new KingdomNetworkNode[3]
			{
				Source(1, 1000),
				Sink(2, 1000, KingdomWorkTier.Industry),
				Sink(3, 1000, KingdomWorkTier.Industry)
			};
			KingdomNetworkEdge[] edges = new KingdomNetworkEdge[2]
			{
				new KingdomNetworkEdge(0, 1, 400, 100),
				new KingdomNetworkEdge(1, 2, 900, 100)
			};
			int visits;
			int[] reach = Bottleneck(Build(nodes, edges), out visits);
			Assert.AreEqual(KingdomNetworkRules.Unlimited, reach[0], "a source is its own bottleneck");
			Assert.AreEqual(400, reach[1]);
			Assert.AreEqual(400, reach[2], "the far end is narrowed by the first segment, not by its own");
		}

		/// <summary>Addendum 10(b): a cracked main carries less rather than the same. Condition
		/// scales the segment, and a segment at nothing carries nothing.</summary>
		[TestCase(100, 400)]
		[TestCase(50, 200)]
		[TestCase(0, 0)]
		public void WearNarrowsASegmentInProportion(int condition, int expected)
		{
			KingdomNetworkNode[] nodes = new KingdomNetworkNode[2] { Source(1, 1000), Sink(2, 1000, KingdomWorkTier.Industry) };
			KingdomNetworkEdge[] edges = new KingdomNetworkEdge[1] { new KingdomNetworkEdge(0, 1, 400, condition) };
			int visits;
			Assert.AreEqual(expected, Bottleneck(Build(nodes, edges), out visits)[1]);
		}

		/// <summary>A node nothing reaches gets nothing, and says so with a zero rather than with a
		/// fault: laying a length of main that does not come back to a source is a legal, ordinary,
		/// entirely silent thing to have done.</summary>
		[Test]
		public void AnUnreachedNodeGetsNothingAndIsNotAFault()
		{
			KingdomNetworkNode[] nodes = new KingdomNetworkNode[3]
			{
				Source(1, 1000),
				Sink(2, 1000, KingdomWorkTier.Industry),
				Sink(3, 1000, KingdomWorkTier.Industry)
			};
			KingdomNetworkEdge[] edges = new KingdomNetworkEdge[1] { new KingdomNetworkEdge(0, 1, 400, 100) };
			KingdomNetworkGraph graph = Build(nodes, edges);
			int visits;
			int[] reach = Bottleneck(graph, out visits);
			Assert.AreEqual(0, reach[2]);
			Assert.AreEqual(2, graph.ReachedCount);
		}

		/// <summary>
		/// A store feeds the line exactly as a wheel does, because the night is when there is no
		/// source at all — which is the whole point of a bed of molten salt. A network of a store
		/// and a post reaches the post.
		/// </summary>
		[Test]
		public void AStoreReachesTheSinksWithNoSourceOnTheLine()
		{
			KingdomNetworkNode[] nodes = new KingdomNetworkNode[2] { Store(1, 24000, 12000), Sink(2, 4000, KingdomWorkTier.Industry) };
			KingdomNetworkEdge[] edges = new KingdomNetworkEdge[1] { new KingdomNetworkEdge(0, 1, 5000, 100) };
			int visits;
			Assert.AreEqual(5000, Bottleneck(Build(nodes, edges), out visits)[1]);
		}

		/// <summary>
		/// A store is the root of LAST RESORT, and only one of them.
		/// <para>
		/// Rooting every store would make a line whose ends are all vessels — a water main between
		/// two cisterns — a forest of roots with no edge ever walked, and its throughput would then
		/// read as unlimited when it is in fact the narrowest length of pipe on it. One root,
		/// lowest index, and the rest are reached through the segments that actually join them.
		/// </para>
		/// </summary>
		[Test]
		public void AChainOfStoresIsNarrowedByItsOwnPipeAndNotReadAsUnlimited()
		{
			KingdomNetworkNode[] nodes = new KingdomNetworkNode[3] { Store(1, 1000, 50), Store(2, 1000, 50), Store(3, 1000, 50) };
			KingdomNetworkEdge[] edges = new KingdomNetworkEdge[2]
			{
				new KingdomNetworkEdge(0, 1, 90, 100),
				new KingdomNetworkEdge(1, 2, 40, 100)
			};
			int visits;
			int[] reach = Bottleneck(Build(nodes, edges), out visits);
			Assert.AreEqual(KingdomNetworkRules.Unlimited, reach[0], "the lowest store is the one root");
			Assert.AreEqual(90, reach[1]);
			Assert.AreEqual(40, reach[2], "the far cistern is narrowed by the narrowest length on the way to it");
		}

		/// <summary>With a source on the line the stores are NOT roots — they are reached through
		/// the segments that feed them, so a store behind a cracked main is fed through the crack
		/// like everything else.</summary>
		[Test]
		public void WithASourceOnTheLineAStoreIsFedThroughItsOwnSegment()
		{
			KingdomNetworkNode[] nodes = new KingdomNetworkNode[2] { Source(1, 1000), Store(2, 1000, 500) };
			KingdomNetworkEdge[] edges = new KingdomNetworkEdge[1] { new KingdomNetworkEdge(0, 1, 300, 50) };
			int visits;
			int[] reach = Bottleneck(Build(nodes, edges), out visits);
			Assert.AreEqual(KingdomNetworkRules.Unlimited, reach[0]);
			Assert.AreEqual(150, reach[1], "the store is behind a half-wrecked main and must be fed through it");
		}

		/// <summary>
		/// &sect;0.0's network lane, asserted rather than asserted-about: one solve costs at most
		/// <c>nodes + edges</c> node-visits, at the caps 32 + 48 = 80. This is what pays for the
		/// stored traversal order — a walk that had to find neighbours by scanning the edge array
		/// would be <c>nodes &times; edges</c>, nineteen times the ceiling.
		/// </summary>
		[Test]
		public void OneSolveNeverExceedsNodesPlusEdges()
		{
			KingdomNetworkNode[] nodes = new KingdomNetworkNode[KingdomNetworkRules.MaxNodes];
			nodes[0] = Source(1, 1000);
			for (int i = 1; i < nodes.Length; i++)
			{
				nodes[i] = Sink(i + 1, 100, KingdomWorkTier.Industry);
			}
			KingdomNetworkEdge[] edges = new KingdomNetworkEdge[KingdomNetworkRules.MaxEdges];
			int laid = 0;
			for (int i = 1; i < nodes.Length; i++)
			{
				edges[laid++] = new KingdomNetworkEdge(i - 1, i, 1000, 100);
			}
			// The spare edges close the chain into cycles, which is the case a naive walk would
			// spin on and the case vanilla needs its GridMask guard for.
			for (int i = 0; laid < edges.Length; i++)
			{
				edges[laid++] = new KingdomNetworkEdge(i, (i + 2) % nodes.Length, 1000, 100);
			}
			KingdomNetworkGraph graph;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomNetworkGraph.TryBuild(1, KingdomNetworkKind.Electrical, null, 1L,
				nodes, nodes.Length, edges, edges.Length, out graph, out fault), fault.ToString());
			int visits;
			Bottleneck(graph, out visits);
			Assert.LessOrEqual(visits, KingdomNetworkRules.MaxSolveVisits(graph.NodeCount, graph.EdgeCount),
				"the solve broke §0.0's network lane");
			Assert.LessOrEqual(visits, 80, "the caps compose to eighty node-visits and the solve must stay inside them");
			Assert.AreEqual(KingdomBudgetVerdict.Within,
				KingdomBudgetRules.JudgeCount(KingdomBudgetLane.NetworkSolve, (long)visits * KingdomBudgetRules.MaxBreakpoints));
		}

		/// <summary>A cyclic network terminates, which is the property vanilla buys with its
		/// <c>GridMask</c>/<c>GridBit</c> guard and we buy by settling each node once.</summary>
		[Test]
		public void ACyclicNetworkSettlesEveryNodeExactlyOnce()
		{
			KingdomNetworkNode[] nodes = new KingdomNetworkNode[3]
			{
				Source(1, 100),
				Sink(2, 100, KingdomWorkTier.Industry),
				Sink(3, 100, KingdomWorkTier.Industry)
			};
			KingdomNetworkEdge[] edges = new KingdomNetworkEdge[3]
			{
				new KingdomNetworkEdge(0, 1, 100, 100),
				new KingdomNetworkEdge(1, 2, 100, 100),
				new KingdomNetworkEdge(2, 0, 100, 100)
			};
			KingdomNetworkGraph graph = Build(nodes, edges);
			int visits;
			Bottleneck(graph, out visits);
			Assert.AreEqual(3, graph.ReachedCount);
			Assert.LessOrEqual(visits, KingdomNetworkRules.MaxSolveVisits(3, 3));
		}

		// ---- Topology invalidates only on placement -------------------------------------------

		/// <summary>
		/// The distance-matrix discipline, one lane over: a graph is stale when and only when the
		/// GROUND stamp has moved. Not on time, not on stock, not on a pass — the three things
		/// §3.11 and <c>KingdomDistanceMatrix.MarkDirty</c> both refuse.
		/// </summary>
		[Test]
		public void AGraphIsStaleOnlyWhenTheGroundStampMoved()
		{
			KingdomNetworkGraph graph = Build(new KingdomNetworkNode[1] { Source(1, 10) }, new KingdomNetworkEdge[0]);
			Assert.IsFalse(KingdomNetworkRules.NeedsRebuild(graph, 7L), "the stamp it was built at is not a reason to rebuild");
			Assert.IsTrue(KingdomNetworkRules.NeedsRebuild(graph, 8L));
			Assert.IsTrue(KingdomNetworkRules.NeedsRebuild(null, 7L), "no graph and a stale graph are one branch");
		}

		// ---- The row widths §0.0(c) budgets ---------------------------------------------------

		/// <summary>
		/// The formula is the contract. Adding a field to a node or an edge must break here, not in
		/// a playtest six months later — the same falsifiability every other row in §0.0(c) has.
		/// </summary>
		[Test]
		public void TheNodeAndEdgeRowsFitTheWidthsTheTableBudgets()
		{
			int nodeBytes;
			Assert.IsTrue(KingdomCityMemoryRules.TryMeasureDeclaredRowBytes(typeof(KingdomNetworkNode), out nodeBytes));
			Assert.LessOrEqual(nodeBytes, KingdomCityMemoryRules.NetworkNodeBytes, "the node row outgrew §0.0(c)'s sixteen bytes");
			int edgeBytes;
			Assert.IsTrue(KingdomCityMemoryRules.TryMeasureDeclaredRowBytes(typeof(KingdomNetworkEdge), out edgeBytes));
			Assert.LessOrEqual(edgeBytes, KingdomCityMemoryRules.NetworkEdgeBytes, "the edge row outgrew §0.0(c)'s sixteen bytes");
			Assert.AreEqual(16, edgeBytes, "the edge row is budgeted at exactly its declared width");
		}

		/// <summary>
		/// The arcology decision, asserted as a NEGATIVE fact so it cannot be undone by accident:
		/// an edge names its endpoints and nothing about who provided it. A megastructure whose
		/// risers are the network declares edges between its floors' nodes with no schema change.
		/// Nothing here builds hosted plots; this only pins that the row does not preclude them.
		/// </summary>
		[Test]
		public void AnEdgeNamesNoProviderSoAShellCouldBeOne()
		{
			System.Reflection.FieldInfo[] fields = typeof(KingdomNetworkEdge)
				.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
			Assert.AreEqual(4, fields.Length);
			foreach (System.Reflection.FieldInfo field in fields)
			{
				Assert.IsTrue(field.FieldType.IsPrimitive,
					"an edge that carried a reference would be an edge that names its provider, and the shell-as-backbone would need a second edge kind");
			}
		}

		// ---- A transfer is a carry ------------------------------------------------------------

		private static KingdomCityState TwoZones(long levelA, long capA, int owedA, long levelB, long capB, int owedB)
		{
			KingdomZoneRow[] zones = new KingdomZoneRow[2]
			{
				new KingdomZoneRow("A", 0, 100L, new KingdomStocks(new KingdomStockPair(levelA, capA), new KingdomStockPair(0L, 0L), new KingdomStockPair(0L, 0L)), 0, 0, 0, 0, owedA, 0, 0),
				new KingdomZoneRow("B", 0, 100L, new KingdomStocks(new KingdomStockPair(levelB, capB), new KingdomStockPair(0L, 0L), new KingdomStockPair(0L, 0L)), 0, 0, 0, 0, owedB, 0, 0)
			};
			KingdomCityState state;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomCityState.TryCreate(1, 1, "seat", 100L, default(KingdomStocks), zones,
				new KingdomWorkRow[0], new KingdomResidentRow[0], new KingdomClockRow[0], out state, out fault), fault.ToString());
			return state;
		}

		/// <summary>
		/// Invariant I1, and the sentence W7 exists to make true: <b>a network transfer is a carry;
		/// level and debt move together.</b> Nothing physical has happened, so <c>level - owed</c>
		/// — the ground — is identical on both rows afterwards, and the city's totals of both are
		/// unchanged.
		/// </summary>
		[Test]
		public void ATransferMovesLevelAndDebtTogetherOnBothRows()
		{
			KingdomCityState before = TwoZones(500L, 1000L, 0, 100L, 1000L, 0);
			KingdomCityState after;
			long moved;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomNetworkRules.TryPostTransfer(before, KingdomStockKind.Water, 0, 1, 120L, out after, out moved, out fault), fault.ToString());
			Assert.AreEqual(120L, moved);
			KingdomZoneRow giver;
			KingdomZoneRow taker;
			Assert.IsTrue(after.TryZone(0, out giver));
			Assert.IsTrue(after.TryZone(1, out taker));
			Assert.AreEqual(380L, giver.Stocks.Water.Level);
			Assert.AreEqual(-120, giver.OwedWater);
			Assert.AreEqual(220L, taker.Stocks.Water.Level);
			Assert.AreEqual(120, taker.OwedWater);
			// The identity, stated the way the audit line states it.
			Assert.AreEqual(500L, giver.Stocks.Water.Level - giver.OwedWater, "the giver's GROUND moved, and it must not have");
			Assert.AreEqual(100L, taker.Stocks.Water.Level - taker.OwedWater, "the taker's GROUND moved, and it must not have");
			Assert.AreEqual(600L, giver.Stocks.Water.Level + taker.Stocks.Water.Level, "the city invented or destroyed water");
			Assert.AreEqual(0, giver.OwedWater + taker.OwedWater, "the city's net debt moved");
		}

		/// <summary>What a row already owes as a DRAW is promised to a vessel nobody has opened,
		/// and a main may not carry it away a second time.</summary>
		[Test]
		public void ARowAlreadyOwingADrawCannotGiveThatMuchAway()
		{
			KingdomCityState before = TwoZones(500L, 1000L, -400, 0L, 1000L, 0);
			KingdomCityState after;
			long moved;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomNetworkRules.TryPostTransfer(before, KingdomStockKind.Water, 0, 1, 500L, out after, out moved, out fault), fault.ToString());
			Assert.AreEqual(100L, moved, "only the part of the level nothing has already claimed may run");
		}

		/// <summary>A line into a full vessel moves nothing, and reports nothing moved rather than
		/// reporting what it wished had.</summary>
		[Test]
		public void ALineIntoAFullVesselMovesNothing()
		{
			KingdomCityState before = TwoZones(500L, 1000L, 0, 1000L, 1000L, 0);
			KingdomCityState after;
			long moved;
			KingdomCityFault fault;
			Assert.IsTrue(KingdomNetworkRules.TryPostTransfer(before, KingdomStockKind.Water, 0, 1, 500L, out after, out moved, out fault), fault.ToString());
			Assert.AreEqual(0L, moved);
			Assert.AreSame(before, after, "a transfer that moved nothing must not publish a new book");
		}

		/// <summary>A line from a zone to itself is a topology bug, and it is refused loudly rather
		/// than absorbed as a zero — a silent zero is how that bug survives to a playtest.</summary>
		[Test]
		public void ALineFromAZoneToItselfIsRefused()
		{
			KingdomCityState before = TwoZones(500L, 1000L, 0, 100L, 1000L, 0);
			KingdomCityState after;
			long moved;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomNetworkRules.TryPostTransfer(before, KingdomStockKind.Water, 1, 1, 10L, out after, out moved, out fault));
			Assert.AreEqual(KingdomCityFault.InvalidIndex, fault);
			Assert.AreSame(before, after);
		}

		/// <summary>The debt is an <c>int</c> because a dram is counted in <c>int</c> everywhere the
		/// ground counts one. A carry that would wrap it refuses instead of publishing a debt with
		/// the wrong sign.</summary>
		[Test]
		public void ACarryThatWouldWrapTheDebtRefuses()
		{
			KingdomCityState before = TwoZones(long.MaxValue / 4L, long.MaxValue / 2L, int.MinValue + 5, 0L, long.MaxValue / 2L, 0);
			KingdomCityState after;
			long moved;
			KingdomCityFault fault;
			Assert.IsFalse(KingdomNetworkRules.TryPostTransfer(before, KingdomStockKind.Water, 0, 1, 1000L, out after, out moved, out fault));
			Assert.AreEqual(KingdomCityFault.ArithmeticOverflow, fault);
			Assert.AreSame(before, after, "a refused carry leaves the book byte-identical");
		}
	}
}
#endif
