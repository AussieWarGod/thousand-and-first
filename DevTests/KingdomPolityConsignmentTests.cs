#if TAF_TESTS
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ThousandAndFirst.DevTests
{
	[TestFixture]
	public sealed class KingdomPolityConsignmentTests
	{
		[Test]
		public void PartialReceiptCreditsExactQuantityAndRelationOnce()
		{
			KingdomPolityLedger ledger = Scene();
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryPlanConsignment(ledger,
				ledger.Revision, KingdomPolityTestData.Plan, KingdomPolityTestData.Cohort,
				KingdomPolityTestData.Settlement, out KingdomPolityConsignmentRequest request,
				out KingdomPolityPublicationResult result, out string failure), failure);
			Assert.AreEqual(8, request.RequestedDrams);
			Assert.AreEqual(KingdomPolityTestData.Rival, request.CounterpartyPolityId);
			CollectionAssert.AreEqual(Incident(ledger, KingdomPolityTestData.Plan).GrievanceRefs,
				Incident(ledger, request.CorrespondencePlanId).GrievanceRefs);
			byte[] planned = KingdomPolityCodec.EncodeEnvelope(ledger);
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryPlanConsignment(ledger, 0L,
				KingdomPolityTestData.Plan, KingdomPolityTestData.Cohort,
				KingdomPolityTestData.Settlement, out _, out result, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
			CollectionAssert.AreEqual(planned, KingdomPolityCodec.EncodeEnvelope(ledger));

			KingdomTradePolityConsignmentReceipt receipt = Receipt(request, 7);
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryConsumeTradeReceipt(ledger,
				ledger.Revision, receipt, out result, out failure), failure);
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryDescribeConsignment(ledger,
				request.CorrespondencePlanId, out _,
				out KingdomPolityCorrespondenceReplyKind reply, out failure), failure);
			Assert.AreEqual(KingdomPolityCorrespondenceReplyKind.Fulfilled, reply);
			KingdomPolityIncidentRecord plan = Incident(ledger, request.CorrespondencePlanId);
			Assert.IsTrue(HasPrefix(plan.Conclusion.ReceiptRefs,
				"taf:receipt:trade-operation-proof:v1:"));
			Assert.IsFalse(plan.Conclusion.ReceiptRefs.Contains(receipt.TradeOperationId));
			Assert.AreEqual(1, plan.Conclusion.SystemicDeltas.Count);
			Assert.AreEqual(KingdomPolitySystemicDeltaKind.Standing,
				plan.Conclusion.SystemicDeltas[0].Kind);
			Assert.AreEqual(7, plan.Conclusion.SystemicDeltas[0].Amount);
			Assert.AreEqual(1, plan.Conclusion.RelationDeltas.Count);
			Assert.AreEqual(KingdomPolityRelationBand.Neutral, Relation(ledger).Band);
			byte[] fulfilled = KingdomPolityCodec.EncodeEnvelope(ledger);
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryConsumeTradeReceipt(ledger,
				0L, receipt, out result, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
			CollectionAssert.AreEqual(fulfilled, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void ZeroOrReplacementCannotCreditAndDeclineCannotFarmRelation()
		{
			KingdomPolityLedger ledger = Scene();
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryPlanConsignment(ledger,
				ledger.Revision, KingdomPolityTestData.Plan, KingdomPolityTestData.Cohort,
				KingdomPolityTestData.Settlement, out KingdomPolityConsignmentRequest request,
				out _, out string failure), failure);
			byte[] open = KingdomPolityCodec.EncodeEnvelope(ledger);
			Assert.IsFalse(KingdomPolityCorrespondenceRules.TryConsumeTradeReceipt(ledger,
				ledger.Revision, Receipt(request, 0), out _, out failure));
			KingdomTradePolityConsignmentReceipt replacement = Receipt(request, 8);
			replacement.RecipientBodyId = "taf:object:polity-cohort:v1:replacement";
			replacement.RecipientWitnessDigest = KingdomPolityRules.ActivationDigest(
				"trade-polity-recipient-witness-v1", replacement.RecipientBodyId,
				replacement.RecipientCohortId, replacement.RecipientProjectionId,
				replacement.SurfaceRef, request.RequestDigest);
			replacement.ReceiptDigest = KingdomPolityCorrespondenceRules.TradeReceiptDigest(
				replacement);
			replacement.ReceiptId = KingdomPolityCorrespondenceRules.TradeReceiptId(replacement);
			Assert.IsFalse(KingdomPolityCorrespondenceRules.TryConsumeTradeReceipt(ledger,
				ledger.Revision, replacement, out _, out failure));
			CollectionAssert.AreEqual(open, KingdomPolityCodec.EncodeEnvelope(ledger));
			KingdomPolityRelationBand before = Relation(ledger).Band;
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryDeclineConsignment(ledger,
				ledger.Revision, request.CorrespondencePlanId,
				"taf:fact:witnessed:consignment-declined", 90L,
				out KingdomPolityPublicationResult result, out failure), failure);
			Assert.AreEqual(before, Relation(ledger).Band);
			Assert.AreEqual(0, Incident(ledger,
				request.CorrespondencePlanId).Conclusion.SystemicDeltas.Count);
			Assert.IsFalse(KingdomPolityCorrespondenceRules.TryConsumeTradeReceipt(ledger,
				ledger.Revision, Receipt(request, 8), out result, out failure));
		}

		[Test]
		public void TerminalFailureBecomesOneZeroDeltaUnfulfilledReply()
		{
			KingdomPolityLedger ledger = Scene();
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryPlanConsignment(ledger,
				ledger.Revision, KingdomPolityTestData.Plan, KingdomPolityTestData.Cohort,
				KingdomPolityTestData.Settlement, out KingdomPolityConsignmentRequest request,
				out _, out string failure), failure);
			KingdomTradePolityConsignmentReceipt receipt = FailedReceipt(request, 3);
			KingdomPolityRelationBand band = Relation(ledger).Band;
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryConsumeTradeReceipt(ledger,
				ledger.Revision, receipt, out KingdomPolityPublicationResult result,
				out failure), failure);
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryDescribeConsignment(ledger,
				request.CorrespondencePlanId, out _,
				out KingdomPolityCorrespondenceReplyKind reply, out failure), failure);
			Assert.AreEqual(KingdomPolityCorrespondenceReplyKind.Unfulfilled, reply);
			KingdomPolityIncidentConclusion conclusion = Incident(ledger,
				request.CorrespondencePlanId).Conclusion;
			Assert.AreEqual(0, conclusion.SystemicDeltas.Count);
			Assert.AreEqual(0, conclusion.RelationDeltas.Count);
			Assert.AreEqual(band, Relation(ledger).Band);
			byte[] stable = KingdomPolityCodec.EncodeEnvelope(ledger);
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryConsumeTradeReceipt(ledger,
				0L, receipt, out result, out failure), failure);
			Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, result.Outcome);
			CollectionAssert.AreEqual(stable, KingdomPolityCodec.EncodeEnvelope(ledger));
		}

		[Test]
		public void ExactTerminalReceiptSurvivesFiniteEnvoyCleanup()
		{
			KingdomPolityLedger ledger = Scene();
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryPlanConsignment(ledger,
				ledger.Revision, KingdomPolityTestData.Plan, KingdomPolityTestData.Cohort,
				KingdomPolityTestData.Settlement, out KingdomPolityConsignmentRequest request,
				out _, out string failure), failure);
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(ledger,
				request.RecipientCohortId);
			KingdomPolityProjectionReceipt projection = KingdomPolityAuthority.Projection(
				ledger, cohort.ManifestationReceiptId);
			Assert.IsTrue(KingdomPolityCohortRules.TryConcludeEndpointCohort(ledger,
				ledger.Revision, cohort.CohortId, "taf:fact:witnessed:envoy-departed",
				out _, out failure), failure);
			Assert.IsTrue(KingdomPolityCohortRules.TryCommitEndpointCleanup(ledger,
				ledger.Revision, cohort.CohortId, projection.ProjectionId,
				projection.ObjectIds, out _, out failure), failure);
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryConsumeTradeReceipt(ledger,
				ledger.Revision, Receipt(request, 8), out _, out failure), failure);
		}

		[Test]
		public void HistoricalRequestsRemainReadableAcrossEveryReplyAfterRealmEnding()
		{
			KingdomPolityCorrespondenceReplyKind[] replies =
			{
				KingdomPolityCorrespondenceReplyKind.None,
				KingdomPolityCorrespondenceReplyKind.Fulfilled,
				KingdomPolityCorrespondenceReplyKind.Declined,
				KingdomPolityCorrespondenceReplyKind.Unfulfilled
			};
			for (int i = 0; i < replies.Length; i++)
			{
				KingdomPolityLedger ledger = PlannedScene(out KingdomPolityConsignmentRequest request);
				string failure;
				if (replies[i] == KingdomPolityCorrespondenceReplyKind.Fulfilled)
					Assert.IsTrue(KingdomPolityCorrespondenceRules.TryConsumeTradeReceipt(ledger,
						ledger.Revision, Receipt(request, 4), out _, out failure), failure);
				else if (replies[i] == KingdomPolityCorrespondenceReplyKind.Declined)
					Assert.IsTrue(KingdomPolityCorrespondenceRules.TryDeclineConsignment(ledger,
						ledger.Revision, request.CorrespondencePlanId,
						"taf:fact:witnessed:historical-decline", 90L, out _, out failure), failure);
				else if (replies[i] == KingdomPolityCorrespondenceReplyKind.Unfulfilled)
					Assert.IsTrue(KingdomPolityCorrespondenceRules.TryConsumeTradeReceipt(ledger,
						ledger.Revision, FailedReceipt(request, 2), out _, out failure), failure);
				EndAllPolities(ledger, 100L);
				Assert.IsTrue(KingdomPolityRules.TryValidate(ledger, out failure), failure);
				Assert.IsTrue(KingdomPolityCorrespondenceRules.TryPlanConsignment(ledger, 0L,
					request.TermsPlanId, request.RecipientCohortId, request.SurfaceRef,
					out KingdomPolityConsignmentRequest retried,
					out KingdomPolityPublicationResult retry, out failure), failure);
				Assert.AreEqual(KingdomPolityCasOutcome.AlreadyApplied, retry.Outcome);
				Assert.AreEqual(request.RequestDigest, retried.RequestDigest);
				Assert.IsTrue(KingdomPolityCorrespondenceRules.TryDescribeConsignment(ledger,
					request.CorrespondencePlanId, out KingdomPolityConsignmentRequest restored,
					out KingdomPolityCorrespondenceReplyKind actual, out failure), failure);
				Assert.AreEqual(request.CurrentPolityId, restored.CurrentPolityId);
				Assert.AreEqual(request.CounterpartyPolityId, restored.CounterpartyPolityId);
				Assert.AreEqual(replies[i], actual);
				KingdomPolityLedger decoded = KingdomPolityCodec.DecodeEnvelope(
					KingdomPolityCodec.EncodeEnvelope(ledger));
				Assert.IsTrue(KingdomPolityCorrespondenceRules.TryDescribeConsignment(decoded,
					request.CorrespondencePlanId, out restored, out actual, out failure), failure);
				Assert.AreEqual(replies[i], actual);
			}
		}

