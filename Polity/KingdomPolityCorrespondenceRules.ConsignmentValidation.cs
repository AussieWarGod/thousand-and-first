using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCorrespondenceRules
	{
		internal static bool IsConsignmentPlan(KingdomPolityIncidentRecord Plan)
		{
			return Plan != null && Plan.IncidentPlanId != null &&
				Plan.IncidentPlanId.StartsWith(ConsignmentPlanPrefix, StringComparison.Ordinal);
		}

		internal static bool TryValidateIncident(KingdomPolityIncidentRecord Plan,
			out string Failure)
		{
			Failure = null;
			if (!IsConsignmentPlan(Plan)) return true;
			if (Plan.GrievanceRefs.Count < 1 || Plan.ParticipantCohortRefs.Count != 1 ||
				(Plan.DisclosedStakeRefs.Count != 2 && Plan.DisclosedStakeRefs.Count != 4) ||
				Plan.MaxSystemicWound != 0 ||
				Plan.Purpose != KingdomPolityCohortPurpose.Courier || Plan.RulesVersion != 1 ||
				Plan.EventOrdinal == 0UL || Plan.EligibleSurfaceRefs.Count != 1 ||
				Plan.InterventionOptionKeys.Count != 2 ||
				Plan.InterventionOptionKeys[0] != "decline-consignment" ||
				Plan.InterventionOptionKeys[1] != "deliver-water-8-drams" ||
				Plan.Hospitality != null || Plan.Intervention != null || Plan.Aftermath != null)
				return Fail("correspondence request shape is invalid", out Failure);
			if (Plan.Conclusion == null) return true;
			KingdomPolityIncidentConclusion c = Plan.Conclusion;
			if (c.ResolutionKind != KingdomPolityResolutionKind.LiveScene ||
				c.ObservedFactIds.Count != 1 || c.SystemicDeltas.Count > 1 ||
				c.RelationDeltas.Count > 1 || c.ReceiptRefs.Count != 1 &&
				c.ReceiptRefs.Count != 3 && c.ReceiptRefs.Count != 4 &&
				c.ReceiptRefs.Count != 5 || !string.IsNullOrEmpty(c.ConsentReceiptId) ||
				!string.IsNullOrEmpty(c.EscrowReceiptId) || !string.IsNullOrEmpty(c.SnapshotReceiptId))
				return Fail("correspondence reply shape is invalid", out Failure);
			return true;
		}

		internal static bool TryValidateGraph(KingdomPolityLedger Ledger,
			KingdomPolityIncidentRecord Plan, out string Failure)
		{
			Failure = null;
			if (!IsConsignmentPlan(Plan)) return true;
			if (!TryReadRequest(Ledger, Plan, out KingdomPolityConsignmentRequest request,
				out Failure)) return false;
			KingdomPolityIncidentRecord expected = BuildPlan(Ledger, request,
				Plan.DisclosedStakeRefs.Count == 2);
			if (!ExactOpenPlan(Plan, expected))
				return Fail("correspondence request changed after publication", out Failure);
			if (Plan.Conclusion == null) return true;
			KingdomPolityCorrespondenceReplyKind reply;
			return TryReplyKind(Ledger, Plan, request, out reply, out Failure) &&
				reply != KingdomPolityCorrespondenceReplyKind.None;
		}

		internal static bool TryValidateTradeReceipt(KingdomPolityConsignmentRequest R,
			KingdomTradePolityConsignmentReceipt P, out string Failure)
		{
			Failure = null;
			bool landed = P?.Kind == KingdomTradePolityConsignmentReceiptKind.Landed;
			bool terminalFailed = P?.Kind ==
				KingdomTradePolityConsignmentReceiptKind.TerminalFailed;
			bool exactWitness = P != null && R != null && KingdomPolityRules.TypedId(
				P.RecipientBodyId, "taf:object:polity-cohort:v1:") &&
				KingdomPolityRules.TypedId(P.RecipientProjectionId,
					"taf:projection:cohort:v1:") && KingdomPolityRules.Digest(
					P.RecipientWitnessDigest) && P.RecipientWitnessDigest ==
				KingdomPolityRules.ActivationDigest("trade-polity-recipient-witness-v1",
					P.RecipientBodyId, P.RecipientCohortId, P.RecipientProjectionId,
					P.SurfaceRef, R.RequestDigest);
			if (R == null || P == null || !KingdomPolityRules.SemanticId(P.ReceiptId) ||
				!KingdomPolityRules.Digest(P.TradeOperationId) ||
				!KingdomPolityRules.Digest(P.TradeEvidenceHash) ||
				P.ConsignmentId != R.ConsignmentId ||
				P.CorrespondencePlanId != R.CorrespondencePlanId ||
				P.CounterpartyPolityId != R.CounterpartyPolityId || !exactWitness ||
				P.RecipientCohortId != R.RecipientCohortId ||
				P.SurfaceRef != R.SurfaceRef || P.RequestedDrams != R.RequestedDrams ||
				P.DebitedDrams < 0 || P.DebitedDrams > P.RequestedDrams ||
				P.DeliveredDrams < 0 || P.RetainedDrams < 0 ||
				(!landed && !terminalFailed) || (landed && (P.DebitedDrams < 1 ||
					P.DeliveredDrams != P.DebitedDrams || P.RetainedDrams != 0 ||
					!string.IsNullOrEmpty(P.FailureText))) || (terminalFailed &&
					(P.DeliveredDrams != 0 || P.RetainedDrams != P.DebitedDrams ||
					!KingdomPolityRules.Text(P.FailureText, true))) ||
				P.CommitTick < 0L || !KingdomPolityRules.Digest(P.ReceiptDigest) ||
				P.ReceiptDigest != TradeReceiptDigest(P) || P.ReceiptId != TradeReceiptId(P))
				return Fail("Trade consignment receipt is invalid, unconserved, or foreign", out Failure);
			return true;
		}

		internal static bool TryValidateConsignmentRequest(KingdomPolityLedger Ledger,
			KingdomPolityConsignmentRequest Request, out string Failure)
		{
			Failure = null;
			if (!TryValidateConsignmentRequestShape(Request, out Failure)) return false;
			KingdomPolityIncidentRecord plan = FindPlan(Ledger, Request?.CorrespondencePlanId);
			if (!TryReadRequest(Ledger, plan, out KingdomPolityConsignmentRequest expected,
				out Failure)) return false;
			if (Request.CorrespondenceId != expected.CorrespondenceId ||
				Request.TermsPlanId != expected.TermsPlanId ||
				Request.RecipientCohortId != expected.RecipientCohortId ||
				Request.CounterpartyPolityId != expected.CounterpartyPolityId ||
				Request.CurrentPolityId != expected.CurrentPolityId ||
				Request.SurfaceRef != expected.SurfaceRef || Request.NeedRef != expected.NeedRef ||
				Request.ConsignmentId != expected.ConsignmentId ||
				Request.RequestedDrams != expected.RequestedDrams ||
				Request.RequestDigest != expected.RequestDigest ||
				!KingdomPolityRules.Digest(Request.RequestDigest) || plan.Conclusion != null)
				return Fail("consignment request is changed, answered, or foreign", out Failure);
			return true;
		}

		internal static bool TryValidateConsumedTradeReceipt(KingdomPolityLedger Ledger,
			KingdomPolityConsignmentRequest Request,
			KingdomTradePolityConsignmentReceipt Receipt, out string Failure)
		{
			Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!TryValidateConsignmentRequestShape(Request, out Failure)) return false;
			KingdomPolityIncidentRecord plan = FindPlan(Ledger,
				Request.CorrespondencePlanId);
			if (!TryReadRequest(Ledger, plan, out KingdomPolityConsignmentRequest expected,
				out Failure) || expected.RequestDigest != Request.RequestDigest ||
				!TryValidateTradeReceipt(expected, Receipt, out Failure) ||
				!TryValidateReceiptRecipient(Ledger, expected, Receipt, out Failure) ||
				!TryReplyKind(Ledger, plan, expected,
					out KingdomPolityCorrespondenceReplyKind reply, out Failure) ||
				(reply != KingdomPolityCorrespondenceReplyKind.Fulfilled && reply !=
					KingdomPolityCorrespondenceReplyKind.Unfulfilled) ||
				!ReceiptMatchesConclusion(plan, expected, Receipt, reply))
				return Fail(Failure ??
					"Polity conclusion does not authenticate this Trade receipt", out Failure);
			return true;
		}

		private static bool TryValidateReceiptRecipient(KingdomPolityLedger Ledger,
			KingdomPolityConsignmentRequest Request,
			KingdomTradePolityConsignmentReceipt Receipt, out string Failure)
		{
			Failure = null;
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(Ledger,
				Request?.RecipientCohortId);
			KingdomPolityProjectionReceipt projection = cohort == null ? null :
				KingdomPolityAuthority.Projection(Ledger, cohort.ManifestationReceiptId);
			bool exactLifecycle = cohort != null && projection != null &&
				(((cohort.Phase == KingdomPolityCohortPhase.Materialized || cohort.Phase ==
					KingdomPolityCohortPhase.Concluded) && projection.Phase ==
					KingdomPolityProjectionPhase.Committed) || (cohort.Phase ==
					KingdomPolityCohortPhase.Cleaned && projection.Phase ==
					KingdomPolityProjectionPhase.Cleaned));
			if (cohort == null || projection == null || cohort.ResolvedMembers.Count < 1 ||
				cohort.Purpose != KingdomPolityCohortPurpose.Envoy ||
				!exactLifecycle ||
				cohort.PolityId != Request.CounterpartyPolityId ||
				cohort.SurfaceRef != Request.SurfaceRef || projection.SourceRef != cohort.CohortId ||
				projection.ProjectionId != Receipt.RecipientProjectionId ||
				KingdomPolityCohortRules.PreparedObjectId(cohort, 0) != Receipt.RecipientBodyId ||
				!KingdomPolityAuthority.Contains(projection.ObjectIds, Receipt.RecipientBodyId))
				return Fail("Trade receipt does not land at the exact witnessed envoy projection",
					out Failure);
			return true;
		}

		internal static bool TryValidateConsignmentRequestShape(
			KingdomPolityConsignmentRequest R, out string Failure)
		{
			Failure = null;
			if (R == null || !KingdomPolityRules.TypedId(R.CorrespondencePlanId,
				ConsignmentPlanPrefix) || !KingdomPolityRules.TypedId(R.CorrespondenceId,
				"taf:incident:correspondence:v1:") || !KingdomPolityRules.TypedId(
				R.TermsPlanId, "taf:incident-plan:") || !KingdomPolityRules.TypedId(
				R.RecipientCohortId, "taf:cohort:") || !KingdomPolityRules.SemanticId(
				R.CounterpartyPolityId) || !KingdomPolityRules.SemanticId(R.CurrentPolityId) ||
				R.CounterpartyPolityId == R.CurrentPolityId || !KingdomPolityRules.TypedId(
				R.SurfaceRef, "taf:settlement:v1:") || !KingdomPolityRules.TypedId(R.NeedRef,
				"taf:need:polity-water:v1:") || !KingdomPolityRules.TypedId(R.ConsignmentId,
				"taf:manifest:polity-consignment:v1:") ||
				R.RequestedDrams != FirstContactWaterDrams || !KingdomPolityRules.Digest(
				R.RequestDigest) || R.RequestDigest != RequestDigest(R))
				return Fail("consignment request identity or commitment is invalid", out Failure);
			return true;
		}

		internal static string TradeReceiptDigest(KingdomTradePolityConsignmentReceipt P)
		{
			return KingdomPolityRules.ActivationDigest("trade-polity-consignment-receipt-v1",
				N((byte)P.Kind), P.TradeOperationId ?? "", P.TradeEvidenceHash ?? "",
				P.ConsignmentId ?? "",
				P.CorrespondencePlanId ?? "", P.CounterpartyPolityId ?? "", P.SurfaceRef ?? "",
				P.RecipientBodyId ?? "", P.RecipientCohortId ?? "",
				P.RecipientProjectionId ?? "", P.RecipientWitnessDigest ?? "",
				N(P.RequestedDrams), N(P.DebitedDrams), N(P.DeliveredDrams),
				N(P.RetainedDrams), P.FailureText ?? "", N(P.CommitTick));
		}

		internal static string TradeReceiptId(KingdomTradePolityConsignmentReceipt P)
		{
			return KingdomPolityRules.ActivationId("taf:receipt:trade-polity-consignment:v1:",
				"trade-polity-consignment-receipt-v1", P.TradeOperationId ?? "",
				P.ConsignmentId ?? "", P.TradeEvidenceHash ?? "");
		}

		private static string RequestDigest(KingdomPolityConsignmentRequest R)
		{
			return KingdomPolityRules.ActivationDigest("polity-consignment-request-v1",
				R.CorrespondencePlanId ?? "", R.CorrespondenceId ?? "", R.TermsPlanId ?? "",
				R.RecipientCohortId ?? "", R.CounterpartyPolityId ?? "", R.CurrentPolityId ?? "",
				R.SurfaceRef ?? "", R.NeedRef ?? "", R.ConsignmentId ?? "", N(R.RequestedDrams));
		}

		private static string Id(string Prefix, string Kind, params string[] Values)
		{
			return KingdomPolityRules.ActivationId(Prefix, "polity-consignment-v1-" + Kind, Values);
		}

		private static string N(long Value)
		{
			return Value.ToString(CultureInfo.InvariantCulture);
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
