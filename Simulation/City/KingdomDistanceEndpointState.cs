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

		/// <summary>Freezes one measured endpoint through the production cache-row seam.</summary>
		internal static KingdomDistanceEndpointState Capture(int EndpointId, string ObjectId,
			short X, short Y, int DedicationOrdinal, long WaterAmount, long FoodAmount,
			long WaterRoom, long FoodRoom, byte WaterHolderEdges, byte FoodHolderEdges,
			byte WaterTargetEdges, byte FoodTargetEdges)
		{
			return new KingdomDistanceEndpointState
			{
				EndpointId = EndpointId,
				ObjectId = ObjectId,
				X = X,
				Y = Y,
				DedicationOrdinal = DedicationOrdinal,
				WaterAmount = WaterAmount,
				FoodAmount = FoodAmount,
				WaterRoom = WaterRoom,
				FoodRoom = FoodRoom,
				WaterHolderEdges = WaterHolderEdges,
				FoodHolderEdges = FoodHolderEdges,
				WaterTargetEdges = WaterTargetEdges,
				FoodTargetEdges = FoodTargetEdges
			};
		}

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