#if !TAF_CONSTRUCTION_INPUT_PORTABLE
		[Test]
		public void TradeReaderClassifiesMissingPartialFailureAndInvalidExactly()
		{
			KingdomPolityLedger ledger = Scene();
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryPlanConsignment(ledger,
				ledger.Revision, KingdomPolityTestData.Plan, KingdomPolityTestData.Cohort,
				KingdomPolityTestData.Settlement, out KingdomPolityConsignmentRequest request,
				out _, out string failure), failure);
			KingdomTradeBook book = TradeBookForWitness(request, 7, KingdomTradePhase.Terminal);
			Assert.IsTrue(KingdomTradeRules.TryInspectPolityConsignmentReceipt(book, request,
				out KingdomTradePolityConsignmentReceipt receipt,
				out KingdomTradePolityConsignmentReceiptKind kind, out failure), failure);
			Assert.AreEqual(KingdomTradePolityConsignmentReceiptKind.Landed, kind);
			Assert.AreEqual(7, receipt.DeliveredDrams);
			Assert.IsTrue(KingdomPolityRules.Digest(receipt.TradeOperationId));
			Assert.AreEqual(RecipientBodyId(), receipt.RecipientBodyId);
			book.RecentProofs[0].PolityRecipient = null;
			Assert.IsTrue(KingdomTradeRules.TryInspectPolityConsignmentReceipt(book, request,
				out _, out kind, out failure));
			Assert.AreEqual(KingdomTradePolityConsignmentReceiptKind.Invalid, kind);
			book = TradeBookForWitness(request, 8, KingdomTradePhase.Terminal);
			book.RecentProofs[0].PolityRecipient.BodyId =
				"taf:object:polity-cohort:v1:replacement";
			Assert.IsTrue(KingdomTradeRules.TryInspectPolityConsignmentReceipt(book, request,
				out _, out kind, out failure));
			Assert.AreEqual(KingdomTradePolityConsignmentReceiptKind.Invalid, kind);
			book = TradeBookForWitness(request, 0, KingdomTradePhase.Quarantined);
			Assert.IsTrue(KingdomTradeRules.TryInspectPolityConsignmentReceipt(book, request,
				out receipt, out kind, out failure));
			Assert.AreEqual(KingdomTradePolityConsignmentReceiptKind.TerminalFailed, kind);
			Assert.AreEqual(0, receipt.DebitedDrams);
			book = TradeBookForWitness(request, 3, KingdomTradePhase.Quarantined);
			book.RecentProofs[0].RetainedDelta = book.RecentProofs[0].RetainedAfter = 3L;
			book.RecentProofs[0].RetainedState = KingdomTradePhysicalState.Proved;
			book.RecentProofs[0].Fault = "recipient lost after debit";
			Assert.IsTrue(KingdomTradeRules.TryInspectPolityConsignmentReceipt(book, request,
				out receipt, out kind, out failure), failure);
			Assert.AreEqual(KingdomTradePolityConsignmentReceiptKind.TerminalFailed, kind);
			Assert.AreEqual(3, receipt.RetainedDrams);
			book = TradeBookForWitness(request, 8, KingdomTradePhase.Terminal);
			book.RecentProofs.Add(book.RecentProofs[0]);
			Assert.IsTrue(KingdomTradeRules.TryInspectPolityConsignmentReceipt(book, request,
				out _, out kind, out failure));
			Assert.AreEqual(KingdomTradePolityConsignmentReceiptKind.Invalid, kind);
			StringAssert.Contains("duplicated", failure);
		}
