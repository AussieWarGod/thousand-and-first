using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCohortRules
	{
		/// <summary>Rolls back only an uncommitted exact endpoint projection after its bodies are gone.</summary>
		public static bool TryRollbackPreparedEndpointManifestation(KingdomPolityLedger Ledger,
			long ExpectedRevision, string CohortId, string ProjectionId, string ZoneId,
			IList<string> RemovedOrAbsentObjectIds, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Result.ProjectionId = ProjectionId;
			Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!KingdomPolityRules.TypedId(ProjectionId, "taf:projection:cohort:v1:") ||
				!KingdomPolityRules.Text(ZoneId, true))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "prepared rollback identity is invalid", out Failure);
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(Ledger, CohortId);
			if (cohort == null) return KingdomPolityAuthority.Refuse(Result,
				"prepared rollback cohort is missing", out Failure);
			KingdomPolityProjectionReceipt expected = PreparedReceipt(cohort, ZoneId, 0L);
			if (expected.ProjectionId != ProjectionId ||
				!ExactObjectIds(expected.ObjectIds, RemovedOrAbsentObjectIds))
				return KingdomPolityAuthority.Refuse(Result,
					"prepared rollback does not account for the exact projection", out Failure);
			KingdomPolityProjectionReceipt receipt = KingdomPolityAuthority.Projection(
				Ledger, ProjectionId);
			if (receipt == null && cohort.Phase == KingdomPolityCohortPhase.Planned &&
				string.IsNullOrEmpty(cohort.ManifestationReceiptId))
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (!BoundReceipt(cohort, receipt) ||
				!ExactEndpointReceipt(cohort, receipt, ZoneId) ||
				receipt.Phase != KingdomPolityProjectionPhase.Prepared ||
				cohort.Phase != KingdomPolityCohortPhase.Planned)
				return KingdomPolityAuthority.Refuse(Result,
					"only an exact uncommitted manifestation can roll back", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityCohortPlan changed = KingdomPolityAuthority.Cohort(candidate, CohortId);
			KingdomPolityProjectionReceipt removed = KingdomPolityAuthority.Projection(
				candidate, ProjectionId);
			changed.ManifestationReceiptId = null; candidate.Projections.Remove(removed);
			ClearRouteManifestation(candidate, ProjectionId);
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}
	}
}
