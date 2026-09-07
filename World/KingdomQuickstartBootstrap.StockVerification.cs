using System;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomQuickstartBootstrap
	{
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
			if (!KingdomQuickstartStockRules.HasDistinctChildren(Larder.Inventory.Objects))
			{
				Failure = "The starter larder contained a missing or repeated physical meal.";
				return false;
			}
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
			if (!KingdomQuickstartStockRules.HasDistinctChildren(Stockpile.Inventory.Objects))
			{
				Failure = "The starter materials chest contained a missing or repeated physical stack.";
				return false;
			}
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
