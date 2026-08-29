using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCorrespondenceRules
	{
		private const string RecipientUnavailableFactPrefix =
			"taf:fact:polity-consignment-recipient-unavailable:v1:";
		private const string RecipientUnavailableReceiptPrefix =
			"taf:receipt:polity-correspondence-recipient-unavailable:v1:";

		internal static string ConsignmentAbsenceDigest(
			KingdomPolityConsignmentAbsenceProof Proof)
		{
			return Proof == null ? null : KingdomPolityRules.ActivationDigest(
				"polity-consignment-absence-proof-v1", Proof.CorrespondencePlanId ?? "",
				Proof.TermsPlanId ?? "", Proof.RecipientCohortId ?? "",
				Proof.ConsignmentId ?? "", Proof.RequestDigest ?? "");
		}

		internal static bool TryApplyRecipientUnavailable(KingdomPolityLedger Ledger,
			string TermsPlanId, string CohortId, KingdomPolityConsignmentAbsenceProof Proof,
			long Tick, out bool Held, out string Failure)
		{
			Held = false; Failure = null;
			KingdomPolityIncidentRecord linked = null;
			for (int i = 0; Ledger != null && i < Ledger.Incidents.Count; i++)
			{
				KingdomPolityIncidentRecord row = Ledger.Incidents[i];
				if (!IsConsignmentPlan(row) || !KingdomPolityAuthority.Contains(
					row.ParticipantCohortRefs, CohortId) || !KingdomPolityAuthority.Contains(
					row.DisclosedStakeRefs, TermsPlanId)) continue;
				if (linked != null) return Fail(
					"envoy owns more than one linked correspondence request", out Failure);
				linked = row;
			}
			if (linked == null || linked.Conclusion != null) return true;
			if (Proof == null) { Held = true; return true; }
			if (Tick < 0L || !TryReadRequest(Ledger, linked,
				out KingdomPolityConsignmentRequest request, out Failure) ||
				!ExactAbsenceProof(Proof, request))
				return Fail(Failure ??
					"recipient-unavailable proof is changed or foreign", out Failure);
			linked.Conclusion = RecipientUnavailableConclusion(request, Proof, Tick);
			return true;
		}

		internal static bool HasOpenDeathCorrespondence(KingdomPolityLedger Ledger,
			string TermsPlanId, string CohortId)
		{
			int matches = 0;
			for (int i = 0; Ledger != null && i < Ledger.Incidents.Count; i++)
			{
				KingdomPolityIncidentRecord row = Ledger.Incidents[i];
				if (IsConsignmentPlan(row) && row.Conclusion == null &&
					KingdomPolityAuthority.Contains(row.ParticipantCohortRefs, CohortId) &&
					KingdomPolityAuthority.Contains(row.DisclosedStakeRefs, TermsPlanId)) matches++;
			}
			return matches != 0;
		}

		internal static bool TryGetOpenDeathCorrespondence(KingdomPolityLedger Ledger,
			string TermsPlanId, string CohortId, out KingdomPolityConsignmentRequest Request,
			out string Failure)
		{
			Request = null; Failure = null;
			for (int i = 0; Ledger != null && i < Ledger.Incidents.Count; i++)
			{
				KingdomPolityIncidentRecord row = Ledger.Incidents[i];
				if (!IsConsignmentPlan(row) || row.Conclusion != null ||
					!KingdomPolityAuthority.Contains(row.ParticipantCohortRefs, CohortId) ||
					!KingdomPolityAuthority.Contains(row.DisclosedStakeRefs, TermsPlanId)) continue;
				if (Request != null) return Fail(
					"envoy owns more than one open correspondence request", out Failure);
				if (!TryReadRequest(Ledger, row, out Request, out Failure)) return false;
			}
			return true;
		}

		private static KingdomPolityIncidentConclusion RecipientUnavailableConclusion(
			KingdomPolityConsignmentRequest Request,
			KingdomPolityConsignmentAbsenceProof Proof, long Tick)
		{
			string fact = Id(RecipientUnavailableFactPrefix, "recipient-unavailable-fact",
				Request.CorrespondencePlanId, Request.RecipientCohortId, Proof.ProofDigest,
				Tick.ToString(CultureInfo.InvariantCulture));
			string receipt = Id(RecipientUnavailableReceiptPrefix,
				"recipient-unavailable-receipt", Request.CorrespondencePlanId, fact);
			return Conclusion(Request.CorrespondencePlanId, "recipient-unavailable", Tick,
				fact, new List<string> { receipt });
		}

		private static bool ExactAbsenceProof(KingdomPolityConsignmentAbsenceProof Proof,
			KingdomPolityConsignmentRequest Request)
		{
			return Proof != null && Request != null &&
				Proof.CorrespondencePlanId == Request.CorrespondencePlanId &&
				Proof.TermsPlanId == Request.TermsPlanId &&
				Proof.RecipientCohortId == Request.RecipientCohortId &&
				Proof.ConsignmentId == Request.ConsignmentId &&
				Proof.RequestDigest == Request.RequestDigest &&
				KingdomPolityRules.Digest(Proof.ProofDigest) &&
				Proof.ProofDigest == ConsignmentAbsenceDigest(Proof);
		}

		private static bool IsRecipientUnavailableConclusion(
			KingdomPolityIncidentConclusion Conclusion)
		{
			return Conclusion != null && Conclusion.ObservedFactIds.Count == 1 &&
				Conclusion.ReceiptRefs.Count == 1 && Conclusion.SystemicDeltas.Count == 0 &&
				Conclusion.RelationDeltas.Count == 0 && Conclusion.ObservedFactIds[0].StartsWith(
					RecipientUnavailableFactPrefix, StringComparison.Ordinal) &&
				Conclusion.ReceiptRefs[0].StartsWith(RecipientUnavailableReceiptPrefix,
					StringComparison.Ordinal);
		}
	}
}
