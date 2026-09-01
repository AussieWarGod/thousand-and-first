using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{
		private static bool TryExactTerminalReceipt(KingdomSystem System, GameObject Target,
			r_KingdomMarketHandoffSourceProjection Source, out KingdomLifecycleOperation Open,
			out KingdomLifecycleLodgeTerminalReceipt Receipt)
		{
			Open = KingdomGuestLifecycle.Open(System, KingdomLifecycleLane.NotableGuest);
			Receipt = Open?.LodgeTerminal;
			return System != null && GameObject.Validate(Target) && Source != null && Receipt != null
				&& KingdomLifecycleRules.ExactLodgeMarketSourceReceipt(
					System.LifecycleBook, Open)
				&& Open.Id == Source.LifecycleOperationId && Open.PlanHash == Source.LifecyclePlanHash
				&& Open.Sequence == Source.LifecycleSequence
				&& Open.ObjectId == Target.IDIfAssigned
				&& Open.SettlementId == Source.SettlementId
				&& Receipt.ResidentId == Source.TargetResidentId
				&& Receipt.MarketSourceBodyObjectId == Source.SourceBodyObjectId
				&& Receipt.MarketSourceResidentId == Source.SourceResidentId
				&& Receipt.MarketTier == Source.Tier && Receipt.MarketIntent == Source.Intent
				&& Receipt.MarketSourcePrepared >= KingdomLifecycleLodgeTerminalReceipt.MarketPrepared
				&& Receipt.MarketSourcePrepared <= KingdomLifecycleLodgeTerminalReceipt.MarketSourceDead
				&& (Source.Exact(System, Source.ParentObject)
					|| Source.ExactTerminal(System, Source.ParentObject));
		}

		private static bool ExactDeadHandoffTarget(KingdomSystem System, GameObject Target,
			r_KingdomMarketHandoffSourceProjection Source, KingdomLifecycleOperation Open)
		{
			return GameObject.Validate(Target) && !Target.IsAlive && !Target.IsPlayer()
				&& Target.IDIfAssigned == Source.TargetBodyObjectId
				&& Target.Blueprint == Open.Blueprint && Target.CurrentZone?.ZoneID == Open.ZoneId
				&& System.SettlementIdForOwnedZone(Target.CurrentZone?.ZoneID) == Source.SettlementId
				&& r_KingdomLegendaryMarketProjection.DeadResident(System,
					Source.SettlementId, Source.TargetResidentId)
				&& Target.GetIntProperty(KingdomGuestbook.LegendaryTraderResidentProperty) == 1;
		}

		private static bool ExactCompletedTargetLegend(KingdomSystem System, GameObject Target,
			r_KingdomMarketHandoffSourceProjection Source,
			r_KingdomLegendaryMarketProjection Legend)
		{
			return Legend != null && Legend.HandoffPrepared == 0 && Legend.MarketTier == 0
				&& Legend.HandoffResidentId == 0 && Legend.PriorResidentId == 0
				&& string.IsNullOrEmpty(Legend.HandoffIntent)
				&& string.IsNullOrEmpty(Legend.PriorBodyObjectId)
				&& Legend.RealmId == System.RealmId
				&& Legend.SettlementId == Source.SettlementId
				&& Legend.BodyObjectId == Target.IDIfAssigned
				&& (!Target.HasIntProperty("VillageMerchant")
					|| Target.GetIntProperty("VillageMerchant") == 1)
				&& Target.HasIntProperty("Merchant") && Target.GetIntProperty("Merchant") == 1
				&& Target.GetIntProperty("InventoryTier") == Source.Tier
				&& SealedFiniteRestocker(
					Target.GetPart<XRL.World.Parts.GenericInventoryRestocker>())
				&& KingdomMarketHandoffIntentRules.ExactOrRecoverable(Target.GetStringProperty(
					KingdomGuestbook.MarketHandoffIntentProperty), Source.Intent,
					Target.GetStringProperty(KingdomGuestbook.MarketHandoffPriorProperty),
					Source.SourceBodyObjectId);
		}

		private static bool TryCollectCompletedTargetRows(KingdomSystem System,
			IList<GameObject> Loaded, GameObject Target,
			r_KingdomMarketHandoffSourceProjection Source, out List<SourceAbortRow> Rows,
			out List<GameObject> Retire, out string Failure)
		{
			Rows = new List<SourceAbortRow>(); Retire = new List<GameObject>(); Failure = null;
			HashSet<string> receipts = new HashSet<string>(); string targetId = Target.IDIfAssigned;
			for (int i = 0; i < Loaded.Count; i++)
			{
				GameObject item = Loaded[i];
				if (!GameObject.Validate(item)) continue;
				string market = item.GetStringProperty(KingdomGuestbook.MarketTransferTargetProperty);
				string stock = item.GetStringProperty(KingdomShopStockRules.StockTransferTargetProperty);
				bool projection = KingdomMarketStockProtection.HasProjection(item);
				bool held = ReferenceEquals(item.InInventory, Target) && projection;
				if (market != targetId && stock != targetId && !held) continue;
				if (Rows.Count >= KingdomShopStockRules.MaximumCustodyRows)
					{ Failure = "terminal target custody exceeds its bound"; return false; }
				KingdomMarketHandoffIntentState pair = KingdomMarketHandoffIntentRules.Classify(
					market, targetId, stock, targetId);
				if (pair == KingdomMarketHandoffIntentState.Divergent)
					{ Failure = "terminal target has a cross-target item intent"; return false; }
				if (projection)
				{
					string receipt = item.GetStringProperty(KingdomShopStockRules.StockReceiptProperty);
					if (!KingdomMarketStockCustody.ExactHeld(System, Source.SettlementId,
						Target, item) || item.GetIntProperty("_stock") != 1
						|| receipt != KingdomShopStockRules.StockReceiptId(System.RealmId,
							Source.SettlementId, item.IDIfAssigned)
						|| string.IsNullOrEmpty(receipt) || !receipts.Add(receipt)
						|| !KingdomMarketStockProtection.CanProtect(item, out Failure))
						{ Failure = Failure ?? "terminal target stock is torn"; return false; }
					Retire.Add(item);
				}
				else if (pair != KingdomMarketHandoffIntentState.FirstOnly)
					{ Failure = "terminal stock intent lost exact custody"; return false; }
				Rows.Add(new SourceAbortRow { Item = item, MarketTarget = market,
					StockTarget = stock, Retire = projection });
			}
			return true;
		}

		private static bool ExactLiveDormantSourceDead(KingdomSystem System,
			IList<GameObject> Loaded, GameObject Target,
			r_KingdomMarketHandoffSourceProjection Source,
			KingdomLifecycleLodgeTerminalReceipt Receipt)
		{
			r_KingdomLegendaryMarketProjection legend =
				Target?.GetPart<r_KingdomLegendaryMarketProjection>();
			return Receipt?.MarketSourcePrepared
					== KingdomLifecycleLodgeTerminalReceipt.MarketSourceDead
				&& GameObject.Validate(Target) && Target.IsAlive && !Target.IsPlayer()
				&& KingdomMarketProviderAuthority.LiveResident(System, Source.SettlementId,
					Target, Source.TargetResidentId)
				&& ExactCompletedTargetLegend(System, Target, Source, legend)
				&& !Target.HasIntProperty("VillageMerchant")
				&& !Target.HasStringProperty(KingdomGuestbook.MarketHandoffIntentProperty)
				&& !Target.HasStringProperty(KingdomGuestbook.MarketHandoffPriorProperty)
				&& NoTargetHandoffResidue(Loaded, Target);
		}

		private static bool NoTargetHandoffResidue(IList<GameObject> Loaded, GameObject Target)
		{
			for (int i = 0; i < Loaded.Count; i++)
				if (Loaded[i]?.GetStringProperty(KingdomGuestbook.MarketTransferTargetProperty)
						== Target.IDIfAssigned || Loaded[i]?.GetStringProperty(
						KingdomShopStockRules.StockTransferTargetProperty) == Target.IDIfAssigned
					|| ReferenceEquals(Loaded[i]?.InInventory, Target)
						&& KingdomMarketStockProtection.HasProjection(Loaded[i])) return false;
			return true;
		}
	}
}
