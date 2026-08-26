using System;

namespace ThousandAndFirst.Simulation.City
{
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
	internal sealed partial class KingdomDistanceMatrix
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
	}
}
