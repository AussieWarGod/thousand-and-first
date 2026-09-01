#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	public class KingdomShopStockRulesTests
	{
		[TestCase(true, 3, 3, 3, true)]
		[TestCase(false, 3, 3, 3, false)]
		[TestCase(true, 4, 3, 3, false)]
		[TestCase(true, 3, 4, 3, false)]
		[TestCase(true, 3, 3, 4, false)]
		[TestCase(true, 2, 2, 2, false)]
		[TestCase(true, 9, 9, 9, false)]
		public void IngressReproofRequiresOneExactLiveTier(bool live, int observed,
			int projected, int recorded, bool expected)
		{
			Assert.AreEqual(expected, KingdomMarketProviderRules.ExactLiveAuthority(
				live, observed, projected, recorded));
		}

		[TestCase(true, 3, 3, true)]
		[TestCase(false, 3, 3, false)]
		[TestCase(true, 4, 3, false)]
		public void ReconciliationReproofDoesNotRequirePreviouslyRecordedTier(bool live,
			int observed, int projected, bool expected)
		{
			Assert.AreEqual(expected, KingdomMarketProviderRules.ExactLiveProjection(
				live, observed, projected));
		}

		[TestCase(-1, 1, 1)]
		[TestCase(0, 0, 1)]
		[TestCase(0, 9, 1)]
		public void MalformedTierRefuses(int acknowledged, int requested, int merchants)
		{
			Assert.AreEqual(KingdomShopStockVerdict.RefusedMalformed,
				KingdomShopStockRules.Classify(acknowledged, requested, merchants));
		}

		[Test]
		public void OneMerchantNoMintAuthorityAndNewStandingAreRequired()
		{
			Assert.AreEqual(KingdomShopStockVerdict.RefusedNoMerchant,
				KingdomShopStockRules.Classify(0, 3, 0));
			Assert.AreEqual(KingdomShopStockVerdict.RefusedAmbiguousMerchant,
				KingdomShopStockRules.Classify(0, 3, 2));
			Assert.AreEqual(KingdomShopStockVerdict.RefusedActiveStockAuthority,
				KingdomShopStockRules.Classify(0, 3, 1, false));
			Assert.AreEqual(KingdomShopStockVerdict.Acknowledge,
				KingdomShopStockRules.Classify(0, 3, 1, true));
			Assert.AreEqual(KingdomShopStockVerdict.AlreadyAcknowledged,
				KingdomShopStockRules.Classify(3, 3, 1, true));
		}

		[TestCase(GrowthStage.Steading, KingdomCivicOfficePhase.Held, true, true, true, 3, false)]
		[TestCase(GrowthStage.Village, KingdomCivicOfficePhase.Vacant, true, true, true, 3, false)]
		[TestCase(GrowthStage.Village, KingdomCivicOfficePhase.Held, false, true, true, 3, false)]
		[TestCase(GrowthStage.Village, KingdomCivicOfficePhase.Held, true, false, true, 3, false)]
		[TestCase(GrowthStage.Village, KingdomCivicOfficePhase.Held, true, true, false, 3, false)]
		[TestCase(GrowthStage.Village, KingdomCivicOfficePhase.Held, true, true, true, 0, false)]
		[TestCase(GrowthStage.Village, KingdomCivicOfficePhase.Held, true, true, true, 3, true)]
		public void ServiceRequiresOfficeAndLivePhysicalCapability(GrowthStage stage,
			KingdomCivicOfficePhase phase, bool holder, bool projection, bool capability,
			int tier, bool expected)
		{
			Assert.AreEqual(expected, KingdomShopStockRules.OfficeServiceEligible(stage,
				phase, holder, projection, capability, tier));
		}

		[TestCase(GrowthStage.City, 4, false, true, 0)]
		[TestCase(GrowthStage.Steading, 4, true, true, 0)]
		[TestCase(GrowthStage.Village, 0, true, false, 3)]
		[TestCase(GrowthStage.Town, 0, true, false, 3)]
		[TestCase(GrowthStage.City, 0, true, false, 3)]
		[TestCase(GrowthStage.Town, 1, true, false, 4)]
		[TestCase(GrowthStage.Town, 4, true, false, 5)]
		[TestCase(GrowthStage.Town, 0, true, true, 4)]
		[TestCase(GrowthStage.Town, 1, true, true, 5)]
		[TestCase(GrowthStage.City, 4, true, true, 8)]
		public void StandingNeedsCapabilityAndCannotOutrunCraftOrGrowth(GrowthStage stage,
			int tech, bool capability, bool district, int expected)
		{
			Assert.AreEqual(expected, KingdomShopStockRules.EffectiveServiceTier(
				stage, tech, capability, district));
		}

		[Test]
		public void FirstLateMarketAcknowledgesAttainedStandingDirectly()
		{
			Assert.AreEqual(3, KingdomShopStockRules.NextAcknowledgementTier(0, 3));
			Assert.AreEqual(7, KingdomShopStockRules.NextAcknowledgementTier(0, 7));
			Assert.AreEqual(7, KingdomShopStockRules.NextAcknowledgementTier(3, 7));
			Assert.AreEqual(3, KingdomShopStockRules.NextAcknowledgementTier(7, 3));
		}

		[Test]
		public void ReceiptNamesOneRealmSettlementAndPhysicalObject()
		{
			string receipt = KingdomShopStockRules.StockReceiptId("realm-1", "city-2", "item-3");
			StringAssert.StartsWith("taf:market-stock:v1:", receipt);
			Assert.AreEqual(receipt,
				KingdomShopStockRules.StockReceiptId("realm-1", "city-2", "item-3"));
			Assert.AreNotEqual(receipt,
				KingdomShopStockRules.StockReceiptId("realm-1", "city-2", "clone-3"));
			Assert.IsTrue(KingdomShopStockRules.ExactStockCustody(receipt,
				"realm-1", "city-2", "keeper-4", "realm-1", "city-2", "keeper-4", "item-3"));
			Assert.IsFalse(KingdomShopStockRules.ExactStockCustody(receipt,
				"realm-1", "city-2", "keeper-4", "realm-1", "city-2", "clone-4", "item-3"));
			Assert.IsFalse(KingdomShopStockRules.ExactStockCustody(receipt,
				"realm-1", "city-2", "keeper-4", "realm-1", "city-2", "keeper-4", "clone-3"));
		}

		[TestCase("realm-1", null, true, "realm-1")]
		[TestCase(null, "realm-1", true, "realm-1")]
		[TestCase("realm-1", "realm-1", true, "realm-1")]
		[TestCase("realm-1", "realm-2", false, "realm-1")]
		[TestCase(null, null, false, null)]
		public void CurrentAndLegacyRealmMarkersResolveOnlyWhenExact(string current,
			string legacy, bool expected, string realm)
		{
			Assert.AreEqual(expected, KingdomShopStockRules.TryResolveStockRealm(
				current, legacy, out string actual));
			Assert.AreEqual(realm, actual);
		}

		[Test]
		public void ConservationUsesReferenceIdentityAndRejectsCloneOrDuplicate()
		{
			object a = new object(); object b = new object(); object clone = new object();
			Assert.IsTrue(KingdomShopStockRules.SamePhysicalSet(
				new List<object> { a, b }, new List<object> { b, a }));
			Assert.IsFalse(KingdomShopStockRules.SamePhysicalSet(
				new List<object> { a, b }, new List<object> { a, clone }));
			Assert.IsFalse(KingdomShopStockRules.SamePhysicalSet(
				new List<object> { a, b }, new List<object> { a, a }));
		}

		[TestCase(false, false, true)]
		[TestCase(true, false, false)]
		[TestCase(false, true, true)]
		[TestCase(true, true, true)]
		public void ProtectionOwnershipNeverClaimsForeignState(bool present,
			bool alreadyOwned, bool expected)
		{
			Assert.AreEqual(expected, KingdomShopStockRules.ShouldOwnProtection(
				present, alreadyOwned));
		}

		[TestCase(true, false, false, false, false, KingdomMarketStockLocation.Detached,
			TestName = "Ground_root_is_detached")]
		[TestCase(false, true, false, false, true, KingdomMarketStockLocation.Detached,
			TestName = "Player_inventory_is_detached")]
		[TestCase(false, true, false, false, true, KingdomMarketStockLocation.Detached,
			TestName = "Ordinary_container_is_detached")]
		[TestCase(false, true, false, false, true, KingdomMarketStockLocation.Detached,
			TestName = "Foreign_body_is_detached")]
		[TestCase(false, true, true, false, true, KingdomMarketStockLocation.ReceiptedKeeper)]
		[TestCase(false, true, false, true, true, KingdomMarketStockLocation.ReceiptedTransfer)]
		public void StockLocationDistinguishesContinuityFromDetachment(bool ground,
			bool holder, bool custodian, bool transfer, bool observed,
			KingdomMarketStockLocation expected)
		{
			Assert.AreEqual(expected, KingdomShopStockRules.ClassifyLocation(
				ground, holder, custodian, transfer, observed));
		}

		[TestCase(true, false, false, true, KingdomMarketAccessionAuthority.Legendary,
			TestName = "Legend_only_owns_stock")]
		[TestCase(false, true, true, true, KingdomMarketAccessionAuthority.Office,
			TestName = "Office_part_and_receipt_own_stock")]
		[TestCase(true, true, true, false,
			KingdomMarketAccessionAuthority.RefusedCompetingOwners,
			TestName = "Legend_and_office_part_refuse")]
		[TestCase(true, false, true, false,
			KingdomMarketAccessionAuthority.RefusedCompetingOwners,
			TestName = "Legend_and_office_receipt_refuse")]
		[TestCase(false, false, false, true,
			KingdomMarketAccessionAuthority.RefusedOrphanedStock,
			TestName = "Markerless_stock_refuses")]
		[TestCase(false, true, true, false, KingdomMarketAccessionAuthority.Office,
			TestName = "Title_only_office_is_recoverable")]
		[TestCase(false, false, true, false, KingdomMarketAccessionAuthority.Office,
			TestName = "Prepared_office_retry_is_recoverable")]
		[TestCase(false, false, false, false, KingdomMarketAccessionAuthority.None,
			TestName = "Legend_commit_retry_is_clean")]
		public void AccessionOwnerShapePreflightsBeforeMutation(bool legend, bool office,
			bool receipt, bool stock, KingdomMarketAccessionAuthority expected)
		{
			Assert.AreEqual(expected, KingdomShopStockRules.ClassifyAccessionAuthority(
				legend, office, receipt, stock));
		}

		[TestCase("city-a", "city-a", true)]
		[TestCase("city-a", "city-b", false)]
		public void LegendaryCivicAuthorityIsCurrentSettlementOnly(string current,
			string marker, bool expected)
		{
			Assert.AreEqual(expected,
				KingdomShopStockRules.IsCurrentLegendaryCivicAuthority(true, true,
					current, marker, 4, 4));
			Assert.IsFalse(KingdomShopStockRules.IsCurrentLegendaryCivicAuthority(
				true, true, current, marker, 4, 3));
		}

		[TestCase(false, false, 0, true)]
		[TestCase(true, false, 0, false)]
		[TestCase(false, true, 0, false)]
		[TestCase(false, false, 42, false)]
		public void HandoffStartWaitsForEverySuccessionAuthority(bool selecting,
			bool pendingDeath, int repairResident, bool expected)
		{
			Assert.AreEqual(expected, KingdomShopStockRules.MayStartMarketHandoff(
				selecting, pendingDeath, repairResident));
		}

		[TestCase("realm-a", "realm-a", true, true)]
		[TestCase("realm-b", "realm-a", true, false)]
		[TestCase("realm-a", "realm-a", false, false)]
		[TestCase("", "realm-a", true, false)]
		public void ItemMovementRetiresOnlyCurrentExactRealm(string current,
			string receipt, bool exact, bool expected)
		{
			Assert.AreEqual(expected, KingdomMarketStockAuthorityRules.MayRetire(
				current, receipt, exact));
		}
	}
}
#endif
