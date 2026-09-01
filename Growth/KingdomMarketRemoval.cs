using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	/// <summary>Exact realm-removal authority for finite-market projections. Physical objects,
	/// native <c>_stock</c>, stack counts, and foreign protections are never removed.</summary>
	internal static partial class KingdomMarketRemoval
	{
		internal static bool CanRetireStock(KingdomSystem System, GameObject Item,
			out bool Retires, out string Failure)
		{
			Retires = false; Failure = null;
			if (!GameObject.Validate(Item) || !KingdomMarketStockProtection.HasProjection(Item))
				return true;
			string current = Item.GetStringProperty(KingdomShopStockRules.StockRealmProperty);
			string legacy = Item.GetStringProperty(KingdomShopStockRules.LegacyStockRealmProperty);
			if (System == null || !KingdomShopStockRules.TryResolveStockRealm(current, legacy,
				out string realm))
			{
				Failure = "market stock has absent or divergent realm custody"; return false;
			}
			if (realm != System.RealmId)
			{
				Failure = "foreign market stock projection is preserved and blocks realm removal";
				return false;
			}
			string settlement = Item.GetStringProperty(
				KingdomShopStockRules.StockSettlementProperty);
			string expected = KingdomShopStockRules.StockReceiptId(System.RealmId,
				settlement, Item.IDIfAssigned);
			if (expected == null || expected != Item.GetStringProperty(
				KingdomShopStockRules.StockReceiptProperty)
				|| string.IsNullOrEmpty(Item.GetStringProperty(
					KingdomShopStockRules.StockCustodianProperty))
				|| !KingdomMarketStockProtection.CanProtect(Item, out Failure))
			{
				Failure = Failure ?? "current-realm market stock receipt is torn"; return false;
			}
			Retires = true; return true;
		}

		internal static bool TryRetireStock(KingdomSystem System, GameObject Item,
			out string Failure)
		{
			if (!CanRetireStock(System, Item, out bool retires, out Failure) || !retires)
				return !retires && Failure == null;
			GameObject holder = Item.InInventory;
			Cell cell = Item.CurrentCell;
			int count = Item.Count;
			bool hadNative = Item.HasIntProperty("_stock");
			int native = Item.GetIntProperty("_stock");
			string marketTarget = Item.GetStringProperty(
				KingdomGuestbook.MarketTransferTargetProperty);
			bool linkedTransfer = !string.IsNullOrEmpty(marketTarget) && marketTarget
				== Item.GetStringProperty(KingdomShopStockRules.StockTransferTargetProperty);
			if (!KingdomMarketStockProtection.TryRetire(Item))
				{ Failure = "current-realm market stock projection resisted removal"; return false; }
			if (linkedTransfer) Item.SetStringProperty(
				KingdomGuestbook.MarketTransferTargetProperty, null, RemoveIfNull: true);
			if (!GameObject.Validate(Item) || Item.InInventory != holder || Item.CurrentCell != cell
				|| Item.Count != count || Item.HasIntProperty("_stock") != hadNative
				|| Item.GetIntProperty("_stock") != native)
			{
				Failure = "market projection retirement changed its physical item"; return false;
			}
			return true;
		}

		internal static bool CanRetireLegendary(KingdomSystem System, GameObject Body,
			out bool Retires, out string Failure)
		{
			Retires = false; Failure = null;
			r_KingdomLegendaryMarketProjection marker =
				Body?.GetPart<r_KingdomLegendaryMarketProjection>();
			if (marker == null) return true;
			GenericInventoryRestocker restocker = Body.GetPart<GenericInventoryRestocker>();
			if (System == null || !GameObject.Validate(Body) || marker.HandoffPrepared != 0
				|| marker.MarketTier != 0 || marker.HandoffResidentId != 0
				|| marker.PriorResidentId != 0 || !string.IsNullOrEmpty(marker.HandoffIntent)
				|| !string.IsNullOrEmpty(marker.PriorBodyObjectId)
				|| Body.HasStringProperty(KingdomGuestbook.MarketHandoffIntentProperty)
				|| Body.HasStringProperty(KingdomGuestbook.MarketHandoffPriorProperty)
				|| marker.RealmId != System.RealmId
				|| marker.BodyObjectId != Body.IDIfAssigned
				|| string.IsNullOrEmpty(marker.SettlementId)
				|| System.SettlementIdForOwnedZone(Body.CurrentZone?.ZoneID) != marker.SettlementId
				|| !KingdomCitizenship.BelongsTo(System, Body)
				|| Body.GetIntProperty(KingdomGuestbook.LegendaryTraderResidentProperty) != 1
				|| !Body.HasIntProperty("Merchant") || Body.GetIntProperty("Merchant") != 1
				|| !Body.HasIntProperty("InventoryTier")
				|| Body.GetIntProperty("InventoryTier") < 1
				|| Body.GetIntProperty("InventoryTier") > KingdomShopStockRules.MaximumTier
				|| (Body.HasIntProperty("VillageMerchant")
					&& Body.GetIntProperty("VillageMerchant") != 1)
				|| !KingdomGrowth.SealedFiniteRestocker(restocker))
			{
				Failure = "legendary market projection is foreign or divergent"; return false;
			}
			Retires = true; return true;
		}

		internal static bool TryRetireLegendary(KingdomSystem System, GameObject Body,
			out string Failure)
		{
			if (!CanRetireLegendary(System, Body, out bool retires, out Failure) || !retires)
				return !retires && Failure == null;
			Body.RemoveIntProperty("VillageMerchant");
			r_KingdomLegendaryMarketProjection marker =
				Body.GetPart<r_KingdomLegendaryMarketProjection>();
			if (marker != null) Body.RemovePart(marker);
			if (Body.HasIntProperty("VillageMerchant")
				|| Body.GetPart<r_KingdomLegendaryMarketProjection>() != null || !Body.IsMerchant())
			{
				Failure = "legendary civic projection resisted exact removal"; return false;
			}
			return true;
		}

		internal static bool IsStockProjectionProperty(string Name)
		{
			return Name == KingdomShopStockRules.StockReceiptProperty
				|| Name == KingdomShopStockRules.StockRealmProperty
				|| Name == KingdomShopStockRules.LegacyStockRealmProperty
				|| Name == KingdomShopStockRules.StockSettlementProperty
				|| Name == KingdomShopStockRules.StockCustodianProperty
				|| Name == KingdomShopStockRules.StockTransferTargetProperty
				|| Name == KingdomGuestbook.MarketTransferTargetProperty
				|| Name == KingdomShopStockRules.StockOwnsNoRestockProperty
				|| Name == KingdomShopStockRules.StockOwnsNeverStackProperty;
		}
	}
}
