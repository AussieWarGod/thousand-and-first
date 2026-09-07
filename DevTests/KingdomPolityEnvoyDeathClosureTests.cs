#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityEnvoyDeathClosureTests
	{
		[Test]
		public void NeutralDeathWithdrawsCauseWithoutBlameRelationFrontOrWar()
		{
			KingdomPolityLedger ledger = Scene(out KingdomPolityCohortPlan envoy,
				out KingdomPolityProjectionReceipt projection, out string body);
			int grievances = ledger.Grievances.Count, fronts = ledger.Fronts.Count;
			KingdomPolityRelation relation = KingdomPolityGapTestData.Relation(ledger);
			KingdomPolityRelationBand band = relation.Band; long changed = relation.ChangedTick;
			long revision = ledger.Revision;
			ClassicAssert.IsTrue(KingdomPolityDiplomacyRules.TryConcludeNeutralEnvoyDeath(ledger,
				revision, KingdomPolityGapTestData.TermsPlan, envoy.CohortId,
				projection.ProjectionId, body, KingdomPolityTestData.Realm, 230L, null,
				out KingdomPolityEnvoyDeathOutcome outcome,
				out KingdomPolityPublicationResult result, out string failure), failure);
			ClassicAssert.AreEqual(KingdomPolityEnvoyDeathOutcome.Committed, outcome);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.Applied, result.Outcome);
			KingdomPolityIncidentRecord terms = Terms(ledger);
			StringAssert.StartsWith("taf:conclusion:envoy-death-neutral:v1:",
				terms.Conclusion.ConclusionId);
			ClassicAssert.AreEqual(0, terms.Conclusion.RelationDeltas.Count);
			ClassicAssert.AreEqual(0, terms.Conclusion.SystemicDeltas.Count);
			ClassicAssert.AreEqual(KingdomPolityGrievancePhase.Withdrawn,
				Original(ledger).Phase);
			ClassicAssert.AreEqual(terms.Conclusion.ConclusionId, Original(ledger).ResolutionRef);
			ClassicAssert.AreEqual(KingdomPolityCohortPhase.Concluded,
				KingdomPolityAuthority.Cohort(ledger, envoy.CohortId).Phase);
			ClassicAssert.AreEqual(grievances, ledger.Grievances.Count);
			ClassicAssert.AreEqual(fronts, ledger.Fronts.Count);
			ClassicAssert.AreEqual(band, relation.Band); ClassicAssert.AreEqual(changed, relation.ChangedTick);
			byte[] committed = KingdomPolityCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsTrue(KingdomPolityDiplomacyRules.TryConcludeNeutralEnvoyDeath(ledger,
				revision, KingdomPolityGapTestData.TermsPlan, envoy.CohortId,
				projection.ProjectionId, body, KingdomPolityTestData.Realm, 230L, null,
				out outcome, out result, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
			CollectionAssert.AreEqual(committed, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void DebitedHospitalityAppliesWithProofWhilePlannedRefusesByteExactly()
		{
			KingdomPolityLedger ledger = Scene(out KingdomPolityCohortPlan envoy,
				out KingdomPolityProjectionReceipt projection, out string body);
			PlanHospitality(ledger, out KingdomPolityHospitalityTransaction transaction);
			ClassicAssert.IsTrue(KingdomPolityHospitalityRules.TryCreateCommittedProof(transaction,
				"taf:fact:witnessed:envoy-last-meal", 200L,
				out KingdomPolityHospitalityProof proof, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityHospitalityRules.TryCommitDebit(ledger, ledger.Revision,
				KingdomPolityGapTestData.TermsPlan, proof, 200L,
				out KingdomPolityPublicationResult _, out failure), failure);
			AssertHarm(ledger, envoy, projection, body,
				KingdomPolityEnvoyDeathOutcome.Committed);
			ClassicAssert.AreEqual(KingdomPolityHospitalityPhase.Applied, Terms(ledger).Hospitality.Phase);
			CollectionAssert.Contains(Terms(ledger).Conclusion.ObservedFactIds,
				proof.ObservedFactId);
			CollectionAssert.Contains(Terms(ledger).Conclusion.ReceiptRefs, proof.ReceiptId);
			AssertHarm(ledger, envoy, projection, body,
				KingdomPolityEnvoyDeathOutcome.Committed);

			ledger = Scene(out envoy, out projection, out body); PlanHospitality(ledger, out _);
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsFalse(KingdomPolityDiplomacyRules.TryRecordWitnessedEnvoyHarm(ledger,
				ledger.Revision, KingdomPolityGapTestData.TermsPlan, envoy.CohortId,
				projection.ProjectionId, body, KingdomPolityTestData.Realm, 230L, null,
				out KingdomPolityEnvoyDeathOutcome refused, out string _,
				out KingdomPolityPublicationResult _, out failure));
			ClassicAssert.AreEqual(KingdomPolityEnvoyDeathOutcome.Refused, refused);
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void PredatingProjectionTickHardRejectsWithoutMutation()
		{
			KingdomPolityLedger ledger = Scene(out KingdomPolityCohortPlan envoy,
				out KingdomPolityProjectionReceipt projection, out string body);
			byte[] before = KingdomPolityCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsFalse(KingdomPolityDiplomacyRules.TryConcludeNeutralEnvoyDeath(ledger,
				ledger.Revision, KingdomPolityGapTestData.TermsPlan, envoy.CohortId,
				projection.ProjectionId, body, KingdomPolityTestData.Realm,
				projection.CommittedTick - 1L, null,
				out KingdomPolityEnvoyDeathOutcome outcome,
				out KingdomPolityPublicationResult _, out string _));
			ClassicAssert.AreEqual(KingdomPolityEnvoyDeathOutcome.Refused, outcome);
			CollectionAssert.AreEqual(before, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[TestCase(KingdomPolityHospitalityPhase.Abandoned)]
		[TestCase(KingdomPolityHospitalityPhase.Quarantined)]
		public void TerminalHospitalityDoesNotBlockExactDeath(
			KingdomPolityHospitalityPhase Phase)
		{
			KingdomPolityLedger ledger = Scene(out KingdomPolityCohortPlan envoy,
				out KingdomPolityProjectionReceipt projection, out string body);
			PlanHospitality(ledger, out _);
			if (Phase == KingdomPolityHospitalityPhase.Quarantined)
				ClassicAssert.IsTrue(KingdomPolityHospitalityRules.TryQuarantineDebit(ledger,
					ledger.Revision, KingdomPolityGapTestData.TermsPlan, "exact serving moved",
					out KingdomPolityPublicationResult _, out string failure), failure);
			else
			{
				Terms(ledger).Hospitality.Phase = KingdomPolityHospitalityPhase.Abandoned;
				ClassicAssert.IsTrue(KingdomPolityRules.TryValidate(ledger, out string failure), failure);
			}
			AssertHarm(ledger, envoy, projection, body,
				KingdomPolityEnvoyDeathOutcome.Committed);
			ClassicAssert.AreEqual(Phase, Terms(ledger).Hospitality.Phase);
		}

		[Test]
		public void FullCapacityPersistsHarmAndDeterministicallyPublishesAfterRelease()
		{
			KingdomPolityLedger ledger = Scene(out KingdomPolityCohortPlan envoy,
				out KingdomPolityProjectionReceipt projection, out string body);
			FillToCapacity(ledger); AssertHarm(ledger, envoy, projection, body,
				KingdomPolityEnvoyDeathOutcome.PendingRecovery);
			ledger = KingdomPolityCodec.DecodeEnvelope(KingdomPolityCodec.EncodeEnvelope(ledger));
			byte[] full = KingdomPolityCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsTrue(KingdomPolityDiplomacyRules.TryRecoverEnvoyDeaths(ledger,
				ledger.Revision, null, out int pending, out int published,
				out KingdomPolityPublicationResult result, out string failure), failure);
			ClassicAssert.AreEqual(1, pending); ClassicAssert.AreEqual(0, published);
			ClassicAssert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
			CollectionAssert.AreEqual(full, KingdomPolityCodec.EncodeEnvelope(ledger));
			RemoveOneCapacityFiller(ledger);
			ClassicAssert.IsTrue(KingdomPolityDiplomacyRules.TryRecoverEnvoyDeaths(ledger,
				ledger.Revision, null, out pending, out published, out result, out failure), failure);
			ClassicAssert.AreEqual(0, pending); ClassicAssert.AreEqual(1, published);
			ClassicAssert.AreEqual(1, CountHarm(ledger)); byte[] recovered =
				KingdomPolityCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsTrue(KingdomPolityDiplomacyRules.TryRecoverEnvoyDeaths(ledger,
				ledger.Revision, null, out pending, out published, out result, out failure), failure);
			ClassicAssert.AreEqual(0, published);
			CollectionAssert.AreEqual(recovered, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void OpenCorrespondenceHoldsDeathUntilExactNoCustodyProofClosesNeutrally()
		{
			KingdomPolityLedger ledger = Scene(out KingdomPolityCohortPlan envoy,
				out KingdomPolityProjectionReceipt projection, out string body);
			ClassicAssert.IsTrue(KingdomPolityCorrespondenceRules.TryPlanConsignment(ledger,
				ledger.Revision, KingdomPolityGapTestData.TermsPlan, envoy.CohortId,
				envoy.SurfaceRef, out KingdomPolityConsignmentRequest request,
				out KingdomPolityPublicationResult _, out string failure), failure);
			ClassicAssert.IsTrue(KingdomPolityDiplomacyRules.TryConcludeNeutralEnvoyDeath(ledger,
				ledger.Revision, KingdomPolityGapTestData.TermsPlan, envoy.CohortId,
				projection.ProjectionId, body, KingdomPolityTestData.Realm, 230L, null,
				out KingdomPolityEnvoyDeathOutcome outcome, out _, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityEnvoyDeathOutcome.PendingRecovery, outcome);
			ClassicAssert.AreEqual(KingdomPolityCohortPhase.Materialized,
				KingdomPolityAuthority.Cohort(ledger, envoy.CohortId).Phase);
			ledger = KingdomPolityCodec.DecodeEnvelope(KingdomPolityCodec.EncodeEnvelope(ledger));
			KingdomPolityConsignmentAbsenceProof proof = Absence(request);
			proof.ProofDigest = new string('0', 64); byte[] held =
				KingdomPolityCodec.EncodeEnvelope(ledger);
			ClassicAssert.IsFalse(KingdomPolityDiplomacyRules.TryRecoverEnvoyDeaths(ledger,
				ledger.Revision, proof, out int _, out int _, out _, out failure));
			CollectionAssert.AreEqual(held, KingdomPolityCodec.EncodeEnvelope(ledger));
			proof = Absence(request);
			ClassicAssert.IsTrue(KingdomPolityDiplomacyRules.TryRecoverEnvoyDeaths(ledger,
				ledger.Revision, proof, out _, out _, out _, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCohortPhase.Concluded,
				KingdomPolityAuthority.Cohort(ledger, envoy.CohortId).Phase);
			ClassicAssert.IsTrue(KingdomPolityCorrespondenceRules.TryDescribeConsignment(ledger,
				request.CorrespondencePlanId, out _,
				out KingdomPolityCorrespondenceReplyKind reply, out failure), failure);
			ClassicAssert.AreEqual(KingdomPolityCorrespondenceReplyKind.RecipientUnavailable, reply);
			ClassicAssert.AreEqual(1, ledger.Grievances.Count);
		}

		private static KingdomPolityLedger Scene(out KingdomPolityCohortPlan Envoy,
			out KingdomPolityProjectionReceipt Projection, out string Body)
		{
			KingdomPolityLedger ledger = KingdomPolityGapTestData.TermsAwaitingAnswer(
				KingdomPolityRelationBand.Contact);
			Envoy = KingdomPolityAuthority.Cohort(ledger, KingdomPolityGapTestData.Envoy);
			Projection = KingdomPolityAuthority.Projection(ledger, Envoy.ManifestationReceiptId);
			Body = Projection.ObjectIds[0]; return ledger;
		}

		private static void AssertHarm(KingdomPolityLedger L, KingdomPolityCohortPlan E,
			KingdomPolityProjectionReceipt P, string Body, KingdomPolityEnvoyDeathOutcome Expected)
		{
			ClassicAssert.IsTrue(KingdomPolityDiplomacyRules.TryRecordWitnessedEnvoyHarm(L,
				L.Revision, KingdomPolityGapTestData.TermsPlan, E.CohortId, P.ProjectionId,
				Body, KingdomPolityTestData.Realm, 230L, null,
				out KingdomPolityEnvoyDeathOutcome outcome, out string _,
				out KingdomPolityPublicationResult _, out string failure), failure);
			ClassicAssert.AreEqual(Expected, outcome);
		}

		private static void PlanHospitality(KingdomPolityLedger L,
			out KingdomPolityHospitalityTransaction Transaction)
		{
			KingdomPolityHospitalityPlanRequest request = new KingdomPolityHospitalityPlanRequest
			{
				SurfaceRef = KingdomPolityTestData.Settlement,
				ZoneId = KingdomPolityGapTestData.Zone, PlannedTick = 200L,
				Lines = new List<KingdomPolityHospitalityDebitLine>
				{
					new KingdomPolityHospitalityDebitLine { Kind = KingdomPolityHospitalityDebitKind.Food,
						ContainerId = "larder", ObjectId = "food", Blueprint = "Jerky",
						Before = 2, After = 1 },
					new KingdomPolityHospitalityDebitLine { Kind = KingdomPolityHospitalityDebitKind.Water,
						ContainerId = "vessel", ObjectId = "vessel", Blueprint = "Waterskin",
						Before = 4, After = 3, Capacity = 64 }
				}
			};
			ClassicAssert.IsTrue(KingdomPolityHospitalityRules.TryPlanDebit(L, L.Revision,
				KingdomPolityGapTestData.TermsPlan, request, out Transaction,
				out KingdomPolityPublicationResult _, out string failure), failure);
		}

		private static KingdomPolityConsignmentAbsenceProof Absence(
			KingdomPolityConsignmentRequest R)
		{
			KingdomPolityConsignmentAbsenceProof p = new KingdomPolityConsignmentAbsenceProof
			{
				CorrespondencePlanId = R.CorrespondencePlanId, TermsPlanId = R.TermsPlanId,
				RecipientCohortId = R.RecipientCohortId, ConsignmentId = R.ConsignmentId,
				RequestDigest = R.RequestDigest
			};
			p.ProofDigest = KingdomPolityCorrespondenceRules.ConsignmentAbsenceDigest(p); return p;
		}

		private static KingdomPolityIncidentRecord Terms(KingdomPolityLedger L) =>
			KingdomPolityGapTestData.Incident(L, KingdomPolityGapTestData.TermsPlan);
		private static KingdomPolityGrievanceRecord Original(KingdomPolityLedger L)
		{
			for (int i = 0; i < L.Grievances.Count; i++)
				if (L.Grievances[i].GrievanceId == "taf:grievance:caused-crossing") return L.Grievances[i];
			return null;
		}

		private static void FillToCapacity(KingdomPolityLedger L)
		{
			for (int i = L.Grievances.Count; i < KingdomPolityRules.MaxGrievances; i++)
				L.Grievances.Add(new KingdomPolityGrievanceRecord { GrievanceId =
					"taf:grievance:death-capacity:" + i.ToString("D3"), IssuerPolityId =
					KingdomPolityTestData.Rival, TargetPolityId = KingdomPolityTestData.Realm,
					Cause = KingdomPolityGrievanceCause.Claim, SourceEventId =
					"taf:event:death-capacity:" + i.ToString("D3"), Severity = 1,
					EvidenceRefs = new List<string> { "taf:evidence:death-capacity:" + i.ToString("D3") },
					Phase = KingdomPolityGrievancePhase.Open });
			L.Grievances.Sort((a, b) => string.CompareOrdinal(a.GrievanceId, b.GrievanceId));
		}

		private static void RemoveOneCapacityFiller(KingdomPolityLedger L)
		{
			for (int i = 0; i < L.Grievances.Count; i++)
				if (L.Grievances[i].GrievanceId.StartsWith("taf:grievance:death-capacity:",
					StringComparison.Ordinal)) { L.Grievances.RemoveAt(i); return; }
			Assert.Fail("capacity filler missing");
		}

		private static int CountHarm(KingdomPolityLedger L)
		{
			int count = 0;
			for (int i = 0; i < L.Grievances.Count; i++)
				if (L.Grievances[i].Cause == KingdomPolityGrievanceCause.WitnessedHarm) count++;
			return count;
		}
	}
}
#endif
