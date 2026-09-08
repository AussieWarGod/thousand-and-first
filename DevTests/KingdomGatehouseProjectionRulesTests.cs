#if TAF_TESTS
using NUnit.Framework;
using NUnit.Framework.Legacy;

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
			ClassicAssert.AreEqual(first, KingdomGatehouseProjectionRules.StableSatelliteId(
				"root-one", Plan, 0));
			StringAssert.StartsWith(KingdomGatehouseProjectionRules.SatelliteIdPrefix, first);
			ClassicAssert.AreNotEqual(first, KingdomGatehouseProjectionRules.StableSatelliteId(
				"root-two", Plan, 0));
			ClassicAssert.AreNotEqual(first, KingdomGatehouseProjectionRules.StableSatelliteId(
				"root-one", Plan + "x", 0));
			ClassicAssert.AreNotEqual(first, KingdomGatehouseProjectionRules.StableSatelliteId(
				"root-one", Plan, 1));
			ClassicAssert.IsNull(KingdomGatehouseProjectionRules.StableSatelliteId("", Plan, 0));
			ClassicAssert.IsNull(KingdomGatehouseProjectionRules.StableSatelliteId(
				"root-one", Plan, KingdomGatehouseTopology.SatelliteCount));
		}

		[Test]
		public void FinalReceiptRejectsIdRewriteAndSameMarkedCounterfeit()
		{
			string exact = KingdomGatehouseProjectionRules.StableSatelliteId(
				"root-one", Plan, 0);
			ClassicAssert.IsTrue(KingdomGatehouseProjectionRules.ExactSatelliteId(
				"root-one", Plan, 0, exact));
			ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.ExactSatelliteId(
				"root-one", Plan, 0, "counterfeit-id"),
				"matching blueprint, marks, and cell cannot replace paid derived identity");
			ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.ExactSatelliteId(
				"root-one", Plan + "x", 0, exact),
				"a changed frozen plan derives a different identity");
			ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.ExactSatelliteId(
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
				ClassicAssert.IsTrue(KingdomGatehouseProjectionRules.ExactStoredSatelliteId(
					false, "root-one", Plan, i, legacy[i]));
				ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.ExactStoredSatelliteId(
					true, "root-one", Plan, i, legacy[i]),
					"form-v2 may never reinterpret a historical engine ID as derived truth");
			}
			string derived = KingdomGatehouseProjectionRules.StableSatelliteId(
				"root-one", Plan, 0);
			ClassicAssert.IsTrue(KingdomGatehouseProjectionRules.ExactStoredSatelliteId(
				true, "root-one", Plan, 0, derived));
			ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.ExactStoredSatelliteId(
				false, "root-one", Plan, 0, "bad\nidentity"));
			ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.ExactStoredSatelliteId(
				false, "root-one", Plan, 0, new string('x', 257)));
		}

		[Test]
		public void CarrierRemovalCutRetainsOwnerAcrossBodyFaultAndResumesOnlyExactSix()
		{
			ClassicAssert.IsTrue(KingdomGatehouseProjectionRules.
				MustRetainLegacyOwnerAcrossSchemaCut(false, false, false, false,
					6, 6, true, true));
			ClassicAssert.IsTrue(KingdomGatehouseProjectionRules.CanResumeLegacySchemaCut(
				false, false, false, false, 6, 6, true, true, true));
			ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.CanResumeLegacySchemaCut(
				false, false, false, false, 6, 6, true, true, false),
				"missing, duplicate, or foreign bodies block resume but not root retention");
			ClassicAssert.IsTrue(KingdomGatehouseProjectionRules.
				MustRetainLegacyOwnerAcrossSchemaCut(false, false, false, false,
					6, 6, true, true), "cleanup must keep the owner on body fault");
			ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.
				MustRetainLegacyOwnerAcrossSchemaCut(false, false, false, true,
					6, 6, true, true), "the carrier-present state is not this cut");
			ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.
				MustRetainLegacyOwnerAcrossSchemaCut(true, false, false, false,
					6, 6, true, true), "schema-committed state is no longer pending");
			ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.
				MustRetainLegacyOwnerAcrossSchemaCut(false, false, false, false,
					5, 5, true, true));
			ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.
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
			ClassicAssert.AreEqual(KingdomGatehouseLegacyPublicationAction.AdoptCustody,
				custodyBeforeId);
			KingdomGatehouseLegacyPublicationAction idBeforeState =
				KingdomGatehouseProjectionRules.ResolveLegacyPublicationCut(4,
					KingdomGatehouseSlotState.Empty, true, true, true, true, true,
					true, true, KingdomGatehouseSlotEvidence.Staged);
			ClassicAssert.AreEqual(KingdomGatehouseLegacyPublicationAction.PublishPending,
				idBeforeState);
			ClassicAssert.AreEqual(KingdomGatehouseLegacyPublicationAction.Refuse,
				KingdomGatehouseProjectionRules.ResolveLegacyPublicationCut(4,
					KingdomGatehouseSlotState.Empty, false, true, true, false, true,
					true, true, KingdomGatehouseSlotEvidence.Foreign),
				"landed unpublished custody is not adoptable");
			ClassicAssert.AreEqual(KingdomGatehouseLegacyPublicationAction.Refuse,
				KingdomGatehouseProjectionRules.ResolveLegacyPublicationCut(4,
					KingdomGatehouseSlotState.Empty, false, true, true, true, true,
					false, true, KingdomGatehouseSlotEvidence.Duplicate),
				"a duplicate arbitrary engine identity is not adoptable");
			ClassicAssert.AreEqual(KingdomGatehouseLegacyPublicationAction.Refuse,
				KingdomGatehouseProjectionRules.ResolveLegacyPublicationCut(4,
					KingdomGatehouseSlotState.Empty, false, true, true, true, true,
					true, false, KingdomGatehouseSlotEvidence.Foreign),
				"foreign partial marks are not overwritten");
		}

		[Test]
		public void V2SerializesOnlyFullyStampedDeterministicBody()
		{
			ClassicAssert.IsTrue(KingdomGatehouseProjectionRules.
				CanSerializeDeterministicCustody(true, true, true));
			ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.
				CanSerializeDeterministicCustody(false, true, true));
			ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.
				CanSerializeDeterministicCustody(true, false, true));
			ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.
				CanSerializeDeterministicCustody(true, true, false));
		}

		[Test]
		public void FunctionalDoorAcceptsOnlyItsExactStateConsistentFrozenRender()
		{
			const string closedDisplay = "+";
			const string openDisplay = "/";
			const string closedTile = "closed.bmp";
			const string openTile = "open.bmp";
			ClassicAssert.IsTrue(ExactDoor(false, true, closedDisplay, closedTile,
				closedDisplay, openDisplay, closedTile, openTile));
			ClassicAssert.IsTrue(ExactDoor(true, true, openDisplay, openTile,
				closedDisplay, openDisplay, closedTile, openTile));
			ClassicAssert.IsFalse(ExactDoor(true, true, openDisplay, closedTile,
				closedDisplay, openDisplay, closedTile, openTile),
				"open state cannot retain the closed tile");
			ClassicAssert.IsFalse(ExactDoor(false, true, openDisplay, closedTile,
				closedDisplay, openDisplay, closedTile, openTile),
				"closed state cannot retain the open display");
			ClassicAssert.IsFalse(ExactDoor(true, true, openDisplay, openTile,
				closedDisplay, openDisplay, "tampered.bmp", openTile),
				"the Door declaration cannot drift from frozen form truth");
			ClassicAssert.IsFalse(ExactDoor(true, true, openDisplay, openTile,
				closedDisplay, openDisplay, closedTile, "tampered.bmp"),
				"the declared open tile must also remain frozen");
			ClassicAssert.IsFalse(ExactDoor(false, false, closedDisplay, closedTile,
				closedDisplay, openDisplay, closedTile, openTile),
				"a Door that no longer synchronizes its Render is not exact");
			ClassicAssert.IsFalse(ExactDoor(false, true, closedDisplay, null,
				closedDisplay, openDisplay, closedTile, openTile),
				"missing live Render tile evidence is not exact");
			ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.ExactLiveDoorRender(false,
				true, closedDisplay, closedTile, closedDisplay, openDisplay,
				closedTile, openTile, closedDisplay, openDisplay, null, openTile));
		}

		[Test]
		public void PendingEnvelopeRejectsEarlySchemaPlanAndFootprintMutation()
		{
			ClassicAssert.IsTrue(KingdomGatehouseProjectionRules.ExactPendingEnvelope(
				false, false, false, Plan, Plan, true));
			ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.ExactPendingEnvelope(
				true, false, false, Plan, Plan, true), "callback published schema early");
			ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.ExactPendingEnvelope(
				false, true, false, Plan, Plan, true), "string schema collision");
			ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.ExactPendingEnvelope(
				false, false, true, Plan, Plan, true), "integer plan collision");
			ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.ExactPendingEnvelope(
				false, false, false, Plan + "x", Plan, true), "callback changed plan");
			ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.ExactPendingEnvelope(
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
			ClassicAssert.AreEqual(KingdomGatehouseSlotAction.PublishIdentity,
				Resolve(index, KingdomGatehouseSlotState.Empty, false,
					KingdomGatehouseSlotEvidence.Absent));
			ClassicAssert.AreEqual(KingdomGatehouseSlotAction.PublishPending,
				Resolve(index, KingdomGatehouseSlotState.Empty, true,
					KingdomGatehouseSlotEvidence.Absent));
			ClassicAssert.AreEqual(KingdomGatehouseSlotAction.Create,
				Resolve(index, KingdomGatehouseSlotState.Pending, true,
					KingdomGatehouseSlotEvidence.Absent),
				"throw-before-effect with proved cleanup recreates the same stable ID");
			ClassicAssert.AreEqual(KingdomGatehouseSlotAction.Place,
				Resolve(index, KingdomGatehouseSlotState.Pending, true,
					KingdomGatehouseSlotEvidence.Staged),
				"removal veto keeps serialized staged custody for cold-load retry");
			ClassicAssert.AreEqual(KingdomGatehouseSlotAction.Settle,
				Resolve(index, KingdomGatehouseSlotState.Pending, true,
					KingdomGatehouseSlotEvidence.ExactPlacement),
				"throw-after-effect settles only exact landed evidence");
			ClassicAssert.AreEqual(KingdomGatehouseSlotAction.Verify,
				Resolve(index, KingdomGatehouseSlotState.Settled, true,
					KingdomGatehouseSlotEvidence.ExactPlacement));
			ClassicAssert.IsFalse(KingdomGatehouseProjectionRules.CanClearCustody(
				KingdomGatehouseSlotState.Pending, true,
				KingdomGatehouseSlotEvidence.Staged));
			ClassicAssert.IsTrue(KingdomGatehouseProjectionRules.CanClearCustody(
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
			ClassicAssert.AreEqual(KingdomGatehouseSlotAction.Refuse,
				Resolve(index, KingdomGatehouseSlotState.Pending, true,
					KingdomGatehouseSlotEvidence.Foreign));
			ClassicAssert.AreEqual(KingdomGatehouseSlotAction.Refuse,
				Resolve(index, KingdomGatehouseSlotState.Pending, true,
					KingdomGatehouseSlotEvidence.Duplicate));
			ClassicAssert.AreEqual(KingdomGatehouseSlotAction.Refuse,
				Resolve(index, KingdomGatehouseSlotState.Settled, true,
					KingdomGatehouseSlotEvidence.Absent));
			ClassicAssert.AreEqual(KingdomGatehouseSlotAction.Refuse,
				Resolve(index, KingdomGatehouseSlotState.Contested, true,
					KingdomGatehouseSlotEvidence.ExactPlacement));
			ClassicAssert.IsTrue(KingdomGatehouseProjectionRules.HasLiveCustody(
				KingdomGatehouseSlotEvidence.Foreign));
			ClassicAssert.IsTrue(KingdomGatehouseProjectionRules.HasLiveCustody(
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
