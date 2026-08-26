using System;

namespace ThousandAndFirst.Simulation.City
{

	/// <summary>
	/// One network's graph row: what it is, what it joins, and the traversal order the solve walks.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.11's row, built:
	/// </para>
	/// <code>
	/// Network = (NetworkId, Kind, TopologyStamp, Nodes[&lt;= 32], Edges[&lt;= 48])
	/// Node    = (WorkId, Role, Capacity, Rate)          16 B
	/// Edge    = (NodeA, NodeB, ConduitCapacity, Condition)  16 B
	/// </code>
	/// <para>
	/// <b>Stocks key by <c>(NetworkId, LiquidId)</c></b> — 12(g)'s explicit ask, and it is forced by
	/// the engine: <c>LiquidVolume</c> is liquid-agnostic
	/// (<c>D/XRL/World/Parts/LiquidVolume.cs:25</c>, a component dictionary of per-mille shares at
	/// <c>:89</c>), so a cistern on the fresh main and a cistern on a brine main are different stock
	/// rows despite being the same part. Here the pair is the row's own identity: a network is
	/// <b>typed</b>, so <see cref="LiquidId"/> travels on the header and the network id implies it.
	/// Keying by network alone would let brine into the city's water figure, which STANDARDS
	/// &sect;1 forbids by another route.
	/// </para>
	/// <para>
	/// <b>Frozen, per &sect;1.3.</b> Sealed, arrays copied in and never handed back, no transition
	/// that is not a rebuild. A topology change does not edit a graph; it builds another one and the
	/// old one is dropped.
	/// </para>
	/// </summary>
	internal sealed partial class KingdomNetworkGraph
	{
		private readonly KingdomNetworkNode[] nodes;

		private readonly KingdomNetworkEdge[] edges;

		/// <summary>Reachable node indices, in the order one traversal from the sources settles
		/// them. Computed once, when the topology is built.</summary>
		private readonly byte[] order;

		/// <summary>Per node, the edge that first reached it, or <see cref="KingdomNetworkRules.NoParent"/>
		/// for a source and for anything nothing reaches.</summary>
		private readonly byte[] parentEdge;

		internal readonly int NetworkId;

		internal readonly KingdomNetworkKind Kind;

		/// <summary>What a liquid line carries. Null for every other kind, and never empty for
		/// <see cref="KingdomNetworkKind.Liquid"/> — an untyped liquid network is refused at
		/// build.</summary>
		internal readonly string LiquidId;

		/// <summary>The ground stamp this graph was built from. Compared, never advanced:
		/// <c>KingdomNetworkRules.NeedsRebuild</c> is the only reader.</summary>
		internal readonly long TopologyStamp;

		private KingdomNetworkGraph(
			int networkId,
			KingdomNetworkKind kind,
			string liquidId,
			long topologyStamp,
			KingdomNetworkNode[] nodes,
			KingdomNetworkEdge[] edges,
			byte[] order,
			byte[] parentEdge)
		{
			NetworkId = networkId;
			Kind = kind;
			LiquidId = liquidId;
			TopologyStamp = topologyStamp;
			this.nodes = nodes;
			this.edges = edges;
			this.order = order;
			this.parentEdge = parentEdge;
		}

		internal int NodeCount
		{
			get { return nodes.Length; }
		}

		internal int EdgeCount
		{
			get { return edges.Length; }
		}

		/// <summary>How many nodes anything actually reaches. Below <see cref="NodeCount"/> when
		/// the founder laid a line that does not come back to a source — which is a legal, silent,
		/// entirely ordinary thing to have done and reads as those nodes getting nothing.</summary>
		internal int ReachedCount
		{
			get { return order.Length; }
		}

		internal bool TryNode(int index, out KingdomNetworkNode node)
		{
			if (index < 0 || index >= nodes.Length)
			{
				node = default(KingdomNetworkNode);
				return false;
			}
			node = nodes[index];
			return true;
		}

		internal bool TryEdge(int index, out KingdomNetworkEdge edge)
		{
			if (index < 0 || index >= edges.Length)
			{
				edge = default(KingdomNetworkEdge);
				return false;
			}
			edge = edges[index];
			return true;
		}

	}
}
