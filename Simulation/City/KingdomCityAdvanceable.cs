using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
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
}
