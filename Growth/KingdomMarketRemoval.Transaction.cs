using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	internal sealed class KingdomMarketRemovalTransaction
	{
		internal readonly List<KingdomMarketStockSnapshot> Stock =
			new List<KingdomMarketStockSnapshot>();
		internal readonly List<KingdomLegendaryMarketSnapshot> Legends =
			new List<KingdomLegendaryMarketSnapshot>();
	}

	internal sealed class KingdomMarketStockSnapshot
	{
		internal GameObject Item;
		internal string[] Strings;
		internal bool[] HasInts;
		internal int[] Ints;
		internal bool HadPart;
		internal GameObject Holder;
		internal Cell Cell;
		internal int Count;
		internal bool HadNativeStock;
		internal int NativeStock;
	}

	internal sealed class KingdomLegendaryMarketSnapshot
	{
		internal GameObject Body;
		internal bool HadVillage;
		internal int Village;
		internal string RealmId;
		internal string SettlementId;
		internal string BodyObjectId;
		internal int HandoffPrepared;
		internal int MarketTier;
		internal int HandoffResidentId;
		internal int PriorResidentId;
		internal string HandoffIntent;
		internal string PriorBodyObjectId;
		internal string BodyHandoffIntent;
		internal string BodyHandoffPrior;
	}

	internal static partial class KingdomMarketRemoval
	{
		private static readonly string[] StockStrings = new string[]
		{
			KingdomShopStockRules.StockReceiptProperty,
			KingdomShopStockRules.StockRealmProperty,
			KingdomShopStockRules.LegacyStockRealmProperty,
			KingdomShopStockRules.StockSettlementProperty,
			KingdomShopStockRules.StockCustodianProperty,
			KingdomShopStockRules.StockTransferTargetProperty,
			KingdomGuestbook.MarketTransferTargetProperty
		};

		private static readonly string[] StockInts = new string[]
		{
			KingdomShopStockRules.StockOwnsNoRestockProperty, "norestock",
			KingdomShopStockRules.StockOwnsNeverStackProperty, "NeverStack"
		};

		internal static bool TryPrepareTransaction(KingdomSystem System,
			IEnumerable<GameObject> Stock, IEnumerable<GameObject> Legends,
			out KingdomMarketRemovalTransaction Transaction, out string Failure)
		{
			Transaction = new KingdomMarketRemovalTransaction(); Failure = null;
			foreach (GameObject item in Stock)
			{
				if (!CanRetireStock(System, item, out bool retires, out Failure) || !retires)
					return false;
				Transaction.Stock.Add(CaptureStock(item));
			}
			foreach (GameObject body in Legends)
			{
				if (!CanRetireLegendary(System, body, out bool retires, out Failure) || !retires)
					return false;
				Transaction.Legends.Add(CaptureLegend(body));
			}
			return true;
		}

		internal static bool TryCommitTransaction(KingdomSystem System,
			KingdomMarketRemovalTransaction Transaction, out string Failure)
		{
			Failure = null;
			if (System == null || Transaction == null) return false;
			for (int i = 0; i < Transaction.Stock.Count; i++)
				if (!MatchesStock(Transaction.Stock[i]))
					{ Failure = "market stock changed after destructive preview"; return false; }
			for (int i = 0; i < Transaction.Legends.Count; i++)
				if (!MatchesLegend(Transaction.Legends[i]))
					{ Failure = "legendary market changed after destructive preview"; return false; }
			try
			{
				for (int i = 0; i < Transaction.Stock.Count; i++)
					if (!TryRetireStock(System, Transaction.Stock[i].Item, out Failure))
					{
						string cause = Failure;
						return RollbackFailure(Transaction, cause, out Failure);
					}
				for (int i = 0; i < Transaction.Legends.Count; i++)
					if (!RetireLegendSnapshot(Transaction.Legends[i], out Failure))
					{
						string cause = Failure;
						return RollbackFailure(Transaction, cause, out Failure);
					}
				return true;
			}
			catch (Exception error)
			{
				return RollbackFailure(Transaction, "market removal threw "
					+ error.GetType().Name, out Failure);
			}
		}

		internal static bool TryRollback(KingdomMarketRemovalTransaction Transaction,
			out string Failure)
		{
			return TryRollbackSnapshots(Transaction, out Failure);
		}

		private static bool RollbackFailure(KingdomMarketRemovalTransaction Transaction,
			string Cause, out string Failure)
		{
			if (TryRollback(Transaction, out string rollback))
				Failure = Cause ?? "market removal transaction failed";
			else Failure = (Cause ?? "market removal transaction failed")
				+ "; rollback failed: " + rollback;
			return false;
		}

		private static KingdomMarketStockSnapshot CaptureStock(GameObject Item)
		{
			KingdomMarketStockSnapshot result = new KingdomMarketStockSnapshot
			{
				Item = Item, Strings = new string[StockStrings.Length],
				HasInts = new bool[StockInts.Length], Ints = new int[StockInts.Length],
				HadPart = Item.GetPart<r_KingdomMarketStockProjection>() != null,
				Holder = Item.InInventory, Cell = Item.CurrentCell, Count = Item.Count,
				HadNativeStock = Item.HasIntProperty("_stock"),
				NativeStock = Item.GetIntProperty("_stock")
			};
			for (int i = 0; i < StockStrings.Length; i++)
				result.Strings[i] = Item.GetStringProperty(StockStrings[i]);
			for (int i = 0; i < StockInts.Length; i++)
			{
				result.HasInts[i] = Item.HasIntProperty(StockInts[i]);
				result.Ints[i] = Item.GetIntProperty(StockInts[i]);
			}
			return result;
		}

		private static KingdomLegendaryMarketSnapshot CaptureLegend(GameObject Body)
		{
			r_KingdomLegendaryMarketProjection marker =
				Body.GetPart<r_KingdomLegendaryMarketProjection>();
			return new KingdomLegendaryMarketSnapshot
			{
				Body = Body, HadVillage = Body.HasIntProperty("VillageMerchant"),
				Village = Body.GetIntProperty("VillageMerchant"), RealmId = marker.RealmId,
				SettlementId = marker.SettlementId, BodyObjectId = marker.BodyObjectId,
				HandoffPrepared = marker.HandoffPrepared, MarketTier = marker.MarketTier,
				HandoffResidentId = marker.HandoffResidentId,
				PriorResidentId = marker.PriorResidentId,
				HandoffIntent = marker.HandoffIntent,
				PriorBodyObjectId = marker.PriorBodyObjectId,
				BodyHandoffIntent = Body.GetStringProperty(
					KingdomGuestbook.MarketHandoffIntentProperty),
				BodyHandoffPrior = Body.GetStringProperty(
					KingdomGuestbook.MarketHandoffPriorProperty)
			};
		}

		private static bool MatchesStock(KingdomMarketStockSnapshot Snapshot)
		{
			if (!GameObject.Validate(Snapshot?.Item)
				|| (Snapshot.Item.GetPart<r_KingdomMarketStockProjection>() != null)
					!= Snapshot.HadPart || Snapshot.Item.InInventory != Snapshot.Holder
				|| Snapshot.Item.CurrentCell != Snapshot.Cell || Snapshot.Item.Count != Snapshot.Count
				|| Snapshot.Item.HasIntProperty("_stock") != Snapshot.HadNativeStock
				|| Snapshot.Item.GetIntProperty("_stock") != Snapshot.NativeStock) return false;
			for (int i = 0; i < StockStrings.Length; i++)
				if (Snapshot.Item.GetStringProperty(StockStrings[i]) != Snapshot.Strings[i])
					return false;
			for (int i = 0; i < StockInts.Length; i++)
				if (Snapshot.Item.HasIntProperty(StockInts[i]) != Snapshot.HasInts[i]
					|| Snapshot.Item.GetIntProperty(StockInts[i]) != Snapshot.Ints[i]) return false;
			return true;
		}

		private static bool MatchesLegend(KingdomLegendaryMarketSnapshot Snapshot)
		{
			r_KingdomLegendaryMarketProjection marker =
				Snapshot?.Body?.GetPart<r_KingdomLegendaryMarketProjection>();
			return GameObject.Validate(Snapshot?.Body) && marker != null
				&& marker.RealmId == Snapshot.RealmId
				&& marker.SettlementId == Snapshot.SettlementId
				&& marker.BodyObjectId == Snapshot.BodyObjectId
				&& marker.HandoffPrepared == Snapshot.HandoffPrepared
				&& marker.MarketTier == Snapshot.MarketTier
				&& marker.HandoffResidentId == Snapshot.HandoffResidentId
				&& marker.PriorResidentId == Snapshot.PriorResidentId
				&& marker.HandoffIntent == Snapshot.HandoffIntent
				&& marker.PriorBodyObjectId == Snapshot.PriorBodyObjectId
				&& Snapshot.Body.GetStringProperty(
					KingdomGuestbook.MarketHandoffIntentProperty) == Snapshot.BodyHandoffIntent
				&& Snapshot.Body.GetStringProperty(
					KingdomGuestbook.MarketHandoffPriorProperty) == Snapshot.BodyHandoffPrior
				&& Snapshot.Body.HasIntProperty("VillageMerchant") == Snapshot.HadVillage
				&& Snapshot.Body.GetIntProperty("VillageMerchant") == Snapshot.Village
				&& Snapshot.Body.HasIntProperty("Merchant")
				&& Snapshot.Body.GetIntProperty("Merchant") == 1
				&& KingdomGrowth.SealedFiniteRestocker(
					Snapshot.Body.GetPart<XRL.World.Parts.GenericInventoryRestocker>());
		}

		private static bool RetireLegendSnapshot(KingdomLegendaryMarketSnapshot Snapshot,
			out string Failure)
		{
			Failure = null;
			if (!MatchesLegend(Snapshot))
				{ Failure = "legendary market changed before exact retirement"; return false; }
			Snapshot.Body.RemoveIntProperty("VillageMerchant");
			r_KingdomLegendaryMarketProjection marker =
				Snapshot.Body.GetPart<r_KingdomLegendaryMarketProjection>();
			if (marker != null) Snapshot.Body.RemovePart(marker);
			if (Snapshot.Body.HasIntProperty("VillageMerchant")
				|| Snapshot.Body.GetPart<r_KingdomLegendaryMarketProjection>() != null
				|| !Snapshot.Body.IsMerchant())
			{
				Failure = "legendary civic projection resisted exact removal"; return false;
			}
			return true;
		}

	}
}
