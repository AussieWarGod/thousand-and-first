namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Exact executable container demand after a ground survey.</summary>
	internal readonly struct KingdomContainerDemandReceipt
	{
		internal readonly int VisibleUnits;
		internal readonly int RestUnits;
		internal readonly int WaterMovable;
		internal readonly int FoodMovable;
		internal readonly int MaterialsMovable;
		internal readonly int WaterBlocked;
		internal readonly int FoodBlocked;
		internal readonly int MaterialsBlocked;

		internal KingdomContainerDemandReceipt(int visibleUnits, int restUnits,
			int waterMovable, int foodMovable, int materialsMovable,
			int waterBlocked, int foodBlocked, int materialsBlocked)
		{
			VisibleUnits = visibleUnits;
			RestUnits = restUnits;
			WaterMovable = waterMovable;
			FoodMovable = foodMovable;
			MaterialsMovable = materialsMovable;
			WaterBlocked = waterBlocked;
			FoodBlocked = foodBlocked;
			MaterialsBlocked = materialsBlocked;
		}

		internal int Units { get { return VisibleUnits + RestUnits; } }

		internal int OwedThirds
		{
			get { return Units * KingdomCatchUpRules.ThirdsPerUnit; }
		}
	}
}
