using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// Optional shared meal transaction. It requires physical ingredients and an operational
	/// cooking provider, discloses its exact cost, then debits real dedicated larders once.
	/// </summary>
	public static class KingdomLarder
	{
		/// <summary>
		/// Calls a shared meal from the dedicated larders on the given ground: proves a kitchen,
		/// spends ingredients,
		/// writes the chronicle, records the deed, and lets a named settler speak. Declines
		/// cleanly, with no state changed, when there is no one to feed or nothing to feed
		/// them with.
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">Zone to draw from; must be the kingdom's own claimed ground.</param>
		/// <param name="Failure">Set to a player-facing reason when this returns false.</param>
		/// <returns>True once the meal is held, spent, and recorded.</returns>
		public static bool HoldSharedMeal(KingdomSystem System, Zone Z, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = "A meal is shared on the kingdom's own ground.";
				return false;
			}
			if (System.Population <= 0)
			{
				Failure = "There is no one here yet to share a meal with.";
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.Take(Z, System);
			int kitchens = KingdomCapabilityRuntime.Count(Z, survey,
				KingdomBenefitCapabilities.Cooking, "shared meal");
			if (kitchens <= 0)
			{
				Failure = "No capable kitchen stands on this designated ground. Place or commission a cooking provider in the correct building, and staff it where the provider requires staff.";
				return false;
			}
			int available;
			string authorityFailure;
			if (!KingdomOrdinaryFoodAuthority.TryAvailable(survey, out available,
				out authorityFailure))
			{
				Failure = authorityFailure ?? "The pantry's custody could not be proved; nothing was spent.";
				return false;
			}
			if (!KingdomRules.CanHoldSharedMeal(available, System.Population, kitchens))
			{
				Failure = "The pantry has no meal ingredients. Put food in a dedicated vessel or larder; no meal was called and nothing was spent.";
				return false;
			}
			// Read the tier before spending: ConsumeFood re-classifies FoodStored downward as it
			// draws, and the meal must be named for what it was when it was called, not for
			// whatever the pantry reads afterward.
			KingdomRules.PantryTier tier = KingdomRules.ClassifyPantry(available);
			int cost = KingdomRules.MealServingsSpent(available);
			int fromDish;
			int spent = survey.ConsumeFood(cost, System.DishStaple, out fromDish);
			System.Ledger.MealIngredientsSpent = KingdomCatalogueRules.SaturatingCounterAdd(
				System.Ledger.MealIngredientsSpent, spent);
			if (spent != cost)
			{
				Failure = (spent <= 0)
					? "The larders changed before the meal could be set. Nothing was spent or shared."
					: ("The meal debit was interrupted after " + spent + " of " + cost
						+ " ingredients. The exact loss is in the ledger; no meal benefit was granted.");
				return false;
			}
			KingdomRules.MealVerdict meal = KingdomRules.JudgeMeal(
				cost, fromDish, spent, true, System.Stage);
			System.LastMeal = meal;
			System.MealShade = 0;
			System.ScrapsAnnounced = false;
			if (meal == KingdomRules.MealVerdict.Favored)
			{
				string favorite = KingdomRules.FavoredMealNote(
					KingdomPresentation.Rich(System.KingdomDisplayName), System.DishName);
				if (favorite != null) System.Ledger.Note(favorite);
			}
			KingdomGovernanceScope.Commit("share meal");
			string sizeName = KingdomRules.MealSizeName(tier);
			KingdomVoiceRules.Speaker speaker = KingdomVoices.Draw(System, VoiceOccasion.MealShared);
			string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
			KingdomChronicle.Record(System, sizeName + " was shared at " + realm + ", and " + speaker.Attribution + " spoke well of it");
			System.RecordDeed(sizeName + " shared at " + realm);
			MessageQueue.AddPlayerMessage(KingdomVoices.Say(speaker, VoiceOccasion.MealShared, "{{G|" + XRL.Language.Grammar.InitCap(sizeName) + " is shared, and the settlement eats together.}}", KingdomRules.MealSpeech(tier)));
			KingdomLog.Log("shared meal: tier=" + tier + " quoted=" + cost + " spent="
				+ spent + " staple=" + fromDish + " verdict=" + meal + " kitchens=" + kitchens
				+ " settler=" + speaker.Attribution);
			KingdomCreed.EaseForMeal(System);
			// Addendum 5's culture channel, riding the meal rather than being a lever of its own: the
			// founder already paid for the food, and this is the evening being worth more than its
			// calories to the people who sat down at it. Small, capped, and nobody converts on supper
			// alone.
			KingdomConversion.OnSharedMeal(System, Z);
			return true;
		}
	}
}
