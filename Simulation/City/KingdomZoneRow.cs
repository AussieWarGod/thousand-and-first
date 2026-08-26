using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One claimed zone as the model last read it, what its works make in a day, and what it owes
	/// the ground.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;0.0(c) budgeted eighty bytes: id ref 8 + district 4 +
	/// LastReadTick 8 + six stock/capacity longs 48 + roofs 4 + defence 4 + pad 4. W1 widened it
	/// to ninety-six for two reasons the wave could not ship without, and &sect;0.0(c) carries the
	/// same edit:
	/// </para>
	/// <list type="bullet">
	/// <item><description><see cref="WaterCarry"/> and <see cref="FoodCarry"/>, because the
	/// <c>ZoneSighting</c> the subsidence arithmetic reads is a projection of CARRIES, not of
	/// levels, and a row that cannot answer it cannot replace the game-state keys it retires.
	/// <see cref="Roofs"/> is the third carry and was already budgeted.</description></item>
	/// <item><description>the signed debt, PER STOCK KIND rather than as one net figure. A single
	/// net counter cannot say that a zone owes a food landing and a water draw at once, which is
	/// the ordinary case for a granary zone the city has been drinking out of; three signed
	/// figures can, and they are also the quantity <c>KingdomDrainRules</c> actually needs. The
	/// weighted thirds &sect;3.5 reports as <c>owed</c> are derived from these by
	/// <c>KingdomCityRules.CounterFor</c>, so there is one debt and one home for it.</description></item>
	/// </list>
	/// <para>
	/// The owed figures are <c>int</c> and not <c>long</c> on purpose: a dram and a serving are
	/// counted in <c>int</c> everywhere the ground counts them (<c>LiquidVolume.Volume</c>, an
	/// inventory tally), and a debt wider than the thing it is owed against would be a lie about
	/// what can be paid.
	/// </para>
	/// </summary>
	internal readonly struct KingdomZoneRow
	{
		internal readonly string ZoneId;

		/// <summary>The district's stable code. The name lives in the district registry, which is
		/// data-driven under the extensibility law, so the row carries a code and not a string.</summary>
		internal readonly int DistrictCode;

		internal readonly long LastReadTick;

		internal readonly KingdomStocks Stocks;

		/// <summary>Roof carry: what this zone's works hold up in a day, as the support tally
		/// counts it. A carry and not a level, like <see cref="WaterCarry"/>.</summary>
		internal readonly int Roofs;

		internal readonly int Defence;

		/// <summary>Water carry: drams this zone's works make in a day, at the effectiveness the
		/// pass that read it measured.</summary>
		internal readonly int WaterCarry;

		/// <summary>Food carry: servings this zone's works make in a day, on the same terms.</summary>
		internal readonly int FoodCarry;

		/// <summary>Signed debt in drams. Positive lands into this zone's vessels, negative draws
		/// out of them (LIVING-CITY-ARCHITECTURE &sect;3.9).</summary>
		internal readonly int OwedWater;

		/// <summary>Signed debt in servings, on the same terms.</summary>
		internal readonly int OwedFood;

		/// <summary>Signed debt in refined units, on the same terms.</summary>
		internal readonly int OwedMaterials;

		internal KingdomZoneRow(
			string zoneId,
			int districtCode,
			long lastReadTick,
			KingdomStocks stocks,
			int roofs,
			int defence,
			int waterCarry,
			int foodCarry,
			int owedWater,
			int owedFood,
			int owedMaterials)
		{
			ZoneId = zoneId;
			DistrictCode = districtCode;
			LastReadTick = lastReadTick;
			Stocks = stocks;
			Roofs = roofs;
			Defence = defence;
			WaterCarry = waterCarry;
			FoodCarry = foodCarry;
			OwedWater = owedWater;
			OwedFood = owedFood;
			OwedMaterials = owedMaterials;
		}

		/// <summary>What this zone owes the ground for one kind, signed. Total over the enum: an
		/// unrecognised kind owes nothing rather than reading as water.</summary>
		internal int OwedOf(KingdomStockKind kind)
		{
			switch (kind)
			{
			case KingdomStockKind.Water:
				return OwedWater;
			case KingdomStockKind.Food:
				return OwedFood;
			case KingdomStockKind.Materials:
				return OwedMaterials;
			default:
				return 0;
			}
		}

		internal KingdomZoneRow WithOwed(int owedWater, int owedFood, int owedMaterials)
		{
			return new KingdomZoneRow(ZoneId, DistrictCode, LastReadTick, Stocks, Roofs, Defence,
				WaterCarry, FoodCarry, owedWater, owedFood, owedMaterials);
		}

		internal KingdomZoneRow WithOwedOf(KingdomStockKind kind, int owed)
		{
			switch (kind)
			{
			case KingdomStockKind.Water:
				return WithOwed(owed, OwedFood, OwedMaterials);
			case KingdomStockKind.Food:
				return WithOwed(OwedWater, owed, OwedMaterials);
			case KingdomStockKind.Materials:
				return WithOwed(OwedWater, OwedFood, owed);
			default:
				return this;
			}
		}

		internal KingdomZoneRow WithReading(long lastReadTick, KingdomStocks stocks, int roofs, int defence, int waterCarry, int foodCarry)
		{
			return new KingdomZoneRow(ZoneId, DistrictCode, lastReadTick, stocks, roofs, defence,
				waterCarry, foodCarry, OwedWater, OwedFood, OwedMaterials);
		}

		internal KingdomZoneRow WithDistrictCode(int districtCode)
		{
			return new KingdomZoneRow(ZoneId, districtCode, LastReadTick, Stocks, Roofs, Defence,
				WaterCarry, FoodCarry, OwedWater, OwedFood, OwedMaterials);
		}
	}
}
