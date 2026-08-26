#if TAF_TESTS
using System.Text;

namespace ThousandAndFirst.Tests
{
	internal static class KingdomRulesLogicalSource
	{
		private static readonly string[] Files =
		{
			"Core/KingdomRules.cs",
			"Core/KingdomRules.Dish.cs",
			"Core/KingdomRules.Meals.cs",
			"Core/KingdomRules.FoodIndustry.cs",
			"Core/KingdomRules.Economy.cs",
			"Core/KingdomRules.Clock.cs",
			"Core/KingdomRules.Population.cs",
			"Core/KingdomRules.Policy.cs",
			"Core/KingdomRules.RaidsAndDefence.cs",
			"Core/KingdomRules.TradeAndGrowth.cs",
			"Core/KingdomRules.InheritanceSeal.cs",
			"Core/KingdomRules.InheritanceResolution.cs",
			"Core/KingdomRules.Scarcity.cs",
			"Core/KingdomRules.Districts.cs",
			"Core/KingdomRules.Catalogue.cs",
			"Core/KingdomRules.Style.cs",
			"Core/KingdomRules.RealmConflict.cs",
			"Core/KingdomRules.Spatial.cs",
			"Core/KingdomRules.Claims.cs"
		};

		internal static string Read()
		{
			StringBuilder source = new StringBuilder();
			for (int i = 0; i < Files.Length; i++)
			{
				source.Append(TestMain.ReadRepositoryText(Files[i]));
			}
			return source.ToString();
		}
	}
}
#endif
