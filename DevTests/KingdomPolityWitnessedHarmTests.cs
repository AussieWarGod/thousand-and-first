#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityWitnessedHarmTests
	{
		[Test]
		public void ExactProjectedEnvoyHarmPublishesOneAtomicRetryableGrievance()
		{
			KingdomPolityLedger ledger = KingdomPolityGapTestData.TermsAwaitingAnswer(
				KingdomPolityRelationBand.Contact);
			KingdomPolityCohortPlan envoy = KingdomPolityAuthority.Cohort(ledger,
				KingdomPolityGapTestData.Envoy);
			KingdomPolityProjectionReceipt projection = KingdomPolityAuthority.Projection(
				ledger, envoy.ManifestationReceiptId);
			string body = projection.ObjectIds[0]; long revision = ledger.Revision;
			ClassicAssert.IsTrue(KingdomPolityDiplomacyRules.TryRecordWitnessedEnvoyHarm(ledger,
				revision, KingdomPolityGapTestData.TermsPlan, envoy.CohortId,
				projection.ProjectionId, body, KingdomPolityTestData.Realm, 230L,
				null, out KingdomPolityEnvoyDeathOutcome outcome, out string grievanceId,
				out KingdomPolityPublicationResult result,
				out string failure), failure);
			ClassicAssert.AreEqual(KingdomPolityEnvoyDeathOutcome.Committed, outcome);
			ClassicAssert.AreEqual(revision + 1L, ledger.Revision);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Applied, result.Outcome);
			envoy = KingdomPolityAuthority.Cohort(ledger, KingdomPolityGapTestData.Envoy);
			KingdomPolityIncidentRecord plan = KingdomPolityGapTestData.Incident(ledger,
				KingdomPolityGapTestData.TermsPlan);
			KingdomPolityGrievanceRecord original = Find(ledger,
				"taf:grievance:caused-crossing");
			KingdomPolityGrievanceRecord grievance = Find(ledger, grievanceId);
			ClassicAssert.AreEqual(KingdomPolityGrievancePhase.Resolved, original.Phase);
			ClassicAssert.AreEqual(plan.Conclusion.ConclusionId, original.ResolutionRef);
			ClassicAssert.AreEqual(KingdomPolityCohortPhase.Concluded, envoy.Phase);
			ClassicAssert.AreEqual(plan.Conclusion.ReceiptRefs[0], envoy.RewardEventId);
			ClassicAssert.AreEqual(KingdomPolityGrievanceCause.WitnessedHarm, grievance.Cause);
			ClassicAssert.AreEqual(KingdomPolityTestData.Rival, grievance.IssuerPolityId);
			ClassicAssert.AreEqual(KingdomPolityTestData.Realm, grievance.TargetPolityId);
			StringAssert.StartsWith("taf:fact:witnessed:envoy-harm:v1:",
				grievance.SourceEventId);
			CollectionAssert.Contains(grievance.EvidenceRefs, body);
			CollectionAssert.Contains(grievance.EvidenceRefs, projection.ProjectionId);
			byte[] committed = KingdomPolityCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsTrue(KingdomPolityDiplomacyRules.TryRecordWitnessedEnvoyHarm(ledger,
				revision, KingdomPolityGapTestData.TermsPlan, envoy.CohortId,
				projection.ProjectionId, body, KingdomPolityTestData.Realm, 230L,
				null, out outcome, out string retryId, out result, out failure), failure);
			ClassicAssert.AreEqual(grievanceId, retryId);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
			CollectionAssert.AreEqual(committed, KingdomPolityCodec.EncodeEnvelope(ledger));
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
		}

		[Test]
		public void ForeignBodyStaleCasAndFullLedgerRemainByteIdentical()
		{
			KingdomPolityLedger wrong = KingdomPolityGapTestData.TermsAwaitingAnswer(
				KingdomPolityRelationBand.Contact);
			KingdomPolityCohortPlan envoy = KingdomPolityAuthority.Cohort(wrong,
				KingdomPolityGapTestData.Envoy);
			KingdomPolityProjectionReceipt projection = KingdomPolityAuthority.Projection(
				wrong, envoy.ManifestationReceiptId);
			byte[] before = KingdomPolityCodec.EncodeEnvelope(wrong);
			ClassicAssert.IsFalse(KingdomPolityDiplomacyRules.TryRecordWitnessedEnvoyHarm(wrong,
				wrong.Revision, KingdomPolityGapTestData.TermsPlan, envoy.CohortId,
				projection.ProjectionId, "taf:object:polity-cohort:v1:foreign",
				KingdomPolityTestData.Realm, 230L, null,
				out KingdomPolityEnvoyDeathOutcome _, out string _,
				out KingdomPolityPublicationResult _, out string failure));
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(wrong));
			ClassicAssert.IsFalse(KingdomPolityDiplomacyRules.TryRecordWitnessedEnvoyHarm(wrong,
				wrong.Revision - 1L, KingdomPolityGapTestData.TermsPlan, envoy.CohortId,
				projection.ProjectionId, projection.ObjectIds[0], KingdomPolityTestData.Realm,
				230L, null, out _, out _, out _, out failure));
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(wrong));

			KingdomPolityLedger full = KingdomPolityGapTestData.TermsAwaitingAnswer(
				KingdomPolityRelationBand.Contact);
			FillToCapacity(full); envoy = KingdomPolityAuthority.Cohort(full,
				KingdomPolityGapTestData.Envoy); projection = KingdomPolityAuthority.Projection(
				full, envoy.ManifestationReceiptId); before = KingdomPolityCodec.EncodeEnvelope(full);
			ClassicAssert.IsTrue(KingdomPolityDiplomacyRules.TryRecordWitnessedEnvoyHarm(full,
				full.Revision, KingdomPolityGapTestData.TermsPlan, envoy.CohortId,
				projection.ProjectionId, projection.ObjectIds[0], KingdomPolityTestData.Realm,
				230L, null, out KingdomPolityEnvoyDeathOutcome pending, out _, out _,
				out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityEnvoyDeathOutcome.PendingRecovery, pending);
			CollectionAssert.AreNotEqual(before, KingdomPolityCodec.EncodeEnvelope(full));
		}

		[Test]
		public void IngressAloneCannotForgeWitnessedHarm()
		{
			KingdomPolityLedger ledger = KingdomPolityGapTestData.TermsAwaitingAnswer(
				KingdomPolityRelationBand.Contact);
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			KingdomPolityGrievanceIngressRequest request =
				new KingdomPolityGrievanceIngressRequest
				{
					SourceKind = KingdomPolityGrievanceSourceKind.WitnessedEnvoyHarm,
					SourceRef = KingdomPolityGapTestData.TermsPlan,
					SourceReceiptId = "taf:receipt:polity-envoy-harm:v1:forged",
					IssuerPolityId = KingdomPolityTestData.Rival,
					TargetPolityId = KingdomPolityTestData.Realm
				};
			ClassicAssert.IsFalse(KingdomPolityDiplomacyRules.TryIngestExactGrievance(ledger,
				ledger.Revision, request, out string _, out KingdomPolityPublicationResult _,
				out string failure));
			StringAssert.Contains("exact loaded audience", failure);
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		private static KingdomPolityGrievanceRecord Find(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; i < L.Grievances.Count; i++)
				if (L.Grievances[i].GrievanceId == Id) return L.Grievances[i];
			return null;
		}

		private static void FillToCapacity(KingdomPolityLedger L)
		{
			for (int i = L.Grievances.Count; i < KingdomPolityRules.MaxGrievances; i++)
				L.Grievances.Add(new KingdomPolityGrievanceRecord
				{
					GrievanceId = "taf:grievance:harm-capacity:" + i.ToString("D3"),
					IssuerPolityId = KingdomPolityTestData.Rival,
					TargetPolityId = KingdomPolityTestData.Realm,
					Cause = KingdomPolityGrievanceCause.Claim,
					SourceEventId = "taf:event:harm-capacity:" + i.ToString("D3"), Severity = 1,
					EvidenceRefs = new List<string>
						{ "taf:evidence:harm-capacity:" + i.ToString("D3") },
					Phase = KingdomPolityGrievancePhase.Open
				});
			L.Grievances.Sort((a, b) => string.CompareOrdinal(a.GrievanceId, b.GrievanceId));
			ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(L, out string failure), failure);
		}
	}
}
#endif
