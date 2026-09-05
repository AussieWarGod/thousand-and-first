using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomQuickstartBootstrap
	{
		private static GameObject CreateWater(XRLGame Game, Zone Zone,
			KingdomQuickstartReceipt Receipt, out string Failure)
		{
			Failure = "";
			if (!TryObserveGrant(Zone, Receipt, KingdomQuickstartPhase.WaterStocked,
				KingdomQuickstartRules.WaterCellX, KingdomQuickstartRules.WaterCellY, true,
				out GameObject existing, out KingdomQuickstartGrantObservation observation,
				out Failure)) return null;
			KingdomQuickstartRecoveryAction action = KingdomQuickstartRules.RecoveryAction(
				Receipt.Phase, KingdomQuickstartPhase.WaterStocked, observation);
			if (action == KingdomQuickstartRecoveryAction.PublishExisting)
				return VerifyWaterGrant(Zone, existing, Receipt, true, out Failure)
					? existing : null;
			if (action != KingdomQuickstartRecoveryAction.PreparePlaceAndPublish)
			{
				Failure = "The starter-water recovery boundary was not lawful.";
				return null;
			}
			string failure = "";
			bool created = TryCreateFreshGrant(Game, scope =>
			{
				GameObject water = scope.Create(() => GameObject.Create("r_KingdomCaskRack"));
				if (!GameObject.Validate(water)) return null;
				water.SetIntProperty("KingdomStores", 1);
				LiquidVolume volume = water.GetPart<LiquidVolume>();
				if (!TryPrepareGrant(water, Receipt, KingdomQuickstartPhase.WaterStocked,
					out failure) || volume == null || KingdomLiquids.Fill(volume, "water",
					KingdomQuickstartRules.StarterWaterDrams)
					!= KingdomQuickstartRules.StarterWaterDrams
					|| volume.Volume != KingdomQuickstartRules.StarterWaterDrams
					|| !KingdomLiquids.HasFreshWater(volume)
					|| !TryPlaceGrant(Zone, water, KingdomQuickstartRules.WaterCellX,
						KingdomQuickstartRules.WaterCellY, out failure)) return null;
				return water;
			}, water => VerifyWaterGrant(Zone, water, Receipt, true, out failure),
				out GameObject grant);
			Failure = created ? "" : FreshGrantFailure(failure);
			return grant;
		}

		private static GameObject CreateLarder(XRLGame Game, Zone Zone,
			KingdomQuickstartReceipt Receipt, out string Failure)
		{
			Failure = "";
			if (!TryObserveGrant(Zone, Receipt, KingdomQuickstartPhase.FoodStocked,
				KingdomQuickstartRules.LarderCellX, KingdomQuickstartRules.LarderCellY, true,
				out GameObject existing, out KingdomQuickstartGrantObservation observation,
				out Failure)) return null;
			KingdomQuickstartRecoveryAction action = KingdomQuickstartRules.RecoveryAction(
				Receipt.Phase, KingdomQuickstartPhase.FoodStocked, observation);
			if (action == KingdomQuickstartRecoveryAction.PublishExisting)
				return VerifyLarderGrant(Zone, existing, Receipt, true, out Failure)
					? existing : null;
			if (action != KingdomQuickstartRecoveryAction.PreparePlaceAndPublish)
			{
				Failure = "The starter-larder recovery boundary was not lawful.";
				return null;
			}
			string failure = "";
			bool created = TryCreateFreshGrant(Game, scope =>
			{
				GameObject larder = scope.Create(() => GameObject.Create("r_KingdomLarder"));
				if (!GameObject.Validate(larder) || larder.Inventory == null
					|| larder.Inventory.Objects.Count != 0) return null;
				larder.SetIntProperty("KingdomLarder", 1);
				if (!TryPrepareGrant(larder, Receipt, KingdomQuickstartPhase.FoodStocked,
					out failure)) return null;
				for (int i = 0; i < KingdomQuickstartRules.StarterFoodServings; i++)
				{
					GameObject food = scope.Create(() => GameObject.Create(Receipt.FoodBlueprint));
					if (!KingdomOrdinaryFoodAuthority.IsEdible(food) || food.Count != 1
						|| !ReferenceEquals(larder.Inventory.AddObject(food, null, Silent: true,
							NoStack: true), food))
					{
						failure = "A private starter meal did not enter the prepared larder exactly.";
						return null;
					}
				}
				if (!TryPlaceGrant(Zone, larder, KingdomQuickstartRules.LarderCellX,
					KingdomQuickstartRules.LarderCellY, out failure)) return null;
				return larder;
			}, larder => VerifyLarderGrant(Zone, larder, Receipt, true, out failure),
				out GameObject grant);
			Failure = created ? "" : FreshGrantFailure(failure);
			return grant;
		}

		private static string FreshGrantFailure(string Failure)
		{
			return string.IsNullOrEmpty(Failure)
				? "A fresh quickstart grant failed preparation or exact placement." : Failure;
		}
	}
}
