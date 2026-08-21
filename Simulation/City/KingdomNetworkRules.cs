using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What a network carries. The first four are vanilla's own transmission families, named the
	/// same way vanilla names them so a reader can put our row beside the engine's part and see one
	/// vocabulary; the fifth is ours, because vanilla has no liquid carrier that moves a dram.
	/// <para>
	/// The engine's five concrete families are <c>ElectricalPowerTransmission</c>
	/// (<c>D/XRL/World/Parts/ElectricalPowerTransmission.cs:6</c>),
	/// <c>HydraulicPowerTransmission</c> (<c>:6</c>), <c>MechanicalPowerTransmission</c> (<c>:6</c>),
	/// <c>BiomechanicalPowerTransmission</c> (<c>:6</c>) and <c>GenericPowerTransmission</c>
	/// (<c>:6</c>), each overriding <c>GetPowerTransmissionType()</c> with its own type string.
	/// <c>IPowerTransmission.GetCorrespondingPart</c> (<c>:1075-1087</c>) matches on that string, so
	/// <b>vanilla itself will not join two families</b> — the typed-line law below is the same rule
	/// carried one level further, down to the liquid.
	/// </para>
	/// </summary>
	internal enum KingdomNetworkKind : byte
	{
		Electrical = 0,
		Hydraulic = 1,
		Mechanical = 2,
		Biomechanical = 3,

		/// <summary>Ours. A typed liquid line: one liquid, declared, never inferred.</summary>
		Liquid = 4
	}

	/// <summary>What a node does on its network. LIVING-CITY-ARCHITECTURE &sect;3.11.</summary>
	internal enum KingdomNetworkRole : byte
	{
		/// <summary>Makes the thing. A power work, a well head, a condenser.</summary>
		Source = 0,

		/// <summary>Spends it. A mill, a charging post, a bath house.</summary>
		Sink = 1,

		/// <summary>Holds it between the making and the spending. A salt bed, a cistern.</summary>
		Store = 2
	}

	/// <summary>
	/// The brownout ladder, and it is a design statement rather than an ordering convenience.
	/// <b>Lower stops first.</b>
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.11 states the order —
	/// <i>industry &rarr; refining &rarr; amenity &rarr; food &rarr; water &rarr; defence and
	/// watch</i> — and says where it comes from: it is the mod's existing <i>stop at the loyal
	/// core</i> discipline, the thirst ladder's <i>"empty casks and one rung of the ladder, never an
	/// empty town"</i> (DECISIONS: <i>"Failure has a floor"</i>), applied to charge instead of
	/// drams. A city gives up what it is doing before it gives up what it is.
	/// </para>
	/// <para>
	/// <b>Where lodging sits, stated because it is the question the order is judged on.</b> Lodging
	/// is <see cref="Amenity"/> — the middle of the ladder, not the top and not the bottom. A roof
	/// needs no charge to keep the rain off; what a dwelling draws power for is comfort, and the
	/// closeness ladder and the roof brink are what actually govern a household
	/// (<c>KingdomLodgingRules</c>, <c>KingdomBrinkRules</c>). Putting lodging <i>last</i> would
	/// make a brownout able to condemn a home, which is the roof brink's business and nothing
	/// else's; putting it <i>first</i> would say a settlement stops housing people before it stops
	/// smelting, which is the opposite of everything the mod says about a city. So the forge and the
	/// smelter go quiet first, comfort third, and the things a city dies without — food, water, the
	/// watch — are the last to be given up, in that order, because a dark hungry city recovers and a
	/// city whose watch went dark on the night raiders came does not.
	/// </para>
	/// </summary>
	internal enum KingdomWorkTier : byte
	{
		Industry = 0,
		Refining = 1,
		Amenity = 2,
		Food = 3,
		Water = 4,
		Watch = 5
	}

	/// <summary>
	/// One node of a network: which work it is, what it does there, and the two figures the solve
	/// reads off it.
	/// <para>
	/// Fourteen declared bytes against the sixteen LIVING-CITY-ARCHITECTURE &sect;0.0(c) budgets.
	/// <b>What is deliberately not here is a level.</b> A store's contents are the ground's, read
	/// back at check-in and landed at reify in dedication order (&sect;3.9,
	/// <c>KingdomDrainRules</c>); the model keeps one aggregate per network and never a second copy
	/// of a number it would then have to hold in step with a container.
	/// </para>
	/// </summary>
	internal readonly struct KingdomNetworkNode
	{
		internal readonly int WorkId;

		internal readonly KingdomNetworkRole Role;

		/// <summary>The brownout ladder's rung for this node. Read only for a
		/// <see cref="KingdomNetworkRole.Sink"/>; carried on every node so the row has one shape.</summary>
		internal readonly KingdomWorkTier Tier;

		/// <summary>What a store can hold. Zero for a source or a sink.</summary>
		internal readonly int Capacity;

		/// <summary>A source's output a day, a sink's demand a day, a store's throughput a day.
		/// Never negative: which direction it points is the role's business, not the number's.</summary>
		internal readonly int RatePerDay;

		internal KingdomNetworkNode(int workId, KingdomNetworkRole role, KingdomWorkTier tier, int capacity, int ratePerDay)
		{
			WorkId = workId;
			Role = role;
			Tier = tier;
			Capacity = (capacity > 0) ? capacity : 0;
			RatePerDay = (ratePerDay > 0) ? ratePerDay : 0;
		}
	}

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

	/// <summary>
	/// What one attempt to join two segments came to. The LIQUID LAW's whole surface
	/// (BUILDING-CATALOGUE-BRIEF, 2026-08-22): <i>connection is DECLARED, never inferred</i>.
	/// </summary>
	internal enum KingdomJoinVerdict : byte
	{
		/// <summary>The two declared toward each other and agree on what they carry.</summary>
		Joined = 0,

		/// <summary>They share ground and neither declared a join, so they pass without meeting.
		/// The crossover piece's whole behaviour, and the default for any two segments that only
		/// happen to be adjacent.</summary>
		Crossed = 1,

		/// <summary>Two different families. An axle will not carry amps, and vanilla will not join
		/// them either (<c>IPowerTransmission.GetCorrespondingPart</c>,
		/// <c>D/XRL/World/Parts/IPowerTransmission.cs:1075-1087</c>).</summary>
		RefusedKind = 2,

		/// <summary>Two liquid lines carrying different liquids. <b>Refused by name, never
		/// merged</b> — mixtures are a future mixing work, and the no-silent-merge rule never
		/// bends.</summary>
		RefusedLiquid = 3,

		/// <summary>A liquid line that never said what it carries. An untyped line joins nothing:
		/// the law is <i>declared, never inferred</i>, and a blank declaration is not a
		/// declaration.</summary>
		RefusedUntyped = 4
	}

	/// <summary>
	/// Network graphs, the declared-topology law, and the traversal the flow solve runs on.
	/// <para>
	/// <b>Two layers, and this is the model one.</b> Attended, the founder's zone runs vanilla's own
	/// transmission family unchanged — that machinery already does network discovery
	/// (<c>IPowerTransmission.FindGrid</c>, <c>D/XRL/World/Parts/IPowerTransmission.cs:1099-1211</c>,
	/// a cardinal-only BFS over cells collecting <c>Producers</c>, <c>Consumers</c> and a
	/// <c>GridCapacity</c>), and charge delivery by event (<c>ChargeAvailableEvent</c> /
	/// <c>FinishChargeAvailableEvent</c> &rarr; <c>Process(E)</c>, <c>:383-393</c> and
	/// <c>:1698-1766</c>; demand gathered by <c>QueryChargeEvent</c> / <c>TestChargeEvent</c>,
	/// <c>:322-358</c>, each OR-ing the part's <c>GridBit</c> into the event's <c>GridMask</c>,
	/// which is the engine's own re-entrancy guard and the reason a cyclic grid terminates). Per
	/// Addendum 11(c) we do not reinvent any of it.
	/// </para>
	/// <para>
	/// <b>The one engine fact that forces a model layer at all.</b> That flood-fill expands with
	/// <c>GetLocalCellFromDirection</c>, which is <c>GetCellFromDirectionGlobal(..., bLocalOnly:
	/// true, ...)</c> (<c>D/XRL/World/Cell.cs:8051-8054</c>): <b>a vanilla network cannot cross a
	/// zone boundary.</b> For a city that spans zones the graph here is not an optimisation of
	/// vanilla's network, it is the only way a multi-zone network exists. Vanilla renders the part
	/// the founder is standing in; this owns the whole of it.
	/// </para>
	/// <para>
	/// <b>Topology changes only on placement</b>, never on time and never on stock — the identical
	/// cache discipline <c>KingdomDistanceRules</c> keeps. A graph carries the ground stamp it was
	/// built from; placing, removing or destroying a conduit or a node bumps the ground's stamp and
	/// <see cref="NeedsRebuild"/> then answers true. The rebuild reads the ground, so it happens
	/// when a zone renders and <b>never at reckon</b> (&sect;0.0(d)).
	/// </para>
	/// <para>
	/// Pure and engine-free, and total over representable input.
	/// </para>
	/// </summary>
	internal static class KingdomNetworkRules
	{
		/// <summary>LIVING-CITY-ARCHITECTURE &sect;3.11 / &sect;0.0(c).</summary>
		internal const int MaxNodes = KingdomBudgetRules.NetworkMaxNodes;

		/// <summary>LIVING-CITY-ARCHITECTURE &sect;3.11 / &sect;0.0(c).</summary>
		internal const int MaxEdges = KingdomBudgetRules.NetworkMaxEdges;

		/// <summary>LIVING-CITY-ARCHITECTURE &sect;3.11 / &sect;0.0(c).</summary>
		internal const int MaxNetworksPerCity = KingdomBudgetRules.NetworksPerCity;

		/// <summary>A node the traversal reached from no edge: a source, or a node nothing
		/// reaches. Two hundred and fifty-five, because a node index is at most thirty-one and an
		/// edge index at most forty-seven, so a byte carries both with a sentinel to spare.</summary>
		internal const byte NoParent = 255;

		/// <summary>
		/// What a node's bottleneck reads as when nothing between it and its source narrows —
		/// which is what a source itself always is. Saturating rather than wrapping: the solve
		/// takes a minimum against it and never adds to it.
		/// </summary>
		internal const int Unlimited = int.MaxValue;

		/// <summary>
		/// The solve's op ceiling for one network, LIVING-CITY-ARCHITECTURE &sect;0.0 /
		/// &sect;3.11: <c>O(nodes + edges)</c>, at most 32 + 48 = <b>80 node-visits</b>. Asserted
		/// rather than asserted-about: <see cref="KingdomNetworkGraph.TryBottleneck"/> reports what
		/// it actually spent and the test compares it to this.
		/// </summary>
		internal static int MaxSolveVisits(int nodeCount, int edgeCount)
		{
			int nodes = (nodeCount > 0) ? nodeCount : 0;
			int edges = (edgeCount > 0) ? edgeCount : 0;
			return nodes + edges;
		}

		/// <summary>North, in the four-bit declaration mask. The four cardinals and no diagonal,
		/// because that is the walk vanilla's own network discovery makes
		/// (<c>Cell.DirectionListCardinalOnly</c>, <c>D/XRL/World/Cell.cs:328</c>, read at
		/// <c>D/XRL/World/Parts/IPowerTransmission.cs:1189-1197</c>) and a founder should not have
		/// to learn two rules about what touches what.</summary>
		internal const int JoinNorth = 1;

		internal const int JoinSouth = 2;

		internal const int JoinEast = 4;

		internal const int JoinWest = 8;

		/// <summary>All four. What an ordinary length of main declares.</summary>
		internal const int JoinAll = JoinNorth | JoinSouth | JoinEast | JoinWest;

		/// <summary>
		/// Reads a segment's declaration off its XML: <c>"NS"</c>, <c>"EW"</c>, <c>"NSEW"</c>, or
		/// the empty string for a segment that joins nothing at all.
		/// <para>
		/// <b>Third-party XML is untrusted and this refuses rather than defaults</b>, the same way
		/// <c>KingdomPowerRules.TryParseSource</c> does: a misspelt declaration makes a segment
		/// that joins nothing, which is inert and visible, rather than one that quietly joins
		/// everything, which is a silent merge and the one thing the LIQUID LAW forbids outright.
		/// </para>
		/// </summary>
		internal static bool TryParseJoins(string text, out int mask)
		{
			mask = 0;
			if (text == null)
			{
				return false;
			}
			string trimmed = text.Trim();
			if (trimmed.Length == 0)
			{
				// Declared nothing, and that is a legal declaration: a capped end.
				return true;
			}
			for (int i = 0; i < trimmed.Length; i++)
			{
				switch (char.ToUpperInvariant(trimmed[i]))
				{
				case 'N':
					mask |= JoinNorth;
					break;
				case 'S':
					mask |= JoinSouth;
					break;
				case 'E':
					mask |= JoinEast;
					break;
				case 'W':
					mask |= JoinWest;
					break;
				default:
					mask = 0;
					return false;
				}
			}
			return true;
		}

		/// <summary>The other end of one cardinal. Zero for anything that is not exactly one of
		/// the four.</summary>
		internal static int OppositeJoin(int single)
		{
			switch (single)
			{
			case JoinNorth:
				return JoinSouth;
			case JoinSouth:
				return JoinNorth;
			case JoinEast:
				return JoinWest;
			case JoinWest:
				return JoinEast;
			default:
				return 0;
			}
		}

		/// <summary>
		/// Whether two neighbouring segments have <b>both</b> declared toward each other.
		/// <para>
		/// This is the whole of <i>declared, never inferred</i>, and the reason it takes both masks
		/// is that one segment's declaration is an offer and not a connection. Tile adjacency is
		/// not consulted anywhere: the caller has already established that these two are neighbours
		/// in <paramref name="direction"/>, and being neighbours is exactly what does not join
		/// them.
		/// </para>
		/// </summary>
		internal static bool DeclaredToward(int mineMask, int theirMask, int direction)
		{
			int back = OppositeJoin(direction);
			if (back == 0)
			{
				return false;
			}
			return (mineMask & direction) != 0 && (theirMask & back) != 0;
		}

		/// <summary>
		/// Which way a crossover carries something that arrived from <paramref name="from"/>, or
		/// zero when it carries nothing that way.
		/// <para>
		/// A crossover pairs opposite cardinals and <b>nothing else</b>: what comes in the north
		/// face leaves by the south, what comes in the east leaves by the west, and neither pair
		/// ever meets the other. That is the piece's entire behaviour and the reason it needs no
		/// liquid of its own — it types nothing, so it can never merge anything.
		/// </para>
		/// </summary>
		internal static int CrossoverExit(int pairsMask, int from)
		{
			int back = OppositeJoin(from);
			if (back == 0)
			{
				return 0;
			}
			// Both faces of the pair must be declared, or the piece is a dead end that way rather
			// than a through-route: half a crossover carries nothing.
			return ((pairsMask & from) != 0 && (pairsMask & back) != 0) ? back : 0;
		}

		/// <summary>
		/// Whether a network's graph must be rebuilt from the ground before it can be believed.
		/// <para>
		/// The only three things that may bump a ground stamp are the three
		/// <c>KingdomDistanceMatrix.MarkDirty</c> permits by the same argument: a conduit or node
		/// placed, one removed, one destroyed. <b>Never a clock and never a stock level.</b> A null
		/// graph always needs building, which is what makes "no graph yet" and "stale graph" the
		/// same caller-side branch instead of two.
		/// </para>
		/// </summary>
		internal static bool NeedsRebuild(KingdomNetworkGraph graph, long groundStamp)
		{
			return graph == null || graph.TopologyStamp != groundStamp;
		}

		/// <summary>
		/// The LIQUID LAW, as one total function.
		/// <para>
		/// BUILDING-CATALOGUE-BRIEF, 2026-08-22: <i>"connection is DECLARED, never inferred. (1)
		/// Typed lines — one liquid per network; a cross-liquid join REFUSES by name, never merges.
		/// (2) Explicit topology — segments join by declaration, not tile adjacency, so lines cross
		/// in one tile via crossover pieces without merging."</i>
		/// </para>
		/// <para>
		/// Note the shape of the first branch: an <b>undeclared</b> pair is
		/// <see cref="KingdomJoinVerdict.Crossed"/> and not a refusal, because two lines sharing a
		/// tile is an ordinary, legal, intended thing — it is what a crossover piece is for. A
		/// refusal is reserved for a join somebody actually asked for and cannot have, so that the
		/// telling means something when it fires.
		/// </para>
		/// </summary>
		/// <param name="declared">Whether both segments declared a join toward each other. Anything
		/// less than both is not a declaration.</param>
		internal static KingdomJoinVerdict JudgeJoin(bool declared, KingdomNetworkKind kindA, string liquidA, KingdomNetworkKind kindB, string liquidB)
		{
			if (!declared)
			{
				return KingdomJoinVerdict.Crossed;
			}
			if (kindA != kindB)
			{
				return KingdomJoinVerdict.RefusedKind;
			}
			if (kindA != KingdomNetworkKind.Liquid)
			{
				// Electrical, hydraulic, mechanical: mixing is not a concept in these families, so
				// a declared same-family join is simply a join. Vanilla's own adjacency stands
				// where it stands and we add nothing to it.
				return KingdomJoinVerdict.Joined;
			}
			if (string.IsNullOrEmpty(liquidA) || string.IsNullOrEmpty(liquidB))
			{
				return KingdomJoinVerdict.RefusedUntyped;
			}
			return LiquidsMatch(liquidA, liquidB) ? KingdomJoinVerdict.Joined : KingdomJoinVerdict.RefusedLiquid;
		}

		/// <summary>
		/// Whether two declared liquid names are the same liquid. Case-insensitive ordinal, because
		/// vanilla's liquid ids are plain lowercase strings — <c>"water"</c>, <c>"salt"</c>,
		/// <c>"blood"</c>, declared as <c>public new const string ID</c> on each
		/// <c>BaseLiquid</c> subclass (<c>D/XRL/Liquids/LiquidWater.cs:17</c> and siblings) — and a
		/// founder writing <c>Water</c> in XML meant water.
		/// <para>
		/// An empty name matches nothing, including another empty name. <i>Declared, never
		/// inferred</i>: two lines that both failed to say what they carry have not agreed on
		/// anything.
		/// </para>
		/// </summary>
		internal static bool LiquidsMatch(string liquidA, string liquidB)
		{
			if (string.IsNullOrEmpty(liquidA) || string.IsNullOrEmpty(liquidB))
			{
				return false;
			}
			return string.Equals(liquidA.Trim(), liquidB.Trim(), StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// The sentence a refused join is told with. <b>Named by liquid</b>, per the law: the
		/// founder is told which two lines would not meet, never that "a connection failed".
		/// <para>
		/// Empty for <see cref="KingdomJoinVerdict.Joined"/> and
		/// <see cref="KingdomJoinVerdict.Crossed"/>: nothing has gone wrong and 7b forbids telling
		/// somebody about the absence of a problem.
		/// </para>
		/// </summary>
		internal static string RefusalLine(KingdomJoinVerdict verdict, string liquidA, string liquidB)
		{
			switch (verdict)
			{
			case KingdomJoinVerdict.RefusedLiquid:
				return "The " + Named(liquidA) + " line will not join the " + Named(liquidB)
					+ " line. A line carries one liquid and only one. Lay a crossover if they are meant to pass.";
			case KingdomJoinVerdict.RefusedUntyped:
				return "That line has never been told what it carries, so it joins nothing. Say what runs in it first.";
			case KingdomJoinVerdict.RefusedKind:
				return "Those two do not belong to one another. A shaft carries no charge and a wire carries no water.";
			default:
				return "";
			}
		}

		/// <summary>A liquid's name as a founder would say it, or an honest word when the line
		/// never declared one.</summary>
		internal static string Named(string liquid)
		{
			return string.IsNullOrEmpty(liquid) ? "unnamed" : liquid.Trim().ToLowerInvariant();
		}

		/// <summary>
		/// A network transfer posted to the book. <b>A transfer is a carry</b>: level and debt move
		/// together, on both sides, in one publish.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.9 and invariant I1 — <i>model total == ground total +
		/// counter-owed, per stock kind, at every instant</i>. Nothing physical has happened yet: a
		/// main that ran a hundred drams from the granary zone's cistern to the forge zone's has
		/// changed no container. So the giving row loses a hundred from its LEVEL and a hundred
		/// from its DEBT, and the taking row gains a hundred of each, which leaves
		/// <c>level - owed</c> — the ground — untouched on both, and leaves the city's totals of
		/// both untouched as well. &sect;3.5's amortised reify is what later opens the real vessels,
		/// in <c>KingdomDrainRules</c>' dedication order, and 12(d)'s <i>containers hold true
		/// numbers</i> is that landing rather than a second ledger.
		/// </para>
		/// <para>
		/// This is <c>KingdomCityRules.TryApplyTransfer</c>'s identity, stated for the case where
		/// <b>both</b> ends are model rows because neither zone is under the founder's feet. That
		/// one posts only the giving side, because its taking side is the seated zone whose real
		/// containers took the goods in the same breath and were measured doing it.
		/// </para>
		/// </summary>
		/// <param name="amount">What the line wants to move. Clamped to what the giver can spare
		/// and what the taker has room for; the clamped figure is reported as
		/// <paramref name="moved"/> rather than being silently the same number.</param>
		internal static bool TryPostTransfer(
			KingdomCityState state,
			KingdomStockKind kind,
			int fromZoneIndex,
			int toZoneIndex,
			long amount,
			out KingdomCityState next,
			out long moved,
			out KingdomCityFault fault)
		{
			next = state;
			moved = 0L;
			if (state == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (fromZoneIndex == toZoneIndex)
			{
				// Loud rather than a quiet no-op: a line that believes it is moving water from a
				// zone to itself has a topology bug, and a silent zero would hide it.
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			KingdomZoneRow giver;
			KingdomZoneRow taker;
			if (!state.TryZone(fromZoneIndex, out giver) || !state.TryZone(toZoneIndex, out taker))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			KingdomStockPair fromPair;
			KingdomStockPair toPair;
			if (!giver.Stocks.TryGet(kind, out fromPair) || !taker.Stocks.TryGet(kind, out toPair))
			{
				fault = KingdomCityFault.InvalidRate;
				return false;
			}
			fault = KingdomCityFault.None;
			if (amount <= 0L)
			{
				return true;
			}
			// A zone already owing a draw has that much of its level spoken for: the vessels have
			// not paid it yet, so a main may not carry it away a second time. The same guard
			// KingdomCityRules.TryPlanTransfer keeps, for the same reason.
			long spokenFor = (giver.OwedOf(kind) < 0) ? -(long)giver.OwedOf(kind) : 0L;
			long spare = fromPair.Level - spokenFor;
			if (spare < 0L)
			{
				spare = 0L;
			}
			long room = toPair.Capacity - toPair.Level;
			if (room < 0L)
			{
				room = 0L;
			}
			long take = amount;
			if (take > spare)
			{
				take = spare;
			}
			if (take > room)
			{
				take = room;
			}
			if (take <= 0L)
			{
				return true;
			}
			long nextGiverOwed = (long)giver.OwedOf(kind) - take;
			long nextTakerOwed = (long)taker.OwedOf(kind) + take;
			if (nextGiverOwed < int.MinValue || nextTakerOwed > int.MaxValue)
			{
				fault = KingdomCityFault.ArithmeticOverflow;
				return false;
			}
			KingdomStocks lowered;
			KingdomStocks raised;
			if (!giver.Stocks.TryWith(kind, new KingdomStockPair(fromPair.Level - take, fromPair.Capacity), out lowered)
				|| !taker.Stocks.TryWith(kind, new KingdomStockPair(toPair.Level + take, toPair.Capacity), out raised))
			{
				fault = KingdomCityFault.InvalidRate;
				return false;
			}
			KingdomCityState afterGiver;
			KingdomCityState afterTaker;
			if (!state.TryWithZone(
					fromZoneIndex,
					giver.WithReading(giver.LastReadTick, lowered, giver.Roofs, giver.Defence, giver.WaterCarry, giver.FoodCarry)
						.WithOwedOf(kind, (int)nextGiverOwed),
					out afterGiver,
					out fault))
			{
				return false;
			}
			if (!afterGiver.TryZone(toZoneIndex, out taker))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (!afterGiver.TryWithZone(
					toZoneIndex,
					taker.WithReading(taker.LastReadTick, raised, taker.Roofs, taker.Defence, taker.WaterCarry, taker.FoodCarry)
						.WithOwedOf(kind, (int)nextTakerOwed),
					out afterTaker,
					out fault))
			{
				return false;
			}
			next = afterTaker;
			moved = take;
			return true;
		}
	}

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
	internal sealed class KingdomNetworkGraph
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
