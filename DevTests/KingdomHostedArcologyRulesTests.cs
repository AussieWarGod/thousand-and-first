#if TAF_TESTS
using System;
using NUnit.Framework;
using ThousandAndFirst;

namespace ThousandAndFirst.Tests
{
	public sealed class KingdomHostedArcologyRulesTests
	{
		[Test]
		public void BuiltInsAreHostedWorkAndCopiesCannotMutateRegistry()
		{
			KingdomHostedLotDefinition ward;
			KingdomHostedLotDefinition terrace;
			Assert.IsTrue(KingdomHostedArcologyRules.TryHostedLot("arcologyward", out ward));
			Assert.IsTrue(KingdomHostedArcologyRules.TryHostedLot("arcologyterrace", out terrace));
			Assert.IsFalse(ward.ReadOnly);
			Assert.AreEqual("roof:26,luxury:2", ward.Supports);
			Assert.IsTrue(terrace.RequiresWater);
			Assert.AreEqual("r_KingdomArcologyGrowbed",
				terrace.PhysicalProducerBlueprint);
			Assert.AreEqual(14, terrace.PhysicalProducerCount);
			ward.DisplayName = "tampered";
			Assert.IsTrue(KingdomHostedArcologyRules.TryHostedLot("arcologyward", out ward));
			Assert.AreNotEqual("tampered", ward.DisplayName);
		}

		[Test]
		public void HostedFoodRequiresBoundedPhysicalProducerContract()
		{
			KingdomHostedLotDefinition missing = new KingdomHostedLotDefinition {
				Key = "test-food-missing-" + Guid.NewGuid().ToString("N"),
				DisplayName = "unbacked food", InteriorCell = "TAFArcologyTerrace",
				MaterialKey = "arcologyterrace", BuildTicks = 1L, Crew = 1,
				Supports = "food:1"
			};
			Assert.IsFalse(KingdomHostedArcologyRules.RegisterHostedLot(missing,
				out string failure));
			StringAssert.Contains("physical producer", failure);
			missing.Key = "test-food-ragged-" + Guid.NewGuid().ToString("N");
			missing.PhysicalProducerBlueprint = "r_KingdomArcologyGrowbed";
			Assert.IsFalse(KingdomHostedArcologyRules.RegisterHostedLot(missing,
				out failure));
			StringAssert.Contains("malformed", failure);
		}

		[Test]
		public void RegistrationRejectsDuplicateAndReadOnlyMutationSurface()
		{
			string key = "test-read-view-" + Guid.NewGuid().ToString("N");
			KingdomHostedLotDefinition view = new KingdomHostedLotDefinition {
				Key = key, DisplayName = "test archive", InteriorCell = "TAFArcologyAtrium",
				ReadOnly = true, KnowledgeView = "test:realm-dag"
			};
			Assert.IsTrue(KingdomHostedArcologyRules.RegisterHostedLot(view, out string failure), failure);
			Assert.IsFalse(KingdomHostedArcologyRules.RegisterHostedLot(view, out failure));
			view.Key += "-mutating"; view.MaterialKey = "arcologyward";
			Assert.IsFalse(KingdomHostedArcologyRules.RegisterHostedLot(view, out failure));
		}

		[Test]
		public void StableChildIdentityIsDeterministicRoleAndRootScoped()
		{
			string first = KingdomHostedArcologyRules.StableChildId("root-a", "ward:bed:1");
			Assert.AreEqual(first,
				KingdomHostedArcologyRules.StableChildId("root-a", "ward:bed:1"));
			Assert.AreNotEqual(first,
				KingdomHostedArcologyRules.StableChildId("root-a", "ward:bed:2"));
			Assert.AreNotEqual(first,
				KingdomHostedArcologyRules.StableChildId("root-b", "ward:bed:1"));
			StringAssert.StartsWith("taf:arcology:v1:", first);
			Assert.AreEqual(80, first.Length);
			Assert.AreEqual("", KingdomHostedArcologyRules.StableChildId(null, "role"));
		}

		[Test]
		public void AuthorityConfirmsOnlyOneExactRealmCapitalCarrier()
		{
			Assert.AreEqual(KingdomHostedAuthorityAction.Reserve,
				KingdomHostedArcologyRules.AuthorityAction(null,
					"realm", "capital", "zone", "root"));
			KingdomHostedArcologyAuthority row = Authority();
			Assert.AreEqual(KingdomHostedAuthorityAction.Confirm,
				KingdomHostedArcologyRules.AuthorityAction(row,
					"realm", "capital", "zone", "root"));
			Assert.AreEqual(KingdomHostedAuthorityAction.Reject,
				KingdomHostedArcologyRules.AuthorityAction(row,
					"realm", "capital", "zone", "duplicate"));
			Assert.AreEqual(KingdomHostedAuthorityAction.Reject,
				KingdomHostedArcologyRules.AuthorityAction(row,
					"realm", "other-capital", "other-zone", "root"));
			row.ConstructionJobId = null;
			Assert.AreEqual(KingdomHostedAuthorityAction.Quarantine,
				KingdomHostedArcologyRules.AuthorityAction(row,
					"realm", "capital", "zone", "root"));
		}

