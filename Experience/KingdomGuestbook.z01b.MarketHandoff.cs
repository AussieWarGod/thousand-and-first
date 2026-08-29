using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGuestbook
	{
		private const string MarketHandoffIntentProperty = "TAFLocalMarketHandoffIntent";
		private const string MarketHandoffPriorProperty = "TAFLocalMarketHandoffPrior";
		private const string MarketTransferTargetProperty = "TAFLocalMarketTransferTarget";

		/// <summary>Moves only stamped, city-local finite stock. The old merchant remains canonical
		/// until every move reads back. A failed move is rolled back; if rollback itself cannot finish,
		/// exact object markers keep the open lifecycle operation deterministically resumable.</summary>
		private static bool ConfigureLegendaryTraderShop(GameObject Trader, int Tier)
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			Zone zone = Trader?.CurrentZone;
			GenericInventoryRestocker restocker = Trader?.GetPart<GenericInventoryRestocker>();
			if (!GameObject.Validate(Trader) || system == null || zone == null || restocker == null
				|| Tier < KingdomGuestRules.LegendaryTraderMinimumShopTier) return false;

			GameObject prior = FindPriorMarketMerchant(system, zone, Trader, out int merchants);
			bool canonical = Trader.GetIntProperty("VillageMerchant") == 1;
			if (canonical && merchants == 1 && prior == null)
			{
				SealFiniteTrader(Trader, restocker, Tier);
				ClearTransferMarkers(Trader);
				ClearHandoffMarkers(Trader);
				return true;
			}
			if ((canonical && (merchants < 1 || merchants > 2))
				|| (!canonical && merchants != 1) || !GameObject.Validate(prior)) return false;
			string priorId = prior.IDIfAssigned;
			string traderId = Trader.IDIfAssigned;
			if (string.IsNullOrEmpty(priorId) || string.IsNullOrEmpty(traderId)) return false;

			string source = KingdomShopStockRules.SourceId(system.RealmId,
				system.CurrentSettlementId, Tier);
			string intent = source == null ? null : source + ":handoff:" + priorId + ":" + traderId;
			string heldIntent = Trader.GetStringProperty(MarketHandoffIntentProperty);
			string heldPrior = Trader.GetStringProperty(MarketHandoffPriorProperty);
			if (intent == null || (!string.IsNullOrEmpty(heldIntent) && heldIntent != intent)
				|| (!string.IsNullOrEmpty(heldPrior) && heldPrior != priorId)) return false;
			Trader.SetStringProperty(MarketHandoffIntentProperty, intent);
			Trader.SetStringProperty(MarketHandoffPriorProperty, priorId);
			if (!TransferExactLocalMarketStock(system, prior, Trader)) return false;

			ProtectFiniteTraderStock(Trader);
			SealFiniteTrader(Trader, restocker, Tier);
			Trader.SetIntProperty("Merchant", 1);
			Trader.SetIntProperty("VillageMerchant", 1);
			ProtectFiniteTraderStock(prior);
			GenericInventoryRestocker old = prior.GetPart<GenericInventoryRestocker>();
			if (old != null) DisableAutomaticStock(old);
			prior.SetIntProperty("VillageMerchant", 0);
			prior.SetIntProperty("Merchant", 0);
			ClearTransferMarkers(Trader);
			ClearHandoffMarkers(Trader);
			return Trader.GetIntProperty("VillageMerchant") == 1
				&& prior.GetIntProperty("VillageMerchant") == 0;
		}

		private static GameObject FindPriorMarketMerchant(KingdomSystem System, Zone Zone,
			GameObject Trader, out int Merchants)
		{
			Merchants = 0;
			GameObject prior = null;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Zone))
			{
				if (item.GetIntProperty("VillageMerchant") != 1
					|| !KingdomCitizenship.BelongsTo(System, item)) continue;
				Merchants++;
				if (!ReferenceEquals(item, Trader)) prior = item;
			}
			string held = Trader.GetStringProperty(MarketHandoffPriorProperty);
			if (!string.IsNullOrEmpty(held))
			{
				GameObject exact = GameObject.FindByID(held);
				if (GameObject.Validate(exact) && exact.CurrentZone == Zone
					&& KingdomCitizenship.BelongsTo(System, exact))
				{
					if (prior != null && !ReferenceEquals(prior, exact)) return null;
					prior = exact;
				}
			}
			return prior;
		}

		private static bool TransferExactLocalMarketStock(KingdomSystem System,
			GameObject Prior, GameObject Trader)
		{
			if (Prior.Inventory == null || Trader.Inventory == null) return false;
			List<GameObject> moved = new List<GameObject>();
			foreach (GameObject item in Trader.Inventory.Objects)
				if (item.GetStringProperty(MarketTransferTargetProperty) == Trader.IDIfAssigned)
				{
					if (!KingdomConstructionInputLeaseAuthority
						.TryObjectGraphAvailableForOrdinaryTransfer(item, out _)
						|| !ExactTransferableStock(System, item)) return false;
					moved.Add(item);
				}
			foreach (GameObject item in new List<GameObject>(Prior.Inventory.Objects))
			{
				string target = item.GetStringProperty(MarketTransferTargetProperty);
				if (!KingdomConstructionInputLeaseAuthority
					.TryObjectGraphAvailableForOrdinaryTransfer(item, out _))
				{
					if (!string.IsNullOrEmpty(target)) return false;
					continue;
				}
				if (!ExactTransferableStock(System, item)) continue;
				if (!string.IsNullOrEmpty(target) && target != Trader.IDIfAssigned) return false;
				item.SetStringProperty(MarketTransferTargetProperty, Trader.IDIfAssigned);
				if (!KingdomConstructionInputLeaseAuthority
					.TryObjectGraphAvailableForOrdinaryTransfer(item, out _)) return false;
				GameObject accepted = null;
				try { accepted = Trader.Inventory.AddObjectToInventory(item, null,
					Silent: true, NoStack: true); }
				catch (Exception ex) { KingdomLog.Log("local market transfer failed: " + ex.Message); }
				if (ReferenceEquals(accepted, item) && ReferenceEquals(item.InInventory, Trader)
					&& !Prior.Inventory.Objects.Contains(item)
					&& KingdomConstructionInputLeaseAuthority
						.TryObjectGraphAvailableForOrdinaryTransfer(item, out _))
					{ moved.Add(item); continue; }
				if (ReferenceEquals(item.InInventory, Trader) && !moved.Contains(item)) moved.Add(item);
				if (!RollbackMarketTransfer(Prior, Trader, moved))
					KingdomLog.Log("local market transfer rollback remains open for exact recovery");
				return false;
			}
			for (int i = 0; i < moved.Count; i++)
				if (!KingdomConstructionInputLeaseAuthority
					.TryObjectGraphAvailableForOrdinaryTransfer(moved[i], out _))
				{
					if (!RollbackMarketTransfer(Prior, Trader, moved))
						KingdomLog.Log("local market transfer rollback remains open for exact recovery");
					return false;
				}
			return true;
		}

		private static bool RollbackMarketTransfer(GameObject Prior, GameObject Trader,
			List<GameObject> Moved)
		{
			bool complete = true;
			for (int i = Moved.Count - 1; i >= 0; i--)
			{
				GameObject item = Moved[i];
				if (!ReferenceEquals(item.InInventory, Trader)) { complete = false; continue; }
				if (!KingdomConstructionInputLeaseAuthority
					.TryObjectGraphAvailableForOrdinaryTransfer(item, out _))
					{ complete = false; continue; }
				GameObject accepted = null;
				try { accepted = Prior.Inventory.AddObjectToInventory(item, null,
					Silent: true, NoStack: true); }
				catch (Exception ex) { KingdomLog.Log("local market rollback failed: " + ex.Message); }
				if (!ReferenceEquals(accepted, item) || !ReferenceEquals(item.InInventory, Prior)
					|| Trader.Inventory.Objects.Contains(item)
					|| !KingdomConstructionInputLeaseAuthority
						.TryObjectGraphAvailableForOrdinaryTransfer(item, out _)) complete = false;
			}
			if (complete)
				for (int i = 0; i < Moved.Count; i++)
					if (!KingdomConstructionInputLeaseAuthority
						.TryObjectGraphAvailableForOrdinaryTransfer(Moved[i], out _))
					{
						complete = false;
						break;
					}
			if (complete)
				for (int i = 0; i < Moved.Count; i++) Moved[i].SetStringProperty(
					MarketTransferTargetProperty, null, RemoveIfNull: true);
			return complete;
		}

		private static bool ExactTransferableStock(KingdomSystem System, GameObject Item)
		{
			int tier = Item.GetIntProperty(KingdomShopStockRules.ItemTierProperty);
			string source = KingdomShopStockRules.SourceId(System.RealmId,
				System.CurrentSettlementId, tier);
			return !Item.IsImportant() && KingdomConstructionInputLeaseAuthority
				.TryObjectGraphAvailableForOrdinaryTransfer(Item, out _)
				&& source != null && tier <= System.ShopTier
				&& Item.GetStringProperty(KingdomShopStockRules.ItemSourceProperty) == source
				&& Item.GetStringProperty(KingdomShopStockRules.ItemSettlementProperty)
					== System.CurrentSettlementId;
		}

		private static void SealFiniteTrader(GameObject Trader,
			GenericInventoryRestocker Restocker, int Tier)
		{
			DisableAutomaticStock(Restocker);
			Trader.SetIntProperty("InventoryTier", Tier);
		}

		private static void DisableAutomaticStock(GenericInventoryRestocker Restocker)
		{
			Restocker.Clear(); Restocker.Chance = 0; Restocker.RestockFrequency = long.MaxValue;
			Restocker.LastRestockTick = Math.Max(1L, The.Game.TimeTicks);
		}

		private static void ProtectFiniteTraderStock(GameObject Trader)
		{
			if (Trader?.Inventory == null) return;
			foreach (GameObject item in Trader.Inventory.Objects)
				if (item.HasProperty("_stock")) item.SetIntProperty("norestock", 1);
		}

		private static void ClearTransferMarkers(GameObject Trader)
		{
			foreach (GameObject item in Trader.Inventory.Objects)
				if (item.GetStringProperty(MarketTransferTargetProperty) == Trader.IDIfAssigned)
					item.SetStringProperty(MarketTransferTargetProperty, null, RemoveIfNull: true);
		}

		private static void ClearHandoffMarkers(GameObject Trader)
		{
			Trader.SetStringProperty(MarketHandoffIntentProperty, null, RemoveIfNull: true);
			Trader.SetStringProperty(MarketHandoffPriorProperty, null, RemoveIfNull: true);
		}
	}
}
