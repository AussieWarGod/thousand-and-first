using System;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityRemovalRules
	{
		/// <summary>One CAS that cancels only bodyless work after every claimed ground locator
		/// was attended. It records no diplomatic outcome and moves no value.</summary>
		internal static bool TrySettleBodylessRetirement(KingdomPolityLedger Ledger,
			KingdomPolityDispatchState Dispatch, long ExpectedRevision, string RetirementReceiptId,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				string.IsNullOrEmpty(RetirementReceiptId))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "polity retirement receipt is absent", out Failure);
			if (!KingdomPolityDispatchRules.ExactRetirementReceipt(Dispatch, Ledger.RealmId,
				RetirementReceiptId, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			bool changed = false;
			for (int i = 0; i < candidate.Routes.Count; i++)
			{
				KingdomPolityRouteRecord route = candidate.Routes[i];
				if (route.Phase == KingdomPolityRoutePhase.Preparing)
				{
					route.Phase = KingdomPolityRoutePhase.Cancelled; changed = true;
				}
				else if (route.Phase == KingdomPolityRoutePhase.AvailableToWitness &&
					AbandonedRoute(candidate, route))
				{
					route.Phase = KingdomPolityRoutePhase.Cancelled; changed = true;
				}
				else if (route.Phase != KingdomPolityRoutePhase.Returned &&
					route.Phase != KingdomPolityRoutePhase.Cancelled)
					return KingdomPolityAuthority.Refuse(Result,
						"departed polity route still owns custody: " + route.RouteId, out Failure);
			}
			for (int i = 0; i < candidate.Cohorts.Count; i++)
			{
				KingdomPolityCohortPlan cohort = candidate.Cohorts[i];
				if (cohort.Phase == KingdomPolityCohortPhase.Planned &&
					string.IsNullOrEmpty(cohort.ManifestationReceiptId))
				{
					cohort.Phase = KingdomPolityCohortPhase.Cancelled;
					cohort.RewardEventId = KingdomPolityRules.ActivationId(
						"taf:event:polity-retirement-cancel:v1:",
						"polity-retirement-cohort-cancel-v1", RetirementReceiptId,
						cohort.CohortId); changed = true;
				}
				else if (cohort.Phase != KingdomPolityCohortPhase.Cancelled &&
					cohort.Phase != KingdomPolityCohortPhase.Cleaned &&
					cohort.Phase != KingdomPolityCohortPhase.Abandoned &&
					cohort.Phase != KingdomPolityCohortPhase.Archived)
					return KingdomPolityAuthority.Refuse(Result,
						"projected polity cohort is not reconciled: " + cohort.CohortId,
						out Failure);
			}
			for (int i = candidate.Incidents.Count - 1; i >= 0; i--)
			{
				KingdomPolityIncidentRecord incident = candidate.Incidents[i];
				if (incident.Conclusion != null) continue;
				if (!SafeIncident(candidate, incident, out string blocker))
					return KingdomPolityAuthority.Refuse(Result, blocker, out Failure);
				for (int j = 0; j < candidate.Grievances.Count; j++)
				{
					KingdomPolityGrievanceRecord grievance = candidate.Grievances[j];
					if (grievance.ConsumedByIncidentId != incident.IncidentId) continue;
					if (grievance.Phase != KingdomPolityGrievancePhase.Consumed)
						return KingdomPolityAuthority.Refuse(Result,
							"retirement incident owns a non-reversible grievance: " +
							grievance.GrievanceId, out Failure);
					grievance.Phase = KingdomPolityGrievancePhase.Open;
					grievance.ConsumedByIncidentId = null; grievance.ResolutionRef = null;
				}
				for (int j = candidate.Projections.Count - 1; j >= 0; j--)
					if (candidate.Projections[j].Kind == KingdomPolityProjectionKind.IncidentView &&
						candidate.Projections[j].SourceRef == incident.IncidentPlanId)
					{
						if (candidate.Projections[j].ObjectIds.Count != 0)
							return KingdomPolityAuthority.Refuse(Result,
								"retirement incident view unexpectedly owns objects", out Failure);
						candidate.Projections.RemoveAt(j);
					}
				EndIncidentFronts(candidate, incident); candidate.Incidents.RemoveAt(i); changed = true;
			}
			for (int i = 0; i < candidate.Projections.Count; i++)
			{
				KingdomPolityProjectionReceipt projection = candidate.Projections[i];
				if (projection.Kind != KingdomPolityProjectionKind.RoutePrompt) continue;
				KingdomPolityRouteRecord route = KingdomPolityAuthority.Route(candidate,
					projection.SourceRef);
				if (route != null && route.Phase == KingdomPolityRoutePhase.Cancelled &&
					projection.Phase != KingdomPolityProjectionPhase.Cancelled)
				{
					if (projection.ObjectIds.Count != 0)
						return KingdomPolityAuthority.Refuse(Result,
							"retirement route prompt unexpectedly owns objects", out Failure);
					projection.Phase = KingdomPolityProjectionPhase.Cancelled; changed = true;
				}
			}
			if (!changed)
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		private static void EndIncidentFronts(KingdomPolityLedger Ledger,
			KingdomPolityIncidentRecord Incident)
		{
			for (int i = 0; i < Ledger.Fronts.Count; i++)
			{
				KingdomPolityFrontRecord front = Ledger.Fronts[i];
				bool relevant = KingdomPolityAuthority.Contains(Incident.DisclosedStakeRefs,
					front.TargetRef) || KingdomPolityAuthority.Contains(
						Incident.ParticipantCohortRefs, front.TargetRef);
				for (int j = 0; !relevant && j < front.GrievanceRefs.Count; j++)
					relevant = KingdomPolityAuthority.Contains(Incident.GrievanceRefs,
						front.GrievanceRefs[j]);
				if (!relevant) continue;
				front.Phase = KingdomPolityFrontPhase.Ended; front.PressureBand = 0;
			}
		}
	}
}
