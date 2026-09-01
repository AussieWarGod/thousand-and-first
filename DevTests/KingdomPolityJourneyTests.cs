using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityJourneyTests
	{
		private const string Remote =
			"taf:settlement:v1:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
		private const string Route = "taf:route:semantic-delegation";
		private const string Manifest = "taf:manifest:exact-food";
		private const string Zone = "JoppaWorld.11.22.1.1.10";

		[Test]
		public void CargoRouteConservesPhysicalCustodyAndOnlyBanksSemanticEntitlement()
		{
			KingdomPolityLedger ledger = Fresh(); PlanRoute(ledger, KingdomPolityRoutePurpose.Delegation,
				Manifest, new List<string> { Remote, "taf:site:waypoint", KingdomPolityTestData.Settlement });
			Assert.IsTrue(KingdomPolityManifestRules.TryCreateCargoProof(
				"taf:manifest-proof:depart", "taf:trade-book:one", Manifest, "serving",
				10L, 6L, 4L, 4L, 0L, 0L, "taf:receipt:debit", null, null,
				out KingdomPolityManifestProof custody, out string failure), failure);
			Assert.IsTrue(KingdomPolityRouteRules.TryDepart(ledger, ledger.Revision, Route, 90L,
				"taf:receipt:route-depart", custody, out KingdomPolityPublicationResult _, out failure), failure);
			Assert.IsTrue(KingdomPolityRouteRules.TryAdvance(ledger, ledger.Revision, Route, 0,
				100L, 110L, out _, out failure), failure);
			Assert.AreEqual(KingdomPolityRoutePhase.Traveling, ledger.Routes[0].Phase);
			Assert.IsTrue(KingdomPolityRouteRules.TryAdvance(ledger, ledger.Revision, Route, 1,
				110L, 110L, out _, out failure), failure);
			Assert.IsTrue(KingdomPolityManifestRules.TryCreateCargoProof(
				"taf:manifest-proof:delivered-too-early", "taf:trade-book:one", Manifest, "serving",
				10L, 6L, 4L, 0L, 4L, 0L, "taf:receipt:debit", "taf:receipt:physical-delivery",
				null, out KingdomPolityManifestProof delivered, out failure), failure);
			Assert.IsFalse(KingdomPolityRouteRules.TryDeliverEntitlement(ledger, ledger.Revision,
				Route, 110L, 150L, "taf:receipt:entitlement", delivered, out _, out failure),
				"offscreen semantic delivery must not claim a physical mutation");
			Assert.IsTrue(KingdomPolityRouteRules.TryDeliverEntitlement(ledger, ledger.Revision,
				Route, 110L, 150L, "taf:receipt:entitlement", custody, out _, out failure), failure);
			Assert.AreEqual(KingdomPolityRoutePhase.Arrived, ledger.Routes[0].Phase);
			Assert.IsTrue(KingdomPolityRouteRules.TryValidateLoadedEndpointDelivery(ledger, Route,
				KingdomPolityTestData.Settlement, delivered, out failure), failure);
			Assert.IsTrue(KingdomPolityRouteRules.TryReturn(ledger, ledger.Revision, Route, 150L,
				"taf:receipt:route-return", delivered, out _, out failure), failure);
			Assert.AreEqual(KingdomPolityRoutePhase.Returned, ledger.Routes[0].Phase);
			Assert.IsFalse(KingdomPolityRouteRules.TryCancelPreparing(ledger, ledger.Revision,
				Route, out _, out failure));
			Assert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
		}

		[Test]
		public void ErrandsAndCorrespondenceAreCanonicalViewsNotInventedCargoOrPeople()
		{
			KingdomPolityLedger ledger = Fresh();
			Assert.IsTrue(KingdomPolityManifestRules.TryCreateErrandProof(
				"taf:manifest-proof:errand", "taf:office:courier", "taf:errand:message",
				out KingdomPolityManifestProof errand, out string failure), failure);
			Assert.AreEqual(0L, errand.Debited); Assert.IsNull(errand.UnitKey);
			PlanRoute(ledger, KingdomPolityRoutePurpose.Courier, errand.ManifestOrErrandId,
				new List<string> { Remote, KingdomPolityTestData.Settlement });
			Assert.IsTrue(KingdomPolityRouteRules.TryDepart(ledger, ledger.Revision, Route, 90L,
				"taf:receipt:courier-depart", errand, out KingdomPolityPublicationResult _, out failure), failure);
			Assert.IsTrue(KingdomPolityRouteRules.TryAdvance(ledger, ledger.Revision, Route, 0,
				100L, 100L, out _, out failure), failure);
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryCreateProof(
				"taf:correspondence:courier", Route, KingdomPolityTestData.Rival,
				"taf:need:answer", "taf:news:crossing", errand.ManifestOrErrandId, null,
				out KingdomPolityCorrespondenceProof proof, out failure), failure);
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryDescribe(ledger, proof,
				out KingdomPolityCorrespondenceView view, out failure), failure);
			Assert.AreEqual("deliver exact message", view.PurposeVerb);
			Assert.AreEqual(KingdomPolityCorrespondencePhase.Available, view.Phase);
			proof.CounterpartyRef = "taf:office:invented";
			Assert.IsFalse(KingdomPolityCorrespondenceRules.TryDescribe(ledger, proof,
				out view, out failure), "changed correspondence digest must fail closed");
		}

		[Test]
		public void CohortsFreezeOneToSevenExactBodiesAndPrepareIdsBeforeMaterialization()
		{
			KingdomPolityLedger ledger = Fresh(); KingdomPolityCohortPlan guard = null;
			for (int purpose = 1; purpose <= 7; purpose++)
			{
				string id = "taf:cohort:purpose-" + purpose;
				KingdomPolityCohortPlanRequest request = CohortRequest(ledger, id,
					(KingdomPolityCohortPurpose)purpose, "taf:event:purpose-" + purpose, 2);
				if (purpose == (int)KingdomPolityCohortPurpose.Envoy)
					request.NamedFigureId = "taf:figure:rival-envoy";
				Assert.IsTrue(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, request,
					out KingdomPolityPublicationResult _, out string failure), failure);
				KingdomPolityCohortPlan planned = FindCohort(ledger, id);
				Assert.AreEqual(2, planned.ResolvedMembers.Count);
				Assert.LessOrEqual(planned.NamedRepresentativeAllowance, 1);
				if (purpose == 1) guard = planned;
			}
			Assert.IsTrue(KingdomPolityCohortRules.TryPrepareEndpointManifestation(ledger,
				ledger.Revision, guard.CohortId, Zone, 120L, out KingdomPolityPublicationResult prepared,
				out string prepareFailure), prepareFailure);
			KingdomPolityProjectionReceipt receipt = FindProjection(ledger, prepared.ProjectionId);
			Assert.AreEqual(KingdomPolityProjectionPhase.Prepared, receipt.Phase);
			Assert.AreEqual(guard.ResolvedMembers.Count, receipt.ObjectIds.Count,
				"object ids must exist before any runtime body call");
			Assert.IsTrue(KingdomPolityCohortRules.TryCommitEndpointManifestation(ledger,
				ledger.Revision, guard.CohortId, receipt.ProjectionId, receipt.ObjectIds, 121L,
				out KingdomPolityPublicationResult _, out prepareFailure), prepareFailure);
			Assert.IsTrue(KingdomPolityCohortRules.TryConcludeEndpointCohort(ledger,
				ledger.Revision, guard.CohortId, "taf:fact:witnessed:guard-dismissed", out _,
				out prepareFailure), prepareFailure);
			Assert.IsTrue(KingdomPolityCohortRules.TryCommitEndpointCleanup(ledger,
				ledger.Revision, guard.CohortId, receipt.ProjectionId, receipt.ObjectIds, out _,
				out prepareFailure), prepareFailure);
			KingdomPolityCohortPlanRequest resident = CohortRequest(ledger,
				"taf:cohort:resident-face", KingdomPolityCohortPurpose.Envoy,
				"taf:event:resident-face", 2, KingdomPolityTestData.Realm);
			resident.NamedFigureId = "taf:figure:current-successor";
			Assert.IsFalse(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, resident,
				out _, out prepareFailure), "resident successor must never be regenerated");
			KingdomPolityCohortPlanRequest single = CohortRequest(ledger, "taf:cohort:single-courier",
				KingdomPolityCohortPurpose.Courier, "taf:event:single-courier", 1);
			Assert.IsTrue(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, single,
				out _, out prepareFailure), prepareFailure);
			KingdomPolityCohortPlanRequest seven = CohortRequest(ledger, "taf:cohort:seven-migrants",
				KingdomPolityCohortPurpose.Migrant, "taf:event:seven-migrants", 7);
			Assert.IsTrue(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, seven,
				out _, out prepareFailure), prepareFailure);
			KingdomPolityCohortPlanRequest eight = CohortRequest(ledger, "taf:cohort:eight-migrants",
				KingdomPolityCohortPurpose.Migrant, "taf:event:eight-migrants", 8);
			Assert.IsFalse(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, eight,
				out _, out prepareFailure), "eight bodies exceed the declared attention bound");
		}

		[Test]
		public void RefusalCausesOneFrontWhileHospitalityCannotChangeDiplomaticOutcome()
		{
			KingdomPolityLedger ledger = DiplomacyScene(out string envoyId, out string warbandId);
			OpenAndPlanTerms(ledger, envoyId, warbandId);
			KingdomPolityLedger withoutFood = KingdomPolityRules.Clone(ledger);
			KingdomPolityHospitalityPlanRequest request = HospitalityRequest(200L);
			Assert.IsTrue(KingdomPolityHospitalityRules.TryPlanDebit(ledger, ledger.Revision,
				"taf:incident-plan:terms", request,
				out KingdomPolityHospitalityTransaction transaction,
				out KingdomPolityPublicationResult _, out string failure), failure);
			Assert.IsTrue(KingdomPolityHospitalityRules.TryCreateCommittedProof(transaction,
				"taf:fact:witnessed:meal-shared", 200L,
				out KingdomPolityHospitalityProof hospitality, out failure), failure);
			Assert.IsTrue(KingdomPolityHospitalityRules.TryCommitDebit(ledger, ledger.Revision,
				"taf:incident-plan:terms", hospitality, 200L, out _, out failure), failure);
			Assert.IsTrue(KingdomPolityDiplomacyRules.TryAnswerTerms(ledger, ledger.Revision,
				"taf:incident-plan:terms", KingdomPolityTermsChoice.Refuse,
				"taf:fact:witnessed:terms-refused", 200L, hospitality,
				out KingdomPolityPublicationResult _, out failure), failure);
			Assert.IsTrue(KingdomPolityDiplomacyRules.TryAnswerTerms(withoutFood,
				withoutFood.Revision, "taf:incident-plan:terms", KingdomPolityTermsChoice.Refuse,
				"taf:fact:witnessed:terms-refused", 200L, null, out _, out failure), failure);
			Assert.AreEqual(Relation(ledger).Band, Relation(withoutFood).Band);
			Assert.AreEqual(KingdomPolityRelationBand.Hostile, Relation(ledger).Band);
			Assert.AreEqual(1, ledger.Fronts.Count);
			Assert.AreEqual(KingdomPolityFrontPhase.ConfrontationAvailable, ledger.Fronts[0].Phase);
			Assert.AreEqual(KingdomPolityRoutePhase.ConfrontationAvailable,
				FindRoute(ledger).Phase);
			Assert.IsNull(FindPlan(ledger, "taf:incident-plan:clash").Conclusion,
				"refusing terms must not conclude the frozen clash");
			Assert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
			Assert.AreEqual(KingdomPolityHospitalityPhase.Applied,
				FindPlan(ledger, "taf:incident-plan:terms").Hospitality.Phase);
		}

		[TestCase(KingdomPolityTermsChoice.Accept, KingdomPolityRelationBand.Pact)]
		[TestCase(KingdomPolityTermsChoice.Counteroffer, KingdomPolityRelationBand.Neutral)]
		[TestCase(KingdomPolityTermsChoice.Truce, KingdomPolityRelationBand.Truce)]
		public void WitnessedTermsChoicesCauseTheirExactDiplomaticBand(
			KingdomPolityTermsChoice Choice, KingdomPolityRelationBand Expected)
		{
			KingdomPolityLedger ledger = DiplomacyScene(out string envoyId, out string warbandId);
			OpenAndPlanTerms(ledger, envoyId, warbandId);
			Assert.IsTrue(KingdomPolityDiplomacyRules.TryAnswerTerms(ledger, ledger.Revision,
				"taf:incident-plan:terms", Choice, "taf:fact:witnessed:terms-answered", 200L,
				null, out KingdomPolityPublicationResult _, out string failure), failure);
			Assert.AreEqual(Expected, Relation(ledger).Band);
			Assert.IsNotNull(FindPlan(ledger, "taf:incident-plan:terms").Conclusion);
			Assert.IsNull(FindPlan(ledger, "taf:incident-plan:clash").Conclusion);
			Assert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
		}

		[Test]
		public void QuarantinedHospitalityNeverBlocksOrdinaryDiplomacy()
		{
			KingdomPolityLedger ledger = DiplomacyScene(out string envoyId, out string warbandId);
			OpenAndPlanTerms(ledger, envoyId, warbandId);
			Assert.IsTrue(KingdomPolityHospitalityRules.TryPlanDebit(ledger, ledger.Revision,
				"taf:incident-plan:terms", HospitalityRequest(200L),
				out KingdomPolityHospitalityTransaction _, out KingdomPolityPublicationResult _,
				out string failure), failure);
			Assert.IsTrue(KingdomPolityHospitalityRules.TryQuarantineDebit(ledger,
				ledger.Revision, "taf:incident-plan:terms", "exact serving moved",
				out _, out failure), failure);
			Assert.IsTrue(KingdomPolityDiplomacyRules.TryAnswerTerms(ledger, ledger.Revision,
				"taf:incident-plan:terms", KingdomPolityTermsChoice.Accept,
				"taf:fact:witnessed:terms-after-empty-table", 201L, null,
				out _, out failure), failure);
			Assert.AreEqual(KingdomPolityRelationBand.Pact, Relation(ledger).Band);
			Assert.AreEqual(KingdomPolityHospitalityPhase.Quarantined,
				FindPlan(ledger, "taf:incident-plan:terms").Hospitality.Phase);
		}

		[Test]
		public void OnlyCommittedWitnessedFiniteClashCanConcludeAndConquestDeltasAreRejected()
		{
			KingdomPolityLedger ledger = DiplomacyScene(out string envoyId, out string warbandId);
			OpenAndPlanTerms(ledger, envoyId, warbandId);
			Assert.IsTrue(KingdomPolityDiplomacyRules.TryAnswerTerms(ledger, ledger.Revision,
				"taf:incident-plan:terms", KingdomPolityTermsChoice.Refuse,
				"taf:fact:witnessed:terms-refused", 200L, null,
				out KingdomPolityPublicationResult _, out string failure), failure);
			KingdomPolityCohortPlan warband = FindCohort(ledger, warbandId);
			KingdomPolityProjectionReceipt projection = FindProjection(ledger,
				warband.ManifestationReceiptId);
			List<string> facts = new List<string> { "taf:fact:witnessed:warband-yielded" };
			List<string> projections = new List<string> { projection.ProjectionId };
			List<string> receipts = new List<string> { "taf:receipt:clash-route-posture" };
			List<KingdomPolitySystemicDelta> forbidden = new List<KingdomPolitySystemicDelta>
			{
				new KingdomPolitySystemicDelta { Kind = KingdomPolitySystemicDeltaKind.ClaimPosture,
					TargetId = KingdomPolityTestData.Settlement, Amount = 1,
					ReceiptId = receipts[0] }
			};
			Assert.IsFalse(KingdomPolityClashRules.TryCreateLiveProof("taf:clash-proof:bad",
				"taf:incident-plan:clash", KingdomPolityTestData.Settlement, Zone, 220L, facts,
				projections, forbidden, new List<KingdomPolityRelationDelta>(), receipts,
				out KingdomPolityWitnessedClashProof _, out failure),
				"clash may not encode offscreen conquest");
			List<KingdomPolitySystemicDelta> actual = new List<KingdomPolitySystemicDelta>
			{
				new KingdomPolitySystemicDelta { Kind = KingdomPolitySystemicDeltaKind.RoutePosture,
					TargetId = Route, Amount = -1, ReceiptId = receipts[0] }
			};
			Assert.IsTrue(KingdomPolityClashRules.TryCreateLiveProof("taf:clash-proof:yield",
				"taf:incident-plan:clash", KingdomPolityTestData.Settlement, Zone, 220L, facts,
				projections, actual, new List<KingdomPolityRelationDelta>(), receipts,
				out KingdomPolityWitnessedClashProof proof, out failure), failure);
			Assert.IsTrue(KingdomPolityClashRules.TryConcludeWitnessed(ledger, ledger.Revision,
				proof, out KingdomPolityPublicationResult _, out failure), failure);
			Assert.IsNotNull(FindPlan(ledger, "taf:incident-plan:clash").Conclusion);
			Assert.AreEqual(KingdomPolityAftermathKind.WitnessedWithdrawal,
				FindPlan(ledger, "taf:incident-plan:clash").Aftermath.Kind);
			Assert.AreEqual(KingdomPolityFrontPhase.Ended, ledger.Fronts[0].Phase);
			Assert.AreEqual(KingdomPolityRoutePhase.AvailableToWitness, FindRoute(ledger).Phase);
		}

		[Test]
		public void ExplicitMediationCausesMutualTruceAndNeutralWitnessedAftermath()
		{
			KingdomPolityLedger ledger = DiplomacyScene(out string envoyId, out string warbandId);
			OpenAndPlanTerms(ledger, envoyId, warbandId);
			Assert.IsTrue(KingdomPolityDiplomacyRules.TryAnswerTerms(ledger, ledger.Revision,
				"taf:incident-plan:terms", KingdomPolityTermsChoice.Refuse,
				"taf:fact:witnessed:terms-refused", 200L, null, out _, out string failure), failure);
			KingdomPolityCohortPlan warband = FindCohort(ledger, warbandId);
			string projection = FindProjection(ledger, warband.ManifestationReceiptId).ProjectionId;
			string interventionFact = "taf:fact:witnessed:ceasefire-mediated";
			Assert.IsTrue(KingdomPolityConflictRules.TryRecordWitnessedIntervention(ledger,
				ledger.Revision, "taf:incident-plan:clash",
				KingdomPolityInterventionChoice.MediateCeasefire,
				KingdomPolityTestData.Settlement, Zone, 220L, interventionFact,
				new List<string> { projection }, out KingdomPolityPublicationResult recorded,
				out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.Applied, recorded.Outcome);
			long afterRecord = ledger.Revision;
			Assert.IsTrue(KingdomPolityConflictRules.TryRecordWitnessedIntervention(ledger,
				afterRecord - 1L, "taf:incident-plan:clash",
				KingdomPolityInterventionChoice.MediateCeasefire,
				KingdomPolityTestData.Settlement, Zone, 220L, interventionFact,
				new List<string> { projection }, out recorded, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, recorded.Outcome);

			List<KingdomPolityRelationDelta> deltas = new List<KingdomPolityRelationDelta>();
			List<string> receipts = new List<string>();
			for (int i = 0; i < ledger.Relations.Count; i++)
			{
				KingdomPolityRelation relation = ledger.Relations[i];
				string receipt = "taf:receipt:ceasefire:" + relation.RelationId;
				deltas.Add(new KingdomPolityRelationDelta { RelationId = relation.RelationId,
					Before = relation.Band, After = KingdomPolityRelationBand.Truce,
					ReceiptId = receipt });
				receipts.Add(receipt);
			}
			KingdomPolityAuthority.AddSortedUnique(receipts, "taf:receipt:clash-mediated");
			Assert.IsTrue(KingdomPolityClashRules.TryCreateLiveProof(
				"taf:clash-proof:mediated", "taf:incident-plan:clash",
				KingdomPolityTestData.Settlement, Zone, 221L,
				new List<string> { interventionFact }, new List<string> { projection },
				new List<KingdomPolitySystemicDelta>(), deltas, receipts,
				out KingdomPolityWitnessedClashProof proof, out failure), failure);
			Assert.IsTrue(KingdomPolityClashRules.TryConcludeWitnessed(ledger, ledger.Revision,
				proof, out _, out failure), failure);
			KingdomPolityIncidentRecord clash = FindPlan(ledger, "taf:incident-plan:clash");
			Assert.AreEqual(KingdomPolityAftermathKind.Ceasefire, clash.Aftermath.Kind);
			Assert.AreEqual(clash.Intervention.InterventionId, clash.Aftermath.InterventionId);
			Assert.Contains(clash.Intervention.ReceiptId, clash.Conclusion.ReceiptRefs);
			Assert.AreEqual(0, clash.Conclusion.SystemicDeltas.Count);
			for (int i = 0; i < ledger.Relations.Count; i++)
				Assert.AreEqual(KingdomPolityRelationBand.Truce, ledger.Relations[i].Band);
			Assert.AreEqual(KingdomPolityFrontPhase.Ended, ledger.Fronts[0].Phase);
			byte[] bytes = KingdomPolityCodec.EncodeEnvelope(ledger);
			KingdomPolityLedger roundTrip = KingdomPolityCodec.DecodeEnvelope(bytes);
			Assert.AreEqual(clash.Aftermath.ProofDigest,
				FindPlan(roundTrip, "taf:incident-plan:clash").Aftermath.ProofDigest);
			Assert.Throws<System.IO.InvalidDataException>(() =>
				KingdomPolityCodec.EncodeEnvelopeV4Fixture(ledger));
		}

		[Test]
		public void SupportStancePersistsButCannotInventOutcomeOrChangeRelation()
		{
			KingdomPolityLedger ledger = DiplomacyScene(out string envoyId, out string warbandId);
			OpenAndPlanTerms(ledger, envoyId, warbandId);
			Assert.IsTrue(KingdomPolityDiplomacyRules.TryAnswerTerms(ledger, ledger.Revision,
				"taf:incident-plan:terms", KingdomPolityTermsChoice.Refuse,
				"taf:fact:witnessed:terms-refused", 200L, null, out _, out string failure), failure);
			KingdomPolityCohortPlan warband = FindCohort(ledger, warbandId);
			string projection = FindProjection(ledger, warband.ManifestationReceiptId).ProjectionId;
			Assert.IsTrue(KingdomPolityConflictRules.TryRecordWitnessedIntervention(ledger,
				ledger.Revision, "taf:incident-plan:clash",
				KingdomPolityInterventionChoice.SupportSettlement,
				KingdomPolityTestData.Settlement, Zone, 220L,
				"taf:fact:witnessed:settlement-supported", new List<string> { projection },
				out _, out failure), failure);
			KingdomPolityIncidentRecord clash = FindPlan(ledger, "taf:incident-plan:clash");
			Assert.IsNull(clash.Conclusion); Assert.IsNull(clash.Aftermath);
			Assert.AreEqual(KingdomPolityRelationBand.Hostile, Relation(ledger).Band);
			Assert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
		}

		[Test]
		public void StandingAloneCannotOpenDiplomacy()
		{
			KingdomPolityLedger ledger = Fresh();
			KingdomPolityGrievanceRequest request = Grievance();
			request.SourceEventId = "taf:standing:rival-minus-one-hundred";
			Assert.IsFalse(KingdomPolityDiplomacyRules.TryOpenGrievance(ledger, ledger.Revision,
				request, out KingdomPolityPublicationResult _, out string failure));
			StringAssert.Contains("caused event", failure);
		}

		private static KingdomPolityLedger DiplomacyScene(out string EnvoyId, out string WarbandId)
		{
			KingdomPolityLedger ledger = Fresh(); PlanRoute(ledger,
				KingdomPolityRoutePurpose.Delegation, "taf:errand:terms",
				new List<string> { Remote, KingdomPolityTestData.Settlement });
			Assert.IsTrue(KingdomPolityManifestRules.TryCreateErrandProof("taf:manifest-proof:terms",
				"taf:office:rival", "taf:errand:terms", out KingdomPolityManifestProof errand,
				out string failure), failure);
			Assert.IsTrue(KingdomPolityRouteRules.TryDepart(ledger, ledger.Revision, Route, 90L,
				"taf:receipt:terms-depart", errand, out KingdomPolityPublicationResult _, out failure), failure);
			Assert.IsTrue(KingdomPolityRouteRules.TryAdvance(ledger, ledger.Revision, Route, 0,
				100L, 100L, out _, out failure), failure);
			EnvoyId = "taf:cohort:terms-envoy"; WarbandId = "taf:cohort:frozen-warband";
			KingdomPolityCohortPlanRequest envoy = CohortRequest(ledger, EnvoyId,
				KingdomPolityCohortPurpose.Envoy, Route, 2);
			envoy.NamedFigureId = "taf:figure:rival-envoy";
			Assert.IsTrue(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, envoy,
				out _, out failure), failure);
			KingdomPolityCohortPlanRequest warband = CohortRequest(ledger, WarbandId,
				KingdomPolityCohortPurpose.Warband, "taf:event:warband-mustered", 2);
			Assert.IsTrue(KingdomPolityCohortRules.TryPlan(ledger, ledger.Revision, warband,
				out _, out failure), failure);
			CommitManifestation(ledger, EnvoyId, 120L);
			CommitManifestation(ledger, WarbandId, 121L); return ledger;
		}

		private static void OpenAndPlanTerms(KingdomPolityLedger Ledger, string EnvoyId,
			string WarbandId)
		{
			Assert.IsTrue(KingdomPolityDiplomacyRules.TryOpenGrievance(Ledger, Ledger.Revision,
				Grievance(), out KingdomPolityPublicationResult _, out string failure), failure);
			KingdomPolityTermsPlanRequest terms = new KingdomPolityTermsPlanRequest
			{
				GrievanceId = "taf:grievance:caused-crossing", TermsPlanId = "taf:incident-plan:terms",
				TermsIncidentId = "taf:incident:terms", ClashPlanId = "taf:incident-plan:clash",
				ClashIncidentId = "taf:incident:clash", EnvoyCohortId = EnvoyId,
				ClashCohortRefs = new List<string> { WarbandId },
				DisclosedStakeRefs = new List<string> { Route },
				EligibleSurfaceRefs = new List<string> { KingdomPolityTestData.Settlement },
				TermKeys = new List<string> { "recognize-passage", "restore-access" },
				EventStreamId = "taf:stream:terms", RulesVersion = 1, MaxSystemicWound = 1
			};
			Assert.IsTrue(KingdomPolityDiplomacyRules.TryPlanTerms(Ledger, Ledger.Revision,
				terms, out _, out failure), failure);
		}

		private static KingdomPolityGrievanceRequest Grievance()
		{
			return new KingdomPolityGrievanceRequest
			{
				GrievanceId = "taf:grievance:caused-crossing",
				IssuerPolityId = KingdomPolityTestData.Rival,
				TargetPolityId = KingdomPolityTestData.Realm,
				Cause = KingdomPolityGrievanceCause.RouteObstruction,
				SourceEventId = "taf:event:crossing-blocked", Severity = 2,
				EvidenceRefs = new List<string> { "taf:evidence:crossing-marker" }
			};
		}

		private static KingdomPolityHospitalityPlanRequest HospitalityRequest(long Tick)
		{
			return new KingdomPolityHospitalityPlanRequest
			{
				SurfaceRef = KingdomPolityTestData.Settlement, ZoneId = Zone,
				PlannedTick = Tick,
				Lines = new List<KingdomPolityHospitalityDebitLine>
				{
					new KingdomPolityHospitalityDebitLine
					{
						Kind = KingdomPolityHospitalityDebitKind.Food,
						ContainerId = "larder-1", ObjectId = "food-1",
						Blueprint = "Sun-Dried Banana", Before = 3, After = 2
					},
					new KingdomPolityHospitalityDebitLine
					{
						Kind = KingdomPolityHospitalityDebitKind.Water,
						ContainerId = "vessel-1", ObjectId = "vessel-1",
						Blueprint = "Waterskin", Before = 4, After = 3, Capacity = 64
					}
				}
			};
		}

		private static void CommitManifestation(KingdomPolityLedger Ledger, string CohortId,
			long Tick)
		{
			Assert.IsTrue(KingdomPolityCohortRules.TryPrepareEndpointManifestation(Ledger,
				Ledger.Revision, CohortId, Zone, Tick, out KingdomPolityPublicationResult result,
				out string failure), failure);
			KingdomPolityProjectionReceipt receipt = FindProjection(Ledger, result.ProjectionId);
			Assert.IsTrue(KingdomPolityCohortRules.TryCommitEndpointManifestation(Ledger,
				Ledger.Revision, CohortId, receipt.ProjectionId, receipt.ObjectIds, Tick,
				out KingdomPolityPublicationResult _, out failure), failure);
		}

		private static KingdomPolityCohortPlanRequest CohortRequest(KingdomPolityLedger Ledger,
			string Id, KingdomPolityCohortPurpose Purpose, string Source, int Count,
			string Polity = KingdomPolityTestData.Rival)
		{
			Assert.IsTrue(KingdomPolityCohortRules.TryResolverContract(Ledger, Polity, Purpose,
				out int resolverRulesVersion, out int minimum, out int maximum,
				out string failure), failure);
			return new KingdomPolityCohortPlanRequest
			{
				CohortId = Id, Purpose = Purpose, SourceRef = Source,
				PolityId = Polity, SurfaceRef = KingdomPolityTestData.Settlement,
				MemberCount = Count, MinimumLevel = minimum, MaximumLevel = maximum,
				EventStreamId = "taf:stream:" + Id.Substring("taf:cohort:".Length),
				RulesVersion = resolverRulesVersion,
				PresentationAuthority = Authority(Purpose, 100L)
			};
		}

		private static KingdomPolityPresentationAuthorityProof Authority(
			KingdomPolityCohortPurpose Purpose, long ReservedTick)
		{
			return new KingdomPolityPresentationAuthorityProof
			{
				OptionKind = Purpose == KingdomPolityCohortPurpose.Envoy ||
					Purpose == KingdomPolityCohortPurpose.Warband
						? KingdomExperienceOptionKind.CivicStory
						: KingdomExperienceOptionKind.AmbientUse,
				EnableEpoch = 1L, ReservedTick = ReservedTick
			};
		}

		private static void PlanRoute(KingdomPolityLedger Ledger,
			KingdomPolityRoutePurpose Purpose, string ManifestId, List<string> Path)
		{
			KingdomPolityRoutePlanRequest request = new KingdomPolityRoutePlanRequest
			{
				RouteId = Route, EventStreamId = "taf:stream:semantic-route", OriginId = Path[0],
				DestinationId = Path[Path.Count - 1], OrderedPath = Path,
				Mode = KingdomPolityRouteMode.Foot, Purpose = Purpose, FirstDueTick = 100L,
				ManifestOrErrandId = ManifestId, CounterpartyRef = KingdomPolityTestData.Rival
			};
			Assert.IsTrue(KingdomPolityRouteRules.TryPlan(Ledger, Ledger.Revision, request,
				out KingdomPolityPublicationResult _, out string failure), failure);
		}

		private static KingdomPolityLedger Fresh()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			ledger.Routes.Clear(); ledger.Grievances.Clear(); ledger.Fronts.Clear();
			ledger.Cohorts.Clear(); ledger.Incidents.Clear();
			for (int i = ledger.Projections.Count - 1; i >= 0; i--)
				if (ledger.Projections[i].Kind != KingdomPolityProjectionKind.Faction)
					ledger.Projections.RemoveAt(i);
			KingdomPolityProfileRevision rival = null;
			for (int i = 0; i < ledger.Profiles.Count; i++)
				if (ledger.Profiles[i].PolityId == KingdomPolityTestData.Rival) rival = ledger.Profiles[i];
			rival.RoleKeys = new List<string> { "claimant", "courier", "envoy", "guard", "migrant",
				"namesake", "patrol", "trader", "warband" };
			Assert.IsTrue(KingdomPolityRules.TryValidate(ledger, out string failure), failure);
			return ledger;
		}

		private static KingdomPolityCohortPlan FindCohort(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; i < L.Cohorts.Count; i++) if (L.Cohorts[i].CohortId == Id) return L.Cohorts[i];
			return null;
		}

		private static KingdomPolityProjectionReceipt FindProjection(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; i < L.Projections.Count; i++) if (L.Projections[i].ProjectionId == Id)
				return L.Projections[i];
			return null;
		}

		private static KingdomPolityIncidentRecord FindPlan(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; i < L.Incidents.Count; i++) if (L.Incidents[i].IncidentPlanId == Id)
				return L.Incidents[i];
			return null;
		}

		private static KingdomPolityRouteRecord FindRoute(KingdomPolityLedger L)
		{
			for (int i = 0; i < L.Routes.Count; i++) if (L.Routes[i].RouteId == Route) return L.Routes[i];
			return null;
		}

		private static KingdomPolityRelation Relation(KingdomPolityLedger L)
		{
			for (int i = 0; i < L.Relations.Count; i++) if (L.Relations[i].FromPolityId ==
				KingdomPolityTestData.Rival && L.Relations[i].ToPolityId == KingdomPolityTestData.Realm)
				return L.Relations[i];
			return null;
		}
	}
}
