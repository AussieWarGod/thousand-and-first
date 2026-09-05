using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomQuickstartBootstrap
	{
		private static GameObject CreateMaterials(XRLGame Game, Zone Zone,
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
			string failure = "";
			bool created = TryCreateFreshGrant(Game, scope =>
			{
				GameObject stockpile = scope.Create(() => GameObject.Create("Chest"));
				if (!GameObject.Validate(stockpile) || stockpile.Inventory == null
					|| stockpile.Inventory.Objects.Count != 0) return null;
				stockpile.SetIntProperty(KingdomMaterials.StockpileProperty, 1);
				if (stockpile.Physics != null) stockpile.Physics.Takeable = false;
				if (stockpile.Render != null) stockpile.Render.DisplayName = "camp materials chest";
				Description description = stockpile.GetPart<Description>();
				if (description != null) description.Short = "Mud, brush, and cut lengths of timber, "
					+ "each piece present because somebody carried it here.";
				if (!TryPrepareGrant(stockpile, Receipt,
					KingdomQuickstartPhase.MaterialsStocked, out failure)
					|| !TryPrepareMaterial(scope, stockpile, KingdomMaterial.Mud,
						KingdomQuickstartRules.StarterMud, out failure)
					|| !TryPrepareMaterial(scope, stockpile, KingdomMaterial.Brush,
						KingdomQuickstartRules.StarterBrush, out failure)
					|| !TryPrepareMaterial(scope, stockpile, KingdomMaterial.Timber,
						KingdomQuickstartRules.StarterTimber, out failure)
					|| !TryPlaceGrant(Zone, stockpile, KingdomQuickstartRules.StockpileCellX,
						KingdomQuickstartRules.StockpileCellY, out failure)) return null;
				return stockpile;
			}, stockpile => VerifyMaterialsGrant(Zone, stockpile, Receipt, true, out failure),
				out GameObject grant);
			Failure = created ? "" : FreshGrantFailure(failure);
			return grant;
		}

		private static bool TryPrepareMaterial(KingdomQuickstartGrantScope<GameObject> Scope,
			GameObject Stockpile, KingdomMaterial Material, int Count, out string Failure)
		{
			Failure = "";
			string blueprint = KingdomMaterials.BlueprintFor(Material);
			GameObject item = string.IsNullOrEmpty(blueprint)
				? null : Scope.Create(() => GameObject.Create(blueprint));
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
	}
}
