using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityEndpointRuntime
	{
		/// <summary>Records one explicit stance only after every finite participant is observed.</summary>
		public static bool TryRecordCurrentEndpointIntervention(KingdomSystem System,
			string IncidentPlanId, KingdomPolityInterventionChoice Choice, long Tick,
			string ObservedFactId, out string Failure)
		{
			Failure = null;
			KingdomPolityLedger ledger = System?.PolityLedger;
			KingdomPolityIncidentRecord plan = FindIncident(ledger, IncidentPlanId);
			if (plan == null || plan.Purpose != KingdomPolityCohortPurpose.Warband ||
				plan.ParticipantCohortRefs.Count < 1 || plan.Conclusion != null)
			{
				Failure = "loaded clash is absent, concluded, or has no finite participant";
				return false;
			}
			Zone zone; KingdomPolityLedger admitted; KingdomPolityCohortPlan first;
			if (!TryAdmit(System, plan.ParticipantCohortRefs[0], out zone, out admitted,
				out first, out Failure)) return false;
			List<string> projections = new List<string>();
			for (int i = 0; i < plan.ParticipantCohortRefs.Count; i++)
			{
				KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(admitted,
					plan.ParticipantCohortRefs[i]);
				KingdomPolityProjectionReceipt receipt = cohort == null ? null :
					KingdomPolityAuthority.Projection(admitted, cohort.ManifestationReceiptId);
				if (cohort == null || cohort.SurfaceRef != first.SurfaceRef ||
					cohort.Phase != KingdomPolityCohortPhase.Materialized ||
					!ExactReceipt(cohort, receipt, zone, out Failure) ||
					receipt.Phase != KingdomPolityProjectionPhase.Committed)
				{
					Failure = Failure ?? "clash participant is not live in this loaded zone";
					return false;
				}
				if (!TryObserve(zone, admitted.RealmId, cohort, receipt,
					out GameObject[] observed, out Failure))
					return false;
				for (int j = 0; j < observed.Length; j++)
					if (!GameObject.Validate(observed[j]))
					{
						Failure = "clash participant projection is physically incomplete";
						return false;
					}
				projections.Add(receipt.ProjectionId);
			}
			projections.Sort(StringComparer.Ordinal);
			if (Choice == KingdomPolityInterventionChoice.SupportSettlement)
			{
				if (!TryTrespassEndpoints(admitted, plan, out string issuer,
					out string target, out Failure)) return false;
				return KingdomPolityConflictRules.TryRecordWitnessedTrespass(admitted,
					admitted.Revision, IncidentPlanId, first.SurfaceRef, zone.ZoneID, Tick,
					ObservedFactId, projections, issuer, target, out string _,
					out KingdomPolityPublicationResult _, out Failure);
			}
			return KingdomPolityConflictRules.TryRecordWitnessedIntervention(admitted,
				admitted.Revision, IncidentPlanId, Choice, first.SurfaceRef, zone.ZoneID, Tick,
				ObservedFactId, projections, out KingdomPolityPublicationResult _, out Failure);
		}

		private static bool TryTrespassEndpoints(KingdomPolityLedger Ledger,
			KingdomPolityIncidentRecord Plan, out string Issuer, out string Target,
			out string Failure)
		{
			Issuer = null; Target = null; Failure = null;
			for (int i = 0; Ledger != null && i < Ledger.Polities.Count; i++)
				if (Ledger.Polities[i].Source == KingdomPolitySource.CurrentRealm)
				{
					if (Issuer != null)
					{
						Failure = "current polity identity is ambiguous"; return false;
					}
					Issuer = Ledger.Polities[i].PolityId;
				}
			for (int i = 0; Plan != null && i < Plan.ParticipantCohortRefs.Count; i++)
			{
				KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(Ledger,
					Plan.ParticipantCohortRefs[i]);
				if (cohort == null || cohort.PolityId == Issuer) continue;
				if (Target != null && Target != cohort.PolityId)
				{
					Failure = "visitor polity identity is ambiguous"; return false;
				}
				Target = cohort.PolityId;
			}
			if (Issuer != null && Target != null) return true;
			Failure = "exact trespass endpoints are absent"; return false;
		}

		private static KingdomPolityIncidentRecord FindIncident(KingdomPolityLedger Ledger,
			string IncidentPlanId)
		{
			for (int i = 0; Ledger != null && i < Ledger.Incidents.Count; i++)
				if (Ledger.Incidents[i].IncidentPlanId == IncidentPlanId)
					return Ledger.Incidents[i];
			return null;
		}
	}
}
