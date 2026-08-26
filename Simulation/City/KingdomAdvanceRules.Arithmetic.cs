using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomAdvanceRules
	{
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
