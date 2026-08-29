namespace ThousandAndFirst
{
	public static partial class KingdomPolityHospitalityRules
	{
		public static bool TryPlanDebit(KingdomPolityLedger Ledger, long ExpectedRevision,
			string TermsPlanId, KingdomPolityHospitalityPlanRequest Request,
			out KingdomPolityHospitalityTransaction Transaction,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Transaction = null;
			Result = KingdomPolityAuthority.Begin(Ledger);
			Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!TryCreateTransaction(TermsPlanId, Request, out Transaction, out Failure))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityIncidentRecord plan = FindIncident(Ledger, TermsPlanId);
			if (!ExactLoadedEnvoyScene(Ledger, plan, Request))
				return KingdomPolityAuthority.Refuse(Result,
					"hospitality requires this exact loaded envoy endpoint", out Failure);
			if (plan.Hospitality != null)
			{
				if (!SameTransaction(plan.Hospitality, Transaction))
					return KingdomPolityAuthority.Refuse(Result,
						"terms already own another hospitality transaction", out Failure);
				Transaction = plan.Hospitality;
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision;
				return true;
			}
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			FindIncident(candidate, TermsPlanId).Hospitality = Transaction;
			if (!KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure)) return false;
			Transaction = FindIncident(Ledger, TermsPlanId).Hospitality;
			return true;
		}

		public static bool TryCommitDebit(KingdomPolityLedger Ledger, long ExpectedRevision,
			string TermsPlanId, KingdomPolityHospitalityProof Proof, long Tick,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger);
			Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!TryValidate(Proof, out Failure) || Tick < 0L)
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityIncidentRecord plan = FindIncident(Ledger, TermsPlanId);
			KingdomPolityHospitalityTransaction transaction = plan?.Hospitality;
			if (transaction == null || !ProofMatches(transaction, Proof, Tick))
				return KingdomPolityAuthority.Refuse(Result,
					"hospitality proof differs from the frozen physical debit", out Failure);
			if (transaction.Phase == KingdomPolityHospitalityPhase.Debited ||
				transaction.Phase == KingdomPolityHospitalityPhase.Applied)
			{
				if (transaction.Proof?.ProofDigest != Proof.ProofDigest)
					return KingdomPolityAuthority.Refuse(Result,
						"hospitality transaction already carries another proof", out Failure);
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision;
				return true;
			}
			if (transaction.Phase != KingdomPolityHospitalityPhase.Planned ||
				plan.Conclusion != null)
				return KingdomPolityAuthority.Refuse(Result,
					"hospitality debit cannot commit from this phase", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityHospitalityTransaction changed =
				FindIncident(candidate, TermsPlanId).Hospitality;
			changed.Phase = KingdomPolityHospitalityPhase.Debited;
			changed.DebitedTick = Tick;
			changed.Proof = CopyProof(Proof);
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		public static bool TryQuarantineDebit(KingdomPolityLedger Ledger,
			long ExpectedRevision, string TermsPlanId, string Fault,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger);
			Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) ||
				!KingdomPolityRules.Text(Fault, true))
				return KingdomPolityAuthority.Refuse(Result, Failure, out Failure);
			KingdomPolityIncidentRecord plan = FindIncident(Ledger, TermsPlanId);
			KingdomPolityHospitalityTransaction transaction = plan?.Hospitality;
			if (transaction?.Phase == KingdomPolityHospitalityPhase.Quarantined &&
				transaction.Fault == Fault)
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision;
				return true;
			}
			if (transaction == null || transaction.Phase != KingdomPolityHospitalityPhase.Planned)
				return KingdomPolityAuthority.Refuse(Result,
					"only an unresolved hospitality debit can quarantine", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityHospitalityTransaction changed =
				FindIncident(candidate, TermsPlanId).Hospitality;
			changed.Phase = KingdomPolityHospitalityPhase.Quarantined;
			changed.Fault = Fault;
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		internal static KingdomPolityIncidentRecord FindIncident(KingdomPolityLedger L,
			string Id)
		{
			for (int i = 0; L != null && i < L.Incidents.Count; i++)
				if (L.Incidents[i].IncidentPlanId == Id) return L.Incidents[i];
			return null;
		}

		private static bool ExactLoadedEnvoyScene(KingdomPolityLedger L,
			KingdomPolityIncidentRecord Plan, KingdomPolityHospitalityPlanRequest Request)
		{
			if (Plan == null || Plan.Conclusion != null ||
				Plan.Purpose != KingdomPolityCohortPurpose.Envoy ||
				Plan.ParticipantCohortRefs.Count != 1 ||
				!KingdomPolityAuthority.Contains(Plan.EligibleSurfaceRefs, Request.SurfaceRef))
				return false;
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(L,
				Plan.ParticipantCohortRefs[0]);
			KingdomPolityProjectionReceipt projection = cohort == null ? null :
				KingdomPolityAuthority.Projection(L, cohort.ManifestationReceiptId);
			return cohort != null && cohort.Purpose == KingdomPolityCohortPurpose.Envoy &&
				cohort.Phase == KingdomPolityCohortPhase.Materialized &&
				cohort.SurfaceRef == Request.SurfaceRef && projection != null &&
				projection.Kind == KingdomPolityProjectionKind.CohortManifestation &&
				projection.SourceRef == cohort.CohortId &&
				projection.Phase == KingdomPolityProjectionPhase.Committed &&
				projection.ZoneId == Request.ZoneId;
		}

		private static KingdomPolityHospitalityProof CopyProof(KingdomPolityHospitalityProof P)
		{
			return new KingdomPolityHospitalityProof
			{
				ProofId = P.ProofId, SourceAuthorityId = P.SourceAuthorityId,
				ItemOrServingId = P.ItemOrServingId, BeforeQuantity = P.BeforeQuantity,
				AfterQuantity = P.AfterQuantity, ConsumedQuantity = P.ConsumedQuantity,
				ReceiptId = P.ReceiptId, ObservedFactId = P.ObservedFactId,
				CommitTick = P.CommitTick, ProofDigest = P.ProofDigest
			};
		}
	}
}
