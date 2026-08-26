using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One edge: a declared join between two nodes, and what it will carry.
	/// <para>
	/// Sixteen declared bytes, exactly the &sect;0.0(c) budget.
	/// </para>
	/// <para>
	/// <b>The arcology decision, and it is this row's shape rather than any machinery.</b> An edge
	/// names its two endpoints and <i>nothing about who provided it</i> — no conduit id, no cell, no
	/// object. That is a schema decision and it is deliberate: the backlog's arcology spine (a
	/// megastructure whose risers ARE the network, BUILDING-CATALOGUE-BRIEF 2026-08-22, <i>"shell as
	/// backbone: riser taps on every floor — interior network segments join the spine's 12(g) graph
	/// edges for free"</i>) needs a network whose edges a <i>building</i> declares, not a player-laid
	/// segment. Because provenance is absent from the row, a shell can declare edges between its
	/// floors' nodes with no schema change and no second edge kind. <b>Nothing here builds hosted
	/// plots</b>, and nothing here should be read as having started them — this is only the
	/// negative fact that the row does not preclude them. Removal needs no provenance either: a
	/// removal bumps the topology stamp and the whole graph is rebuilt from the ground (&sect;3.11).
	/// </para>
	/// </summary>
	internal readonly struct KingdomNetworkEdge
	{
		internal readonly int NodeA;

		internal readonly int NodeB;

		/// <summary>What this segment will carry in a day, before wear.</summary>
		internal readonly int CapacityPerDay;

		/// <summary>The segment's own condition, 0-100. Addendum 10(b): typed wear consequences
		/// reach the carrier too, so a cracked main carries less rather than the same.</summary>
		internal readonly int ConditionPercent;

		internal KingdomNetworkEdge(int nodeA, int nodeB, int capacityPerDay, int conditionPercent)
		{
			NodeA = nodeA;
			NodeB = nodeB;
			CapacityPerDay = (capacityPerDay > 0) ? capacityPerDay : 0;
			ConditionPercent = (conditionPercent < 0) ? 0 : ((conditionPercent > 100) ? 100 : conditionPercent);
		}

		/// <summary>What this segment actually carries in a day, wear applied.</summary>
		internal int EffectiveCapacityPerDay
		{
			get { return (int)((long)CapacityPerDay * ConditionPercent / 100L); }
		}
	}
}
