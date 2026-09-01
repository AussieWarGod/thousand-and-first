#if TAF_TESTS
using System;
using System.Collections.Generic;
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
			Assert.AreEqual(KingdomHostedArcologyTopology.Schema, ward.InteriorCell);
			Assert.AreEqual(KingdomHostedArcologyTopology.Schema, terrace.InteriorCell);
			Assert.AreEqual("roof:8,luxury:2", ward.Supports);
			Assert.AreEqual(8, KingdomHostedArcologyRules.ContractCap(ward, "roof"));
			Assert.AreEqual(2, KingdomHostedArcologyRules.ContractCap(ward, "luxury"));
			Assert.AreEqual(0, KingdomHostedArcologyRules.ContractCap(ward, "food"));
			Assert.IsTrue(terrace.RequiresWater);
			Assert.AreEqual("r_KingdomArcologyGrowbed",
				terrace.PhysicalProducerBlueprint);
			Assert.AreEqual(14, terrace.PhysicalProducerCount);
			ward.DisplayName = "tampered";
			Assert.IsTrue(KingdomHostedArcologyRules.TryHostedLot("arcologyward", out ward));
			Assert.AreNotEqual("tampered", ward.DisplayName);
		}

		[Test]
		public void PaidHostedRegistrationIsClosedWithoutMutatingCatalogue()
		{
			int before = KingdomHostedArcologyRules.RegisteredHostedLots().Count;
			KingdomHostedLotDefinition paid = new KingdomHostedLotDefinition {
				Key = "test-paid-" + Guid.NewGuid().ToString("N"),
				DisplayName = "unsupported paid floor", InteriorCell = "TAFArcology",
				MaterialKey = "arcologyterrace", BuildTicks = 1L, Crew = 1,
				Supports = "food:1", PhysicalProducerBlueprint = "r_KingdomArcologyGrowbed",
				PhysicalProducerCount = 1
			};
			Assert.IsFalse(KingdomHostedArcologyRules.RegisterReadOnlyHostedLot(paid,
				out string failure));
			StringAssert.Contains("closed in v1", failure);
			Assert.AreEqual(before, KingdomHostedArcologyRules.RegisteredHostedLots().Count);
		}

		[Test]
		public void RegistrationRejectsDuplicateAndReadOnlyMutationSurface()
		{
			string key = "test-read-view-" + Guid.NewGuid().ToString("N");
			KingdomHostedLotDefinition view = new KingdomHostedLotDefinition {
				Key = key, DisplayName = "test archive", InteriorCell = "TAFArcology",
				ReadOnly = true, KnowledgeView = "test:realm-dag"
			};
			Assert.IsTrue(KingdomHostedArcologyRules.RegisterReadOnlyHostedLot(
				view, out string failure), failure);
			Assert.IsFalse(KingdomHostedArcologyRules.RegisterReadOnlyHostedLot(view, out failure));
			view.Key += "-mutating"; view.MaterialKey = "arcologyward";
			Assert.IsFalse(KingdomHostedArcologyRules.RegisterReadOnlyHostedLot(view, out failure));
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
		public void CompleteSlateRejectsDuplicateForeignAndDivergentReceipts()
		{
			KingdomHostedLotReceipt ward = WorkingLot();
			string encoded = KingdomHostedArcologyReceiptCodec.EncodeLot(ward);
			Assert.IsTrue(KingdomHostedArcologySlateRules.TryRead(
				new List<string> { encoded }, "root",
				out List<KingdomHostedLotReceipt> rows, out string failure), failure);
			Assert.AreEqual(1, rows.Count);

			Assert.IsFalse(KingdomHostedArcologySlateRules.TryRead(
				new List<string> { encoded, encoded }, "root", out rows, out failure));
			StringAssert.Contains("duplicate", failure);

			ward.RootId = "other-root";
			Assert.IsFalse(KingdomHostedArcologySlateRules.TryRead(new List<string> {
				KingdomHostedArcologyReceiptCodec.EncodeLot(ward) }, "root",
				out rows, out failure));
			StringAssert.Contains("another shell", failure);

			ward.RootId = "root";
			ward.Supports = "roof:999";
			Assert.IsFalse(KingdomHostedArcologySlateRules.TryRead(new List<string> {
				KingdomHostedArcologyReceiptCodec.EncodeLot(ward) }, "root",
				out rows, out failure));
			StringAssert.Contains("work contract", failure);
		}

		[Test]
		public void FinalObservationRoundTripsCanonicallyAndCopiesOnRead()
		{
			KingdomHostedObservation row = Observation(
				KingdomHostedArcologyTopology.WardLotKey);
			string encoded = KingdomHostedArcologyReceiptCodec.EncodeObservation(row);
			Assert.IsTrue(KingdomHostedArcologyReceiptCodec.TryDecodeObservation(
				encoded, out KingdomHostedObservation decoded));
			Assert.AreEqual(8, decoded.Roof);
			Assert.AreEqual(row.ReceiptRevision, decoded.ReceiptRevision);
			Assert.IsFalse(KingdomHostedArcologyReceiptCodec.TryDecodeObservation(
				encoded + "AAAA", out decoded));

			Assert.IsTrue(KingdomHostedArcologySlateRules.TryReadObservations(
				new List<string> { encoded }, "root",
				out List<KingdomHostedObservation> rows, out string failure), failure);
			rows[0].Roof = 0;
			Assert.IsTrue(KingdomHostedArcologySlateRules.TryReadObservations(
				new List<string> { encoded }, "root", out rows, out failure), failure);
			Assert.AreEqual(8, rows[0].Roof);
		}

		[Test]
		public void ObservationSlateRejectsDuplicateForeignCrossLaneAndOverCapRows()
		{
			KingdomHostedObservation row = Observation(
				KingdomHostedArcologyTopology.WardLotKey);
			string encoded = KingdomHostedArcologyReceiptCodec.EncodeObservation(row);
			Assert.IsFalse(KingdomHostedArcologySlateRules.TryReadObservations(
				new List<string> { encoded, encoded }, "root",
				out List<KingdomHostedObservation> rows, out string failure));
			StringAssert.Contains("duplicate", failure);

			row.RootId = "foreign";
			Assert.IsFalse(KingdomHostedArcologySlateRules.TryReadObservations(
				new List<string> { KingdomHostedArcologyReceiptCodec.EncodeObservation(row) },
				"root", out rows, out failure));
			StringAssert.Contains("another shell", failure);

			row = Observation(KingdomHostedArcologyTopology.WardLotKey); row.Food = 1;
			Assert.IsFalse(KingdomHostedArcologySlateRules.TryReadObservations(
				new List<string> { KingdomHostedArcologyReceiptCodec.EncodeObservation(row) },
				"root", out rows, out failure));
			StringAssert.Contains("physical work contract", failure);

			row = Observation(KingdomHostedArcologyTopology.WardLotKey); row.Roof = 9;
			Assert.IsFalse(KingdomHostedArcologySlateRules.TryReadObservations(
				new List<string> { KingdomHostedArcologyReceiptCodec.EncodeObservation(row) },
				"root", out rows, out failure));
			StringAssert.Contains("physical work contract", failure);
		}

		[Test]
		public void ObservationMatchBindsReceiptRootZoneAnchorAndTime()
		{
			KingdomHostedObservation row = Observation(
				KingdomHostedArcologyTopology.WardLotKey);
			Assert.IsTrue(KingdomHostedArcologySlateRules.Matches(row, "root",
				row.LotKey, row.ReceiptRevision, row.InteriorZoneId, row.AnchorId,
				100L, out string failure), failure);
			Assert.IsFalse(KingdomHostedArcologySlateRules.Matches(row, "other",
				row.LotKey, row.ReceiptRevision, row.InteriorZoneId, row.AnchorId,
				100L, out failure));
			Assert.IsFalse(KingdomHostedArcologySlateRules.Matches(row, "root",
				row.LotKey, "changed", row.InteriorZoneId, row.AnchorId, 100L, out failure));
			Assert.IsFalse(KingdomHostedArcologySlateRules.Matches(row, "root",
				row.LotKey, row.ReceiptRevision, "other-zone", row.AnchorId, 100L, out failure));
			Assert.IsFalse(KingdomHostedArcologySlateRules.Matches(row, "root",
				row.LotKey, row.ReceiptRevision, row.InteriorZoneId, "other-anchor",
				100L, out failure));
			Assert.IsFalse(KingdomHostedArcologySlateRules.Matches(row, "root",
				row.LotKey, row.ReceiptRevision, row.InteriorZoneId, row.AnchorId,
				99L, out failure));
		}

		[Test]
		public void PhysicalRowsAndObservationAgeAreBoundedPureArithmetic()
		{
			Assert.AreEqual(14, KingdomHostedArcologyRules.PhysicalFoodForRows(
				28, 3, 6));
			Assert.AreEqual(0, KingdomHostedArcologyRules.PhysicalFoodForRows(0, 3, 6));
			Assert.AreEqual(int.MaxValue, KingdomHostedArcologyRules.PhysicalFoodForRows(
				int.MaxValue, int.MaxValue, 1));
			Assert.AreEqual(0, KingdomHostedArcologySlateRules.AgeDays(100L, 100L, 10L));
			Assert.AreEqual(2, KingdomHostedArcologySlateRules.AgeDays(100L, 129L, 10L));
			Assert.AreEqual(int.MaxValue, KingdomHostedArcologySlateRules.AgeDays(
				0L, long.MaxValue, 1L));
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
				JobId = "job", RootId = "root", Supports = "roof:8,luxury:2",
				Remaining = 900, LastTick = 100L, StaffingBasis = 50
			};
		}

		private static KingdomHostedObservation Observation(string LotKey)
		{
			KingdomHostedLotReceipt receipt = new KingdomHostedLotReceipt {
				Phase = KingdomHostedLotPhase.Active, LotKey = LotKey, JobId = "job",
				RootId = "root", Supports = LotKey == KingdomHostedArcologyTopology.WardLotKey
					? "roof:8,luxury:2" : "food:14", Remaining = 0,
				LastTick = 100L, StaffingBasis = 100,
				RequiresWater = LotKey == KingdomHostedArcologyTopology.TerraceLotKey
			};
			return new KingdomHostedObservation { RootId = "root", LotKey = LotKey,
				ReceiptRevision = KingdomHostedArcologyRules.ReceiptRevision(receipt),
				InteriorZoneId = "Interior@TAFArcology@root.0.0.0.1.11",
				AnchorId = "anchor", ObservedTick = 100L,
				Roof = LotKey == KingdomHostedArcologyTopology.WardLotKey ? 8 : 0,
				Luxury = LotKey == KingdomHostedArcologyTopology.WardLotKey ? 2 : 0,
				Food = LotKey == KingdomHostedArcologyTopology.TerraceLotKey ? 14 : 0 };
		}
	}
}
#endif
