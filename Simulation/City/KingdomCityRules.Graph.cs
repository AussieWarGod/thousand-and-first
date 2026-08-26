using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCityRules
	{
		/// <summary>
		/// The city's level-1 zone graph, built from the book's own rows.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.10(2): nodes are claimed zones, edges are adjacency,
		/// all-pairs by Floyd&ndash;Warshall over &le; 9 nodes — 729 integer ops and an &le; 81-entry
		/// table. This half of the metric is composed from ZONE IDS ALONE, which is exactly why it
		/// may be built here: &sect;3.10(2) forbids recomputing the level-2 slices at reckon because
		/// they need the ground, and this needs none.
		/// </para>
		/// </summary>
		internal static bool TryZoneGraph(KingdomCityState state, out KingdomZoneGraph graph, out KingdomCityFault fault)
		{
			return TryZoneGraph(state, null, out graph, out fault);
		}

		/// <summary>
		/// The same graph, told which of the city's grounds have a shaft cut down from them.
		/// <para>
		/// The shafts are handed in rather than read here for the same reason the coordinates are
		/// composed from zone ids: this file may not touch the ground (&sect;3.10(2)), and where a
		/// delve stands is a fact about a built work. <c>KingdomDelve.DelvedZones</c> is what reads
		/// it; this only spends it.
		/// </para>
		/// </summary>
		/// <param name="shafts">Zone ids carrying a finished delve. Null and empty mean the caller
		/// knows of none, which closes every vertical edge &mdash; the conservative answer, and the
		/// right one for a realm that never went below.</param>
		internal static bool TryZoneGraph(KingdomCityState state, string[] shafts, out KingdomZoneGraph graph, out KingdomCityFault fault)
		{
			graph = null;
			if (state == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			int zones = state.ZoneCount;
			if (zones > KingdomDistanceRules.MaxNodes)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			KingdomZoneNode[] nodes = new KingdomZoneNode[zones];
			for (int i = 0; i < zones; i++)
			{
				KingdomZoneRow row;
				if (!state.TryZone(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				string world;
				int gx;
				int gy;
				int stratum;
				if (!KingdomRules.TryParseZoneID(row.ZoneId, out world, out gx, out gy, out stratum))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				nodes[i] = new KingdomZoneNode(row.ZoneId, gx, gy, stratum, Names(shafts, row.ZoneId));
			}
			return KingdomZoneGraph.TryBuild(nodes, zones, KingdomDistanceRules.ZoneTransitCells, out graph, out fault);
		}

		private static bool Names(string[] Zones, string ZoneId)
		{
			if (Zones == null || string.IsNullOrEmpty(ZoneId))
			{
				return false;
			}
			for (int i = 0; i < Zones.Length; i++)
			{
				if (string.Equals(Zones[i], ZoneId, StringComparison.Ordinal))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Each zone row's distance from the seated ground, for the logistics order.
		/// <para>
		/// A zone the graph cannot reach, and every zone when the graph itself cannot be built,
		/// reads as <b>zero</b> rather than as unreachable. That is deliberate and it is the
		/// never-worse fallback: at zero the distance key stops discriminating and the
		/// apportionment falls back to row order, which is precisely what every wave before W6
		/// did. A malformed zone id degrades the routing and never refuses the carry.
		/// </para>
		/// </summary>
		internal static bool TryZoneDistances(KingdomCityState state, string seatedZoneId, int[] cells, out KingdomCityFault fault)
		{
			return TryZoneDistances(state, seatedZoneId, null, cells, out fault);
		}

		/// <summary>The same distances, over a graph that knows which rock has been opened.</summary>
		/// <param name="shafts">Zone ids carrying a finished delve; see
		/// <see cref="TryZoneGraph(KingdomCityState, string[], out KingdomZoneGraph, out KingdomCityFault)"/>.
		/// Ground the graph cannot reach reads as <c>NoRoute</c> and so sorts LAST in the
		/// nearest-first apportionment, which is the honest answer for rock nobody can carry out
		/// of: it is drawn on only when there is nothing else.</param>
		internal static bool TryZoneDistances(KingdomCityState state, string seatedZoneId, string[] shafts, int[] cells, out KingdomCityFault fault)
		{
			if (state == null || cells == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			int zones = state.ZoneCount;
			if (cells.Length < zones)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			fault = KingdomCityFault.None;
			for (int i = 0; i < zones; i++)
			{
				cells[i] = 0;
			}
			KingdomZoneGraph graph;
			KingdomCityFault built;
			if (!TryZoneGraph(state, shafts, out graph, out built))
			{
				return true;
			}
			int seat;
			if (!graph.TryIndexOf(seatedZoneId, out seat))
			{
				return true;
			}
			for (int i = 0; i < zones; i++)
			{
				int measured;
				cells[i] = graph.TryDistance(i, seat, out measured) ? measured : KingdomDistanceRules.NoRoute;
			}
			return true;
		}

	}
}
