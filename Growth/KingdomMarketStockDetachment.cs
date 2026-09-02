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
}
