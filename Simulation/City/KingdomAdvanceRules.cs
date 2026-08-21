using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// Why a rate changed. LIVING-CITY-ARCHITECTURE &sect;2.3 lists them, and the list is closed:
	/// a breakpoint is any moment a rate can change, and a model with no more structural changes
	/// available has no more breakpoints to spend.
	/// </summary>
	internal enum KingdomBreakpointKind : byte
	{
		/// <summary>No structural change remains inside the span.</summary>
		None = 0,

		/// <summary>A stock hit empty — a solvable linear crossing, computed, never searched.</summary>
		StockEmpty = 1,

		/// <summary>A stock hit full.</summary>
		StockFull = 2,

		/// <summary>A crop's next stage tick.</summary>
		CropStage = 3,

		/// <summary>A periodic clock's next due tick, folded O(1) rather than looped.</summary>
		ClockDue = 4,

		/// <summary>A brink window's expiry.</summary>
		BrinkExpiry = 5,

		/// <summary>A subsidence rung change — <c>KingdomSubsidenceRules.Slide</c>'s own breakpoint.</summary>
		SubsidenceRung = 6,

		/// <summary>A stage change, which changes upkeep and therefore every rate at once.</summary>
		StageChange = 7,

		/// <summary>The end of the span. Not a rate change: the closing integration.</summary>
		Horizon = 8
	}

	/// <summary>One dated moment a rate changes, and which row owns it.</summary>
	internal readonly struct KingdomBreakpoint
	{
		internal readonly KingdomBreakpointKind Kind;

		internal readonly long Tick;

		/// <summary>The row that proposed it, for the deterministic tie-break and for the telling
		/// layer. Negative for a breakpoint no single row owns.</summary>
		internal readonly int RowIndex;

		internal KingdomBreakpoint(KingdomBreakpointKind kind, long tick, int rowIndex)
		{
			Kind = kind;
			Tick = tick;
			RowIndex = rowIndex;
		}

		internal static KingdomBreakpoint Horizon(long tick)
		{
			return new KingdomBreakpoint(KingdomBreakpointKind.Horizon, tick, -1);
		}

		internal static KingdomBreakpoint None
		{
			get { return new KingdomBreakpoint(KingdomBreakpointKind.None, 0L, -1); }
		}
	}

	/// <summary>
	/// What the advancement driver needs of a model, and nothing more.
	/// <para>
	/// One propose pass (every row emits its next candidate tick), one apply pass (every row
	/// integrates to the chosen tick), and one closed-form escape to the fixed point for the
	/// overflow case. LIVING-CITY-ARCHITECTURE &sect;2.3.
	/// </para>
	/// <para>
	/// A structural interface rather than a base class, and pure: an implementation may not read a
	/// clock — <c>nowTick</c> arrives as <c>horizonTick</c> — and may not touch an engine type, which
	/// is what <c>KingdomComputeSeam</c> checks when the model crosses the executor.
	/// </para>
	/// </summary>
	internal interface IKingdomAdvanceable<TState>
	{
		/// <summary>The live <c>R</c> of LIVING-CITY-ARCHITECTURE &sect;0.0(f). The receipt checks
		/// row-visits against this, never against 14,848.</summary>
		int RowCount(TState state);

		/// <summary>
		/// The earliest tick at which a rate changes, strictly after <paramref name="fromTick"/> and
		/// at or before <paramref name="horizonTick"/>, or <see cref="KingdomBreakpoint.None"/>.
		/// Every candidate is computed — "the tick at which this will happen at the current rates" —
		/// and the minimum taken. Nothing here searches, and nothing here loops a day at a time.
		/// </summary>
		bool TryProposeNext(TState state, long fromTick, long horizonTick, out KingdomBreakpoint breakpoint, out KingdomCityFault fault);

		/// <summary>Integrates every row linearly to the breakpoint's tick and applies it.</summary>
		bool TryApply(TState state, KingdomBreakpoint breakpoint, out TState next, out KingdomCityFault fault);

		/// <summary>
		/// The honest overflow of LIVING-CITY-ARCHITECTURE &sect;2.3: jump to the equilibrium the
		/// model converges on and date the remainder as settled. Not a forgiveness cap in disguise
		/// — the same convergence reached by arithmetic instead of by steps.
		/// </summary>
		bool TryJumpToFixedPoint(TState state, long throughTick, out TState next, out KingdomCityFault fault);
	}

	/// <summary>What one advancement did, in counts the receipt can check.</summary>
	internal readonly struct KingdomAdvanceOutcome<TState>
	{
		internal readonly TState State;

		/// <summary>Passes spent. One pass is one propose plus one apply.</summary>
		internal readonly int Steps;

		/// <summary>Steps x 2R. LIVING-CITY-ARCHITECTURE &sect;0.0(a).</summary>
		internal readonly long RowVisits;

		internal readonly long ProcessedThroughTick;

		/// <summary>Whether the step budget ran out and the model jumped to its fixed point.</summary>
		internal readonly bool Overflowed;

		internal KingdomAdvanceOutcome(TState state, int steps, long rowVisits, long processedThroughTick, bool overflowed)
		{
			State = state;
			Steps = steps;
			RowVisits = rowVisits;
			ProcessedThroughTick = processedThroughTick;
			Overflowed = overflowed;
		}
	}

	/// <summary>
	/// Breakpoint integration: O(model), never O(days).
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;2.3, generalising the shape
	/// <c>KingdomSubsidenceRules.Slide</c> already uses: <i>between two consecutive breakpoints,
	/// every rate in the model is constant</i>, so integrate linearly to the next breakpoint, apply
	/// it, and repeat — and the number of breakpoints is bounded by the model, not by the elapsed.
	/// </para>
	/// <para>
	/// Pure and engine-free. Nothing here reads a clock: the span arrives as two ticks, which is
	/// also what makes an advancement replayable in a test.
	/// </para>
	/// </summary>
	internal static class KingdomAdvanceRules
	{
		/// <summary>
		/// Passes one advancement may spend. LIVING-CITY-ARCHITECTURE &sect;0.0(a) / &sect;2.3: the
		/// 64-cap is belt-and-braces over a loop that already terminates in O(model), and its
		/// overflow is honest rather than silent.
		/// <para>
		/// The last affordable pass is spent on the fixed-point jump rather than on another step,
		/// so row-visits are <c>Steps x 2R</c> and never exceed <c>64 x 2R</c> — the figure the
		/// constitution's table is written against.
		/// </para>
		/// </summary>
		internal const int MaxPasses = KingdomBudgetRules.MaxBreakpoints;

		/// <summary>
		/// Runs a model forward across one span.
		/// <para>
		/// Total over representable input, publishing nothing on a fault: a refusal leaves the
		/// caller holding exactly the state it handed in. An empty span (<c>toTick</c> equal to
		/// <c>fromTick</c>) is a no-op with one closing pass, which is what makes calling this
		/// twice at the same tick idempotent.
		/// </para>
		/// </summary>
		internal static bool TryRun<TState>(
			IKingdomAdvanceable<TState> model,
			TState state,
			long fromTick,
			long toTick,
			out KingdomAdvanceOutcome<TState> outcome,
			out KingdomCityFault fault)
		{
			outcome = default(KingdomAdvanceOutcome<TState>);
			if (model == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			KernelFaultCode kernelFault;
			if (!TickMath.TryValidateAdvance(fromTick, toTick, out kernelFault))
			{
				fault = KingdomCityFaults.FromKernel(kernelFault);
				return false;
			}
			int rows = model.RowCount(state);
			if (rows < 0)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			long perPass = 2L * rows;

			TState current = state;
			long cursor = fromTick;
			int steps = 0;
			bool overflowed = false;
			while (true)
			{
				KingdomBreakpoint breakpoint;
				if (!model.TryProposeNext(current, cursor, toTick, out breakpoint, out fault))
				{
					return false;
				}
				bool closing = breakpoint.Kind == KingdomBreakpointKind.None
					|| breakpoint.Kind == KingdomBreakpointKind.Horizon
					|| breakpoint.Tick >= toTick;
				bool lastAffordablePass = (steps + 1) >= MaxPasses;
				TState next;
				if (closing)
				{
					if (!model.TryApply(current, KingdomBreakpoint.Horizon(toTick), out next, out fault))
					{
						return false;
					}
					current = next;
					steps++;
					cursor = toTick;
					break;
				}
				if (lastAffordablePass)
				{
					if (!model.TryJumpToFixedPoint(current, toTick, out next, out fault))
					{
						return false;
					}
					current = next;
					steps++;
					cursor = toTick;
					overflowed = true;
					break;
				}
				if (breakpoint.Tick < cursor)
				{
					fault = KingdomCityFault.ClockRegression;
					return false;
				}
				if (!model.TryApply(current, breakpoint, out next, out fault))
				{
					return false;
				}
				current = next;
				cursor = breakpoint.Tick;
				steps++;
			}

			outcome = new KingdomAdvanceOutcome<TState>(current, steps, (long)steps * perPass, cursor, overflowed);
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// When a stock running at a constant rate reaches its floor or its ceiling, in ticks from
		/// now. Solved, never searched — LIVING-CITY-ARCHITECTURE &sect;2.3.
		/// <para>
		/// A zero rate has no crossing and says so with <c>false</c> and
		/// <see cref="KingdomCityFault.None"/>, which is the one place here a refusal is an
		/// ordinary answer rather than a fault: a stock that is not moving will not arrive.
		/// </para>
		/// </summary>
		internal static bool TryCrossingTicks(
			long level,
			long capacity,
			long ratePerDay,
			long ticksPerDay,
			out long ticksUntil,
			out KingdomBreakpointKind kind,
			out KingdomCityFault fault)
		{
			ticksUntil = 0L;
			kind = KingdomBreakpointKind.None;
			if (ticksPerDay <= 0L)
			{
				fault = KingdomCityFault.InvalidInterval;
				return false;
			}
			if (capacity < 0L || level < 0L || level > capacity)
			{
				fault = KingdomCityFault.InvalidCapacity;
				return false;
			}
			fault = KingdomCityFault.None;
			if (ratePerDay == 0L)
			{
				return false;
			}
			long distance;
			if (ratePerDay > 0L)
			{
				kind = KingdomBreakpointKind.StockFull;
				distance = capacity - level;
			}
			else
			{
				kind = KingdomBreakpointKind.StockEmpty;
				distance = level;
			}
			long magnitude = (ratePerDay > 0L) ? ratePerDay : -ratePerDay;
			long days = CeilingDivide(distance, magnitude);
			if (days > long.MaxValue / ticksPerDay)
			{
				fault = KingdomCityFault.ArithmeticOverflow;
				kind = KingdomBreakpointKind.None;
				return false;
			}
			ticksUntil = days * ticksPerDay;
			return true;
		}

		/// <summary>
		/// One segment of the integration: a constant rate over a stretch of ticks, clamped to the
		/// stock's floor and ceiling. The clamp is the crossing, so a segment can never overshoot
		/// a breakpoint that the propose pass should have found first.
		/// <para>
		/// <b>The driver's primitive, and not the city's.</b> Since W6 the city book integrates
		/// through <c>KingdomProductionRules.TryProduce</c> instead, for two reasons this one
		/// deliberately does not take on: production must move the row's DEBT by the same amount it
		/// moves the level (invariant I1), and its days must be counted as world-day boundaries
		/// crossed rather than as elapsed divided by a day, so that splitting a span at a breakpoint
		/// reaches the same total as running it whole. This remains the general primitive an
		/// <c>IKingdomAdvanceable</c> with no debt to keep can use, and it is what the toy model in
		/// the tests integrates on.
		/// </para>
		/// </summary>
		internal static bool TryIntegrateSegment(
			long level,
			long capacity,
			long ratePerDay,
			long ticks,
			long ticksPerDay,
			out long nextLevel,
			out KingdomCityFault fault)
		{
			nextLevel = level;
			if (ticksPerDay <= 0L)
			{
				fault = KingdomCityFault.InvalidInterval;
				return false;
			}
			if (ticks < 0L)
			{
				fault = KingdomCityFault.InvalidTick;
				return false;
			}
			if (capacity < 0L || level < 0L || level > capacity)
			{
				fault = KingdomCityFault.InvalidCapacity;
				return false;
			}
			long days = ticks / ticksPerDay;
			if (days != 0L && ratePerDay != 0L)
			{
				long magnitude = (ratePerDay > 0L) ? ratePerDay : -ratePerDay;
				if (days > long.MaxValue / magnitude)
				{
					fault = KingdomCityFault.ArithmeticOverflow;
					return false;
				}
			}
			long delta = days * ratePerDay;
			long moved = level + delta;
			if (delta > 0L && moved < level)
			{
				moved = capacity;
			}
			if (delta < 0L && moved > level)
			{
				moved = 0L;
			}
			if (moved < 0L)
			{
				moved = 0L;
			}
			if (moved > capacity)
			{
				moved = capacity;
			}
			nextLevel = moved;
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// The earliest of a bounded candidate set, with a frozen tie-break: lowest tick, then
		/// lowest kind ordinal, then lowest row index. Deterministic without a draw, and stable
		/// under a reload, which is the same reason &sect;3.9's drain order is a stored fact rather
		/// than a ranking recomputed from contents.
		/// </summary>
		internal static bool TryEarliest(
			KingdomBreakpoint[] candidates,
			int count,
			long afterTick,
			long horizonTick,
			out KingdomBreakpoint earliest,
			out KingdomCityFault fault)
		{
			earliest = KingdomBreakpoint.None;
			if (candidates == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (count < 0 || count > candidates.Length)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			fault = KingdomCityFault.None;
			bool found = false;
			for (int i = 0; i < count; i++)
			{
				KingdomBreakpoint candidate = candidates[i];
				if (candidate.Kind == KingdomBreakpointKind.None)
				{
					continue;
				}
				if (candidate.Tick < afterTick || candidate.Tick > horizonTick)
				{
					continue;
				}
				if (!found || Precedes(candidate, earliest))
				{
					earliest = candidate;
					found = true;
				}
			}
			if (!found)
			{
				earliest = KingdomBreakpoint.None;
			}
			return found;
		}

		private static bool Precedes(KingdomBreakpoint candidate, KingdomBreakpoint standing)
		{
			if (candidate.Tick != standing.Tick)
			{
				return candidate.Tick < standing.Tick;
			}
			if (candidate.Kind != standing.Kind)
			{
				return candidate.Kind < standing.Kind;
			}
			return candidate.RowIndex < standing.RowIndex;
		}

		private static long CeilingDivide(long numerator, long denominator)
		{
			if (numerator <= 0L)
			{
				return 0L;
			}
			return ((numerator - 1L) / denominator) + 1L;
		}
	}
}
