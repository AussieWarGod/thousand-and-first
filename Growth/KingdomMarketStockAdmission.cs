using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomMarketStockCustody
	{
		internal static bool HasNativeStock(GameObject Body)
		{
			if (Body?.Inventory == null) return false;
			for (int i = 0; i < Body.Inventory.Objects.Count; i++)
				if (NativeStock(Body, Body.Inventory.Objects[i])) return true;
			return false;
		}

		internal static bool TryAdmitLegacyHandoff(KingdomSystem System, string SettlementId,
			GameObject Body, GameObject Item, out string Failure)
		{
			Failure = null;
			return !KingdomMarketStockProtection.HasProjection(Item)
				&& TryBind(System, SettlementId, Body, Item, false, out Failure);
		}

		internal static bool HasExactLocalCustody(KingdomSystem System, string SettlementId,
			GameObject Body)
		{
			if (Body?.Inventory == null) return false;
			for (int i = 0; i < Body.Inventory.Objects.Count; i++)
				if (Exact(System, SettlementId, Body, Body.Inventory.Objects[i])) return true;
			return false;
		}

		/// <summary>Read-only twin for admission and ordinary office cleanup. One torn row blocks
		/// the whole mutation, so an earlier row is never stamped before a later fault is known.</summary>
		internal static bool CanAdmitHeld(KingdomSystem System, string SettlementId,
			GameObject Body, out string Failure)
		{
			Failure = null;
			if (System == null || Body?.Inventory == null || string.IsNullOrEmpty(SettlementId))
				{ Failure = "market stock has no exact local custodian"; return false; }
			int rows = 0;
			for (int i = 0; i < Body.Inventory.Objects.Count; i++)
			{
				GameObject item = Body.Inventory.Objects[i];
				if (!NativeStock(Body, item)) continue;
				if (++rows > KingdomShopStockRules.MaximumCustodyRows)
					{ Failure = "market stock exceeds the bounded custody roster"; return false; }
				if (Exact(System, SettlementId, Body, item))
				{
					if (!KingdomMarketStockProtection.CanProtect(item, out Failure)) return false;
					continue;
				}
				if (PendingTo(System, SettlementId, Body, item)) continue;
				if (KingdomMarketStockProtection.HasProjection(item))
				{
					Failure = "foreign, torn, or stale market custody blocks automatic admission";
					return false;
				}
				if (!KingdomMarketStockProtection.CanProtect(item, out Failure)) return false;
			}
			return true;
		}
	}
}
