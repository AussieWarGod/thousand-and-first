using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGuestbook
	{
		internal const string MarketHandoffIntentProperty = "TAFLocalMarketHandoffIntent";
		internal const string MarketHandoffPriorProperty = "TAFLocalMarketHandoffPrior";
		internal const string MarketTransferTargetProperty = "TAFLocalMarketTransferTarget";

		/// <summary>Moves only stamped, city-local finite stock. The old merchant remains canonical
		/// until every move reads back. A failed move is rolled back; if rollback itself cannot finish,
		/// exact object markers keep the open lifecycle operation deterministically resumable.</summary>
		private static bool ConfigureLegendaryTraderShop(GameObject Trader, int Tier)
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			Zone zone = Trader?.CurrentZone;
			GenericInventoryRestocker restocker = Trader?.GetPart<GenericInventoryRestocker>();
			r_KingdomLegendaryMarketProjection heldProjection =
				Trader?.GetPart<r_KingdomLegendaryMarketProjection>();
			int marketTier = heldProjection?.HandoffPrepared == 1
				? heldProjection.MarketTier : Tier;
			if (!GameObject.Validate(Trader) || system == null || zone == null || restocker == null
				|| marketTier < KingdomGuestRules.LegendaryTraderMinimumShopTier
				|| marketTier > KingdomShopStockRules.MaximumTier) return false;
			if (CompletedDeadSourceHandoff(system, Trader)) return true;
			GameObject prior = FindPriorMarketMerchant(system, zone, Trader, out int merchants);
			bool canonical = Trader.GetIntProperty("VillageMerchant") == 1;
			if (canonical && merchants == 1
				&& CompleteCommittedSourceResidue(system, Trader, marketTier)) prior = null;
			if (canonical && merchants == 1 && prior == null)
			{
				if (!system.HasShopkeeper || marketTier != system.ShopTier) return false;
				SealFiniteTrader(Trader, restocker, marketTier);
				if (!TryCommitLegendaryMarketProjection(system, Trader, marketTier)) return false;
				return ExactLifecycleMarketSourceCheckpoint(system, Trader);
			}
			if (!GameObject.Validate(prior)) return false;
			string priorId = prior.IDIfAssigned;
			string traderId = Trader.IDIfAssigned;
			if (string.IsNullOrEmpty(priorId) || string.IsNullOrEmpty(traderId)) return false;

			string source = KingdomShopStockRules.SourceId(system.RealmId,
				system.CurrentSettlementId, marketTier);
			string intent = source == null ? null : source + ":handoff:" + priorId + ":" + traderId;
			string heldIntent = Trader.GetStringProperty(MarketHandoffIntentProperty);
			string heldPrior = Trader.GetStringProperty(MarketHandoffPriorProperty);
			if (intent == null || (!string.IsNullOrEmpty(heldIntent) && heldIntent != intent)
				|| (!string.IsNullOrEmpty(heldPrior) && heldPrior != priorId)) return false;
			r_KingdomLegendaryMarketProjection prepared = heldProjection;
			bool resuming = heldIntent == intent && heldPrior == priorId && prepared != null
				&& prepared.MatchesPreparedHandoff(system, Trader, marketTier, intent, priorId);
			if (!resuming && !KingdomSuccession.MarketHandoffMayStart()) return false;
			r_KingdomOfficeProjection priorOffice;
			bool officeAuthority;
			bool ownsPriorRestocker;
			bool priorAlreadyRetired;
			if (resuming)
			{
				if (!ReproveResumingHandoff(system, Trader, marketTier)) return false;
				if (!TryPreparedPriorAuthority(system, zone, prior, Trader, marketTier,
					out priorOffice, out officeAuthority, out ownsPriorRestocker,
					out priorAlreadyRetired)) return false;
			}
			else
			{
				if ((canonical && (merchants < 1 || merchants > 2))
					|| (!canonical && merchants != 1)
					|| !system.HasShopkeeper || marketTier != system.ShopTier
					|| !KingdomGrowth.TryAuthorizedMarketBody(system, zone, prior, marketTier,
						out ownsPriorRestocker)) return false;
				priorOffice = prior.GetPart<r_KingdomOfficeProjection>();
				officeAuthority = priorOffice != null && priorOffice.MarketServicePhase == 2;
				priorAlreadyRetired = false;
				if (officeAuthority && !KingdomGrowth.CanCompleteTransferredMarketService(
					system, prior, priorOffice, out _)) return false;
			}
			int traderResidentId = resuming ? prepared.HandoffResidentId
				: Simulation.City.KingdomResidents.IdOf(Trader);
			int priorResidentId = resuming ? prepared.PriorResidentId
				: Simulation.City.KingdomResidents.IdOf(prior);
			if (!PreflightHandoffGraph(system, prior, Trader, resuming)) return false;
			if (!PrepareSourceHandoff(system, prior, Trader, marketTier, intent,
				priorResidentId, traderResidentId)) return false;
			Trader.SetStringProperty(MarketHandoffIntentProperty, intent);
			Trader.SetStringProperty(MarketHandoffPriorProperty, priorId);
			SealFiniteTrader(Trader, restocker, marketTier);
			if (!KingdomGrowth.SealedFiniteRestocker(restocker)
				|| Trader.GetIntProperty("InventoryTier") != marketTier) return false;
			if (!TryPrepareLegendaryMarketProjection(system, Trader, marketTier,
				intent, priorId, traderResidentId, priorResidentId)) return false;
			if (!TransferExactLocalMarketStock(system, prior, Trader, marketTier)) return false;
			Trader.SetIntProperty("Merchant", 1);
			Trader.SetIntProperty("VillageMerchant", 1);
			if (!TryCommitLegendaryMarketProjection(system, Trader, marketTier)) return false;
			if (!TryRetirePriorMarketAuthority(system, prior, priorOffice,
				officeAuthority, ownsPriorRestocker, priorAlreadyRetired)) return false;
			if (!Trader.GetPart<r_KingdomLegendaryMarketProjection>().CompleteHandoff()) return false;
			if (!CompleteCommittedSourceResidue(system, Trader, marketTier)) return false;
			return Trader.GetIntProperty("VillageMerchant") == 1
				&& !prior.HasIntProperty("VillageMerchant")
				&& (ownsPriorRestocker ? prior.IsMerchant()
					: (!prior.HasIntProperty("InventoryTier") && !prior.IsMerchant()));
		}

		private static bool TransferExactLocalMarketStock(KingdomSystem System,
			GameObject Prior, GameObject Trader, int Tier)
		{
			if (Prior.Inventory == null || Trader.Inventory == null) return false;
			List<GameObject> moved = new List<GameObject>();
			foreach (GameObject item in Trader.Inventory.Objects)
				if (item.GetStringProperty(MarketTransferTargetProperty) == Trader.IDIfAssigned)
				{
					if (!OurCurrentMarketReceipt(System, item))
					{
						TryRetireLegacyIntent(System, Prior, Trader, item, Tier);
						return false;
					}
					if (OurCurrentMarketReceipt(System, item)
						&& !KingdomMarketStockCustody.ExactHeld(System,
							System.CurrentSettlementId, Trader, item)
						&& item.GetStringProperty(KingdomShopStockRules.StockTransferTargetProperty)
							!= Trader.IDIfAssigned) return false;
					if (!KingdomConstructionInputLeaseAuthority
						.TryObjectGraphAvailableForOrdinaryTransfer(item, out _)
						|| !ExactTransferableStock(System, Prior, Trader, item, Tier)) return false;
					moved.Add(item);
				}
			foreach (GameObject item in new List<GameObject>(Prior.Inventory.Objects))
			{
				string target = item.GetStringProperty(MarketTransferTargetProperty);
				bool ours = OurCurrentMarketReceipt(System, item);
				if (!ours && target == Trader.IDIfAssigned)
				{
					TryRetireLegacyIntent(System, Prior, Prior, item, Tier);
					return false;
				}
				if (!KingdomConstructionInputLeaseAuthority
					.TryObjectGraphAvailableForOrdinaryTransfer(item, out _))
				{
					if (ours || !string.IsNullOrEmpty(target)) return false;
					continue;
				}
				if (!ExactTransferableStock(System, Prior, Prior, item, Tier))
				{
					if (ours) return false;
					continue;
				}
				if (!ours && !EnsureHandoffReceipt(System, Prior, item)) return false;
				if (!string.IsNullOrEmpty(target) && target != Trader.IDIfAssigned) return false;
				if (OurCurrentMarketReceipt(System, item))
				{
					string stockTarget = item.GetStringProperty(
						KingdomShopStockRules.StockTransferTargetProperty);
					if (!string.IsNullOrEmpty(stockTarget) && stockTarget != Trader.IDIfAssigned)
						return false;
					item.SetStringProperty(KingdomShopStockRules.StockTransferTargetProperty,
						Trader.IDIfAssigned);
				}
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
				if (!RollbackMarketTransfer(System, Prior, Trader, moved))
					KingdomLog.Log("local market transfer rollback remains open for exact recovery");
				return false;
			}
			for (int i = 0; i < moved.Count; i++)
				if (!KingdomConstructionInputLeaseAuthority
					.TryObjectGraphAvailableForOrdinaryTransfer(moved[i], out _))
				{
					if (!RollbackMarketTransfer(System, Prior, Trader, moved))
						KingdomLog.Log("local market transfer rollback remains open for exact recovery");
					return false;
				}
			for (int i = 0; i < moved.Count; i++)
			{
				GameObject item = moved[i];
				if (!item.HasStringProperty(KingdomShopStockRules.StockReceiptProperty)
					|| KingdomMarketStockCustody.ExactHeld(System,
						System.CurrentSettlementId, Trader, item)) continue;
				if (!KingdomMarketStockCustody.TryCommitExternal(System,
					System.CurrentSettlementId, Prior, Trader, item, out string failure))
				{
					KingdomLog.Log("local market receipt handoff waits ("
						+ (failure ?? "divergent receipt") + ")");
					return false;
				}
			}
			return true;
		}

		private static bool RollbackMarketTransfer(KingdomSystem System, GameObject Prior,
			GameObject Trader, List<GameObject> Moved)
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
				if (OurCurrentMarketReceipt(System, item)
					&& !KingdomMarketStockCustody.TryRebindPhysical(System,
						System.CurrentSettlementId, Prior, item, out _)) complete = false;
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

		private static bool OurCurrentMarketReceipt(KingdomSystem System, GameObject Item)
		{
			return System != null && Item != null && KingdomShopStockRules.TryResolveStockRealm(
				Item.GetStringProperty(KingdomShopStockRules.StockRealmProperty),
				Item.GetStringProperty(KingdomShopStockRules.LegacyStockRealmProperty),
				out string realm) && realm == System.RealmId
				&& Item.GetStringProperty(KingdomShopStockRules.StockSettlementProperty)
					== System.CurrentSettlementId;
		}

		private static bool TryCommitLegendaryMarketProjection(KingdomSystem System,
			GameObject Trader, int Tier)
		{
			string settlement = System?.SettlementIdForOwnedZone(Trader?.CurrentZone?.ZoneID);
			if (string.IsNullOrEmpty(settlement) || settlement != System.CurrentSettlementId)
				return false;
			r_KingdomLegendaryMarketProjection marker =
				Trader.RequirePart<r_KingdomLegendaryMarketProjection>();
			bool blank = string.IsNullOrEmpty(marker.RealmId)
				&& string.IsNullOrEmpty(marker.SettlementId)
				&& string.IsNullOrEmpty(marker.BodyObjectId);
			if (!blank && (marker.RealmId != System.RealmId
				|| marker.SettlementId != settlement
				|| marker.BodyObjectId != Trader.IDIfAssigned)) return false;
			marker.Stamp(System, settlement, Trader);
			if (!marker.Prepared(System, Trader, Tier)
				|| !KingdomMarketStockCustody.TryAdmitHeld(
					System, settlement, Trader, out _)) return false;
			return true;
		}

		private static bool TryPrepareLegendaryMarketProjection(KingdomSystem System,
			GameObject Trader, int Tier, string Intent, string PriorId,
			int ResidentId, int PriorResidentId)
		{
			string settlement = System?.SettlementIdForOwnedZone(Trader?.CurrentZone?.ZoneID);
			if (string.IsNullOrEmpty(settlement) || settlement != System.CurrentSettlementId)
				return false;
			r_KingdomLegendaryMarketProjection marker =
				Trader.RequirePart<r_KingdomLegendaryMarketProjection>();
			return marker.StampPrepared(System, settlement, Trader, Tier, Intent, PriorId,
				ResidentId, PriorResidentId);
		}

	}
}
