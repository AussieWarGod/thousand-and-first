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
		// ==================================================================================
		// Small shared helpers
		// ==================================================================================

		private static KingdomCatchUpCounter CityCounter(KingdomCityBook book)
		{
			KingdomCityState state;
			KingdomCityFault fault;
			if (!book.TryRead(out state, out fault))
			{
				return new KingdomCatchUpCounter(0, 0);
			}
			return KingdomCityRules.CityCounter(state);
		}

		/// <summary>The book with a row for this zone, creating an unread one if the city has just
		/// claimed it. An unread row contributes nothing anywhere: nothing is invented for ground
		/// the game has never looked at.</summary>
		private static bool Ensure(KingdomSystem System, Zone Z, out KingdomCityState state, out KingdomCityFault fault)
		{
			state = null;
			if (!System.City.TryRead(out state, out fault))
			{
				return false;
			}
			int index;
			string district;
			System.ZoneDistricts.TryGetValue(Z.ZoneID, out district);
			int code = KingdomCityRules.DistrictCode(district);
			if (IndexOf(state, Z.ZoneID, out index))
			{
				KingdomZoneRow existing;
				state.TryZone(index, out existing);
				if (existing.DistrictCode == code)
				{
					return true;
				}
				KingdomCityState zoned;
				if (!state.TryWithZone(index, existing.WithDistrictCode(code), out zoned, out fault))
				{
					return false;
				}
				state = zoned;
				return true;
			}
			List<KingdomZoneRow> rows = new List<KingdomZoneRow>();
			for (int i = 0; i < state.ZoneCount; i++)
			{
				KingdomZoneRow row;
				if (state.TryZone(i, out row))
				{
					rows.Add(row);
				}
			}
			if (rows.Count >= KingdomCityState.MaxZones)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			rows.Add(new KingdomZoneRow(Z.ZoneID, code, 0L, default(KingdomStocks), 0, 0, 0, 0, 0, 0, 0));
			return Rebuild(state, rows, out state, out fault);
		}

		private static bool Rebuild(KingdomCityState state, List<KingdomZoneRow> zones, out KingdomCityState next, out KingdomCityFault fault)
		{
			KingdomWorkRow[] works = new KingdomWorkRow[state.WorkCount];
			for (int i = 0; i < works.Length; i++)
			{
				state.TryWork(i, out works[i]);
			}
			return Compose(state, zones.ToArray(), works, out next, out fault);
		}

		private static bool Rebuild(KingdomCityState state, List<KingdomWorkRow> works, out KingdomCityState next, out KingdomCityFault fault)
		{
			KingdomZoneRow[] zones = new KingdomZoneRow[state.ZoneCount];
			for (int i = 0; i < zones.Length; i++)
			{
				state.TryZone(i, out zones[i]);
			}
			return Compose(state, zones, works.ToArray(), out next, out fault);
		}

		private static bool Compose(KingdomCityState state, KingdomZoneRow[] zones, KingdomWorkRow[] works, out KingdomCityState next, out KingdomCityFault fault)
		{
			KingdomResidentRow[] residents = new KingdomResidentRow[state.ResidentCount];
			for (int i = 0; i < residents.Length; i++)
			{
				state.TryResident(i, out residents[i]);
			}
			KingdomClockRow[] clocks = new KingdomClockRow[state.ClockCount];
			for (int i = 0; i < clocks.Length; i++)
			{
				state.TryClock(i, out clocks[i]);
			}
			if (!KingdomCityState.TryCreate(state.SchemaVersion, state.RulesVersion, state.SettlementId,
				state.ProcessedThroughTick, state.Stocks, zones, works, residents, clocks, out next, out fault))
			{
				return false;
			}
			for (int i = 0; i < state.ToldCount; i++)
			{
				KingdomToldRow told;
				KingdomCityState carried;
				if (!state.TryTold(i, out told) || !next.TryTell(told, out carried, out fault))
				{
					return false;
				}
				next = carried;
			}
			return true;
		}

		/// <summary>The one publisher (&sect;1.3): the city's own totals are recomputed from its
		/// rows and the whole book is written in one assignment, after the rules have succeeded.</summary>
		private static void Publish(KingdomSystem System, KingdomCityState state)
		{
			KingdomStocks stocks;
			KingdomCityFault fault;
			KingdomCityState totalled = state;
			if (KingdomCityRules.TryCityStocks(state, out stocks))
			{
				KingdomCityState written;
				if (state.TryWithStocks(stocks, out written, out fault))
				{
					totalled = written;
				}
			}
			if (!System.City.TryPublish(totalled, out fault))
			{
				Refuse("publish", fault);
				return;
			}
			KingdomResidents.ProjectCompatibility(System);
		}

		private static KingdomStocks Ground(KingdomSurvey Survey, KingdomZoneRow row)
		{
			return new KingdomStocks(
				Measured(Survey.StoredWater, Survey.StorageCapacity),
				Measured(Survey.FoodStored, Survey.FoodCapacity),
				row.Stocks.Materials);
		}

		/// <summary>
		/// One ground reading, with the ceiling raised to whatever is actually standing in it.
		/// <para>
		/// W7 repair. <c>KingdomProductionRules.TryReconcile</c> refuses <c>InvalidCapacity</c>
		/// when the ground holds more than the ground can hold, which is a perfectly reachable
		/// state: a founder who hand-stuffs a dedicated larder past its counted capacity, or a
		/// design whose capacity was retuned downward under a full vessel. That refusal used to
		/// abandon the WHOLE reconcile -- both stock kinds, because the two were joined by
		/// <c>||</c> -- and leave a stale rate stamped on the row.
		/// </para>
		/// <para>
		/// &sect;3.1's ruling settles it: <b>the ground wins for anything physical.</b> A vessel
		/// holding more than the books said it could is the books being wrong about the ceiling,
		/// not the vessel being wrong about its contents. So the ceiling is raised to the reading
		/// and nothing is clamped away -- the alternative, clamping the level, would silently
		/// destroy real drams the founder can walk up to and see.
		/// </para>
		/// </summary>
		private static KingdomStockPair Measured(int level, int capacity)
		{
			long held = Floor(level);
			long ceiling = Floor(capacity);
			return new KingdomStockPair(held, (ceiling < held) ? held : ceiling);
		}

	}
}
