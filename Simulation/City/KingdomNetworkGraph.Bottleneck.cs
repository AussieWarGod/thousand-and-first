using System;

namespace ThousandAndFirst.Simulation.City
{
	internal sealed partial class KingdomNetworkGraph
	{
		/// <summary>
		/// What each node can actually be fed, in a day: the narrowest segment between it and
		/// whatever source reaches it.
		/// <para>
		/// <b>The bottleneck relaxation, deliberately not max-flow</b>
		/// (LIVING-CITY-ARCHITECTURE &sect;3.11). Player-laid conduit is essentially a tree; a true
		/// max-flow is <c>O(V&middot;E&sup2;)</c>, buys nothing a player can perceive, and the
		/// relaxation is <b>conservative</b> — it can understate throughput, never overstate it, so
		/// it can never manufacture supply. That is the right direction for an error to point, and
		/// it is also vanilla's own answer: <c>FindGrid</c> reduces <c>GridCapacity</c> to the
		/// minimum effective charge rate on the grid
		/// (<c>D/XRL/World/Parts/IPowerTransmission.cs:1172-1175</c>) and then hands that one figure
		/// to every member (<c>:1201-1210</c>). We are narrower than vanilla, per path rather than
		/// per grid, and never wider.
		/// </para>
		/// <para>
		/// One linear pass over the precomputed order. <paramref name="nodeVisits"/> is what it
		/// spent, in the unit &sect;0.0's network lane counts: one per node settled, one per edge
		/// read. It is bounded by <c>2&middot;reached - sources</c> and therefore by
		/// <c>nodes + edges</c>, which the receipt checks rather than assumes.
		/// </para>
		/// </summary>
		/// <param name="bottleneck">Filled per node index: what may reach it in a day.
		/// <c>0</c> for a node nothing reaches, <see cref="KingdomNetworkRules.Unlimited"/> for a
		/// source.</param>
		internal bool TryBottleneck(int[] bottleneck, out int nodeVisits, out KingdomCityFault fault)
		{
			nodeVisits = 0;
			if (bottleneck == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (bottleneck.Length < nodes.Length)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			for (int i = 0; i < nodes.Length; i++)
			{
				bottleneck[i] = 0;
			}
			for (int i = 0; i < order.Length; i++)
			{
				int at = order[i];
				nodeVisits++;
				byte parent = parentEdge[at];
				if (parent == KingdomNetworkRules.NoParent)
				{
					bottleneck[at] = KingdomNetworkRules.Unlimited;
					continue;
				}
				if (parent >= edges.Length)
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				nodeVisits++;
				KingdomNetworkEdge edge = edges[parent];
				int from = (edge.NodeA == at) ? edge.NodeB : edge.NodeA;
				int through = edge.EffectiveCapacityPerDay;
				int upstream = bottleneck[from];
				bottleneck[at] = (upstream < through) ? upstream : through;
			}
			fault = KingdomCityFault.None;
			return true;
		}
	}
}
