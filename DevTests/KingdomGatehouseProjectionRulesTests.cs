#if TAF_TESTS
using NUnit.Framework;

namespace ThousandAndFirst.Tests
{
	[TestFixture]
	public sealed class KingdomGatehouseProjectionRulesTests
	{
		private const string Plan = "v1,1,10,1,9,1,11,3";

		[Test]
		public void StableIdentityBindsRootPlanAndSlotWithoutDrawing()
		{
			string first = KingdomGatehouseProjectionRules.StableSatelliteId(
				"root-one", Plan, 0);
			Assert.AreEqual(first, KingdomGatehouseProjectionRules.StableSatelliteId(
				"root-one", Plan, 0));
			StringAssert.StartsWith(KingdomGatehouseProjectionRules.SatelliteIdPrefix, first);
			Assert.AreNotEqual(first, KingdomGatehouseProjectionRules.StableSatelliteId(
				"root-two", Plan, 0));
			Assert.AreNotEqual(first, KingdomGatehouseProjectionRules.StableSatelliteId(
				"root-one", Plan + "x", 0));
			Assert.AreNotEqual(first, KingdomGatehouseProjectionRules.StableSatelliteId(
				"root-one", Plan, 1));
			Assert.IsNull(KingdomGatehouseProjectionRules.StableSatelliteId("", Plan, 0));
			Assert.IsNull(KingdomGatehouseProjectionRules.StableSatelliteId(
				"root-one", Plan, KingdomGatehouseTopology.SatelliteCount));
		}

		[Test]
		public void FinalReceiptRejectsIdRewriteAndSameMarkedCounterfeit()
		{
			string exact = KingdomGatehouseProjectionRules.StableSatelliteId(
				"root-one", Plan, 0);
			Assert.IsTrue(KingdomGatehouseProjectionRules.ExactSatelliteId(
				"root-one", Plan, 0, exact));
			Assert.IsFalse(KingdomGatehouseProjectionRules.ExactSatelliteId(
				"root-one", Plan, 0, "counterfeit-id"),
				"matching blueprint, marks, and cell cannot replace paid derived identity");
			Assert.IsFalse(KingdomGatehouseProjectionRules.ExactSatelliteId(
				"root-one", Plan + "x", 0, exact),
				"a changed frozen plan derives a different identity");
			Assert.IsFalse(KingdomGatehouseProjectionRules.ExactSatelliteId(
				"root-one", Plan, 1, exact));
		}

		[Test]
		public void HistoricalV1FixtureKeepsBoundedEngineAssignedIdsWithoutPretendingHashes()
		{
			string[] legacy = new string[]
			{
				"legacy-random-A91f", "legacy-random-0b72", "legacy-random-C3e4",
				"legacy-random-d5F6", "legacy-random-E708", "legacy-random-f92A"
			};
			for (int i = 0; i < legacy.Length; i++)
			{
				Assert.IsTrue(KingdomGatehouseProjectionRules.ExactStoredSatelliteId(
					false, "root-one", Plan, i, legacy[i]));
				Assert.IsFalse(KingdomGatehouseProjectionRules.ExactStoredSatelliteId(
					true, "root-one", Plan, i, legacy[i]),
					"form-v2 may never reinterpret a historical engine ID as derived truth");
			}
			string derived = KingdomGatehouseProjectionRules.StableSatelliteId(
				"root-one", Plan, 0);
			Assert.IsTrue(KingdomGatehouseProjectionRules.ExactStoredSatelliteId(
				true, "root-one", Plan, 0, derived));
			Assert.IsFalse(KingdomGatehouseProjectionRules.ExactStoredSatelliteId(
				false, "root-one", Plan, 0, "bad\nidentity"));
			Assert.IsFalse(KingdomGatehouseProjectionRules.ExactStoredSatelliteId(
				false, "root-one", Plan, 0, new string('x', 257)));
		}

