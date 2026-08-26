using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ThousandAndFirst.Api
{
	internal static partial class KingdomBehaviourRules
	{
		private static bool TryLegs(KingdomJobPlan plan, KingdomCityReading city, int pace,
			long start, out KingdomExtensionLeg[] legs, out long due)
		{
			legs = new KingdomExtensionLeg[plan.LegCount]; due = start;
			for (int i = 0; i < legs.Length; i++)
			{
				KingdomExtensionLeg leg;
				if (!plan.TryLeg(i, out leg) || !Held(city, leg.ZoneId)
					|| leg.EnterX < 0 || leg.EnterX >= 80 || leg.ExitX < 0 || leg.ExitX >= 80
					|| leg.EnterY < 0 || leg.EnterY >= 25 || leg.ExitY < 0 || leg.ExitY >= 25) return false;
				int dx = Math.Abs((int)leg.ExitX - leg.EnterX), dy = Math.Abs((int)leg.ExitY - leg.EnterY);
				int cells = Math.Max(dx, dy); if (cells <= 0) cells = 1;
				long cost = (long)cells * pace;
				if (cost <= 0L || due > long.MaxValue - cost) return false;
				due += cost; legs[i] = leg;
			}
			return due > start;
		}

		private static bool TryNormalizeChanges(string owner, KingdomJobPlan plan,
			out KingdomResourceChange[] changes)
		{
			changes = new KingdomResourceChange[plan.CompletionChangeCount];
			List<string> seen = new List<string>();
			for (int i = 0; i < changes.Length; i++)
			{
				KingdomResourceChange change;
				if (!plan.TryCompletionChange(i, out change)) return false;
				string key = KingdomApiRules.ExtensionKey(owner, change.ResourceKey);
				if (key == null || change.Amount == 0L || seen.Contains(key)) return false;
				seen.Add(key); changes[i] = new KingdomResourceChange(key, change.Amount);
			}
			return true;
		}

		private static bool TryApplyOwnedChanges(KingdomResourceReading[] source, string owner,
			KingdomResourceChange[] changes, out KingdomResourceReading[] next)
		{
			next = Copy(source);
			if (changes == null || changes.Length > KingdomApiRules.MaxChangesPerResult) return false;
			List<string> seen = new List<string>();
			for (int i = 0; i < changes.Length; i++)
			{
				string key = KingdomApiRules.ExtensionKey(owner, changes[i].ResourceKey);
				int at = ResourceIndex(next, key);
				if (key == null || at < 0 || changes[i].Amount == 0L || seen.Contains(key)) return false;
				seen.Add(key);
				long amount = changes[i].Amount;
				if ((amount > 0L && next[at].Level > next[at].Capacity - amount)
					|| (amount < 0L && (amount == long.MinValue || next[at].Level < -amount))) return false;
				next[at] = WithLevel(next[at], next[at].Level + amount);
			}
			return true;
		}

		private static bool TrySolve(KingdomNetworkPlan plan, KingdomCityReading city,
			out int flow, out int brownout, out int totalSupply)
		{
			flow = 0; brownout = 0; totalSupply = 0;
			int n = plan.NodeCount, e = plan.EdgeCount;
			KingdomExtensionNetworkNode[] nodes = new KingdomExtensionNetworkNode[n];
			KingdomExtensionNetworkEdge[] edges = new KingdomExtensionNetworkEdge[e];
			List<string> nodeKeys = new List<string>();
			int totalDemand = 0;
			for (int i = 0; i < n; i++)
			{
				if (!plan.TryNode(i, out nodes[i]) || !Held(city, nodes[i].ZoneId)
					|| KingdomApiRules.BehaviourIdentifier(nodes[i].Key, true) == null
					|| nodeKeys.Contains(nodes[i].Key) || nodes[i].RatePerDay < 0
					|| !Enum.IsDefined(typeof(KingdomExtensionNetworkRole), nodes[i].Role)) return false;
				nodeKeys.Add(nodes[i].Key);
				if (nodes[i].Role == KingdomExtensionNetworkRole.Relay && nodes[i].RatePerDay != 0) return false;
				if (nodes[i].Role == KingdomExtensionNetworkRole.Source)
				{
					if (totalSupply > int.MaxValue - nodes[i].RatePerDay) return false;
					totalSupply += nodes[i].RatePerDay;
				}
				else if (nodes[i].Role == KingdomExtensionNetworkRole.Sink)
				{
					if (totalDemand > int.MaxValue - nodes[i].RatePerDay) return false;
					totalDemand += nodes[i].RatePerDay;
				}
			}
			for (int i = 0; i < e; i++)
			{
				if (!plan.TryEdge(i, out edges[i]) || edges[i].A < 0 || edges[i].A >= n
					|| edges[i].B < 0 || edges[i].B >= n || edges[i].A == edges[i].B
					|| edges[i].CapacityPerDay <= 0) return false;
			}
			int[] sourceRemaining = new int[n];
			for (int i = 0; i < n; i++) if (nodes[i].Role == KingdomExtensionNetworkRole.Source)
				sourceRemaining[i] = nodes[i].RatePerDay;
			int[] edgeRemaining = new int[e];
			for (int i = 0; i < e; i++) edgeRemaining[i] = edges[i].CapacityPerDay;
			int[] sinks = new int[n]; int sinkCount = 0;
			for (int i = 0; i < n; i++) if (nodes[i].Role == KingdomExtensionNetworkRole.Sink) sinks[sinkCount++] = i;
			for (int i = 0; i < sinkCount; i++)
				for (int j = i + 1; j < sinkCount; j++)
					if (nodes[sinks[j]].Priority < nodes[sinks[i]].Priority
						|| (nodes[sinks[j]].Priority == nodes[sinks[i]].Priority && sinks[j] < sinks[i]))
					{ int swap = sinks[i]; sinks[i] = sinks[j]; sinks[j] = swap; }
			for (int s = 0; s < sinkCount; s++)
			{
				int demand = nodes[sinks[s]].RatePerDay;
				while (demand > 0)
				{
					int source, bottleneck; int[] path;
					if (!TryPath(sinks[s], nodes, edges, edgeRemaining, sourceRemaining,
						out source, out path, out bottleneck)) break;
					int sent = Math.Min(demand, Math.Min(sourceRemaining[source], bottleneck));
					if (sent <= 0) break;
					sourceRemaining[source] -= sent; demand -= sent; flow += sent;
					for (int p = 0; p < path.Length; p++) edgeRemaining[path[p]] -= sent;
				}
				brownout += demand;
			}
			return flow <= totalSupply && brownout == totalDemand - flow;
		}

		private static bool TryPath(int sink, KingdomExtensionNetworkNode[] nodes,
			KingdomExtensionNetworkEdge[] edges, int[] edgeRemaining, int[] sourceRemaining,
			out int source, out int[] path, out int bottleneck)
		{
			source = -1; path = new int[0]; bottleneck = 0;
			int n = nodes.Length; int[] parentNode = new int[n]; int[] parentEdge = new int[n];
			for (int i = 0; i < n; i++) { parentNode[i] = -2; parentEdge[i] = -1; }
			int[] queue = new int[n]; int head = 0, tail = 0; queue[tail++] = sink; parentNode[sink] = -1;
			while (head < tail && source < 0)
			{
				int here = queue[head++];
				if (nodes[here].Role == KingdomExtensionNetworkRole.Source && sourceRemaining[here] > 0)
				{ source = here; break; }
				for (int i = 0; i < edges.Length; i++)
				{
					if (edgeRemaining[i] <= 0) continue;
					int there = edges[i].A == here ? edges[i].B : (edges[i].B == here ? edges[i].A : -1);
					if (there < 0 || parentNode[there] != -2) continue;
					parentNode[there] = here; parentEdge[there] = i; queue[tail++] = there;
				}
			}
			if (source < 0) return false;
			List<int> found = new List<int>(); bottleneck = int.MaxValue;
			for (int at = source; at != sink; at = parentNode[at])
			{
				int edge = parentEdge[at]; if (edge < 0) return false;
				found.Add(edge); if (edgeRemaining[edge] < bottleneck) bottleneck = edgeRemaining[edge];
			}
			path = found.ToArray(); return path.Length > 0 && bottleneck > 0;
		}

	}
}
