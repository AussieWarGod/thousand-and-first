using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityDiplomacyRules
	{
		private static bool DeriveBrokenPact(KingdomPolityLedger L,
			KingdomPolityGrievanceIngressRequest R, out KingdomPolityGrievanceRecord G,
			out string Failure)
		{
			G = null; Failure = null;
			KingdomPolityIncidentRecord plan = FindPlan(L, R.SourceRef);
			KingdomPolityIncidentConclusion conclusion = plan?.Conclusion;
			KingdomPolityRelationDelta selected = null;
			for (int i = 0; conclusion != null && i < conclusion.RelationDeltas.Count; i++)
			{
				KingdomPolityRelationDelta delta = conclusion.RelationDeltas[i];
				bool broken = (delta.Before == KingdomPolityRelationBand.Pact ||
					delta.Before == KingdomPolityRelationBand.Truce) &&
					(delta.After == KingdomPolityRelationBand.Rival ||
					 delta.After == KingdomPolityRelationBand.Hostile);
				if (!broken) continue;
				if (delta.ReceiptId == R.SourceReceiptId) selected = delta;
			}
			KingdomPolityRelation relation = selected == null ? null :
				FindRelationById(L, selected.RelationId);
			if (conclusion == null || conclusion.ResolutionKind !=
				KingdomPolityResolutionKind.LiveScene || selected == null ||
				relation == null || relation.FromPolityId != R.IssuerPolityId ||
				relation.ToPolityId != R.TargetPolityId ||
				!KingdomPolityAuthority.Contains(conclusion.ReceiptRefs,
					selected.ReceiptId) || conclusion.ObservedFactIds.Count < 1)
				return IngressFail("broken pact lacks one exact hostile relation delta",
					out Failure);
			string id = KingdomPolityRules.ActivationId("taf:grievance:broken-pact:v1:",
				"polity-broken-pact-v1", plan.IncidentPlanId, conclusion.ConclusionId,
				selected.ReceiptId);
			return BuildIngress(id, R, KingdomPolityGrievanceCause.BrokenPact,
				selected.ReceiptId, 4, new List<string> { plan.IncidentPlanId,
					conclusion.ConclusionId, selected.ReceiptId,
					conclusion.ObservedFactIds[0] }, out G, out Failure);
		}

		private static bool DeriveResourceRefusal(KingdomPolityLedger L,
			KingdomPolityGrievanceIngressRequest R, out KingdomPolityGrievanceRecord G,
			out string Failure)
		{
			G = null; Failure = null;
			if (!KingdomPolityCorrespondenceRules.TryDescribeConsignment(L, R.SourceRef,
				out KingdomPolityConsignmentRequest request,
				out KingdomPolityCorrespondenceReplyKind reply, out Failure) ||
				reply != KingdomPolityCorrespondenceReplyKind.Declined)
				return IngressFail(Failure ?? "resource request has no exact decline",
					out Failure);
			KingdomPolityIncidentRecord plan = FindPlan(L, R.SourceRef);
			KingdomPolityIncidentConclusion conclusion = plan?.Conclusion;
			if (request.CounterpartyPolityId != R.IssuerPolityId ||
				request.CurrentPolityId != R.TargetPolityId || conclusion == null ||
				conclusion.ReceiptRefs.Count != 1 ||
				conclusion.ReceiptRefs[0] != R.SourceReceiptId ||
				conclusion.ObservedFactIds.Count != 1)
				return IngressFail("resource refusal endpoints or receipt changed", out Failure);
			string id = KingdomPolityRules.ActivationId("taf:grievance:resource-refusal:v1:",
				"polity-resource-refusal-v1", plan.IncidentPlanId, request.NeedRef,
				conclusion.ObservedFactIds[0]);
			return BuildIngress(id, R, KingdomPolityGrievanceCause.ResourceRefusal,
				conclusion.ObservedFactIds[0], 2, new List<string> { plan.IncidentPlanId,
					request.NeedRef, request.ConsignmentId, conclusion.ConclusionId,
					R.SourceReceiptId }, out G, out Failure);
		}

		private static bool DeriveRefusedTerms(KingdomPolityLedger L,
			KingdomPolityGrievanceIngressRequest R, out KingdomPolityGrievanceRecord G,
			out string Failure)
		{
			G = null; Failure = null;
			KingdomPolityIncidentRecord plan = FindPlan(L, R.SourceRef);
			KingdomPolityIncidentConclusion conclusion = plan?.Conclusion;
			KingdomPolityGrievanceRecord original = plan == null ||
				plan.GrievanceRefs.Count != 1 ? null : FindGrievance(L, plan.GrievanceRefs[0]);
			string answerFact = RefusalFact(plan);
			string receipt = conclusion == null ? null : KingdomPolityRules.ActivationId(
				"taf:receipt:polity-terms:v1:", "polity-terms-receipt-v1",
				conclusion.ConclusionId);
			if (plan == null || plan.Purpose != KingdomPolityCohortPurpose.Envoy ||
				conclusion == null || original == null || answerFact == null ||
				original.IssuerPolityId != R.IssuerPolityId ||
				original.TargetPolityId != R.TargetPolityId || receipt != R.SourceReceiptId ||
				!KingdomPolityAuthority.Contains(conclusion.ReceiptRefs, receipt))
				return IngressFail("refused terms lack their exact witnessed conclusion",
					out Failure);
			string id = KingdomPolityRules.ActivationId(
				"taf:grievance:refused-terms:v1:", "polity-refused-terms-v1",
				plan.IncidentPlanId, answerFact);
			return BuildIngress(id, R, KingdomPolityGrievanceCause.RefusedTerms,
				answerFact, Math.Min(5, original.Severity + 1), new List<string>
					{ plan.IncidentPlanId, conclusion.ConclusionId, answerFact, receipt },
				out G, out Failure);
		}

		private static string RefusalFact(KingdomPolityIncidentRecord Plan)
		{
			for (int i = 0; Plan?.Conclusion != null &&
				i < Plan.Conclusion.ObservedFactIds.Count; i++)
			{
				string fact = Plan.Conclusion.ObservedFactIds[i];
				if (TermsConclusionId(Plan.IncidentPlanId,
					KingdomPolityTermsChoice.Refuse, fact) == Plan.Conclusion.ConclusionId)
					return fact;
			}
			return null;
		}

		private static bool BuildIngress(string Id,
			KingdomPolityGrievanceIngressRequest R, KingdomPolityGrievanceCause Cause,
			string SourceEvent, int Severity, IList<string> Evidence,
			out KingdomPolityGrievanceRecord G, out string Failure)
		{
			G = null; Failure = null;
			List<string> canonical = new List<string>();
			for (int i = 0; Evidence != null && i < Evidence.Count; i++)
			{
				if (!KingdomPolityRules.SemanticId(Evidence[i]))
					return IngressFail("exact grievance evidence is not semantic", out Failure);
				KingdomPolityAuthority.AddSortedUnique(canonical, Evidence[i]);
			}
			if (!KingdomPolityRules.TypedId(Id, "taf:grievance:") ||
				!KingdomPolityRules.SemanticId(SourceEvent) || canonical.Count < 1 ||
				canonical.Count > KingdomPolityRules.MaxRefs)
				return IngressFail("exact grievance evidence exceeds its bound", out Failure);
			G = new KingdomPolityGrievanceRecord
			{
				GrievanceId = Id, IssuerPolityId = R.IssuerPolityId,
				TargetPolityId = R.TargetPolityId, Cause = Cause,
				SourceEventId = SourceEvent, Severity = Severity,
				EvidenceRefs = canonical, Phase = KingdomPolityGrievancePhase.Open
			};
			return true;
		}

		internal static void InsertGrievance(KingdomPolityLedger L,
			KingdomPolityGrievanceRecord G)
		{
			int at = 0;
			while (at < L.Grievances.Count && string.CompareOrdinal(
				L.Grievances[at].GrievanceId, G.GrievanceId) < 0) at++;
			L.Grievances.Insert(at, G);
		}

		internal static bool TryInsertBrokenPactGrievances(KingdomPolityLedger L,
			KingdomPolityIncidentRecord Plan, out string Failure)
		{
			Failure = null;
			for (int i = 0; Plan?.Conclusion != null &&
				i < Plan.Conclusion.RelationDeltas.Count; i++)
			{
				KingdomPolityRelationDelta delta = Plan.Conclusion.RelationDeltas[i];
				if ((delta.Before != KingdomPolityRelationBand.Pact &&
					delta.Before != KingdomPolityRelationBand.Truce) ||
					(delta.After != KingdomPolityRelationBand.Rival &&
					 delta.After != KingdomPolityRelationBand.Hostile)) continue;
				KingdomPolityRelation relation = FindRelationById(L, delta.RelationId);
				if (relation == null)
					return IngressFail("broken pact relation target is missing", out Failure);
				KingdomPolityGrievanceIngressRequest ingress =
					new KingdomPolityGrievanceIngressRequest
					{
						SourceKind = KingdomPolityGrievanceSourceKind.BrokenPact,
						SourceRef = Plan.IncidentPlanId, SourceReceiptId = delta.ReceiptId,
						IssuerPolityId = relation.FromPolityId,
						TargetPolityId = relation.ToPolityId
					};
				if (!TryDeriveExactGrievance(L, ingress,
					out KingdomPolityGrievanceRecord expected, out Failure)) return false;
				KingdomPolityGrievanceRecord existing = FindGrievance(L,
					expected.GrievanceId);
				if (existing != null)
				{
					if (!ExactOpenGrievance(existing, expected))
						return IngressFail("broken pact retry changed source", out Failure);
					continue;
				}
				for (int j = 0; j < L.Grievances.Count; j++)
					if (L.Grievances[j].SourceEventId == expected.SourceEventId)
						return IngressFail("broken pact receipt already emitted a grievance",
							out Failure);
				if (L.Grievances.Count >= KingdomPolityRules.MaxGrievances)
					return IngressFail("broken pact grievance capacity is exhausted",
						out Failure);
				InsertGrievance(L, expected);
			}
			return true;
		}

		private static bool HasExactRouteEnvoy(KingdomPolityLedger L,
			KingdomPolityRouteRecord Route, string PolityId)
		{
			int found = 0;
			for (int i = 0; i < L.Cohorts.Count; i++)
			{
				KingdomPolityCohortPlan c = L.Cohorts[i];
				if (c.Purpose == KingdomPolityCohortPurpose.Envoy &&
					c.SourceRef == Route.RouteId && c.PolityId == PolityId &&
					c.SurfaceRef == Route.DestinationId) found++;
			}
			return found == 1;
		}

		private static bool ExactParticipantPolity(KingdomPolityLedger L,
			KingdomPolityIncidentRecord Plan, string PolityId)
		{
			if (Plan.ParticipantCohortRefs.Count < 1) return false;
			for (int i = 0; i < Plan.ParticipantCohortRefs.Count; i++)
			{
				KingdomPolityCohortPlan c = KingdomPolityAuthority.Cohort(L,
					Plan.ParticipantCohortRefs[i]);
				if (c == null || c.PolityId != PolityId) return false;
			}
			return true;
		}

		private static bool HasEndpointCause(KingdomPolityLedger L,
			KingdomPolityIncidentRecord Plan, string A, string B)
		{
			for (int i = 0; i < Plan.GrievanceRefs.Count; i++)
			{
				KingdomPolityGrievanceRecord g = FindGrievance(L, Plan.GrievanceRefs[i]);
				if (g != null && (g.IssuerPolityId == A && g.TargetPolityId == B ||
					g.IssuerPolityId == B && g.TargetPolityId == A)) return true;
			}
			return false;
		}

		private static KingdomPolityRelation FindRelationById(KingdomPolityLedger L,
			string Id)
		{
			for (int i = 0; L != null && i < L.Relations.Count; i++)
				if (L.Relations[i].RelationId == Id) return L.Relations[i];
			return null;
		}

		private static bool IngressFail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
