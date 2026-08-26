using System;
using System.Collections.Generic;
using System.Diagnostics;

using XRL;
using XRL.World;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	public static partial class KingdomCity
	{
		/// <summary>
		/// The pass's last word: what this zone actually holds once the day has been drawn, the
		/// harvest gathered and the works run.
		/// <para>
		/// &sect;3.4 names <c>SuspendingEvent</c> as the true last read and this as the cheaper one
		/// that usually beats it there. Both write the same row; whichever fires last is the
		/// reading the other zones will be measured against.
		/// </para>
		/// </summary>
		public static void CheckOut(KingdomSystem System, Zone Z, KingdomSurvey Survey, long TimeTicks)
		{
			// A stamp of zero would date the row as never read, and a row that reads as never read
			// contributes nothing anywhere. So a check-out with no clock to date it is skipped
			// rather than written: a missed check-out costs freshness, and a zeroed one would cost
			// the city a whole parasang.
			if (System == null || !System.Founded || Z == null || Survey == null || System.City == null || TimeTicks <= 0L)
			{
				return;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			if (!Ensure(System, Z, out state, out fault))
			{
				Refuse("check-out", fault);
				return;
			}
			int index;
			if (!IndexOf(state, Z.ZoneID, out index))
			{
				return;
			}
			KingdomZoneRow row;
			state.TryZone(index, out row);
			KingdomStocks ground = Ground(Survey, row);
			KingdomProductionStep water;
			KingdomProductionStep food;
			// W7 repair. Each kind is reconciled ON ITS OWN. These two used to be joined by `||`,
			// so one refusal abandoned the other kind's reconcile AND the rate stamp below -- a
			// larder the founder had over-stuffed could suppress the water half and strand a stale
			// carry on the row. A fault in one stock is now a fault about one stock: it is told,
			// the other half still lands, and the row still learns what this ground makes.
			bool wet = KingdomProductionRules.TryReconcile(ground.Water.Level, ground.Water.Capacity, row.OwedWater, out water, out fault);
			if (!wet)
			{
				Refuse("check-out water", fault);
			}
			bool fed = KingdomProductionRules.TryReconcile(ground.Food.Level, ground.Food.Capacity, row.OwedFood, out food, out fault);
			if (!fed)
			{
				Refuse("check-out food", fault);
			}
			if (!wet && !fed)
			{
				return;
			}
			if (!wet)
			{
				water = new KingdomProductionStep(row.Stocks.Water.Level, row.OwedWater, 0L, 0L);
			}
			if (!fed)
			{
				food = new KingdomProductionStep(row.Stocks.Food.Level, row.OwedFood, 0L, 0L);
			}
			KingdomStocks trued = new KingdomStocks(
				new KingdomStockPair(water.NextLevel, wet ? ground.Water.Capacity : row.Stocks.Water.Capacity),
				new KingdomStockPair(food.NextLevel, fed ? ground.Food.Capacity : row.Stocks.Food.Capacity),
				ground.Materials);
			KingdomCityState written;
			// The last read is also the last measurement of what this ground MAKES: the founder is
			// about to walk out, and the rate stamped here is the one the model will run this zone
			// at for as long as they are away (§7.4, W6).
			if (!state.TryWithZone(
					index,
					row.WithReading(TimeTicks, trued, row.Roofs, Survey.Defence(), WaterMadePerDay(Survey), FoodMadePerDay(Survey))
						.WithOwed(water.NextOwed, food.NextOwed, row.OwedMaterials),
					out written,
					out fault))
			{
				Refuse("check-out", fault);
				return;
			}
			if (!KingdomDistanceRuntime.Observe(System, Z, Survey, written, out fault))
			{
				Refuse("distance observe", fault);
			}
			Publish(System, written);
		}

		/// <summary>
		/// The true last read (&sect;3.4). Fires from <c>SuspendZone</c> before <c>Suspended</c> is
		/// set, for ANY zone as it suspends — so the filter is the whole of the handler: only a
		/// zone the seated realm claims is ours to read, and only while its objects are still in
		/// RAM.
		/// </summary>
		public static void OnSuspending(KingdomSystem System, Zone Z)
		{
			if (System == null || !System.Founded || Z == null || System.ClaimedZones == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			CheckOut(System, Z, KingdomSurvey.Take(Z, System), (The.Game != null) ? The.Game.TimeTicks : 0L);
		}

	}
}
