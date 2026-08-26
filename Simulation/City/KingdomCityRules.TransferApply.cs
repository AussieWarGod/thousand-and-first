using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCityRules
	{
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

		/// <summary>Posts a physical central-logistics pickup after the exact source callback proved
		/// the stock left its dedicated holder. Unlike legacy claim-transfer accounting this does not
		/// add a future draw debt: the ground already changed, and the durable delivery row owns the
		/// cargo until an exact target receipt lands it.</summary>
		internal static bool TryApplyPhysicalDebit(KingdomCityState state, int zoneIndex,
			KingdomStockKind kind, long amount, out KingdomCityState next,
			out KingdomCityFault fault)
		{
			next = state;
			if (state == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (zoneIndex < 0 || zoneIndex >= state.ZoneCount || amount <= 0L)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			KingdomZoneRow row;
			KingdomStockPair pair;
			KingdomStocks lowered;
			if (!state.TryZone(zoneIndex, out row) || !row.Stocks.TryGet(kind, out pair)
				|| amount > pair.Level
				|| !row.Stocks.TryWith(kind,
					new KingdomStockPair(pair.Level - amount, pair.Capacity), out lowered))
			{
				fault = KingdomCityFault.InvalidCapacity;
				return false;
			}
			return state.TryWithZone(zoneIndex,
				row.WithReading(row.LastReadTick, lowered, row.Roofs, row.Defence,
					row.WaterCarry, row.FoodCarry), out next, out fault);
		}

	}
}
