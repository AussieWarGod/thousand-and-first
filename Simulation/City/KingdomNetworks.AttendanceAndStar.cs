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
		/// <summary>
		/// The visible half, where the founder is standing: a few drams actually crossing between
		/// two real vessels on one line.
		/// <para>
		/// <b>This changes no row and owes the book nothing.</b> Both vessels are in the same zone,
		/// so the zone's level and the zone's ground move by exactly zero &mdash; it is the same
		/// water in a different cask. Invariant I1 is untouched because nothing about it moved.
		/// One transfer a slice, bounded by <see cref="HeartbeatTransferDrams"/>.
		/// </para>
		/// </summary>
		/// <returns>Drams actually moved, measured from the vessels rather than assumed
		/// (STANDARDS &sect;1).</returns>
		public static int Attend(KingdomSystem System, Zone Z, long nowTick)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || nowTick <= 0L)
			{
				return 0;
			}
			// Two gates, and both are about not paying for something nobody laid.
			//
			// The first: a city with no plumbing costs exactly nothing here, because Lines() is a
			// dictionary hit whenever the topology stamp has not moved.
			//
			// The second: a survey walks the zone, and §0.0 budgets a slice at 0.3 ms. One survey
			// an in-game HOUR would be inside that on most machines and is not a bet worth taking
			// on all of them, so the visible transfer runs at a quarter of the slice cadence -- one
			// survey every four in-game hours in a city that actually has a main. The model has
			// already decided what the day comes to either way; this is only how often the founder
			// gets to watch it happen.
			if (Lines(System, Z).Length == 0)
			{
				return 0;
			}
			if (lastAttendTick > 0L && nowTick - lastAttendTick < KingdomBudgetRules.HeartbeatCadenceTicks * SlicesPerVisibleTransfer)
			{
				return 0;
			}
			lastAttendTick = nowTick;
			KingdomSurvey Survey = KingdomSurvey.Take(Z, System);
			if (Survey == null || Survey.Stores.Count < 2)
			{
				return 0;
			}
			LiquidVolume fullest = null;
			LiquidVolume emptiest = null;
			for (int i = 0; i < Survey.Stores.Count; i++)
			{
				LiquidVolume store = Survey.Stores[i];
				if (store == null || store.MaxVolume <= 0)
				{
					continue;
				}
				if (fullest == null || (long)store.Volume * fullest.MaxVolume > (long)fullest.Volume * store.MaxVolume)
				{
					fullest = store;
				}
				if (emptiest == null || (long)store.Volume * emptiest.MaxVolume < (long)emptiest.Volume * store.MaxVolume)
				{
					emptiest = store;
				}
			}
			if (fullest == null || emptiest == null || fullest == emptiest)
			{
				return 0;
			}
			// Levelling only. A main that pushed a cask past the one it was drawing from would be
			// running uphill, and a founder watching it would be right to call it a bug.
			if ((long)fullest.Volume * emptiest.MaxVolume <= (long)emptiest.Volume * fullest.MaxVolume)
			{
				return 0;
			}
			int want = HeartbeatTransferDrams;
			int room = emptiest.MaxVolume - emptiest.Volume;
			if (want > room)
			{
				want = room;
			}
			if (want <= 0)
			{
				return 0;
			}
			int drawn = KingdomLiquids.Drain(fullest, want);
			if (drawn <= 0)
			{
				return 0;
			}
			int landed = KingdomLiquids.Fill(emptiest, "water", drawn);
			if (landed < drawn)
			{
				// Measured, never trusted: whatever the receiving vessel would not take goes back
				// where it came from rather than evaporating into a rounding error.
				KingdomLiquids.Fill(fullest, "water", drawn - landed);
			}
			return landed;
		}

		/// <summary>
		/// A star graph over one set of nodes: what the power lane's network is, and why.
		/// <para>
		/// <c>KingdomPower</c>'s own remarks state the case and it stands: vanilla's
		/// <c>IPowerTransmission</c> grid is built from cardinal-adjacent runs of matching conduit
		/// (<c>FindGrid</c>, <c>D/XRL/World/Parts/IPowerTransmission.cs:1099-1211</c>), which would
		/// make the founder lay gearbox fence between a windmill and a charging post &mdash; a
		/// wiring puzzle this mod's automatic placement could not guarantee a solution to. The
		/// settlement's own carrying is the conduit instead: settlers walk the charge across, so
		/// every work and every post joins one hub with nothing between them to narrow it.
		/// </para>
		/// <para>
		/// It is a real graph row and not a special case: node 0 is the hub, every other node
		/// hangs off it, and the solve that runs on it is the same solve a laid line gets. That is
		/// the migration &mdash; one accounting, not two.
		/// </para>
		/// </summary>
		internal static bool TryStar(int networkId, KingdomNetworkNode[] nodes, int nodeCount, int capacityPerDay, out KingdomNetworkGraph graph, out KingdomCityFault fault)
		{
			graph = null;
			if (nodes == null || nodeCount < 1)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			KingdomNetworkEdge[] edges = new KingdomNetworkEdge[(nodeCount > 1) ? (nodeCount - 1) : 0];
			for (int i = 1; i < nodeCount; i++)
			{
				edges[i - 1] = new KingdomNetworkEdge(0, i, capacityPerDay, 100);
			}
			return KingdomNetworkGraph.TryBuild(networkId, KingdomNetworkKind.Electrical, null, groundStamp,
				nodes, nodeCount, edges, edges.Length, out graph, out fault);
		}

	}
}