		[Test]
		public void CarrierRemovalCutRetainsOwnerAcrossBodyFaultAndResumesOnlyExactSix()
		{
			Assert.IsTrue(KingdomGatehouseProjectionRules.
				MustRetainLegacyOwnerAcrossSchemaCut(false, false, false, false,
					6, 6, true, true));
			Assert.IsTrue(KingdomGatehouseProjectionRules.CanResumeLegacySchemaCut(
				false, false, false, false, 6, 6, true, true, true));
			Assert.IsFalse(KingdomGatehouseProjectionRules.CanResumeLegacySchemaCut(
				false, false, false, false, 6, 6, true, true, false),
				"missing, duplicate, or foreign bodies block resume but not root retention");
			Assert.IsTrue(KingdomGatehouseProjectionRules.
				MustRetainLegacyOwnerAcrossSchemaCut(false, false, false, false,
					6, 6, true, true), "cleanup must keep the owner on body fault");
			Assert.IsFalse(KingdomGatehouseProjectionRules.
				MustRetainLegacyOwnerAcrossSchemaCut(false, false, false, true,
					6, 6, true, true), "the carrier-present state is not this cut");
			Assert.IsFalse(KingdomGatehouseProjectionRules.
				MustRetainLegacyOwnerAcrossSchemaCut(true, false, false, false,
					6, 6, true, true), "schema-committed state is no longer pending");
			Assert.IsFalse(KingdomGatehouseProjectionRules.
				MustRetainLegacyOwnerAcrossSchemaCut(false, false, false, false,
					5, 5, true, true));
			Assert.IsFalse(KingdomGatehouseProjectionRules.
				MustRetainLegacyOwnerAcrossSchemaCut(false, false, false, false,
					6, 6, false, true));
		}

		[Test]
		public void PendingV1PublicationCutsAdoptExactCarrierAndNeverDrawReplacement()
		{
			KingdomGatehouseLegacyPublicationAction custodyBeforeId =
				KingdomGatehouseProjectionRules.ResolveLegacyPublicationCut(4,
					KingdomGatehouseSlotState.Empty, false, true, true, true, true,
					true, true, KingdomGatehouseSlotEvidence.Foreign);
			Assert.AreEqual(KingdomGatehouseLegacyPublicationAction.AdoptCustody,
				custodyBeforeId);
			KingdomGatehouseLegacyPublicationAction idBeforeState =
				KingdomGatehouseProjectionRules.ResolveLegacyPublicationCut(4,
					KingdomGatehouseSlotState.Empty, true, true, true, true, true,
					true, true, KingdomGatehouseSlotEvidence.Staged);
			Assert.AreEqual(KingdomGatehouseLegacyPublicationAction.PublishPending,
				idBeforeState);
			Assert.AreEqual(KingdomGatehouseLegacyPublicationAction.Refuse,
				KingdomGatehouseProjectionRules.ResolveLegacyPublicationCut(4,
					KingdomGatehouseSlotState.Empty, false, true, true, false, true,
					true, true, KingdomGatehouseSlotEvidence.Foreign),
				"landed unpublished custody is not adoptable");
			Assert.AreEqual(KingdomGatehouseLegacyPublicationAction.Refuse,
				KingdomGatehouseProjectionRules.ResolveLegacyPublicationCut(4,
					KingdomGatehouseSlotState.Empty, false, true, true, true, true,
					false, true, KingdomGatehouseSlotEvidence.Duplicate),
				"a duplicate arbitrary engine identity is not adoptable");
			Assert.AreEqual(KingdomGatehouseLegacyPublicationAction.Refuse,
				KingdomGatehouseProjectionRules.ResolveLegacyPublicationCut(4,
					KingdomGatehouseSlotState.Empty, false, true, true, true, true,
					true, false, KingdomGatehouseSlotEvidence.Foreign),
				"foreign partial marks are not overwritten");
		}

