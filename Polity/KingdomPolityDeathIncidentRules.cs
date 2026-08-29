using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	internal static class KingdomPolityDeathIncidentRules
	{
		internal static bool TryFreeze(KingdomPolityLedger Ledger, KingdomPolityCohortPlan Cohort,
			int Ordinal, bool PlayerVisible, out string PlanId, out string IncidentId, out string Digest,
			out string Failure)
		{
			PlanId = IncidentId = Digest = ""; Failure = null;
			if (!PlayerVisible || Ordinal != 0 || (Cohort.Purpose != KingdomPolityCohortPurpose.Envoy &&
				Cohort.Purpose != KingdomPolityCohortPurpose.Warband)) return true;
			KingdomPolityIncidentRecord match = null;
			for (int i = 0; i < Ledger.Incidents.Count; i++)
			{
				KingdomPolityIncidentRecord candidate = Ledger.Incidents[i];
				if (candidate.Purpose != Cohort.Purpose || Count(candidate.ParticipantCohortRefs,
					Cohort.CohortId) == 0) continue;
				if (match != null) return Fail("death cohort is reused by multiple open incident authorities",
					out Failure);
				match = candidate;
			}
			if (match == null || Count(match.ParticipantCohortRefs, Cohort.CohortId) != 1 ||
				(Cohort.Purpose == KingdomPolityCohortPurpose.Envoy &&
				 match.ParticipantCohortRefs.Count != 1)) return Fail(
				"death cohort lacks one exact incident authority", out Failure);
			PlanId = match.IncidentPlanId; IncidentId = match.IncidentId; Digest = BindingDigest(match);
			return true;
		}

		internal static string BindingDigest(KingdomPolityIncidentRecord Incident)
		{
			List<string> values = new List<string> { Incident.IncidentPlanId, Incident.IncidentId,
				((byte)Incident.Purpose).ToString(CultureInfo.InvariantCulture), Incident.EventStreamId,
				Incident.RulesVersion.ToString(CultureInfo.InvariantCulture),
				Incident.EventOrdinal.ToString(CultureInfo.InvariantCulture),
				Incident.MaxSystemicWound.ToString(CultureInfo.InvariantCulture) };
			Append(values, Incident.GrievanceRefs); Append(values, Incident.ParticipantCohortRefs);
			Append(values, Incident.DisclosedStakeRefs); Append(values, Incident.EligibleSurfaceRefs);
			Append(values, Incident.InterventionOptionKeys);
			return KingdomPolityRules.ActivationDigest("polity-death-incident-binding-v1", values);
		}

		internal static int Count(List<string> Values, string Value)
		{
			int count = 0; for (int i = 0; i < Values.Count; i++) if (Values[i] == Value) count++;
			return count;
		}

		private static void Append(List<string> Values, List<string> Source)
		{
			Values.Add(Source.Count.ToString(CultureInfo.InvariantCulture));
			for (int i = 0; i < Source.Count; i++) Values.Add(Source[i]);
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
