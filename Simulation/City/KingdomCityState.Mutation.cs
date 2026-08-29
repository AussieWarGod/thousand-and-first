using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	internal sealed partial class KingdomCityState
	{
		internal bool TryWithStocks(KingdomStocks stocks, out KingdomCityState next, out KingdomCityFault fault)
		{
			next = new KingdomCityState(SchemaVersion, RulesVersion, SettlementId, ProcessedThroughTick, stocks,
				zones, works, residents, clocks, told, toldCount, toldNext);
			fault = KingdomCityFault.None;
			return true;
		}

		internal bool TryWithZone(int index, KingdomZoneRow row, out KingdomCityState next, out KingdomCityFault fault)
		{
			next = null;
			KingdomZoneRow[] replaced;
			if (!TryReplace(zones, index, row, out replaced))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			next = new KingdomCityState(SchemaVersion, RulesVersion, SettlementId, ProcessedThroughTick, Stocks,
				replaced, works, residents, clocks, told, toldCount, toldNext);
			fault = KingdomCityFault.None;
			return true;
		}

		internal bool TryWithResident(int index, KingdomResidentRow row, out KingdomCityState next, out KingdomCityFault fault)
		{
			next = null;
			KingdomResidentRow[] replaced;
			if (!TryReplace(residents, index, row, out replaced))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			next = new KingdomCityState(SchemaVersion, RulesVersion, SettlementId, ProcessedThroughTick, Stocks,
				zones, works, replaced, clocks, told, toldCount, toldNext);
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// Advances the processed-through mark. Refuses a regression rather than repairing it:
		/// silently accepting a backward clock would let a corrupted save look healthy, which is
		/// the kernel's own ruling in <c>TickMath.TryValidateAdvance</c>.
		/// </summary>
		internal bool TryWithProcessedThroughTick(long processedThroughTick, out KingdomCityState next, out KingdomCityFault fault)
		{
			next = null;
			KernelFaultCode kernelFault;
			if (!TickMath.TryValidateAdvance(ProcessedThroughTick, processedThroughTick, out kernelFault))
			{
				fault = KingdomCityFaults.FromKernel(kernelFault);
				return false;
			}
			next = new KingdomCityState(SchemaVersion, RulesVersion, SettlementId, processedThroughTick, Stocks,
				zones, works, residents, clocks, told, toldCount, toldNext);
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// Writes one line into the told-log ring. The ring is bounded at
		/// <see cref="MaxToldEntries"/> and overwrites its oldest line rather than growing, so a
		/// season of happenings and a day of them differ in what is remembered and never in what
		/// is held.
		/// </summary>
		internal bool TryTell(KingdomToldRow row, out KingdomCityState next, out KingdomCityFault fault)
		{
			next = null;
			if (row.Tick < 0L)
			{
				fault = KingdomCityFault.InvalidTick;
				return false;
			}
			KingdomToldRow[] ring = new KingdomToldRow[MaxToldEntries];
			Array.Copy(told, ring, MaxToldEntries);
			ring[toldNext] = row;
			int count = (toldCount < MaxToldEntries) ? (toldCount + 1) : MaxToldEntries;
			int cursor = (toldNext + 1) % MaxToldEntries;
			next = new KingdomCityState(SchemaVersion, RulesVersion, SettlementId, ProcessedThroughTick, Stocks,
				zones, works, residents, clocks, ring, count, cursor);
			fault = KingdomCityFault.None;
			return true;
		}

		private static bool TryRow<T>(T[] rows, int index, out T row)
		{
			if (index < 0 || index >= rows.Length)
			{
				row = default(T);
				return false;
			}
			row = rows[index];
			return true;
		}

		private static bool TryReplace<T>(T[] rows, int index, T row, out T[] replaced)
		{
			replaced = null;
			if (index < 0 || index >= rows.Length)
			{
				return false;
			}
			T[] copy = new T[rows.Length];
			Array.Copy(rows, copy, rows.Length);
			copy[index] = row;
			replaced = copy;
			return true;
		}

		private static int Length<T>(T[] rows)
		{
			return (rows == null) ? 0 : rows.Length;
		}

		private static T[] Copy<T>(T[] rows)
		{
			if (rows == null)
			{
				return new T[0];
			}
			T[] copy = new T[rows.Length];
			Array.Copy(rows, copy, rows.Length);
			return copy;
		}
	}
}
