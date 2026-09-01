namespace ThousandAndFirst
{
	public static partial class KingdomPolityAmbientTransactionRules
	{
		/// <summary>CAS-publishes the resident consumer's prepared or terminal receipt without
		/// changing the accepted ambient choice or its frozen terminal receipt.</summary>
		public static bool TryPublishAdmissionReceipt(KingdomPolityLedger Ledger,
			long ExpectedRevision, KingdomPolityAdmissionReceipt Receipt,
			out KingdomPolityPublicationResult Result, out string Failure)
		{
			Result = KingdomPolityAuthority.Begin(Ledger); Failure = null;
			if (!KingdomPolityRules.TryValidate(Ledger, out Failure) || Receipt == null)
				return KingdomPolityAuthority.Refuse(Result, Failure ??
					"admission publication lacks a receipt", out Failure);
			KingdomPolityCohortPlan cohort = KingdomPolityAuthority.Cohort(Ledger,
				Receipt.CohortId);
			KingdomPolityAdmissionHandoff handoff = cohort?.AmbientTransaction?.AdmissionHandoff;
			if (cohort == null || cohort.Purpose != KingdomPolityCohortPurpose.Migrant ||
				cohort.AmbientTransaction.TerminalChoice !=
					KingdomPolityAmbientTerminalChoice.PetitionAccepted ||
				handoff?.Decision != KingdomPolityAdmissionDecision.Accepted ||
				!KingdomPolityAdmissionReceiptRules.Valid(Receipt, handoff))
				return KingdomPolityAuthority.Refuse(Result,
					"admission receipt does not consume the exact accepted petition", out Failure);
			KingdomPolityAdmissionReceipt current = handoff.AdmissionReceipt;
			if (KingdomPolityAdmissionReceiptRules.Same(current, Receipt))
			{
				Result.Outcome = KingdomPolityCasOutcome.AlreadyApplied;
				Result.CommittedRevision = Ledger.Revision; return true;
			}
			if (!CanAdvance(current, Receipt)) return KingdomPolityAuthority.Refuse(Result,
				"admission receipt would regress or replace its operation", out Failure);
			if (Ledger.Revision != ExpectedRevision)
				return KingdomPolityAuthority.Conflict(Result, out Failure);
			KingdomPolityLedger candidate = KingdomPolityRules.Clone(Ledger);
			KingdomPolityAdmissionHandoff changed = KingdomPolityAuthority.Cohort(candidate,
				Receipt.CohortId).AmbientTransaction.AdmissionHandoff;
			changed.AdmissionReceipt = Receipt.Copy();
			return KingdomPolityAuthority.Commit(Ledger, candidate, Result, out Failure);
		}

		private static bool CanAdvance(KingdomPolityAdmissionReceipt Current,
			KingdomPolityAdmissionReceipt Next)
		{
			if (Current == null)
				return Next.Phase == KingdomPolityAdmissionReceiptPhase.Prepared ||
					Next.Phase == KingdomPolityAdmissionReceiptPhase.Rejected ||
					Next.Phase == KingdomPolityAdmissionReceiptPhase.RolledBack;
			return Current.ReceiptId == Next.ReceiptId && Current.OperationId == Next.OperationId &&
				Current.Digest != Next.Digest &&
				Current.Phase == KingdomPolityAdmissionReceiptPhase.Prepared &&
				Next.Phase != KingdomPolityAdmissionReceiptPhase.Prepared;
		}
	}
}
