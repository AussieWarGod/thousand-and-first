using System;
using System.Collections.Generic;
using ConsoleLib.Console;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Skills.Cooking;

namespace ThousandAndFirst
{
	internal static partial class KingdomRemovalProjectionRuntime
	{
		private const string FavoredDishType = "r_KingdomFavoredDish";

		internal static bool TryInspectCooking(out List<string> Rows, out string Failure)
		{
			Rows = new List<string>(); Failure = null;
			CookingGameState state = CookingGameState.instance;
			if (state == null) return true;
			if (state.knownRecipies == null)
				return Fail("the native learned-recipe list is absent", out Failure);
			for (int i = 0; i < state.knownRecipies.Count; i++)
			{
				CookingRecipe recipe = state.knownRecipies[i];
				if (recipe == null || recipe.GetType().Name != FavoredDishType) continue;
				if (!BaseRecipeGraph(recipe))
					return Fail("the learned realm dish contains an unknown custom component",
						out Failure);
				Rows.Add(i + "\u001f" + (recipe.DisplayName ?? "") + "\u001f"
					+ (recipe.ChefName ?? ""));
			}
			return true;
		}

		internal static bool TryConvertCooking(out int Converted, out string Failure)
		{
			Converted = 0;
			if (!TryInspectCooking(out List<string> _, out Failure)) return false;
			CookingGameState state = CookingGameState.instance;
			if (state == null) return true;
			for (int i = 0; i < state.knownRecipies.Count; i++)
			{
				CookingRecipe recipe = state.knownRecipies[i];
				if (recipe == null || recipe.GetType().Name != FavoredDishType) continue;
				state.knownRecipies[i] = ToBaseRecipe(recipe); Converted++;
			}
			return TryInspectCooking(out List<string> remaining, out Failure)
				&& (remaining.Count == 0 || Fail("a custom learned recipe remains", out Failure));
		}

		internal static void StripCampfireRecipe(GameObject Item)
		{
			Campfire fire = Item?.GetPart<Campfire>();
			if (fire == null) return;
			fire.PresetMeals = WithoutRecipeToken(fire.PresetMeals);
			ConvertRecipeList(fire.presetMeals);
			ConvertRecipeList(fire.specificProcgenMeals);
		}

		internal static bool TryInspectCampfire(GameObject Item, out List<string> Rows,
			out string Failure)
		{
			Rows = new List<string>(); Failure = null;
			Campfire fire = Item?.GetPart<Campfire>();
			if (fire == null) return true;
			if (!string.IsNullOrEmpty(fire.PresetMeals))
			{
				string[] tokens = fire.PresetMeals.Split(',');
				for (int i = 0; i < tokens.Length; i++)
					if (tokens[i].Trim() == FavoredDishType)
						Rows.Add("preset-token\u001f" + i);
			}
			if (!InspectRecipeList(fire.presetMeals, "preset", Rows, out Failure)
				|| !InspectRecipeList(fire.specificProcgenMeals, "specific", Rows,
					out Failure)) return false;
			Rows.Sort(StringComparer.Ordinal); return true;
		}

		private static bool InspectRecipeList(IList<CookingRecipe> Recipes, string Kind,
			List<string> Rows, out string Failure)
		{
			Failure = null;
			for (int i = 0; i < (Recipes?.Count ?? 0); i++)
			{
				CookingRecipe recipe = Recipes[i];
				if (recipe?.GetType().Name != FavoredDishType) continue;
				if (!BaseRecipeGraph(recipe))
					return Fail("campfire realm dish contains an unknown custom component",
						out Failure);
				Rows.Add(Kind + "\u001f" + i + "\u001f" + (recipe.DisplayName ?? "")
					+ "\u001f" + (recipe.ChefName ?? ""));
			}
			return true;
		}

		private static string WithoutRecipeToken(string Encoded)
		{
			if (string.IsNullOrEmpty(Encoded)) return Encoded;
			string[] tokens = Encoded.Split(',');
			List<string> kept = new List<string>();
			for (int i = 0; i < tokens.Length; i++)
			{
				string token = tokens[i].Trim();
				if (token.Length > 0 && token != FavoredDishType) kept.Add(token);
			}
			return string.Join(",", kept.ToArray());
		}

		private static void ConvertRecipeList(List<CookingRecipe> Recipes)
		{
			if (Recipes == null) return;
			for (int i = 0; i < Recipes.Count; i++)
				if (Recipes[i]?.GetType().Name == FavoredDishType
					&& BaseRecipeGraph(Recipes[i])) Recipes[i] = ToBaseRecipe(Recipes[i]);
		}

		private static CookingRecipe ToBaseRecipe(CookingRecipe Source)
		{
			return new CookingRecipe
			{
				Hidden = Source.Hidden,
				Favorite = Source.Favorite,
				DisplayName = Source.DisplayName,
				ChefName = Source.ChefName,
				Components = new List<ICookingRecipeComponent>(Source.Components),
				Effects = new List<ICookingRecipeResult>(Source.Effects),
				Tile = Source.Tile == null ? null : new Renderable(Source.Tile)
			};
		}

		private static bool BaseRecipeGraph(CookingRecipe Recipe)
		{
			if (Recipe?.Components == null || Recipe.Effects == null) return false;
			for (int i = 0; i < Recipe.Components.Count; i++)
				if (LooksCustom(Recipe.Components[i]?.GetType())) return false;
			for (int i = 0; i < Recipe.Effects.Count; i++)
				if (LooksCustom(Recipe.Effects[i]?.GetType())) return false;
			return true;
		}

		private static bool LooksCustom(Type Type)
		{
			return Type != null && (Type.Name.StartsWith("r_Kingdom", StringComparison.Ordinal)
				|| Type.Namespace == "ThousandAndFirst");
		}
	}
}
