using System;
using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityEndpointRuntime
	{
		public static bool TryConcludeCurrentEndpointClash(KingdomSystem System,
			string IncidentPlanId, long Tick, IList<string> ObservedFactIds,
			IList<KingdomPolitySystemicDelta> SystemicDeltas,
			IList<KingdomPolityRelationDelta> RelationDeltas, IList<string> ReceiptRefs,
			out string Failure)
		{
			Failure = null; KingdomPolityLedger ledger = System == null ? null : System.PolityLedger;
			KingdomPolityIncidentRecord plan = null;
			for (int i = 0; ledger != null && i < ledger.Incidents.Count; i++)
				if (ledger.Incidents[i].IncidentPlanId == IncidentPlanId) plan = ledger.Incidents[i];
			if (plan == null || plan.Purpose != KingdomPolityCohortPurpose.Warband ||
				plan.ParticipantCohortRefs.Count < 1)
			{
				Failure = "loaded clash plan is missing or has no finite participant"; return false;
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
					!ExactReceipt(cohort, receipt, zone, out Failure) ||
					receipt.Phase != KingdomPolityProjectionPhase.Committed)
				{
					Failure = Failure ?? "clash participant is not committed in this loaded zone";
					return false;
				}
				if (!TryObserve(zone, admitted.RealmId, cohort, receipt,
					out GameObject[] observed, out Failure))
					return false;
				for (int j = 0; j < observed.Length; j++)
					if (!GameObject.Validate(observed[j]))
					{
						string objectId = KingdomPolityCohortRules.PreparedObjectId(cohort, j);
						if (!HasRemovalWitness(zone,
							KingdomPolityPhysicalCustodyRules.DeathRemovalKind,
							admitted.RealmId, cohort.CohortId, receipt.ProjectionId,
							objectId, j))
						{
							Failure = "clash participant projection is physically incomplete";
							return false;
						}
					}
				projections.Add(receipt.ProjectionId);
			}
			projections.Sort(StringComparer.Ordinal);
			string firstFact = ObservedFactIds == null || ObservedFactIds.Count == 0 ? "" :
				ObservedFactIds[0];
			string proofId = KingdomPolityRules.ActivationId("taf:clash-proof:v1:",
				"polity-loaded-clash-proof-v1", IncidentPlanId, zone.ZoneID,
				Tick.ToString(global::System.Globalization.CultureInfo.InvariantCulture), firstFact);
			if (!KingdomPolityClashRules.TryCreateLiveProof(proofId, IncidentPlanId,
				first.SurfaceRef, zone.ZoneID, Tick, ObservedFactIds, projections, SystemicDeltas,
				RelationDeltas, ReceiptRefs, out KingdomPolityWitnessedClashProof proof,
				out Failure)) return false;
			return KingdomPolityClashRules.TryConcludeWitnessed(admitted, admitted.Revision,
				proof, out KingdomPolityPublicationResult _, out Failure);
		}
	}
}
