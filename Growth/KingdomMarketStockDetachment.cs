using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Finds old market receipts through the survey's maintained loaded index. Anything
	/// outside exact direct keeper/transfer custody keeps its physical item and loses only TAF marks.</summary>
	internal static class KingdomMarketStockDetachment
	{
		/// <summary>Ends only exact current-settlement TAF custody when the physical market
		/// closes. Native <c>_stock</c>, item location, stack count, and foreign receipts remain.</summary>
		internal static bool TryRetireServiceStock(KingdomSystem System,
			KingdomSurvey Survey, string SettlementId, out string Failure)
		{
			Failure = null;
			if (System == null || Survey == null || string.IsNullOrEmpty(SettlementId)
				|| !Survey.TryLoaded(out IList<GameObject> loaded))
			{
				Failure = "market closure cannot prove the bounded loaded index"; return false;
			}
			List<GameObject> retired = new List<GameObject>();
			for (int i = 0; i < loaded.Count; i++)
			{
				GameObject item = loaded[i];
				if (!GameObject.Validate(item) || !KingdomMarketStockProtection.HasProjection(item))
					continue;
				string current = item.GetStringProperty(KingdomShopStockRules.StockRealmProperty);
				string legacy = item.GetStringProperty(KingdomShopStockRules.LegacyStockRealmProperty);
				if (!KingdomShopStockRules.TryResolveStockRealm(current, legacy,
					out string realm))
				{
					if (current == System.RealmId || legacy == System.RealmId)
						{ Failure = "market stock has divergent realm custody"; return false; }
					continue;
				}
				if (realm != System.RealmId) continue;
				string settlement = item.GetStringProperty(
					KingdomShopStockRules.StockSettlementProperty);
				if (settlement != SettlementId) continue;
				string expected = KingdomShopStockRules.StockReceiptId(System.RealmId,
					SettlementId, item.IDIfAssigned);
				if (expected == null || item.GetStringProperty(
					KingdomShopStockRules.StockReceiptProperty) != expected)
					{ Failure = "market closure found torn current-realm custody"; return false; }
				if (r_KingdomLegendaryMarketProjection.PreparedTransferAuthority(
					System, item.InInventory, item)) continue;
				if (retired.Count >= KingdomShopStockRules.MaximumCustodyRows)
					{ Failure = "market closure exceeds the bounded custody roster"; return false; }
				retired.Add(item);
			}
			if (!KingdomMarketRemoval.TryPrepareTransaction(System, retired,
				new List<GameObject>(), out KingdomMarketRemovalTransaction transaction,
				out Failure) || !KingdomMarketRemoval.TryCommitTransaction(System,
					transaction, out Failure)) return false;
			if (retired.Count > 0) KingdomLog.Log("market stock: retired " + retired.Count
				+ " closed-service receipt(s); physical goods remained untouched");
			return true;
		}

		internal static bool TryRetire(KingdomSystem System, Zone Zone,
			KingdomSurvey Survey, string SettlementId, out string Failure)
		{
			Failure = null;
			if (System == null || Zone == null || Survey == null
				|| string.IsNullOrEmpty(SettlementId)) return false;
			if (!Survey.TryLoaded(out IList<GameObject> loaded))
				{ Failure = "market detachment cannot prove the bounded loaded index"; return false; }
			List<GameObject> detached = new List<GameObject>();
			for (int i = 0; i < loaded.Count; i++)
			{
				GameObject item = loaded[i];
				if (!GameObject.Validate(item)) continue;
				string currentRealm = item.GetStringProperty(
					KingdomShopStockRules.StockRealmProperty);
				string legacyRealm = item.GetStringProperty(
					KingdomShopStockRules.LegacyStockRealmProperty);
				if (!KingdomShopStockRules.TryResolveStockRealm(currentRealm, legacyRealm,
					out string stockRealm))
				{
					if (currentRealm == System.RealmId || legacyRealm == System.RealmId)
						{ Failure = "market stock has divergent realm custody"; return false; }
					continue;
				}
				if (stockRealm != System.RealmId
					|| item.GetStringProperty(KingdomShopStockRules.StockSettlementProperty)
						!= SettlementId) continue;
				if (detached.Count >= KingdomShopStockRules.MaximumCustodyRows)
					{ Failure = "market detachment exceeds the bounded custody roster"; return false; }
				GameObject holder = item.InInventory;
				string receipt = item.GetStringProperty(KingdomShopStockRules.StockReceiptProperty);
				string expected = KingdomShopStockRules.StockReceiptId(System.RealmId,
					SettlementId, item.IDIfAssigned);
				string holderId = holder?.IDIfAssigned;
				string custodian = item.GetStringProperty(
					KingdomShopStockRules.StockCustodianProperty);
				string target = item.GetStringProperty(
					KingdomShopStockRules.StockTransferTargetProperty);
				bool eligibleHolder = GameObject.Validate(holder) && holder.IsAlive
					&& !holder.IsPlayer()
					&& KingdomCitizenship.BelongsTo(System, holder);
				bool onGround = holder == null && item.CurrentCell != null
					&& ReferenceEquals(item.CurrentCell.ParentZone, Zone);
				KingdomMarketStockLocation location = receipt == expected
					&& item.GetIntProperty("_stock") == 1
					? KingdomShopStockRules.ClassifyLocation(onGround, holder != null,
						eligibleHolder && holderId == custodian,
						eligibleHolder && holderId == target,
						ReferenceEquals(holder?.CurrentZone, Zone))
					: KingdomMarketStockLocation.Detached;
				if (location != KingdomMarketStockLocation.ReceiptedKeeper
					&& location != KingdomMarketStockLocation.ReceiptedTransfer)
					detached.Add(item);
			}
			if (!KingdomMarketRemoval.TryPrepareTransaction(System, detached,
				new List<GameObject>(), out KingdomMarketRemovalTransaction transaction,
				out Failure) || !KingdomMarketRemoval.TryCommitTransaction(System,
					transaction, out Failure)) return false;
			if (detached.Count > 0) KingdomLog.Log("market stock: retired " + detached.Count
				+ " detached receipt(s); physical goods remained untouched");
			return true;
		}
	}

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