		[Test]
		public void TwoAuthoritySlotsProtectOnlyCurrentAndRetainedRealm()
		{
			KingdomHostedArcologyAuthority old = Authority();
			old.RealmId = "old-realm";
			KingdomHostedArcologyAuthority retained = Authority();
			retained.RealmId = "retained-realm";
			Assert.AreEqual(0, KingdomHostedArcologyRules.AuthoritySlotForWrite(
				old, retained, "new-realm", "retained-realm"));
			Assert.AreEqual(1, KingdomHostedArcologyRules.AuthoritySlotForWrite(
				old, retained, "retained-realm", "old-realm"));
			Assert.AreEqual(1, KingdomHostedArcologyRules.AuthoritySlotForWrite(
				old, null, "new-realm", "old-realm"));
			retained.RealmId = old.RealmId;
			Assert.AreEqual(-1, KingdomHostedArcologyRules.AuthoritySlotForWrite(
				old, retained, "new-realm", "old-realm"));
		}

		[Test]
		public void VersionedReceiptsRoundTripAndRejectTampering()
		{
			KingdomHostedArcologyAuthority authority = Authority();
			string encodedAuthority = KingdomHostedArcologyReceiptCodec.EncodeAuthority(authority);
			Assert.IsTrue(KingdomHostedArcologyReceiptCodec.TryDecodeAuthority(
				encodedAuthority, out KingdomHostedArcologyAuthority decodedAuthority));
			Assert.AreEqual(authority.CarrierId, decodedAuthority.CarrierId);

			KingdomHostedLotReceipt lot = WorkingLot();
			string encodedLot = KingdomHostedArcologyReceiptCodec.EncodeLot(lot);
			Assert.IsTrue(KingdomHostedArcologyReceiptCodec.TryDecodeLot(
				encodedLot, out KingdomHostedLotReceipt decodedLot));
			Assert.AreEqual(lot.Remaining, decodedLot.Remaining);
			Assert.IsFalse(KingdomHostedArcologyReceiptCodec.TryDecodeLot(
				encodedLot + "AAAA", out decodedLot));
			lot.Phase = (KingdomHostedLotPhase)255;
			Assert.AreEqual("", KingdomHostedArcologyReceiptCodec.EncodeLot(lot));
		}

		[Test]
		public void LabourUsesPriorStaffingAndBoundsElapsedCatchup()
		{
			long next;
			Assert.AreEqual(900, KingdomHostedArcologyRules.AdvanceLabor(
				1000, 100L, 300L, 50, out next));
			Assert.AreEqual(300L, next);
			Assert.AreEqual(1000, KingdomHostedArcologyRules.AdvanceLabor(
				1000, 100L, 300L, 0, out next));
			Assert.AreEqual(0, KingdomHostedArcologyRules.AdvanceLabor(
				1000, 1L, 100000L, 100, out next));
			Assert.AreEqual(1000, KingdomHostedArcologyRules.AdvanceLabor(
				1000, 300L, 200L, 100, out next));
		}

		[Test]
		public void MasterEdgeExcludesDisabledTimeButPreservesPostResumeLabor()
		{
			long next;
			Assert.AreEqual(1000, KingdomHostedArcologyRules.AdvanceLaborAfterMasterEdge(
				1000, 100L, 100000L, 100000L, 100, out next));
			Assert.AreEqual(100000L, next);
			Assert.AreEqual(950, KingdomHostedArcologyRules.AdvanceLaborAfterMasterEdge(
				1000, 100L, 100000L, 100100L, 50, out next));
			Assert.AreEqual(100100L, next);
			Assert.AreEqual(900, KingdomHostedArcologyRules.AdvanceLaborAfterMasterEdge(
				1000, 100000L, 90000L, 100200L, 50, out next));
		}

		private static KingdomHostedArcologyAuthority Authority()
		{
			return new KingdomHostedArcologyAuthority {
				Phase = KingdomHostedAuthorityPhase.Active, RealmId = "realm",
				SettlementId = "capital", ZoneId = "zone", CarrierId = "root",
				ConstructionJobId = "job"
			};
		}

		private static KingdomHostedLotReceipt WorkingLot()
		{
			return new KingdomHostedLotReceipt {
				Phase = KingdomHostedLotPhase.Working, LotKey = "arcologyward",
				JobId = "job", RootId = "root", Supports = "roof:26,luxury:2",
				Remaining = 900, LastTick = 100L, StaffingBasis = 50
			};
		}
	}
}
#endif
