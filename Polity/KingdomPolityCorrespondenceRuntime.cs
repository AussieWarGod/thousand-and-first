namespace ThousandAndFirst
{
	/// <summary>Consumes only terminal Trade proof; it never loads ground or owns cargo.</summary>
	internal static class KingdomPolityCorrespondenceRuntime
	{
		internal static bool TryEnsureFirstContact(KingdomSystem System,
			KingdomPolityVisitPlan Plan, out string Failure)
		{
			Failure = null;
			if (System?.PolityLedger == null || Plan == null) return false;
			if (Plan.HostileContact) return true;
			KingdomPolityIncidentRecord terms = null; bool existing = false;
			for (int i = 0; i < System.PolityLedger.Incidents.Count; i++)
			{
				KingdomPolityIncidentRecord row = System.PolityLedger.Incidents[i];
				if (row.IncidentPlanId == Plan.TermsPlanId) terms = row;
				if (KingdomPolityCorrespondenceRules.IsConsignmentPlan(row) &&
					KingdomPolityAuthority.Contains(row.ParticipantCohortRefs,
						Plan.EnvoyCohortId) && KingdomPolityAuthority.Contains(
						row.DisclosedStakeRefs, Plan.TermsPlanId)) existing = true;
			}
			KingdomPolityCohortPlan envoy = KingdomPolityAuthority.Cohort(
				System.PolityLedger, Plan.EnvoyCohortId);
			if (!existing && (terms?.Conclusion != null || envoy == null ||
				envoy.Phase != KingdomPolityCohortPhase.Materialized)) return true;
			return KingdomPolityCorrespondenceRules.TryPlanConsignment(System.PolityLedger,
				System.PolityLedger.Revision, Plan.TermsPlanId, Plan.EnvoyCohortId,
				Plan.SurfaceId, out KingdomPolityConsignmentRequest _,
				out KingdomPolityPublicationResult _, out Failure);
		}

		internal static bool TryRecoverTradeReceipts(KingdomSystem System, out string Failure)
		{
			Failure = null;
			if (System?.PolityLedger == null) return false;
			for (int i = 0; i < System.PolityLedger.Incidents.Count; i++)
			{
				KingdomPolityIncidentRecord plan = System.PolityLedger.Incidents[i];
				if (!KingdomPolityCorrespondenceRules.IsConsignmentPlan(plan)) continue;
				if (!KingdomPolityCorrespondenceRules.TryDescribeConsignment(
					System.PolityLedger, plan.IncidentPlanId,
					out KingdomPolityConsignmentRequest request,
					out KingdomPolityCorrespondenceReplyKind reply, out Failure)) return false;
				if (System.TradeBook == null) continue;
				if (!KingdomTradeRules.TryInspectPolityConsignmentReceipt(System.TradeBook,
					request, out KingdomTradePolityConsignmentReceipt receipt,
					out KingdomTradePolityConsignmentReceiptKind kind, out string receiptFailure))
				{
					Failure = null; continue;
				}
				if (kind == KingdomTradePolityConsignmentReceiptKind.Missing) continue;
				if (kind == KingdomTradePolityConsignmentReceiptKind.Invalid)
				{
					KingdomLog.Log("polity: invalid consignment proof retained for " +
						(plan.IncidentPlanId ?? "?") + ": " + (receiptFailure ?? "unknown"));
					continue;
				}
				if (reply == KingdomPolityCorrespondenceReplyKind.None &&
					!KingdomPolityCorrespondenceRules.TryConsumeTradeReceipt(
						System.PolityLedger, System.PolityLedger.Revision, receipt,
						out KingdomPolityPublicationResult _, out Failure))
				{
					KingdomLog.Log("polity: consignment reply retained for recovery at " +
						(plan.IncidentPlanId ?? "?") + ": " + (Failure ?? "unknown"));
					Failure = null;
					continue;
				}
				if (!KingdomTradeRules.TryAcknowledgePolityConsignment(System.TradeBook,
					System.PolityLedger, request, receipt, out bool _, out string ackFailure))
					KingdomLog.Log("polity: consumed consignment proof remains retained at " +
						(plan.IncidentPlanId ?? "?") + ": " + (ackFailure ?? "unknown"));
			}
			return true;
		}

		internal static bool TryGetEnvoyDeathAbsence(KingdomSystem System,
			string TermsPlanId, string CohortId,
			out KingdomPolityConsignmentAbsenceProof Proof, out bool CustodyOrProofExists,
			out string Failure)
		{
			Proof = null; CustodyOrProofExists = false; Failure = null;
			if (System?.PolityLedger == null) return false;
			if (!KingdomPolityCorrespondenceRules.TryGetOpenDeathCorrespondence(
				System.PolityLedger, TermsPlanId, CohortId,
				out KingdomPolityConsignmentRequest request, out Failure)) return false;
			if (request == null) return true;
			if (System.TradeBook == null)
			{
				CustodyOrProofExists = true; return true;
			}
			if (KingdomTradeRules.TryProveNoPolityConsignmentCustody(System.TradeBook,
				request, out Proof, out CustodyOrProofExists, out string proofFailure)) return true;
			KingdomLog.Log("polity: consignment absence proof retained for inspection at " +
				(request.CorrespondencePlanId ?? "?") + ": " + (proofFailure ?? "unknown"));
			CustodyOrProofExists = true; Failure = null; return true;
		}

		internal static bool TryRecoverEnvoyDeaths(KingdomSystem System, out string Failure)
		{
			Failure = null;
			if (System?.PolityLedger == null) return false;
			KingdomPolityConsignmentAbsenceProof absence = null;
			for (int i = 0; i < System.PolityLedger.Incidents.Count; i++)
			{
				KingdomPolityIncidentRecord plan = System.PolityLedger.Incidents[i];
				if (plan.ParticipantCohortRefs.Count != 1 ||
					!KingdomPolityDiplomacyRules.IsPendingEnvoyDeathClosure(
						System.PolityLedger, plan.IncidentPlanId,
						plan.ParticipantCohortRefs[0]) ||
					!KingdomPolityCorrespondenceRules.HasOpenDeathCorrespondence(
						System.PolityLedger, plan.IncidentPlanId,
						plan.ParticipantCohortRefs[0])) continue;
				if (!TryGetEnvoyDeathAbsence(System, plan.IncidentPlanId,
					plan.ParticipantCohortRefs[0], out absence, out bool _, out Failure)) return false;
				break;
			}
			return KingdomPolityDiplomacyRules.TryRecoverEnvoyDeaths(System.PolityLedger,
				System.PolityLedger.Revision, absence, out int _, out int _,
				out KingdomPolityPublicationResult _, out Failure);
		}
	}
}
