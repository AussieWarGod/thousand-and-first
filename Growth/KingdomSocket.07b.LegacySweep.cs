using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomSocket
	{
		/// <summary>Compatibility-only clearance for a pre-receipt conversion whose predecessor
		/// still stands. Current authored owners and any stateful/protected object refuse.</summary>
		private static bool TrySweepLegacyPlotParts(Zone Z,
			KingdomPlotRules.PlotRect Rect, string PlotId, GameObject Owner)
		{
			if (Z == null || !GameObject.Validate(Owner)
				|| Owner.HasIntProperty(KingdomArchitectureRuntime.SchemaProperty)
				|| Owner.HasStringProperty(KingdomArchitectureRuntime.SchemaProperty)) return false;
			List<GameObject> targets = new List<GameObject>();
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
			{
				for (int x = Rect.X1; x <= Rect.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null) continue;
					List<GameObject> standing = new List<GameObject>(cell.GetObjects());
					for (int i = 0; i < standing.Count; i++)
					{
						GameObject item = standing[i];
						if (!GameObject.Validate(item)
							|| item.GetIntProperty(KingdomPlots.PlotPartProperty) != 1) continue;
						if (!string.IsNullOrEmpty(PlotId)
							&& item.GetStringProperty(KingdomPlots.PlotIdProperty) != PlotId) continue;
						if (item.Inventory != null && item.Inventory.Objects.Count != 0) return false;
						LiquidVolume liquid = item.GetPart<LiquidVolume>();
						if (liquid != null && liquid.Volume > 0) return false;
						if (item.GetIntProperty("KingdomCitizen") == 1
							|| item.GetIntProperty("KingdomStores") == 1
							|| item.GetIntProperty("KingdomLarder") == 1
							|| item.GetIntProperty(KingdomMaterials.StockpileProperty) == 1)
							return false;
						targets.Add(item);
					}
				}
			}
			for (int i = 0; i < targets.Count; i++)
			{
				GameObject target = targets[i];
				if (!GameObject.Validate(target)) return false;
				bool removed = target.Obliterate(null, Silent: true);
				if (removed || !GameObject.Validate(target))
					KingdomSurvey.ObserveRemovedFromActive(Z, target);
				if (!removed || GameObject.Validate(target)) return false;
			}
			return true;
		}
	}
}
