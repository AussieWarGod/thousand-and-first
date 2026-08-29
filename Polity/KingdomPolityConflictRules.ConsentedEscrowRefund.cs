using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityConflictRules
	{
		/// <summary>Builds trusted proof after runtime re-proves the unchanged visible lease.</summary>
		internal static bool TryCreateEscrowRefundProof(KingdomPolityLedger Ledger,
			string ProjectionId, long Tick, out KingdomPolityEscrowRefundProof Proof,
			out string Failure)
		{
			Proof = null; Failure = null;
			KingdomPolityProjectionReceipt projection = KingdomPolityAuthority.Projection(
				Ledger, ProjectionId);
			if (projection == null || projection.Kind !=
				KingdomPolityProjectionKind.ConsentedEscrow || projection.ObjectIds.Count != 1 ||
				Tick < projection.PreparedTick)
				return Fail("escrow refund projection is absent", out Failure);
			Proof = new KingdomPolityEscrowRefundProof
			{
				ProjectionId = projection.ProjectionId, IncidentPlanId = projection.SourceRef,
				ZoneId = projection.ZoneId, CollateralObjectId = projection.ObjectIds[0],
				SnapshotDigest = projection.PriorDigest, RefundTick = Tick
			};
			Proof.ProofDigest = RefundDigest(Proof);
			return ValidRefundProof(Proof) || Fail("escrow refund proof is invalid", out Failure);
		}

		internal static bool TryReleaseConsentedEscrow(KingdomPolityLedger Ledger,
			long ExpectedRevision, KingdomPolityEscrowRefundProof Proof,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!ValidRefundProof(Proof))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "escrow refund proof is invalid", out Failure);
			KingdomPolityProjectionReceipt projection = KingdomPolityAuthority.Projection(
				Ledger, Proof.ProjectionId);
			KingdomPolityIncidentRecord plan = FindPlan(Ledger, Proof.IncidentPlanId);
			if (!RefundMatches(projection, Proof) || plan == null)
				return KingdomPolityAuthority.Refuse(Result,
					"escrow refund is absent or foreign", out Failure);
			KingdomPolityProjectionPhase target = plan.Conclusion == null
				? KingdomPolityProjectionPhase.Cancelled
				: KingdomPolityProjectionPhase.Cleaned;
			if (projection.Phase == target)
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (projection.Phase != KingdomPolityProjectionPhase.Prepared &&
				projection.Phase != KingdomPolityProjectionPhase.Committed)
				return KingdomPolityAuthority.Refuse(Result,
					"escrow projection is already terminal", out Failure);
			KingdomPolityRouteRecord stake = ExactEscrowStake(Ledger, plan);
			if (plan.Conclusion != null && (plan.Conclusion.ResolutionKind !=
				KingdomPolityResolutionKind.ConsentedEscrow || stake == null ||
				stake.Phase != KingdomPolityRoutePhase.Blocked))
				return KingdomPolityAuthority.Refuse(Result,
					"concluded escrow lost its exact reversible route stake", out Failure);
			if (plan.Conclusion == null && plan.Intervention?.Choice !=
				KingdomPolityInterventionChoice.ConsentAbstractResolution)
				return KingdomPolityAuthority.Refuse(Result,
					"open escrow lost its consent receipt", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityProjectionReceipt changed = KingdomPolityAuthority.Projection(
				candidate, Proof.ProjectionId);
			KingdomPolityIncidentRecord changedPlan = FindPlan(candidate,
				Proof.IncidentPlanId);
			changed.Phase = target;
			if (changedPlan.Conclusion == null) changedPlan.Intervention = null;
			else KingdomPolityAuthority.Route(candidate, stake.RouteId).Phase =
				KingdomPolityRoutePhase.AvailableToWitness;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		private static bool ValidRefundProof(KingdomPolityEscrowRefundProof P)
		{
			return P != null && KingdomPolityRules.TypedId(P.ProjectionId,
				"taf:projection:consented-escrow:v1:") && KingdomPolityRules.TypedId(
					P.IncidentPlanId, "taf:incident-plan:") &&
				KingdomPolityRules.Text(P.ZoneId, true) &&
				KingdomPolityRules.Text(P.CollateralObjectId, true) &&
				KingdomPolityRules.Digest(P.SnapshotDigest) && P.RefundTick >= 0L &&
				KingdomPolityRules.Digest(P.ProofDigest) && P.ProofDigest == RefundDigest(P);
		}

		private static bool RefundMatches(KingdomPolityProjectionReceipt P,
			KingdomPolityEscrowRefundProof Proof)
		{
			return P != null && P.Kind == KingdomPolityProjectionKind.ConsentedEscrow &&
				P.SourceRef == Proof.IncidentPlanId && P.ZoneId == Proof.ZoneId &&
				P.ObjectIds.Count == 1 && P.ObjectIds[0] == Proof.CollateralObjectId &&
				P.PriorDigest == Proof.SnapshotDigest && Proof.RefundTick >= P.PreparedTick;
		}

		private static string RefundDigest(KingdomPolityEscrowRefundProof P)
		{
			return KingdomPolityRules.ActivationDigest("polity-consented-escrow-refund-v1",
				P.ProjectionId ?? "", P.IncidentPlanId ?? "", P.ZoneId ?? "",
				P.CollateralObjectId ?? "", P.SnapshotDigest ?? "",
				P.RefundTick.ToString(CultureInfo.InvariantCulture));
		}
	}
}
