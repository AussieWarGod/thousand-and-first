using System;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomQuickstartBootstrap
	{
		private static GameObject CreateWater(Zone Zone, KingdomQuickstartReceipt Receipt,
			out string Failure)
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
			GameObject water = GameObject.Create("r_KingdomCaskRack");
			if (!GameObject.Validate(water)) return null;
			water.SetIntProperty("KingdomStores", 1);
			LiquidVolume volume = water.GetPart<LiquidVolume>();
			if (!TryPrepareGrant(water, Receipt, KingdomQuickstartPhase.WaterStocked,
				out Failure) || volume == null || KingdomLiquids.Fill(volume, "water",
				KingdomQuickstartRules.StarterWaterDrams)
				!= KingdomQuickstartRules.StarterWaterDrams
				|| volume.Volume != KingdomQuickstartRules.StarterWaterDrams
				|| !KingdomLiquids.HasFreshWater(volume)
				|| !TryPlaceGrant(Zone, water, KingdomQuickstartRules.WaterCellX,
					KingdomQuickstartRules.WaterCellY, out Failure)
				|| !VerifyWaterGrant(Zone, water, Receipt, true, out Failure)) return null;
			return water;
		}

		private static bool VerifyWaterGrant(Zone Zone, GameObject Water,
			KingdomQuickstartReceipt Receipt, bool InitialQuantity, out string Failure)
		{
			Failure = "";
			LiquidVolume volume = Water?.GetPart<LiquidVolume>();
			if (!ExactRole(Zone, Water, "r_KingdomCaskRack",
				KingdomQuickstartRules.WaterCellX, KingdomQuickstartRules.WaterCellY)
				|| !ExactGrantMarker(Water, Receipt, KingdomQuickstartPhase.WaterStocked)
				|| (Receipt.Phase >= KingdomQuickstartPhase.WaterStocked
					&& !ReceiptOwns(Water, Receipt.WaterObjectId))
				|| Water.GetIntProperty("KingdomStores") != 1
				|| Water.GetIntProperty("KingdomBuilt") != 0
				|| Water.HasPart("LiquidProducer") || volume == null || volume.MaxVolume != 64
				|| (InitialQuantity && (volume.Volume != KingdomQuickstartRules.StarterWaterDrams
					|| !KingdomLiquids.HasFreshWater(volume))))
			{
				Failure = InitialQuantity
					? "The starter water was not exactly 24 physical drams in its dedicated casks."
					: "The receipted starter casks lost their durable identity or became a producer.";
				return false;
			}
			return true;
		}

		private static GameObject CreateLarder(Zone Zone, KingdomQuickstartReceipt Receipt,
			out string Failure)
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
			GameObject larder = GameObject.Create("r_KingdomLarder");
			if (!GameObject.Validate(larder) || larder.Inventory == null
				|| larder.Inventory.Objects.Count != 0) return larder;
			larder.SetIntProperty("KingdomLarder", 1);
			if (!TryPrepareGrant(larder, Receipt, KingdomQuickstartPhase.FoodStocked,
				out Failure)) return null;
			for (int i = 0; i < KingdomQuickstartRules.StarterFoodServings; i++)
			{
				GameObject food = GameObject.Create(Receipt.FoodBlueprint);
				if (!KingdomOrdinaryFoodAuthority.IsEdible(food) || food.Count != 1
					|| !ReferenceEquals(larder.Inventory.AddObject(food, null, Silent: true,
						NoStack: true), food))
				{
					Failure = "A private starter meal did not enter the prepared larder exactly.";
					ObliterateExact(larder);
					return null;
				}
			}
			if (!TryPlaceGrant(Zone, larder, KingdomQuickstartRules.LarderCellX,
				KingdomQuickstartRules.LarderCellY, out Failure)
				|| !VerifyLarderGrant(Zone, larder, Receipt, true, out Failure)) return null;
			return larder;
		}

		private static bool VerifyLarderGrant(Zone Zone, GameObject Larder,
			KingdomQuickstartReceipt Receipt, bool InitialQuantity, out string Failure)
		{
			Failure = "";
			int servings = 0;
			if (!ExactRole(Zone, Larder, "r_KingdomLarder",
				KingdomQuickstartRules.LarderCellX, KingdomQuickstartRules.LarderCellY)
				|| !ExactGrantMarker(Larder, Receipt, KingdomQuickstartPhase.FoodStocked)
				|| (Receipt.Phase >= KingdomQuickstartPhase.FoodStocked
					&& !ReceiptOwns(Larder, Receipt.LarderObjectId))
				|| Larder.GetIntProperty("KingdomLarder") != 1
				|| Larder.GetIntProperty("KingdomBuilt") != 0 || Larder.Inventory == null)
			{
				Failure = "The starter larder was absent or was not a physical dedicated container.";
				return false;
			}
			if (!InitialQuantity) return true;
			for (int i = 0; i < Larder.Inventory.Objects.Count; i++)
			{
				GameObject food = Larder.Inventory.Objects[i];
				if (!GameObject.Validate(food) || food.InInventory != Larder
					|| !string.Equals(food.Blueprint, Receipt.FoodBlueprint,
						StringComparison.Ordinal)
					|| !KingdomOrdinaryFoodAuthority.IsEdible(food) || food.Count < 1)
				{
					Failure = "The starter larder contained something other than its frozen ordinary food.";
					return false;
				}
				servings += food.Count;
			}
			if (servings != KingdomQuickstartRules.StarterFoodServings)
			{
				Failure = "The starter larder did not contain exactly 12 physical meals.";
				return false;
			}
			return true;
		}

		private static GameObject CreateMaterials(Zone Zone,
			KingdomQuickstartReceipt Receipt, out string Failure)
		{
			Failure = "";
			if (!TryObserveGrant(Zone, Receipt, KingdomQuickstartPhase.MaterialsStocked,
				KingdomQuickstartRules.StockpileCellX, KingdomQuickstartRules.StockpileCellY,
				true, out GameObject existing,
				out KingdomQuickstartGrantObservation observation, out Failure)) return null;
			KingdomQuickstartRecoveryAction action = KingdomQuickstartRules.RecoveryAction(
				Receipt.Phase, KingdomQuickstartPhase.MaterialsStocked, observation);
			if (action == KingdomQuickstartRecoveryAction.PublishExisting)
				return VerifyMaterialsGrant(Zone, existing, Receipt, true, out Failure)
					? existing : null;
			if (action != KingdomQuickstartRecoveryAction.PreparePlaceAndPublish)
			{
				Failure = "The starter-material recovery boundary was not lawful.";
				return null;
			}
			GameObject stockpile = GameObject.Create("Chest");
			if (!GameObject.Validate(stockpile) || stockpile.Inventory == null
				|| stockpile.Inventory.Objects.Count != 0) return stockpile;
			stockpile.SetIntProperty(KingdomMaterials.StockpileProperty, 1);
			if (stockpile.Physics != null) stockpile.Physics.Takeable = false;
			if (stockpile.Render != null) stockpile.Render.DisplayName = "camp materials chest";
			Description description = stockpile.GetPart<Description>();
			if (description != null) description.Short = "Mud, brush, and cut lengths of timber, "
				+ "each piece present because somebody carried it here.";
			if (!TryPrepareGrant(stockpile, Receipt,
				KingdomQuickstartPhase.MaterialsStocked, out Failure)
				|| !TryPrepareMaterial(stockpile, KingdomMaterial.Mud,
					KingdomQuickstartRules.StarterMud, out Failure)
				|| !TryPrepareMaterial(stockpile, KingdomMaterial.Brush,
					KingdomQuickstartRules.StarterBrush, out Failure)
				|| !TryPrepareMaterial(stockpile, KingdomMaterial.Timber,
					KingdomQuickstartRules.StarterTimber, out Failure))
			{
				ObliterateExact(stockpile);
				return null;
			}
			if (!TryPlaceGrant(Zone, stockpile, KingdomQuickstartRules.StockpileCellX,
				KingdomQuickstartRules.StockpileCellY, out Failure)
				|| !VerifyMaterialsGrant(Zone, stockpile, Receipt, true, out Failure))
				return null;
			return stockpile;
		}

		private static bool TryPrepareMaterial(GameObject Stockpile,
			KingdomMaterial Material, int Count, out string Failure)
		{
			Failure = "";
			string blueprint = KingdomMaterials.BlueprintFor(Material);
			GameObject item = string.IsNullOrEmpty(blueprint)
				? null : GameObject.Create(blueprint);
			if (!GameObject.Validate(item) || item.Count != 1 || Count < 1)
			{
				Failure = "A starter material blueprint did not create one ordinary item.";
				return false;
			}
			item.Count = Count;
			if (!KingdomMaterials.TryOrdinaryMaterialOf(item, out KingdomMaterial measured)
				|| measured != Material || !ReferenceEquals(Stockpile.Inventory.AddObject(
					item, null, Silent: true, NoStack: true), item))
			{
				Failure = "A private starter material did not enter its prepared chest exactly.";
				return false;
			}
			return true;
		}

		private static bool VerifyMaterialsGrant(Zone Zone, GameObject Stockpile,
			KingdomQuickstartReceipt Receipt, bool InitialQuantity, out string Failure)
		{
			Failure = "";
			if (!ExactRole(Zone, Stockpile, "Chest", KingdomQuickstartRules.StockpileCellX,
				KingdomQuickstartRules.StockpileCellY)
				|| !ExactGrantMarker(Stockpile, Receipt,
					KingdomQuickstartPhase.MaterialsStocked)
				|| (Receipt.Phase >= KingdomQuickstartPhase.MaterialsStocked
					&& !ReceiptOwns(Stockpile, Receipt.StockpileObjectId))
				|| Stockpile.GetIntProperty(KingdomMaterials.StockpileProperty) != 1
				|| Stockpile.GetIntProperty("KingdomBuilt") != 0 || Stockpile.Inventory == null)
			{
				Failure = "The starter materials chest was absent or was not a physical stockpile.";
				return false;
			}
			if (!InitialQuantity) return true;
			int mud = 0, brush = 0, timber = 0;
			for (int i = 0; i < Stockpile.Inventory.Objects.Count; i++)
			{
				GameObject item = Stockpile.Inventory.Objects[i];
				KingdomMaterial kind;
				if (!GameObject.Validate(item) || item.InInventory != Stockpile || item.Count < 1
					|| !KingdomMaterials.TryOrdinaryMaterialOf(item, out kind))
				{
					Failure = "The starter materials chest contained an unreceipted thing.";
					return false;
				}
				if (kind == KingdomMaterial.Mud) mud += item.Count;
				else if (kind == KingdomMaterial.Brush) brush += item.Count;
				else if (kind == KingdomMaterial.Timber) timber += item.Count;
				else
				{
					Failure = "The starter materials chest contained a material outside its modest grant.";
					return false;
				}
			}
			if (mud != KingdomQuickstartRules.StarterMud
				|| brush != KingdomQuickstartRules.StarterBrush
				|| timber != KingdomQuickstartRules.StarterTimber)
			{
				Failure = "The starter chest did not contain exactly 1 mud, 3 brush, and 4 timber.";
				return false;
			}
			return true;
		}
	}
}
