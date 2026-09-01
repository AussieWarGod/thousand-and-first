using System;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomGrowth
	{
		private static bool TryProjectMarketService(GameObject Body,
			r_KingdomOfficeProjection Marker, int Tier, out string Failure)
		{
			Failure = null; Tier = BoundedMarketTier(Tier);
			if (!GameObject.Validate(Body) || Marker == null || Marker.MarketServicePhase != 0
				|| Body.IsMerchant() || Body.HasIntProperty("VillageMerchant")
				|| Body.HasIntProperty("InventoryTier")
				|| KingdomMarketStockCustody.HasNativeStock(Body))
			{
				Failure = "the appointed body has foreign or divergent merchant state"; return false;
			}
			Marker.MarketServicePhase = 1;
			Marker.MarketTier = Tier;
			Marker.OwnsMarketRestocker = false;
			return TryMaintainMarketService(Body, Marker, Tier, out Failure);
		}

		private static bool TryMaintainMarketService(GameObject Body,
			r_KingdomOfficeProjection Marker, int Tier, out string Failure)
		{
			Failure = null; Tier = BoundedMarketTier(Tier);
			if (!GameObject.Validate(Body) || Marker == null
				|| Marker.MarketServicePhase < 1 || Marker.MarketServicePhase > 2
				|| !RecoverableOwnedInt(Body, "Merchant", 1)
				|| !RecoverableOwnedInt(Body, "VillageMerchant", 1)
				|| !RecoverableOwnedInt(Body, "InventoryTier", Marker.MarketTier))
			{
				Failure = "the market projection diverged from its exact office marker"; return false;
			}
			GenericInventoryRestocker restocker = Body.GetPart<GenericInventoryRestocker>();
			if (Marker.OwnsMarketRestocker)
			{
				if (restocker != null && !SealedFiniteRestocker(restocker))
				{
					Failure = "the legacy office-owned restocker was changed by another authority";
					return false;
				}
				if (restocker != null) Body.RemovePart(restocker);
				Marker.OwnsMarketRestocker = false;
			}
			else if (restocker != null)
			{
				Failure = "a foreign stock authority appeared on the appointed market body";
				return false;
			}
			Body.SetIntProperty("Merchant", 1);
			Body.SetIntProperty("VillageMerchant", 1);
			Body.SetIntProperty("InventoryTier", Tier);
			Marker.MarketTier = Tier;
			Marker.MarketServicePhase = 2;
			if (!KnownMarketServiceState(Body, Marker))
			{
				Failure = "the market projection did not read back exactly"; return false;
			}
			return true;
		}

		private static bool IsLegacyMarketProjection(KingdomSystem System, GameObject Body,
			r_KingdomOfficeProjection Marker, int Tier, bool PriorShopClaim)
		{
			int heldTier = Body?.GetIntProperty("InventoryTier") ?? 0;
			return Marker != null && Marker.MarketServicePhase == 0
				&& IsLegacyAutomaticMarket(System, Body, PriorShopClaim)
				&& heldTier <= BoundedMarketTier(Tier);
		}

		private static bool IsLegacyAutomaticMarket(KingdomSystem System, GameObject Body,
			bool PriorShopClaim)
		{
			int heldTier = Body?.GetIntProperty("InventoryTier") ?? 0;
			return PriorShopClaim && System != null && GameObject.Validate(Body)
				&& Body.GetPart<r_KingdomOfficeProjection>()?.MarketServicePhase != 2
				&& Body.GetIntProperty("KingdomBorn") == 1
				&& Body.HasIntProperty("Merchant") && Body.GetIntProperty("Merchant") == 1
				&& Body.HasIntProperty("VillageMerchant")
				&& Body.GetIntProperty("VillageMerchant") == 1
				&& Body.HasIntProperty("InventoryTier") && heldTier >= 1
				&& heldTier <= KingdomShopStockRules.MaximumTier
				&& System.ShopTier >= heldTier
				&& SealedFiniteRestocker(Body.GetPart<GenericInventoryRestocker>());
		}

		private static bool AdoptLegacyMarketProjection(GameObject Body,
			r_KingdomOfficeProjection Marker, out string Failure)
		{
			Failure = null;
			Marker.MarketTier = Body.GetIntProperty("InventoryTier");
			Marker.MarketServicePhase = 2;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (!KingdomMarketStockCustody.TryAdmitHeld(system,
				Marker.SettlementId, Body, out Failure))
			{
				Marker.MarketServicePhase = 0; Marker.MarketTier = 0; return false;
			}
			GenericInventoryRestocker restocker = Body.GetPart<GenericInventoryRestocker>();
			if (restocker != null) Body.RemovePart(restocker);
			Marker.OwnsMarketRestocker = false;
			return KnownMarketServiceState(Body, Marker);
		}

		internal static bool TryCleanupMarketService(KingdomSystem System,
			KingdomCivicOfficeReceipt Receipt, GameObject Body,
			r_KingdomOfficeProjection Marker, out string Failure)
		{
			Failure = null;
			if (Marker == null || Marker.MarketServicePhase == 0) return true;
			if (!Marker.Matches(System, Receipt, Body))
			{
				Failure = "market service does not match the exact office receipt"; return false;
			}
			if (!CanCleanupMarketService(Body, Marker, out Failure)) return false;
			// Exact owner is proven. Fail authorization closed while cleanup preserves physical
			// stock and durable retry evidence on any later refusal.
			System.HasShopkeeper = false;
			System.ShopTier = 0;
			if (!TryCleanupMarketService(Body, Marker, out Failure)) return false;
			// Removing the exact held office removes civic market authority. A legendary
			// trader remains personally tradeable, but cannot stand in for the staffed
			// physical provider and held-office pair.
			return true;
		}

		internal static bool CanCleanupMarketService(GameObject Body,
			r_KingdomOfficeProjection Marker, out string Failure)
		{
			if (!CanCleanupMarketFields(Body, Marker, out Failure)) return false;
			if (Marker == null || Marker.MarketServicePhase != 2) return true;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			return KingdomMarketStockCustody.CanAdmitHeld(system,
				Marker.SettlementId, Body, out Failure);
		}

		private static bool CanCleanupMarketFields(GameObject Body,
			r_KingdomOfficeProjection Marker, out string Failure)
		{
			Failure = null;
			if (Marker == null || Marker.MarketServicePhase == 0) return true;
			if (!GameObject.Validate(Body) || Marker.MarketServicePhase < 1
				|| Marker.MarketServicePhase > 2 || Marker.MarketTier < 1
				|| Marker.MarketTier > KingdomShopStockRules.MaximumTier
				|| !OwnedIntCanBeRemoved(Body, "Merchant", 1)
				|| !OwnedIntCanBeRemoved(Body, "VillageMerchant", 1)
				|| !OwnedIntCanBeRemoved(Body, "InventoryTier", Marker.MarketTier))
			{
				Failure = "market service fields diverged from their exact marker"; return false;
			}
			GenericInventoryRestocker restocker = Body.GetPart<GenericInventoryRestocker>();
			if (Marker.OwnsMarketRestocker && restocker != null
				&& !SealedFiniteRestocker(restocker))
			{
				Failure = "the office-owned restocker is no longer finite and sealed"; return false;
			}
			if (!Marker.OwnsMarketRestocker && restocker != null)
			{
				Failure = "a foreign stock authority blocks exact market cleanup"; return false;
			}
			return true;
		}

		internal static bool TryCleanupMarketService(GameObject Body,
			r_KingdomOfficeProjection Marker, out string Failure)
		{
			if (!CanCleanupMarketService(Body, Marker, out Failure)) return false;
			if (Marker == null || Marker.MarketServicePhase == 0) return true;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (Marker.MarketServicePhase == 2
				&& !KingdomMarketStockCustody.TrySealDeparting(system,
					Body, Marker, out Failure)) return false;
			return TryClearMarketServiceFields(Body, Marker, out Failure);
		}

		internal static bool CanRemoveMarketServiceForRealmRemoval(KingdomSystem System,
			GameObject Body, r_KingdomOfficeProjection Marker, out string Failure)
		{
			if (System == null || Marker == null || Marker.RealmId != System.RealmId)
			{
				Failure = "realm-removal market projection has foreign authority"; return false;
			}
			return CanCleanupMarketFields(Body, Marker, out Failure);
		}

		internal static bool TryRemoveMarketServiceForRealmRemoval(KingdomSystem System,
			GameObject Body, r_KingdomOfficeProjection Marker, out string Failure)
		{
			if (!CanRemoveMarketServiceForRealmRemoval(System, Body, Marker,
				out Failure)) return false;
			return TryClearMarketServiceFields(Body, Marker, out Failure);
		}

		internal static bool CanCompleteTransferredMarketService(KingdomSystem System,
			GameObject Body, r_KingdomOfficeProjection Marker, out string Failure)
		{
			return CanRemoveMarketServiceForRealmRemoval(System, Body, Marker, out Failure);
		}

		internal static bool TryCompleteTransferredMarketService(KingdomSystem System,
			GameObject Body, r_KingdomOfficeProjection Marker, out string Failure)
		{
			bool pendingStock = KingdomMarketStockCustody.HasExactLocalCustody(System,
				Marker?.SettlementId, Body);
			if (!TryRemoveMarketServiceForRealmRemoval(System, Body, Marker, out Failure))
				return false;
			if (!pendingStock) return true;
			Failure = "office authority retired; exact stock awaits physical detachment";
			return false;
		}

		private static bool TryClearMarketServiceFields(GameObject Body,
			r_KingdomOfficeProjection Marker, out string Failure)
		{
			Failure = null;
			if (Marker == null || Marker.MarketServicePhase == 0) return true;
			Body.RemoveIntProperty("Merchant");
			Body.RemoveIntProperty("VillageMerchant");
			Body.RemoveIntProperty("InventoryTier");
			if (Marker.OwnsMarketRestocker)
			{
				GenericInventoryRestocker restocker = Body.GetPart<GenericInventoryRestocker>();
				if (restocker != null) Body.RemovePart(restocker);
			}
			if (Body.HasIntProperty("Merchant") || Body.HasIntProperty("VillageMerchant")
				|| Body.HasIntProperty("InventoryTier")
				|| Body.GetPart<GenericInventoryRestocker>() != null)
			{
				Failure = "owned market fields or restocker resisted exact removal"; return false;
			}
			Marker.MarketServicePhase = 0;
			Marker.MarketTier = 0;
			Marker.OwnsMarketRestocker = false;
			return Marker.MarketServicePhase == 0 && Marker.MarketTier == 0
				&& !Marker.OwnsMarketRestocker;
		}

		private static bool KnownMarketServiceState(GameObject Body,
			r_KingdomOfficeProjection Marker)
		{
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			return GameObject.Validate(Body) && Marker != null
				&& Marker.MarketServicePhase == 2
				&& Body.HasIntProperty("Merchant") && Body.GetIntProperty("Merchant") == 1
				&& Body.HasIntProperty("VillageMerchant")
				&& Body.GetIntProperty("VillageMerchant") == 1
				&& Body.HasIntProperty("InventoryTier")
				&& Body.GetIntProperty("InventoryTier") == Marker.MarketTier
				&& !Marker.OwnsMarketRestocker
				&& Body.GetPart<GenericInventoryRestocker>() == null
				&& KingdomMarketProviderAuthority.TryProveProjection(system, Body,
					Marker.MarketTier, out string _);
		}

		private static bool RecoverableOwnedInt(GameObject Body, string Name, int Expected)
		{
			return !Body.HasIntProperty(Name) || Body.GetIntProperty(Name) == Expected;
		}

		private static bool OwnedIntCanBeRemoved(GameObject Body, string Name, int Expected)
		{
			return !Body.HasIntProperty(Name) || Body.GetIntProperty(Name) == Expected
				|| (Name != "InventoryTier" && Body.GetIntProperty(Name) == 0);
		}

		internal static bool SealedFiniteRestocker(GenericInventoryRestocker Restocker)
		{
			return Restocker != null && Restocker.Chance == 0
				&& Restocker.RestockFrequency == long.MaxValue
				&& Restocker.LastRestockTick > 0
				&& (Restocker.Tables == null || Restocker.Tables.Count == 0)
				&& (Restocker.HeroTables == null || Restocker.HeroTables.Count == 0);
		}

		private static int BoundedMarketTier(int Tier)
		{
			if (Tier < 1) return 1;
			return Tier > KingdomShopStockRules.MaximumTier
				? KingdomShopStockRules.MaximumTier : Tier;
		}

		private static bool HasExplicitLegendaryMarket(KingdomSystem System, Zone Zone)
		{
			if (System == null || Zone == null) return false;
			foreach (GameObject body in KingdomSurvey.ObjectsFor(Zone))
				if (IsExplicitLegendaryMarket(body)
					&& KingdomCitizenship.BelongsTo(System, body)) return true;
			return false;
		}
	}
}
