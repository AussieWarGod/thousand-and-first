using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Whole-graph read-only preflight. No handoff recovery may mutate before this
	/// authority rejects duplicate identities, markers, receipts, or cross-target intent pairs.</summary>
	internal static class KingdomMarketHandoffGraphAuthority
	{
		internal static bool TryPreflight(KingdomSystem System, IList<GameObject> Loaded,
			string Settlement, out string Failure)
		{
			Failure = null;
			if (System == null || Loaded == null || string.IsNullOrEmpty(Settlement))
				return Refuse("market handoff graph has no exact authority", out Failure);
			Dictionary<string, int> identities = IdentityCounts(Loaded);
			HashSet<string> operations = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> targets = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Loaded.Count; i++)
			{
				GameObject source = Loaded[i];
				r_KingdomMarketHandoffSourceProjection marker = source?
					.GetPart<r_KingdomMarketHandoffSourceProjection>();
				if (marker == null || marker.RealmId != System.RealmId) continue;
				if ((!marker.Exact(System, source) && !marker.ExactTerminal(System, source))
					|| !operations.Add(marker.LifecycleOperationId)
					|| Count(identities, marker.SourceBodyObjectId) != 1
					|| Count(identities, marker.TargetBodyObjectId) > 1)
					return Refuse("market handoff graph has duplicate or divergent identity authority",
						out Failure);
				if (marker.SettlementId == Settlement && !targets.Add(marker.TargetBodyObjectId))
					return Refuse("market handoff graph has duplicate or divergent identity authority",
						out Failure);
			}
			HashSet<string> receipts = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Loaded.Count; i++)
			{
				GameObject item = Loaded[i];
				if (!GameObject.Validate(item)) continue;
				string market = item.GetStringProperty(KingdomGuestbook.MarketTransferTargetProperty);
				string stock = item.GetStringProperty(
					KingdomShopStockRules.StockTransferTargetProperty);
				bool marketKnown = !string.IsNullOrEmpty(market) && targets.Contains(market);
				bool stockKnown = !string.IsNullOrEmpty(stock) && targets.Contains(stock);
				if (marketKnown || stockKnown)
				{
					string target = marketKnown ? market : stock;
					if (!KingdomMarketHandoffIntentRules.ExactOrRecoverable(
						market, target, stock, target))
						return Refuse("market handoff graph has a cross-target intent pair", out Failure);
				}
				if (!KingdomMarketStockProtection.HasProjection(item)) continue;
				if (!KingdomShopStockRules.TryResolveStockRealm(item.GetStringProperty(
					KingdomShopStockRules.StockRealmProperty), item.GetStringProperty(
					KingdomShopStockRules.LegacyStockRealmProperty), out string realm))
				{
					if (marketKnown || stockKnown)
						return Refuse("target-linked stock has no exact realm", out Failure);
					continue;
				}
				string stockSettlement = item.GetStringProperty(
					KingdomShopStockRules.StockSettlementProperty);
				if (realm != System.RealmId)
				{
					if (marketKnown || stockKnown)
						return Refuse("target-linked stock belongs to another market", out Failure);
					continue;
				}
				string receipt = item.GetStringProperty(KingdomShopStockRules.StockReceiptProperty);
				string expected = KingdomShopStockRules.StockReceiptId(System.RealmId,
					stockSettlement, item.IDIfAssigned);
				if (expected == null || receipt != expected || !receipts.Add(receipt))
					return Refuse("market handoff graph has a torn or duplicate stock receipt",
						out Failure);
				if ((marketKnown || stockKnown) && stockSettlement != Settlement)
					return Refuse("target-linked stock belongs to another market", out Failure);
			}
			return true;
		}

		internal static bool TryUnique(IList<GameObject> Loaded, string ObjectId,
			out GameObject Found)
		{
			Found = null; int matches = 0;
			for (int i = 0; Loaded != null && i < Loaded.Count; i++)
				if (Loaded[i]?.IDIfAssigned == ObjectId) { Found = Loaded[i]; matches++; }
			return matches <= 1;
		}

		private static Dictionary<string, int> IdentityCounts(IList<GameObject> Loaded)
		{
			Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.Ordinal);
			for (int i = 0; i < Loaded.Count; i++)
			{
				string id = Loaded[i]?.IDIfAssigned;
				if (string.IsNullOrEmpty(id)) continue;
				result.TryGetValue(id, out int count); result[id] = count + 1;
			}
			return result;
		}

		private static int Count(Dictionary<string, int> Counts, string Key)
		{
			return !string.IsNullOrEmpty(Key) && Counts.TryGetValue(Key, out int value) ? value : 0;
		}

		private static bool Refuse(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
