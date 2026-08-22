using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What one check-in found the ground holding, in the model's own units. The engine edge fills
	/// this from <c>KingdomSurvey</c>; nothing downstream of it touches a <c>Zone</c>.
	/// </summary>
	internal readonly struct KingdomGroundReading
	{
		internal readonly long WaterLevel;

		internal readonly long WaterCapacity;

		internal readonly long FoodLevel;

		internal readonly long FoodCapacity;

		internal readonly int Defence;

		internal KingdomGroundReading(long waterLevel, long waterCapacity, long foodLevel, long foodCapacity, int defence)
		{
			WaterLevel = waterLevel;
			WaterCapacity = waterCapacity;
			FoodLevel = foodLevel;
			FoodCapacity = foodCapacity;
			Defence = defence;
		}
	}

	/// <summary>
	/// One span of model time, frozen, as the reckon job receives it.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;2.5: a job may not read the clock, so the span arrives as two
	/// ticks. Every field is <c>readonly</c> and every type in the closure is ours or the
	/// framework's, which is what <c>KingdomComputeSeam</c> checks before this crosses.
	/// </para>
	/// </summary>
	internal readonly struct KingdomReckonInput
	{
		internal readonly KingdomCityState State;

		internal readonly long ToTick;

		internal KingdomReckonInput(KingdomCityState state, long toTick)
		{
			State = state;
			ToTick = toTick;
		}
	}

	/// <summary>
	/// The city book as something <c>KingdomAdvanceRules</c> can run forward.
	/// <para>
	/// <b>The shape</b> is the whole of &sect;2.3 — one propose pass, one apply pass, a closed-form
	/// crossing rather than a search, and an honest jump to the fixed point when the breakpoint
	/// budget runs out.
	/// </para>
	/// <para>
	/// <b>The rates, and why they can only exist once (W6).</b> W1 shipped this with a net rate of
	/// zero and said why: the attended pass already credited the seated zone's works for the
	/// settlement's whole elapsed off <c>KingdomGrowth</c>'s settlement-wide <c>LastWaterWorkTick</c>,
	/// so a model that also credited them here would pay the same day twice. W6 does not add a
	/// second accounting beside that one — it <b>moves</b> it. Every zone's per-day make is
	/// measured onto its own row at the pass that reads it
	/// (<c>KingdomZoneRow.WaterCarry</c> / <c>FoodCarry</c>), the model integrates all of them off
	/// its ONE clock, and the settlement pass credits nothing. One accounting; two renderings —
	/// unattended ground fills its book, and attended ground has the same drams poured into real
	/// vessels by &sect;3.5's amortised reify.
	/// </para>
	/// <para>
	/// Days are counted as <b>world-day boundaries crossed</b>
	/// (<c>KingdomProductionRules.TryDaysBetween</c>), never as elapsed divided by a day, so
	/// splitting a span at a breakpoint reaches the same total as integrating it whole and a
	/// horizon that lands mid-day loses nothing.
	/// </para>
	/// <para>
	/// A rate is now a real per-day term, so a reckoning over ninety days spends more passes than
	/// one over a day whenever a stock crosses — which is what finally gives &sect;0.0(a)'s
	/// 1-day-vs-90-day assertion something to bite on.
	/// </para>
	/// </summary>
	internal sealed class KingdomCityAdvanceable : IKingdomAdvanceable<KingdomCityState>
	{
		private readonly long ticksPerDay;

		private readonly int[] waterRatePerDay;

		private readonly int[] foodRatePerDay;

		/// <summary>
		/// The realm's method, as <c>KingdomResearch.MethodPercent</c> reads it off the keepers'
		/// roster. Handed in and never derived, because this class may not read a realm: it is one
		/// number for the whole book, exactly as it is one number for the whole yard floor
		/// (<c>KingdomMaterials.OnSettlementPass</c>), because the keepers write to each other.
		/// </summary>
		private readonly int methodPercent;

		/// <summary>
		/// Rates may be handed in per zone row, in row order, so a test can drive the integration
		/// over a chosen crossing. A null or short array reads as <b>the row's own measured
		/// carry</b>: the runtime supplies no override at all, because the rate a zone runs at is a
		/// fact about that zone's works and belongs on that zone's row rather than in a parallel
		/// array somebody has to keep in step with it.
		/// <para>
		/// This overload is the realm that has researched nothing, and it is what every caller who
		/// does not care about method gets: <c>KingdomProductionRules.BaselineMethodPercent</c>
		/// changes no number anywhere.
		/// </para>
		/// </summary>
		internal KingdomCityAdvanceable(long ticksPerDay, int[] waterRatePerDay, int[] foodRatePerDay)
			: this(ticksPerDay, waterRatePerDay, foodRatePerDay, KingdomProductionRules.BaselineMethodPercent)
		{
		}

		/// <summary>
		/// The same model, told what the realm's keepers have worked out.
		/// </summary>
		/// <param name="methodPercent">RESEARCH-SYSTEM-DESIGN &sect;8.2's third factor. Anything at
		/// or below <c>KingdomProductionRules.BaselineMethodPercent</c> is the baseline, so no
		/// roster and no research reach exactly the numbers this model produced before the tree
		/// existed.</param>
		internal KingdomCityAdvanceable(long ticksPerDay, int[] waterRatePerDay, int[] foodRatePerDay, int methodPercent)
		{
			this.ticksPerDay = ticksPerDay;
			this.waterRatePerDay = waterRatePerDay;
			this.foodRatePerDay = foodRatePerDay;
			this.methodPercent = methodPercent;
		}

		/// <summary>What this zone makes in a day, per stock kind: the override if one was handed
		/// in for this row, and the row's own measured carry otherwise, with the keepers' method
		/// riding it.
		/// <para>
		/// The measured carry already composes crew and condition &mdash; it is
		/// <c>KingdomSubsidence.Supports</c>'s own tally, folded at
		/// <c>KingdomWearRules.WorkEffectiveness</c> on the pass that read the ground. Method is the
		/// THIRD factor and enters here rather than there, so it scales what the works MAKE without
		/// touching what they carry: the supported level a settlement can hold is a fact about its
		/// buildings, and no amount of knowledge adds a roof.
		/// </para>
		/// </summary>
		internal long WaterRateOf(KingdomZoneRow row, int index)
		{
			return Methoded(RateOf(waterRatePerDay, index, row.WaterCarry));
		}

		internal long FoodRateOf(KingdomZoneRow row, int index)
		{
			return Methoded(RateOf(foodRatePerDay, index, row.FoodCarry));
		}

		public int RowCount(KingdomCityState state)
		{
			return (state == null) ? 0 : state.RowCount;
		}

		public bool TryProposeNext(KingdomCityState state, long fromTick, long horizonTick, out KingdomBreakpoint breakpoint, out KingdomCityFault fault)
		{
			breakpoint = KingdomBreakpoint.None;
			if (state == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (ticksPerDay <= 0L)
			{
				fault = KingdomCityFault.InvalidInterval;
				return false;
			}
			int zones = state.ZoneCount;
			KingdomBreakpoint[] candidates = new KingdomBreakpoint[zones * 2];
			int count = 0;
			for (int i = 0; i < zones; i++)
			{
				KingdomZoneRow row;
				if (!state.TryZone(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				if (!TryCandidate(row.Stocks.Water, WaterRateOf(row, i), fromTick, i, candidates, ref count, out fault))
				{
					return false;
				}
				if (!TryCandidate(row.Stocks.Food, FoodRateOf(row, i), fromTick, i, candidates, ref count, out fault))
				{
					return false;
				}
			}
			KingdomAdvanceRules.TryEarliest(candidates, count, fromTick + 1L, horizonTick, out breakpoint, out fault);
			return fault == KingdomCityFault.None;
		}

		public bool TryApply(KingdomCityState state, KingdomBreakpoint breakpoint, out KingdomCityState next, out KingdomCityFault fault)
		{
			next = null;
			if (state == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			long days;
			if (!KingdomProductionRules.TryDaysBetween(state.ProcessedThroughTick, breakpoint.Tick, ticksPerDay, out days, out fault))
			{
				return false;
			}
			KingdomCityState current = state;
			for (int i = 0; i < current.ZoneCount; i++)
			{
				KingdomZoneRow row;
				if (!current.TryZone(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				KingdomProductionStep water;
				KingdomProductionStep food;
				if (!KingdomProductionRules.TryProduce(row.Stocks.Water.Level, row.Stocks.Water.Capacity, row.OwedWater, WaterRateOf(row, i), days, out water, out fault)
					|| !KingdomProductionRules.TryProduce(row.Stocks.Food.Level, row.Stocks.Food.Capacity, row.OwedFood, FoodRateOf(row, i), days, out food, out fault))
				{
					return false;
				}
				if (water.Landed == 0L && food.Landed == 0L)
				{
					continue;
				}
				KingdomStocks moved = new KingdomStocks(
					new KingdomStockPair(water.NextLevel, row.Stocks.Water.Capacity),
					new KingdomStockPair(food.NextLevel, row.Stocks.Food.Capacity),
					row.Stocks.Materials);
				// Level and debt move by the same amount, in one write. That is invariant I1 in a
				// single statement: the ground has not changed, so `level - owed` has not changed,
				// and what the works made is a claim on a vessel nobody has poured yet.
				KingdomCityState written;
				if (!current.TryWithZone(
						i,
						row.WithReading(row.LastReadTick, moved, row.Roofs, row.Defence, row.WaterCarry, row.FoodCarry)
							.WithOwed(water.NextOwed, food.NextOwed, row.OwedMaterials),
						out written,
						out fault))
				{
					return false;
				}
				current = written;
			}
			return current.TryWithProcessedThroughTick(breakpoint.Tick, out next, out fault);
		}

		/// <summary>
		/// The honest overflow of &sect;2.3: every rate in this model is linear and clamped, so the
		/// equilibrium is simply the whole remaining span integrated in one segment. Not a
		/// forgiveness cap — the same convergence, reached by arithmetic instead of by steps.
		/// </summary>
		public bool TryJumpToFixedPoint(KingdomCityState state, long throughTick, out KingdomCityState next, out KingdomCityFault fault)
		{
			return TryApply(state, KingdomBreakpoint.Horizon(throughTick), out next, out fault);
		}

		private bool TryCandidate(KingdomStockPair pair, long ratePerDay, long fromTick, int rowIndex, KingdomBreakpoint[] candidates, ref int count, out KingdomCityFault fault)
		{
			long ticksUntil;
			KingdomBreakpointKind kind;
			if (!KingdomAdvanceRules.TryCrossingTicks(pair.Level, pair.Capacity, ratePerDay, ticksPerDay, out ticksUntil, out kind, out fault))
			{
				return fault == KingdomCityFault.None;
			}
			if (ticksUntil > long.MaxValue - fromTick)
			{
				fault = KingdomCityFault.ArithmeticOverflow;
				return false;
			}
			candidates[count++] = new KingdomBreakpoint(kind, fromTick + ticksUntil, rowIndex);
			return true;
		}

		private static long RateOf(int[] rates, int index, int measured)
		{
			if (rates == null || index < 0 || index >= rates.Length)
			{
				return measured;
			}
			return rates[index];
		}

		/// <summary>
		/// A bonus lane and never a tax, and never a charge on a draw. <b>Both of those are
		/// <c>KingdomProductionRules.Methoded</c>'s own law</b> — it reads a sub-baseline percent as
		/// the baseline and returns a non-positive quantity untouched — and neither is restated
		/// here; what this adds is only the narrowing, so that a rate outside <c>int</c> is carried
		/// through rather than cast into a different number. Every rate reaching here came off an
		/// <c>int</c> row or an <c>int</c> override, so that is a guard against a representable
		/// future and not against today's arithmetic.
		/// </summary>
		private long Methoded(long ratePerDay)
		{
			return (ratePerDay > 0L && ratePerDay <= int.MaxValue)
				? KingdomProductionRules.Methoded((int)ratePerDay, methodPercent)
				: ratePerDay;
		}
	}

	/// <summary>
	/// One reckoning, in the only shape the executor accepts. LIVING-CITY-ARCHITECTURE &sect;2.5.
	/// </summary>
	internal sealed class KingdomReckonJob : IKingdomComputation<KingdomReckonInput, KingdomCityState>
	{
		private readonly KingdomCityAdvanceable model;

		private readonly string label;

		internal KingdomReckonJob(string label, KingdomCityAdvanceable model)
		{
			this.label = label;
			this.model = model;
		}

		public string Label
		{
			get { return label ?? ""; }
		}

		public KingdomBudgetLane Lane
		{
			get { return KingdomBudgetLane.Reckon; }
		}

		public bool TryRun(KingdomReckonInput input, out KingdomCityState output, out KingdomComputeCounters counters, out KingdomCityFault fault)
		{
			output = null;
			counters = KingdomComputeCounters.None;
			if (input.State == null || model == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			KingdomAdvanceOutcome<KingdomCityState> outcome;
			if (!KingdomAdvanceRules.TryRun(model, input.State, input.State.ProcessedThroughTick, input.ToTick, out outcome, out fault))
			{
				return false;
			}
			output = outcome.State;
			// No draws anywhere in W1's reckoning: nothing here rolls, and the receipt says so by
			// reporting a zero the tester can read against §0.0(a)'s per-happening cap.
			counters = new KingdomComputeCounters(outcome.Steps, outcome.RowVisits, 0, 0, 0L);
			fault = KingdomCityFault.None;
			return true;
		}
	}

	/// <summary>
	/// The city book's arithmetic: what a zone row projects, what the city holds, what one pass
	/// owes, and where a deficit is taken from.
	/// <para>
	/// Pure and engine-free. Every figure the founder reads about a zone they are not standing in
	/// comes through here, which is the whole of &sect;1.1: the city is a book, and a zone is a page
	/// of it that happens to be open.
	/// </para>
	/// </summary>
	internal static class KingdomCityRules
	{
		/// <summary>The schema the book is written under. Bumped whenever a column is added,
		/// removed or retyped; Addendum 9 waives migration pre-release, so a bump is clean and
		/// deliberate rather than a migration.
		/// <para>
		/// Version 2 is W2's resident rows: the standing gains a cause, each brink window gains its
		/// own standing flag, and both warned columns are retyped from a flag to the tick the
		/// window is anchored on. A version-1 book's brink columns cannot answer when a window
		/// started, which is the one number every consumer of a brink reads.
		/// </para>
		/// </summary>
		internal const int SchemaVersion = 2;

		/// <summary>The rules revision the book was last advanced by. Separate from the schema:
		/// a rules change that does not move a column still wants saying.</summary>
		internal const int RulesVersion = 1;

		/// <summary>A district the row does not name. Zero is "no district", which is what a zone
		/// claimed and never zoned actually is.</summary>
		internal const int NoDistrict = 0;

		/// <summary>
		/// The city's own stocks, summed from its zone rows.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;1.2(a): stocks are city-level, and that is the point —
		/// water raised in the mine and food grown on the terrace are one set of rows, and
		/// consumption anywhere draws on them. A zone nobody has ever stood in contributes nothing,
		/// which is the sighting doctrine unchanged: nothing is invented for ground the game has
		/// never looked at.
		/// </para>
		/// </summary>
		internal static bool TryCityStocks(KingdomCityState state, out KingdomStocks stocks)
		{
			stocks = default(KingdomStocks);
			if (state == null)
			{
				return false;
			}
			long water = 0L;
			long waterCap = 0L;
			long food = 0L;
			long foodCap = 0L;
			long materials = 0L;
			long materialsCap = 0L;
			for (int i = 0; i < state.ZoneCount; i++)
			{
				KingdomZoneRow row;
				if (!state.TryZone(i, out row))
				{
					return false;
				}
				if (row.LastReadTick <= 0L)
				{
					continue;
				}
				water += row.Stocks.Water.Level;
				waterCap += row.Stocks.Water.Capacity;
				food += row.Stocks.Food.Level;
				foodCap += row.Stocks.Food.Capacity;
				materials += row.Stocks.Materials.Level;
				materialsCap += row.Stocks.Materials.Capacity;
			}
			stocks = new KingdomStocks(
				new KingdomStockPair(water, waterCap),
				new KingdomStockPair(food, foodCap),
				new KingdomStockPair(materials, materialsCap));
			return true;
		}

		/// <summary>
		/// What one zone still owes the ground, as the weighted counter &sect;3.5 reports as
		/// <c>owed</c>.
		/// <para>
		/// Derived from the signed per-kind figures rather than stored beside them, so there is one
		/// debt and one home for it. Each kind that owes anything is one MEDIUM unit — one item
		/// stack into one container, or one container drained — which is the unit &sect;0.0(b)
		/// prices a landing and a draw at.
		/// </para>
		/// </summary>
		internal static KingdomCatchUpCounter CounterFor(KingdomZoneRow row)
		{
			int land = 0;
			int draw = 0;
			for (int kind = 0; kind <= (int)KingdomStockKind.Materials; kind++)
			{
				int owed = row.OwedOf((KingdomStockKind)kind);
				if (owed > 0)
				{
					land += KingdomCatchUpRules.WeightThirds(KingdomUnitWeight.Medium);
				}
				else if (owed < 0)
				{
					draw += KingdomCatchUpRules.WeightThirds(KingdomUnitWeight.Medium);
				}
			}
			return new KingdomCatchUpCounter(land, draw);
		}

		/// <summary>Everything the city still owes the ground, summed over its zones.</summary>
		internal static KingdomCatchUpCounter CityCounter(KingdomCityState state)
		{
			int land = 0;
			int draw = 0;
			for (int i = 0; state != null && i < state.ZoneCount; i++)
			{
				KingdomZoneRow row;
				if (!state.TryZone(i, out row))
				{
					continue;
				}
				KingdomCatchUpCounter counter = CounterFor(row);
				land += counter.LandThirds;
				draw += counter.DrawThirds;
			}
			return new KingdomCatchUpCounter(land, draw);
		}

		/// <summary>
		/// The city's level-1 zone graph, built from the book's own rows.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.10(2): nodes are claimed zones, edges are adjacency,
		/// all-pairs by Floyd&ndash;Warshall over &le; 9 nodes — 729 integer ops and an &le; 81-entry
		/// table. This half of the metric is composed from ZONE IDS ALONE, which is exactly why it
		/// may be built here: &sect;3.10(2) forbids recomputing the level-2 slices at reckon because
		/// they need the ground, and this needs none.
		/// </para>
		/// </summary>
		internal static bool TryZoneGraph(KingdomCityState state, out KingdomZoneGraph graph, out KingdomCityFault fault)
		{
			graph = null;
			if (state == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			int zones = state.ZoneCount;
			if (zones > KingdomDistanceRules.MaxNodes)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			KingdomZoneNode[] nodes = new KingdomZoneNode[zones];
			for (int i = 0; i < zones; i++)
			{
				KingdomZoneRow row;
				if (!state.TryZone(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				string world;
				int gx;
				int gy;
				int stratum;
				if (!KingdomRules.TryParseZoneID(row.ZoneId, out world, out gx, out gy, out stratum))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				nodes[i] = new KingdomZoneNode(row.ZoneId, gx, gy, stratum);
			}
			return KingdomZoneGraph.TryBuild(nodes, zones, KingdomDistanceRules.ZoneTransitCells, out graph, out fault);
		}

		/// <summary>
		/// Each zone row's distance from the seated ground, for the logistics order.
		/// <para>
		/// A zone the graph cannot reach, and every zone when the graph itself cannot be built,
		/// reads as <b>zero</b> rather than as unreachable. That is deliberate and it is the
		/// never-worse fallback: at zero the distance key stops discriminating and the
		/// apportionment falls back to row order, which is precisely what every wave before W6
		/// did. A malformed zone id degrades the routing and never refuses the carry.
		/// </para>
		/// </summary>
		internal static bool TryZoneDistances(KingdomCityState state, string seatedZoneId, int[] cells, out KingdomCityFault fault)
		{
			if (state == null || cells == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			int zones = state.ZoneCount;
			if (cells.Length < zones)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			fault = KingdomCityFault.None;
			for (int i = 0; i < zones; i++)
			{
				cells[i] = 0;
			}
			KingdomZoneGraph graph;
			KingdomCityFault built;
			if (!TryZoneGraph(state, out graph, out built))
			{
				return true;
			}
			int seat;
			if (!graph.TryIndexOf(seatedZoneId, out seat))
			{
				return true;
			}
			for (int i = 0; i < zones; i++)
			{
				int measured;
				cells[i] = graph.TryDistance(i, seat, out measured) ? measured : KingdomDistanceRules.NoRoute;
			}
			return true;
		}

		/// <summary>
		/// How much of one kind is carried out of the city's OTHER zones to cover a shortfall where
		/// the founder is standing, and out of which zones.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;1.2(a) and &sect;3.9 together: consumption anywhere draws
		/// on the same rows, but a dram is drunk out of a particular urn — so every zone the demand
		/// reaches owes its own vessels the difference. The apportionment is
		/// <c>KingdomDrainRules</c>'s own, with a zone standing in for a vessel: one rule, one
		/// order, one home.
		/// </para>
		/// <para>
		/// <b>W6 decides WHICH zone by distance</b> (&sect;3.10(1), invariant I6): the demand is
		/// spread over the city's grounds <i>nearest first</i>, on the level-1 zone graph, tie-broken
		/// on the lower row index — a stored fact, stable under a reload because rows are never
		/// reordered. Before W6 it was spread in row order, which is how a carrier ends up crossing
		/// the city past a nearer store. Inside a ground, the oldest dedication still pays first and
		/// nothing about I4 moves: the two rules answer different questions.
		/// </para>
		/// <para>
		/// Nothing is created, and the exact sense of that is worth stating because the loose
		/// reading of it is false. A row's LEVEL includes what its works have made and nobody has
		/// poured yet -- that is what a positive <c>owed</c> means -- so a carry can and should be
		/// able to deliver a harvest that is still a claim. What it moves is the CLAIM: the giving
		/// row loses level and debt together, the taking ground receives real goods that the model
		/// had already booked as made, and the city's total of both is unchanged. What
		/// <c>spokenFor</c> reserves is the other sign only -- a row already owing a DRAW has that
		/// much of its level promised to a vessel nobody has opened, and it may not be given away
		/// twice. So: no dram is invented, but a dram may land in a vessel other than the one it
		/// was booked against, which is exactly what a porter is for. I1 holds across the transfer
		/// by construction: what leaves one row arrives on another as a debt against real
		/// containers.
		/// </para>
		/// </summary>
		/// <param name="state">The book.</param>
		/// <param name="seatedZoneId">The zone the founder is standing in. It is never a source.</param>
		/// <param name="kind">Which stock is short.</param>
		/// <param name="demand">What the seated zone is short by. Zero or less moves nothing.</param>
		/// <param name="room">What the seated zone's own containers can still take.</param>
		/// <param name="moved">Per zone row, indexed as the rows are: what this zone gives up.</param>
		/// <param name="total">The sum of <paramref name="moved"/>.</param>
		internal static bool TryPlanTransfer(
			KingdomCityState state,
			string seatedZoneId,
			KingdomStockKind kind,
			long demand,
			long room,
			long[] moved,
			out long total,
			out KingdomCityFault fault)
		{
			total = 0L;
			if (state == null || moved == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			int zones = state.ZoneCount;
			if (moved.Length < zones)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			for (int i = 0; i < zones; i++)
			{
				moved[i] = 0L;
			}
			fault = KingdomCityFault.None;
			long wanted = (demand < room) ? demand : room;
			if (wanted <= 0L)
			{
				return true;
			}
			// W6, LIVING-CITY-ARCHITECTURE §3.10(1). The order the city is drawn on is
			// NEAREST-HOLDER, not zone-row order: an input job binds to the closest ground
			// actually holding the resource. Dedication order is untouched and still decides which
			// urn inside that ground pays (§3.9, I4) — the two rules are about different
			// questions, and stacking them this way is what lets both be true at once.
			int[] cells = new int[zones];
			if (!TryZoneDistances(state, seatedZoneId, cells, out fault))
			{
				return false;
			}
			KingdomVesselRow[] sources = new KingdomVesselRow[zones];
			for (int i = 0; i < zones; i++)
			{
				KingdomZoneRow row;
				if (!state.TryZone(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				KingdomStockPair pair;
				long available = 0L;
				if (row.LastReadTick > 0L
					&& !string.Equals(row.ZoneId, seatedZoneId, StringComparison.Ordinal)
					&& row.Stocks.TryGet(kind, out pair))
				{
					// A zone already owing a draw has that much of its level spoken for: the
					// vessels have not paid it yet, so it may not be given away twice.
					long spokenFor = -(long)Min(row.OwedOf(kind), 0);
					available = pair.Level - spokenFor;
					if (available < 0L)
					{
						available = 0L;
					}
				}
				// The drain's own ordering keys, carrying the logistics order: the "dedication
				// ordinal" it sorts on first is the distance to the seat, and the zone row index
				// beneath it is the frozen tie-break. One sort, one implementation, and no second
				// opinion about precedence.
				sources[i] = new KingdomVesselRow(i, cells[i], kind, available, available, true);
			}
			long[] drawn = new long[zones];
			long shortfall;
			if (!KingdomDrainRules.TryApportion(sources, zones, kind, wanted, drawn, out shortfall, out fault))
			{
				return false;
			}
			for (int i = 0; i < zones; i++)
			{
				moved[i] = drawn[i];
				total += drawn[i];
			}
			return true;
		}

		/// <summary>
		/// Posts a carry that actually landed against the rows it came out of.
		/// <para>
		/// The half of the transfer that must be arithmetic rather than I/O, because it is where I1
		/// is kept: what leaves a row's LEVEL is added to that row's DEBT in the same step, so
		/// <c>model total == ground total + counter-owed</c> holds across the carry by construction.
		/// The engine edge lands the goods and hands back how much of the plan the near containers
		/// actually took; nothing here touches a container.
		/// </para>
		/// </summary>
		/// <param name="landed">What the near containers actually took, which may be less than the
		/// plan asked for. Only that much is posted.</param>
		internal static bool TryApplyTransfer(
			KingdomCityState state,
			KingdomStockKind kind,
			long[] moved,
			long landed,
			out KingdomCityState next,
			out long applied,
			out KingdomCityFault fault)
		{
			next = state;
			applied = 0L;
			if (state == null || moved == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (moved.Length < state.ZoneCount)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			fault = KingdomCityFault.None;
			if (landed <= 0L)
			{
				return true;
			}
			KingdomCityState current = state;
			long left = landed;
			for (int i = 0; i < state.ZoneCount && left > 0L; i++)
			{
				if (moved[i] <= 0L)
				{
					continue;
				}
				long take = (moved[i] < left) ? moved[i] : left;
				KingdomZoneRow row;
				if (!current.TryZone(i, out row))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				KingdomStockPair pair;
				KingdomStocks lowered;
				if (!row.Stocks.TryGet(kind, out pair) || !row.Stocks.TryWith(kind, new KingdomStockPair(pair.Level - take, pair.Capacity), out lowered))
				{
					fault = KingdomCityFault.InvalidRate;
					return false;
				}
				// W7 repair. The debt is an `int` on purpose -- a dram and a serving are counted in
				// `int` everywhere the ground counts them -- and `take` is a `long`, so the
				// subtraction was done in `int` after an unchecked cast and could wrap a row's debt
				// from a draw into a landing. Widened and range-checked, the same way TryProduce
				// and TryReconcile already check theirs, so an impossible carry refuses instead of
				// publishing a debt with the wrong sign.
				long nextOwed = (long)row.OwedOf(kind) - take;
				if (nextOwed > int.MaxValue || nextOwed < int.MinValue)
				{
					fault = KingdomCityFault.ArithmeticOverflow;
					return false;
				}
				KingdomCityState written;
				if (!current.TryWithZone(
					i,
					row.WithReading(row.LastReadTick, lowered, row.Roofs, row.Defence, row.WaterCarry, row.FoodCarry)
						.WithOwedOf(kind, (int)nextOwed),
					out written,
					out fault))
				{
					return false;
				}
				current = written;
				left -= take;
				applied += take;
			}
			next = current;
			return true;
		}

		/// <summary>
		/// The realm's simulation seed, minted once at founding.
		/// <para>
		/// The kernel is explicit that it never generates one and that "whatever mints it must
		/// domain-separate on realm incarnation" (<c>KernelSeed128</c>). So the mint is a pure
		/// function of the world seed, the realm's name and the tick the water was poured: two
		/// realms in one world differ, the same realm across a reload does not, and a test can
		/// assert both without a clock or a random source in the room.
		/// </para>
		/// <para>
		/// FNV-1a over a canonical byte order, with the two halves separated by their own offset
		/// basis. This is an identity mint, never a cryptographic one, and the kernel's counter
		/// mode is what actually shapes the draws.
		/// </para>
		/// </summary>
		internal static bool TryMintSeed(int worldSeed, string realmName, long foundedTick, out KernelSeed128 seed, out KingdomCityFault fault)
		{
			seed = default(KernelSeed128);
			if (realmName == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (foundedTick < 0L)
			{
				fault = KingdomCityFault.InvalidTick;
				return false;
			}
			seed = new KernelSeed128(
				Mint(0xCBF29CE484222325UL, worldSeed, realmName, foundedTick),
				Mint(0x9E3779B97F4A7C15UL, worldSeed, realmName, foundedTick));
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// What the founder is told when the city carries its own stock to where they are standing.
		/// Plain, in the register the ledger already uses: this is news about drams, not a rule.
		/// </summary>
		internal static string CarryNote(KingdomStockKind kind, long amount, string realmName)
		{
			if (amount <= 0L)
			{
				return null;
			}
			string realm = string.IsNullOrEmpty(realmName) ? "the city" : realmName;
			if (kind == KingdomStockKind.Water)
			{
				return amount + " drams came in from " + realm + "'s other quarters, out of the oldest casks first.";
			}
			if (kind == KingdomStockKind.Food)
			{
				return amount + ((amount == 1L) ? " serving was" : " servings were") + " carried in from " + realm + "'s other pantries.";
			}
			return null;
		}

		/// <summary>
		/// The difference between what the model expected and what the ground actually holds,
		/// attributed rather than repaired (LIVING-CITY-ARCHITECTURE &sect;3.1 step 4). A cask with
		/// less in it than the book says means the founder poured some, and that is a story rather
		/// than a bug. Null when the two agree, which is the ordinary case.
		/// </summary>
		internal static string ReconcileNote(long water, long food)
		{
			if (water == 0L && food == 0L)
			{
				return null;
			}
			string clause = null;
			if (water < 0L)
			{
				clause = Join(clause, (-water) + " drams fewer than the books had");
			}
			else if (water > 0L)
			{
				clause = Join(clause, water + " drams more than the books had");
			}
			if (food < 0L)
			{
				clause = Join(clause, (-food) + " fewer servings than the books had");
			}
			else if (food > 0L)
			{
				clause = Join(clause, food + " more servings than the books had");
			}
			return "The stores hold " + clause + ". The stores are right and the books have been corrected.";
		}

		/// <summary>
		/// What is still owed after the containers have paid what they can. Never silently
		/// forgiven: LIVING-CITY-ARCHITECTURE &sect;3.9 rules that a mismatch is named.
		/// </summary>
		internal static string ShortfallNote(int waterOwed, int foodOwed)
		{
			if (waterOwed == 0 && foodOwed == 0)
			{
				return null;
			}
			if (waterOwed < 0 || foodOwed < 0)
			{
				string clause = null;
				if (waterOwed < 0)
				{
					clause = Join(clause, (-waterOwed) + " drams");
				}
				if (foodOwed < 0)
				{
					clause = Join(clause, (-foodOwed) + " servings");
				}
				return "The city drew " + clause + " it did not have here. The debt stands against these stores.";
			}
			string held = null;
			if (waterOwed > 0)
			{
				held = Join(held, waterOwed + " drams");
			}
			if (foodOwed > 0)
			{
				held = Join(held, foodOwed + " servings");
			}
			return "The books hold " + held + " these stores have no room for. It waits.";
		}

		/// <summary>
		/// What the founder is told when a porter puts a load down beside them. Addendum 12(c)'s
		/// canonical image, in the register the ledger already uses.
		/// </summary>
		internal static string PorterNote(int servings, string store)
		{
			if (servings <= 0)
			{
				return null;
			}
			string where = string.IsNullOrEmpty(store) ? "the store" : ("the " + store);
			return "A porter set " + servings + ((servings == 1) ? " serving" : " servings")
				+ " down in " + where + ", nodded, and went back the way they came.";
		}

		/// <summary>
		/// What the founder is told when a carrier could not finish. LIVING-CITY-ARCHITECTURE
		/// &sect;3.7: a job whose elapsed exceeds twice its projected duration <b>fails and is
		/// told</b>, and the cargo is real items that stay where they fell &mdash; so a founder who
		/// blocks a doorway forever produces a story rather than an unbounded job set.
		/// </summary>
		internal static string PorterFailedNote(int servings)
		{
			if (servings <= 0)
			{
				return "A carrier gave up on the road and turned back.";
			}
			return "A carrier could not get through, and set " + servings
				+ ((servings == 1) ? " serving" : " servings") + " down where they stood.";
		}

		/// <summary>
		/// The one ledger line the stale-transient sweep owes when it fires
		/// (LIVING-CITY-ARCHITECTURE &sect;3.8). <b>Deduplication, not destruction of property</b>,
		/// and the register says exactly that: the load reached the store by another hand.
		/// </summary>
		internal static string SweptNote(int carriers)
		{
			if (carriers <= 0)
			{
				return null;
			}
			return ((carriers == 1) ? "The load" : "The loads") + " you left on the road reached the store by another hand.";
		}

		/// <summary>
		/// The heartbeat's one line an hour. LIVING-CITY-ARCHITECTURE &sect;3.6 caps a slice at one
		/// told line city-wide, so a shortfall that has just begun says itself once and then lives
		/// in the status report.
		/// </summary>
		internal static string SliceNote(string cityName, int thirds)
		{
			if (thirds <= 0)
			{
				return null;
			}
			string city = string.IsNullOrEmpty(cityName) ? "the city" : cityName;
			return "Word from " + city + ": its stores are being drawn down faster than they are filling.";
		}

		/// <summary>
		/// The audit of LIVING-CITY-ARCHITECTURE &sect;3.9, as one greppable line: model total,
		/// what of it is still owed to real containers, ground total, and whether the three agree.
		/// <para>
		/// I1 in full is <c>model total == ground total + counter-owed</c>, per stock kind, at
		/// every instant. Before W6 the two owed figures were always zero on the seated row by the
		/// time this ran, so the line compared <c>model</c> to <c>ground</c> directly and was right
		/// by accident. W6 gives the model a producing rate, which means a seated row can carry a
		/// real claim that the containers have not taken yet — so the line now states the whole
		/// identity and MISMATCHes on the whole identity.
		/// </para>
		/// <para>
		/// <c>debt</c> is the signed per-kind claim (positive: made and not yet poured; negative:
		/// drunk and not yet drawn). <c>owed=n/3</c> is the catch-up counter's weighted thirds and
		/// is unchanged — it says how much of a turn's budget the backlog wants, not how many
		/// drams it is.
		/// </para>
		/// </summary>
		internal static string AuditNote(long modelWater, long debtWater, long groundWater, long modelFood, long debtFood, long groundFood, int owedThirds)
		{
			return "audit water model=" + modelWater + " debt=" + debtWater + " ground=" + groundWater
				+ " food model=" + modelFood + " debt=" + debtFood + " ground=" + groundFood
				+ " owed=" + owedThirds + "/3"
				+ ((modelWater - debtWater == groundWater && modelFood - debtFood == groundFood) ? "" : " MISMATCH");
		}

		/// <summary>
		/// A stable identifier for a string, for a work row that has to survive a save.
		/// <para>
		/// FNV-1a, written out rather than taken from the runtime, for the reason the kernel gives
		/// about hashing at all: a runtime hash is not stable across processes, and an id that
		/// changes when the game restarts is not an id.
		/// </para>
		/// </summary>
		internal static int StableId(string value)
		{
			if (string.IsNullOrEmpty(value))
			{
				return 0;
			}
			uint hash = 2166136261u;
			for (int i = 0; i < value.Length; i++)
			{
				hash ^= (uint)(value[i] & 0xFF);
				hash *= 16777619u;
				hash ^= (uint)((value[i] >> 8) & 0xFF);
				hash *= 16777619u;
			}
			return (int)(hash & 0x7FFFFFFFu);
		}

		private static string Join(string standing, string clause)
		{
			return (standing == null) ? clause : (standing + " and " + clause);
		}

		/// <summary>
		/// Where a container sorts in the drain.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.9 wants the order to be a STORED FACT, so the number is
		/// the ordinal stamped on the container the first pass the city counted it. A container
		/// carrying no ordinal has not been counted yet, and it sorts <b>last</b> rather than
		/// first: an unstamped vessel has no claim to being the oldest, and sorting it first would
		/// let a container the city has never seen jump the whole queue.
		/// </para>
		/// </summary>
		internal static int DrainOrdinal(int stamped)
		{
			return (stamped > 0) ? stamped : int.MaxValue;
		}

		/// <summary>The stable code for a district key, or <see cref="NoDistrict"/>. The registry is
		/// data-driven under the extensibility law, so the row carries a code and the name stays in
		/// one place.</summary>
		internal static int DistrictCode(string district)
		{
			if (string.IsNullOrEmpty(district))
			{
				return NoDistrict;
			}
			for (int i = 0; i < KingdomRules.Districts.Length; i++)
			{
				if (string.Equals(KingdomRules.Districts[i], district, StringComparison.Ordinal))
				{
					return i + 1;
				}
			}
			return NoDistrict;
		}

		/// <summary>The district key a code names, or null. The inverse of
		/// <see cref="DistrictCode"/> over every representable input.</summary>
		internal static string DistrictKey(int code)
		{
			int index = code - 1;
			if (index < 0 || index >= KingdomRules.Districts.Length)
			{
				return null;
			}
			return KingdomRules.Districts[index];
		}

		private static ulong Mint(ulong basis, int worldSeed, string realmName, long foundedTick)
		{
			ulong hash = basis;
			hash = Fold(hash, (ulong)(uint)worldSeed);
			for (int i = 0; i < realmName.Length; i++)
			{
				hash = Fold(hash, realmName[i]);
			}
			hash = Fold(hash, (ulong)foundedTick);
			return hash;
		}

		private static ulong Fold(ulong hash, ulong value)
		{
			for (int shift = 0; shift < 64; shift += 8)
			{
				hash ^= (value >> shift) & 0xFFUL;
				hash *= 0x100000001B3UL;
			}
			return hash;
		}

		private static int Min(int left, int right)
		{
			return (left < right) ? left : right;
		}
	}
}
