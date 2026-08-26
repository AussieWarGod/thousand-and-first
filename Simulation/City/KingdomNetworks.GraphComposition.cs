using System;
using System.Collections.Generic;

using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomNetworks
	{
		// ==================================================================================
		// Composing the graph from the ground
		// ==================================================================================

		/// <summary>
		/// The city's liquid networks as graph rows, stitched across zone boundaries out of the
		/// per-zone components every visited zone left behind.
		/// <para>
		/// A component declares across an edge; the neighbouring zone's component declares back;
		/// they carry the same liquid; so they are one network. That stitch is the model's whole
		/// reason for existing &mdash; vanilla cannot make it, because its walk is local-only.
		/// </para>
		/// </summary>
		private static bool TryComposeGraphs(KingdomSystem System, KingdomCityState state, List<KingdomNetworkGraph> graphs, List<int[]> members)
		{
			if (System.ClaimedZones == null || state.ZoneCount <= 0)
			{
				return false;
			}
			// One network per liquid, per connected run of zones. Composed in ZONE ROW ORDER so the
			// same ground always produces the same network ids and the same node ordering, which is
			// what makes the solve reproduce across a reload.
			Dictionary<string, List<int>> byLiquid = new Dictionary<string, List<int>>();
			Dictionary<string, int> narrowest = new Dictionary<string, int>();
			for (int i = 0; i < state.ZoneCount; i++)
			{
				KingdomZoneRow row;
				if (!state.TryZone(i, out row))
				{
					continue;
				}
				KingdomZoneLine[] lines;
				if (!Composed.TryGetValue(row.ZoneId, out lines))
				{
					continue;
				}
				for (int l = 0; l < lines.Length; l++)
				{
					KingdomZoneLine line = lines[l];
					if (line.Taps <= 0 || line.EdgeMask == 0 || line.CapacityPerDay <= 0)
					{
						// A line that taps nothing carries nothing anywhere, and a line that never
						// declares across an edge is one zone's plumbing rather than a network.
						continue;
					}
					List<int> zones;
					if (!byLiquid.TryGetValue(line.Liquid, out zones))
					{
						zones = new List<int>();
						byLiquid[line.Liquid] = zones;
						narrowest[line.Liquid] = int.MaxValue;
					}
					if (!zones.Contains(i))
					{
						zones.Add(i);
					}
					int carry = (int)((long)line.CapacityPerDay * line.ConditionPercent / 100L);
					if (carry < narrowest[line.Liquid])
					{
						narrowest[line.Liquid] = carry;
					}
				}
			}
			int id = 0;
			foreach (KeyValuePair<string, List<int>> pair in byLiquid)
			{
				List<int> zones = pair.Value;
				if (zones.Count < 2 || graphs.Count >= KingdomNetworkRules.MaxNetworksPerCity)
				{
					continue;
				}
				KingdomNetworkNode[] nodes = new KingdomNetworkNode[zones.Count];
				int[] rows = new int[zones.Count];
				int carry = narrowest[pair.Key];
				for (int n = 0; n < zones.Count; n++)
				{
					KingdomZoneRow row;
					state.TryZone(zones[n], out row);
					long capacity = row.Stocks.Water.Capacity;
					nodes[n] = new KingdomNetworkNode(
						zones[n],
						KingdomNetworkRole.Store,
						KingdomWorkTier.Water,
						(capacity > int.MaxValue) ? int.MaxValue : (int)capacity,
						carry);
					rows[n] = zones[n];
				}
				// The zones on one liquid are chained in row order: a line laid across four
				// quarters is four nodes and three segments, and the bottleneck relaxation then
				// answers what reaches the far end. A chain and not a clique, because a clique
				// would claim segments the founder never laid.
				KingdomNetworkEdge[] edges = new KingdomNetworkEdge[zones.Count - 1];
				for (int e = 0; e < edges.Length; e++)
				{
					edges[e] = new KingdomNetworkEdge(e, e + 1, carry, 100);
				}
				KingdomNetworkGraph graph;
				KingdomCityFault fault;
				if (!KingdomNetworkGraph.TryBuild(id, KingdomNetworkKind.Liquid, pair.Key, groundStamp,
						nodes, nodes.Length, edges, edges.Length, out graph, out fault))
				{
					KingdomLog.Log("network: refused to compose the " + pair.Key + " line (" + fault + ")");
					continue;
				}
				graphs.Add(graph);
				members.Add(rows);
				id++;
			}
			return graphs.Count > 0;
		}

		/// <summary>
		/// Walks one zone's ground and returns the components standing in it.
		/// <para>
		/// <b>Declared, never inferred, at every step.</b> Two segments are in one component only
		/// when both declared toward each other and both carry the same liquid; a crossover carries
		/// a run through without joining it to the other run; and a cross-liquid join
		/// <b>refuses by name</b>, once, on the ledger.
		/// </para>
		/// </summary>
		private static KingdomZoneLine[] Compose(Zone Z)
		{
			List<GameObject> pieces = new List<GameObject>();
			Dictionary<int, int> atCell = new Dictionary<int, int>();
			KingdomSurvey survey = KingdomSurvey.Take(Z);
			for (int indexed = 0; indexed < survey.NetworkPieces.Count; indexed++)
			{
				GameObject item = survey.NetworkPieces[indexed];
				// The founder's designation is the whole of a line's membership, exactly as it is
				// the whole of the power grid's: nothing the player merely left lying about is ever
				// read, moved or drained (the protection law).
				if (item.GetIntProperty("KingdomBuilt") != 1 && item.GetIntProperty("KingdomGrid") != 1)
				{
					continue;
				}
				if (item.GetPart<r_KingdomLiquidConduit>() == null
					&& item.GetPart<r_KingdomLiquidTap>() == null
					&& item.GetPart<r_KingdomLiquidCrossover>() == null)
				{
					continue;
				}
				Cell cell = item.CurrentCell;
				if (cell == null)
				{
					continue;
				}
				int key = cell.X * 1000 + cell.Y;
				if (atCell.ContainsKey(key))
				{
					// One piece to a cell. A second is a stacking accident and joins nothing rather
					// than silently becoming part of whichever line was walked first.
					continue;
				}
				atCell[key] = pieces.Count;
				pieces.Add(item);
			}
			if (pieces.Count == 0)
			{
				return new KingdomZoneLine[0];
			}
			int[] parent = new int[pieces.Count];
			for (int i = 0; i < parent.Length; i++)
			{
				parent[i] = i;
			}
			int[] directions = new int[4]
			{
				KingdomNetworkRules.JoinNorth,
				KingdomNetworkRules.JoinSouth,
				KingdomNetworkRules.JoinEast,
				KingdomNetworkRules.JoinWest
			};
			int told = 0;
			for (int i = 0; i < pieces.Count; i++)
			{
				int mine = DeclarationOf(pieces[i]);
				string liquid = LiquidOf(pieces[i]);
				if (mine == 0 || liquid == null)
				{
					continue;
				}
				Cell cell = pieces[i].CurrentCell;
				for (int d = 0; d < directions.Length; d++)
				{
					if ((mine & directions[d]) == 0)
					{
						continue;
					}
					int neighbour = Through(Z, atCell, pieces, cell.X, cell.Y, directions[d]);
					if (neighbour < 0)
					{
						continue;
					}
					string theirs = LiquidOf(pieces[neighbour]);
					int theirMask = DeclarationOf(pieces[neighbour]);
					if (!KingdomNetworkRules.DeclaredToward(mine, theirMask, directions[d]))
					{
						continue;
					}
					KingdomJoinVerdict verdict = KingdomNetworkRules.JudgeJoin(true, KingdomNetworkKind.Liquid, liquid, KingdomNetworkKind.Liquid, theirs);
					if (verdict == KingdomJoinVerdict.Joined)
					{
						Union(parent, i, neighbour);
						continue;
					}
					// One refusal reaches the founder per composition, however many tiles two
					// mismatched mains run beside each other for. STANDARDS 7b asks for a sentence
					// the founder will SEE; twenty identical sentences is the thing 7b's own
					// complaint is about. Every piece still keeps its own latch underneath, so
					// laying a second bad join somewhere else is told on its own pass.
					told += Refuse(pieces[i], verdict, liquid, theirs, told > 0) ? 1 : 0;
				}
			}
			Dictionary<int, KingdomZoneLine> lines = new Dictionary<int, KingdomZoneLine>();
			for (int i = 0; i < pieces.Count; i++)
			{
				string liquid = LiquidOf(pieces[i]);
				if (liquid == null)
				{
					continue;
				}
				int root = Find(parent, i);
				Cell cell = pieces[i].CurrentCell;
				int edge = 0;
				if (cell.X == 0)
				{
					edge |= KingdomNetworkRules.JoinWest;
				}
				if (cell.X >= Z.Width - 1)
				{
					edge |= KingdomNetworkRules.JoinEast;
				}
				if (cell.Y == 0)
				{
					edge |= KingdomNetworkRules.JoinNorth;
				}
				if (cell.Y >= Z.Height - 1)
				{
					edge |= KingdomNetworkRules.JoinSouth;
				}
				edge &= DeclarationOf(pieces[i]);
				int taps = (pieces[i].GetPart<r_KingdomLiquidTap>() != null) ? 1 : 0;
				int condition = KingdomWear.EffectivenessOf(pieces[i]);
				if (condition < MinimumCarryingCondition)
				{
					condition = 0;
				}
				int capacity = CapacityOf(pieces[i]);
				KingdomZoneLine held;
				if (lines.TryGetValue(root, out held))
				{
					lines[root] = new KingdomZoneLine(
						Z.ZoneID,
						held.Liquid,
						(held.CapacityPerDay < capacity) ? held.CapacityPerDay : capacity,
						(held.ConditionPercent < condition) ? held.ConditionPercent : condition,
						held.EdgeMask | edge,
						held.Taps + taps);
				}
				else
				{
					lines[root] = new KingdomZoneLine(Z.ZoneID, liquid, capacity, condition, edge, taps);
				}
			}
			KingdomZoneLine[] composed = new KingdomZoneLine[lines.Count];
			lines.Values.CopyTo(composed, 0);
			return composed;
		}

	}
}
