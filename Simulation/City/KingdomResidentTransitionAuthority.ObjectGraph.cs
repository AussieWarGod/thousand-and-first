using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomResidentTransitionAuthority
	{
		private const int MaximumDestructiveObjectGraph = 65536;

		private static bool TryProjectObjectGraphClaims(GameObject Body,
			ref KingdomResidentTransitionClaim Claims)
		{
			if (!GameObject.Validate(Body)) return false;
			bool merchant = Body.IsMerchant();
			List<GameObject> pending;
			try { pending = Body.GetInventoryDirectAndEquipment(); }
			catch { return false; }
			if (pending == null) pending = new List<GameObject>();
			HashSet<GameObject> seen = new HashSet<GameObject> { Body };
			for (int at = 0; at < pending.Count; at++)
			{
				GameObject item = pending[at];
				if (!GameObject.Validate(item) || !seen.Add(item)
					|| seen.Count > MaximumDestructiveObjectGraph) return false;
				if (KingdomMarketStockProtection.HasProjection(item)
					|| item.GetPart<r_KingdomLegendaryMarketProjection>() != null
					|| item.GetPart<r_KingdomMarketHandoffSourceProjection>() != null)
					Claims |= KingdomResidentTransitionClaim.MarketStock;
				if (item.HasStringProperty(KingdomGuestbook.MarketTransferTargetProperty)
					|| item.HasStringProperty(
						KingdomShopStockRules.StockTransferTargetProperty))
					Claims |= KingdomResidentTransitionClaim.MarketTransfer;
				if (merchant && item.GetIntProperty("_stock") == 1)
					Claims |= KingdomResidentTransitionClaim.NativeMerchantStock;
				List<GameObject> children;
				try { children = item.GetInventoryDirectAndEquipment(); }
				catch { return false; }
				if (children == null) continue;
				if (pending.Count > MaximumDestructiveObjectGraph - children.Count)
					return false;
				for (int i = 0; i < children.Count; i++) pending.Add(children[i]);
			}
			return true;
		}
	}
}
