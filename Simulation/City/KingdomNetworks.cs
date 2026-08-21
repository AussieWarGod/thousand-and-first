using System;
using System.Collections.Generic;

using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One zone's share of the liquid lines standing in it: what each component carries, which
	/// zone row it feeds, how much it will pass in a day, and which of the zone's four edges it
	/// declares across.
	/// <para>
	/// Derived state, and deliberately not saved. LIVING-CITY-ARCHITECTURE &sect;3.11: <i>"the graph
	/// is rebuilt from the ground the next time that zone renders"</i>. What persists is the
	/// DECLARATION, and it persists where the founder put it &mdash; on the conduit objects
	/// themselves, which the engine saves with the zone. Storing a second copy in the model would
	/// be storing a fact we would then have to hold in step with the ground it came from.
	/// </para>
	/// </summary>
	internal sealed class KingdomZoneLine
	{
		internal readonly string ZoneId;

		internal readonly string Liquid;

		/// <summary>The narrowest segment on this component, in drams a day.</summary>
		internal readonly int CapacityPerDay;

		/// <summary>The worst condition on the component. Addendum 10(b): a cracked main carries
		/// less rather than the same.</summary>
		internal readonly int ConditionPercent;

		/// <summary>Which of this zone's edges the component declares across, as a
		/// <c>KingdomNetworkRules</c> join mask.</summary>
		internal readonly int EdgeMask;

		/// <summary>How many taps bind a vessel on this component. A line with no tap is a line
		/// that reaches nothing, which is legal and does nothing.</summary>
		internal readonly int Taps;

		internal KingdomZoneLine(string zoneId, string liquid, int capacityPerDay, int conditionPercent, int edgeMask, int taps)
		{
			ZoneId = zoneId;
			Liquid = liquid;
			CapacityPerDay = capacityPerDay;
			ConditionPercent = conditionPercent;
			EdgeMask = edgeMask;
			Taps = taps;
		}
	}

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
	public static class KingdomNetworks
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
			foreach (GameObject item in Z.GetObjects())
			{
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

		/// <summary>
		/// The piece a declaration in <paramref name="direction"/> actually reaches: the immediate
		/// neighbour, or &mdash; through any run of crossovers &mdash; the first piece beyond them.
		/// <para>
		/// A crossover is transparent to the run that enters its paired face and opaque to
		/// everything else, which is exactly what <i>"lines cross in one tile without merging"</i>
		/// means. The walk is bounded by the zone's own width so a mis-declared ring of crossovers
		/// cannot spin.
		/// </para>
		/// </summary>
		private static int Through(Zone Z, Dictionary<int, int> atCell, List<GameObject> pieces, int x, int y, int direction)
		{
			int step = direction;
			int atX = x;
			int atY = y;
			for (int hops = 0; hops <= Z.Width; hops++)
			{
				switch (step)
				{
				case KingdomNetworkRules.JoinNorth:
					atY--;
					break;
				case KingdomNetworkRules.JoinSouth:
					atY++;
					break;
				case KingdomNetworkRules.JoinEast:
					atX++;
					break;
				default:
					atX--;
					break;
				}
				int index;
				if (atX < 0 || atY < 0 || atX >= Z.Width || atY >= Z.Height || !atCell.TryGetValue(atX * 1000 + atY, out index))
				{
					return -1;
				}
				r_KingdomLiquidCrossover crossing = pieces[index].GetPart<r_KingdomLiquidCrossover>();
				if (crossing == null)
				{
					return index;
				}
				// Arrived by the face opposite the way we were travelling; the piece carries us on
				// only if it pairs that face through.
				int exit = KingdomNetworkRules.CrossoverExit(crossing.PairMask, KingdomNetworkRules.OppositeJoin(step));
				if (exit == 0)
				{
					return -1;
				}
				step = KingdomNetworkRules.OppositeJoin(exit);
			}
			return -1;
		}

		/// <returns>Whether this refusal reached the founder's own register, so the caller can
		/// hold itself to one a composition.</returns>
		private static bool Refuse(GameObject piece, KingdomJoinVerdict verdict, string mine, string theirs, bool alreadySpoken)
		{
			string line = KingdomNetworkRules.RefusalLine(verdict, mine, theirs);
			if (string.IsNullOrEmpty(line))
			{
				return false;
			}
			// STANDARDS 7b, and the latch is on the PIECE rather than on the settlement: each
			// length of main remembers its own telling, so a dormant city keeps that memory with no
			// field on the system, and a founder who lays a second bad join is told about that one.
			r_KingdomLiquidConduit conduit = piece.GetPart<r_KingdomLiquidConduit>();
			if (conduit != null)
			{
				if (conduit.RefusalAnnounced)
				{
					return false;
				}
				conduit.RefusalAnnounced = true;
			}
			else
			{
				r_KingdomLiquidTap tap = piece.GetPart<r_KingdomLiquidTap>();
				if (tap == null || tap.RefusalAnnounced)
				{
					return false;
				}
				tap.RefusalAnnounced = true;
			}
			// The log takes every one of them, because the log is for everything and the register
			// is for what the founder can still do something about (§3.1's own split).
			KingdomLog.Log("network: " + line);
			if (alreadySpoken)
			{
				return false;
			}
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			if (system != null && system.Founded)
			{
				system.Ledger.Note("{{r|" + line + "}}");
			}
			return true;
		}

		private static string LiquidOf(GameObject piece)
		{
			r_KingdomLiquidConduit conduit = piece.GetPart<r_KingdomLiquidConduit>();
			if (conduit != null)
			{
				return string.IsNullOrEmpty(conduit.Liquid) ? null : conduit.Liquid.Trim();
			}
			r_KingdomLiquidTap tap = piece.GetPart<r_KingdomLiquidTap>();
			if (tap != null)
			{
				return string.IsNullOrEmpty(tap.Liquid) ? null : tap.Liquid.Trim();
			}
			// A crossover types nothing, which is what makes it safe. It never begins a component
			// of its own; it is only ever walked through.
			return null;
		}

		private static int DeclarationOf(GameObject piece)
		{
			r_KingdomLiquidConduit conduit = piece.GetPart<r_KingdomLiquidConduit>();
			if (conduit != null)
			{
				return conduit.JoinMask;
			}
			r_KingdomLiquidTap tap = piece.GetPart<r_KingdomLiquidTap>();
			return (tap != null) ? tap.JoinMask : 0;
		}

		/// <summary>What one segment will pass in a day, read off its own vessel: the hydraulic
		/// family's segment-volume idiom, turned into a rate. A pipe that holds eight drams passes
		/// eight drams a turn's worth of running, and a day is what the model counts in.</summary>
		private static int CapacityOf(GameObject piece)
		{
			LiquidVolume volume = piece.GetPart<LiquidVolume>();
			if (volume == null || volume.MaxVolume <= 0)
			{
				return 0;
			}
			long perDay = (long)volume.MaxVolume * KingdomRules.TicksPerDay / 100L;
			return (perDay > int.MaxValue) ? int.MaxValue : (int)perDay;
		}

		private static long Narrowest(int[] bottleneck, int count)
		{
			long narrowest = 0L;
			for (int i = 0; i < count; i++)
			{
				if (bottleneck[i] <= 0 || bottleneck[i] == KingdomNetworkRules.Unlimited)
				{
					continue;
				}
				if (narrowest == 0L || bottleneck[i] < narrowest)
				{
					narrowest = bottleneck[i];
				}
			}
			return narrowest;
		}

		private static int Find(int[] parent, int index)
		{
			while (parent[index] != index)
			{
				parent[index] = parent[parent[index]];
				index = parent[index];
			}
			return index;
		}

		private static void Union(int[] parent, int a, int b)
		{
			int rootA = Find(parent, a);
			int rootB = Find(parent, b);
			if (rootA == rootB)
			{
				return;
			}
			// Lower root wins, so the component's identity is a stored fact about the ground rather
			// than an artefact of which piece the walk happened to reach first.
			if (rootA < rootB)
			{
				parent[rootB] = rootA;
			}
			else
			{
				parent[rootA] = rootB;
			}
		}
	}
}
