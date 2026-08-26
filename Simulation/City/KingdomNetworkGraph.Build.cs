using System;

namespace ThousandAndFirst.Simulation.City
{
	internal sealed partial class KingdomNetworkGraph
	{
		/// <summary>
		/// Builds one network's graph from a declared topology, or refuses and publishes nothing.
		/// <para>
		/// <b>Everything expensive happens here, and here is never reckon.</b> The traversal order
		/// is a fact about the topology, so it is computed once, when the topology is laid, out of
		/// the ground — <c>O(nodes &times; edges)</c> at worst, 32 &times; 48 = 1,536 integer
		/// operations, paid on a placement and never on a pass. What reckon then runs
		/// (<see cref="TryBottleneck"/>) is one linear walk of that order, which is what holds the
		/// solve inside &sect;0.0's <c>nodes + edges</c> ceiling instead of <c>nodes &times; edges</c>.
		/// </para>
		/// </summary>
		/// <param name="nodes">Node rows, in a stable order the caller owns. Copied.</param>
		/// <param name="edges">Declared joins. Copied. An edge naming a node that is not there is a
		/// refusal, not a dropped edge.</param>
		internal static bool TryBuild(
			int networkId,
			KingdomNetworkKind kind,
			string liquidId,
			long topologyStamp,
			KingdomNetworkNode[] nodes,
			int nodeCount,
			KingdomNetworkEdge[] edges,
			int edgeCount,
			out KingdomNetworkGraph graph,
			out KingdomCityFault fault)
		{
			graph = null;
			if (nodes == null || edges == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (nodeCount < 0 || nodeCount > nodes.Length || edgeCount < 0 || edgeCount > edges.Length)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (nodeCount > KingdomNetworkRules.MaxNodes || edgeCount > KingdomNetworkRules.MaxEdges)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			if (topologyStamp < 0L)
			{
				fault = KingdomCityFault.InvalidTick;
				return false;
			}
			// The typed-line law, checked where a network is made rather than where it is read: a
			// liquid network with no liquid could never refuse a join by name, and a network of any
			// other kind that carried a liquid name would be claiming something it cannot mean.
			bool liquid = kind == KingdomNetworkKind.Liquid;
			if (liquid == string.IsNullOrEmpty(liquidId))
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			for (int i = 0; i < edgeCount; i++)
			{
				KingdomNetworkEdge edge = edges[i];
				if (edge.NodeA < 0 || edge.NodeA >= nodeCount || edge.NodeB < 0 || edge.NodeB >= nodeCount || edge.NodeA == edge.NodeB)
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
			}
			KingdomNetworkNode[] nodeRows = new KingdomNetworkNode[nodeCount];
			Array.Copy(nodes, nodeRows, nodeCount);
			KingdomNetworkEdge[] edgeRows = new KingdomNetworkEdge[edgeCount];
			Array.Copy(edges, edgeRows, edgeCount);
			byte[] parents = new byte[nodeCount];
			for (int i = 0; i < nodeCount; i++)
			{
				parents[i] = KingdomNetworkRules.NoParent;
			}
			byte[] walk = new byte[nodeCount];
			int walked = 0;
			bool[] seen = new bool[nodeCount];
			// Seeded with every source, in node order, so a network with two wheels settles the
			// same way every pass and after every reload. There is no draw here and there is no tie
			// to break by one: the order IS the node order.
			//
			// A STORE IS THE ROOT OF LAST RESORT, and only the lowest-indexed one. A store that
			// holds something feeds the line exactly as a wheel does -- §3.11's solve has the
			// stores discharging into a deficit, which would be a sentence about nothing if the
			// store could not reach the sinks, and a bed of molten salt is FOR the night, which is
			// when there is no source at all. But rooting every store would make a line whose ends
			// are all vessels -- a water main between two cisterns -- a forest of roots with no
			// edge ever walked, and its throughput would read as unlimited when it is in fact the
			// narrowest length of pipe on it. One root, lowest index, and the rest are reached
			// through the segments that actually join them.
			bool anySource = false;
			for (int i = 0; i < nodeCount; i++)
			{
				if (nodeRows[i].Role == KingdomNetworkRole.Source)
				{
					anySource = true;
					break;
				}
			}
			for (int i = 0; i < nodeCount; i++)
			{
				bool root = anySource
					? nodeRows[i].Role == KingdomNetworkRole.Source
					: (nodeRows[i].Role == KingdomNetworkRole.Store && walked == 0);
				if (!root || seen[i])
				{
					continue;
				}
				seen[i] = true;
				walk[walked++] = (byte)i;
			}
			for (int cursor = 0; cursor < walked; cursor++)
			{
				int at = walk[cursor];
				// Edges are scanned in index order, so the parent a node ends up with is the
				// lowest-numbered edge on the shallowest frontier that reaches it. Deterministic
				// without a sort, and the rebuild is off the reckon path anyway.
				for (int e = 0; e < edgeCount; e++)
				{
					KingdomNetworkEdge edge = edgeRows[e];
					int other;
					if (edge.NodeA == at)
					{
						other = edge.NodeB;
					}
					else if (edge.NodeB == at)
					{
						other = edge.NodeA;
					}
					else
					{
						continue;
					}
					if (seen[other])
					{
						continue;
					}
					seen[other] = true;
					parents[other] = (byte)e;
					walk[walked++] = (byte)other;
				}
			}
			byte[] reached = new byte[walked];
			Array.Copy(walk, reached, walked);
			graph = new KingdomNetworkGraph(networkId, kind, liquid ? liquidId.Trim() : null, topologyStamp, nodeRows, edgeRows, reached, parents);
			fault = KingdomCityFault.None;
			return true;
		}

	}
}
