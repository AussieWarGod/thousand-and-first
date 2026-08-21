using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Effects;
using XRL.World.Skills.Cooking;

using ThousandAndFirst;

// Vanilla reaches a recipe by TYPE NAME and only ever in one namespace:
//   Activator.CreateInstance(ModManager.ResolveType("XRL.World.Skills.Cooking." + text))
// is how Campfire resolves each entry of its PresetMeals (D/XRL/World/Parts/Campfire.cs:203-214)
// and how the water ritual resolves a faction's WaterRitualRecipe
// (D/XRL/World/Conversations/Parts/WaterRitualCookingRecipe.cs:29-31,53). So the settlement's own
// dish MUST live in that namespace, exactly as r_KingdomPlot must live in XRL.World.Parts. Only
// the class moves; the settlement-side stamping below stays where the rest of the mod's code is.
namespace XRL.World.Skills.Cooking
{
	/// <summary>
	/// The settlement's favourite dish, as a real vanilla cooking recipe.
	/// <para>
	/// <b>One class, every realm.</b> Vanilla ships eleven hand-written <c>CookingRecipe</c>
	/// subclasses and identifies a recipe by its DISPLAY NAME, not its type
	/// (<c>CookingGameState.KnowsRecipe</c> matches on <c>GetDisplayName()</c>,
	/// <c>D/…/CookingGameState.cs:69</c>) &mdash; and <c>GetDisplayName</c> falls back to the
	/// <c>DisplayName</c> field before the type name (<c>D/…/CookingRecipe.cs:86-100</c>). So a
	/// single class whose instance reads the realm's own stamped dish gives every settlement a
	/// distinct, learnable, journal-able recipe without a class per settlement.
	/// </para>
	/// <para>
	/// Instances are only ever made by those two vanilla call sites, both at runtime with a game
	/// in play. Everything below is nevertheless guarded: a recipe built before the realm has a
	/// dish is a plain settlement supper rather than an exception in a conversation.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomFavoredDish : CookingRecipe
	{
		/// <summary>What the dish is called when the realm has not named one yet. Never seen in
		/// ordinary play; it exists so the two vanilla resolvers can never hand a menu a null.</summary>
		public const string UnnamedDish = "settlement stew";

		public r_KingdomFavoredDish()
		{
			// GetSystem and not RequireSystem: this constructor runs from vanilla call sites the
			// mod does not control - a conversation opening, a cooking menu building, and a
			// recipe being read back off a save - and a recipe should never be the thing that
			// MINTS a kingdom system. A realm that does not exist has no dish, and that is a
			// name, not an error.
			ThousandAndFirst.KingdomSystem system = The.Game?.GetSystem<ThousandAndFirst.KingdomSystem>();
			DisplayName = (system != null && !string.IsNullOrEmpty(system.DishName)) ? system.DishName : UnnamedDish;
			// The components are what the settlement actually eats: the preserved staple its mill
			// binds the harvest into, and the harvest itself. Listing the staple FIRST is not
			// cosmetic - it is the same order the ration draw reaches in
			// (KingdomSurvey.ConsumeFood), so the recipe a founder reads and the meal the
			// settlement eats name the same thing in the same order.
			AddComponent(system?.DishStaple);
			AddComponent(ThousandAndFirst.KingdomDish.CropOf(system));
			// Regeneration and half thirst: vanilla's own Apple Matz pair
			// (D/XRL/World/Skills/Cooking/AppleMatz.cs), and the right two for a settlement's
			// table in a desert - it feeds you and it stretches your water. CreateSpecific
			// swallows a bad unit name into a log line rather than throwing (:340-352), so this
			// cannot break a conversation even if a later build renames one.
			Effects.Add(new CookingRecipeResultProceduralEffect(ProceduralCookingEffect.CreateSpecific(new List<string>
			{
				"CookingDomainRegenLowtier_RegenerationUnit",
				"ProceduralCookingEffectUnit_LessThirst"
			})));
		}

		/// <summary>Adds one blueprint component, if it is one this build can actually make.
		/// PreparedCookingRecipieComponentBlueprint's constructor builds a sample object to read
		/// its display name from (<c>D/…/PreparedCookingRecipieComponentBlueprint.cs:26-30</c>),
		/// so a blueprint that does not resolve would throw inside a conversation.</summary>
		private void AddComponent(string Blueprint)
		{
			if (string.IsNullOrEmpty(Blueprint) || !GameObjectFactory.Factory.HasBlueprint(Blueprint))
			{
				return;
			}
			Components.Add(new PreparedCookingRecipieComponentBlueprint(Blueprint));
		}

		public override string GetDescription()
		{
			return "+10-15% to natural healing rate\nYou thirst at half rate.";
		}

		public override string GetApplyMessage()
		{
			return "";
		}
	}
}

