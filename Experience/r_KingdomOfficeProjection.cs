using System;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>Object-local proof that one exact SocialRoles string came from one office row. A
	/// true clone copies this marker but not its frozen body id, making clone cleanup source-owned.</summary>
	[Serializable]
	public sealed class r_KingdomOfficeProjection : IPart
	{
		public string RealmId = "";
		public string SettlementId = "";
		public int Generation;
		public int ResidentId;
		public string BodyObjectId = "";
		public string RoleText = "";
		public bool OwnsRole;

		/// <summary>0 is title-only, 1 is a recoverable market projection intent, and 2 is the
		/// complete finite-market service. These fields extend the existing exact-body receipt so
		/// old saves remain title-only until an attended pass proves every shop prerequisite.</summary>
		public int MarketServicePhase;
		public int MarketTier;
		public bool OwnsMarketRestocker;

		/// <summary>Durable quarantine after this exact holder's terminal resident row proved
		/// death but title/market cleanup could not finish. It grants no office or market authority;
		/// the exact body keeps this residue until a later non-admitting cleanup can prove safety.</summary>
		public bool DeathResidue;

		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade)
				|| ID == PooledEvent<AllowTradeWithNoInventoryEvent>.ID
				|| ID == TookEvent.ID;
		}

		/// <summary>Vanilla normally refuses an empty trader. Exact active office service opens
		/// the screen so its first wares can enter through the same physical trade transaction.</summary>
		public override bool HandleEvent(AllowTradeWithNoInventoryEvent E)
		{
			if (ReferenceEquals(E.Trader, ParentObject)
				&& KingdomMarketStockCustody.TryActiveOffice(ParentObject, this,
					out KingdomSystem system, out string _)
				&& KingdomMarketProviderAuthority.TryProve(system, ParentObject,
					MarketTier, out string _)) return false;
			return base.HandleEvent(E);
		}

		/// <summary>TradeUI sets <c>_stock</c> before <c>TakeObject</c>; TookEvent therefore binds
		/// a receipt to the exact object after native custody has already moved.</summary>
		public override bool HandleEvent(TookEvent E)
		{
			string failure = null;
			if (ReferenceEquals(E.Actor, ParentObject) && E.Item?.GetIntProperty("_stock") == 1
				&& (!KingdomMarketStockCustody.TryActiveOffice(ParentObject, this,
					out KingdomSystem system, out string _)
					|| !KingdomMarketProviderAuthority.TryProve(system, ParentObject,
						MarketTier, out failure)
					|| !KingdomMarketStockCustody.TryAdmitNativeTrade(ParentObject, this,
						E.Item, out failure)))
				KingdomLog.Log("market stock receipt waits (" + (failure ?? "inactive office") + ")");
			return base.HandleEvent(E);
		}

		public bool Matches(KingdomSystem System, KingdomCivicOfficeReceipt Receipt,
			GameObject Body)
		{
			return System != null && Receipt != null && Body != null
				&& !DeathResidue
				&& RealmId == System.RealmId && SettlementId == Receipt.SettlementId
				&& Generation == Receipt.Generation && ResidentId == Receipt.HolderResidentId
				&& BodyObjectId == Receipt.HolderObjectId
				&& Body.IDIfAssigned == BodyObjectId
				&& RoleText == KingdomOfficeRuntime.RoleFor(Receipt)
				&& OwnsRole == Receipt.OwnsRole;
		}
	}
}
