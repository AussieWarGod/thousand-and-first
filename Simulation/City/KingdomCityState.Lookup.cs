using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	internal sealed partial class KingdomCityState
	{
		internal bool TryZone(int index, out KingdomZoneRow row)
		{
			return TryRow(zones, index, out row);
		}

		internal bool TryWork(int index, out KingdomWorkRow row)
		{
			return TryRow(works, index, out row);
		}

		internal bool TryResident(int index, out KingdomResidentRow row)
		{
			return TryRow(residents, index, out row);
		}

		/// <summary>
		/// Where the row for this resident id sits, or false.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;8.3: the id is the identity and the body is a view, so
		/// every reader that starts from a settler starts here. A linear walk over at most sixty
		/// rows and no dictionary, for the reason &sect;0.0(c) gives about per-row object headers:
		/// a map keyed on sixty ints would cost more to hold than the rows it indexes.
		/// </para>
		/// </summary>
		internal bool TryResidentIndex(int residentId, out int index)
		{
			for (index = 0; index < residents.Length; index++)
			{
				if (residents[index].ResidentId == residentId)
				{
					return true;
				}
			}
			index = -1;
			return false;
		}

		/// <summary>
		/// This book with a whole new roster, in one copy-on-write publish. Refuses over the cap
		/// and refuses a duplicated id rather than seating a settler twice &mdash; the row-level
		/// half of invariant I3, checked where the roster is written rather than where it is read.
		/// </summary>
		internal bool TryWithResidents(KingdomResidentRow[] rows, out KingdomCityState next, out KingdomCityFault fault)
		{
			next = null;
			int count = Length(rows);
			if (count > MaxResidents)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			for (int i = 0; i < count; i++)
			{
				for (int j = i + 1; j < count; j++)
				{
					if (rows[i].ResidentId == rows[j].ResidentId)
					{
						fault = KingdomCityFault.DuplicateBinding;
						return false;
					}
				}
			}
			next = new KingdomCityState(SchemaVersion, RulesVersion, SettlementId, ProcessedThroughTick, Stocks,
				zones, works, Copy(rows), clocks, told, toldCount, toldNext);
			fault = KingdomCityFault.None;
			return true;
		}

		internal bool TryClock(int index, out KingdomClockRow row)
		{
			return TryRow(clocks, index, out row);
		}

		/// <summary>The told-log, oldest first. Index 0 is the oldest line still held, not the
		/// oldest line ever written: the ring forgets, and says so by counting.</summary>
		internal bool TryTold(int ordinalFromOldest, out KingdomToldRow row)
		{
			row = default(KingdomToldRow);
			if (ordinalFromOldest < 0 || ordinalFromOldest >= toldCount)
			{
				return false;
			}
			int oldest = (toldCount < MaxToldEntries) ? 0 : toldNext;
			return TryRow(told, (oldest + ordinalFromOldest) % MaxToldEntries, out row);
		}

	}
}
