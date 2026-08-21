using XRL.Messages;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The shared meal: the one action in the mod that spends what the dedicated larders
	/// hold. Nothing else consumes food &mdash; an empty larder costs the founder nothing,
	/// per the mod's own rule that engaging is rewarded and abstaining is never punished, and
	/// this is the only path by which a full one ever costs anything, and only because the
	/// founder called for it.
	/// </summary>
	public static class KingdomLarder
	{
		/// <summary>
		/// Calls a shared meal from the dedicated larders on the given ground: spends food,
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
			KingdomSurvey survey = KingdomSurvey.Take(Z);
			if (!KingdomRules.CanHoldSharedMeal(survey.FoodStored, System.Population))
			{
				Failure = "The larders hold nothing to share. Put food in a dedicated vessel or larder, or commission a civic larder, and the settlement can sit down together.";
				return false;
			}
			// Read the tier before spending: ConsumeFood re-classifies FoodStored downward as it
			// draws, and the meal must be named for what it was when it was called, not for
			// whatever the pantry reads afterward.
			KingdomRules.PantryTier tier = survey.FoodAbundance;
			int spent = survey.ConsumeFood(KingdomRules.MealServingsSpent(survey.FoodStored));
			string sizeName = KingdomRules.MealSizeName(tier);
			KingdomVoiceRules.Speaker speaker = KingdomVoices.Draw(System, VoiceOccasion.MealShared);
			KingdomChronicle.Record(System, sizeName + " was shared at " + System.KingdomDisplayName + ", and " + speaker.Attribution + " spoke well of it");
			System.RecordDeed(sizeName + " shared at " + System.KingdomDisplayName);
			MessageQueue.AddPlayerMessage(KingdomVoices.Say(speaker, VoiceOccasion.MealShared, "{{G|" + XRL.Language.Grammar.InitCap(sizeName) + " is shared, and the settlement eats together.}}", KingdomRules.MealSpeech(tier)));
			KingdomLog.Log("shared meal: tier=" + tier + " spent=" + spent + " settler=" + speaker.Attribution);
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
