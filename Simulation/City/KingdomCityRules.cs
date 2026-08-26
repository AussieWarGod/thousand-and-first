using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The city book's arithmetic: what a zone row projects, what the city holds, what one pass
	/// owes, and where a deficit is taken from.
	/// <para>
	/// Pure and engine-free. Every figure the founder reads about a zone they are not standing in
	/// comes through here, which is the whole of &sect;1.1: the city is a book, and a zone is a page
	/// of it that happens to be open.
	/// </para>
	/// </summary>
	internal static partial class KingdomCityRules
	{
		/// <summary>The schema the book is written under. Bumped whenever a column is added,
		/// removed or retyped; Addendum 9 waives migration pre-release, so a bump is clean and
		/// deliberate rather than a migration.
		/// <para>
		/// Version 2 is W2's resident rows: the standing gains a cause, each brink window gains its
		/// own standing flag, and both warned columns are retyped from a flag to the tick the
		/// window is anchored on. A version-1 book's brink columns cannot answer when a window
		/// started, which is the one number every consumer of a brink reads.
		/// Version 3 completes the resident identity row with exact origin and arrival-label
		/// columns. The origin code and arrival tick remain the catalogue projection and sole clock;
		/// neither can preserve open guest provenance or an unparseable legacy roll label.
		/// </para>
		/// </summary>
		internal const int SchemaVersion = 3;

		/// <summary>The rules revision the book was last advanced by. Separate from the schema:
		/// a rules change that does not move a column still wants saying.</summary>
		internal const int RulesVersion = 1;

		/// <summary>A district the row does not name. Zero is "no district", which is what a zone
		/// claimed and never zoned actually is.</summary>
		internal const int NoDistrict = 0;

		/// <summary>
		/// The city's own stocks, summed from its zone rows.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;1.2(a): stocks are city-level, and that is the point —
		/// water raised in the mine and food grown on the terrace are one set of rows, and
		/// consumption anywhere draws on them. A zone nobody has ever stood in contributes nothing,
		/// which is the sighting doctrine unchanged: nothing is invented for ground the game has
		/// never looked at.
		/// </para>
		/// </summary>
		internal static bool TryCityStocks(KingdomCityState state, out KingdomStocks stocks)
		{
			stocks = default(KingdomStocks);
			if (state == null)
			{
				return false;
			}
			long water = 0L;
			long waterCap = 0L;
			long food = 0L;
			long foodCap = 0L;
			long materials = 0L;
			long materialsCap = 0L;
			for (int i = 0; i < state.ZoneCount; i++)
			{
				KingdomZoneRow row;
				if (!state.TryZone(i, out row))
				{
					return false;
				}
				if (row.LastReadTick <= 0L)
				{
					continue;
				}
				water += row.Stocks.Water.Level;
				waterCap += row.Stocks.Water.Capacity;
				food += row.Stocks.Food.Level;
				foodCap += row.Stocks.Food.Capacity;
				materials += row.Stocks.Materials.Level;
				materialsCap += row.Stocks.Materials.Capacity;
			}
			stocks = new KingdomStocks(
				new KingdomStockPair(water, waterCap),
				new KingdomStockPair(food, foodCap),
				new KingdomStockPair(materials, materialsCap));
			return true;
		}

		/// <summary>
		/// What one zone still owes the ground, as the weighted counter &sect;3.5 reports as
		/// <c>owed</c>.
		/// <para>
		/// Derived from the signed per-kind figures rather than stored beside them, so there is one
		/// debt and one home for it. Each kind that owes anything is one MEDIUM unit — one item
		/// stack into one container, or one container drained — which is the unit &sect;0.0(b)
		/// prices a landing and a draw at.
		/// </para>
		/// </summary>
		internal static KingdomCatchUpCounter CounterFor(KingdomZoneRow row)
		{
			int land = 0;
			int draw = 0;
			for (int kind = 0; kind <= (int)KingdomStockKind.Materials; kind++)
			{
				int owed = row.OwedOf((KingdomStockKind)kind);
				if (owed > 0)
				{
					land += KingdomCatchUpRules.WeightThirds(KingdomUnitWeight.Medium);
				}
				else if (owed < 0)
				{
					draw += KingdomCatchUpRules.WeightThirds(KingdomUnitWeight.Medium);
				}
			}
			return new KingdomCatchUpCounter(land, draw);
		}

		/// <summary>Everything the city still owes the ground, summed over its zones.</summary>
		internal static KingdomCatchUpCounter CityCounter(KingdomCityState state)
		{
			int land = 0;
			int draw = 0;
			for (int i = 0; state != null && i < state.ZoneCount; i++)
			{
				KingdomZoneRow row;
				if (!state.TryZone(i, out row))
				{
					continue;
				}
				KingdomCatchUpCounter counter = CounterFor(row);
				land += counter.LandThirds;
				draw += counter.DrawThirds;
			}
			return new KingdomCatchUpCounter(land, draw);
		}

	}
}
