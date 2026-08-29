using System;
using ConsoleLib.Console;
using XRL.World.Effects;
using XRL.World.Parts;
using XRL.World.Skills.Cooking;

namespace ThousandAndFirst
{
	public static partial class KingdomNamedCook
	{
		/// <summary>Builds the exact native, directly serializable recipe graph. No procedural
		/// generator, recipe subclass, journal injection, or post-uninstall dependency participates.</summary>
		internal static CookingRecipe BuildRecipe(KingdomNamedCookReceipt Receipt)
		{
			string failure;
			if (!KingdomNamedCookRules.Validate(Receipt, out failure)
				|| Receipt.Phase == KingdomNamedCookPhase.None
				|| Receipt.Phase == KingdomNamedCookPhase.Quarantined) return null;
			Guid effectId;
			if (!Guid.TryParseExact(Receipt.EffectId, "D", out effectId)) return null;

			ProceduralCookingEffect effect = new ProceduralCookingEffect();
			effect.ID = effectId;
			effect.AddUnit(new CookingDomainTaste_UnitDoNothing());
			CookingRecipe recipe = new CookingRecipe
			{
				Hidden = false,
				Favorite = false,
				DisplayName = Receipt.RecipeDisplayName,
				ChefName = Receipt.ResidentName,
				Tile = new Renderable
				{
					Tile = KingdomNamedCookRules.RecipeTile,
					RenderString = " ",
					ColorString = KingdomNamedCookRules.RecipeColor,
					TileColor = null,
					DetailColor = KingdomNamedCookRules.RecipeDetail
				}
			};
			recipe.Components.Add(new PreparedCookingRecipieComponentBlueprint(
				KingdomNamedCookRules.IngredientBlueprint,
				KingdomNamedCookRules.IngredientDisplayName,
				KingdomNamedCookRules.IngredientAmount));
			recipe.Effects.Add(new CookingRecipeResultProceduralEffect(effect));
			return recipe;
		}

		internal static bool ExactRecipe(CookingRecipe Recipe,
			KingdomNamedCookReceipt Receipt)
		{
			if (Recipe == null || Receipt == null || Recipe.GetType() != typeof(CookingRecipe)
				|| !string.Equals(Recipe.DisplayName, Receipt.RecipeDisplayName,
					StringComparison.Ordinal)
				|| !string.Equals(Recipe.ChefName, Receipt.ResidentName,
					StringComparison.Ordinal)
				|| Recipe.Components == null || Recipe.Components.Count != 1
				|| Recipe.Effects == null || Recipe.Effects.Count != 1
				|| Recipe.Tile == null
				|| !string.Equals(Recipe.Tile.Tile, KingdomNamedCookRules.RecipeTile,
					StringComparison.Ordinal)
				|| !string.Equals(Recipe.Tile.RenderString, " ", StringComparison.Ordinal)
				|| !string.Equals(Recipe.Tile.ColorString, KingdomNamedCookRules.RecipeColor,
					StringComparison.Ordinal)
				|| Recipe.Tile.TileColor != null
				|| Recipe.Tile.DetailColor != KingdomNamedCookRules.RecipeDetail) return false;

			PreparedCookingRecipieComponentBlueprint ingredient = Recipe.Components[0]
				as PreparedCookingRecipieComponentBlueprint;
			CookingRecipeResultProceduralEffect result = Recipe.Effects[0]
				as CookingRecipeResultProceduralEffect;
			Guid expected;
			return ingredient != null
				&& ingredient.GetType() == typeof(PreparedCookingRecipieComponentBlueprint)
				&& ingredient.ingredientBlueprint == KingdomNamedCookRules.IngredientBlueprint
				&& ingredient.ingredientDisplayName == KingdomNamedCookRules.IngredientDisplayName
				&& ingredient.amount == KingdomNamedCookRules.IngredientAmount
				&& result != null
				&& result.GetType() == typeof(CookingRecipeResultProceduralEffect)
				&& result.Effect != null
				&& result.Effect.GetType() == typeof(ProceduralCookingEffect)
				&& Guid.TryParseExact(Receipt.EffectId, "D", out expected)
				&& result.Effect.ID == expected
				&& result.Effect.units != null && result.Effect.units.Count == 1
				&& result.Effect.units[0] != null
				&& result.Effect.units[0].GetType() == typeof(CookingDomainTaste_UnitDoNothing);
		}

		internal static bool ExactTeaching(TeachesDish Teaching,
			KingdomNamedCookReceipt Receipt)
		{
			return Teaching != null && ExactRecipe(Teaching.Recipe, Receipt)
				&& string.Equals(Teaching.Text, KingdomNamedCookRules.TeachingText(Receipt),
					StringComparison.Ordinal);
		}
	}
}