namespace ThousandAndFirst
{
	/// <summary>
	/// The realm's favourite dish: derived from who lives here and what the ground grows, and
	/// stamped where vanilla already looks for it.
	/// <para>
	/// <b>Why the faction is the right home.</b> <c>Faction.WaterRitualRecipe</c>,
	/// <c>WaterRitualRecipeText</c> and <c>WaterRitualRecipeGenotype</c> are plain fields on
	/// <c>Faction</c> (<c>D/XRL/World/Faction.cs:72-76</c>) written and read by the faction's own
	/// serializer (<c>:286-288</c>, <c>:362</c>) &mdash; so setting them on the RUNTIME faction
	/// <c>KingdomFounding</c> mints is not a hack around vanilla's persistence, it IS vanilla's
	/// persistence. Eight shipped factions declare one in <c>B/Factions.xml</c>; the realm makes
	/// nine. The mod keeps its own copy on <c>KingdomSystem</c> only so a pass can notice a change
	/// of heart, and so a save whose faction was stripped by another mod can be put right.
	/// </para>
	/// </summary>
	public static class KingdomDish
	{
		/// <summary>The crop the realm's dish is made of, or null when there is no realm yet.
		/// Kept here rather than on the system so the recipe class has one thing to ask.</summary>
		public static string CropOf(KingdomSystem System)
		{
			return (System == null) ? null : KingdomCropRules.CropBlueprintForStyle(System.Style);
		}

		/// <summary>
		/// Derives the realm's dish and writes it to the faction, the settlement, or both, if it
		/// is not already what it should be. Idempotent, and cheap enough to call on every pass:
		/// the derivation is a switch and two string joins, and nothing is written when nothing
		/// changed.
		/// </summary>
		/// <param name="System">The realm. Nothing happens for an unfounded one.</param>
		/// <param name="Announce">Whether a CHANGE of dish is worth telling the founder about.
		/// False at founding, where the chronicle already has a line about the day.</param>
		/// <returns>True when the faction now carries a dish.</returns>
		public static bool Ensure(KingdomSystem System, bool Announce = true)
		{
			if (System == null || !System.Founded)
			{
				return false;
			}
			Faction faction = Factions.GetIfExists(System.KingdomFactionName);
			if (faction == null)
			{
				return false;
			}
			KingdomRules.FavoredDish dish = KingdomRules.DeriveDish(
				System.KingdomDisplayName, CreedRecipeOf(System), CropOf(System));
			bool changed = !string.IsNullOrEmpty(System.DishName) && System.DishName != dish.Name;
			bool first = string.IsNullOrEmpty(System.DishName);
			System.DishName = dish.Name;
			System.DishText = dish.Text;
			System.DishStaple = dish.Staple;
			System.DishSource = dish.Source;
			// The three fields vanilla reads. RecipeGenotype is left alone: the two genotype
			// gates it drives both REFUSE the recipe to somebody (D/…/WaterRitualCookingRecipe.cs
			// :99-119), and a settlement the founder built has no business refusing them dinner.
			faction.WaterRitualRecipe = KingdomRules.DishRecipeType;
			faction.WaterRitualRecipeText = dish.Text;
			if (changed && Announce)
			{
				KingdomChronicle.Record(System, "the kitchens of " + System.KingdomDisplayName + " changed their minds, and " + dish.Name + " became what the settlement is known for");
				System.Ledger.Note("{{W|" + XRL.Language.Grammar.InitCap(dish.Name) + " is what " + System.KingdomDisplayName + " cooks now.}}");
			}
			if (KingdomLog.Enabled && (changed || first))
			{
				KingdomLog.Log("dish: " + dish.Name + " form=" + dish.Form + " staple=" + dish.Staple + " source=" + (string.IsNullOrEmpty(dish.Source) ? "(none)" : dish.Source));
			}
			return true;
		}

		/// <summary>
		/// The dish the realm's people already hold with: the declared creed's if the founder
		/// declared one, otherwise the seated city's own creed's, and null for a realm of mixed
		/// people. Read off the creed faction's own <c>WaterRitualRecipe</c>, so the borrowing is
		/// from vanilla's own table rather than from a list this mod keeps.
		/// </summary>
		public static string CreedRecipeOf(KingdomSystem System)
		{
			if (System == null)
			{
				return null;
			}
			string creed = !string.IsNullOrEmpty(System.DeclaredCreed)
				? System.DeclaredCreed
				: KingdomCreed.SeatCreed(System);
			if (string.IsNullOrEmpty(creed))
			{
				return null;
			}
			Faction faction = Factions.GetIfExists(creed);
			return (faction == null || string.IsNullOrEmpty(faction.WaterRitualRecipe)) ? null : faction.WaterRitualRecipe;
		}
	}
}
