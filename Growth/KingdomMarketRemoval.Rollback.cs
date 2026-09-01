using System;

namespace ThousandAndFirst
{
	internal static partial class KingdomMarketRemoval
	{
		private static bool TryRollbackSnapshots(KingdomMarketRemovalTransaction Transaction,
			out string Failure)
		{
			Failure = null;
			if (Transaction == null) return true;
			bool restored = true;
			for (int i = Transaction.Legends.Count - 1; i >= 0; i--)
				try { RestoreLegend(Transaction.Legends[i]); }
				catch (Exception) { restored = false; }
			for (int i = Transaction.Stock.Count - 1; i >= 0; i--)
				try { RestoreStock(Transaction.Stock[i]); }
				catch (Exception) { restored = false; }
			for (int i = 0; i < Transaction.Stock.Count; i++)
				if (!MatchesStock(Transaction.Stock[i])) restored = false;
			for (int i = 0; i < Transaction.Legends.Count; i++)
				if (!MatchesLegend(Transaction.Legends[i])) restored = false;
			if (restored) return true;
			Failure = "market removal rollback did not restore every exact snapshot";
			return false;
		}

		private static void RestoreStock(KingdomMarketStockSnapshot Snapshot)
		{
			for (int i = 0; i < StockStrings.Length; i++) Snapshot.Item.SetStringProperty(
				StockStrings[i], Snapshot.Strings[i], RemoveIfNull: true);
			for (int i = 0; i < StockInts.Length; i++)
				if (Snapshot.HasInts[i]) Snapshot.Item.SetIntProperty(StockInts[i], Snapshot.Ints[i]);
				else Snapshot.Item.RemoveIntProperty(StockInts[i]);
			if (Snapshot.HadPart) Snapshot.Item.RequirePart<r_KingdomMarketStockProjection>();
		}

		private static void RestoreLegend(KingdomLegendaryMarketSnapshot Snapshot)
		{
			if (Snapshot.HadVillage) Snapshot.Body.SetIntProperty("VillageMerchant", Snapshot.Village);
			else Snapshot.Body.RemoveIntProperty("VillageMerchant");
			r_KingdomLegendaryMarketProjection marker =
				Snapshot.Body.RequirePart<r_KingdomLegendaryMarketProjection>();
			marker.RealmId = Snapshot.RealmId; marker.SettlementId = Snapshot.SettlementId;
			marker.BodyObjectId = Snapshot.BodyObjectId;
			marker.HandoffPrepared = Snapshot.HandoffPrepared;
			marker.MarketTier = Snapshot.MarketTier;
			marker.HandoffResidentId = Snapshot.HandoffResidentId;
			marker.PriorResidentId = Snapshot.PriorResidentId;
			marker.HandoffIntent = Snapshot.HandoffIntent;
			marker.PriorBodyObjectId = Snapshot.PriorBodyObjectId;
			Snapshot.Body.SetStringProperty(KingdomGuestbook.MarketHandoffIntentProperty,
				Snapshot.BodyHandoffIntent, RemoveIfNull: true);
			Snapshot.Body.SetStringProperty(KingdomGuestbook.MarketHandoffPriorProperty,
				Snapshot.BodyHandoffPrior, RemoveIfNull: true);
		}
	}
}
