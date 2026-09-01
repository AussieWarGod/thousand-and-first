using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{
		private static bool TryResolveAbortSource(KingdomSystem System, GameObject Target,
			r_KingdomLegendaryMarketProjection Legend, IList<GameObject> Loaded,
			out GameObject Source, out r_KingdomMarketHandoffSourceProjection Marker,
			out KingdomLifecycleOperation Open)
		{
			Source = null; Marker = null;
			Open = KingdomGuestLifecycle.Open(System, KingdomLifecycleLane.NotableGuest);
			KingdomLifecycleLodgeTerminalReceipt receipt = Open?.LodgeTerminal;
			string lodge = Target?.GetStringProperty(KingdomGuestbook.LodgeReceiptProperty);
			if (Legend == null || receipt == null
				|| !KingdomLifecycleRules.ExactLodgeMarketSourceReceipt(
					System?.LifecycleBook, Open)
				|| Open.ObjectId != Target.IDIfAssigned
				|| lodge != Open.Id && lodge != "intent:" + Open.Id
				|| receipt.MarketSourcePrepared != KingdomLifecycleLodgeTerminalReceipt.MarketPrepared
					&& receipt.MarketSourcePrepared
						!= KingdomLifecycleLodgeTerminalReceipt.MarketSourceDead
				|| receipt.MarketSourceBodyObjectId != Legend.PriorBodyObjectId
				|| receipt.MarketSourceResidentId != Legend.PriorResidentId
				|| receipt.MarketTier != Legend.MarketTier
				|| receipt.MarketIntent != Legend.HandoffIntent) return false;
			if (!KingdomMarketHandoffGraphAuthority.TryPreflight(System, Loaded,
				Legend.SettlementId, out _) || !KingdomMarketHandoffGraphAuthority.TryUnique(
				Loaded, Target.IDIfAssigned, out GameObject uniqueTarget)
				|| !ReferenceEquals(uniqueTarget, Target)
				|| !KingdomMarketHandoffGraphAuthority.TryUnique(Loaded,
					receipt.MarketSourceBodyObjectId, out GameObject uniqueSource)) return false;
			int matches = 0;
			for (int i = 0; i < Loaded.Count; i++)
			{
				r_KingdomMarketHandoffSourceProjection found = Loaded[i]?
					.GetPart<r_KingdomMarketHandoffSourceProjection>();
				if (found?.LifecycleOperationId != Open.Id) continue;
				matches++; Source = Loaded[i]; Marker = found;
			}
			if (matches > 1) return false;
			if (matches == 0)
			{
				Source = uniqueSource;
				return receipt.MarketSourcePrepared
					== KingdomLifecycleLodgeTerminalReceipt.MarketSourceDead
					&& r_KingdomLegendaryMarketProjection.DeadResident(System,
						Legend.SettlementId, Legend.PriorResidentId)
					&& (!GameObject.Validate(Source) || !Source.IsAlive
						&& Source.IDIfAssigned == receipt.MarketSourceBodyObjectId);
			}
			if (!ReferenceEquals(Source, uniqueSource)) return false;
			return Marker.SourceBodyObjectId == receipt.MarketSourceBodyObjectId
				&& Marker.SourceResidentId == receipt.MarketSourceResidentId
				&& Marker.TargetBodyObjectId == Target.IDIfAssigned
				&& Marker.TargetResidentId == receipt.ResidentId && Marker.Tier == receipt.MarketTier
				&& Marker.Intent == receipt.MarketIntent && Marker.LifecyclePlanHash == Open.PlanHash
				&& Marker.LifecycleSequence == Open.Sequence
				&& (Marker.Exact(System, Source) || Marker.ExactTerminal(System, Source));
		}

		private static bool ExactSourceDeadPreparedTarget(KingdomSystem System,
			GameObject Target, r_KingdomLegendaryMarketProjection Legend)
		{
			return System != null && GameObject.Validate(Target) && Target.IsAlive
				&& !Target.IsPlayer() && Legend.HandoffPrepared == 1
				&& Legend.RealmId == System.RealmId && !string.IsNullOrEmpty(Legend.SettlementId)
				&& Legend.BodyObjectId == Target.IDIfAssigned
				&& Legend.HandoffResidentId > 0 && Legend.PriorResidentId > 0
				&& r_KingdomLegendaryMarketProjection.DeadResident(System,
					Legend.SettlementId, Legend.PriorResidentId)
				&& KingdomCitizenship.BelongsTo(System, Target)
				&& Target.GetIntProperty(KingdomGuestbook.LegendaryTraderResidentProperty) == 1
				&& Target.GetIntProperty("Merchant") == 1
				&& Target.GetIntProperty("InventoryTier") == Legend.MarketTier
				&& KingdomGrowth.SealedFiniteRestocker(
					Target.GetPart<XRL.World.Parts.GenericInventoryRestocker>());
		}
	}
}
