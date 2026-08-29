#if TAF_TESTS
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityGrievanceIngestionTests
	{
		[Test]
		public void RefusalAndBrokenPactPublishTogetherFromExactConclusion()
		{
			KingdomPolityLedger ledger = KingdomPolityGapTestData.OpenClash(
				KingdomPolityRelationBand.Pact);
			KingdomPolityGrievanceRecord refusal = OneCause(ledger,
				KingdomPolityGrievanceCause.RefusedTerms);
			KingdomPolityGrievanceRecord broken = OneCause(ledger,
				KingdomPolityGrievanceCause.BrokenPact);
			Assert.AreEqual(KingdomPolityTestData.Rival, refusal.IssuerPolityId);
			Assert.AreEqual(KingdomPolityTestData.Realm, refusal.TargetPolityId);
			Assert.AreEqual(KingdomPolityGapTestData.RefusalFact, refusal.SourceEventId);
			StringAssert.StartsWith("taf:receipt:polity-relation:v1:", broken.SourceEventId);
			CollectionAssert.Contains(broken.EvidenceRefs, broken.SourceEventId);
			Assert.AreEqual(KingdomPolityRelationBand.Hostile,
				KingdomPolityGapTestData.Relation(ledger).Band);
			Assert.IsTrue(KingdomPolityRules.TryValidate(ledger, out string failure), failure);
		}

		[Test]
		public void RefusalCapacityFailureCannotPartiallyCommitEitherGrievance()
		{
			KingdomPolityLedger ledger = KingdomPolityGapTestData.TermsAwaitingAnswer(
				KingdomPolityRelationBand.Pact);
			FillGrievances(ledger, KingdomPolityRules.MaxGrievances - 1);
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			long revision = ledger.Revision;
			Assert.IsFalse(KingdomPolityDiplomacyRules.TryAnswerTerms(ledger, revision,
				KingdomPolityGapTestData.TermsPlan, KingdomPolityTermsChoice.Refuse,
				KingdomPolityGapTestData.RefusalFact, 200L, null,
				out KingdomPolityPublicationResult _, out string failure));
			StringAssert.Contains("grievance", failure);
			Assert.AreEqual(revision, ledger.Revision);
			Assert.IsNull(KingdomPolityGapTestData.Incident(ledger,
				KingdomPolityGapTestData.TermsPlan).Conclusion);
			Assert.AreEqual(KingdomPolityRelationBand.Pact,
				KingdomPolityGapTestData.Relation(ledger).Band);
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void ResourceDeclineAndGrievanceUseOneCasAndRetryIdentity()
		{
			KingdomPolityLedger ledger = KingdomPolityConsignmentTests.Scene();
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryPlanConsignment(ledger,
				ledger.Revision, KingdomPolityTestData.Plan, KingdomPolityTestData.Cohort,
				KingdomPolityTestData.Settlement, out KingdomPolityConsignmentRequest request,
				out KingdomPolityPublicationResult _, out string failure), failure);
			long before = ledger.Revision;
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryDeclineConsignmentWithExactGrievance(
				ledger, before, request.CorrespondencePlanId,
				"taf:fact:witnessed:resource-declined", 90L, out string grievanceId,
				out KingdomPolityPublicationResult result, out failure), failure);
			Assert.AreEqual(before + 1L, ledger.Revision);
			Assert.AreEqual(KingdomPolityCasOutcome.Applied, result.Outcome);
			KingdomPolityGrievanceRecord grievance = Find(ledger, grievanceId);
			Assert.AreEqual(KingdomPolityGrievanceCause.ResourceRefusal, grievance.Cause);
			Assert.AreEqual("taf:fact:witnessed:resource-declined", grievance.SourceEventId);
			byte[] committed = KingdomPolityCodec.EncodeEnvelope(ledger);
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryDeclineConsignmentWithExactGrievance(
				ledger, before, request.CorrespondencePlanId,
				"taf:fact:witnessed:resource-declined", 90L, out string retryId,
				out result, out failure), failure);
			Assert.AreEqual(grievanceId, retryId);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
			CollectionAssert.AreEqual(committed, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void ResourceCapacityAndCasFailuresLeaveDeclineByteIdentical()
		{
			KingdomPolityLedger ledger = KingdomPolityConsignmentTests.Scene();
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryPlanConsignment(ledger,
				ledger.Revision, KingdomPolityTestData.Plan, KingdomPolityTestData.Cohort,
				KingdomPolityTestData.Settlement, out KingdomPolityConsignmentRequest request,
				out KingdomPolityPublicationResult _, out string failure), failure);
			byte[] beforeCas = KingdomPolityCodec.EncodeEnvelope(ledger);
			Assert.IsFalse(KingdomPolityCorrespondenceRules.TryDeclineConsignmentWithExactGrievance(
				ledger, ledger.Revision - 1L, request.CorrespondencePlanId,
				"taf:fact:witnessed:resource-declined", 90L, out string _, out _, out failure));
			CollectionAssert.AreEqual(beforeCas, KingdomPolityCodec.EncodeEnvelope(ledger));
			FillGrievances(ledger, KingdomPolityRules.MaxGrievances);
			byte[] beforeCap = KingdomPolityCodec.EncodeEnvelope(ledger);
			Assert.IsFalse(KingdomPolityCorrespondenceRules.TryDeclineConsignmentWithExactGrievance(
				ledger, ledger.Revision, request.CorrespondencePlanId,
				"taf:fact:witnessed:resource-declined", 90L, out _, out _, out failure));
			Assert.IsNull(KingdomPolityGapTestData.Incident(ledger,
				request.CorrespondencePlanId).Conclusion);
			CollectionAssert.AreEqual(beforeCap, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void WitnessedSupportAndTrespassUseOneCasAndExactReceipt()
		{
			KingdomPolityLedger ledger = KingdomPolityGapTestData.OpenClash(
				KingdomPolityRelationBand.Contact);
			KingdomPolityConsentedEscrowRequest escrow =
				KingdomPolityGapTestData.EscrowRequest(ledger, "collateral", 
					KingdomPolityTestData.DigestA, 220L);
			long before = ledger.Revision;
			Assert.IsTrue(KingdomPolityConflictRules.TryRecordWitnessedTrespass(ledger,
				before, KingdomPolityGapTestData.ClashPlan,
				KingdomPolityTestData.Settlement, KingdomPolityGapTestData.Zone, 220L,
				"taf:fact:witnessed:settlement-supported", escrow.ParticipantProjectionIds,
				KingdomPolityTestData.Realm, KingdomPolityTestData.Rival,
				out string grievanceId, out KingdomPolityPublicationResult result,
				out string failure), failure);
			Assert.AreEqual(before + 1L, ledger.Revision);
			Assert.AreEqual(KingdomPolityCasOutcome.Applied, result.Outcome);
			KingdomPolityIncidentRecord clash = KingdomPolityGapTestData.Incident(ledger,
				KingdomPolityGapTestData.ClashPlan);
			KingdomPolityGrievanceRecord grievance = Find(ledger, grievanceId);
			Assert.AreEqual(KingdomPolityGrievanceCause.Trespass, grievance.Cause);
			Assert.AreEqual(clash.Intervention.ObservedFactId, grievance.SourceEventId);
			CollectionAssert.Contains(grievance.EvidenceRefs, clash.Intervention.ReceiptId);
			Assert.IsTrue(KingdomPolityConflictRules.TryRecordWitnessedTrespass(ledger,
				before, KingdomPolityGapTestData.ClashPlan,
				KingdomPolityTestData.Settlement, KingdomPolityGapTestData.Zone, 220L,
				"taf:fact:witnessed:settlement-supported", escrow.ParticipantProjectionIds,
				KingdomPolityTestData.Realm, KingdomPolityTestData.Rival,
				out string retryId, out result, out failure), failure);
			Assert.AreEqual(grievanceId, retryId);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
		}

		[Test]
		public void SupportCapacityFailureLeavesStanceByteIdentical()
		{
			KingdomPolityLedger ledger = KingdomPolityGapTestData.OpenClash(
				KingdomPolityRelationBand.Contact);
			KingdomPolityConsentedEscrowRequest escrow =
				KingdomPolityGapTestData.EscrowRequest(ledger, "collateral",
					KingdomPolityTestData.DigestA, 220L);
			FillGrievances(ledger, KingdomPolityRules.MaxGrievances);
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			Assert.IsFalse(KingdomPolityConflictRules.TryRecordWitnessedTrespass(ledger,
				ledger.Revision, KingdomPolityGapTestData.ClashPlan,
				KingdomPolityTestData.Settlement, KingdomPolityGapTestData.Zone, 220L,
				"taf:fact:witnessed:settlement-supported", escrow.ParticipantProjectionIds,
				KingdomPolityTestData.Realm, KingdomPolityTestData.Rival,
				out string _, out KingdomPolityPublicationResult _, out string failure));
			StringAssert.Contains("capacity", failure);
			Assert.IsNull(KingdomPolityGapTestData.Incident(ledger,
				KingdomPolityGapTestData.ClashPlan).Intervention);
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void StandingAndUnwitnessedTheftCannotFabricateGrievance()
		{
			KingdomPolityLedger ledger = KingdomPolityGapTestData.OpenClash(
				KingdomPolityRelationBand.Contact);
			KingdomPolityGrievanceIngressRequest request = new KingdomPolityGrievanceIngressRequest
			{
				SourceKind = KingdomPolityGrievanceSourceKind.DesignatedTheftReceipt,
				SourceRef = "taf:standing:rival-minus-one-hundred",
				SourceReceiptId = "taf:receipt:unwitnessed-theft",
				IssuerPolityId = KingdomPolityTestData.Rival,
				TargetPolityId = KingdomPolityTestData.Realm
			};
			Assert.IsFalse(KingdomPolityDiplomacyRules.TryIngestExactGrievance(ledger,
				ledger.Revision, request, out string _, out KingdomPolityPublicationResult _,
				out string failure));
			StringAssert.Contains("no exact authored theft", failure);
		}

		private static KingdomPolityGrievanceRecord OneCause(KingdomPolityLedger L,
			KingdomPolityGrievanceCause Cause)
		{
			KingdomPolityGrievanceRecord found = null;
			for (int i = 0; i < L.Grievances.Count; i++)
				if (L.Grievances[i].Cause == Cause)
				{
					Assert.IsNull(found, "cause must have one exact receipt"); found = L.Grievances[i];
				}
			Assert.IsNotNull(found); return found;
		}

		private static KingdomPolityGrievanceRecord Find(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; i < L.Grievances.Count; i++)
				if (L.Grievances[i].GrievanceId == Id) return L.Grievances[i];
			return null;
		}

		private static void FillGrievances(KingdomPolityLedger L, int Target)
		{
			for (int i = L.Grievances.Count; i < Target; i++)
				L.Grievances.Add(new KingdomPolityGrievanceRecord
				{
					GrievanceId = "taf:grievance:capacity:" + i.ToString("D3"),
					IssuerPolityId = KingdomPolityTestData.Rival,
					TargetPolityId = KingdomPolityTestData.Realm,
					Cause = KingdomPolityGrievanceCause.Claim,
					SourceEventId = "taf:event:capacity:" + i.ToString("D3"), Severity = 1,
					EvidenceRefs = new List<string> { "taf:evidence:capacity:" + i.ToString("D3") },
					Phase = KingdomPolityGrievancePhase.Open
				});
			L.Grievances.Sort((a, b) => string.CompareOrdinal(a.GrievanceId, b.GrievanceId));
			Assert.AreEqual(Target, L.Grievances.Count);
			Assert.IsTrue(KingdomPolityRules.TryValidate(L, out string failure), failure);
		}
	}
}
#endif
