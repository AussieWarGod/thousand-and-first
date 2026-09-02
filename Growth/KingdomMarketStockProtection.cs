using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	internal static class KingdomMarketStockProtection
	{
		private const string NoRestockProperty = "norestock";
		private const string NeverStackProperty = "NeverStack";

		internal static bool CanProtect(GameObject Item, out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Item))
				{ Failure = "market protection has no physical item"; return false; }
			return CanProtectOwned(Item, NoRestockProperty,
				KingdomShopStockRules.StockOwnsNoRestockProperty, out Failure)
				&& CanProtectOwned(Item, NeverStackProperty,
					KingdomShopStockRules.StockOwnsNeverStackProperty, out Failure);
		}

		internal static bool TryProtect(GameObject Item, out string Failure)
		{
			if (!CanProtect(Item, out Failure)) return false;
			string current = Item.GetStringProperty(KingdomShopStockRules.StockRealmProperty);
			string legacy = Item.GetStringProperty(KingdomShopStockRules.LegacyStockRealmProperty);
			if (!string.IsNullOrEmpty(current) && current == legacy)
				Item.SetStringProperty(KingdomShopStockRules.LegacyStockRealmProperty, null,
					RemoveIfNull: true);
			ProtectOwned(Item, NoRestockProperty,
				KingdomShopStockRules.StockOwnsNoRestockProperty);
			ProtectOwned(Item, NeverStackProperty,
				KingdomShopStockRules.StockOwnsNeverStackProperty);
			r_KingdomMarketStockProjection projection =
				Item.RequirePart<r_KingdomMarketStockProjection>();
			if (projection == null || Item.GetPart<r_KingdomMarketStockProjection>() != projection)
				{ Failure = "market stock projection did not attach exactly"; return false; }
			return CanProtect(Item, out Failure);
		}

		internal static bool HasProjection(GameObject Item)
		{
			return GameObject.Validate(Item) && (Item.HasStringProperty(
				KingdomShopStockRules.StockReceiptProperty) || Item.HasStringProperty(
				KingdomShopStockRules.StockRealmProperty) || Item.HasStringProperty(
				KingdomShopStockRules.LegacyStockRealmProperty) || Item.HasStringProperty(
				KingdomShopStockRules.StockSettlementProperty) || Item.HasStringProperty(
				KingdomShopStockRules.StockCustodianProperty) || Item.HasStringProperty(
				KingdomShopStockRules.StockTransferTargetProperty) || Item.HasIntProperty(
				KingdomShopStockRules.StockOwnsNoRestockProperty) || Item.HasIntProperty(
				KingdomShopStockRules.StockOwnsNeverStackProperty)
				|| Item.GetPart<r_KingdomMarketStockProjection>() != null);
		}

		internal static bool TryRetire(GameObject Item)
		{
			if (!GameObject.Validate(Item)) return false;
			Item.SetStringProperty(KingdomShopStockRules.StockReceiptProperty, null,
				RemoveIfNull: true);
			Item.SetStringProperty(KingdomShopStockRules.StockRealmProperty, null,
				RemoveIfNull: true);
			Item.SetStringProperty(KingdomShopStockRules.LegacyStockRealmProperty, null,
				RemoveIfNull: true);
			Item.SetStringProperty(KingdomShopStockRules.StockSettlementProperty, null,
				RemoveIfNull: true);
			Item.SetStringProperty(KingdomShopStockRules.StockCustodianProperty, null,
				RemoveIfNull: true);
			Item.SetStringProperty(KingdomShopStockRules.StockTransferTargetProperty, null,
				RemoveIfNull: true);
			RetireOwned(Item, NoRestockProperty,
				KingdomShopStockRules.StockOwnsNoRestockProperty);
			RetireOwned(Item, NeverStackProperty,
				KingdomShopStockRules.StockOwnsNeverStackProperty);
			r_KingdomMarketStockProjection projection =
				Item.GetPart<r_KingdomMarketStockProjection>();
			if (projection != null) Item.RemovePart(projection);
			return !Item.HasStringProperty(KingdomShopStockRules.StockReceiptProperty)
				&& !Item.HasStringProperty(KingdomShopStockRules.StockRealmProperty)
				&& !Item.HasStringProperty(KingdomShopStockRules.LegacyStockRealmProperty)
				&& !Item.HasStringProperty(KingdomShopStockRules.StockSettlementProperty)
				&& !Item.HasStringProperty(KingdomShopStockRules.StockCustodianProperty)
				&& !Item.HasStringProperty(KingdomShopStockRules.StockTransferTargetProperty)
				&& !Item.HasIntProperty(KingdomShopStockRules.StockOwnsNoRestockProperty)
				&& !Item.HasIntProperty(KingdomShopStockRules.StockOwnsNeverStackProperty)
				&& Item.GetPart<r_KingdomMarketStockProjection>() == null;
		}

		/// <summary>Item events may release only this running realm's exact receipt. A
		/// foreign or torn receipt remains visible so another realm cannot commandeer it.</summary>
		internal static bool TryRetireCurrent(KingdomSystem System, GameObject Item,
			out string Failure)
		{
			Failure = null;
			if (!GameObject.Validate(Item))
				{ Failure = "market stock item is unavailable"; return false; }
			if (!HasProjection(Item)) return true;
			string current = Item.GetStringProperty(KingdomShopStockRules.StockRealmProperty);
			string legacy = Item.GetStringProperty(
				KingdomShopStockRules.LegacyStockRealmProperty);
			if (System == null || !KingdomShopStockRules.TryResolveStockRealm(current,
				legacy, out string realm) || !KingdomMarketStockAuthorityRules.MayRetire(
					System.RealmId, realm, true))
			{
				Failure = "market receipt belongs to another or divergent realm"; return false;
			}
			string settlement = Item.GetStringProperty(
				KingdomShopStockRules.StockSettlementProperty);
			string custodian = Item.GetStringProperty(
				KingdomShopStockRules.StockCustodianProperty);
			if (!KingdomShopStockRules.ExactStockCustody(Item.GetStringProperty(
				KingdomShopStockRules.StockReceiptProperty), realm, settlement, custodian,
				System.RealmId, settlement, custodian, Item.IDIfAssigned)
				|| !CanProtect(Item, out Failure))
			{
				Failure = Failure ?? "market receipt is not exact current-realm authority";
				return false;
			}
			return TryRetire(Item);
		}

		private static void ProtectOwned(GameObject Item, string Property, string Ownership)
		{
			bool owned = Item.GetIntProperty(Ownership) == 1;
			if (!KingdomShopStockRules.ShouldOwnProtection(
				Item.HasPropertyOrTag(Property), owned)) return;
			Item.SetIntProperty(Property, 1); Item.SetIntProperty(Ownership, 1);
		}

		private static bool CanProtectOwned(GameObject Item, string Property,
			string Ownership, out string Failure)
		{
			Failure = null;
			bool hasOwnership = Item.HasIntProperty(Ownership);
			int owned = Item.GetIntProperty(Ownership);
			if (hasOwnership && owned != 1)
			{
				Failure = "market protection ownership is divergent: " + Ownership; return false;
			}
			if (owned == 1 && (!Item.HasIntProperty(Property)
				|| Item.GetIntProperty(Property) != 1))
			{
				Failure = "market-owned protection was changed: " + Property; return false;
			}
			return true;
		}

		private static void RetireOwned(GameObject Item, string Property, string Ownership)
		{
			if (Item.GetIntProperty(Ownership) == 1 && Item.GetIntProperty(Property) == 1)
				Item.RemoveIntProperty(Property);
			Item.RemoveIntProperty(Ownership);
		}
	}
}
