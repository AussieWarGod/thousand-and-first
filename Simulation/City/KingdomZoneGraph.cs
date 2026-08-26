using System;

namespace ThousandAndFirst.Simulation.City
{
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
}