#endif

		internal static KingdomPolityLedger Scene()
		{
			KingdomPolityLedger ledger = KingdomPolityTestData.Full();
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(ledger,
				KingdomPolityTestData.Cohort);
			cohort.SurfaceRef = KingdomPolityTestData.Settlement;
			KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(ledger,
				KingdomPolityTestData.Route);
			route.OriginId = "taf:site:rival-consignment-origin";
			route.DestinationId = KingdomPolityTestData.Settlement;
			route.OrderedPath = new List<string>
				{ route.OriginId, KingdomPolityTestData.Settlement };
			route.NextDueTick = 40L;
			Assert.IsTrue(KingdomPolityManifestRules.TryCreateErrandProof(
				"taf:manifest-proof:consignment-terms", "taf:office:rival-envoy",
				route.ManifestOrErrandId, out KingdomPolityManifestProof errand,
				out string routeFailure), routeFailure);
			Assert.IsTrue(KingdomPolityRouteRules.TryDepart(ledger, ledger.Revision,
				route.RouteId, 40L, "taf:receipt:consignment-route-departed", errand,
				out KingdomPolityPublicationResult _, out routeFailure), routeFailure);
			Assert.IsTrue(KingdomPolityRouteRules.TryAdvance(ledger, ledger.Revision,
				route.RouteId, 0, 40L, 40L, out _, out routeFailure), routeFailure);
			KingdomPolityIncidentRecord terms = Incident(ledger, KingdomPolityTestData.Plan);
			terms.EligibleSurfaceRefs = new List<string> { KingdomPolityTestData.Settlement };
			Assert.IsTrue(KingdomPolityCohortRules.TryPrepareEndpointManifestation(ledger,
				ledger.Revision, KingdomPolityTestData.Cohort, "taf-test-zone", 50L,
				out KingdomPolityPublicationResult prepared, out string manifestationFailure),
				manifestationFailure);
			KingdomPolityProjectionReceipt projection = KingdomPolityAuthority.Projection(
				ledger, prepared.ProjectionId);
			Assert.IsTrue(KingdomPolityCohortRules.TryCommitEndpointManifestation(ledger,
				ledger.Revision, KingdomPolityTestData.Cohort, prepared.ProjectionId,
				projection.ObjectIds, 51L, out _, out manifestationFailure), manifestationFailure);
			Assert.IsTrue(KingdomPolityRules.TryValidate(ledger, out string failure), failure);
			return ledger;
		}

		[Test]
		public void AvailableRouteWithoutLawfulDepartureReceiptIsRejected()
		{
			KingdomPolityLedger ledger = Scene();
			KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(ledger,
				KingdomPolityTestData.Route);
			route.DepartureReceiptId = null;
			Assert.IsFalse(KingdomPolityRules.TryValidate(ledger, out string failure));
			StringAssert.Contains("route", failure.ToLowerInvariant());
		}

		private static KingdomTradePolityConsignmentReceipt Receipt(
			KingdomPolityConsignmentRequest request, int delivered)
		{
			KingdomTradePolityConsignmentReceipt receipt = new KingdomTradePolityConsignmentReceipt
			{
				Kind = KingdomTradePolityConsignmentReceiptKind.Landed,
				TradeOperationId = KingdomPolityRules.ActivationDigest(
					"test-trade-operation-v1", request.ConsignmentId),
				TradeEvidenceHash = KingdomPolityTestData.DigestA,
				ConsignmentId = request.ConsignmentId,
				CorrespondencePlanId = request.CorrespondencePlanId,
					CounterpartyPolityId = request.CounterpartyPolityId,
					SurfaceRef = request.SurfaceRef,
					RecipientBodyId = RecipientBodyId(),
					RecipientCohortId = request.RecipientCohortId,
					RecipientProjectionId = RecipientProjectionId(),
					RequestedDrams = request.RequestedDrams, DebitedDrams = delivered,
					DeliveredDrams = delivered, RetainedDrams = 0, CommitTick = 80L
				};
			receipt.RecipientWitnessDigest = KingdomPolityRules.ActivationDigest(
				"trade-polity-recipient-witness-v1", receipt.RecipientBodyId,
				receipt.RecipientCohortId, receipt.RecipientProjectionId,
				receipt.SurfaceRef, request.RequestDigest);
			receipt.ReceiptDigest = KingdomPolityCorrespondenceRules.TradeReceiptDigest(receipt);
			receipt.ReceiptId = KingdomPolityCorrespondenceRules.TradeReceiptId(receipt);
			return receipt;
		}

		private static KingdomPolityLedger PlannedScene(
			out KingdomPolityConsignmentRequest request)
		{
			KingdomPolityLedger ledger = Scene();
			Assert.IsTrue(KingdomPolityCorrespondenceRules.TryPlanConsignment(ledger,
				ledger.Revision, KingdomPolityTestData.Plan, KingdomPolityTestData.Cohort,
				KingdomPolityTestData.Settlement, out request,
				out KingdomPolityPublicationResult _, out string failure), failure);
			return ledger;
		}

		private static void EndAllPolities(KingdomPolityLedger ledger, long tick)
		{
			for (int i = 0; i < ledger.Polities.Count; i++)
			{
				ledger.Polities[i].Lifecycle = KingdomPolityLifecycle.Ended;
				ledger.Polities[i].EndedTick = tick;
			}
		}

		private static KingdomTradePolityConsignmentReceipt FailedReceipt(
			KingdomPolityConsignmentRequest request, int retained)
		{
			KingdomTradePolityConsignmentReceipt receipt = Receipt(request, retained);
			receipt.Kind = KingdomTradePolityConsignmentReceiptKind.TerminalFailed;
			receipt.DeliveredDrams = 0; receipt.RetainedDrams = retained;
			receipt.FailureText = "exact recipient was lost before landing";
			receipt.ReceiptDigest = KingdomPolityCorrespondenceRules.TradeReceiptDigest(receipt);
			receipt.ReceiptId = KingdomPolityCorrespondenceRules.TradeReceiptId(receipt);
			return receipt;
		}

