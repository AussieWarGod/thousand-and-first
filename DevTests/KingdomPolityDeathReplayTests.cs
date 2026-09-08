#if TAF_TESTS
using System;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityDeathReplayTests
	{
		[Test]
		public void CanonicalEnvelopeRoundTripsEveryFrozenField()
		{
			KingdomPolityDeathIntentRecord expected = Record();
			ClassicAssert.IsTrue(KingdomPolityDeathIntentRules.TryEncode(expected,
				out string wire, out string failure), failure);
			StringAssert.StartsWith(KingdomPolityDeathIntentRules.WirePrefix, wire);
			ClassicAssert.IsTrue(KingdomPolityDeathIntentRules.TryDecode(wire,
				out KingdomPolityDeathIntentRecord actual, out failure), failure);
			ClassicAssert.AreEqual(expected.Kind, actual.Kind);
			ClassicAssert.AreEqual(expected.RealmId, actual.RealmId);
			ClassicAssert.AreEqual(expected.CohortId, actual.CohortId);
			ClassicAssert.AreEqual(expected.ProjectionId, actual.ProjectionId);
			ClassicAssert.AreEqual(expected.ZoneId, actual.ZoneId);
			ClassicAssert.AreEqual(expected.ObjectId, actual.ObjectId);
			ClassicAssert.AreEqual(expected.Ordinal, actual.Ordinal);
			ClassicAssert.AreEqual(expected.Purpose, actual.Purpose);
			ClassicAssert.AreEqual(expected.Representative, actual.Representative);
			ClassicAssert.AreEqual(expected.Tick, actual.Tick);
			ClassicAssert.AreEqual(expected.Attribution, actual.Attribution);
			ClassicAssert.AreEqual(expected.Visibility, actual.Visibility);
			ClassicAssert.AreEqual(expected.IncidentPlanId, actual.IncidentPlanId);
			ClassicAssert.AreEqual(expected.IncidentId, actual.IncidentId);
			ClassicAssert.AreEqual(expected.IncidentDigest, actual.IncidentDigest);
			ClassicAssert.IsTrue(KingdomPolityDeathIntentRules.TryEncode(actual,
				out string repeated, out failure), failure);
			ClassicAssert.AreEqual(wire, repeated);
		}

		[Test]
		public void DigestTamperAndFutureEnvelopeFailClosed()
		{
			ClassicAssert.IsTrue(KingdomPolityDeathIntentRules.TryEncode(Record(),
				out string wire, out string failure), failure);
			char replacement = wire[wire.Length - 1] == '0' ? '1' : '0';
			string tampered = wire.Substring(0, wire.Length - 1) + replacement;
			ClassicAssert.IsFalse(KingdomPolityDeathIntentRules.TryDecode(tampered, out _, out _));
			string future = wire.Replace(":v2:", ":v3:");
			ClassicAssert.IsFalse(KingdomPolityDeathIntentRules.TryDecode(future, out _, out _));
			ClassicAssert.AreEqual(KingdomPolityDeathIntentState.Ambiguous,
				KingdomPolityDeathIntentRules.Classify(true, true, false, false));
		}

		[Test]
		public void LegacyV1PhysicalIntentDecodesOnlyAsBoundedMigrationEvidence()
		{
			KingdomPolityDeathIntentRecord source = Record();
			source.Visibility = KingdomPolityDeathVisibility.PhysicalOnly;
			source.Attribution = KingdomPolityDeathAttribution.Unattributed;
			string wire = KingdomPolityDeathIntentRules.EncodeV1Fixture(source);
			ClassicAssert.IsTrue(KingdomPolityDeathIntentRules.TryDecode(wire,
				out KingdomPolityDeathIntentRecord decoded, out string failure), failure);
			ClassicAssert.AreEqual(KingdomPolityDeathIntentProvenance.LegacyV1, decoded.Provenance);
			ClassicAssert.AreEqual("", decoded.IncidentPlanId);
			ClassicAssert.AreEqual("", decoded.IncidentId);
			ClassicAssert.AreEqual("", decoded.IncidentDigest);

			// A v1 record cannot re-encode until a first-read freeze stamps its provenance, and
			// when it does the bytes carry the migrated prefix, never the death-time one.
			ClassicAssert.IsFalse(KingdomPolityDeathIntentRules.TryEncode(decoded, out _, out failure));
			StringAssert.Contains("freeze at first read", failure);
			decoded.Provenance = KingdomPolityDeathIntentProvenance.FrozenAtFirstRead;
			ClassicAssert.IsTrue(KingdomPolityDeathIntentRules.TryEncode(decoded,
				out string rewritten, out failure), failure);
			StringAssert.StartsWith(KingdomPolityDeathIntentRules.MigratedWirePrefix, rewritten);
			StringAssert.DoesNotStartWith(KingdomPolityDeathIntentRules.WirePrefix, rewritten);
		}

		[Test]
		public void TruncatedAndOversizedEnvelopeFailClosed()
		{
			ClassicAssert.IsTrue(KingdomPolityDeathIntentRules.TryEncode(Record(),
				out string wire, out string failure), failure);
			ClassicAssert.IsFalse(KingdomPolityDeathIntentRules.TryDecode(
				wire.Substring(0, wire.Length - 3), out _, out _));
			ClassicAssert.IsFalse(KingdomPolityDeathIntentRules.TryDecode(new string('x',
				KingdomPolityDeathIntentRules.MaximumWireCharacters + 1), out _, out _));
		}

		[Test]
		public void InvalidUtf8WithFreshDigestStillFailsClosed()
		{
			ClassicAssert.IsTrue(KingdomPolityDeathIntentRules.TryEncode(Record(),
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
			ClassicAssert.IsFalse(KingdomPolityDeathIntentRules.TryDecode(invalid, out _, out _));
		}

		[Test]
		public void OversizedOrNonUtf16FieldCannotEncode()
		{
			KingdomPolityDeathIntentRecord record = Record();
			record.ZoneId = new string('z', KingdomPolityDeathIntentRules.MaximumFieldBytes + 1);
			ClassicAssert.IsFalse(KingdomPolityDeathIntentRules.TryEncode(record, out _, out _));
			record = Record(); record.ZoneId = "zone-\ud800";
			ClassicAssert.IsFalse(KingdomPolityDeathIntentRules.TryEncode(record, out _, out _));
		}

		[Test]
		public void SlotClassifierPreservesWrongTypedMalformedAndForeignAuthority()
		{
			ClassicAssert.AreEqual(KingdomPolityDeathIntentState.Clear,
				KingdomPolityDeathIntentRules.Classify(false, false, false, false));
			ClassicAssert.AreEqual(KingdomPolityDeathIntentState.Ambiguous,
				KingdomPolityDeathIntentRules.Classify(true, false, false, false));
			ClassicAssert.AreEqual(KingdomPolityDeathIntentState.Ambiguous,
				KingdomPolityDeathIntentRules.Classify(true, true, false, false));
			ClassicAssert.AreEqual(KingdomPolityDeathIntentState.Ambiguous,
				KingdomPolityDeathIntentRules.Classify(true, true, true, false));
			ClassicAssert.AreEqual(KingdomPolityDeathIntentState.Outstanding,
				KingdomPolityDeathIntentRules.Classify(true, true, true, true));
		}

		[Test]
		public void ExactTupleAndCausalTickRejectDrift()
		{
			KingdomPolityDeathIntentRecord record = Record();
			ClassicAssert.IsTrue(KingdomPolityDeathIntentRules.ExactBinding(record, record.RealmId,
				record.CohortId, record.ProjectionId, record.ZoneId, record.ObjectId,
				record.Ordinal, record.Purpose, record.Representative));
			ClassicAssert.IsFalse(KingdomPolityDeathIntentRules.ExactBinding(record, record.RealmId,
				record.CohortId, "taf:projection:foreign", record.ZoneId, record.ObjectId,
				record.Ordinal, record.Purpose, record.Representative));
			ClassicAssert.IsTrue(KingdomPolityDeathIntentRules.CausalTick(record, 100L, 200L));
			ClassicAssert.IsFalse(KingdomPolityDeathIntentRules.CausalTick(record, 151L, 200L));
			ClassicAssert.IsFalse(KingdomPolityDeathIntentRules.CausalTick(record, 100L, 149L));
		}

		[Test]
		public void FrozenVisibilityAndAttributionSelectOnlyOwnedConsequences()
		{
			KingdomPolityDeathIntentRecord record = Record();
			ClassicAssert.AreEqual(KingdomPolityDeathIntentAction.ReplayEnvoy,
				KingdomPolityDeathIntentRules.Decide(record,
					KingdomPolityCohortPhase.Materialized));
			record.Purpose = KingdomPolityCohortPurpose.Warband;
			ClassicAssert.AreEqual(KingdomPolityDeathIntentAction.ReplayWarband,
				KingdomPolityDeathIntentRules.Decide(record,
					KingdomPolityCohortPhase.Concluded));
			record.Ordinal = 1; record.Representative = false;
			ClassicAssert.AreEqual(KingdomPolityDeathIntentAction.Clear,
				KingdomPolityDeathIntentRules.Decide(record,
					KingdomPolityCohortPhase.Materialized));
			record.Visibility = KingdomPolityDeathVisibility.PhysicalOnly;
			record.IncidentPlanId = record.IncidentId = record.IncidentDigest = "";
			record.Attribution = KingdomPolityDeathAttribution.Unattributed;
			ClassicAssert.AreEqual(KingdomPolityDeathIntentAction.Abandon,
				KingdomPolityDeathIntentRules.Decide(record,
					KingdomPolityCohortPhase.Materialized));
			ClassicAssert.AreEqual(KingdomPolityDeathIntentAction.Clear,
				KingdomPolityDeathIntentRules.Decide(record,
					KingdomPolityCohortPhase.Abandoned));
		}

		[Test]
		public void PhysicalOnlyIntentCannotClaimPlayerAttribution()
		{
			KingdomPolityDeathIntentRecord record = Record();
			record.Visibility = KingdomPolityDeathVisibility.PhysicalOnly;
			record.Attribution = KingdomPolityDeathAttribution.PlayerWitnessed;
			ClassicAssert.IsFalse(KingdomPolityDeathIntentRules.TryEncode(record, out _, out _));
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
			ClassicAssert.IsFalse(KingdomPolityDeathIncidentRules.TryFreeze(ledger, cohort, 0, true,
				out _, out _, out _, out string failure));
			StringAssert.Contains("multiple open incident authorities", failure);
		}

		[Test]
		public void OffscreenRepresentativeNeedsNoIncidentAndFreezesEmptyTuple()
		{
			KingdomPolityLedger ledger = Scene(out KingdomPolityCohortPlan cohort,
				out KingdomPolityProjectionReceipt _);
			ledger.Incidents.Clear();
			ClassicAssert.IsTrue(KingdomPolityDeathIncidentRules.TryFreeze(ledger, cohort, 0, false,
				out string plan, out string incident, out string digest, out string failure), failure);
			ClassicAssert.AreEqual("", plan); ClassicAssert.AreEqual("", incident); ClassicAssert.AreEqual("", digest);
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
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryAbandonEndpointCohort(ledger,
				ledger.Revision, intent, true, out KingdomPolityPublicationResult result,
				out string failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Applied, result.Outcome);
			cohort = KingdomPolityAuthority.Cohort(ledger, cohort.CohortId);
			ClassicAssert.AreEqual(KingdomPolityCohortPhase.Abandoned, cohort.Phase);
			ClassicAssert.IsNull(cohort.RewardEventId);
			ClassicAssert.IsNull(terms.Conclusion);
			ClassicAssert.AreEqual(route, KingdomPolityGapTestData.RouteRecord(ledger).Phase);
			ClassicAssert.AreEqual(grievancePhase, grievance.Phase);
			ClassicAssert.AreEqual(consumed, grievance.ConsumedByIncidentId);
		}

		[Test]
		public void AbandonmentRefusesMissingWitnessOrVisibleClaimByteExactly()
		{
			KingdomPolityLedger ledger = Scene(out KingdomPolityCohortPlan cohort,
				out KingdomPolityProjectionReceipt projection);
			KingdomPolityDeathIntentRecord intent = PhysicalIntent(ledger, cohort, projection);
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsFalse(KingdomPolityCohortRules.TryAbandonEndpointCohort(ledger,
				ledger.Revision, intent, false, out _, out _));
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
			intent.Visibility = KingdomPolityDeathVisibility.PlayerVisible;
			ClassicAssert.IsFalse(KingdomPolityCohortRules.TryAbandonEndpointCohort(ledger,
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
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryAbandonEndpointCohort(ledger, revision,
				intent, true, out _, out string failure), failure);
			byte[] abandoned = KingdomPolityCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryAbandonEndpointCohort(ledger, revision,
				intent, true, out KingdomPolityPublicationResult repeated, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, repeated.Outcome);
			CollectionAssert.AreEqual(abandoned, KingdomPolityCodec.EncodeEnvelope(ledger));
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryCommitEndpointCleanup(ledger,
				ledger.Revision, cohort.CohortId, projection.ProjectionId, projection.ObjectIds,
				out _, out failure), failure);
			cohort = KingdomPolityAuthority.Cohort(ledger, cohort.CohortId);
			projection = KingdomPolityAuthority.Projection(ledger, projection.ProjectionId);
			ClassicAssert.AreEqual(KingdomPolityCohortPhase.Abandoned, cohort.Phase);
			ClassicAssert.AreEqual(KingdomPolityProjectionPhase.Cleaned, projection.Phase);
			KingdomPolityLedger decoded = KingdomPolityCodec.DecodeEnvelope(
				KingdomPolityCodec.EncodeEnvelope(ledger));
			ClassicAssert.AreEqual(6, (byte)KingdomPolityAuthority.Cohort(decoded,
				cohort.CohortId).Phase);
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(decoded, out failure), failure);
		}

		[Test]
		public void AbandonedReleasesAttentionAndSelectsOnlyPhysicalCleanup()
		{
			KingdomPolityLedger ledger = Scene(out KingdomPolityCohortPlan cohort,
				out KingdomPolityProjectionReceipt projection);
			ClassicAssert.IsFalse(KingdomPolityAttentionRules.TryAdmitPlan(ledger, 4, out _));
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryAbandonEndpointCohort(ledger,
				ledger.Revision, PhysicalIntent(ledger, cohort, projection), true,
				out _, out string failure), failure);
			cohort = KingdomPolityAuthority.Cohort(ledger, cohort.CohortId);
			ClassicAssert.IsTrue(KingdomPolityAttentionRules.TryAdmitPlan(ledger, 4, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityLeaseRecoveryAction.CleanupAbandonedLoaded,
				KingdomPolityExperienceRecoveryRules.Decide(cohort, cohort.SurfaceRef, false));
			ClassicAssert.AreEqual(KingdomPolityLeaseRecoveryAction.ReleaseTerminal,
				KingdomPolityExperienceRecoveryRules.Decide(cohort, null, false));
		}

		[Test]
		public void AbandonedRewardClaimFailsValidation()
		{
			KingdomPolityLedger ledger = Scene(out KingdomPolityCohortPlan cohort,
				out KingdomPolityProjectionReceipt projection);
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryAbandonEndpointCohort(ledger,
				ledger.Revision, PhysicalIntent(ledger, cohort, projection), true,
				out _, out string failure), failure);
			KingdomPolityAuthority.Cohort(ledger, cohort.CohortId).RewardEventId =
				"taf:receipt:false-semantic-reward";
			ClassicAssert.IsFalse(KingdomPolityRules.TryValidate(ledger, out failure));
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
			ClassicAssert.IsFalse(KingdomPolityDiplomacyRules.TryConcludeNeutralEnvoyDeath(ledger,
				ledger.Revision, KingdomPolityGapTestData.TermsPlan, cohort.CohortId,
				projection.ProjectionId, projection.ObjectIds[0], KingdomPolityTestData.Realm,
				230L, null, out KingdomPolityEnvoyDeathOutcome refused, out _, out _));
			ClassicAssert.AreEqual(KingdomPolityEnvoyDeathOutcome.Refused, refused);
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
			ClassicAssert.IsNotEmpty(cut);
			KingdomPolityCleanupEvidenceProof proof =
				KingdomPolityPhysicalCustodyRules.ClassifyCleanupEvidence(complete, matches,
					exactType, exactValue);
			ClassicAssert.AreEqual(expected, proof);
			ClassicAssert.AreEqual(expected == KingdomPolityCleanupEvidenceProof.Exact,
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
			ClassicAssert.IsNotEmpty(cut);
			KingdomPolityLedger ledger = Scene(out _, out _);
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsFalse(KingdomPolityPhysicalCustodyRules.PreparedAbsenceCanRollback(
				intent, witness));
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void PlannedAbsentBodyNeedsIntentOrExactFinalWitness()
		{
			ClassicAssert.IsTrue(KingdomPolityPhysicalCustodyRules.PreparedAbsenceCanRollback(
				KingdomPolityCleanupEvidenceProof.Exact,
				KingdomPolityCleanupEvidenceProof.Absent));
			ClassicAssert.IsTrue(KingdomPolityPhysicalCustodyRules.PreparedAbsenceCanRollback(
				KingdomPolityCleanupEvidenceProof.Absent,
				KingdomPolityCleanupEvidenceProof.Exact));
		}

		[Test]
		public void FinalWitnessMutationDuringIntentClearIsNeverAcknowledged()
		{
			ClassicAssert.IsTrue(KingdomPolityPhysicalCustodyRules.CleanupIntentCanClear(
				KingdomPolityCleanupEvidenceProof.Exact,
				KingdomPolityCleanupEvidenceProof.Exact));
			ClassicAssert.IsFalse(KingdomPolityPhysicalCustodyRules.CleanupIntentClearAcknowledged(
				KingdomPolityCleanupEvidenceProof.Absent,
				KingdomPolityCleanupEvidenceProof.Ambiguous));
			ClassicAssert.IsFalse(KingdomPolityPhysicalCustodyRules.CleanupIntentClearAcknowledged(
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
			ClassicAssert.IsNotEmpty(cut);
			bool actual = locator == KingdomPolityCleanupEvidenceProof.Absent && localAbsent &&
				KingdomPolityPhysicalCustodyRules.PreparedAbsenceCanRollback(initialIntent,
					initialWitness) && KingdomPolityPhysicalCustodyRules.CleanupIntentCanClear(
					intentBeforeClear, witnessBeforeClear) &&
				KingdomPolityPhysicalCustodyRules.CleanupIntentClearAcknowledged(
					intentAfterClear, witnessAfterClear);
			ClassicAssert.AreEqual(expected, actual);
		}

		[Test]
		public void ForeignIntentReplacementRemainsUntouched()
		{
			object foreign = new object(); object slot = foreign;
			if (KingdomPolityPhysicalCustodyRules.CleanupIntentCanClear(
				KingdomPolityCleanupEvidenceProof.Ambiguous,
				KingdomPolityCleanupEvidenceProof.Exact)) slot = null;
			ClassicAssert.AreSame(foreign, slot);
		}

		[TestCase(false, 0, KingdomPolityCleanupEvidenceProof.Unscannable)]
		[TestCase(true, 0, KingdomPolityCleanupEvidenceProof.Absent)]
		[TestCase(true, 1, KingdomPolityCleanupEvidenceProof.Exact)]
		[TestCase(true, 2, KingdomPolityCleanupEvidenceProof.Ambiguous)]
		public void BoundedResidentLookupRefusesDuplicateAndScanExhaustion(bool complete,
			int matches, KingdomPolityCleanupEvidenceProof expected)
		{
			ClassicAssert.AreEqual(expected,
				KingdomPolityPhysicalCustodyRules.ClassifyResidentEvidence(complete, matches));
		}

		[Test]
		public void CachedAndUncachedDuplicateIdRefusesEvenWhenNativeCacheWouldReturnFirst()
		{
			const int cachedMatches = 1, uncachedMatches = 1;
			ClassicAssert.AreEqual(KingdomPolityCleanupEvidenceProof.Ambiguous,
				KingdomPolityPhysicalCustodyRules.ClassifyResidentEvidence(true,
					cachedMatches + uncachedMatches));
		}

		[Test]
		public void CleanupIntentAndFinalWitnessHaveFrozenV1GoldenBytes()
		{
			const string projection = "taf:projection:cleanup-golden";
			const string body = "taf:object:cleanup-golden";
			ClassicAssert.AreEqual(
				"r_TAF_PolityCleanupIntent_v1:c489599ea039178dcaa03dbcfaf1077a0acbcbccbfd0102e758a4992ba1ba715",
				KingdomPolityPhysicalCustodyRules.CleanupIntentKey(projection, body));
			ClassicAssert.AreEqual(
				"taf:intent:polity-cleanup:v1:dd1402f7c3b4d2c449a40667a96a4f416d9c3c35c4de93a8051aa73b88263834",
				KingdomPolityPhysicalCustodyRules.PreparedCleanupIntent(
					"taf:realm:v1:cleanup-golden", "taf:cohort:cleanup-golden", projection,
					"zone/cleanup-golden", body, 2, 17, 23, 1, 1));
			ClassicAssert.AreEqual(
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
			ClassicAssert.IsNotEmpty(cut);
			ClassicAssert.AreEqual(expected,
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
			ClassicAssert.IsFalse(KingdomPolityCohortRules.TryRollbackPreparedEndpointManifestation(
				ledger, ledger.Revision - 1L, cohort.CohortId, projection.ProjectionId,
				projection.ZoneId, projection.ObjectIds, out KingdomPolityPublicationResult conflict,
				out string failure));
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Conflict, conflict.Outcome);
			ClassicAssert.AreEqual(KingdomPolityCleanupEvidenceProof.Exact, finalBodyWitness);
			ClassicAssert.AreEqual(KingdomPolityCleanupEvidenceProof.Exact, finalGearWitness);
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryRollbackPreparedEndpointManifestation(
				ledger, ledger.Revision, cohort.CohortId, projection.ProjectionId,
				projection.ZoneId, projection.ObjectIds, out KingdomPolityPublicationResult retry,
				out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Applied, retry.Outcome);
		}

		private static KingdomPolityLedger PreparedScene(out KingdomPolityCohortPlan Cohort,
			out KingdomPolityProjectionReceipt Projection)
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			Cohort = KingdomPolityAuthority.Cohort(ledger, KingdomPolityTestData.Cohort);
			KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(ledger,
				KingdomPolityTestData.Route);
			Cohort.SurfaceRef = route.DestinationId;
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(ledger, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityManifestRules.TryCreateErrandProof(
				"taf:manifest-proof:cleanup-rollback", "taf:office:rival",
				route.ManifestOrErrandId, out KingdomPolityManifestProof errand, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRouteRules.TryDepart(ledger, ledger.Revision,
				route.RouteId, 1200L, "taf:receipt:cleanup-rollback-departed", errand,
				out KingdomPolityPublicationResult _, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityRouteRules.TryAdvance(ledger, ledger.Revision,
				route.RouteId, 0, 1200L, 1200L, out _, out failure), failure);
			ClassicAssert.IsTrue(KingdomPolityCohortRules.TryPrepareEndpointManifestation(ledger,
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
			// A physical-only loss freezes no incident: TryFreezeDeathIncident returns the empty
			// tuple when the death was unwitnessed, and the record refuses any incident claim
			// that no player-visible representative death authorized.
			record.IncidentPlanId = record.IncidentId = record.IncidentDigest = "";
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
