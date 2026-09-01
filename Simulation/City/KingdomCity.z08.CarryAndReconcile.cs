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
		/// Water consumption anywhere draws on the same rows (&sect;1.2(a)), but a dram is drunk out
		/// of a particular urn (&sect;3.9). When the seated zone cannot cover water upkeep, the city's
		/// own water is carried in from holding zones, oldest dedication first, and those zones owe
		/// their vessels the difference next time anybody opens them.
		/// <para>
		/// Nothing is created here and nothing is destroyed: what leaves a row arrives on another
		/// row as a debt against real containers, which is exactly what makes I1 hold across the
		/// carry. A one-zone city, and a city whose seated zone can pay its own bill, are untouched.
		/// </para>
		/// </summary>
		private static KingdomCityState Carry(KingdomSystem System, Zone Z, KingdomSurvey Survey, KingdomCityState state, long TimeTicks)
		{
			long elapsed = (System.LastHeartbeatTick > 0L) ? (TimeTicks - System.LastHeartbeatTick) : 0L;
			if (elapsed <= 0L || state.ZoneCount < 2)
			{
				return state;
			}
			KingdomCityState current = state;
			current = CarryKind(System, Z, current, KingdomStockKind.Water,
				KingdomRules.PolicyUpkeepForElapsed(System.Population, elapsed, System.Stores, System.Stage) - Survey.StoredWater,
				Survey.StorageSpace,
				TimeTicks);
			// Food has no passive elapsed-time demand. Explicit meal, industry, and trade operations
			// move physical food through their own quoted receipt lanes; city carry must not invent a
			// destination demand or an owed-container debit for them.
			return current;
		}

		private static KingdomCityState CarryKind(KingdomSystem System, Zone Z,
			KingdomCityState state, KingdomStockKind kind, long demand, long room,
			long TimeTicks)
		{
			if (demand <= 0L || room <= 0L)
			{
				return state;
			}
			KingdomCityFault fault;
			int queued;
			if (!KingdomCentralLogistics.TryQueueScalar(System, state, Z.ZoneID, kind,
				demand, room, TimeTicks, out queued, out fault))
			{
				Refuse("carry queue", fault);
				return state;
			}
			if (queued <= 0)
			{
				return state;
			}
			KingdomLog.Log("city: queued " + queued + " " + kind + " to " + Z.ZoneID);
			return state;
		}

		/// <summary>
		/// Ground to model (&sect;3.1). The ground wins for anything physical; the difference is
		/// attributed and told, never silently repaired. A cask with less water in it than the
		/// model expected means the founder poured some.
		/// </summary>
		private static KingdomCityState Reconcile(KingdomSystem System, Zone Z, KingdomSurvey Survey, KingdomCityState state, int index, long TimeTicks)
		{
			KingdomZoneRow row;
			if (!state.TryZone(index, out row))
			{
				return state;
			}
			KingdomStocks ground = Ground(Survey, row);
			KingdomProductionStep water;
			KingdomProductionStep food;
			KingdomCityFault fault;
			// W7 repair, and the same one CheckOut carries: one kind's refusal is not the other
			// kind's, and neither is the rate stamp's.
			bool wet = KingdomProductionRules.TryReconcile(ground.Water.Level, ground.Water.Capacity, row.OwedWater, out water, out fault);
			if (!wet)
			{
				Refuse("reconcile water", fault);
			}
			bool fed = KingdomProductionRules.TryReconcile(ground.Food.Level, ground.Food.Capacity, row.OwedFood, out food, out fault);
			if (!fed)
			{
				Refuse("reconcile food", fault);
			}
			if (!wet && !fed)
			{
				return state;
			}
			if (!wet)
			{
				water = new KingdomProductionStep(row.Stocks.Water.Level, row.OwedWater, 0L, 0L);
			}
			if (!fed)
			{
				food = new KingdomProductionStep(row.Stocks.Food.Level, row.OwedFood, 0L, 0L);
			}
			if (row.LastReadTick > 0L)
			{
				// Drift is measured against what the model SAYS the ground holds, which is
				// `level - owed` and not `level` (I1). Before W6 the two were the same number on a
				// seated row and the distinction did not show; with a producing rate they are not,
				// and measuring against the level would report the city's own unpoured making as
				// though the founder had taken it.
				long drank = ground.Water.Level - (row.Stocks.Water.Level - row.OwedWater);
				long ate = ground.Food.Level - (row.Stocks.Food.Level - row.OwedFood);
				string note = KingdomCityRules.ReconcileNote(drank, ate);
				if (note != null)
				{
					// Both directions are recorded; only a SHORTFALL reaches the founder's own
					// register. A cask holding less than the books had is something they can act
					// on — they poured it, or something took it — and a cask holding more is the
					// world working. STANDARDS 7b's other half: the ledger is for what the founder
					// can still do something about, and the log is for everything.
					if (drank < 0L || ate < 0L)
					{
						System.Ledger.Note("{{K|" + note + "}}");
					}
					KingdomLog.Log("city: reconcile " + Z.ZoneID + " water=" + drank + " food=" + ate);
				}
			}
			if (water.Spilled != 0L || food.Spilled != 0L)
			{
				// A claim the containers can no longer hold room for. Dropped rather than carried,
				// for the same reason a harvest with nowhere to go is left in the field — and said,
				// because §3.9 rules that nothing is silently forgiven.
				KingdomLog.Log("city: reconcile " + Z.ZoneID + " spilled water=" + water.Spilled + " food=" + food.Spilled);
			}
			KingdomStocks trued = new KingdomStocks(
				new KingdomStockPair(water.NextLevel, wet ? ground.Water.Capacity : row.Stocks.Water.Capacity),
				new KingdomStockPair(food.NextLevel, fed ? ground.Food.Capacity : row.Stocks.Food.Capacity),
				ground.Materials);
			KingdomCityState written;
			if (!state.TryWithZone(
					index,
					row.WithReading(TimeTicks, trued, row.Roofs, Survey.Defence(), WaterMadePerDay(Survey), FoodMadePerDay(Survey))
						.WithOwed(water.NextOwed, food.NextOwed, row.OwedMaterials),
					out written,
					out fault))
			{
				Refuse("reconcile", fault);
				return state;
			}
			return written;
		}

		/// <summary>
	}
}
