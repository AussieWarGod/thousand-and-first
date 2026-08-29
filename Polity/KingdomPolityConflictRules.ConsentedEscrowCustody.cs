using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityConflictRules
	{
		/// <summary>Builds trusted proof shape after runtime observes exact loaded lease.</summary>
		internal static bool TryCreateEscrowCustodyProof(KingdomPolityLedger Ledger,
			string ProjectionId, long Tick, out KingdomPolityEscrowCustodyProof Proof,
			out string Failure)
		{
			Proof = null; Failure = null;
			KingdomPolityProjectionReceipt projection = KingdomPolityAuthority.Projection(
				Ledger, ProjectionId);
			if (projection == null || projection.Kind !=
				KingdomPolityProjectionKind.ConsentedEscrow || projection.ObjectIds.Count != 1 ||
				(projection.Phase != KingdomPolityProjectionPhase.Prepared &&
				 projection.Phase != KingdomPolityProjectionPhase.Committed) ||
				Tick < projection.PreparedTick)
				return Fail("escrow custody projection is not active", out Failure);
			long commitTick = projection.Phase == KingdomPolityProjectionPhase.Committed
				? projection.CommittedTick : Tick;
			Proof = new KingdomPolityEscrowCustodyProof
			{
				ProjectionId = projection.ProjectionId, IncidentPlanId = projection.SourceRef,
				ZoneId = projection.ZoneId, CollateralObjectId = projection.ObjectIds[0],
				SnapshotDigest = projection.PriorDigest,
				AppliedDigest = projection.AppliedDigest, CommitTick = commitTick
			};
			Proof.ProofDigest = CustodyDigest(Proof);
			return ValidCustodyProof(Proof) || Fail("escrow custody proof is invalid", out Failure);
		}

		internal static bool TryCommitConsentedEscrowCustody(KingdomPolityLedger Ledger,
			long ExpectedRevision, KingdomPolityEscrowCustodyProof Proof,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!ValidCustodyProof(Proof))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "escrow custody proof is invalid", out Failure);
			KingdomPolityProjectionReceipt projection = KingdomPolityAuthority.Projection(
				Ledger, Proof.ProjectionId);
			KingdomPolityIncidentRecord plan = FindPlan(Ledger, Proof.IncidentPlanId);
			if (!ProofMatches(projection, Proof) || plan?.Intervention?.Choice !=
				KingdomPolityInterventionChoice.ConsentAbstractResolution ||
				projection.Phase == KingdomPolityProjectionPhase.Cancelled ||
				projection.Phase == KingdomPolityProjectionPhase.Cleaned ||
				projection.Phase == KingdomPolityProjectionPhase.Archived)
				return KingdomPolityAuthority.Refuse(Result,
					"escrow custody is absent, foreign, or terminal", out Failure);
			if (projection.Phase == KingdomPolityProjectionPhase.Committed)
			{
				if (projection.CommittedTick != Proof.CommitTick)
					return KingdomPolityAuthority.Refuse(Result,
						"escrow custody retry changed commit tick", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityProjectionReceipt changed = KingdomPolityAuthority.Projection(
				candidate, Proof.ProjectionId);
			changed.Phase = KingdomPolityProjectionPhase.Committed;
			changed.CommittedTick = Proof.CommitTick;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		private static bool ValidCustodyProof(KingdomPolityEscrowCustodyProof P)
		{
			return P != null && KingdomPolityRules.TypedId(P.ProjectionId,
				"taf:projection:consented-escrow:v1:") && KingdomPolityRules.TypedId(
					P.IncidentPlanId, "taf:incident-plan:") &&
				KingdomPolityRules.Text(P.ZoneId, true) &&
				KingdomPolityRules.Text(P.CollateralObjectId, true) &&
				KingdomPolityRules.Digest(P.SnapshotDigest) &&
				KingdomPolityRules.Digest(P.AppliedDigest) && P.CommitTick >= 0L &&
				KingdomPolityRules.Digest(P.ProofDigest) && P.ProofDigest == CustodyDigest(P);
		}

		private static bool ProofMatches(KingdomPolityProjectionReceipt P,
			KingdomPolityEscrowCustodyProof Proof)
		{
			return P != null && P.Kind == KingdomPolityProjectionKind.ConsentedEscrow &&
				P.SourceRef == Proof.IncidentPlanId && P.ZoneId == Proof.ZoneId &&
				P.ObjectIds.Count == 1 && P.ObjectIds[0] == Proof.CollateralObjectId &&
				P.PriorDigest == Proof.SnapshotDigest && P.AppliedDigest == Proof.AppliedDigest &&
				Proof.CommitTick >= P.PreparedTick;
		}

		private static string CustodyDigest(KingdomPolityEscrowCustodyProof P)
		{
			return KingdomPolityRules.ActivationDigest("polity-consented-escrow-custody-v1",
				P.ProjectionId ?? "", P.IncidentPlanId ?? "", P.ZoneId ?? "",
				P.CollateralObjectId ?? "", P.SnapshotDigest ?? "", P.AppliedDigest ?? "",
				P.CommitTick.ToString(CultureInfo.InvariantCulture));
		}
	}
}