#if !TAF_CONSTRUCTION_INPUT_PORTABLE
		internal static KingdomTradeBook TradeBookForWitness(
			KingdomPolityConsignmentRequest request,
			int proved, KingdomTradePhase phase)
		{
			KingdomTradeBook book = new KingdomTradeBook();
			Assert.IsTrue(KingdomTradeRules.BindExactIdentity(book, request.CurrentPolityId,
				new[] { request.SurfaceRef }, out string failure), failure);
			book.NextOperationSequence = 2L; book.RetiredThrough = 1L;
			Assert.IsTrue(KingdomTradeRules.TryCreatePolityRecipientWitness(request,
				RecipientBodyId(), RecipientProjectionId(),
				out KingdomTradePolityRecipientWitness witness, out failure), failure);
			book.RecentProofs.Add(new KingdomTradeProof
			{
				RealmId = request.CurrentPolityId, Sequence = 1L,
				Id = KingdomTradeRules.OperationId(request.CurrentPolityId, 1L),
				OperationEvidenceHash = KingdomPolityTestData.DigestA,
				Kind = KingdomTradeOperationKind.PolityConsignmentDelivery,
				Disposition = phase, RequestedWater = request.RequestedDrams,
				ProvedWater = proved, SettlementId = request.SurfaceRef,
					ManifestId = request.ConsignmentId,
					PolityRecipient = witness,
					ChronicleState = KingdomTradeSinkState.Skipped,
					LedgerState = KingdomTradeSinkState.Skipped,
					MessageState = KingdomTradeSinkState.Skipped,
				DeedState = KingdomTradeSinkState.Skipped, Tick = 80L,
				Fault = phase == KingdomTradePhase.Quarantined
					? "one-shot consignment could not land" : null
			});
			return book;
		}
