using System;

namespace ThousandAndFirst
{

	[Serializable]
	public sealed class KingdomGrowthScarcitySnapshot
	{
		public int DryStreak;
		public bool Withered;
		public int HungerStreak;
		public bool Famished;
		public KingdomRules.MealVerdict LastMeal;
		public int MealShade;
		public bool ScrapsAnnounced;
		public long ElapsedTicks;
		public int Days;
		public int Population;
		public int Stage;
		public int UpkeepRequested;
		public int WaterAvailable;
		public int RationsAvailable;
		public int Foraged;
		public int Eaten;
		public int FromDish;
		public int Kitchens;
		public string DishName;
		public string DishText;
		public string DishStaple;
		public string DishSource;
		public KingdomGrowthComposedBite ComposedBite;
		public int RequestedWater;
		public int ProvedWater;
		public int RequestedRations;
		public int ProvedRations;
		public int StoresPolicy;
		public int DistrictPercent;
		public KingdomGrowthThirstOutcome ThirstOutcome;
		public KingdomGrowthHungerOutcome HungerOutcome;
		public bool Thirsting;
		public bool Starving;
		public bool Withering;
		public bool Famishing;
		public bool Healthy;
	}
}
