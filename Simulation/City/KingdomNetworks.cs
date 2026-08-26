using System;
using System.Collections.Generic;

using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The engine edge of &sect;3.11: reading declared topology off real ground, composing the model's
	/// graph rows from it, running the flow solve, and landing what the solve decided on real
	/// vessels.
	/// <para>
	/// <b>Two layers, and this file is the seam between them.</b> Attended, vanilla's own
	/// transmission family runs unchanged in the zone the founder is standing in &mdash; we add
	/// nothing to it and per Addendum 11(c) must not. What this composes is the model's graph, which
	/// exists because a vanilla network <b>cannot cross a zone boundary</b>: its flood-fill expands
	/// with <c>GetLocalCellFromDirection</c> (<c>D/XRL/World/Cell.cs:8051-8054</c>). For a city that
	/// spans zones the model graph is not an optimisation of vanilla's network, it is the only way
	/// a multi-zone network exists at all.
	/// </para>
	/// <para>
	/// <b>Nothing here runs at reckon.</b> Composing reads the ground, and reckon may not touch it
	/// (&sect;0.0(d)), so composition happens on a zone render and the stamp is what stops it
	/// happening twice. What reckon runs is the arithmetic in <c>KingdomFlowRules</c>, over rows
	/// this file already wrote.
	/// </para>
	/// </summary>
	public static partial class KingdomNetworks
	{
		/// <summary>
		/// What the heartbeat may move in one slice, in drams, where the founder can watch it.
		/// <para>
		/// Small on purpose. This is <b>rendering</b>: the model has already decided what the day's
		/// running comes to, and this is a founder seeing a main actually run rather than finding
		/// out from a number. LIVING-CITY-ARCHITECTURE &sect;3.6 budgets a slice at four breakpoint
		/// steps and one told line; a bounded dram move between two vessels in the zone under the
		/// founder's feet is cheaper than either and changes no row, because both vessels are in the
		/// same zone and the zone's total does not move.
		/// </para>
		/// </summary>
		public const int HeartbeatTransferDrams = 4;

		/// <summary>The lowest condition a segment may be in and still carry anything. Below it the
		/// main is a ruin with a hole in it, and vanilla agrees: <c>FindGrid</c> drops any conduit
		/// whose effective rate has fallen to nothing
		/// (<c>D/XRL/World/Parts/IPowerTransmission.cs:1149-1153</c>).</summary>
		public const int MinimumCarryingCondition = 1;

		/// <summary>
		/// The ground's topology stamp. Bumped by a conduit, crossover or tap arriving or leaving,
		/// and by nothing else &mdash; never a clock, never a stock level
		/// (LIVING-CITY-ARCHITECTURE &sect;3.11, and <c>KingdomDistanceMatrix.MarkDirty</c>'s
		/// identical discipline one lane over).
		/// </summary>
		private static long groundStamp = 1L;

		/// <summary>How many heartbeat slices pass between visible transfers. Derived state, not
		/// saved: a founder who reloads waits at most four in-game hours to see a main run, which
		/// is not a thing the save owes them.</summary>
		private const int SlicesPerVisibleTransfer = 4;

		private static long lastAttendTick;

		/// <summary>Per zone, the lines composed from it, and the stamp they were composed at.</summary>
		private static readonly Dictionary<string, KingdomZoneLine[]> Composed = new Dictionary<string, KingdomZoneLine[]>();

		private static readonly Dictionary<string, long> ComposedAt = new Dictionary<string, long>();

		public static bool Enabled => Options.GetOption("r_TAF_OptionPower") != "No";

		public static long TopologyStamp
		{
			get { return groundStamp; }
		}

		/// <summary>
		/// Something was laid, taken up or destroyed. Every composed graph is now suspect and will
		/// be rebuilt the next time its zone renders.
		/// </summary>
		public static void MarkTopologyChanged()
		{
			groundStamp++;
		}

		/// <summary>
		/// The lines standing in this zone, composed from the ground if the stamp has moved since
		/// they last were.
		/// <para>
		/// The composition walk is <c>O(objects + cells declared)</c> and happens on a zone render.
		/// It is deliberately not on the reckon path and deliberately not cheap-per-pass: the whole
		/// discipline of &sect;3.11 is that topology is expensive and rare, and flow is cheap and
		/// frequent.
		/// </para>
		/// </summary>
		internal static KingdomZoneLine[] Lines(KingdomSystem System, Zone Z)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || System.ClaimedZones == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return new KingdomZoneLine[0];
			}
			long at;
			KingdomZoneLine[] held;
			if (ComposedAt.TryGetValue(Z.ZoneID, out at) && at == groundStamp && Composed.TryGetValue(Z.ZoneID, out held))
			{
				return held;
			}
			KingdomZoneLine[] built = Compose(Z);
			Composed[Z.ZoneID] = built;
			ComposedAt[Z.ZoneID] = groundStamp;
			return built;
		}

		/// <summary>
		/// One pass of every liquid line the city can see, posted to the book.
		/// <para>
		/// The model half of 12(g). A line runs downhill and stops level
		/// (<c>KingdomFlowRules.TryChooseDownhill</c>), the move is posted as a CARRY &mdash; level
		/// and debt together on both rows, so <c>level - owed</c> is untouched and invariant I1
		/// holds by construction &mdash; and &sect;3.5's amortised reify is what later opens the
		/// real vessels in <c>KingdomDrainRules</c>' dedication order. Nothing is poured here.
		/// </para>
		/// </summary>
		/// <param name="Days">Whole world-day boundaries this pass is running, from
		/// <c>KingdomProductionRules.TryDaysBetween</c>. The one clock.</param>
		internal static KingdomCityState Run(KingdomSystem System, Zone Z, KingdomCityState state, long Days, out int nodeVisits)
		{
			nodeVisits = 0;
			if (!Enabled || state == null || System == null || Z == null || Days <= 0L)
			{
				return state;
			}
			List<KingdomNetworkGraph> graphs = new List<KingdomNetworkGraph>();
			List<int[]> members = new List<int[]>();
			if (!TryComposeGraphs(System, state, graphs, members))
			{
				return state;
			}
			KingdomCityState current = state;
			for (int g = 0; g < graphs.Count; g++)
			{
				KingdomNetworkGraph graph = graphs[g];
				int[] bottleneck = new int[graph.NodeCount];
				int spent;
				KingdomCityFault fault;
				if (!graph.TryBottleneck(bottleneck, out spent, out fault))
				{
					KingdomLog.Log("network: solve refused (" + fault + ") on line " + graph.NetworkId);
					continue;
				}
				nodeVisits += spent;
				long budget = Narrowest(bottleneck, graph.NodeCount);
				if (budget <= 0L)
				{
					continue;
				}
				if (budget > long.MaxValue / Days)
				{
					budget = long.MaxValue;
				}
				else
				{
					budget *= Days;
				}
				int from;
				int to;
				long amount;
				if (!KingdomFlowRules.TryChooseDownhill(current, KingdomStockKind.Water, members[g], members[g].Length, budget, out from, out to, out amount, out fault))
				{
					KingdomLog.Log("network: downhill refused (" + fault + ") on line " + graph.NetworkId);
					continue;
				}
				if (amount <= 0L || from < 0 || to < 0)
				{
					continue;
				}
				KingdomCityState next;
				long moved;
				if (!KingdomNetworkRules.TryPostTransfer(current, KingdomStockKind.Water, from, to, amount, out next, out moved, out fault))
				{
					KingdomLog.Log("network: carry refused (" + fault + ") on line " + graph.NetworkId);
					continue;
				}
				if (moved <= 0L)
				{
					continue;
				}
				current = next;
				KingdomLog.Log("network: line " + graph.NetworkId + " carried " + moved + " " + graph.LiquidId
					+ " downhill, nodes=" + graph.NodeCount + " edges=" + graph.EdgeCount + " visits=" + spent);
			}
			return current;
		}
	}
}
