using System;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Item-local lifetime seam for exact native market stock. It prevents Qud's
	/// post-trade stack merge from erasing a newly receipted sale, then removes only TAF-owned
	/// custody/protection as soon as the item enters any non-custodian context.</summary>
	[Serializable]
	public sealed class r_KingdomMarketStockProjection : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == AddedToInventoryEvent.ID
				|| ID == TakenEvent.ID || ID == DroppedEvent.ID
				|| ID == EnteredCellEvent.ID;
		}

		public override bool HandleEvent(AddedToInventoryEvent E)
		{
			RetireUnlessContinuing(); return base.HandleEvent(E);
		}

		public override bool HandleEvent(TakenEvent E)
		{
			RetireUnlessContinuing(); return base.HandleEvent(E);
		}

		public override bool HandleEvent(DroppedEvent E)
		{
			Retire(); return base.HandleEvent(E);
		}

		public override bool HandleEvent(EnteredCellEvent E)
		{
			if (ParentObject?.InInventory == null) Retire();
			return base.HandleEvent(E);
		}

		public override bool CanGenerateStacked() { return false; }
		public override bool SameAs(IPart Part) { return ReferenceEquals(this, Part); }

		public override void FinalizeCopy(GameObject Source, bool CopyEffects, bool CopyID,
			Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			// A copy is a new physical object and cannot inherit the source receipt. The exact
			// native SplitStack postfix may issue fresh authority after proving its lifecycle.
			KingdomMarketStockProtection.TryRetire(ParentObject);
		}

		private void RetireUnlessContinuing()
		{
			if (!Continuing(ParentObject)) Retire();
		}

		private void Retire()
		{
			string marketTarget = ParentObject?.GetStringProperty(
				KingdomGuestbook.MarketTransferTargetProperty);
			string stockTarget = ParentObject?.GetStringProperty(
				KingdomShopStockRules.StockTransferTargetProperty);
			bool linked = !string.IsNullOrEmpty(marketTarget) && marketTarget == stockTarget;
			string failure = null;
			if (GameObject.Validate(ParentObject) && KingdomMarketStockProtection
				.TryRetireCurrent(The.Game?.GetSystem<KingdomSystem>(), ParentObject,
					out failure))
			{
				if (linked) ParentObject.SetStringProperty(
					KingdomGuestbook.MarketTransferTargetProperty, null, RemoveIfNull: true);
			}
			else if (failure != null)
				KingdomLog.Log("market stock receipt preserved (" + failure + ")");
		}

		private static bool Continuing(GameObject Item)
		{
			GameObject holder = Item?.InInventory;
			KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
			if (!GameObject.Validate(Item) || !GameObject.Validate(holder) || !holder.IsAlive
				|| holder.IsPlayer() || Item.GetIntProperty("_stock") != 1 || system == null
				|| !KingdomCitizenship.BelongsTo(system, holder)) return false;
			string currentRealm = Item.GetStringProperty(
				KingdomShopStockRules.StockRealmProperty);
			string legacyRealm = Item.GetStringProperty(
				KingdomShopStockRules.LegacyStockRealmProperty);
			string settlement = Item.GetStringProperty(
				KingdomShopStockRules.StockSettlementProperty);
			string custodian = Item.GetStringProperty(
				KingdomShopStockRules.StockCustodianProperty);
			string holderId = holder.IDIfAssigned;
			r_KingdomOfficeProjection office = holder.GetPart<r_KingdomOfficeProjection>();
			r_KingdomLegendaryMarketProjection legend =
				holder.GetPart<r_KingdomLegendaryMarketProjection>();
			bool activeOffice = office != null && KingdomMarketStockCustody.TryActiveOffice(
				holder, office, out KingdomSystem officeSystem, out string officeSettlement)
				&& ReferenceEquals(officeSystem, system) && officeSettlement == settlement;
			bool activeLegend = legend != null && legend.Active(system, holder)
				&& legend.SettlementId == settlement;
			bool preparedHandoff = r_KingdomLegendaryMarketProjection
				.PreparedTransferAuthority(system, holder, Item);
			if (!activeOffice && !activeLegend && !preparedHandoff) return false;
			if (!KingdomShopStockRules.TryResolveStockRealm(currentRealm, legacyRealm,
				out string realm) || realm != system.RealmId || system.SettlementIdForOwnedZone(
				holder.CurrentZone?.ZoneID) != settlement
				|| !KingdomShopStockRules.ExactStockCustody(
				Item.GetStringProperty(KingdomShopStockRules.StockReceiptProperty), realm,
				settlement, custodian, system.RealmId, settlement, custodian,
				Item.IDIfAssigned)) return false;
			return holderId == custodian || holderId == Item.GetStringProperty(
				KingdomShopStockRules.StockTransferTargetProperty);
		}
	}

	/// <summary>Exact body-side ingress seam for an explicit legendary market. Its sealed native
	/// restocker opens empty trade; only Qud's subsequent physical TookEvent can add stock.</summary>
	[Serializable]
	public sealed partial class r_KingdomLegendaryMarketProjection : IPart
	{
		public string RealmId = "";
		public string SettlementId = "";
		public string BodyObjectId = "";
		public int HandoffPrepared;
		public int MarketTier;
		public string HandoffIntent = "";
		public string PriorBodyObjectId = "";
		public int HandoffResidentId;
		public int PriorResidentId;

		internal void Stamp(KingdomSystem System, string Settlement, GameObject Body)
		{
			RealmId = System?.RealmId ?? "";
			SettlementId = Settlement ?? "";
			BodyObjectId = Body?.ID ?? "";
		}

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == TookEvent.ID
				|| ID == PooledEvent<AllowTradeWithNoInventoryEvent>.ID;
		}

		public override bool HandleEvent(TookEvent E)
		{
			if (ReferenceEquals(E.Actor, ParentObject) && E.Item?.GetIntProperty("_stock") == 1)
			{
				KingdomSystem system = The.Game?.GetSystem<KingdomSystem>();
				string failure = null;
				if (!Active(system, ParentObject) || !KingdomMarketStockCustody.TryBind(system,
					SettlementId, ParentObject, E.Item, true, out failure))
					KingdomLog.Log("legendary market stock receipt waits ("
						+ (failure ?? "inactive legendary market") + ")");
			}
			return base.HandleEvent(E);
		}

		internal bool Active(KingdomSystem System, GameObject Body)
		{
			return HandoffPrepared == 0 && System != null && System.HasShopkeeper
				&& System.ShopTier >= KingdomShopStockRules.FirstPhysicalMarketTier
				&& Prepared(System, Body, System.ShopTier)
				&& KingdomMarketProviderAuthority.TryProveLegendary(System, Body,
					System.ShopTier, out _);
		}

		internal bool Prepared(KingdomSystem System, GameObject Body, int Tier)
		{
			return PreparedIdentity(System, Body, Tier)
				&& Body.HasIntProperty("Merchant") && Body.GetIntProperty("Merchant") == 1
				&& Body.GetIntProperty("VillageMerchant") == 1;
		}

		private bool PreparedIdentity(KingdomSystem System, GameObject Body, int Tier)
		{
			return PreparedIdentity(System, Body, Tier, RequireAlive: true);
		}

		private bool PreparedIdentity(KingdomSystem System, GameObject Body, int Tier,
			bool RequireAlive)
		{
			return System != null && GameObject.Validate(Body) && (!RequireAlive || Body.IsAlive)
				&& !Body.IsPlayer()
				&& RealmId == System.RealmId && !string.IsNullOrEmpty(SettlementId)
				&& Tier >= KingdomShopStockRules.FirstPhysicalMarketTier
				&& Tier <= KingdomShopStockRules.MaximumTier && BodyObjectId == Body.IDIfAssigned
				&& System.SettlementIdForOwnedZone(Body.CurrentZone?.ZoneID) == SettlementId
				&& KingdomCitizenship.BelongsTo(System, Body)
				&& Body.GetIntProperty(KingdomGuestbook.LegendaryTraderResidentProperty) == 1
				&& Body.IsMerchant() && Body.GetIntProperty("InventoryTier") == Tier;
		}

		internal bool StampPrepared(KingdomSystem System, string Settlement,
			GameObject Body, int Tier, string Intent, string PriorId,
			int ResidentId, int FrozenPriorResidentId)
		{
			if (ResidentId <= 0 || FrozenPriorResidentId <= 0) return false;
			bool baseBlank = string.IsNullOrEmpty(RealmId)
				&& string.IsNullOrEmpty(SettlementId) && string.IsNullOrEmpty(BodyObjectId);
			bool baseExact = RealmId == System?.RealmId && SettlementId == Settlement
				&& BodyObjectId == Body?.IDIfAssigned;
			if (!baseBlank && !baseExact) return false;
			bool blank = HandoffPrepared == 0 && string.IsNullOrEmpty(HandoffIntent)
				&& string.IsNullOrEmpty(PriorBodyObjectId) && HandoffResidentId == 0
				&& PriorResidentId == 0;
			if (!blank && (HandoffPrepared != 1 || HandoffIntent != Intent
				|| PriorBodyObjectId != PriorId || MarketTier != Tier
				|| HandoffResidentId != ResidentId
				|| PriorResidentId != FrozenPriorResidentId)) return false;
			Stamp(System, Settlement, Body); HandoffPrepared = 1; MarketTier = Tier;
			HandoffIntent = Intent ?? ""; PriorBodyObjectId = PriorId ?? "";
			HandoffResidentId = ResidentId; PriorResidentId = FrozenPriorResidentId;
			return ExactPreparedBody(System, Body);
		}

		internal bool CompleteHandoff()
		{
			HandoffPrepared = 0; MarketTier = 0; HandoffIntent = "";
			PriorBodyObjectId = ""; HandoffResidentId = 0; PriorResidentId = 0;
			return true;
		}

		internal static bool PreparedTransferAuthority(KingdomSystem System,
			GameObject Holder, GameObject Item)
		{
			string targetId = Item?.GetStringProperty(
				KingdomShopStockRules.StockTransferTargetProperty);
			GameObject target = string.IsNullOrEmpty(targetId) ? null : GameObject.FindByID(targetId);
			r_KingdomLegendaryMarketProjection marker =
				target?.GetPart<r_KingdomLegendaryMarketProjection>();
			return marker != null && marker.ExactPreparedBody(System, target)
				&& ReferenceEquals(Item?.InInventory, Holder)
				&& (Holder?.IDIfAssigned == marker.BodyObjectId
					|| Holder?.IDIfAssigned == marker.PriorBodyObjectId)
				&& Item.GetIntProperty("_stock") == 1
				&& KingdomShopStockRules.TryResolveStockRealm(Item.GetStringProperty(
					KingdomShopStockRules.StockRealmProperty), Item.GetStringProperty(
					KingdomShopStockRules.LegacyStockRealmProperty), out string realm)
				&& realm == marker.RealmId
				&& Item.GetStringProperty(KingdomShopStockRules.StockSettlementProperty)
					== marker.SettlementId
				&& Item.GetStringProperty(KingdomShopStockRules.StockCustodianProperty)
					== marker.PriorBodyObjectId
				&& Item.GetStringProperty(KingdomShopStockRules.StockReceiptProperty)
					== KingdomShopStockRules.StockReceiptId(realm, marker.SettlementId,
						Item.IDIfAssigned);
		}

		private bool ExactPreparedBody(KingdomSystem System, GameObject Body)
		{
			string expected = KingdomShopStockRules.SourceId(System?.RealmId,
				SettlementId, MarketTier) + ":handoff:" + PriorBodyObjectId + ":" + BodyObjectId;
			return HandoffPrepared == 1 && HandoffResidentId > 0 && PriorResidentId > 0
				&& Simulation.City.KingdomResidents.IdOf(Body) == HandoffResidentId
				&& PreparedIdentity(System, Body, MarketTier)
				&& HandoffIntent == expected
				&& Body.GetStringProperty(KingdomGuestbook.MarketHandoffIntentProperty)
					== HandoffIntent
				&& Body.GetStringProperty(KingdomGuestbook.MarketHandoffPriorProperty)
					== PriorBodyObjectId
				&& KingdomGrowth.SealedFiniteRestocker(
					Body.GetPart<XRL.World.Parts.GenericInventoryRestocker>());
		}

		internal bool MatchesPreparedHandoff(KingdomSystem System, GameObject Body,
			int Tier, string Intent, string PriorId)
		{
			return MarketTier == Tier && HandoffIntent == Intent
				&& PriorBodyObjectId == PriorId && ExactPreparedBody(System, Body);
		}

		public override void FinalizeCopy(GameObject Source, bool CopyEffects,
			bool CopyID, Func<GameObject, GameObject> MapInv)
		{
			base.FinalizeCopy(Source, CopyEffects, CopyID, MapInv);
			ParentObject?.RemovePart(this);
		}
	}
}
