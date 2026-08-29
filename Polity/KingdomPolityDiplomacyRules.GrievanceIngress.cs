using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityDiplomacyRules
	{
		/// <summary>Ingests only one exact, already-authored ledger receipt.</summary>
		public static bool TryIngestExactGrievance(KingdomPolityLedger Ledger,
			long ExpectedRevision, KingdomPolityGrievanceIngressRequest Request,
			out string GrievanceId, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			GrievanceId = null; Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!TryDeriveExactGrievance(Ledger, Request,
					out KingdomPolityGrievanceRecord expected, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			GrievanceId = expected.GrievanceId;
			KingdomPolityGrievanceRecord existing = FindGrievance(Ledger, GrievanceId);
			if (existing != null)
			{
				if (!ExactOpenGrievance(existing, expected))
					return KingdomPolityAuthority.Refuse(Result,
						"grievance retry changed its exact source receipt", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			for (int i = 0; i < Ledger.Grievances.Count; i++)
				if (Ledger.Grievances[i].SourceEventId == expected.SourceEventId)
					return KingdomPolityAuthority.Refuse(Result,
						"authored source already emitted one grievance", out Failure);
			if (Ledger.Grievances.Count >= KingdomPolityRules.MaxGrievances)
				return KingdomPolityAuthority.Refuse(Result,
					"grievance capacity is exhausted", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			InsertGrievance(candidate, expected);
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		internal static bool TryDeriveExactGrievance(KingdomPolityLedger Ledger,
			KingdomPolityGrievanceIngressRequest Request,
			out KingdomPolityGrievanceRecord Grievance, out string Failure)
		{
			Grievance = null; Failure = null;
			if (!ValidIngressShape(Request))
				return IngressFail("grievance source selector is invalid", out Failure);
			if (KingdomPolityAuthority.Polity(Ledger, Request.IssuerPolityId) == null ||
				KingdomPolityAuthority.Polity(Ledger, Request.TargetPolityId) == null)
				return IngressFail("grievance endpoint polity is missing", out Failure);
			switch (Request.SourceKind)
			{
			case KingdomPolityGrievanceSourceKind.ClaimDeparture:
				return DeriveClaim(Ledger, Request, out Grievance, out Failure);
			case KingdomPolityGrievanceSourceKind.WitnessedTrespass:
				return DeriveTrespass(Ledger, Request, out Grievance, out Failure);
			case KingdomPolityGrievanceSourceKind.BrokenPact:
				return DeriveBrokenPact(Ledger, Request, out Grievance, out Failure);
			case KingdomPolityGrievanceSourceKind.ResourceRefusal:
				return DeriveResourceRefusal(Ledger, Request, out Grievance, out Failure);
			case KingdomPolityGrievanceSourceKind.RefusedTerms:
				return DeriveRefusedTerms(Ledger, Request, out Grievance, out Failure);
			case KingdomPolityGrievanceSourceKind.DesignatedTheftReceipt:
				return IngressFail("no exact authored theft/custody receipt authority exists",
					out Failure);
			case KingdomPolityGrievanceSourceKind.WitnessedEnvoyHarm:
				return DeriveWitnessedEnvoyHarm(Ledger, Request, out Grievance, out Failure);
			default: return IngressFail("grievance source kind is unsupported", out Failure);
			}
		}

		private static bool DeriveClaim(KingdomPolityLedger L,
			KingdomPolityGrievanceIngressRequest R, out KingdomPolityGrievanceRecord G,
			out string Failure)
		{
			G = null; Failure = null;
			KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(L, R.SourceRef);
			KingdomPolityRelation relation = FindRelation(L, R.IssuerPolityId,
				R.TargetPolityId);
			string expectedRoute = FirstContactId("taf:route:legacy-visit:v1:", "route",
				R.IssuerPolityId, R.TargetPolityId);
			string eventId = FirstContactId("taf:event:legacy-claim:v1:", "claim-event",
				R.SourceRef);
			string grievanceId = FirstContactId("taf:grievance:legacy-claim:v1:",
				"grievance", R.SourceRef);
			if (route == null || route.RouteId != expectedRoute ||
				route.Purpose != KingdomPolityRoutePurpose.Delegation ||
				route.CounterpartyRef != R.TargetPolityId || route.Phase ==
					KingdomPolityRoutePhase.Preparing || route.Phase ==
					KingdomPolityRoutePhase.Cancelled ||
				route.DepartureReceiptId != R.SourceReceiptId || relation == null ||
				(relation.Band != KingdomPolityRelationBand.Rival &&
				 relation.Band != KingdomPolityRelationBand.Hostile) ||
				!HasExactRouteEnvoy(L, route, R.IssuerPolityId))
				return IngressFail("claim lacks its exact hostile departure receipt",
					out Failure);
			List<string> evidence = new List<string>
				{ route.RouteId, route.DepartureReceiptId, relation.RelationId };
			evidence.AddRange(relation.SourceRefs);
			return BuildIngress(grievanceId, R, KingdomPolityGrievanceCause.Claim,
				eventId, 2, evidence, out G, out Failure);
		}

		private static bool DeriveTrespass(KingdomPolityLedger L,
			KingdomPolityGrievanceIngressRequest R, out KingdomPolityGrievanceRecord G,
			out string Failure)
		{
			G = null; Failure = null;
			KingdomPolityIncidentRecord plan = FindPlan(L, R.SourceRef);
			KingdomPolityInterventionRecord intervention = plan?.Intervention;
			KingdomPolityRecord issuer = KingdomPolityAuthority.Polity(L, R.IssuerPolityId);
			if (plan == null || plan.Purpose != KingdomPolityCohortPurpose.Warband ||
				intervention == null || intervention.Choice !=
					KingdomPolityInterventionChoice.SupportSettlement ||
				intervention.ReceiptId != R.SourceReceiptId || issuer == null ||
				issuer.Source != KingdomPolitySource.CurrentRealm ||
				!ExactParticipantPolity(L, plan, R.TargetPolityId) ||
				!HasEndpointCause(L, plan, R.IssuerPolityId, R.TargetPolityId))
				return IngressFail("trespass lacks exact witnessed settlement support",
					out Failure);
			string id = KingdomPolityRules.ActivationId("taf:grievance:trespass:v1:",
				"polity-witnessed-trespass-v1", plan.IncidentPlanId,
				intervention.InterventionId, R.IssuerPolityId, R.TargetPolityId);
			List<string> evidence = new List<string> { plan.IncidentPlanId,
				intervention.InterventionId, intervention.ObservedFactId,
				intervention.ReceiptId, intervention.SurfaceRef };
			evidence.AddRange(intervention.ParticipantProjectionIds);
			return BuildIngress(id, R, KingdomPolityGrievanceCause.Trespass,
				intervention.ObservedFactId, 2, evidence, out G, out Failure);
		}

		private static bool ValidIngressShape(KingdomPolityGrievanceIngressRequest R)
		{
			return R != null && R.SourceKind > KingdomPolityGrievanceSourceKind.None &&
				R.SourceKind <= KingdomPolityGrievanceSourceKind.WitnessedEnvoyHarm &&
				KingdomPolityRules.SemanticId(R.SourceRef) &&
				KingdomPolityRules.SemanticId(R.SourceReceiptId) &&
				KingdomPolityRules.SemanticId(R.IssuerPolityId) &&
				KingdomPolityRules.SemanticId(R.TargetPolityId) &&
				R.IssuerPolityId != R.TargetPolityId;
		}

		private static string FirstContactId(string Prefix, string Kind, params string[] Values)
		{
			string[] input = new string[Values.Length + 1]; input[0] = Kind;
			for (int i = 0; i < Values.Length; i++) input[i + 1] = Values[i];
			return KingdomPolityRules.ActivationId(Prefix, "polity-first-contact-v1", input);
		}
	}
}