#endif

		internal static string RecipientBodyId()
		{
			return KingdomPolityRules.ActivationId("taf:object:polity-cohort:v1:",
				"polity-cohort-object-v1", KingdomPolityTestData.Cohort,
				"taf:cohort-member:envoy");
		}

		internal static string RecipientProjectionId()
		{
			return KingdomPolityRules.ActivationId("taf:projection:cohort:v1:",
				"polity-cohort-projection-v1", KingdomPolityTestData.Cohort,
				"taf-test-zone", KingdomPolityTestData.RivalProfile, "1");
		}

		private static KingdomPolityIncidentRecord Incident(KingdomPolityLedger ledger,
			string id)
		{
			for (int i = 0; i < ledger.Incidents.Count; i++)
				if (ledger.Incidents[i].IncidentPlanId == id) return ledger.Incidents[i];
			return null;
		}

		private static KingdomPolityRelation Relation(KingdomPolityLedger ledger)
		{
			for (int i = 0; i < ledger.Relations.Count; i++)
				if (ledger.Relations[i].FromPolityId == KingdomPolityTestData.Rival &&
					ledger.Relations[i].ToPolityId == KingdomPolityTestData.Realm)
					return ledger.Relations[i];
			return null;
		}

		private static bool HasPrefix(IList<string> values, string prefix)
		{
			for (int i = 0; i < values.Count; i++)
				if (values[i].StartsWith(prefix, StringComparison.Ordinal)) return true;
			return false;
		}
	}
}
#endif
