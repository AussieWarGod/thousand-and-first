using System;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityCohortRules
	{
		public static bool TryCancelExpiredScheduled(KingdomPolityLedger Ledger,
			long ExpectedRevision, string CohortId, long Tick,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) || Tick < 0L)
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(Ledger, CohortId);
			if (cohort != null && cohort.Phase == KingdomPolityCohortPhase.Cancelled)
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true;
			}
			if (cohort == null || cohort.Phase != KingdomPolityCohortPhase.Planned ||
				!string.IsNullOrEmpty(cohort.ManifestationReceiptId) ||
				!KingdomPolityDispatchRules.Expired(cohort, Tick))
				return KingdomPolityAuthority.Refuse(Result,
					"only an unprojected expired scheduled cohort can cancel", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityAuthority.Cohort(candidate, CohortId).Phase =
				KingdomPolityCohortPhase.Cancelled;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		public static bool TryConcludeScheduledStay(KingdomPolityLedger Ledger,
			long ExpectedRevision, string CohortId, string SettlementId, long Tick,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(Ledger, CohortId);
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!KingdomPolityRules.TypedId(SettlementId, "taf:settlement:v1:") || Tick < 0L ||
				cohort == null || cohort.SurfaceRef != SettlementId ||
				!KingdomPolityDispatchRules.Expired(cohort, Tick))
				return KingdomPolityAuthority.Refuse(Result,
					Failure ?? "scheduled stay has not expired at its exact endpoint", out Failure);
			string witnessed = KingdomPolityRules.ActivationId(
				"taf:event:polity-departure:v1:", "polity-scheduled-departure-v1",
				cohort.CohortId, SettlementId,
					cohort.EventOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture));
			return TryConcludeEndpointCohort(Ledger, ExpectedRevision, CohortId, witnessed,
				out Result, out Failure);
		}

		public static bool TryPruneScheduledTerminals(KingdomPolityLedger Ledger,
			long ExpectedRevision, ulong CurrentWindow,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			bool any = false;
			for (int i = 0; i < Ledger.Cohorts.Count; i++)
				if (Prunable(Ledger, Ledger.Cohorts[i], CurrentWindow)) { any = true; break; }
			if (!any) { Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied; return true; }
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			for (int i = candidate.Cohorts.Count - 1; i >= 0; i--)
			{
				KingdomPolityCohortPlan cohort = candidate.Cohorts[i];
					if (!Prunable(candidate, cohort, CurrentWindow)) continue;
				if (!string.IsNullOrEmpty(cohort.ManifestationReceiptId))
				{
					KingdomPolityProjectionReceipt receipt = KingdomPolityAuthority.Projection(
						candidate, cohort.ManifestationReceiptId);
					if (receipt == null || receipt.Phase != KingdomPolityProjectionPhase.Cleaned)
						return KingdomPolityAuthority.Refuse(Result,
							"scheduled cohort terminal projection is not clean", out Failure);
					candidate.Projections.Remove(receipt);
				}
				candidate.Cohorts.RemoveAt(i);
			}
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		private static bool Prunable(KingdomPolityLedger Ledger, KingdomPolityCohortPlan C,
			ulong CurrentWindow)
		{
			if (!KingdomPolityDispatchRules.IsScheduled(C) || C.EventOrdinal >= CurrentWindow)
				return false;
			if (C.Phase == KingdomPolityCohortPhase.Cancelled ||
				C.Phase == KingdomPolityCohortPhase.Cleaned) return true;
			if (C.Phase != KingdomPolityCohortPhase.Abandoned) return false;
			KingdomPolityProjectionReceipt receipt = KingdomPolityAuthority.Projection(Ledger,
				C.ManifestationReceiptId);
			return receipt?.Phase == KingdomPolityProjectionPhase.Cleaned;
		}
	}
}
