using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGuestbook
	{
		private static void SealFiniteTrader(GameObject Trader,
			GenericInventoryRestocker Restocker, int Tier)
		{
			DisableAutomaticStock(Restocker);
			Trader.SetIntProperty("InventoryTier", Tier);
		}

		private static void DisableAutomaticStock(GenericInventoryRestocker Restocker)
		{
			Restocker.Clear(); Restocker.Chance = 0;
			Restocker.RestockFrequency = long.MaxValue;
			Restocker.LastRestockTick = Math.Max(1L, The.Game.TimeTicks);
		}

		private static void ClearHandoffMarkers(GameObject Trader)
		{
			Trader.SetStringProperty(MarketHandoffIntentProperty, null, RemoveIfNull: true);
			Trader.SetStringProperty(MarketHandoffPriorProperty, null, RemoveIfNull: true);
		}

		private static bool PreflightHandoffGraph(KingdomSystem System, GameObject Prior,
			GameObject Target, bool Resuming)
		{
			if (!KingdomMarketHandoffGlobalIndex.TryLoaded(out IList<GameObject> loaded)
				|| !KingdomMarketHandoffGraphAuthority.TryPreflight(System, loaded,
					System.CurrentSettlementId, out _)
				|| !KingdomMarketHandoffGraphAuthority.TryUnique(loaded,
					Prior?.IDIfAssigned, out GameObject uniquePrior)
				|| !ReferenceEquals(uniquePrior, Prior)
				|| !KingdomMarketHandoffGraphAuthority.TryUnique(loaded,
					Target?.IDIfAssigned, out GameObject uniqueTarget)
				|| !ReferenceEquals(uniqueTarget, Target)) return false;
			if (Resuming) return true;
			for (int i = 0; i < loaded.Count; i++)
				if (loaded[i]?.GetStringProperty(MarketTransferTargetProperty)
						== Target.IDIfAssigned
					|| loaded[i]?.GetStringProperty(
						KingdomShopStockRules.StockTransferTargetProperty) == Target.IDIfAssigned)
					return false;
			return true;
		}

		private static bool ExactCompletedHandoffTarget(KingdomSystem System, GameObject Source,
			GameObject Target, r_KingdomMarketHandoffSourceProjection Receipt,
			r_KingdomLegendaryMarketProjection Legend, int Tier)
		{
			return Receipt != null && Receipt.Exact(System, Source) && Receipt.Tier == Tier
				&& Receipt.TargetBodyObjectId == Target?.IDIfAssigned
				&& Receipt.LifecycleTerminalClosed == 0 && Receipt.TargetTerminalDead == 0
				&& Legend != null && Legend.HandoffPrepared == 0
				&& Legend.RealmId == Receipt.RealmId
				&& Legend.SettlementId == Receipt.SettlementId
				&& Legend.BodyObjectId == Receipt.TargetBodyObjectId
				&& Legend.BodyObjectId == Target.IDIfAssigned && Target.IsAlive && !Target.IsPlayer()
				&& KingdomCitizenship.BelongsTo(System, Target)
				&& System.SettlementIdForOwnedZone(Target.CurrentZone?.ZoneID)
					== Receipt.SettlementId
				&& Target.GetIntProperty(LegendaryTraderResidentProperty) == 1
				&& Target.HasIntProperty("Merchant") && Target.GetIntProperty("Merchant") == 1
				&& Target.GetIntProperty("InventoryTier") == Receipt.Tier
				&& Target.GetIntProperty("VillageMerchant") == 1
				&& KingdomGrowth.SealedFiniteRestocker(
					Target.GetPart<GenericInventoryRestocker>())
				&& KingdomMarketHandoffIntentRules.ExactOrRecoverable(Target.GetStringProperty(
					MarketHandoffIntentProperty), Receipt.Intent, Target.GetStringProperty(
					MarketHandoffPriorProperty), Receipt.SourceBodyObjectId);
		}

		private static GameObject FindPriorMarketMerchant(KingdomSystem System, Zone Zone,
			GameObject Trader, out int Merchants)
		{
			Merchants = 0; GameObject prior = null;
			foreach (GameObject item in KingdomSurvey.ObjectsFor(Zone))
			{
				if (!item.IsAlive || item.GetIntProperty("VillageMerchant") != 1
					|| !KingdomCitizenship.BelongsTo(System, item)) continue;
				Merchants++;
				if (!ReferenceEquals(item, Trader)) prior = item;
			}
			string held = Trader.GetStringProperty(MarketHandoffPriorProperty);
			if (string.IsNullOrEmpty(held)) return prior;
			GameObject exact = GameObject.FindByID(held);
			if (!GameObject.Validate(exact) || !exact.IsAlive || exact.CurrentZone != Zone
				|| !KingdomCitizenship.BelongsTo(System, exact)
				|| (prior != null && !ReferenceEquals(prior, exact))) return null;
			return exact;
		}

		private static bool TryRetirePriorMarketAuthority(KingdomSystem System,
			GameObject Prior, r_KingdomOfficeProjection Office, bool OfficeAuthority,
			bool OwnsRestocker, bool AlreadyRetired)
		{
			if (AlreadyRetired)
				return !Prior.HasIntProperty("VillageMerchant")
					&& (OwnsRestocker ? Prior.IsMerchant() : (!Prior.IsMerchant()
						&& !Prior.HasIntProperty("InventoryTier")));
			if (OfficeAuthority)
				return !OwnsRestocker && KingdomGrowth.TryCompleteTransferredMarketService(
					System, Prior, Office, out _);
			GenericInventoryRestocker old = Prior.GetPart<GenericInventoryRestocker>();
			if (!OwnsRestocker || old == null) return false;
			Prior.RemoveIntProperty("VillageMerchant");
			r_KingdomLegendaryMarketProjection marker =
				Prior.GetPart<r_KingdomLegendaryMarketProjection>();
			if (marker != null) Prior.RemovePart(marker);
			return !Prior.HasIntProperty("VillageMerchant")
				&& Prior.HasIntProperty("Merchant")
				&& Prior.HasIntProperty("InventoryTier")
				&& Prior.GetPart<GenericInventoryRestocker>() == old
				&& Prior.GetPart<r_KingdomLegendaryMarketProjection>() == null
				&& Prior.IsMerchant();
		}

		private static bool TryPreparedPriorAuthority(KingdomSystem System, Zone Zone,
			GameObject Prior, GameObject Target, int Tier, out r_KingdomOfficeProjection Office,
			out bool OfficeAuthority, out bool OwnsRestocker, out bool AlreadyRetired)
		{
			Office = Prior?.GetPart<r_KingdomOfficeProjection>();
			OfficeAuthority = false; OwnsRestocker = false; AlreadyRetired = false;
			if (System == null || !GameObject.Validate(Prior) || !Prior.IsAlive
				|| !ReferenceEquals(Prior.CurrentZone, Zone)
				|| !KingdomCitizenship.BelongsTo(System, Prior)) return false;
			string settlement = System.SettlementIdForOwnedZone(Zone.ZoneID);
			r_KingdomMarketHandoffSourceProjection source =
				Prior.GetPart<r_KingdomMarketHandoffSourceProjection>();
			if (source == null || !source.ExactLive(System, Prior) || source.Tier != Tier
				|| source.TargetBodyObjectId != Target?.IDIfAssigned) return false;
			if (Office != null && !Office.DeathResidue && Office.MarketServicePhase == 2
				&& Office.RealmId == System.RealmId && Office.SettlementId == settlement
				&& Office.BodyObjectId == Prior.IDIfAssigned
				&& Prior.GetIntProperty("InventoryTier") == Tier)
			{
				OfficeAuthority = KingdomGrowth.CanCompleteTransferredMarketService(
					System, Prior, Office, out _);
				return OfficeAuthority;
			}
			r_KingdomLegendaryMarketProjection legend =
				Prior.GetPart<r_KingdomLegendaryMarketProjection>();
			GenericInventoryRestocker restocker = Prior.GetPart<GenericInventoryRestocker>();
			int heldTier = Prior.GetIntProperty("InventoryTier");
			if (legend != null && legend.HandoffPrepared == 0
				&& legend.RealmId == System.RealmId && legend.SettlementId == settlement
				&& legend.BodyObjectId == Prior.IDIfAssigned
				&& Prior.GetIntProperty(LegendaryTraderResidentProperty) == 1
				&& Prior.HasIntProperty("Merchant") && Prior.GetIntProperty("Merchant") == 1
				&& heldTier == Tier
				&& (!Prior.HasIntProperty("VillageMerchant")
					|| Prior.GetIntProperty("VillageMerchant") == 1)
				&& KingdomGrowth.SealedFiniteRestocker(restocker))
			{
				OwnsRestocker = true; return true;
			}
			if (legend == null && Prior.GetIntProperty(LegendaryTraderResidentProperty) == 1
				&& Prior.HasIntProperty("Merchant") && Prior.GetIntProperty("Merchant") == 1
				&& !Prior.HasIntProperty("VillageMerchant")
				&& heldTier == Tier
				&& KingdomGrowth.SealedFiniteRestocker(restocker))
			{
				OwnsRestocker = true; AlreadyRetired = true; return true;
			}
			AlreadyRetired = Office != null && !Office.DeathResidue
				&& Office.RealmId == System.RealmId && Office.SettlementId == settlement
				&& Office.BodyObjectId == Prior.IDIfAssigned && Office.MarketServicePhase == 0
				&& !Prior.HasIntProperty("VillageMerchant")
				&& !Prior.HasIntProperty("Merchant") && !Prior.HasIntProperty("InventoryTier")
				&& restocker == null && !Prior.IsMerchant();
			return AlreadyRetired;
		}
	}
}
