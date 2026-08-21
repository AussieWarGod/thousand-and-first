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
	/// deep is the same arithmetic as a flat one. <b>Verticality is free.</b>
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

		internal KingdomZoneNode(string zoneId, int globalX, int globalY, int stratum)
		{
			ZoneId = zoneId;
			GlobalX = globalX;
			GlobalY = globalY;
			Stratum = stratum;
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

		/// <summary>Whether two nodes share an edge of the routing graph.</summary>
		internal static bool Adjacent(KingdomZoneNode from, KingdomZoneNode to)
		{
			return StepBetween(from, to) != KingdomZoneStep.None;
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
						table[at] = (ushort)hopCells;
						hops[at] = (sbyte)j;
						continue;
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

		private readonly int worksPerZone;

		private readonly ushort[] workEdge;

		private readonly ushort[] samePair;

		private readonly bool[] dirty;

		private KingdomDistanceMatrix(KingdomZoneGraph graph, int worksPerZone, ushort[] workEdge, ushort[] samePair, bool[] dirty)
		{
			this.graph = graph;
			this.worksPerZone = worksPerZone;
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

		internal int WorksPerZone
		{
			get { return worksPerZone; }
		}

		/// <summary>Entries the two level-2 slices actually occupy. The figure &sect;0.0(c)'s
		/// memory row is measured against, reported rather than asserted.</summary>
		internal int Entries
		{
			get { return workEdge.Length + samePair.Length + graph.Count * graph.Count; }
		}

		/// <summary>
		/// An empty matrix over these zones, with every zone dirty. Refuses a shape that would
		/// break &sect;0.0(c)'s entry budget rather than allocating past it &mdash; the budget is
		/// the contract and an allocation that quietly exceeds it is the regression the table
		/// exists to catch.
		/// </summary>
		internal static bool TryCreate(KingdomZoneGraph graph, int worksPerZone, out KingdomDistanceMatrix matrix, out KingdomCityFault fault)
		{
			matrix = null;
			if (graph == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (worksPerZone < 0)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			int edgeEntries = graph.Count * worksPerZone * KingdomDistanceRules.EdgesPerZone;
			int pairEntries = graph.Count * KingdomDistanceRules.PairSlots(worksPerZone);
			if (edgeEntries > KingdomDistanceRules.MaxWorkEdgeEntries || pairEntries > KingdomDistanceRules.MaxSamePairEntries)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			ushort[] edges = new ushort[edgeEntries];
			ushort[] pairs = new ushort[pairEntries];
			for (int i = 0; i < edges.Length; i++)
			{
				edges[i] = (ushort)KingdomDistanceRules.NoRoute;
			}
			for (int i = 0; i < pairs.Length; i++)
			{
				pairs[i] = (ushort)KingdomDistanceRules.NoRoute;
			}
			bool[] flags = new bool[graph.Count];
			for (int i = 0; i < flags.Length; i++)
			{
				flags[i] = true;
			}
			matrix = new KingdomDistanceMatrix(graph, worksPerZone, edges, pairs, flags);
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
		internal bool TryWriteZone(int zoneIndex, ushort[] edges, ushort[] pairs, out KingdomCityFault fault)
		{
			if (zoneIndex < 0 || zoneIndex >= graph.Count)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			int edgeStride = worksPerZone * KingdomDistanceRules.EdgesPerZone;
			int pairStride = KingdomDistanceRules.PairSlots(worksPerZone);
			if (edges == null || pairs == null || edges.Length != edgeStride || pairs.Length != pairStride)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			Array.Copy(edges, 0, workEdge, zoneIndex * edgeStride, edgeStride);
			Array.Copy(pairs, 0, samePair, zoneIndex * pairStride, pairStride);
			dirty[zoneIndex] = false;
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>How far one work stands from one of its zone's six edges, in cells.</summary>
		internal bool TryWorkToEdge(int zoneIndex, int workSlot, KingdomZoneStep step, out int cells)
		{
			cells = 0;
			if (zoneIndex < 0 || zoneIndex >= graph.Count || workSlot < 0 || workSlot >= worksPerZone
				|| step == KingdomZoneStep.None)
			{
				return false;
			}
			int at = (zoneIndex * worksPerZone + workSlot) * KingdomDistanceRules.EdgesPerZone + (int)step;
			int value = workEdge[at];
			if (value >= KingdomDistanceRules.NoRoute)
			{
				return false;
			}
			cells = value;
			return true;
		}

		/// <summary>How far two works in the same zone stand from each other, in cells.</summary>
		internal bool TrySameZone(int zoneIndex, int slotA, int slotB, out int cells)
		{
			cells = 0;
			if (zoneIndex < 0 || zoneIndex >= graph.Count)
			{
				return false;
			}
			if (slotA == slotB)
			{
				return true;
			}
			int index;
			KingdomCityFault fault;
			if (!KingdomDistanceRules.TryPairIndex(slotA, slotB, worksPerZone, out index, out fault))
			{
				return false;
			}
			int value = samePair[zoneIndex * KingdomDistanceRules.PairSlots(worksPerZone) + index];
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
		internal bool TryCompose(int fromZone, int fromSlot, int toZone, int toSlot, out int cells, out KingdomCityFault fault)
		{
			cells = 0;
			if (IsDirty(fromZone) || IsDirty(toZone))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (fromZone == toZone)
			{
				if (!TrySameZone(fromZone, fromSlot, toSlot, out cells))
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
			int[] path = new int[KingdomDistanceRules.MaxNodes];
			int length;
			if (!graph.TryPath(fromZone, toZone, path, out length, out fault) || length < 2
				|| !graph.TryDistance(fromZone, toZone, out between)
				|| !graph.TryStep(path[0], path[1], out out_)
				|| !graph.TryStep(path[length - 1], path[length - 2], out in_))
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			int leaving;
			int arriving;
			if (!TryWorkToEdge(fromZone, fromSlot, out_, out leaving) || !TryWorkToEdge(toZone, toSlot, in_, out arriving))
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
	}
}
