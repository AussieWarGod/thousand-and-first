namespace ThousandAndFirst
{
	public static partial class KingdomPolityCohortRules
	{
		/// <summary>Records proved physical loss without claiming any semantic death consequence.</summary>
		internal static bool TryAbandonEndpointCohort(KingdomPolityLedger Ledger,
			long ExpectedRevision, KingdomPolityDeathIntentRecord Intent,
			bool ExactDeathRemovalWitness, out KingdomPolityPublicationResult Result,
			out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!ExactDeathRemovalWitness || Intent == null ||
				Intent.Visibility != KingdomPolityDeathVisibility.PhysicalOnly ||
				Intent.Attribution != KingdomPolityDeathAttribution.Unattributed ||
				!KingdomPolityDeathIntentRules.TryEncode(Intent, out string _, out Failure))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "cohort abandonment lacks exact physical death proof", out Failure);
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(Ledger, Intent.CohortId);
			KingdomPolityProjectionReceipt receipt = cohort == null ? null :
				KingdomPolityAuthority.Projection(Ledger, cohort.ManifestationReceiptId);
			Result.ProjectionId = Intent.ProjectionId;
			bool exact = cohort != null && receipt != null &&
				KingdomPolityDeathIntentRules.ExactBinding(Intent, Ledger.RealmId, cohort.CohortId,
					receipt.ProjectionId, receipt.ZoneId,
					Intent.Ordinal >= 0 && Intent.Ordinal < cohort.ResolvedMembers.Count
						? PreparedObjectId(cohort, Intent.Ordinal) : null,
					Intent.Ordinal, cohort.Purpose, Intent.Ordinal == 0) &&
				cohort.ManifestationReceiptId == receipt.ProjectionId &&
				receipt.Kind == KingdomPolityProjectionKind.CohortManifestation &&
				receipt.SourceRef == cohort.CohortId &&
				KingdomPolityAuthority.Contains(receipt.ObjectIds, Intent.ObjectId) &&
				Intent.Tick >= receipt.CommittedTick;
			if (!exact) return KingdomPolityAuthority.Refuse(Result,
				"death authority changed during callbacks", out Failure);
			if (cohort.Phase == KingdomPolityCohortPhase.Abandoned &&
				string.IsNullOrEmpty(cohort.RewardEventId) &&
				(receipt.Phase == KingdomPolityProjectionPhase.Committed ||
				 receipt.Phase == KingdomPolityProjectionPhase.Cleaned))
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (cohort.Phase != KingdomPolityCohortPhase.Materialized ||
				receipt.Phase != KingdomPolityProjectionPhase.Committed ||
				!string.IsNullOrEmpty(cohort.RewardEventId))
				return KingdomPolityAuthority.Refuse(Result,
					"only an unrewarded materialized cohort can be abandoned", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityCohortPlan changed = KingdomPolityAuthority.Cohort(candidate,
				Intent.CohortId);
			changed.Phase = KingdomPolityCohortPhase.Abandoned;
			changed.RewardEventId = null;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}
	}
}
