namespace ThousandAndFirst
{
	public static partial class KingdomPolityDiplomacyRules
	{
		internal static bool TryRecoverEnvoyDeaths(KingdomPolityLedger Ledger,
			long ExpectedRevision, KingdomPolityConsignmentAbsenceProof ConsignmentAbsence,
			out int PendingCount, out int PublishedCount,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			PendingCount = 0; PublishedCount = 0;
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			bool changed = false;
			for (int i = 0; i < candidate.Incidents.Count; i++)
			{
				KingdomPolityIncidentRecord plan = candidate.Incidents[i];
				bool harm = OwnsHarmConclusion(plan);
				bool neutral = OwnsNeutralDeathConclusion(plan);
				if (!harm && !neutral) continue;
				if (plan.ParticipantCohortRefs.Count != 1)
					return KingdomPolityAuthority.Refuse(Result,
						"envoy death owner lost its exact cohort", out Failure);
				KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(candidate,
					plan.ParticipantCohortRefs[0]);
				if (cohort == null || (cohort.Phase != KingdomPolityCohortPhase.Materialized &&
					cohort.Phase != KingdomPolityCohortPhase.Concluded))
					return KingdomPolityAuthority.Refuse(Result,
						"envoy death owner has an invalid pending cohort", out Failure);
				if (cohort.Phase == KingdomPolityCohortPhase.Materialized)
				{
					KingdomPolityConsignmentAbsenceProof exactAbsence =
						ConsignmentAbsence?.TermsPlanId == plan.IncidentPlanId &&
						ConsignmentAbsence.RecipientCohortId == cohort.CohortId
							? ConsignmentAbsence : null;
					if (!TryCloseDeathCorrespondence(candidate, plan.IncidentPlanId,
						cohort.CohortId, exactAbsence, plan.Conclusion.CommitTick,
						out bool held, out Failure))
						return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
					if (held) { PendingCount++; continue; }
					cohort.Phase = KingdomPolityCohortPhase.Concluded; changed = true;
				}
				if (!harm) continue;
				KingdomPolityGrievanceRecord original = plan.GrievanceRefs.Count == 1 ?
					FindGrievance(candidate, plan.GrievanceRefs[0]) : null;
				if (original == null) return KingdomPolityAuthority.Refuse(Result,
					"witnessed envoy harm lost its original grievance", out Failure);
				KingdomPolityGrievanceIngressRequest ingress = HarmIngress(plan.IncidentPlanId,
					cohort.RewardEventId, cohort.PolityId, original.TargetPolityId);
				if (!TryDeriveExactGrievance(candidate, ingress,
					out KingdomPolityGrievanceRecord expected, out Failure))
					return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
				KingdomPolityGrievanceRecord existing = FindGrievance(candidate,
					expected.GrievanceId);
				if (existing != null)
				{
					if (!ExactOpenGrievance(existing, expected))
						return KingdomPolityAuthority.Refuse(Result,
							"envoy harm recovery changed its exact grievance", out Failure);
					continue;
				}
				if (HasGrievanceSourceCollision(candidate, expected))
					return KingdomPolityAuthority.Refuse(Result,
						"envoy harm source already emitted another grievance", out Failure);
				if (candidate.Grievances.Count >= KingdomPolityRules.MaxGrievances)
				{
					PendingCount++; continue;
				}
				InsertGrievance(candidate, expected); changed = true; PublishedCount++;
			}
			if (!changed)
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}
	}
}
