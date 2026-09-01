using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public enum KingdomShopStockVerdict
	{
		RefusedMalformed = 0,
		RefusedNoMerchant = 1,
		RefusedAmbiguousMerchant = 2,
		AlreadyAcknowledged = 3,
		AlreadyIssued = AlreadyAcknowledged,
		Acknowledge = 4,
		Issue = Acknowledge,
		RefusedActiveStockAuthority = 5
	}

	public enum KingdomMarketStockLocation
	{
		Malformed = 0,
		ReceiptedKeeper = 1,
		ReceiptedTransfer = 2,
		Detached = 3,
		OutsideObservedGround = 4
	}

	public enum KingdomMarketAccessionAuthority
	{
		None = 0,
		Legendary = 1,
		Office = 2,
		RefusedCompetingOwners = 3,
		RefusedOfficeWithoutReceipt = 4,
		RefusedOrphanedStock = 5
	}

	/// <summary>Pure law for a local, finite market. ShopTier records service reach only. Stock is
	/// always a physical object already in the exact merchant inventory or later moved there by
	/// Qud's native trade transaction; no tier transition may create, replace, or delete it.</summary>
	public static class KingdomShopStockRules
	{
		public const int MaximumTier = 8;
		public const int FirstPhysicalMarketTier = 3;
		public const int MaximumCustodyRows = 1024;
		public const string StockReceiptProperty = "TAFLocalMarketStockReceipt";
		public const string StockRealmProperty = "TAFLocalMarketStockRealmId";
		public const string LegacyStockRealmProperty = "TAFLocalMarketStockRealm";
		public const string StockSettlementProperty = "TAFLocalMarketStockSettlement";
		public const string StockCustodianProperty = "TAFLocalMarketStockCustodian";
		public const string StockTransferTargetProperty = "TAFLocalMarketStockTransferTarget";
		public const string StockOwnsNoRestockProperty = "TAFLocalMarketOwnsNoRestock";
		public const string StockOwnsNeverStackProperty = "TAFLocalMarketOwnsNeverStack";
		// Frozen v1 output names remain readable for old saves and the legendary-trader handoff.
		// Current code never writes a new output intent or stamps an item with these properties.
		public const string IssueIntentProperty = "TAFLocalMarketOutputIntent";
		public const string ItemSourceProperty = "TAFLocalMarketOutputSource";
		public const string ItemSettlementProperty = "TAFLocalMarketOutputSettlement";
		public const string ItemTierProperty = "TAFLocalMarketOutputTier";

		/// <summary>A market is operational only while one accepted physical capability and one
		/// exact, explicitly held civic-office receipt remain live. Stage alone grants nothing.</summary>
		public static bool OfficeServiceEligible(GrowthStage Stage,
			KingdomCivicOfficePhase Phase, bool ExactHolder, bool ExactProjection,
			bool LiveMarketCapability, int ServiceTier)
		{
			return Stage >= GrowthStage.Village && Phase == KingdomCivicOfficePhase.Held
				&& ExactHolder && ExactProjection && LiveMarketCapability
				&& ServiceTier >= FirstPhysicalMarketTier && ServiceTier <= MaximumTier;
		}

		/// <summary>Physical market fixture opens tier 3. Authored craft knowledge and a market
		/// district can improve service, but growth is only a ceiling and can never improve it.</summary>
		public static int EffectiveServiceTier(GrowthStage Stage, int CraftLevel,
			bool LiveMarketCapability, bool MarketDistrict)
		{
			if (!LiveMarketCapability || Stage < GrowthStage.Village
				|| CraftLevel < 0 || CraftLevel > 4) return 0;
			int district = MarketDistrict ? 1 : 0;
			int ceiling = Stage == GrowthStage.Village ? 3
				: Stage == GrowthStage.Town ? 5 : 7;
			ceiling = Math.Min(MaximumTier, ceiling + district);
			int capability = FirstPhysicalMarketTier + CraftLevel + district;
			return Math.Min(ceiling, Math.Min(MaximumTier, capability));
		}

		public static KingdomShopStockVerdict Classify(int IssuedTier, int RequestedTier,
			int ExactMerchantCount)
		{
			return Classify(IssuedTier, RequestedTier, ExactMerchantCount,
				NoActiveStockAuthority: true);
		}

		public static KingdomShopStockVerdict Classify(int AcknowledgedTier, int RequestedTier,
			int ExactMerchantCount, bool NoActiveStockAuthority)
		{
			if (AcknowledgedTier < 0 || AcknowledgedTier > MaximumTier || RequestedTier < 1 ||
				RequestedTier > MaximumTier) return KingdomShopStockVerdict.RefusedMalformed;
			if (ExactMerchantCount < 1) return KingdomShopStockVerdict.RefusedNoMerchant;
			if (ExactMerchantCount > 1)
				return KingdomShopStockVerdict.RefusedAmbiguousMerchant;
			if (!NoActiveStockAuthority)
				return KingdomShopStockVerdict.RefusedActiveStockAuthority;
			return RequestedTier <= AcknowledgedTier
				? KingdomShopStockVerdict.AlreadyAcknowledged
				: KingdomShopStockVerdict.Acknowledge;
		}

		public static int NextAcknowledgementTier(int AcknowledgedTier, int AttainedTier)
		{
			// Compatibility surface. A first physical bazaar may open at tier 3 or later; lower
			// tiers were never operating markets and must not be replayed as invented history.
			return AttainedTier;
		}

		public static int NextIssueTier(int IssuedTier, int AttainedTier)
		{
			return NextAcknowledgementTier(IssuedTier, AttainedTier);
		}

		public static string SourceId(string RealmId, string SettlementId, int Tier)
		{
			return ReceiptId("taf:local-market-output:v1:", RealmId, SettlementId, Tier);
		}

		public static string TierReceiptId(string RealmId, string SettlementId, int Tier)
		{
			return ReceiptId("taf:market-service-tier:v2:", RealmId, SettlementId, Tier);
		}

		public static string StockReceiptId(string RealmId, string SettlementId, string ItemId)
		{
			if (!ValidId(RealmId) || !ValidId(SettlementId) || !ValidId(ItemId)) return null;
			return ReceiptId("taf:market-stock:v1:", RealmId, SettlementId, ItemId);
		}

		/// <summary>Receipt is exact only for one realm, settlement, physical object, and current
		/// direct custodian. Copied properties on a clone therefore authorize nothing.</summary>
		public static bool ExactStockCustody(string Receipt, string HeldRealm,
			string HeldSettlement, string HeldCustodian, string RealmId,
			string SettlementId, string CustodianId, string ItemId)
		{
			string expected = StockReceiptId(RealmId, SettlementId, ItemId);
			return expected != null && Receipt == expected && HeldRealm == RealmId
				&& HeldSettlement == SettlementId && HeldCustodian == CustodianId;
		}

		public static bool TryResolveStockRealm(string Current, string Legacy, out string Realm)
		{
			Current = string.IsNullOrEmpty(Current) ? null : Current;
			Legacy = string.IsNullOrEmpty(Legacy) ? null : Legacy;
			Realm = Current ?? Legacy;
			return (Current == null || Legacy == null || Current == Legacy) && ValidId(Realm);
		}

		/// <summary>Only direct keeper custody or an explicit transfer target is continuity.
		/// Ground, player, ordinary-container, and foreign-body custody is detached.</summary>
		public static KingdomMarketStockLocation ClassifyLocation(bool OnGround,
			bool HasHolder, bool HolderIsCustodian, bool HolderIsTransferTarget,
			bool HolderOnObservedGround)
		{
			if (HasHolder)
			{
				if (HolderIsCustodian) return KingdomMarketStockLocation.ReceiptedKeeper;
				if (HolderIsTransferTarget) return KingdomMarketStockLocation.ReceiptedTransfer;
				return HolderOnObservedGround ? KingdomMarketStockLocation.Detached
					: KingdomMarketStockLocation.OutsideObservedGround;
			}
			if (OnGround) return KingdomMarketStockLocation.Detached;
			return HolderIsCustodian || HolderIsTransferTarget
				? KingdomMarketStockLocation.Malformed
				: KingdomMarketStockLocation.OutsideObservedGround;
		}

		/// <summary>Accession must prove one civic owner before mutating either authority.
		/// A prepared office receipt may outlive its body projection during an exact retry.</summary>
		public static KingdomMarketAccessionAuthority ClassifyAccessionAuthority(
			bool HasLegendaryMarker, bool HasOfficeMarker, bool HasOfficeReceipt,
			bool HasDirectStockProjection)
		{
			if (HasLegendaryMarker && (HasOfficeMarker || HasOfficeReceipt))
				return KingdomMarketAccessionAuthority.RefusedCompetingOwners;
			if (HasLegendaryMarker) return KingdomMarketAccessionAuthority.Legendary;
			if (HasOfficeReceipt) return KingdomMarketAccessionAuthority.Office;
			if (HasOfficeMarker)
				return KingdomMarketAccessionAuthority.RefusedOfficeWithoutReceipt;
			return HasDirectStockProjection
				? KingdomMarketAccessionAuthority.RefusedOrphanedStock
				: KingdomMarketAccessionAuthority.None;
		}

		public static bool IsCurrentLegendaryCivicAuthority(bool HasVillageProjection,
			bool HasShopkeeper, string CurrentSettlementId, string MarkerSettlementId,
			int CurrentShopTier, int BodyTier)
		{
			return HasVillageProjection && HasShopkeeper
				&& !string.IsNullOrEmpty(CurrentSettlementId)
				&& CurrentSettlementId == MarkerSettlementId
				&& CurrentShopTier >= FirstPhysicalMarketTier
				&& CurrentShopTier == BodyTier;
		}

		public static bool MayStartMarketHandoff(bool DeathSelectionInProgress,
			bool HasPendingDeath, int PendingAccessionRepairResidentId)
		{
			return !DeathSelectionInProgress && !HasPendingDeath
				&& PendingAccessionRepairResidentId == 0;
		}

		/// <summary>Project a native protection only when the market already owns that slot or
		/// the item had no prior authority. A foreign pre-existing protection is never claimed.</summary>
		public static bool ShouldOwnProtection(bool AlreadyPresent, bool MarketAlreadyOwns)
		{
			return MarketAlreadyOwns || !AlreadyPresent;
		}

		/// <summary>Reference identity, not blueprint/value equality, is the conservation unit.
		/// Reordering is harmless; a null, duplicate reference, clone, loss, or replacement fails.</summary>
		public static bool SamePhysicalSet<T>(IList<T> Before, IList<T> After) where T : class
		{
			if (Before == null || After == null || Before.Count != After.Count) return false;
			for (int i = 0; i < Before.Count; i++)
			{
				T item = Before[i];
				if (item == null || ReferenceCount(Before, item) != 1
					|| ReferenceCount(After, item) != 1) return false;
			}
			for (int i = 0; i < After.Count; i++)
				if (After[i] == null || ReferenceCount(After, After[i]) != 1) return false;
			return true;
		}

		private static int ReferenceCount<T>(IList<T> Values, T Item) where T : class
		{
			int count = 0;
			for (int i = 0; i < Values.Count; i++)
				if (ReferenceEquals(Values[i], Item)) count++;
			return count;
		}

		private static string ReceiptId(string Domain, string RealmId,
			string SettlementId, int Tier)
		{
			if (!ValidId(RealmId) || !ValidId(SettlementId) ||
				Tier < 1 || Tier > MaximumTier) return null;
			using (SHA256 sha = SHA256.Create())
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
			{
				writer.Write(RealmId); writer.Write(SettlementId); writer.Write(Tier); writer.Flush();
				byte[] hash = sha.ComputeHash(stream.ToArray());
				StringBuilder result = new StringBuilder(Domain);
				for (int i = 0; i < hash.Length; i++) result.Append(hash[i].ToString("x2"));
				return result.ToString();
			}
		}

		private static string ReceiptId(string Domain, string RealmId,
			string SettlementId, string ItemId)
		{
			using (SHA256 sha = SHA256.Create())
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
			{
				writer.Write(RealmId); writer.Write(SettlementId); writer.Write(ItemId); writer.Flush();
				byte[] hash = sha.ComputeHash(stream.ToArray());
				StringBuilder result = new StringBuilder(Domain);
				for (int i = 0; i < hash.Length; i++) result.Append(hash[i].ToString("x2"));
				return result.ToString();
			}
		}

		public static string IntentReceipt(string SourceId)
		{
			return string.IsNullOrEmpty(SourceId) ? null : SourceId + ":intent";
		}

		private static bool ValidId(string value)
		{
			if (string.IsNullOrEmpty(value) || value.Length > 128 || value.Trim() != value) return false;
			try { return new UTF8Encoding(false, true).GetByteCount(value) <= 384; }
			catch (EncoderFallbackException) { return false; }
		}
	}
}
