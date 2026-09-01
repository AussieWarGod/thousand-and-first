using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomGuestbook
	{
		private static bool ReproveResumingHandoff(KingdomSystem System,
			GameObject Target, int Tier)
		{
			return System != null && System.HasShopkeeper && System.ShopTier == Tier
				&& KingdomMarketProviderAuthority.TryProveLegendary(System, Target,
					Tier, out _);
		}
		private static bool EnsureHandoffReceipt(KingdomSystem System, GameObject Source,
			GameObject Item)
		{
			return KingdomMarketStockCustody.TryAdmitLegacyHandoff(System,
				System.CurrentSettlementId, Source, Item, out _);
		}
		private static bool TryRetireLegacyIntent(KingdomSystem System, GameObject Prior,
			GameObject Holder, GameObject Item, int Tier)
		{
			r_KingdomMarketHandoffSourceProjection source =
				Prior?.GetPart<r_KingdomMarketHandoffSourceProjection>();
			GameObject target = GameObject.FindByID(source?.TargetBodyObjectId);
			r_KingdomLegendaryMarketProjection legend =
				target?.GetPart<r_KingdomLegendaryMarketProjection>();
			if (source == null || !source.ExactLive(System, Prior) || source.Tier != Tier
				|| !ReferenceEquals(Holder, Prior) && !ReferenceEquals(Holder, target)
				|| legend == null || legend.RealmId != source.RealmId
				|| legend.SettlementId != source.SettlementId
				|| legend.BodyObjectId != source.TargetBodyObjectId
				|| legend.HandoffPrepared != 0 && legend.HandoffPrepared != 1
				|| legend.HandoffPrepared == 1 && legend.HandoffIntent != source.Intent) return false;
			if (!string.IsNullOrEmpty(Item.GetStringProperty(
				KingdomShopStockRules.StockTransferTargetProperty))
				|| !ExactTransferableStock(System, Prior, Holder, Item, Tier)) return false;
			Item.SetStringProperty(MarketTransferTargetProperty, null, RemoveIfNull: true);
			return !Item.HasStringProperty(MarketTransferTargetProperty);
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

		private static bool PrepareSourceHandoff(KingdomSystem System, GameObject Source,
			GameObject Target, int Tier, string Intent, int SourceResident, int TargetResident)
		{
			string lodge = Target.GetStringProperty(LodgeReceiptProperty);
			string operation = lodge != null && lodge.StartsWith("intent:",
				StringComparison.Ordinal) ? lodge.Substring(7) : null;
			if (string.IsNullOrEmpty(operation)) return false;
			KingdomLifecycleOperation open = KingdomGuestLifecycle.Open(System,
				KingdomLifecycleLane.NotableGuest);
			if (open == null || open.Id != operation || open.ObjectId != Target.IDIfAssigned
				|| string.IsNullOrEmpty(open.PlanHash)) return false;
			r_KingdomMarketHandoffSourceProjection marker =
				Source.GetPart<r_KingdomMarketHandoffSourceProjection>();
			bool created = marker == null;
			if (created) marker = Source.RequirePart<r_KingdomMarketHandoffSourceProjection>();
			bool exact = marker != null && marker.Stamp(System, System.CurrentSettlementId,
				Source, Target, Tier, SourceResident, TargetResident, Intent, operation,
				open.PlanHash, open.Sequence)
				&& Source.GetPart<r_KingdomMarketHandoffSourceProjection>() == marker;
			if (exact) exact = KingdomLifecycleRules.TryFreezeLodgeMarketSource(
				System.LifecycleBook, open, Source.IDIfAssigned, SourceResident, Tier, Intent);
			if (!exact && created && Source.GetPart<r_KingdomMarketHandoffSourceProjection>()
				== marker) Source.RemovePart(marker);
			return exact;
		}
		private static bool ExactLifecycleMarketSourceCheckpoint(KingdomSystem System,
			GameObject Target)
		{
			return TryOpenLodgeForTarget(System, Target, out KingdomLifecycleOperation open)
				&& KingdomLifecycleRules.TryFreezeNoLodgeMarketSource(System.LifecycleBook, open);
		}
		private static bool TryOpenLodgeForTarget(KingdomSystem System, GameObject Target,
			out KingdomLifecycleOperation Open)
		{
			Open = KingdomGuestLifecycle.Open(System, KingdomLifecycleLane.NotableGuest);
			string lodge = Target?.GetStringProperty(LodgeReceiptProperty);
			return Open != null && Open.ObjectId == Target?.IDIfAssigned
				&& (lodge == Open.Id || lodge == "intent:" + Open.Id);
		}
		private static bool CompleteCommittedSourceResidue(KingdomSystem System,
			GameObject Target, int Tier)
		{
			if (!KingdomMarketHandoffGlobalIndex.TryLoaded(out IList<GameObject> loaded))
				return false;
			if (!KingdomMarketHandoffGraphAuthority.TryPreflight(System, loaded,
				System.CurrentSettlementId, out _)
				|| !KingdomMarketHandoffGraphAuthority.TryUnique(loaded,
					Target?.IDIfAssigned, out GameObject uniqueTarget)
				|| !ReferenceEquals(uniqueTarget, Target)) return false;
			List<GameObject> sources = new List<GameObject>();
			for (int i = 0; i < loaded.Count; i++)
			{
				GameObject candidate = loaded[i];
				r_KingdomMarketHandoffSourceProjection marker = candidate?
					.GetPart<r_KingdomMarketHandoffSourceProjection>();
				if (marker == null || marker.TargetBodyObjectId != Target.IDIfAssigned) continue;
				sources.Add(candidate);
				if (sources.Count > 1) return false;
			}
			if (sources.Count == 0)
				return ExactLifecycleNoMarketSourceCheckpoint(System, Target)
					|| ExactLifecycleCommittedMarketSourceCheckpoint(System, Target);
			GameObject source = sources[0];
			r_KingdomMarketHandoffSourceProjection exact =
				source.GetPart<r_KingdomMarketHandoffSourceProjection>();
			r_KingdomLegendaryMarketProjection legend =
				Target.GetPart<r_KingdomLegendaryMarketProjection>();
			if (!ExactCompletedHandoffTarget(System, source, Target, exact, legend, Tier))
				return false;
			List<GameObject> transfer = new List<GameObject>();
			List<string> marketTargets = new List<string>();
			List<string> stockTargets = new List<string>();
			HashSet<string> receipts = new HashSet<string>();
			for (int i = 0; i < loaded.Count; i++)
			{
				GameObject item = loaded[i];
				string marketTarget = item?.GetStringProperty(MarketTransferTargetProperty);
				string stockTarget = item.GetStringProperty(
						KingdomShopStockRules.StockTransferTargetProperty);
				if (marketTarget != Target.IDIfAssigned && stockTarget != Target.IDIfAssigned)
					continue;
				KingdomMarketHandoffIntentState pair = KingdomMarketHandoffIntentRules.Classify(
						marketTarget, Target.IDIfAssigned, stockTarget, Target.IDIfAssigned);
				string receipt = item.GetStringProperty(KingdomShopStockRules.StockReceiptProperty);
				bool projection = KingdomMarketStockProtection.HasProjection(item);
				if (pair == KingdomMarketHandoffIntentState.Divergent
					|| projection && (!KingdomMarketStockCustody.ExactHeld(System,
							exact.SettlementId, Target, item)
							|| !KingdomMarketStockProtection.CanProtect(item, out _)
							|| string.IsNullOrEmpty(receipt) || !receipts.Add(receipt))
					|| !projection && pair != KingdomMarketHandoffIntentState.FirstOnly)
					return false;
				transfer.Add(item); marketTargets.Add(marketTarget); stockTargets.Add(stockTarget);
			}
			if (!TryCommitLifecycleMarketSource(System, Target, exact, false)) return false;
			if (!TryClearCommittedHandoff(Target, exact, transfer, marketTargets,
				stockTargets)) return false;
			if (!ExactLifecycleCommittedMarketSourceCheckpoint(System, Target)) return false;
			source.RemovePart(exact);
			return source.GetPart<r_KingdomMarketHandoffSourceProjection>() == null;
		}
		private static bool TryClearCommittedHandoff(GameObject Target,
			r_KingdomMarketHandoffSourceProjection Source, List<GameObject> Transfer,
			List<string> MarketTargets, List<string> StockTargets)
		{
			string bodyIntent = Target.GetStringProperty(MarketHandoffIntentProperty);
			string bodyPrior = Target.GetStringProperty(MarketHandoffPriorProperty);
			try
			{
				for (int i = 0; i < Transfer.Count; i++)
				{
					Transfer[i].SetStringProperty(
						KingdomShopStockRules.StockTransferTargetProperty, null, RemoveIfNull: true);
					Transfer[i].SetStringProperty(MarketTransferTargetProperty, null,
						RemoveIfNull: true);
				}
				ClearHandoffMarkers(Target);
				for (int i = 0; i < Transfer.Count; i++)
					if (Transfer[i].HasStringProperty(MarketTransferTargetProperty)
						|| Transfer[i].HasStringProperty(
							KingdomShopStockRules.StockTransferTargetProperty)) throw new InvalidOperationException();
				if (!Target.HasStringProperty(MarketHandoffIntentProperty)
					&& !Target.HasStringProperty(MarketHandoffPriorProperty)) return true;
			}
			catch { }
			bool restored = true;
			for (int i = 0; i < Transfer.Count; i++)
				try
				{
					Transfer[i].SetStringProperty(MarketTransferTargetProperty,
						MarketTargets[i], RemoveIfNull: true);
					Transfer[i].SetStringProperty(
						KingdomShopStockRules.StockTransferTargetProperty,
						StockTargets[i], RemoveIfNull: true);
				}
				catch { restored = false; }
			try
			{
				Target.SetStringProperty(MarketHandoffIntentProperty, bodyIntent,
					RemoveIfNull: true);
				Target.SetStringProperty(MarketHandoffPriorProperty, bodyPrior,
					RemoveIfNull: true);
			}
			catch { restored = false; }
			if (!restored) KingdomLog.Log("committed market handoff rollback remains quarantined");
			return false;
		}
		private static bool TryCommitLifecycleMarketSource(KingdomSystem System,
			GameObject Target, r_KingdomMarketHandoffSourceProjection Source, bool Dead)
		{
			KingdomLifecycleOperation open = KingdomGuestLifecycle.Open(System,
				KingdomLifecycleLane.NotableGuest);
			return open != null && open.Id == Source.LifecycleOperationId
				&& open.ObjectId == Target.IDIfAssigned && open.PlanHash == Source.LifecyclePlanHash
				&& open.Sequence == Source.LifecycleSequence
				&& KingdomLifecycleRules.TryCommitLodgeMarketSource(System.LifecycleBook, open,
					Source.SourceBodyObjectId, Source.SourceResidentId, Source.Tier,
					Source.Intent, Dead);
		}
		private static bool ExactLifecycleCommittedMarketSourceCheckpoint(KingdomSystem System,
			GameObject Target)
		{
			if (!TryOpenLodgeForTarget(System, Target, out KingdomLifecycleOperation open))
				return false;
			KingdomLifecycleLodgeTerminalReceipt receipt = open?.LodgeTerminal;
			return receipt != null
				&& receipt.MarketSourcePrepared
					== KingdomLifecycleLodgeTerminalReceipt.MarketCommitted
				&& !string.IsNullOrEmpty(receipt.MarketSourceBodyObjectId)
				&& receipt.MarketSourceResidentId > 0 && receipt.MarketTier > 0
				&& !string.IsNullOrEmpty(receipt.MarketIntent)
				&& !string.IsNullOrEmpty(receipt.MarketSourceProofId)
				&& !Target.HasStringProperty(MarketHandoffIntentProperty)
				&& !Target.HasStringProperty(MarketHandoffPriorProperty)
				&& KingdomLifecycleRules.TryFreezeNoLodgeMarketSource(System.LifecycleBook, open);
		}

		private static bool CompletedDeadSourceHandoff(KingdomSystem System, GameObject Target)
		{
			if (!TryOpenLodgeForTarget(System, Target, out KingdomLifecycleOperation open))
				return false;
			string operation = open.Id;
			KingdomLifecycleLodgeTerminalReceipt receipt = open?.LodgeTerminal;
			if (receipt == null
				|| receipt.MarketSourcePrepared
					!= KingdomLifecycleLodgeTerminalReceipt.MarketSourceDead
				|| Target.HasIntProperty("VillageMerchant")
				|| Target.HasStringProperty(MarketHandoffIntentProperty)
				|| Target.HasStringProperty(MarketHandoffPriorProperty)
				|| !KingdomMarketHandoffGlobalIndex.TryLoaded(out IList<GameObject> loaded))
				return false;
			r_KingdomLegendaryMarketProjection legend =
				Target.GetPart<r_KingdomLegendaryMarketProjection>();
			if (legend == null || legend.HandoffPrepared != 0
				|| legend.RealmId != System.RealmId || legend.SettlementId != open.SettlementId
				|| legend.BodyObjectId != Target.IDIfAssigned || !Target.IsAlive || Target.IsPlayer()
				|| !KingdomCitizenship.BelongsTo(System, Target)
				|| Target.GetIntProperty(LegendaryTraderResidentProperty) != 1
				|| Target.GetIntProperty("Merchant") != 1
				|| Target.GetIntProperty("InventoryTier") != receipt.MarketTier
				|| !KingdomGrowth.SealedFiniteRestocker(
					Target.GetPart<XRL.World.Parts.GenericInventoryRestocker>())) return false;
			for (int i = 0; i < loaded.Count; i++)
				if (loaded[i]?.GetPart<r_KingdomMarketHandoffSourceProjection>()?
					.LifecycleOperationId == operation
					|| loaded[i]?.GetStringProperty(MarketTransferTargetProperty)
						== Target.IDIfAssigned
					|| loaded[i]?.GetStringProperty(
						KingdomShopStockRules.StockTransferTargetProperty) == Target.IDIfAssigned
					|| ReferenceEquals(loaded[i]?.InInventory, Target)
						&& KingdomMarketStockProtection.HasProjection(loaded[i])) return false;
			return KingdomLifecycleRules.TryFreezeNoLodgeMarketSource(System.LifecycleBook, open);
		}

		private static bool ExactLifecycleNoMarketSourceCheckpoint(KingdomSystem System,
			GameObject Target)
		{
			if (!TryOpenLodgeForTarget(System, Target, out KingdomLifecycleOperation open))
				return false;
			KingdomLifecycleLodgeTerminalReceipt receipt = open?.LodgeTerminal;
			return receipt != null
				&& receipt.MarketSourcePrepared == KingdomLifecycleLodgeTerminalReceipt.MarketNone
				&& receipt.MarketSourceBodyObjectId == null
				&& receipt.MarketSourceResidentId == 0 && receipt.MarketTier == 0
				&& receipt.MarketIntent == null && receipt.MarketSourceProofId == null
				&& KingdomLifecycleRules.TryFreezeNoLodgeMarketSource(System.LifecycleBook, open);
		}
	}
}
