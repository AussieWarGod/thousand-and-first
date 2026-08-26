using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Which way one claimed zone lies from another. Six, because the engine's own
	/// topology gives at most six neighbours &mdash; four orthogonal, plus the stratum above and
	/// below (LIVING-CITY-ARCHITECTURE &sect;0.0(f), &sect;3.10(2)).</summary>
	internal enum KingdomZoneStep : byte
	{
		North = 0,
		South = 1,
		East = 2,
		West = 3,
		Up = 4,
		Down = 5,

		/// <summary>Not a direction. The two zones are the same, or not neighbours at all.</summary>
		None = 6
	}

	/// <summary>
	/// One node of the level-1 graph: a claimed zone, by id and by the world coordinates its
	/// <c>ZoneID</c> already carries.
	/// <para>
	/// <c>ZoneID</c> carries the stratum &mdash; <c>Assemble(...).Append(ZoneZ)</c>
	/// (<c>D/XRL/World/ZoneID.cs:12-24</c>) &mdash; so a city three parasangs wide and three strata
	/// deep is the same <i>arithmetic</i> as a flat one.
	/// </para>
	/// <para>
	/// <b>The arithmetic was free and the ground never was.</b> This file used to say verticality
	/// cost nothing, and it was true of the sums and false of the world: rock is not a doorway, and
	/// a carrier cannot walk down through it because the coordinates happen to differ by one. What
	/// makes the descent real is a shaft somebody cut (<see cref="KingdomDelveRules"/>), and
	/// <see cref="Shaft"/> is where the node carries whether one stands here.
	/// </para>
	/// </summary>
	internal readonly struct KingdomZoneNode
	{
		internal readonly string ZoneId;

		/// <summary>Global zone x, as <c>KingdomRules.TryParseZoneID</c> composes it
		/// (<c>parasangX * 3 + zoneX</c>).</summary>
		internal readonly int GlobalX;

		internal readonly int GlobalY;

		internal readonly int Stratum;

		/// <summary>Whether a finished delve goes down from this ground, which is the only thing
		/// that makes the stratum below it an edge of this graph at all.</summary>
		internal readonly bool Shaft;

		/// <summary>Ground with no shaft in it, which is every piece of ground a caller has not
		/// said otherwise about. The conservative default on purpose: an edge nobody vouched for
		/// is unbroken rock, and a route through unbroken rock is refused rather than estimated.</summary>
		internal KingdomZoneNode(string zoneId, int globalX, int globalY, int stratum)
			: this(zoneId, globalX, globalY, stratum, shaft: false)
		{
		}

		internal KingdomZoneNode(string zoneId, int globalX, int globalY, int stratum, bool shaft)
		{
			ZoneId = zoneId;
			GlobalX = globalX;
			GlobalY = globalY;
			Stratum = stratum;
			Shaft = shaft;
		}
	}

	/// <summary>
	/// The two-level distance matrix, and the roads discount that scales it.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.10(2): <b>we never store <c>works&sup2;</c>.</b> Level 1 is
	/// the zone graph &mdash; at most nine nodes, all-pairs by Floyd&ndash;Warshall at 9&sup3; = 729
	/// integer operations, a table of at most 81 entries. Level 2 is within a zone &mdash;
	/// work-to-edge lengths and same-zone work pairs. Any cross-zone distance composes from them in
	/// O(1):
	/// </para>
	/// <code>
	/// Dist(a, b) = IntraZone(a -&gt; exitEdge) + Sum EdgeCrossing + IntraZone(entryEdge -&gt; b)
	/// </code>
	/// <para>
	/// Pure and engine-free. <b>Invalidation is by structure, never by time or by stock</b> &mdash;
	/// a dirty flag per zone, set only on work placement, work removal, or a road change, and the
	/// zone's slice recomputed the next time that zone renders. Never at reckon: recomputing needs
	/// the ground, and reckon may not touch it (&sect;0.0(d)).
	/// </para>
	/// </summary>
	internal static class KingdomDistanceRules
	{
		/// <summary>LIVING-CITY-ARCHITECTURE &sect;0.0(f): nine zones is one whole parasang, and
		/// the level-1 table is sized for it whatever the stage-gate cap says today.</summary>
		internal const int MaxNodes = 9;

		/// <summary>The six directions a neighbour can lie in.</summary>
		internal const int EdgesPerZone = 6;

		/// <summary>Entries the level-2 work-to-edge store may hold, per city.
		/// LIVING-CITY-ARCHITECTURE &sect;0.0(c).</summary>
		internal const int MaxWorkEdgeEntries = KingdomCityMemoryRules.DistanceWorkEdgeEntries;

		/// <summary>Entries the level-2 same-zone pair store may hold, per city.
		/// LIVING-CITY-ARCHITECTURE &sect;0.0(c).</summary>
		internal const int MaxSamePairEntries = KingdomCityMemoryRules.DistanceSameZoneEntries;

		/// <summary>
		/// What one hop across a zone boundary costs the metric, in cells.
		/// <para>
		/// A vanilla zone is eighty cells wide (<c>D/XRL/World/Zone.cs</c>'s own default
		/// dimensions), so a carrier that enters one edge and leaves by another crosses about half
		/// of it. Named rather than inlined for the same reason the sinuosity constants are: a
		/// metric constant nobody can find is a metric nobody can retune.
		/// </para>
		/// </summary>
		internal const int ZoneTransitCells = 40;

		/// <summary>An entry the table has no route for. <c>ushort.MaxValue</c>, so an
		/// unreachable pair reads as unreachable rather than as adjacent.</summary>
		internal const int NoRoute = 65535;

		/// <summary>Which way <paramref name="to"/> lies from <paramref name="from"/>, or
		/// <see cref="KingdomZoneStep.None"/> when they are not neighbours.
		/// <para>
		/// Orthogonal only in the same stratum &mdash; deliberately narrower than
		/// <c>KingdomRules.CoordsAdjacent</c>, which admits diagonals because a CLAIM may border a
		/// zone corner-to-corner. A carrier cannot walk through a corner, so the routing graph does
		/// not have that edge.
		/// </para>
		/// </summary>
		internal static KingdomZoneStep StepBetween(KingdomZoneNode from, KingdomZoneNode to)
		{
			int dx = to.GlobalX - from.GlobalX;
			int dy = to.GlobalY - from.GlobalY;
			int dz = to.Stratum - from.Stratum;
			if (dz != 0)
			{
				if (dx != 0 || dy != 0 || (dz != 1 && dz != -1))
				{
					return KingdomZoneStep.None;
				}
				return (dz > 0) ? KingdomZoneStep.Down : KingdomZoneStep.Up;
			}
			if (dx == 0 && (dy == 1 || dy == -1))
			{
				return (dy > 0) ? KingdomZoneStep.South : KingdomZoneStep.North;
			}
			if (dy == 0 && (dx == 1 || dx == -1))
			{
				return (dx > 0) ? KingdomZoneStep.East : KingdomZoneStep.West;
			}
			return KingdomZoneStep.None;
		}

		/// <summary>
		/// Whether two nodes share an edge a carrier can actually walk.
		/// <para>
		/// Not the same question as <see cref="StepBetween"/>, and the difference is the whole of
		/// the delve. A direction always exists between a zone and the one under it; an EDGE
		/// exists only where a shaft was cut (<see cref="KingdomDelveRules.ShaftJoinsStrata"/>).
		/// Symmetric, because a shaft is: the flag is read off the SHALLOWER node, which is the
		/// ground the winding gear stands on, whichever end the question is asked from.
		/// </para>
		/// </summary>
		internal static bool Adjacent(KingdomZoneNode from, KingdomZoneNode to)
		{
			KingdomZoneStep step = StepBetween(from, to);
			if (step == KingdomZoneStep.None)
			{
				return false;
			}
			if (step != KingdomZoneStep.Up && step != KingdomZoneStep.Down)
			{
				return true;
			}
			KingdomZoneNode head = (from.Stratum < to.Stratum) ? from : to;
			KingdomZoneNode foot = (from.Stratum < to.Stratum) ? to : from;
			return KingdomDelveRules.ShaftJoinsStrata(head.Stratum, foot.Stratum, head.Shaft);
		}

		/// <summary>
		/// A distance scaled by a road discount, in percent of the undiscounted figure.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.10(3): a leg following a laid road is scaled by
		/// <c>KingdomItineraryRules.RoadDiscountPercent</c>, <b>applied identically to the estimate
		/// and to the measured length, so a road cannot make the two disagree</b>. The consequence
		/// the player sees is the point: laying a road visibly shortens every itinerary that uses
		/// it.
		/// </para>
		/// <para>
		/// Rounds up, and never below one cell for a non-zero distance: a road makes a journey
		/// shorter and never instantaneous.
		/// </para>
		/// </summary>
		internal static bool TryDiscount(int cells, int roadDiscountPercent, out int discounted, out KingdomCityFault fault)
		{
			discounted = 0;
			if (cells < 0 || roadDiscountPercent <= 0 || roadDiscountPercent > 100)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			fault = KingdomCityFault.None;
			if (cells == 0)
			{
				return true;
			}
			long scaled = ((long)cells * roadDiscountPercent + 99L) / 100L;
			discounted = (scaled < 1L) ? 1 : ((scaled > NoRoute) ? NoRoute : (int)scaled);
			return true;
		}

		/// <summary>Same-zone pair index inside one zone's triangular slice. Total over any
		/// ordering of the two work slots, because a distance is symmetric and storing it twice is
		/// storing two answers.</summary>
		internal static bool TryPairIndex(int slotA, int slotB, int worksInZone, out int index, out KingdomCityFault fault)
		{
			index = -1;
			if (worksInZone < 0 || slotA < 0 || slotB < 0 || slotA >= worksInZone || slotB >= worksInZone || slotA == slotB)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			int low = (slotA < slotB) ? slotA : slotB;
			int high = (slotA < slotB) ? slotB : slotA;
			// The row-major upper triangle: rows shrink by one each step down.
			index = low * worksInZone - (low * (low + 1)) / 2 + (high - low - 1);
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>Entries a zone's triangular same-zone slice needs for this many works.</summary>
		internal static int PairSlots(int worksInZone)
		{
			if (worksInZone < 2)
			{
				return 0;
			}
			return worksInZone * (worksInZone - 1) / 2;
		}
	}

	/// <summary>
	/// The level-1 all-pairs table over the claimed zones, with the next-hop matrix the itinerary
	/// planner reads the zone path off.
	/// <para>
	/// Built once per structural change and never at reckon. Immutable once built: a route table
	/// that could be edited in place is a route table two callers can disagree about mid-plan.
	/// </para>
	/// </summary>
	internal sealed class KingdomZoneGraph
	{
		private readonly KingdomZoneNode[] nodes;

		private readonly int count;

		private readonly ushort[] distance;

		private readonly sbyte[] nextHop;

		/// <summary>Integer operations the Floyd&ndash;Warshall pass actually ran, for the
		/// receipt. LIVING-CITY-ARCHITECTURE &sect;3.10(2) prices it at 9&sup3; = 729.</summary>
		internal readonly long Operations;

		private KingdomZoneGraph(KingdomZoneNode[] nodes, int count, ushort[] distance, sbyte[] nextHop, long operations)
		{
			this.nodes = nodes;
			this.count = count;
			this.distance = distance;
			this.nextHop = nextHop;
			Operations = operations;
		}

		internal int Count
		{
			get { return count; }
		}

		/// <summary>
		/// The zone graph, all-pairs. Refuses and publishes nothing rather than handing back a
		/// half-built table.
		/// </summary>
		/// <param name="hopCells">What one hop costs. Pass a road-discounted figure to make a road
		/// shorten every route that uses it.</param>
		internal static bool TryBuild(KingdomZoneNode[] rows, int rowCount, int hopCells, out KingdomZoneGraph graph, out KingdomCityFault fault)
		{
			graph = null;
			if (rows == null || rowCount < 0 || rowCount > rows.Length || rowCount > KingdomDistanceRules.MaxNodes)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (hopCells <= 0 || hopCells > KingdomDistanceRules.NoRoute)
			{
				fault = KingdomCityFault.InvalidInterval;
				return false;
			}
			KingdomZoneNode[] kept = new KingdomZoneNode[rowCount];
			for (int i = 0; i < rowCount; i++)
			{
				if (string.IsNullOrEmpty(rows[i].ZoneId))
				{
					fault = KingdomCityFault.NullArgument;
					return false;
				}
				kept[i] = rows[i];
			}
			ushort[] table = new ushort[rowCount * rowCount];
			sbyte[] hops = new sbyte[rowCount * rowCount];
			for (int i = 0; i < rowCount; i++)
			{
				for (int j = 0; j < rowCount; j++)
				{
					int at = i * rowCount + j;
					if (i == j)
					{
						table[at] = 0;
						hops[at] = (sbyte)j;
						continue;
					}
					if (KingdomDistanceRules.Adjacent(kept[i], kept[j]))
					{
						KingdomZoneStep step = KingdomDistanceRules.StepBetween(kept[i], kept[j]);
						int cells = (step == KingdomZoneStep.Up || step == KingdomZoneStep.Down)
							? KingdomDelveRules.ShaftHopCells(hopCells)
							: hopCells;
						// A shaft priced past the table's own ceiling is a shaft nothing can be
						// carried up. Clamped rather than cast, because the cast wraps and a
						// wrapped distance reads as the shortest way through the city.
						if (cells < KingdomDistanceRules.NoRoute)
						{
							table[at] = (ushort)cells;
							hops[at] = (sbyte)j;
							continue;
						}
					}
					table[at] = (ushort)KingdomDistanceRules.NoRoute;
					hops[at] = -1;
				}
			}
			long operations = 0L;
			for (int k = 0; k < rowCount; k++)
			{
				for (int i = 0; i < rowCount; i++)
				{
					for (int j = 0; j < rowCount; j++)
					{
						operations++;
						int through = table[i * rowCount + k] + table[k * rowCount + j];
						if (through >= KingdomDistanceRules.NoRoute || through >= table[i * rowCount + j])
						{
							continue;
						}
						table[i * rowCount + j] = (ushort)through;
						hops[i * rowCount + j] = hops[i * rowCount + k];
					}
				}
			}
			graph = new KingdomZoneGraph(kept, rowCount, table, hops, operations);
			fault = KingdomCityFault.None;
			return true;
		}

		internal bool TryIndexOf(string zoneId, out int index)
		{
			for (index = 0; index < count; index++)
			{
				if (string.Equals(nodes[index].ZoneId, zoneId, StringComparison.Ordinal))
				{
					return true;
				}
			}
			index = -1;
			return false;
		}

		internal bool TryNode(int index, out KingdomZoneNode node)
		{
			node = default(KingdomZoneNode);
			if (index < 0 || index >= count)
			{
				return false;
			}
			node = nodes[index];
			return true;
		}

		/// <summary>The composed cost of the zone-to-zone half of a route, in cells. False when
		/// there is no route at all, which is a refusal to plan rather than a very long trip.</summary>
		internal bool TryDistance(int from, int to, out int cells)
		{
			cells = 0;
			if (from < 0 || to < 0 || from >= count || to >= count)
			{
				return false;
			}
			int value = distance[from * count + to];
			if (value >= KingdomDistanceRules.NoRoute)
			{
				return false;
			}
			cells = value;
			return true;
		}

		/// <summary>
		/// The zone path from one node to another, written into <paramref name="path"/> as node
		/// indices including both ends.
		/// <para>
		/// Bounded by <c>KingdomItineraryRules.MaxLegs</c> at the call site, not here: this returns
		/// the true path and the planner refuses a job whose path wants more legs than a job may
		/// carry, because a silently truncated route is a carrier that arrives somewhere else.
		/// </para>
		/// </summary>
		internal bool TryPath(int from, int to, int[] path, out int length, out KingdomCityFault fault)
		{
			length = 0;
			if (path == null || from < 0 || to < 0 || from >= count || to >= count)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (nextHop[from * count + to] < 0)
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			int at = from;
			while (true)
			{
				if (length >= path.Length)
				{
					fault = KingdomCityFault.RowCapExceeded;
					return false;
				}
				path[length++] = at;
				if (at == to)
				{
					break;
				}
				int step = nextHop[at * count + to];
				if (step < 0 || length > count)
				{
					fault = KingdomCityFault.OutsideItinerary;
					return false;
				}
				at = step;
			}
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>Which way the first hop of a route leaves by. The edge a carrier exits on,
		/// and the one the work-to-edge slice is read against.</summary>
		internal bool TryStep(int from, int to, out KingdomZoneStep step)
		{
			step = KingdomZoneStep.None;
			if (from < 0 || to < 0 || from >= count || to >= count)
			{
				return false;
			}
			step = KingdomDistanceRules.StepBetween(nodes[from], nodes[to]);
			return step != KingdomZoneStep.None;
		}

		/// <summary>The two endpoint-facing steps of a complete route, without materialising its
		/// node list. <paramref name="leaving"/> faces from the source toward its first hop;
		/// <paramref name="arriving"/> faces from the destination back toward its last hop. Those
		/// are exactly the two work-to-edge columns §3.10 composes around the level-1 distance.
		/// Constant time and allocation-free.</summary>
		internal bool TryRouteSteps(int from, int to, out KingdomZoneStep leaving, out KingdomZoneStep arriving)
		{
			leaving = KingdomZoneStep.None;
			arriving = KingdomZoneStep.None;
			if (from < 0 || to < 0 || from >= count || to >= count || from == to)
			{
				return false;
			}
			int first = nextHop[from * count + to];
			int last = nextHop[to * count + from];
			if (first < 0 || last < 0)
			{
				return false;
			}
			leaving = KingdomDistanceRules.StepBetween(nodes[from], nodes[first]);
			arriving = KingdomDistanceRules.StepBetween(nodes[to], nodes[last]);
			return leaving != KingdomZoneStep.None && arriving != KingdomZoneStep.None;
		}
	}

	/// <summary>
	/// One city's two-level distance matrix: the zone graph, the work-to-edge slice, the same-zone
	/// pair slice, and one dirty flag per zone.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.10(2). A cache, never a book &mdash; every entry is
	/// recomputable from the ground, so it is not serialized and a save that comes back without one
	/// is a save that rebuilds it on the next render. What it must never be is <i>stale and
	/// trusted</i>, which is what the dirty flags are for: <b>invalidation is by structure, never by
	/// time or by stock.</b>
	/// </para>
	/// </summary>
	internal sealed class KingdomDistanceMatrix
	{
		private readonly KingdomZoneGraph graph;

		/// <summary>Stable endpoint ids per zone. Legal works are not matrix rows: only endpoints
		/// named by an open logistics snapshot are cached. This is the sparse/cache ruling required
		/// by the live 880-work envelope.</summary>
		private readonly int[][] endpointIds;

		private readonly ushort[][] workEdge;

		private readonly ushort[][] samePair;

		private readonly bool[] dirty;

		private int workEdgeEntries;

		private int samePairEntries;

		private KingdomDistanceMatrix(KingdomZoneGraph graph, int[][] endpointIds,
			ushort[][] workEdge, ushort[][] samePair, bool[] dirty)
		{
			this.graph = graph;
			this.endpointIds = endpointIds;
			this.workEdge = workEdge;
			this.samePair = samePair;
			this.dirty = dirty;
		}

		internal KingdomZoneGraph Graph
		{
			get { return graph; }
		}

		internal int ZoneCount
		{
			get { return graph.Count; }
		}

		/// <summary>Entries the two level-2 slices actually occupy. The figure &sect;0.0(c)'s
		/// memory row is measured against, reported rather than asserted.</summary>
		internal int Entries
		{
			get { return workEdgeEntries + samePairEntries + graph.Count * graph.Count; }
		}

		internal int WorkEdgeEntries
		{
			get { return workEdgeEntries; }
		}

		internal int SamePairEntries
		{
			get { return samePairEntries; }
		}

		internal int EndpointCount(int zoneIndex)
		{
			return (zoneIndex < 0 || zoneIndex >= endpointIds.Length) ? 0 : endpointIds[zoneIndex].Length;
		}

		/// <summary>Conservative equal-share cap a render can use when every zone wants a slice.
		/// The matrix itself admits uneven slices up to the two city-wide entry caps.</summary>
		internal static int EndpointShare(int zoneCount)
		{
			if (zoneCount <= 0 || zoneCount > KingdomDistanceRules.MaxNodes)
			{
				return 0;
			}
			int byEdges = KingdomDistanceRules.MaxWorkEdgeEntries
				/ (zoneCount * KingdomDistanceRules.EdgesPerZone);
			int byPairs = 0;
			while (KingdomDistanceRules.PairSlots(byPairs + 1) * zoneCount
				<= KingdomDistanceRules.MaxSamePairEntries)
			{
				byPairs++;
			}
			return (byEdges < byPairs) ? byEdges : byPairs;
		}

		/// <summary>Actual remaining city budget for one slice. Legal catalogue/job counts are
		/// uneven across zones; an equal-share fiction may not silently discard a live endpoint.</summary>
		internal int MaxEndpointsForZone(int zoneIndex)
		{
			if (zoneIndex < 0 || zoneIndex >= endpointIds.Length) return 0;
			int baseEdges = workEdgeEntries - workEdge[zoneIndex].Length;
			int basePairs = samePairEntries - samePair[zoneIndex].Length;
			int count = 0;
			while (count < KingdomDistanceSliceRules.MaxCandidateEndpoints
				&& baseEdges + (count + 1) * KingdomDistanceRules.EdgesPerZone
					<= KingdomDistanceRules.MaxWorkEdgeEntries
				&& basePairs + KingdomDistanceRules.PairSlots(count + 1)
					<= KingdomDistanceRules.MaxSamePairEntries) count++;
			return count;
		}

		/// <summary>
		/// An empty matrix over these zones, with every zone dirty. Legal catalogue work rows are
		/// not preallocated here: each rendered zone later publishes one bounded sparse endpoint
		/// slice, and <see cref="TryWriteZone"/> enforces the city-wide entry budgets.
		/// </summary>
		internal static bool TryCreate(KingdomZoneGraph graph, out KingdomDistanceMatrix matrix, out KingdomCityFault fault)
		{
			matrix = null;
			if (graph == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			int[][] ids = new int[graph.Count][];
			ushort[][] edges = new ushort[graph.Count][];
			ushort[][] pairs = new ushort[graph.Count][];
			bool[] flags = new bool[graph.Count];
			for (int i = 0; i < flags.Length; i++)
			{
				ids[i] = new int[0];
				edges[i] = new ushort[0];
				pairs[i] = new ushort[0];
				flags[i] = true;
			}
			matrix = new KingdomDistanceMatrix(graph, ids, edges, pairs, flags);
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>Whether this zone's level-2 slice needs recomputing before it can be
		/// believed.</summary>
		internal bool IsDirty(int zoneIndex)
		{
			return zoneIndex < 0 || zoneIndex >= dirty.Length || dirty[zoneIndex];
		}

		/// <summary>
		/// Marks one zone's slice stale. The only three callers &sect;3.10(2) permits are work
		/// placement, work removal, and a road change &mdash; never a clock and never a stock
		/// level.
		/// </summary>
		internal void MarkDirty(string zoneId)
		{
			int index;
			if (graph.TryIndexOf(zoneId, out index))
			{
				dirty[index] = true;
			}
		}

		/// <summary>Writes one zone's whole level-2 slice and clears its flag. All of it or none of
		/// it: a half-written slice is a matrix that answers some pairs from this pass and some
		/// from the one before.</summary>
		internal bool TryWriteZone(int zoneIndex, int[] ids, ushort[] edges, ushort[] pairs, out KingdomCityFault fault)
		{
			if (zoneIndex < 0 || zoneIndex >= graph.Count)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (ids == null || edges == null || pairs == null
				|| edges.Length != ids.Length * KingdomDistanceRules.EdgesPerZone
				|| pairs.Length != KingdomDistanceRules.PairSlots(ids.Length))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			for (int i = 0; i < ids.Length; i++)
			{
				if (ids[i] <= 0)
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				for (int j = 0; j < i; j++)
				{
					if (ids[j] == ids[i])
					{
						fault = KingdomCityFault.InvalidIndex;
						return false;
					}
				}
			}
			int nextEdges = workEdgeEntries - workEdge[zoneIndex].Length + edges.Length;
			int nextPairs = samePairEntries - samePair[zoneIndex].Length + pairs.Length;
			if (nextEdges > KingdomDistanceRules.MaxWorkEdgeEntries
				|| nextPairs > KingdomDistanceRules.MaxSamePairEntries)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			endpointIds[zoneIndex] = (int[])ids.Clone();
			workEdge[zoneIndex] = (ushort[])edges.Clone();
			samePair[zoneIndex] = (ushort[])pairs.Clone();
			workEdgeEntries = nextEdges;
			samePairEntries = nextPairs;
			dirty[zoneIndex] = false;
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>How far one sparse endpoint stands from one of its zone's six edges, in cells.</summary>
		internal bool TryWorkToEdge(int zoneIndex, int endpointId, KingdomZoneStep step, out int cells)
		{
			cells = 0;
			int slot;
			int direction = (int)step;
			if (zoneIndex < 0 || zoneIndex >= graph.Count || direction < 0
				|| direction >= KingdomDistanceRules.EdgesPerZone
				|| !TrySlot(zoneIndex, endpointId, out slot))
			{
				return false;
			}
			int at = slot * KingdomDistanceRules.EdgesPerZone + direction;
			int value = workEdge[zoneIndex][at];
			if (value >= KingdomDistanceRules.NoRoute)
			{
				return false;
			}
			cells = value;
			return true;
		}

		/// <summary>How far two works in the same zone stand from each other, in cells.</summary>
		internal bool TrySameZone(int zoneIndex, int endpointA, int endpointB, out int cells)
		{
			cells = 0;
			if (zoneIndex < 0 || zoneIndex >= graph.Count)
			{
				return false;
			}
			if (endpointA == endpointB && endpointA > 0)
			{
				int standing;
				return TrySlot(zoneIndex, endpointA, out standing);
			}
			int slotA;
			int slotB;
			if (!TrySlot(zoneIndex, endpointA, out slotA) || !TrySlot(zoneIndex, endpointB, out slotB))
			{
				return false;
			}
			int index;
			KingdomCityFault fault;
			if (!KingdomDistanceRules.TryPairIndex(slotA, slotB, endpointIds[zoneIndex].Length, out index, out fault))
			{
				return false;
			}
			int value = samePair[zoneIndex][index];
			if (value >= KingdomDistanceRules.NoRoute)
			{
				return false;
			}
			cells = value;
			return true;
		}

		/// <summary>
		/// <c>Dist(a, b)</c>, composed in O(1) from the three stores, exactly as &sect;3.10(2)
		/// writes it. Refuses when either endpoint's slice is dirty: a distance composed out of a
		/// slice the city knows is stale is worse than no distance, because a route planned on one
		/// is a carrier walking past a nearer holder (I6).
		/// </summary>
		internal bool TryCompose(int fromZone, int fromEndpoint, int toZone, int toEndpoint, out int cells, out KingdomCityFault fault)
		{
			cells = 0;
			if (IsDirty(fromZone) || IsDirty(toZone))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (fromZone == toZone)
			{
				if (!TrySameZone(fromZone, fromEndpoint, toEndpoint, out cells))
				{
					fault = KingdomCityFault.OutsideItinerary;
					return false;
				}
				fault = KingdomCityFault.None;
				return true;
			}
			KingdomZoneStep out_;
			KingdomZoneStep in_;
			int between;
			if (!graph.TryDistance(fromZone, toZone, out between)
				|| !graph.TryRouteSteps(fromZone, toZone, out out_, out in_))
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			int leaving;
			int arriving;
			if (!TryWorkToEdge(fromZone, fromEndpoint, out_, out leaving)
				|| !TryWorkToEdge(toZone, toEndpoint, in_, out arriving))
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			long total = (long)leaving + between + arriving;
			if (total > KingdomDistanceRules.NoRoute)
			{
				fault = KingdomCityFault.ArithmeticOverflow;
				return false;
			}
			cells = (int)total;
			fault = KingdomCityFault.None;
			return true;
		}

		private bool TrySlot(int zoneIndex, int endpointId, out int slot)
		{
			slot = -1;
			if (zoneIndex < 0 || zoneIndex >= endpointIds.Length || endpointId <= 0)
			{
				return false;
			}
			int[] ids = endpointIds[zoneIndex];
			for (int i = 0; i < ids.Length; i++)
			{
				if (ids[i] == endpointId)
				{
					slot = i;
					return true;
				}
			}
			return false;
		}
	}
}
