namespace ThousandAndFirst.Simulation.City
{
	/// <summary>One retained live endpoint. Amount/room are a frozen ground observation; edge
	/// masks record which source/target question this endpoint won exactly.</summary>
	internal struct KingdomDistanceEndpointState
	{
		internal int EndpointId;

		internal string ObjectId;

		internal short X;

		internal short Y;

		internal int DedicationOrdinal;

		internal long WaterAmount;

		internal long FoodAmount;

		internal long WaterRoom;

		internal long FoodRoom;

		internal byte WaterHolderEdges;

		internal byte FoodHolderEdges;

		internal byte WaterTargetEdges;

		internal byte FoodTargetEdges;

		internal long Amount(KingdomStockKind kind)
		{
			return (kind == KingdomStockKind.Water) ? WaterAmount
				: ((kind == KingdomStockKind.Food) ? FoodAmount : 0L);
		}

		internal long Room(KingdomStockKind kind)
		{
			return (kind == KingdomStockKind.Water) ? WaterRoom
				: ((kind == KingdomStockKind.Food) ? FoodRoom : 0L);
		}

		internal bool WinsHolder(KingdomStockKind kind, KingdomZoneStep edge)
		{
			int ordinal = (int)edge;
			if (ordinal < 0 || ordinal >= KingdomDistanceRules.EdgesPerZone) return false;
			byte mask = (kind == KingdomStockKind.Water) ? WaterHolderEdges
				: ((kind == KingdomStockKind.Food) ? FoodHolderEdges : (byte)0);
			return (mask & (1 << ordinal)) != 0;
		}

		internal bool WinsTarget(KingdomStockKind kind, KingdomZoneStep edge)
		{
			int ordinal = (int)edge;
			if (ordinal < 0 || ordinal >= KingdomDistanceRules.EdgesPerZone) return false;
			byte mask = (kind == KingdomStockKind.Water) ? WaterTargetEdges
				: ((kind == KingdomStockKind.Food) ? FoodTargetEdges : (byte)0);
			return (mask & (1 << ordinal)) != 0;
		}
	}
}
