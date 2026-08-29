namespace ThousandAndFirst
{
	public static partial class KingdomPolityCohortRules
	{
		/// <summary>Cancels only a finite plan that never prepared or minted an endpoint body.</summary>
		public static bool TryCancelUnpresented(KingdomPolityLedger Ledger,
			long ExpectedRevision, string CohortId, string CancellationRef,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!KingdomPolityRules.SemanticId(CancellationRef) ||
				CancellationRef.StartsWith("taf:standing:", System.StringComparison.Ordinal))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "unpresented cancellation has no exact cause", out Failure);
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(Ledger, CohortId);
			if (cohort != null && cohort.Phase == KingdomPolityCohortPhase.Cancelled &&
				cohort.RewardEventId == CancellationRef)
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (cohort == null || cohort.Phase != KingdomPolityCohortPhase.Planned ||
				!string.IsNullOrEmpty(cohort.ManifestationReceiptId))
				return KingdomPolityAuthority.Refuse(Result,
					"only an unpresented finite cohort can cancel", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityCohortPlan changed = KingdomPolityAuthority.Cohort(candidate, CohortId);
			changed.Phase = KingdomPolityCohortPhase.Cancelled;
			changed.RewardEventId = CancellationRef;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}
	}
}
