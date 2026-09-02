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
}
