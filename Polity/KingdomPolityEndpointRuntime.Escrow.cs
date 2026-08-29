using System;
using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityEndpointRuntime
	{
		internal static bool TryPrepareCurrentEndpointEscrow(KingdomSystem System,
			string IncidentPlanId, string StakeRef, string ObjectId, string SnapshotDigest,
			long Tick, string ConsentFactId, out string ProjectionId, out string Failure)
		{
			ProjectionId = null; Failure = null;
			KingdomPolityLedger ledger = System?.PolityLedger;
			KingdomPolityIncidentRecord plan = FindIncident(ledger, IncidentPlanId);
			if (plan == null || plan.Purpose != KingdomPolityCohortPurpose.Warband ||
				plan.ParticipantCohortRefs.Count < 1 || plan.Conclusion != null)
			{
				Failure = "loaded escrow clash is absent or concluded"; return false;
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
					!ExactReceipt(cohort, receipt, zone, out Failure) || receipt.Phase !=
						KingdomPolityProjectionPhase.Committed ||
					!TryObserve(zone, admitted.RealmId, cohort, receipt,
						out GameObject[] observed, out Failure))
					return false;
				for (int j = 0; j < observed.Length; j++)
					if (!GameObject.Validate(observed[j]))
					{
						Failure = "escrow clash participant is physically incomplete"; return false;
					}
				projections.Add(receipt.ProjectionId);
			}
			projections.Sort(StringComparer.Ordinal);
			KingdomPolityConsentedEscrowRequest request = new KingdomPolityConsentedEscrowRequest
			{
				IncidentPlanId = IncidentPlanId, SurfaceRef = first.SurfaceRef,
				ZoneId = zone.ZoneID, ConsentTick = Tick, ConsentFactId = ConsentFactId,
				ParticipantProjectionIds = projections, StakeRef = StakeRef,
				CollateralObjectId = ObjectId, SnapshotDigest = SnapshotDigest
			};
			return KingdomPolityConflictRules.TryPrepareConsentedEscrow(admitted,
				admitted.Revision, request, out ProjectionId,
				out KingdomPolityPublicationResult _, out Failure);
		}
	}
}
