using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{
		private sealed class MarketAbortRow
		{
			internal GameObject Item;
			internal string MarketTarget;
			internal string StockTarget;
			internal GameObject Holder;
			internal Cell Cell;
			internal int Count;
			internal bool Retire;
		}

		/// <summary>A witnessed party death cancels an open handoff without moving or deleting
		/// goods. Exact moved receipts retire; an exact living source keeps its receipts after only
		/// the frozen transfer intent is cleared. The whole bounded item mutation rolls back.</summary>
		private static bool TryAbortPreparedLegendaryHandoff(KingdomSystem System,
			KingdomSurvey Survey, GameObject Body, r_KingdomLegendaryMarketProjection Marker,
			out string Failure)
		{
			Failure = null;
			if (!KingdomMarketHandoffGlobalIndex.TryLoaded(out IList<GameObject> loaded)
				|| !TryResolveAbortSource(System, Body, Marker, loaded, out GameObject prior,
					out r_KingdomMarketHandoffSourceProjection sourceMarker,
					out KingdomLifecycleOperation lifecycle))
				{ Failure = "prepared handoff source authority is absent or divergent"; return false; }
			bool targetDead = Marker != null && Marker.ExactPreparedTerminal(System, Body);
			bool priorDead = Marker != null && Marker.ExactPriorTerminal(System, prior);
			bool sourceDeadResume = lifecycle?.LodgeTerminal?.MarketSourcePrepared
				== KingdomLifecycleLodgeTerminalReceipt.MarketSourceDead;
			if (!targetDead && !(priorDead && (Marker.ExactLivePreparedBody(System, Body)
				|| sourceDeadResume && ExactSourceDeadPreparedTarget(System, Body, Marker))))
				{ Failure = "prepared handoff lacks exact terminal resident proof"; return false; }
			if (Body.HasIntProperty("VillageMerchant")
				&& Body.GetIntProperty("VillageMerchant") != 1)
				{ Failure = "prepared target has divergent civic merchant state"; return false; }
			bool hadVillage = Body.HasIntProperty("VillageMerchant");
			int village = Body.GetIntProperty("VillageMerchant");
			int preparedTier = Marker.MarketTier;
			string preparedIntent = Marker.HandoffIntent;
			string preparedPrior = Marker.PriorBodyObjectId;
			int preparedResident = Marker.HandoffResidentId;
			int preparedPriorResident = Marker.PriorResidentId;
			if (targetDead && (sourceMarker == null
				|| sourceMarker.LifecycleTerminalClosed != 1))
				{ Failure = "dead handoff target awaits exact lifecycle terminal closure"; return false; }
			if (priorDead && !targetDead && !TryCommitDeadSourceOutcome(System, Body,
				sourceMarker, lifecycle))
				{ Failure = "dead handoff source lacks a durable lifecycle outcome"; return false; }

			List<MarketAbortRow> rows = new List<MarketAbortRow>();
			List<GameObject> retire = new List<GameObject>();
			HashSet<string> receipts = new HashSet<string>();
			string targetId = Body.IDIfAssigned;
			for (int i = 0; i < loaded.Count; i++)
			{
				GameObject item = loaded[i];
				if (!GameObject.Validate(item)) continue;
				string marketTarget = item.GetStringProperty(
					KingdomGuestbook.MarketTransferTargetProperty);
				string stockTarget = item.GetStringProperty(
					KingdomShopStockRules.StockTransferTargetProperty);
				bool exactTargetStock = KingdomMarketStockCustody.ExactHeld(System,
					Marker.SettlementId, Body, item);
				bool related = marketTarget == targetId || stockTarget == targetId
					|| exactTargetStock;
				if (!related) continue;
				KingdomMarketHandoffIntentState pair = KingdomMarketHandoffIntentRules.Classify(
					marketTarget, targetId, stockTarget, targetId);
				if (pair == KingdomMarketHandoffIntentState.Divergent)
					{ Failure = "prepared handoff has a cross-target item intent"; return false; }
				if (rows.Count >= KingdomShopStockRules.MaximumCustodyRows)
					{ Failure = "prepared handoff exceeds the bounded custody roster"; return false; }
				bool projection = KingdomMarketStockProtection.HasProjection(item);
				GameObject holder = item.InInventory;
				bool atPrior = GameObject.Validate(prior) && ReferenceEquals(holder, prior);
				bool shouldRetire = false;
				if (projection)
				{
					string custodian = item.GetStringProperty(
						KingdomShopStockRules.StockCustodianProperty);
					string receipt = item.GetStringProperty(
						KingdomShopStockRules.StockReceiptProperty);
					if (item.GetIntProperty("_stock") != 1
						|| receipt
							!= KingdomShopStockRules.StockReceiptId(System.RealmId,
								Marker.SettlementId, item.IDIfAssigned)
						|| string.IsNullOrEmpty(receipt) || !receipts.Add(receipt)
						|| item.GetStringProperty(KingdomShopStockRules.StockSettlementProperty)
							!= Marker.SettlementId
						|| !KingdomShopStockRules.TryResolveStockRealm(item.GetStringProperty(
							KingdomShopStockRules.StockRealmProperty), item.GetStringProperty(
							KingdomShopStockRules.LegacyStockRealmProperty), out string realm)
						|| realm != System.RealmId
						|| (custodian != Marker.PriorBodyObjectId && custodian != targetId)
						|| (!string.IsNullOrEmpty(stockTarget) && stockTarget != targetId)
						|| !KingdomMarketStockProtection.CanProtect(item, out Failure))
					{
						Failure = Failure ?? "prepared handoff stock is foreign or torn"; return false;
					}
					if (atPrior && !priorDead)
					{
						if (custodian != Marker.PriorBodyObjectId)
							{ Failure = "living source does not own its prepared stock"; return false; }
					}
					else { shouldRetire = true; retire.Add(item); }
				}
				else if (pair == KingdomMarketHandoffIntentState.SecondOnly
					|| pair == KingdomMarketHandoffIntentState.Paired)
					{ Failure = "prepared transfer intent lost its stock receipt"; return false; }
				rows.Add(new MarketAbortRow { Item = item, MarketTarget = marketTarget,
					StockTarget = stockTarget, Holder = holder, Cell = item.CurrentCell,
					Count = item.Count, Retire = shouldRetire });
			}

			if (!KingdomMarketRemoval.TryPrepareTransaction(System, retire,
				new List<GameObject>(), out KingdomMarketRemovalTransaction transaction,
				out Failure) || !KingdomMarketRemoval.TryCommitTransaction(
					System, transaction, out Failure)) return false;
			if (!TryClearAbortItemIntents(rows, transaction, out Failure)) return false;
			bool keepDormantLegend = priorDead && !targetDead;
			try
			{
				Body.RemoveIntProperty("VillageMerchant");
				Body.SetStringProperty(KingdomGuestbook.MarketHandoffIntentProperty,
					null, RemoveIfNull: true);
				Body.SetStringProperty(KingdomGuestbook.MarketHandoffPriorProperty,
					null, RemoveIfNull: true);
				if (keepDormantLegend) Marker.CompleteHandoff();
				else Body.RemovePart(Marker);
				if (sourceMarker != null && targetDead)
				{
					sourceMarker.TargetTerminalDead = 1;
				}
				// The source receipt is the last recovery authority to leave. A save cut before
				// this removal can prove the already-dormant SourceDead target and finish safely.
				if (sourceMarker != null && !targetDead) prior.RemovePart(sourceMarker);
				bool sourceReadback = sourceMarker == null
					|| targetDead && prior.GetPart<r_KingdomMarketHandoffSourceProjection>()
						== sourceMarker && sourceMarker.TargetTerminalDead == 1
					|| !targetDead && prior.GetPart<r_KingdomMarketHandoffSourceProjection>() == null;
				if (sourceReadback
					&& AbortReadback(Body, Marker, rows, keepDormantLegend)) return true;
			}
			catch (System.Exception error)
				{ Failure = "prepared handoff terminal cleanup threw " + error.GetType().Name; }
			for (int i = 0; i < rows.Count; i++)
			{
				rows[i].Item.SetStringProperty(KingdomGuestbook.MarketTransferTargetProperty,
					rows[i].MarketTarget, RemoveIfNull: true);
				rows[i].Item.SetStringProperty(KingdomShopStockRules.StockTransferTargetProperty,
					rows[i].StockTarget, RemoveIfNull: true);
			}
			KingdomMarketRemoval.TryRollback(transaction, out string rollback);
			if (Body.GetPart<r_KingdomLegendaryMarketProjection>() == null) Body.AddPart(Marker);
			if (keepDormantLegend) Marker.StampPrepared(System, Marker.SettlementId, Body,
				preparedTier, preparedIntent, preparedPrior, preparedResident,
				preparedPriorResident);
			if (sourceMarker != null && !targetDead
				&& prior.GetPart<r_KingdomMarketHandoffSourceProjection>() == null)
				prior.AddPart(sourceMarker);
			if (sourceMarker != null && targetDead) sourceMarker.TargetTerminalDead = 0;
			if (hadVillage) Body.SetIntProperty("VillageMerchant", village);
			else Body.RemoveIntProperty("VillageMerchant");
			Body.SetStringProperty(KingdomGuestbook.MarketHandoffIntentProperty,
				preparedIntent, RemoveIfNull: true);
			Body.SetStringProperty(KingdomGuestbook.MarketHandoffPriorProperty,
				preparedPrior, RemoveIfNull: true);
			Failure = "prepared handoff abort did not read back"
				+ (string.IsNullOrEmpty(rollback) ? "" : "; rollback: " + rollback);
			return false;
		}

		private static bool TryClearAbortItemIntents(List<MarketAbortRow> Rows,
			KingdomMarketRemovalTransaction Transaction, out string Failure)
		{
			Failure = null;
			try
			{
				for (int i = 0; i < Rows.Count; i++)
				{
					Rows[i].Item.SetStringProperty(
						KingdomShopStockRules.StockTransferTargetProperty, null, RemoveIfNull: true);
					Rows[i].Item.SetStringProperty(
						KingdomGuestbook.MarketTransferTargetProperty, null, RemoveIfNull: true);
				}
				for (int i = 0; i < Rows.Count; i++)
					if (Rows[i].Item.HasStringProperty(
						KingdomGuestbook.MarketTransferTargetProperty)
						|| Rows[i].Item.HasStringProperty(
							KingdomShopStockRules.StockTransferTargetProperty))
						throw new System.InvalidOperationException();
				return true;
			}
			catch (System.Exception error)
			{
				bool restored = true;
				for (int i = Rows.Count - 1; i >= 0; i--)
					try
					{
						Rows[i].Item.SetStringProperty(
							KingdomGuestbook.MarketTransferTargetProperty,
							Rows[i].MarketTarget, RemoveIfNull: true);
						Rows[i].Item.SetStringProperty(
							KingdomShopStockRules.StockTransferTargetProperty,
							Rows[i].StockTarget, RemoveIfNull: true);
					}
					catch { restored = false; }
				if (!KingdomMarketRemoval.TryRollback(Transaction, out string rollback))
					restored = false;
				Failure = "prepared handoff intent cleanup threw " + error.GetType().Name
					+ (restored ? "" : "; rollback incomplete: " + rollback);
				return false;
			}
		}

		private static bool TryCommitDeadSourceOutcome(KingdomSystem System, GameObject Target,
			r_KingdomMarketHandoffSourceProjection Source, KingdomLifecycleOperation Open)
		{
			KingdomLifecycleLodgeTerminalReceipt receipt = Open?.LodgeTerminal;
			return receipt != null && Open.ObjectId == Target.IDIfAssigned
				&& (Source == null || Source.LifecycleOperationId == Open.Id
					&& Source.LifecyclePlanHash == Open.PlanHash
					&& Source.LifecycleSequence == Open.Sequence)
				&& KingdomLifecycleRules.TryCommitLodgeMarketSource(System.LifecycleBook, Open,
					receipt.MarketSourceBodyObjectId, receipt.MarketSourceResidentId,
					receipt.MarketTier, receipt.MarketIntent, true);
		}

		private static bool AbortReadback(GameObject Body,
			r_KingdomLegendaryMarketProjection Marker, List<MarketAbortRow> Rows,
			bool KeepDormantLegend)
		{
			if (Body.HasIntProperty("VillageMerchant")
				|| Body.HasStringProperty(KingdomGuestbook.MarketHandoffIntentProperty)
				|| Body.HasStringProperty(KingdomGuestbook.MarketHandoffPriorProperty)
				|| KeepDormantLegend != (Body.GetPart<r_KingdomLegendaryMarketProjection>()
					== Marker)
				|| KeepDormantLegend && Marker.HandoffPrepared != 0) return false;
			for (int i = 0; i < Rows.Count; i++)
				if (!GameObject.Validate(Rows[i].Item) || Rows[i].Item.InInventory != Rows[i].Holder
					|| Rows[i].Item.CurrentCell != Rows[i].Cell || Rows[i].Item.Count != Rows[i].Count
					|| Rows[i].Item.HasStringProperty(
						KingdomGuestbook.MarketTransferTargetProperty)
					|| Rows[i].Item.HasStringProperty(
						KingdomShopStockRules.StockTransferTargetProperty)
					|| Rows[i].Retire && KingdomMarketStockProtection.HasProjection(Rows[i].Item))
					return false;
			return true;
		}
	}
}
