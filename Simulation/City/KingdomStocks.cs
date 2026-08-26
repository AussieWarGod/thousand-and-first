using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The civic share, and nothing else. Player-carried and undedicated stock stays purely
	/// physical and outside the model (LIVING-CITY-ARCHITECTURE &sect;1.2(a)), which is what keeps
	/// the protection law simple: the model only ever speaks for what the founder designated.
	/// <para>
	/// Six longs, forty-eight bytes — the width LIVING-CITY-ARCHITECTURE &sect;0.0(c) budgets on
	/// both the city and the zone row.
	/// </para>
	/// </summary>
	internal readonly struct KingdomStocks
	{
		internal readonly KingdomStockPair Water;

		internal readonly KingdomStockPair Food;

		internal readonly KingdomStockPair Materials;

		internal KingdomStocks(KingdomStockPair water, KingdomStockPair food, KingdomStockPair materials)
		{
			Water = water;
			Food = food;
			Materials = materials;
		}

		internal bool TryGet(KingdomStockKind kind, out KingdomStockPair pair)
		{
			switch (kind)
			{
			case KingdomStockKind.Water:
				pair = Water;
				return true;
			case KingdomStockKind.Food:
				pair = Food;
				return true;
			case KingdomStockKind.Materials:
				pair = Materials;
				return true;
			default:
				pair = default(KingdomStockPair);
				return false;
			}
		}

		internal bool TryWith(KingdomStockKind kind, KingdomStockPair pair, out KingdomStocks next)
		{
			switch (kind)
			{
			case KingdomStockKind.Water:
				next = new KingdomStocks(pair, Food, Materials);
				return true;
			case KingdomStockKind.Food:
				next = new KingdomStocks(Water, pair, Materials);
				return true;
			case KingdomStockKind.Materials:
				next = new KingdomStocks(Water, Food, pair);
				return true;
			default:
				next = this;
				return false;
			}
		}
	}
}
