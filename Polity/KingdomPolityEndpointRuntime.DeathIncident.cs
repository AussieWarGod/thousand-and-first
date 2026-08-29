using System;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityEndpointRuntime
	{
		private static bool TryFreezeDeathIncident(KingdomPolityLedger Ledger,
			KingdomPolityCohortPlan Cohort, int Ordinal, bool PlayerVisible, out string PlanId,
			out string IncidentId, out string Digest, out string Failure)
		{
			return KingdomPolityDeathIncidentRules.TryFreeze(Ledger, Cohort, Ordinal, PlayerVisible,
				out PlanId, out IncidentId, out Digest, out Failure);
		}

		internal static bool TryResolveDeathIncident(KingdomPolityLedger Ledger,
			KingdomPolityDeathIntentRecord Intent, out KingdomPolityIncidentRecord Incident,
			out string Failure)
		{
			Incident = null; Failure = null;
			if (Ledger == null || Intent == null) return FailPhysical(
				"death intent lacks durable incident authority", out Failure);
			for (int i = 0; i < Ledger.Incidents.Count; i++)
			{
				KingdomPolityIncidentRecord candidate = Ledger.Incidents[i];
				if (candidate.IncidentPlanId != Intent.IncidentPlanId) continue;
				if (Incident != null) return FailPhysical(
					"death incident plan id is ambiguous", out Failure);
				Incident = candidate;
			}
			if (Incident == null || Incident.IncidentId != Intent.IncidentId ||
				Incident.Purpose != Intent.Purpose || KingdomPolityDeathIncidentRules.Count(
				Incident.ParticipantCohortRefs,
				Intent.CohortId) != 1 || KingdomPolityDeathIncidentRules.BindingDigest(Incident) !=
				Intent.IncidentDigest)
				return FailPhysical("death intent lost its exact frozen incident", out Failure);
			return true;
		}
	}
}
