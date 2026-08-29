using System;

namespace ThousandAndFirst
{
	/// <summary>One immutable directed row in the accepted five-work purpose cycle.</summary>
	public sealed class KingdomPurposePortfolioRecipe
	{
		public KingdomPurposeKind Source;
		public KingdomPurposeKind Destination;
		public string CargoKey;
		public string CargoName;
		public int WaterDrams;
		public int FoodServings;
		public string MaterialClaim;
		public KingdomMaterial EmbodiedMaterial;
		public int EmbodiedUnits;
		public int CarriedFood;

		public KingdomPurposePortfolioRecipe Copy()
		{
			return (KingdomPurposePortfolioRecipe)MemberwiseClone();
		}
	}
}
