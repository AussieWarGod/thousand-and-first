using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Caused grievance and frozen terms authority. Standing is never a cause.</summary>
	public static partial class KingdomPolityDiplomacyRules
	{
		public const int RulesVersion = 1;

		/// <summary>Legacy low-level constructor retained for same-assembly fixtures only.
		/// Runtime cause ingestion must use TryIngestExactGrievance.</summary>
		internal static bool TryOpenGrievance(KingdomPolityLedger Ledger, long ExpectedRevision,
			KingdomPolityGrievanceRequest Request, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!ValidGrievanceRequest(Request, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			if (KingdomPolityAuthority.Polity(Ledger, Request.IssuerPolityId) == null ||
				KingdomPolityAuthority.Polity(Ledger, Request.TargetPolityId) == null)
				return KingdomPolityAuthority.Refuse(Result,
					"grievance endpoint polity is missing", out Failure);
			KingdomPolityGrievanceRecord expected = Grievance(Request);
			KingdomPolityGrievanceRecord existing = FindGrievance(Ledger, Request.GrievanceId);
			if (existing != null)
			{
				if (!ExactOpenGrievance(existing, expected)) return KingdomPolityAuthority.Refuse(Result,
					"grievance id already carries another cause", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			for (int i = 0; i < Ledger.Grievances.Count; i++)
				if (Ledger.Grievances[i].SourceEventId == Request.SourceEventId)
					return KingdomPolityAuthority.Refuse(Result,
						"caused event already emitted one grievance", out Failure);
			if (Ledger.Grievances.Count >= KingdomPolityRules.MaxGrievances)
				return KingdomPolityAuthority.Refuse(Result,
					"grievance capacity is exhausted", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			candidate.Grievances.Add(expected);
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		public static bool TryPlanTerms(KingdomPolityLedger Ledger, long ExpectedRevision,
			KingdomPolityTermsPlanRequest Request, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!ValidTermsRequest(Request, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityGrievanceRecord grievance = FindGrievance(Ledger, Request.GrievanceId);
			KingdomPolityCohortPlan envoy = KingdomPolityAuthority.Cohort(Ledger,
				Request.EnvoyCohortId);
			if (grievance == null || envoy == null || envoy.Purpose != KingdomPolityCohortPurpose.Envoy ||
				!KingdomPolityAuthority.Contains(Request.EligibleSurfaceRefs, envoy.SurfaceRef) ||
				!ValidClashCohorts(Ledger, Request))
				return KingdomPolityAuthority.Refuse(Result,
					"terms lack their open cause, envoy, surface, or frozen clash", out Failure);
			KingdomPolityIncidentRecord terms = TermsIncident(Request);
			KingdomPolityIncidentRecord clash = HasClash(Request) ? ClashIncident(Request) : null;
			KingdomPolityIncidentRecord existingTerms = FindPlan(Ledger, Request.TermsPlanId);
			KingdomPolityIncidentRecord existingClash = clash == null ? null :
				FindPlan(Ledger, Request.ClashPlanId);
			if (existingTerms != null || existingClash != null)
			{
				if (!ExactPlan(existingTerms, terms) || (clash != null &&
					!ExactPlan(existingClash, clash)) || grievance.ConsumedByIncidentId !=
					Request.TermsIncidentId)
					return KingdomPolityAuthority.Refuse(Result,
						"terms plan ids already carry foreign frozen authority", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (grievance.Phase != KingdomPolityGrievancePhase.Open ||
				Ledger.Incidents.Count + (clash == null ? 1 : 2) > KingdomPolityRules.MaxIncidents ||
				FindIncidentId(Ledger, Request.TermsIncidentId) != null ||
				(clash != null && FindIncidentId(Ledger, Request.ClashIncidentId) != null))
				return KingdomPolityAuthority.Refuse(Result,
					"grievance is consumed or incident capacity/identity is unavailable", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityGrievanceRecord changed = FindGrievance(candidate, Request.GrievanceId);
			changed.Phase = KingdomPolityGrievancePhase.Consumed;
			changed.ConsumedByIncidentId = Request.TermsIncidentId;
			candidate.Incidents.Add(terms); if (clash != null) candidate.Incidents.Add(clash);
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		private static bool ValidGrievanceRequest(KingdomPolityGrievanceRequest R,
			out string Failure)
		{
			Failure = null;
			if (R == null || !KingdomPolityRules.TypedId(R.GrievanceId, "taf:grievance:") ||
				!KingdomPolityRules.SemanticId(R.IssuerPolityId) ||
				!KingdomPolityRules.SemanticId(R.TargetPolityId) ||
				R.IssuerPolityId == R.TargetPolityId || R.Cause == KingdomPolityGrievanceCause.None ||
				(byte)R.Cause > 8 || !CausedEvent(R.SourceEventId) || R.Severity < 1 ||
				R.Severity > 5 || !CanonicalSemantic(R.EvidenceRefs, 1,
					KingdomPolityRules.MaxRefs))
			{
				Failure = "grievance requires one bounded caused event and exact evidence"; return false;
			}
			return true;
		}

		private static bool ValidTermsRequest(KingdomPolityTermsPlanRequest R, out string Failure)
		{
			Failure = null; bool clash = HasClash(R);
			if (R == null || !KingdomPolityRules.TypedId(R.GrievanceId, "taf:grievance:") ||
				!KingdomPolityRules.TypedId(R.TermsPlanId, "taf:incident-plan:") ||
				!KingdomPolityRules.TypedId(R.TermsIncidentId, "taf:incident:") ||
				!KingdomPolityRules.TypedId(R.EnvoyCohortId, "taf:cohort:") ||
				!CanonicalSemantic(R.DisclosedStakeRefs, 1, 2) ||
				!CanonicalSemantic(R.EligibleSurfaceRefs, 1, KingdomPolityRules.MaxRefs) ||
				!CanonicalText(R.TermKeys, 1, 2) ||
				!KingdomPolityRules.TypedId(R.EventStreamId, "taf:stream:") ||
				R.RulesVersion != RulesVersion || R.MaxSystemicWound < 0 ||
				R.MaxSystemicWound > KingdomPolityRules.MaxValueBudget ||
				(clash && (!KingdomPolityRules.TypedId(R.ClashPlanId, "taf:incident-plan:") ||
				 !KingdomPolityRules.TypedId(R.ClashIncidentId, "taf:incident:") ||
				 R.ClashPlanId == R.TermsPlanId || R.ClashIncidentId == R.TermsIncidentId ||
				 !CanonicalSemantic(R.ClashCohortRefs, 1, 3))) ||
				(!clash && (!string.IsNullOrEmpty(R.ClashPlanId) ||
				 !string.IsNullOrEmpty(R.ClashIncidentId) || R.ClashCohortRefs == null ||
				 R.ClashCohortRefs.Count != 0)))
			{
				Failure = "terms and optional clash plan are invalid or not bounded"; return false;
			}
			return true;
		}

		private static bool CausedEvent(string Id)
		{
			return KingdomPolityRules.SemanticId(Id) &&
				(Id.StartsWith("taf:event:", StringComparison.Ordinal) ||
				 Id.StartsWith("taf:fact:witnessed:", StringComparison.Ordinal));
		}

		private static bool HasClash(KingdomPolityTermsPlanRequest R)
		{
			return R != null && (!string.IsNullOrEmpty(R.ClashPlanId) ||
				!string.IsNullOrEmpty(R.ClashIncidentId));
		}
	}
}
