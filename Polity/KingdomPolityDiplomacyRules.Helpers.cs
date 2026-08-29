using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityDiplomacyRules
	{
		private static KingdomPolityGrievanceRecord Grievance(KingdomPolityGrievanceRequest R)
		{
			return new KingdomPolityGrievanceRecord
			{
				GrievanceId = R.GrievanceId, IssuerPolityId = R.IssuerPolityId,
				TargetPolityId = R.TargetPolityId, Cause = R.Cause,
				SourceEventId = R.SourceEventId, Severity = R.Severity,
				EvidenceRefs = new List<string>(R.EvidenceRefs),
				Phase = KingdomPolityGrievancePhase.Open
			};
		}

		private static KingdomPolityIncidentRecord TermsIncident(KingdomPolityTermsPlanRequest R)
		{
			return new KingdomPolityIncidentRecord
			{
				IncidentPlanId = R.TermsPlanId, IncidentId = R.TermsIncidentId,
				GrievanceRefs = new List<string> { R.GrievanceId },
				ParticipantCohortRefs = new List<string> { R.EnvoyCohortId },
				DisclosedStakeRefs = new List<string>(R.DisclosedStakeRefs),
				MaxSystemicWound = 0, Purpose = KingdomPolityCohortPurpose.Envoy,
				EventStreamId = R.EventStreamId, RulesVersion = R.RulesVersion,
				EventOrdinal = R.EventOrdinal,
				EligibleSurfaceRefs = new List<string>(R.EligibleSurfaceRefs),
				InterventionOptionKeys = new List<string>(R.TermKeys)
			};
		}

		private static KingdomPolityIncidentRecord ClashIncident(KingdomPolityTermsPlanRequest R)
		{
			return new KingdomPolityIncidentRecord
			{
				IncidentPlanId = R.ClashPlanId, IncidentId = R.ClashIncidentId,
				GrievanceRefs = new List<string> { R.GrievanceId },
				ParticipantCohortRefs = new List<string>(R.ClashCohortRefs),
				DisclosedStakeRefs = new List<string>(R.DisclosedStakeRefs),
				MaxSystemicWound = R.MaxSystemicWound,
				Purpose = KingdomPolityCohortPurpose.Warband,
				EventStreamId = R.EventStreamId, RulesVersion = R.RulesVersion,
				EventOrdinal = R.EventOrdinal,
				EligibleSurfaceRefs = new List<string>(R.EligibleSurfaceRefs),
				InterventionOptionKeys = new List<string>
					{ "assist-attacker", "assist-defender", "consent-abstract-resolution",
						"mediate-ceasefire", "observe" }
			};
		}

		private static bool ValidClashCohorts(KingdomPolityLedger L,
			KingdomPolityTermsPlanRequest R)
		{
			for (int i = 0; i < R.ClashCohortRefs.Count; i++)
			{
				KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(L,
					R.ClashCohortRefs[i]);
				if (cohort == null || (cohort.Purpose != KingdomPolityCohortPurpose.Warband &&
					cohort.Purpose != KingdomPolityCohortPurpose.Guard &&
					cohort.Purpose != KingdomPolityCohortPurpose.Patrol) ||
					!KingdomPolityAuthority.Contains(R.EligibleSurfaceRefs, cohort.SurfaceRef)) return false;
			}
			return true;
		}

		private static KingdomPolityGrievanceRecord FindGrievance(KingdomPolityLedger L,
			string Id)
		{
			for (int i = 0; L != null && i < L.Grievances.Count; i++)
				if (L.Grievances[i].GrievanceId == Id) return L.Grievances[i];
			return null;
		}

		private static KingdomPolityIncidentRecord FindPlan(KingdomPolityLedger L, string Id)
		{
			for (int i = 0; L != null && i < L.Incidents.Count; i++)
				if (L.Incidents[i].IncidentPlanId == Id) return L.Incidents[i];
			return null;
		}

		private static KingdomPolityIncidentRecord FindIncidentId(KingdomPolityLedger L,
			string Id)
		{
			for (int i = 0; L != null && i < L.Incidents.Count; i++)
				if (L.Incidents[i].IncidentId == Id) return L.Incidents[i];
			return null;
		}

		internal static bool ExactOpenGrievance(KingdomPolityGrievanceRecord A,
			KingdomPolityGrievanceRecord E)
		{
			return A.IssuerPolityId == E.IssuerPolityId && A.TargetPolityId == E.TargetPolityId &&
				A.Cause == E.Cause && A.SourceEventId == E.SourceEventId && A.Severity == E.Severity &&
				ExactStrings(A.EvidenceRefs, E.EvidenceRefs);
		}

		private static bool ExactPlan(KingdomPolityIncidentRecord A,
			KingdomPolityIncidentRecord E)
		{
			return A != null && E != null && A.IncidentId == E.IncidentId &&
				A.MaxSystemicWound == E.MaxSystemicWound && A.Purpose == E.Purpose &&
				A.EventStreamId == E.EventStreamId && A.RulesVersion == E.RulesVersion &&
				A.EventOrdinal == E.EventOrdinal && A.Conclusion == null &&
				ExactStrings(A.GrievanceRefs, E.GrievanceRefs) &&
				ExactStrings(A.ParticipantCohortRefs, E.ParticipantCohortRefs) &&
				ExactStrings(A.DisclosedStakeRefs, E.DisclosedStakeRefs) &&
				ExactStrings(A.EligibleSurfaceRefs, E.EligibleSurfaceRefs) &&
				ExactStrings(A.InterventionOptionKeys, E.InterventionOptionKeys);
		}

		private static bool CanonicalSemantic(IList<string> Values, int Minimum, int Maximum)
		{
			if (Values == null || Values.Count < Minimum || Values.Count > Maximum) return false;
			string previous = null;
			for (int i = 0; i < Values.Count; i++)
			{
				if (!KingdomPolityRules.SemanticId(Values[i]) ||
					(previous != null && string.CompareOrdinal(previous, Values[i]) >= 0)) return false;
				previous = Values[i];
			}
			return true;
		}

		private static bool CanonicalText(IList<string> Values, int Minimum, int Maximum)
		{
			if (Values == null || Values.Count < Minimum || Values.Count > Maximum) return false;
			string previous = null;
			for (int i = 0; i < Values.Count; i++)
			{
				if (!KingdomPolityRules.Text(Values[i], true) ||
					(previous != null && string.CompareOrdinal(previous, Values[i]) >= 0)) return false;
				previous = Values[i];
			}
			return true;
		}

		private static bool ExactStrings(IList<string> A, IList<string> B)
		{
			if (A == null || B == null || A.Count != B.Count) return false;
			for (int i = 0; i < A.Count; i++) if (A[i] != B[i]) return false;
			return true;
		}
	}
}
