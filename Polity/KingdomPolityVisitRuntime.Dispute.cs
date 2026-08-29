using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityVisitRuntime
	{
		private static bool EnsureDispute(KingdomSystem System, KingdomPolityVisitPlan P,
			long Tick, out string Failure)
		{
			KingdomPolityLedger L = System.PolityLedger;
			Failure = null;
			if (Incident(L, P.TermsPlanId) != null || !P.HostileContact) return true;
			if (!KingdomPolityRules.CanEmitOptionalProjection(L, P.DepartureTick)) return true;
			KingdomPolityCohortPlan existing = KingdomPolityAuthority.Cohort(L,
				P.WarbandCohortId);
			if (existing != null && (existing.Phase == KingdomPolityCohortPhase.Cancelled ||
				existing.Phase == KingdomPolityCohortPhase.Cleaned ||
				existing.Phase == KingdomPolityCohortPhase.Abandoned ||
				existing.Phase == KingdomPolityCohortPhase.Archived)) return true;
			if (existing == null && !KingdomPolityAttentionRules.TryAdmitPlan(L, 3,
				out string _)) return true;
			if (!KingdomPolityExperienceRuntime.TryReserveDirectedPlan(System, P.WarbandCohortId,
				P.SurfaceId, 3, P.DepartureTick, Tick,
				out KingdomPolityPresentationAuthorityProof authority, out bool _,
				out KingdomExperienceCapacityFault fault, out Failure))
			{
				if (KingdomPolityExperienceRuntime.ExpectedCapacityRefusal(fault))
				{
					Failure = null; return true;
				}
				return false;
			}
			KingdomPolityGrievanceIngressRequest grievance =
				new KingdomPolityGrievanceIngressRequest
			{
				SourceKind = KingdomPolityGrievanceSourceKind.ClaimDeparture,
				SourceRef = P.RouteId, SourceReceiptId = P.DepartureReceiptId,
				IssuerPolityId = P.Visitor.PolityId, TargetPolityId = P.Current.PolityId
			};
			if (!KingdomPolityDiplomacyRules.TryIngestExactGrievance(L, L.Revision,
				grievance, out string grievanceId, out KingdomPolityPublicationResult _,
				out Failure) || grievanceId != P.GrievanceId)
			{
				string cause = Failure ?? "claim receipt resolved to another grievance";
				return RollBackPresentation(System, P.WarbandCohortId, existing == null, cause,
					out Failure);
			}
			KingdomPolityCohortPlanRequest warband = new KingdomPolityCohortPlanRequest
			{
				CohortId = P.WarbandCohortId, Purpose = KingdomPolityCohortPurpose.Warband,
				SourceRef = P.ClaimEventId, PolityId = P.Visitor.PolityId,
				SurfaceRef = P.SurfaceId, MemberCount = 3,
				NamedFigureId = P.Claimant?.FigureId, EventStreamId = P.StreamId,
				RulesVersion = KingdomPolityNpcRules.RulesVersion, EventOrdinal = 1UL,
				PresentationAuthority = authority
			};
			if (!KingdomPolityCohortRules.TryPlan(L, L.Revision, warband,
				out KingdomPolityPublicationResult _, out Failure))
			{
				string cause = Failure;
				return RollBackPresentation(System, P.WarbandCohortId, existing == null, cause,
					out Failure);
			}
			KingdomPolityTermsPlanRequest terms = new KingdomPolityTermsPlanRequest
			{
				GrievanceId = P.GrievanceId, TermsPlanId = P.TermsPlanId,
				TermsIncidentId = P.TermsIncidentId, ClashPlanId = P.ClashPlanId,
				ClashIncidentId = P.ClashIncidentId, EnvoyCohortId = P.EnvoyCohortId,
				ClashCohortRefs = new List<string> { P.WarbandCohortId },
				DisclosedStakeRefs = new List<string> { P.RouteId },
				EligibleSurfaceRefs = new List<string> { P.SurfaceId },
				TermKeys = new List<string> { "mutual-recognition", "safe-passage" },
				EventStreamId = P.StreamId, RulesVersion = KingdomPolityDiplomacyRules.RulesVersion,
				EventOrdinal = 2UL, MaxSystemicWound = 1
			};
			return KingdomPolityDiplomacyRules.TryPlanTerms(L, L.Revision, terms,
				out KingdomPolityPublicationResult _, out Failure);
		}

		private static bool RollBackPresentation(KingdomSystem System, string CohortId,
			bool Release, string Cause, out string Failure)
		{
			Failure = Cause;
			if (!Release) return false;
			if (!KingdomPolityExperienceRuntime.TryReleaseDirected(System, CohortId,
				out string releaseFailure)) Failure = Cause +
				"; presentation rollback failed: " + releaseFailure;
			return false;
		}

		private static KingdomPolityIncidentRecord Incident(KingdomPolityLedger L, string PlanId)
		{
			for (int i = 0; L != null && i < L.Incidents.Count; i++)
				if (L.Incidents[i].IncidentPlanId == PlanId) return L.Incidents[i];
			return null;
		}
	}
}
