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
	/// <b>What W1 gives it, and what it deliberately does not.</b> The shape is the whole of
	/// &sect;2.3 — one propose pass, one apply pass, a closed-form crossing rather than a search,
	/// and an honest jump to the fixed point when the breakpoint budget runs out. The RATES are
	/// another matter. A zone's works make what their carries promise, but the attended pass
	/// already credits the seated zone's works for the settlement's whole elapsed
	/// (<c>KingdomGrowth</c>'s <c>LastWaterWorkTick</c> is a settlement stamp, not a zone one), so
	/// a model that also credited them here would pay the same day twice. W1 therefore ships the
	/// integration with a net rate of zero and leaves the rates to the wave that owns the flows;
	/// what W1 does move through the book is CONSERVED — water carried from one of the city's
	/// zones to another (&sect;1.2(a)), never water invented.
	/// </para>
	/// <para>
	/// The consequence is worth stating rather than hiding: today a reckoning over any span spends
	/// exactly one closing pass, so the 1-day and 90-day row-visit counts of &sect;0.0(a) are equal
	/// by construction. The assertion still earns its place — it is what fails the moment a later
	/// wave gives a lane a per-day term.
	/// </para>
	/// </summary>
	internal sealed class KingdomCityAdvanceable : IKingdomAdvanceable<KingdomCityState>
	{
		private readonly long ticksPerDay;

		private readonly int[] waterRatePerDay;

		private readonly int[] foodRatePerDay;

		/// <summary>
		/// Rates are handed in per zone row, in row order, so a test can drive the integration over
		/// a real crossing without production having to invent one. A null or short array reads as
		/// a zero rate for that row: a rate nobody supplied is a rate that is not running.
		/// </summary>
		internal KingdomCityAdvanceable(long ticksPerDay, int[] waterRatePerDay, int[] foodRatePerDay)
		{
			this.ticksPerDay = ticksPerDay;
			this.waterRatePerDay = waterRatePerDay;
			this.foodRatePerDay = foodRatePerDay;
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
				if (!TryCandidate(row.Stocks.Water, RateOf(waterRatePerDay, i), fromTick, i, candidates, ref count, out fault))
				{
					return false;
				}
				if (!TryCandidate(row.Stocks.Food, RateOf(foodRatePerDay, i), fromTick, i, candidates, ref count, out fault))
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
			long ticks = breakpoint.Tick - state.ProcessedThroughTick;
			if (ticks < 0L)
			{
				fault = KingdomCityFault.ClockRegression;
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
				long water;
				long food;
				if (!KingdomAdvanceRules.TryIntegrateSegment(row.Stocks.Water.Level, row.Stocks.Water.Capacity, RateOf(waterRatePerDay, i), ticks, ticksPerDay, out water, out fault)
					|| !KingdomAdvanceRules.TryIntegrateSegment(row.Stocks.Food.Level, row.Stocks.Food.Capacity, RateOf(foodRatePerDay, i), ticks, ticksPerDay, out food, out fault))
				{
					return false;
				}
				if (water == row.Stocks.Water.Level && food == row.Stocks.Food.Level)
				{
					continue;
				}
				KingdomStocks moved = new KingdomStocks(
					new KingdomStockPair(water, row.Stocks.Water.Capacity),
					new KingdomStockPair(food, row.Stocks.Food.Capacity),
					row.Stocks.Materials);
				KingdomCityState written;
				if (!current.TryWithZone(i, row.WithReading(row.LastReadTick, moved, row.Roofs, row.Defence, row.WaterCarry, row.FoodCarry), out written, out fault))
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

		private static long RateOf(int[] rates, int index)
		{
			if (rates == null || index < 0 || index >= rates.Length)
			{
				return 0L;
			}
			return rates[index];
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
		/// deliberate rather than a migration.</summary>
		internal const int SchemaVersion = 1;

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
		/// How much of one kind is carried out of the city's OTHER zones to cover a shortfall where
		/// the founder is standing, and out of which zones.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;1.2(a) and &sect;3.9 together: consumption anywhere draws
		/// on the same rows, but a dram is drunk out of a particular urn — so the demand is spread
		/// oldest first, and every zone it reaches owes its own vessels the difference. The
		/// apportionment is <c>KingdomDrainRules</c>'s own, with a zone standing in for a vessel:
		/// one rule, one order, one home. "Oldest" here is row order, which is the order the city
		/// first read each zone — a stored fact, for the same reason a vessel's dedication ordinal
		/// is one, and stable under a reload because rows are never reordered.
		/// </para>
		/// <para>
		/// Nothing is created. The total moved can never exceed what the rows say the other zones
		/// hold, nor the room the near vessels have, so I1 holds across the transfer by
		/// construction: what leaves one row arrives on another as a debt against real containers.
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
				sources[i] = new KingdomVesselRow(i, i, kind, available, available, true);
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
				KingdomCityState written;
				if (!current.TryWithZone(
					i,
					row.WithReading(row.LastReadTick, lowered, row.Roofs, row.Defence, row.WaterCarry, row.FoodCarry)
						.WithOwedOf(kind, row.OwedOf(kind) - (int)take),
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
		/// The audit of LIVING-CITY-ARCHITECTURE &sect;3.9, as one greppable line: model total,
		/// ground total, and what stands between them. I1 in its general form is
		/// <c>model == ground + counter-owed</c>; with the counter at zero the two totals are
		/// simply equal, and a mismatch is attributed rather than repaired.
		/// </summary>
		internal static string AuditNote(long modelWater, long groundWater, long modelFood, long groundFood, int owedThirds)
		{
			return "audit water model=" + modelWater + " ground=" + groundWater
				+ " food model=" + modelFood + " ground=" + groundFood
				+ " owed=" + owedThirds + "/3"
				+ ((modelWater == groundWater && modelFood == groundFood) ? "" : " MISMATCH");
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
