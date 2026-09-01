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
		// What the retired sightings used to answer
		// ==================================================================================

		/// <summary>
		/// Writes down what this zone's works carry, on the pass that stood in it. Rewritten from
		/// the ground every time, including down to zero: a reservoir that was struck stops
		/// counting toward the city the pass the founder sees the empty plot, and never before.
		/// <para>
		/// This is <c>KingdomSubsidence.RecordZone</c>'s discipline unchanged; what moved is where
		/// it is written. Five game-state ints became one row of the city book, and the arithmetic
		/// downstream reads the same numbers.
		/// </para>
		/// </summary>
		public static void RecordSupports(KingdomSystem System, Zone Z, KingdomSurvey Survey,
			int Roof, int WaterCarry, int FoodCarry, int StorageCapacity, long TimeTicks)
		{
			if (System == null || Z == null || Survey == null || System.City == null)
			{
				return;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			if (!Ensure(System, Z, out state, out fault))
			{
				Refuse("record supports", fault);
				return;
			}
			int index;
			KingdomZoneRow row;
			if (!IndexOf(state, Z.ZoneID, out index) || !state.TryZone(index, out row))
			{
				return;
			}
			KingdomStocks stocks = new KingdomStocks(
				new KingdomStockPair(row.Stocks.Water.Level, Floor(StorageCapacity)),
				row.Stocks.Food,
				row.Stocks.Materials);
			KingdomCityState written;
			// FoodCarry is a frozen row/API column, not a live rate. Ignore even a legacy or external
			// nonzero argument so city integration cannot mint physical food from support metadata.
			if (!state.TryWithZone(index, row.WithReading(TimeTicks, stocks, Floor(Roof),
				row.Defence, Floor(WaterCarry), 0), out written, out fault))
			{
				Refuse("record supports", fault);
				return;
			}
			Publish(System, written);
		}

		/// <summary>
		/// Writes down what this zone's dedicated pantries hold and can hold, on the pass that
		/// stood in it. <c>KingdomCrops.RecordLarders</c>'s own contract, in the book.
		/// </summary>
		public static void RecordLarder(KingdomSystem System, Zone Z, int FoodPhysical,
			int FoodAvailable, int FoodCapacity, long TimeTicks)
		{
			if (System == null || Z == null || System.City == null)
			{
				return;
			}
			KingdomCityState state;
			KingdomCityFault fault;
			if (!Ensure(System, Z, out state, out fault))
			{
				Refuse("record larder", fault);
				return;
			}
			int index;
			KingdomZoneRow row;
			if (!IndexOf(state, Z.ZoneID, out index) || !state.TryZone(index, out row))
			{
				return;
			}
			// W7 repair, and the same one Ground() carries: a pantry holding more than it was
			// counted able to hold is the count being wrong, never the shelves. Reading it through
			// Measured raises the ceiling to the reading rather than refusing the whole write.
			KingdomStockPair larder = Measured(FoodAvailable,
				KingdomOrdinaryFoodAuthority.EffectiveCapacity(
					FoodPhysical, FoodAvailable, FoodCapacity));
			KingdomProductionStep food;
			if (!KingdomProductionRules.TryReconcile(larder.Level, larder.Capacity, row.OwedFood, out food, out fault))
			{
				Refuse("record larder", fault);
				return;
			}
			KingdomStocks stocks = new KingdomStocks(
				row.Stocks.Water,
				new KingdomStockPair(food.NextLevel, larder.Capacity),
				row.Stocks.Materials);
			KingdomCityState written;
			if (!state.TryWithZone(
					index,
					row.WithReading(TimeTicks, stocks, row.Roofs, row.Defence, row.WaterCarry, 0)
						.WithOwed(row.OwedWater, food.NextOwed, row.OwedMaterials),
					out written,
					out fault))
			{
				Refuse("record larder", fault);
				return;
			}
			Publish(System, written);
		}

		/// <summary>
		/// Every claimed zone of the seated city EXCEPT the one the pass is in, as each was last
		/// read. The exclusion is the whole point: this zone has just been counted from the ground,
		/// and counting it twice would double its cisterns.
		/// <para>
		/// The projection &sect;1.2(b) promised: the rows hand a <c>ZoneSighting</c> to the
		/// subsidence arithmetic, so that arithmetic does not change at all — it simply stops
		/// reading a dictionary of ints.
		/// </para>
		/// </summary>
		public static List<KingdomSubsidenceRules.ZoneSighting> OtherZones(KingdomSystem System, Zone Z)
		{
			List<KingdomSubsidenceRules.ZoneSighting> others = new List<KingdomSubsidenceRules.ZoneSighting>();
			KingdomCityBook book = (System == null) ? null : System.City;
			if (book == null)
			{
				return others;
			}
			// Normalized before it is indexed: these read the columns directly rather than
			// materialising the whole model for one projection, so the columns have to be square
			// first. Normalize is idempotent and O(rows).
			book.Normalize();
			string here = (Z == null) ? null : Z.ZoneID;
			for (int i = 0; i < book.ZoneCount; i++)
			{
				if (book.ZoneLastReadTicks[i] <= 0L || string.Equals(book.ZoneIds[i], here, StringComparison.Ordinal))
				{
					continue;
				}
				others.Add(new KingdomSubsidenceRules.ZoneSighting(
					book.ZoneWaterCarries[i],
					book.ZoneFoodCarries[i],
					book.ZoneRoofs[i],
					(int)Clamp(book.ZoneWaterCapacities[i]),
					DayStamp(book.ZoneLastReadTicks[i])));
			}
			return others;
		}

		/// <summary>
		/// Room the city's OTHER claimed zones were last read holding for a harvest.
		/// <c>KingdomCrops.LarderRoomElsewhere</c>'s own contract, in the book.
		/// </summary>
		public static int LarderRoomElsewhere(KingdomSystem System, Zone Z)
		{
			KingdomCityBook book = (System == null) ? null : System.City;
			if (book == null)
			{
				return 0;
			}
			book.Normalize();
			string here = (Z == null) ? null : Z.ZoneID;
			long room = 0L;
			for (int i = 0; i < book.ZoneCount; i++)
			{
				if (book.ZoneLastReadTicks[i] <= 0L || string.Equals(book.ZoneIds[i], here, StringComparison.Ordinal))
				{
					continue;
				}
				long space = book.ZoneFoodCapacities[i] - book.ZoneFoodLevels[i];
				if (space > 0L)
				{
					room += space;
				}
			}
			return (int)Clamp(room);
		}

	}
}
