namespace ThousandAndFirst
{
	public static partial class KingdomPurposePortfolioRules
	{
		public static bool TryOutstanding(KingdomPurposeOperationReceipt Operation,
			out int Water, out int Food, out string Materials)
		{
			Water = 0;
			Food = 0;
			Materials = null;
			if (Operation == null
				|| !ScalarAccounting(Operation.WaterRequested, Operation.WaterSpent,
					Operation.WaterLost)
				|| !ScalarAccounting(Operation.FoodRequested, Operation.FoodSpent,
					Operation.FoodLost)
				|| !MaterialAccounting(Operation.MaterialRequested, Operation.MaterialSpent,
					Operation.MaterialLost, out KingdomMaterialDebitCost outstanding)) return false;
			Water = Operation.WaterRequested - Operation.WaterSpent - Operation.WaterLost;
			Food = Operation.FoodRequested - Operation.FoodSpent - Operation.FoodLost;
			Materials = outstanding.ToClaimString();
			return true;
		}

		public static bool FullyDebited(KingdomPurposeOperationReceipt Operation)
		{
			return TryOutstanding(Operation, out int water, out int food, out string material)
				&& water == 0 && food == 0
				&& KingdomMaterialDebitCost.TryParseClaim(material, out var outstanding)
				&& outstanding.IsEmpty;
		}

		private static bool ScalarAccounting(int Requested, int Spent, int Lost)
		{
			return Requested >= 0 && Spent >= 0 && Lost >= 0
				&& (long)Spent + Lost <= Requested;
		}

		private static bool MaterialAccounting(string Requested, string Spent, string Lost,
			out KingdomMaterialDebitCost Outstanding)
		{
			Outstanding = null;
			if (!KingdomMaterialDebitCost.TryParseClaim(Requested, out var requested)
				|| !KingdomMaterialDebitCost.TryParseClaim(Spent, out var spent)
				|| !KingdomMaterialDebitCost.TryParseClaim(Lost, out var lost)) return false;
			KingdomMaterialTally materials = new KingdomMaterialTally();
			KingdomBitTally bits = new KingdomBitTally();
			KingdomExoticTally exotics = new KingdomExoticTally();
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
			{
				long used = (long)spent.Materials.Get((KingdomMaterial)i)
					+ lost.Materials.Get((KingdomMaterial)i);
				int total = requested.Materials.Get((KingdomMaterial)i);
				if (used > total) return false;
				materials.Set((KingdomMaterial)i, total - (int)used);
			}
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
			{
				long used = (long)spent.Bits.Get(i) + lost.Bits.Get(i);
				int total = requested.Bits.Get(i);
				if (used > total) return false;
				bits.Set(i, total - (int)used);
			}
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
			{
				long used = (long)spent.Exotics.Get((KingdomExotic)i)
					+ lost.Exotics.Get((KingdomExotic)i);
				int total = requested.Exotics.Get((KingdomExotic)i);
				if (used > total) return false;
				exotics.Set((KingdomExotic)i, total - (int)used);
			}
			Outstanding = new KingdomMaterialDebitCost(materials, bits, exotics);
			return true;
		}

		private static string EmptyClaim()
		{
			return new KingdomMaterialDebitCost().ToClaimString();
		}
	}
}
