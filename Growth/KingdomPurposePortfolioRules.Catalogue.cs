using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPurposePortfolioRules
	{
		private static readonly KingdomPurposePortfolioRecipe[] Recipes =
		{
			R(KingdomPurposeKind.Deep, KingdomPurposeKind.Forge, "deep-ore-assay",
				"sealed deep-ore assay", 12, 0, "stone:6,scrap:2",
				KingdomMaterial.Scrap, 1, 0),
			R(KingdomPurposeKind.Forge, KingdomPurposeKind.Deep, "drill-crown",
				"worked drill-crown", 16, 0, "shapedstone:2,workedmetal:4",
				KingdomMaterial.WorkedMetal, 1, 0),
			R(KingdomPurposeKind.Forge, KingdomPurposeKind.Harvest, "irrigation-manifold",
				"sealed irrigation manifold", 16, 0, "shapedtimber:2,workedmetal:3",
				KingdomMaterial.WorkedMetal, 1, 0),
			R(KingdomPurposeKind.Harvest, KingdomPurposeKind.Forge, "quench-provision-lot",
				"sealed quench provision lot", 12, 8, "shapedtimber:1",
				KingdomMaterial.ShapedTimber, 1, 6),
			R(KingdomPurposeKind.Harvest, KingdomPurposeKind.Flesh, "sterile-culture-mash",
				"sealed sterile culture mash", 10, 8, "workedmetal:1",
				KingdomMaterial.WorkedMetal, 1, 6),
			R(KingdomPurposeKind.Flesh, KingdomPurposeKind.Harvest, "blightproof-seed-graft",
				"sealed blightproof seed-graft", 12, 4, "brush:4",
				KingdomMaterial.Brush, 1, 0),
			R(KingdomPurposeKind.Flesh, KingdomPurposeKind.Chrome, "living-neural-lattice",
				"sealed living neural lattice", 16, 4, "brush:4,workedmetal:1",
				KingdomMaterial.WorkedMetal, 1, 0),
			R(KingdomPurposeKind.Chrome, KingdomPurposeKind.Flesh,
				"psybernetic-control-wafer", "sealed psybernetic control wafer", 16, 0,
				"scrap:4,workedmetal:2", KingdomMaterial.WorkedMetal, 1, 0),
			R(KingdomPurposeKind.Chrome, KingdomPurposeKind.Deep, "strata-sense-coil",
				"sealed strata-sense coil", 14, 0, "scrap:4,workedmetal:2",
				KingdomMaterial.WorkedMetal, 1, 0),
			R(KingdomPurposeKind.Deep, KingdomPurposeKind.Chrome, "conductor-assay",
				"sealed conductor assay", 10, 0, "stone:4,scrap:2",
				KingdomMaterial.Scrap, 1, 0)
		};

		public static IList<KingdomPurposePortfolioRecipe> AllRecipes()
		{
			List<KingdomPurposePortfolioRecipe> copy = new List<KingdomPurposePortfolioRecipe>();
			for (int i = 0; i < Recipes.Length; i++) copy.Add(Recipes[i].Copy());
			return copy.AsReadOnly();
		}

		public static bool TryRecipe(KingdomPurposeKind Source,
			KingdomPurposeKind Destination, out KingdomPurposePortfolioRecipe Recipe)
		{
			for (int i = 0; i < Recipes.Length; i++)
				if (Recipes[i].Source == Source && Recipes[i].Destination == Destination)
				{
					Recipe = Recipes[i].Copy();
					return true;
				}
			Recipe = null;
			return false;
		}

		public static bool Compatible(KingdomPurposeKind A, KingdomPurposeKind B)
		{
			return TryRecipe(A, B, out _) && TryRecipe(B, A, out _);
		}

		public static IList<KingdomPurposeKind> Partners(KingdomPurposeKind Kind)
		{
			List<KingdomPurposeKind> partners = new List<KingdomPurposeKind>();
			for (int i = 1; i <= (int)KingdomPurposeKind.Harvest; i++)
			{
				KingdomPurposeKind candidate = (KingdomPurposeKind)i;
				if (Compatible(Kind, candidate)) partners.Add(candidate);
			}
			return partners.AsReadOnly();
		}

		public static string BuildKey(KingdomPurposeKind Kind)
		{
			switch (Kind)
			{
			case KingdomPurposeKind.Flesh: return "chimerictheatre";
			case KingdomPurposeKind.Chrome: return "becomingannexe";
			case KingdomPurposeKind.Deep: return "deepbore";
			case KingdomPurposeKind.Forge: return "greatfoundry";
			case KingdomPurposeKind.Harvest: return "realmgranary";
			default: return null;
			}
		}

		public static bool TryBuildKind(string Key, out KingdomPurposeKind Kind)
		{
			for (int i = 1; i <= (int)KingdomPurposeKind.Harvest; i++)
				if (string.Equals(Key, BuildKey((KingdomPurposeKind)i),
					StringComparison.Ordinal))
				{
					Kind = (KingdomPurposeKind)i;
					return true;
				}
			Kind = KingdomPurposeKind.None;
			return false;
		}

		public static string PurposeName(KingdomPurposeKind Kind)
		{
			switch (Kind)
			{
			case KingdomPurposeKind.Flesh: return "the chimeric theatre";
			case KingdomPurposeKind.Chrome: return "the becoming annexe";
			case KingdomPurposeKind.Deep: return "the Deep-Bore";
			case KingdomPurposeKind.Forge: return "the Great Foundry";
			case KingdomPurposeKind.Harvest: return "the Granary-Colossus";
			default: return "no purpose";
			}
		}

		private static KingdomPurposePortfolioRecipe R(KingdomPurposeKind Source,
			KingdomPurposeKind Destination, string CargoKey, string CargoName, int Water,
			int Food, string Materials, KingdomMaterial Embodied, int Units, int CarriedFood)
		{
			if (!KingdomMaterialRules.TryParseMaterialCost(Materials,
				out KingdomMaterialTally tally, out string failure))
				throw new InvalidOperationException("Invalid purpose recipe " + CargoKey + ": " + failure);
			return new KingdomPurposePortfolioRecipe
			{
				Source = Source, Destination = Destination, CargoKey = CargoKey,
				CargoName = CargoName, WaterDrams = Water, FoodServings = Food,
				MaterialClaim = new KingdomMaterialDebitCost(tally).ToClaimString(),
				EmbodiedMaterial = Embodied, EmbodiedUnits = Units, CarriedFood = CarriedFood
			};
		}
	}
}