		[Test]
		public void V2SerializesOnlyFullyStampedDeterministicBody()
		{
			Assert.IsTrue(KingdomGatehouseProjectionRules.
				CanSerializeDeterministicCustody(true, true, true));
			Assert.IsFalse(KingdomGatehouseProjectionRules.
				CanSerializeDeterministicCustody(false, true, true));
			Assert.IsFalse(KingdomGatehouseProjectionRules.
				CanSerializeDeterministicCustody(true, false, true));
			Assert.IsFalse(KingdomGatehouseProjectionRules.
				CanSerializeDeterministicCustody(true, true, false));
		}

		[Test]
		public void FunctionalDoorAcceptsOnlyItsExactStateConsistentFrozenRender()
		{
			const string closedDisplay = "+";
			const string openDisplay = "/";
			const string closedTile = "closed.bmp";
			const string openTile = "open.bmp";
			Assert.IsTrue(ExactDoor(false, true, closedDisplay, closedTile,
				closedDisplay, openDisplay, closedTile, openTile));
			Assert.IsTrue(ExactDoor(true, true, openDisplay, openTile,
				closedDisplay, openDisplay, closedTile, openTile));
			Assert.IsFalse(ExactDoor(true, true, openDisplay, closedTile,
				closedDisplay, openDisplay, closedTile, openTile),
				"open state cannot retain the closed tile");
			Assert.IsFalse(ExactDoor(false, true, openDisplay, closedTile,
				closedDisplay, openDisplay, closedTile, openTile),
				"closed state cannot retain the open display");
			Assert.IsFalse(ExactDoor(true, true, openDisplay, openTile,
				closedDisplay, openDisplay, "tampered.bmp", openTile),
				"the Door declaration cannot drift from frozen form truth");
			Assert.IsFalse(ExactDoor(true, true, openDisplay, openTile,
				closedDisplay, openDisplay, closedTile, "tampered.bmp"),
				"the declared open tile must also remain frozen");
			Assert.IsFalse(ExactDoor(false, false, closedDisplay, closedTile,
				closedDisplay, openDisplay, closedTile, openTile),
				"a Door that no longer synchronizes its Render is not exact");
			Assert.IsFalse(ExactDoor(false, true, closedDisplay, null,
				closedDisplay, openDisplay, closedTile, openTile),
				"missing live Render tile evidence is not exact");
			Assert.IsFalse(KingdomGatehouseProjectionRules.ExactLiveDoorRender(false,
				true, closedDisplay, closedTile, closedDisplay, openDisplay,
				closedTile, openTile, closedDisplay, openDisplay, null, openTile));
		}

