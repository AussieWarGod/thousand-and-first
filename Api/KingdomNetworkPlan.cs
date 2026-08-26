using System;

namespace ThousandAndFirst.Api
{
	/// <summary>One extension network proposal. Host solve is bounded, deterministic, and generic;
	/// the extension declares topology and rates but does not mutate resource rows itself.</summary>
	public sealed class KingdomNetworkPlan
	{
		private readonly KingdomExtensionNetworkNode[] nodes;
		private readonly KingdomExtensionNetworkEdge[] edges;

		/// <summary>Owner-local stable network key.</summary>
		public readonly string Key;
		/// <summary>Owner-local resource this network carries.</summary>
		public readonly string ResourceKey;

		/// <summary>Builds a frozen plan. Arrays are copied immediately.</summary>
		public KingdomNetworkPlan(string Key, string ResourceKey,
			KingdomExtensionNetworkNode[] Nodes, KingdomExtensionNetworkEdge[] Edges)
		{
			this.Key = Key;
			this.ResourceKey = ResourceKey;
			nodes = Copy(Nodes);
			edges = Copy(Edges);
		}

		/// <summary>Node count.</summary>
		public int NodeCount { get { return nodes.Length; } }
		/// <summary>Edge count.</summary>
		public int EdgeCount { get { return edges.Length; } }

		/// <summary>Reads one copied node; false out of range.</summary>
		public bool TryNode(int Index, out KingdomExtensionNetworkNode Node)
		{
			Node = default(KingdomExtensionNetworkNode);
			if (Index < 0 || Index >= nodes.Length) return false;
			Node = nodes[Index];
			return true;
		}

		/// <summary>Reads one copied edge; false out of range.</summary>
		public bool TryEdge(int Index, out KingdomExtensionNetworkEdge Edge)
		{
			Edge = default(KingdomExtensionNetworkEdge);
			if (Index < 0 || Index >= edges.Length) return false;
			Edge = edges[Index];
			return true;
		}

		private static KingdomExtensionNetworkNode[] Copy(KingdomExtensionNetworkNode[] source)
		{
			if (source == null || source.Length == 0) return new KingdomExtensionNetworkNode[0];
			KingdomExtensionNetworkNode[] copy = new KingdomExtensionNetworkNode[source.Length];
			Array.Copy(source, copy, source.Length);
			return copy;
		}

		private static KingdomExtensionNetworkEdge[] Copy(KingdomExtensionNetworkEdge[] source)
		{
			if (source == null || source.Length == 0) return new KingdomExtensionNetworkEdge[0];
			KingdomExtensionNetworkEdge[] copy = new KingdomExtensionNetworkEdge[source.Length];
			Array.Copy(source, copy, source.Length);
			return copy;
		}
	}
}
