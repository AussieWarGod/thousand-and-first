namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Measured outcome of applying container units.</summary>
	internal readonly struct KingdomContainerSettlementReceipt
	{
		internal readonly int OwedWater;
		internal readonly int OwedFood;
		internal readonly int OwedMaterials;
		internal readonly int UnitsSpent;
		internal readonly int VisibleSpent;
		internal readonly bool CallbackFailed;

		internal KingdomContainerSettlementReceipt(int owedWater, int owedFood,
			int owedMaterials, int unitsSpent, int visibleSpent, bool callbackFailed)
		{
			OwedWater = owedWater;
			OwedFood = owedFood;
			OwedMaterials = owedMaterials;
			UnitsSpent = unitsSpent;
			VisibleSpent = visibleSpent;
			CallbackFailed = callbackFailed;
		}
	}
}