		[Test]
		public void PendingEnvelopeRejectsEarlySchemaPlanAndFootprintMutation()
		{
			Assert.IsTrue(KingdomGatehouseProjectionRules.ExactPendingEnvelope(
				false, false, false, Plan, Plan, true));
			Assert.IsFalse(KingdomGatehouseProjectionRules.ExactPendingEnvelope(
				true, false, false, Plan, Plan, true), "callback published schema early");
			Assert.IsFalse(KingdomGatehouseProjectionRules.ExactPendingEnvelope(
				false, true, false, Plan, Plan, true), "string schema collision");
			Assert.IsFalse(KingdomGatehouseProjectionRules.ExactPendingEnvelope(
				false, false, true, Plan, Plan, true), "integer plan collision");
			Assert.IsFalse(KingdomGatehouseProjectionRules.ExactPendingEnvelope(
				false, false, false, Plan + "x", Plan, true), "callback changed plan");
			Assert.IsFalse(KingdomGatehouseProjectionRules.ExactPendingEnvelope(
				false, false, false, Plan, Plan, false), "callback changed footprint");
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		[TestCase(4)]
		[TestCase(5)]
		public void EverySlotRecoversPublicationAndBothCallbackCuts(int index)
		{
			Assert.AreEqual(KingdomGatehouseSlotAction.PublishIdentity,
				Resolve(index, KingdomGatehouseSlotState.Empty, false,
					KingdomGatehouseSlotEvidence.Absent));
			Assert.AreEqual(KingdomGatehouseSlotAction.PublishPending,
				Resolve(index, KingdomGatehouseSlotState.Empty, true,
					KingdomGatehouseSlotEvidence.Absent));
			Assert.AreEqual(KingdomGatehouseSlotAction.Create,
				Resolve(index, KingdomGatehouseSlotState.Pending, true,
					KingdomGatehouseSlotEvidence.Absent),
				"throw-before-effect with proved cleanup recreates the same stable ID");
			Assert.AreEqual(KingdomGatehouseSlotAction.Place,
				Resolve(index, KingdomGatehouseSlotState.Pending, true,
					KingdomGatehouseSlotEvidence.Staged),
				"removal veto keeps serialized staged custody for cold-load retry");
			Assert.AreEqual(KingdomGatehouseSlotAction.Settle,
				Resolve(index, KingdomGatehouseSlotState.Pending, true,
					KingdomGatehouseSlotEvidence.ExactPlacement),
				"throw-after-effect settles only exact landed evidence");
			Assert.AreEqual(KingdomGatehouseSlotAction.Verify,
				Resolve(index, KingdomGatehouseSlotState.Settled, true,
					KingdomGatehouseSlotEvidence.ExactPlacement));
			Assert.IsFalse(KingdomGatehouseProjectionRules.CanClearCustody(
				KingdomGatehouseSlotState.Pending, true,
				KingdomGatehouseSlotEvidence.Staged));
			Assert.IsTrue(KingdomGatehouseProjectionRules.CanClearCustody(
				KingdomGatehouseSlotState.Pending, true,
				KingdomGatehouseSlotEvidence.Absent));
		}

		[TestCase(0)]
		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		[TestCase(4)]
		[TestCase(5)]
		public void EveryColdLoadedSlotRefusesLossForeignDuplicateAndContested(int index)
		{
			Assert.AreEqual(KingdomGatehouseSlotAction.Refuse,
				Resolve(index, KingdomGatehouseSlotState.Pending, true,
					KingdomGatehouseSlotEvidence.Foreign));
			Assert.AreEqual(KingdomGatehouseSlotAction.Refuse,
				Resolve(index, KingdomGatehouseSlotState.Pending, true,
					KingdomGatehouseSlotEvidence.Duplicate));
			Assert.AreEqual(KingdomGatehouseSlotAction.Refuse,
				Resolve(index, KingdomGatehouseSlotState.Settled, true,
					KingdomGatehouseSlotEvidence.Absent));
			Assert.AreEqual(KingdomGatehouseSlotAction.Refuse,
				Resolve(index, KingdomGatehouseSlotState.Contested, true,
					KingdomGatehouseSlotEvidence.ExactPlacement));
			Assert.IsTrue(KingdomGatehouseProjectionRules.HasLiveCustody(
				KingdomGatehouseSlotEvidence.Foreign));
			Assert.IsTrue(KingdomGatehouseProjectionRules.HasLiveCustody(
				KingdomGatehouseSlotEvidence.Duplicate));
		}

		private static KingdomGatehouseSlotAction Resolve(int index,
			KingdomGatehouseSlotState state, bool identity,
			KingdomGatehouseSlotEvidence evidence)
		{
			return KingdomGatehouseProjectionRules.Resolve(index, state, identity, evidence);
		}

		private static bool ExactDoor(bool open, bool syncRender,
			string liveDisplay, string liveTile, string declaredClosedDisplay,
			string declaredOpenDisplay, string declaredClosedTile, string declaredOpenTile)
		{
			return KingdomGatehouseProjectionRules.ExactLiveDoorRender(open, syncRender,
				liveDisplay, liveTile, declaredClosedDisplay, declaredOpenDisplay,
				declaredClosedTile, declaredOpenTile, "+", "/", "closed.bmp", "open.bmp");
		}
	}
}
#endif
