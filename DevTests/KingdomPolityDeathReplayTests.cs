#if TAF_TESTS
using System;
using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityDeathReplayTests
	{
		[Test]
		public void CanonicalEnvelopeRoundTripsEveryFrozenField()
		{
			KingdomPolityDeathIntentRecord expected = Record();
			Assert.IsTrue(KingdomPolityDeathIntentRules.TryEncode(expected,
				out string wire, out string failure), failure);
			StringAssert.StartsWith(KingdomPolityDeathIntentRules.WirePrefix, wire);
			Assert.IsTrue(KingdomPolityDeathIntentRules.TryDecode(wire,
				out KingdomPolityDeathIntentRecord actual, out failure), failure);
			Assert.AreEqual(expected.Kind, actual.Kind);
			Assert.AreEqual(expected.RealmId, actual.RealmId);
			Assert.AreEqual(expected.CohortId, actual.CohortId);
			Assert.AreEqual(expected.ProjectionId, actual.ProjectionId);
			Assert.AreEqual(expected.ZoneId, actual.ZoneId);
			Assert.AreEqual(expected.ObjectId, actual.ObjectId);
			Assert.AreEqual(expected.Ordinal, actual.Ordinal);
			Assert.AreEqual(expected.Purpose, actual.Purpose);
			Assert.AreEqual(expected.Representative, actual.Representative);
			Assert.AreEqual(expected.Tick, actual.Tick);
			Assert.AreEqual(expected.Attribution, actual.Attribution);
			Assert.AreEqual(expected.Visibility, actual.Visibility);
			Assert.AreEqual(expected.IncidentPlanId, actual.IncidentPlanId);
			Assert.AreEqual(expected.IncidentId, actual.IncidentId);
			Assert.AreEqual(expected.IncidentDigest, actual.IncidentDigest);
			Assert.IsTrue(KingdomPolityDeathIntentRules.TryEncode(actual,
				out string repeated, out failure), failure);
			Assert.AreEqual(wire, repeated);
		}

		[Test]
		public void DigestTamperAndFutureEnvelopeFailClosed()
		{
			Assert.IsTrue(KingdomPolityDeathIntentRules.TryEncode(Record(),
				out string wire, out string failure), failure);
			char replacement = wire[wire.Length - 1] == '0' ? '1' : '0';
			string tampered = wire.Substring(0, wire.Length - 1) + replacement;
			Assert.IsFalse(KingdomPolityDeathIntentRules.TryDecode(tampered, out _, out _));
			string future = wire.Replace(":v2:", ":v3:");
			Assert.IsFalse(KingdomPolityDeathIntentRules.TryDecode(future, out _, out _));
			Assert.AreEqual(KingdomPolityDeathIntentState.Ambiguous,
				KingdomPolityDeathIntentRules.Classify(true, true, false, false));
		}

		[Test]
		public void LegacyV1PhysicalIntentDecodesOnlyAsBoundedMigrationEvidence()
		{
			KingdomPolityDeathIntentRecord source = Record();
			source.Visibility = KingdomPolityDeathVisibility.PhysicalOnly;
			source.Attribution = KingdomPolityDeathAttribution.Unattributed;
			string wire = KingdomPolityDeathIntentRules.EncodeV1Fixture(source);
			Assert.IsTrue(KingdomPolityDeathIntentRules.TryDecode(wire,
				out KingdomPolityDeathIntentRecord decoded, out string failure), failure);
			Assert.IsTrue(decoded.LegacyV1);
			Assert.AreEqual("", decoded.IncidentPlanId);
			Assert.AreEqual("", decoded.IncidentId);
			Assert.AreEqual("", decoded.IncidentDigest);
			Assert.IsTrue(KingdomPolityDeathIntentRules.TryEncode(decoded,
				out string rewritten, out failure), failure);
			StringAssert.StartsWith(KingdomPolityDeathIntentRules.WirePrefix, rewritten);
		}

		[Test]
		public void TruncatedAndOversizedEnvelopeFailClosed()
		{
			Assert.IsTrue(KingdomPolityDeathIntentRules.TryEncode(Record(),
				out string wire, out string failure), failure);
			Assert.IsFalse(KingdomPolityDeathIntentRules.TryDecode(
				wire.Substring(0, wire.Length - 3), out _, out _));
			Assert.IsFalse(KingdomPolityDeathIntentRules.TryDecode(new string('x',
				KingdomPolityDeathIntentRules.MaximumWireCharacters + 1), out _, out _));
		}

		[Test]
		public void InvalidUtf8WithFreshDigestStillFailsClosed()
		{
			Assert.IsTrue(KingdomPolityDeathIntentRules.TryEncode(Record(),
				out string wire, out string failure), failure);
			int separator = wire.Length - 65;
			string body = wire.Substring(KingdomPolityDeathIntentRules.WirePrefix.Length,
				separator - KingdomPolityDeathIntentRules.WirePrefix.Length);
			byte[] payload = Convert.FromBase64String(body);
			payload[5] = 0xff;
			body = Convert.ToBase64String(payload);
			string invalid = KingdomPolityDeathIntentRules.WirePrefix + body + ":" +
				KingdomPolityRules.ActivationDigest(
					"polity-visible-death-intent-envelope-v2", body);
			Assert.IsFalse(KingdomPolityDeathIntentRules.TryDecode(invalid, out _, out _));
		}

		[Test]
		public void OversizedOrNonUtf16FieldCannotEncode()
		{
			KingdomPolityDeathIntentRecord record = Record();
			record.ZoneId = new string('z', KingdomPolityDeathIntentRules.MaximumFieldBytes + 1);
			Assert.IsFalse(KingdomPolityDeathIntentRules.TryEncode(record, out _, out _));
			record = Record(); record.ZoneId = "zone-\ud800";
			Assert.IsFalse(KingdomPolityDeathIntentRules.TryEncode(record, out _, out _));
		}

		[Test]
		public void SlotClassifierPreservesWrongTypedMalformedAndForeignAuthority()
		{
			Assert.AreEqual(KingdomPolityDeathIntentState.Clear,
				KingdomPolityDeathIntentRules.Classify(false, false, false, false));
			Assert.AreEqual(KingdomPolityDeathIntentState.Ambiguous,
				KingdomPolityDeathIntentRules.Classify(true, false, false, false));
			Assert.AreEqual(KingdomPolityDeathIntentState.Ambiguous,
				KingdomPolityDeathIntentRules.Classify(true, true, false, false));
			Assert.AreEqual(KingdomPolityDeathIntentState.Ambiguous,
				KingdomPolityDeathIntentRules.Classify(true, true, true, false));
			Assert.AreEqual(KingdomPolityDeathIntentState.Outstanding,
				KingdomPolityDeathIntentRules.Classify(true, true, true, true));
		}

		[Test]
		public void ExactTupleAndCausalTickRejectDrift()
		{
			KingdomPolityDeathIntentRecord record = Record();
			Assert.IsTrue(KingdomPolityDeathIntentRules.ExactBinding(record, record.RealmId,
				record.CohortId, record.ProjectionId, record.ZoneId, record.ObjectId,
				record.Ordinal, record.Purpose, record.Representative));
			Assert.IsFalse(KingdomPolityDeathIntentRules.ExactBinding(record, record.RealmId,
				record.CohortId, "taf:projection:foreign", record.ZoneId, record.ObjectId,
				record.Ordinal, record.Purpose, record.Representative));
			Assert.IsTrue(KingdomPolityDeathIntentRules.CausalTick(record, 100L, 200L));
			Assert.IsFalse(KingdomPolityDeathIntentRules.CausalTick(record, 151L, 200L));
			Assert.IsFalse(KingdomPolityDeathIntentRules.CausalTick(record, 100L, 149L));
		}

		[Test]
		public void FrozenVisibilityAndAttributionSelectOnlyOwnedConsequences()
		{
			KingdomPolityDeathIntentRecord record = Record();
			Assert.AreEqual(KingdomPolityDeathIntentAction.ReplayEnvoy,
				KingdomPolityDeathIntentRules.Decide(record,
					KingdomPolityCohortPhase.Materialized));
			record.Purpose = KingdomPolityCohortPurpose.Warband;
			Assert.AreEqual(KingdomPolityDeathIntentAction.ReplayWarband,
				KingdomPolityDeathIntentRules.Decide(record,
					KingdomPolityCohortPhase.Concluded));
			record.Ordinal = 1; record.Representative = false;
			Assert.AreEqual(KingdomPolityDeathIntentAction.Clear,
				KingdomPolityDeathIntentRules.Decide(record,
					KingdomPolityCohortPhase.Materialized));
			record.Visibility = KingdomPolityDeathVisibility.PhysicalOnly;
			record.IncidentPlanId = record.IncidentId = record.IncidentDigest = "";
			record.Attribution = KingdomPolityDeathAttribution.Unattributed;
			Assert.AreEqual(KingdomPolityDeathIntentAction.Abandon,
				KingdomPolityDeathIntentRules.Decide(record,
					KingdomPolityCohortPhase.Materialized));
			Assert.AreEqual(KingdomPolityDeathIntentAction.Clear,
				KingdomPolityDeathIntentRules.Decide(record,
					KingdomPolityCohortPhase.Abandoned));
		}

		[Test]
		public void PhysicalOnlyIntentCannotClaimPlayerAttribution()
		{
			KingdomPolityDeathIntentRecord record = Record();
			record.Visibility = KingdomPolityDeathVisibility.PhysicalOnly;
			record.Attribution = KingdomPolityDeathAttribution.PlayerWitnessed;
			Assert.IsFalse(KingdomPolityDeathIntentRules.TryEncode(record, out _, out _));
		}

		[Test]
		public void TwoOpenIncidentsSharingCohortRefuseBeforeIntentCanFreeze()
		{
			KingdomPolityLedger ledger = Scene(out KingdomPolityCohortPlan cohort,
				out KingdomPolityProjectionReceipt _);
			KingdomPolityIncidentRecord original = KingdomPolityGapTestData.Incident(ledger,
				KingdomPolityGapTestData.TermsPlan);
			KingdomPolityIncidentRecord reused = new KingdomPolityIncidentRecord
			{
				IncidentPlanId = "taf:incident-plan:reused-death", IncidentId =
					"taf:incident:reused-death", Purpose = original.Purpose,
				EventStreamId = original.EventStreamId, RulesVersion = original.RulesVersion,
				EventOrdinal = original.EventOrdinal, MaxSystemicWound = original.MaxSystemicWound,
				GrievanceRefs = new System.Collections.Generic.List<string>(original.GrievanceRefs),
				ParticipantCohortRefs = new System.Collections.Generic.List<string>(
					original.ParticipantCohortRefs),
				DisclosedStakeRefs = new System.Collections.Generic.List<string>(original.DisclosedStakeRefs),
				EligibleSurfaceRefs = new System.Collections.Generic.List<string>(original.EligibleSurfaceRefs),
				InterventionOptionKeys = new System.Collections.Generic.List<string>(
					original.InterventionOptionKeys)
			};
			ledger.Incidents.Add(reused);
			Assert.IsFalse(KingdomPolityDeathIncidentRules.TryFreeze(ledger, cohort, 0, true,
				out _, out _, out _, out string failure));
			StringAssert.Contains("multiple open incident authorities", failure);
		}

		[Test]
		public void OffscreenRepresentativeNeedsNoIncidentAndFreezesEmptyTuple()
		{
			KingdomPolityLedger ledger = Scene(out KingdomPolityCohortPlan cohort,
				out KingdomPolityProjectionReceipt _);
			ledger.Incidents.Clear();
			Assert.IsTrue(KingdomPolityDeathIncidentRules.TryFreeze(ledger, cohort, 0, false,
				out string plan, out string incident, out string digest, out string failure), failure);
			Assert.AreEqual("", plan); Assert.AreEqual("", incident); Assert.AreEqual("", digest);
		}

		[Test]
		public void ExactPhysicalLossCommitsHonestAbandonmentWithoutSemanticMutation()
		{
			KingdomPolityLedger ledger = Scene(out KingdomPolityCohortPlan cohort,
				out KingdomPolityProjectionReceipt projection);
			KingdomPolityDeathIntentRecord intent = PhysicalIntent(ledger, cohort, projection);
			KingdomPolityIncidentRecord terms = KingdomPolityGapTestData.Incident(ledger,
				KingdomPolityGapTestData.TermsPlan);
			KingdomPolityRoutePhase route = KingdomPolityGapTestData.RouteRecord(ledger).Phase;
			KingdomPolityGrievanceRecord grievance = ledger.Grievances[0];
			KingdomPolityGrievancePhase grievancePhase = grievance.Phase;
			string consumed = grievance.ConsumedByIncidentId;
			Assert.IsTrue(KingdomPolityCohortRules.TryAbandonEndpointCohort(ledger,
				ledger.Revision, intent, true, out KingdomPolityPublicationResult result,
				out string failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.Applied, result.Outcome);
			cohort = KingdomPolityAuthority.Cohort(ledger, cohort.CohortId);
			Assert.AreEqual(KingdomPolityCohortPhase.Abandoned, cohort.Phase);
			Assert.IsNull(cohort.RewardEventId);
			Assert.IsNull(terms.Conclusion);
			Assert.AreEqual(route, KingdomPolityGapTestData.RouteRecord(ledger).Phase);
			Assert.AreEqual(grievancePhase, grievance.Phase);
			Assert.AreEqual(consumed, grievance.ConsumedByIncidentId);
		}

		[Test]
		public void AbandonmentRefusesMissingWitnessOrVisibleClaimByteExactly()
		{
			KingdomPolityLedger ledger = Scene(out KingdomPolityCohortPlan cohort,
				out KingdomPolityProjectionReceipt projection);
			KingdomPolityDeathIntentRecord intent = PhysicalIntent(ledger, cohort, projection);
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			Assert.IsFalse(KingdomPolityCohortRules.TryAbandonEndpointCohort(ledger,
				ledger.Revision, intent, false, out _, out _));
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
			intent.Visibility = KingdomPolityDeathVisibility.PlayerVisible;
			Assert.IsFalse(KingdomPolityCohortRules.TryAbandonEndpointCohort(ledger,
				ledger.Revision, intent, true, out _, out _));
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void AbandonedTerminalIsIdempotentCodecStableAndCleanupPreservesPhase()
		{
			KingdomPolityLedger ledger = Scene(out KingdomPolityCohortPlan cohort,
				out KingdomPolityProjectionReceipt projection);
			KingdomPolityDeathIntentRecord intent = PhysicalIntent(ledger, cohort, projection);
			long revision = ledger.Revision;
			Assert.IsTrue(KingdomPolityCohortRules.TryAbandonEndpointCohort(ledger, revision,
				intent, true, out _, out string failure), failure);
			byte[] abandoned = KingdomPolityCodec.EncodeEnvelope(ledger);
			Assert.IsTrue(KingdomPolityCohortRules.TryAbandonEndpointCohort(ledger, revision,
				intent, true, out KingdomPolityPublicationResult repeated, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, repeated.Outcome);
			CollectionAssert.AreEqual(abandoned, KingdomPolityCodec.EncodeEnvelope(ledger));
			Assert.IsTrue(KingdomPolityCohortRules.TryCommitEndpointCleanup(ledger,
				ledger.Revision, cohort.CohortId, projection.ProjectionId, projection.ObjectIds,
				out _, out failure), failure);
			cohort = KingdomPolityAuthority.Cohort(ledger, cohort.CohortId);
			projection = KingdomPolityAuthority.Projection(ledger, projection.ProjectionId);
			Assert.AreEqual(KingdomPolityCohortPhase.Abandoned, cohort.Phase);
			Assert.AreEqual(KingdomPolityProjectionPhase.Cleaned, projection.Phase);
			KingdomPolityLedger decoded = KingdomPolityCodec.DecodeEnvelope(
				KingdomPolityCodec.EncodeEnvelope(ledger));
			Assert.AreEqual(6, (byte)KingdomPolityAuthority.Cohort(decoded,
				cohort.CohortId).Phase);
			Assert.IsTrue(KingdomPolityRules.TryValidate(decoded, out failure), failure);
		}

		[Test]
		public void AbandonedReleasesAttentionAndSelectsOnlyPhysicalCleanup()
		{
			KingdomPolityLedger ledger = Scene(out KingdomPolityCohortPlan cohort,
				out KingdomPolityProjectionReceipt projection);
			Assert.IsFalse(KingdomPolityAttentionRules.TryAdmitPlan(ledger, 4, out _));
			Assert.IsTrue(KingdomPolityCohortRules.TryAbandonEndpointCohort(ledger,
				ledger.Revision, PhysicalIntent(ledger, cohort, projection), true,
				out _, out string failure), failure);
			cohort = KingdomPolityAuthority.Cohort(ledger, cohort.CohortId);
			Assert.IsTrue(KingdomPolityAttentionRules.TryAdmitPlan(ledger, 4, out failure), failure);
			Assert.AreEqual(KingdomPolityLeaseRecoveryAction.CleanupAbandonedLoaded,
				KingdomPolityExperienceRecoveryRules.Decide(cohort, cohort.SurfaceRef, false));
			Assert.AreEqual(KingdomPolityLeaseRecoveryAction.ReleaseTerminal,
				KingdomPolityExperienceRecoveryRules.Decide(cohort, null, false));
		}

		[Test]
		public void AbandonedRewardClaimFailsValidation()
		{
			KingdomPolityLedger ledger = Scene(out KingdomPolityCohortPlan cohort,
				out KingdomPolityProjectionReceipt projection);
			Assert.IsTrue(KingdomPolityCohortRules.TryAbandonEndpointCohort(ledger,
				ledger.Revision, PhysicalIntent(ledger, cohort, projection), true,
				out _, out string failure), failure);
			KingdomPolityAuthority.Cohort(ledger, cohort.CohortId).RewardEventId =
				"taf:receipt:false-semantic-reward";
			Assert.IsFalse(KingdomPolityRules.TryValidate(ledger, out failure));
			StringAssert.Contains("abandoned cohort", failure);
		}

		[Test]
		public void ConcludedEnvoyWithNoOrWrongDeathConclusionRefusesByteExactly()
		{
			KingdomPolityLedger ledger = Scene(out KingdomPolityCohortPlan cohort,
				out KingdomPolityProjectionReceipt projection);
			cohort.Phase = KingdomPolityCohortPhase.Concluded;
			cohort.RewardEventId = "taf:receipt:wrong-envoy-conclusion";
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			Assert.IsFalse(KingdomPolityDiplomacyRules.TryConcludeNeutralEnvoyDeath(ledger,
				ledger.Revision, KingdomPolityGapTestData.TermsPlan, cohort.CohortId,
				projection.ProjectionId, projection.ObjectIds[0], KingdomPolityTestData.Realm,
				230L, null, out KingdomPolityEnvoyDeathOutcome refused, out _, out _));
			Assert.AreEqual(KingdomPolityEnvoyDeathOutcome.Refused, refused);
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[TestCase("delete", true, 0, false, false, KingdomPolityCleanupEvidenceProof.Absent)]
		[TestCase("mutate", true, 1, true, false, KingdomPolityCleanupEvidenceProof.Ambiguous)]
		[TestCase("raw-null", true, 1, false, false, KingdomPolityCleanupEvidenceProof.Ambiguous)]
		[TestCase("wrong-type", true, 1, false, false, KingdomPolityCleanupEvidenceProof.Ambiguous)]
		[TestCase("dual", true, 2, true, true, KingdomPolityCleanupEvidenceProof.Ambiguous)]
		[TestCase("unscannable", false, 0, false, false,
			KingdomPolityCleanupEvidenceProof.Unscannable)]
		[TestCase("exact", true, 1, true, true, KingdomPolityCleanupEvidenceProof.Exact)]
		public void ArmedCleanupTokenAcceptsOnlyOneExactRawIntent(string cut, bool complete, int matches,
			bool exactType, bool exactValue, KingdomPolityCleanupEvidenceProof expected)
		{
			Assert.IsNotEmpty(cut);
			KingdomPolityCleanupEvidenceProof proof =
				KingdomPolityPhysicalCustodyRules.ClassifyCleanupEvidence(complete, matches,
					exactType, exactValue);
			Assert.AreEqual(expected, proof);
			Assert.AreEqual(expected == KingdomPolityCleanupEvidenceProof.Exact,
				proof == KingdomPolityCleanupEvidenceProof.Exact);
		}

		[TestCase("no-evidence", KingdomPolityCleanupEvidenceProof.Absent,
			KingdomPolityCleanupEvidenceProof.Absent)]
		[TestCase("foreign-witness", KingdomPolityCleanupEvidenceProof.Absent,
			KingdomPolityCleanupEvidenceProof.Ambiguous)]
		[TestCase("malformed-witness", KingdomPolityCleanupEvidenceProof.Absent,
			KingdomPolityCleanupEvidenceProof.Ambiguous)]
		[TestCase("malformed-intent", KingdomPolityCleanupEvidenceProof.Ambiguous,
			KingdomPolityCleanupEvidenceProof.Exact)]
		[TestCase("unscannable-intent", KingdomPolityCleanupEvidenceProof.Unscannable,
			KingdomPolityCleanupEvidenceProof.Exact)]
		[TestCase("foreign-witness-with-exact-intent", KingdomPolityCleanupEvidenceProof.Exact,
			KingdomPolityCleanupEvidenceProof.Ambiguous)]
		[TestCase("unscannable-witness-with-exact-intent",
			KingdomPolityCleanupEvidenceProof.Exact,
			KingdomPolityCleanupEvidenceProof.Unscannable)]
		public void PlannedAbsentBodyRefusesNonExactEvidenceByteExactly(
			string cut, KingdomPolityCleanupEvidenceProof intent,
			KingdomPolityCleanupEvidenceProof witness)
		{
			Assert.IsNotEmpty(cut);
			KingdomPolityLedger ledger = Scene(out _, out _);
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			Assert.IsFalse(KingdomPolityPhysicalCustodyRules.PreparedAbsenceCanRollback(
				intent, witness));
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void PlannedAbsentBodyNeedsIntentOrExactFinalWitness()
		{
			Assert.IsTrue(KingdomPolityPhysicalCustodyRules.PreparedAbsenceCanRollback(
				KingdomPolityCleanupEvidenceProof.Exact,
				KingdomPolityCleanupEvidenceProof.Absent));
			Assert.IsTrue(KingdomPolityPhysicalCustodyRules.PreparedAbsenceCanRollback(
				KingdomPolityCleanupEvidenceProof.Absent,
				KingdomPolityCleanupEvidenceProof.Exact));
		}

		[Test]
		public void FinalWitnessMutationDuringIntentClearIsNeverAcknowledged()
		{
			Assert.IsTrue(KingdomPolityPhysicalCustodyRules.CleanupIntentCanClear(
				KingdomPolityCleanupEvidenceProof.Exact,
				KingdomPolityCleanupEvidenceProof.Exact));
			Assert.IsFalse(KingdomPolityPhysicalCustodyRules.CleanupIntentClearAcknowledged(
				KingdomPolityCleanupEvidenceProof.Absent,
				KingdomPolityCleanupEvidenceProof.Ambiguous));
			Assert.IsFalse(KingdomPolityPhysicalCustodyRules.CleanupIntentClearAcknowledged(
				KingdomPolityCleanupEvidenceProof.Exact,
				KingdomPolityCleanupEvidenceProof.Exact));
		}

		[TestCase("locator", KingdomPolityCleanupEvidenceProof.Exact, true,
			KingdomPolityCleanupEvidenceProof.Exact, KingdomPolityCleanupEvidenceProof.Absent,
			KingdomPolityCleanupEvidenceProof.Exact, KingdomPolityCleanupEvidenceProof.Exact,
			KingdomPolityCleanupEvidenceProof.Absent, KingdomPolityCleanupEvidenceProof.Exact,
			false)]
		[TestCase("local-zone", KingdomPolityCleanupEvidenceProof.Absent, false,
			KingdomPolityCleanupEvidenceProof.Exact, KingdomPolityCleanupEvidenceProof.Absent,
			KingdomPolityCleanupEvidenceProof.Exact, KingdomPolityCleanupEvidenceProof.Exact,
			KingdomPolityCleanupEvidenceProof.Absent, KingdomPolityCleanupEvidenceProof.Exact,
			false)]
		[TestCase("witness-before-write", KingdomPolityCleanupEvidenceProof.Absent, true,
			KingdomPolityCleanupEvidenceProof.Exact, KingdomPolityCleanupEvidenceProof.Ambiguous,
			KingdomPolityCleanupEvidenceProof.Exact, KingdomPolityCleanupEvidenceProof.Exact,
			KingdomPolityCleanupEvidenceProof.Absent, KingdomPolityCleanupEvidenceProof.Exact,
			false)]
		[TestCase("witness-after-write", KingdomPolityCleanupEvidenceProof.Absent, true,
			KingdomPolityCleanupEvidenceProof.Exact, KingdomPolityCleanupEvidenceProof.Absent,
			KingdomPolityCleanupEvidenceProof.Exact, KingdomPolityCleanupEvidenceProof.Ambiguous,
			KingdomPolityCleanupEvidenceProof.Absent, KingdomPolityCleanupEvidenceProof.Exact,
			false)]
		[TestCase("mutation-readback", KingdomPolityCleanupEvidenceProof.Absent, true,
			KingdomPolityCleanupEvidenceProof.Exact, KingdomPolityCleanupEvidenceProof.Absent,
			KingdomPolityCleanupEvidenceProof.Ambiguous, KingdomPolityCleanupEvidenceProof.Exact,
			KingdomPolityCleanupEvidenceProof.Absent, KingdomPolityCleanupEvidenceProof.Exact,
			false)]
		[TestCase("clear-throw", KingdomPolityCleanupEvidenceProof.Absent, true,
			KingdomPolityCleanupEvidenceProof.Exact, KingdomPolityCleanupEvidenceProof.Absent,
			KingdomPolityCleanupEvidenceProof.Exact, KingdomPolityCleanupEvidenceProof.Exact,
			KingdomPolityCleanupEvidenceProof.Exact, KingdomPolityCleanupEvidenceProof.Exact,
			false)]
		[TestCase("foreign-replacement", KingdomPolityCleanupEvidenceProof.Absent, true,
			KingdomPolityCleanupEvidenceProof.Exact, KingdomPolityCleanupEvidenceProof.Absent,
			KingdomPolityCleanupEvidenceProof.Exact, KingdomPolityCleanupEvidenceProof.Exact,
			KingdomPolityCleanupEvidenceProof.Ambiguous, KingdomPolityCleanupEvidenceProof.Exact,
			false)]
		[TestCase("exact", KingdomPolityCleanupEvidenceProof.Absent, true,
			KingdomPolityCleanupEvidenceProof.Exact, KingdomPolityCleanupEvidenceProof.Absent,
			KingdomPolityCleanupEvidenceProof.Exact, KingdomPolityCleanupEvidenceProof.Exact,
			KingdomPolityCleanupEvidenceProof.Absent, KingdomPolityCleanupEvidenceProof.Exact,
			true)]
		public void IntentPromotionFaultCutsAcknowledgeOnlyExactAftermath(string cut,
			KingdomPolityCleanupEvidenceProof locator, bool localAbsent,
			KingdomPolityCleanupEvidenceProof initialIntent,
			KingdomPolityCleanupEvidenceProof initialWitness,
			KingdomPolityCleanupEvidenceProof intentBeforeClear,
			KingdomPolityCleanupEvidenceProof witnessBeforeClear,
			KingdomPolityCleanupEvidenceProof intentAfterClear,
			KingdomPolityCleanupEvidenceProof witnessAfterClear, bool expected)
		{
			Assert.IsNotEmpty(cut);
			bool actual = locator == KingdomPolityCleanupEvidenceProof.Absent && localAbsent &&
				KingdomPolityPhysicalCustodyRules.PreparedAbsenceCanRollback(initialIntent,
					initialWitness) && KingdomPolityPhysicalCustodyRules.CleanupIntentCanClear(
					intentBeforeClear, witnessBeforeClear) &&
				KingdomPolityPhysicalCustodyRules.CleanupIntentClearAcknowledged(
					intentAfterClear, witnessAfterClear);
			Assert.AreEqual(expected, actual);
		}

		[Test]
		public void ForeignIntentReplacementRemainsUntouched()
		{
			object foreign = new object(); object slot = foreign;
			if (KingdomPolityPhysicalCustodyRules.CleanupIntentCanClear(
				KingdomPolityCleanupEvidenceProof.Ambiguous,
				KingdomPolityCleanupEvidenceProof.Exact)) slot = null;
			Assert.AreSame(foreign, slot);
		}

		[TestCase(false, 0, KingdomPolityCleanupEvidenceProof.Unscannable)]
		[TestCase(true, 0, KingdomPolityCleanupEvidenceProof.Absent)]
		[TestCase(true, 1, KingdomPolityCleanupEvidenceProof.Exact)]
		[TestCase(true, 2, KingdomPolityCleanupEvidenceProof.Ambiguous)]
		public void BoundedResidentLookupRefusesDuplicateAndScanExhaustion(bool complete,
			int matches, KingdomPolityCleanupEvidenceProof expected)
		{
			Assert.AreEqual(expected,
				KingdomPolityPhysicalCustodyRules.ClassifyResidentEvidence(complete, matches));
		}

		[Test]
		public void CachedAndUncachedDuplicateIdRefusesEvenWhenNativeCacheWouldReturnFirst()
		{
			const int cachedMatches = 1, uncachedMatches = 1;
			Assert.AreEqual(KingdomPolityCleanupEvidenceProof.Ambiguous,
				KingdomPolityPhysicalCustodyRules.ClassifyResidentEvidence(true,
					cachedMatches + uncachedMatches));
		}

		[Test]
		public void CleanupIntentAndFinalWitnessHaveFrozenV1GoldenBytes()
		{
			const string projection = "taf:projection:cleanup-golden";
			const string body = "taf:object:cleanup-golden";
			Assert.AreEqual(
				"r_TAF_PolityCleanupIntent_v1:c489599ea039178dcaa03dbcfaf1077a0acbcbccbfd0102e758a4992ba1ba715",
				KingdomPolityPhysicalCustodyRules.CleanupIntentKey(projection, body));
			Assert.AreEqual(
				"taf:intent:polity-cleanup:v1:dd1402f7c3b4d2c449a40667a96a4f416d9c3c35c4de93a8051aa73b88263834",
				KingdomPolityPhysicalCustodyRules.PreparedCleanupIntent(
					"taf:realm:v1:cleanup-golden", "taf:cohort:cleanup-golden", projection,
					"zone/cleanup-golden", body, 2, 17, 23, 1, 1));
			Assert.AreEqual(
				"taf:receipt:polity-body-removal-witness:v1:8f039b4e116ed4da749d5273caf1ccc0cb02c4d3eeea25b93ebf6b1296c012f7",
				KingdomPolityPhysicalCustodyRules.RemovalWitness(
					KingdomPolityPhysicalCustodyRules.CleanupRemovalKind,
					"taf:realm:v1:cleanup-golden", "taf:cohort:cleanup-golden", projection,
					"zone/cleanup-golden", body, 2));
		}

		[TestCase("setter-after-write", true, true, true, true, false,
			KingdomPolityLegacyRewriteRecovery.Applied)]
		[TestCase("setter-before-write", true, true, true, false, true,
			KingdomPolityLegacyRewriteRecovery.OldBytesPreserved)]
		[TestCase("setter-corrupt-write", true, true, true, false, false,
			KingdomPolityLegacyRewriteRecovery.Ambiguous)]
		[TestCase("setter-unreadable", false, false, false, false, false,
			KingdomPolityLegacyRewriteRecovery.Ambiguous)]
		public void LegacySetterFaultAcceptsOnlyExactNewOrByteExactOld(string cut, bool read,
			bool present, bool exactType, bool exactCurrent, bool exactLegacy,
			KingdomPolityLegacyRewriteRecovery expected)
		{
			Assert.IsNotEmpty(cut);
			Assert.AreEqual(expected,
				KingdomPolityPhysicalCustodyRules.ClassifyLegacyRewriteRecovery(read,
					present, exactType, exactCurrent, exactLegacy));
		}

		[Test]
		public void PreparedRollbackConflictPreservesBytesThenExactRevisionRetries()
		{
			KingdomPolityLedger ledger = PreparedScene(out KingdomPolityCohortPlan cohort,
				out KingdomPolityProjectionReceipt projection);
			KingdomPolityCleanupEvidenceProof finalBodyWitness =
				KingdomPolityCleanupEvidenceProof.Exact;
			KingdomPolityCleanupEvidenceProof finalGearWitness =
				KingdomPolityCleanupEvidenceProof.Exact;
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			Assert.IsFalse(KingdomPolityCohortRules.TryRollbackPreparedEndpointManifestation(
				ledger, ledger.Revision - 1L, cohort.CohortId, projection.ProjectionId,
				projection.ZoneId, projection.ObjectIds, out KingdomPolityPublicationResult conflict,
				out string failure));
			Assert.AreEqual(KingdomPolityCasOutcome.Conflict, conflict.Outcome);
			Assert.AreEqual(KingdomPolityCleanupEvidenceProof.Exact, finalBodyWitness);
			Assert.AreEqual(KingdomPolityCleanupEvidenceProof.Exact, finalGearWitness);
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
			Assert.IsTrue(KingdomPolityCohortRules.TryRollbackPreparedEndpointManifestation(
				ledger, ledger.Revision, cohort.CohortId, projection.ProjectionId,
				projection.ZoneId, projection.ObjectIds, out KingdomPolityPublicationResult retry,
				out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.Applied, retry.Outcome);
		}

		private static KingdomPolityLedger PreparedScene(out KingdomPolityCohortPlan Cohort,
			out KingdomPolityProjectionReceipt Projection)
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			Cohort = KingdomPolityAuthority.Cohort(ledger, KingdomPolityTestData.Cohort);
			KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(ledger,
				KingdomPolityTestData.Route);
			Cohort.SurfaceRef = route.DestinationId;
			Assert.IsTrue(KingdomPolityRules.TryValidate(ledger, out string failure), failure);
			Assert.IsTrue(KingdomPolityManifestRules.TryCreateErrandProof(
				"taf:manifest-proof:cleanup-rollback", "taf:office:rival",
				route.ManifestOrErrandId, out KingdomPolityManifestProof errand, out failure), failure);
			Assert.IsTrue(KingdomPolityRouteRules.TryDepart(ledger, ledger.Revision,
				route.RouteId, 1200L, "taf:receipt:cleanup-rollback-departed", errand,
				out KingdomPolityPublicationResult _, out failure), failure);
			Assert.IsTrue(KingdomPolityRouteRules.TryAdvance(ledger, ledger.Revision,
				route.RouteId, 0, 1200L, 1200L, out _, out failure), failure);
			Assert.IsTrue(KingdomPolityCohortRules.TryPrepareEndpointManifestation(ledger,
				ledger.Revision, Cohort.CohortId, KingdomPolityGapTestData.Zone, 1201L,
				out KingdomPolityPublicationResult prepared, out failure), failure);
			Cohort = KingdomPolityAuthority.Cohort(ledger, Cohort.CohortId);
			Projection = KingdomPolityAuthority.Projection(ledger, prepared.ProjectionId);
			return ledger;
		}

		private static KingdomPolityLedger Scene(out KingdomPolityCohortPlan Cohort,
			out KingdomPolityProjectionReceipt Projection)
		{
			KingdomPolityLedger ledger = KingdomPolityGapTestData.TermsAwaitingAnswer(
				KingdomPolityRelationBand.Contact);
			Cohort = KingdomPolityAuthority.Cohort(ledger, KingdomPolityGapTestData.Envoy);
			Projection = KingdomPolityAuthority.Projection(ledger,
				Cohort.ManifestationReceiptId);
			return ledger;
		}

		private static KingdomPolityDeathIntentRecord PhysicalIntent(KingdomPolityLedger Ledger,
			KingdomPolityCohortPlan Cohort, KingdomPolityProjectionReceipt Projection)
		{
			KingdomPolityDeathIntentRecord record = Record();
			record.RealmId = Ledger.RealmId; record.CohortId = Cohort.CohortId;
			record.ProjectionId = Projection.ProjectionId; record.ZoneId = Projection.ZoneId;
			record.ObjectId = KingdomPolityCohortRules.PreparedObjectId(Cohort, 0);
			record.Ordinal = 0; record.Purpose = Cohort.Purpose; record.Representative = true;
			record.Tick = Projection.CommittedTick + 1L;
			record.Attribution = KingdomPolityDeathAttribution.Unattributed;
			record.Visibility = KingdomPolityDeathVisibility.PhysicalOnly;
			return record;
		}

		private static KingdomPolityDeathIntentRecord Record()
		{
			return new KingdomPolityDeathIntentRecord
			{
				Kind = KingdomPolityPhysicalCustodyRules.DeathRemovalKind,
				RealmId = "taf:realm:v1:death-wire", CohortId = "taf:cohort:death-wire",
				ProjectionId = "taf:projection:death-wire", ZoneId = "zone/death-wire",
				ObjectId = "taf:object:death-wire", Ordinal = 0,
				Purpose = KingdomPolityCohortPurpose.Envoy, Representative = true,
				Tick = 150L, Attribution = KingdomPolityDeathAttribution.PlayerWitnessed,
				Visibility = KingdomPolityDeathVisibility.PlayerVisible,
				IncidentPlanId = "taf:incident-plan:death-wire",
				IncidentId = "taf:incident:death-wire",
				IncidentDigest = new string('a', 64)
			};
		}
	}
}
#endif
