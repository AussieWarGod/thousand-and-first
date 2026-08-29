using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityConflictRules
	{
		public static bool TryPrepareConsentedEscrow(KingdomPolityLedger Ledger,
			long ExpectedRevision, KingdomPolityConsentedEscrowRequest Request,
			out string ProjectionId, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			ProjectionId = null; Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!ValidEscrowRequest(Request, out Failure) || !TryCreateIntervention(
					Request.IncidentPlanId,
					KingdomPolityInterventionChoice.ConsentAbstractResolution,
					Request.SurfaceRef, Request.ZoneId, Request.ConsentTick,
					Request.ConsentFactId, Request.ParticipantProjectionIds,
					out KingdomPolityInterventionRecord intervention, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityIncidentRecord plan = FindPlan(Ledger, Request.IncidentPlanId);
			KingdomPolityRouteRecord stake = KingdomPolityAuthority.Route(Ledger,
				Request.StakeRef);
			if (plan == null || plan.Conclusion != null || plan.Purpose !=
				KingdomPolityCohortPurpose.Warband || !KingdomPolityAuthority.Contains(
					plan.InterventionOptionKeys, OptionKey(intervention.Choice)) ||
				!KingdomPolityAuthority.Contains(plan.DisclosedStakeRefs, Request.StakeRef) ||
				stake == null || (stake.Phase != KingdomPolityRoutePhase.AvailableToWitness &&
				 stake.Phase != KingdomPolityRoutePhase.ConfrontationAvailable) ||
				!ExactLiveParticipants(Ledger, plan, intervention, out Failure))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "consented escrow lacks one exact open loaded clash",
					out Failure);
			KingdomPolityProjectionReceipt projection = EscrowProjection(Request);
			ProjectionId = projection.ProjectionId;
			KingdomPolityProjectionReceipt existing = KingdomPolityAuthority.Projection(
				Ledger, ProjectionId);
			if (plan.Intervention != null && plan.Intervention.ProofDigest !=
				intervention.ProofDigest)
				return KingdomPolityAuthority.Refuse(Result,
					"clash already carries another player stance", out Failure);
			if (existing != null)
			{
				if (!ExactEscrowProjection(existing, projection) || plan.Intervention == null)
					return KingdomPolityAuthority.Refuse(Result,
						"escrow retry changed its object, snapshot, or consent", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			for (int i = 0; i < Ledger.Projections.Count; i++)
				if (Ledger.Projections[i].Kind == KingdomPolityProjectionKind.ConsentedEscrow &&
					Ledger.Projections[i].SourceRef == Request.IncidentPlanId &&
					Ledger.Projections[i].Phase != KingdomPolityProjectionPhase.Cancelled &&
					Ledger.Projections[i].Phase != KingdomPolityProjectionPhase.Cleaned &&
					Ledger.Projections[i].Phase != KingdomPolityProjectionPhase.Archived)
					return KingdomPolityAuthority.Refuse(Result,
						"another exact escrow is already active for this clash", out Failure);
			if (Ledger.Projections.Count >= KingdomPolityRules.MaxProjections)
				return KingdomPolityAuthority.Refuse(Result,
					"projection capacity is exhausted", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			FindPlan(candidate, Request.IncidentPlanId).Intervention = intervention;
			InsertProjection(candidate, projection);
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		internal static string EscrowAppliedDigest(string ProjectionId,
			KingdomPolityConsentedEscrowRequest R)
		{
			return KingdomPolityRules.ActivationDigest("polity-consented-escrow-lease-v1",
				ProjectionId ?? "", R.IncidentPlanId ?? "", R.SurfaceRef ?? "",
				R.ZoneId ?? "", R.StakeRef ?? "", R.CollateralObjectId ?? "",
				R.SnapshotDigest ?? "", R.ConsentFactId ?? "");
		}

		private static KingdomPolityProjectionReceipt EscrowProjection(
			KingdomPolityConsentedEscrowRequest R)
		{
			string id = KingdomPolityRules.ActivationId(
				"taf:projection:consented-escrow:v1:", "polity-consented-escrow-v1",
				R.IncidentPlanId, R.CollateralObjectId, R.SnapshotDigest, R.ConsentFactId);
			return new KingdomPolityProjectionReceipt
			{
				ProjectionId = id, Kind = KingdomPolityProjectionKind.ConsentedEscrow,
				SourceRef = R.IncidentPlanId, Phase = KingdomPolityProjectionPhase.Prepared,
				ZoneId = R.ZoneId, ObjectIds = new List<string> { R.CollateralObjectId },
				PriorDigest = R.SnapshotDigest, AppliedDigest = EscrowAppliedDigest(id, R),
				PreparedTick = R.ConsentTick, CommittedTick = 0L
			};
		}

		private static bool ValidEscrowRequest(KingdomPolityConsentedEscrowRequest R,
			out string Failure)
		{
			Failure = null;
			if (R == null || !KingdomPolityRules.TypedId(R.IncidentPlanId,
				"taf:incident-plan:") || !KingdomPolityRules.SemanticId(R.SurfaceRef) ||
				!KingdomPolityRules.Text(R.ZoneId, true) || R.ConsentTick < 0L ||
				!KingdomPolityRules.TypedId(R.ConsentFactId, "taf:fact:witnessed:") ||
				!Canonical(R.ParticipantProjectionIds, 1, KingdomPolityRules.MaxRefs) ||
				!KingdomPolityRules.TypedId(R.StakeRef, "taf:route:") ||
				!KingdomPolityRules.Text(R.CollateralObjectId, true) ||
				!KingdomPolityRules.Digest(R.SnapshotDigest))
				return Fail("consented escrow request is invalid or unbounded", out Failure);
			return true;
		}

		private static bool ExactEscrowProjection(KingdomPolityProjectionReceipt A,
			KingdomPolityProjectionReceipt E)
		{
			return A.Kind == E.Kind && A.SourceRef == E.SourceRef && A.ZoneId == E.ZoneId &&
				A.ObjectIds.Count == 1 && A.ObjectIds[0] == E.ObjectIds[0] &&
				A.PriorDigest == E.PriorDigest && A.AppliedDigest == E.AppliedDigest &&
				A.PreparedTick == E.PreparedTick;
		}

		private static void InsertProjection(KingdomPolityLedger L,
			KingdomPolityProjectionReceipt P)
		{
			int at = 0;
			while (at < L.Projections.Count && string.CompareOrdinal(
				L.Projections[at].ProjectionId, P.ProjectionId) < 0) at++;
			L.Projections.Insert(at, P);
		}
	}
}
