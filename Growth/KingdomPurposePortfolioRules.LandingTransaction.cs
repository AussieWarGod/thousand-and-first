namespace ThousandAndFirst
{
	public static partial class KingdomPurposePortfolioRules
	{
		/// <summary>Only a complete fresh walk proves absence. Every named incomplete index is a
		/// refusal, never an empty collection.</summary>
		internal static bool LandingCustodyIsComplete(
			KingdomPurposeLandingCustodyProof Proof)
		{
			return Proof == KingdomPurposeLandingCustodyProof.Complete;
		}

		/// <summary>Only bidirectional cell-rack membership proves the frozen store stands on ground.
		/// A surviving cell pointer with no reciprocal list entry is torn custody.</summary>
		internal static bool LandingStoreRackIsExact(
			KingdomPurposeLandingStoreRackProof Proof)
		{
			return Proof == KingdomPurposeLandingStoreRackProof.Exact;
		}

		/// <summary>Fresh placement is possible only from clean, fully proved evidence. This closes
		/// the retry after either an outstanding attempt or a durable terminal fault.</summary>
		internal static bool LandingCanOfferFresh(
			KingdomPurposeLandingTransactionState State)
		{
			return State.OperationPhase == KingdomPurposeOperationPhase.LandingPending
				&& State.Cleanup == KingdomPurposeLandingCleanupStep.None
				&& State.Carried > 0 && State.ExactServingMarks >= 0
				&& State.ExactServingMarks <= State.Carried && State.RootPresent
				&& State.Attempt == KingdomPurposeLandingAttemptState.Clear
				&& !State.FaultPresent && State.MeasuredRosterExact
				&& !State.MalformedCoResidentEvidence
				&& LandingCustodyIsComplete(State.Custody)
				&& LandingStoreRackIsExact(State.StoreRack)
				&& (State.CargoRecord == KingdomPurposeLandingCargoRecordShape.CleanLegacy
					|| State.CargoRecord
						== KingdomPurposeLandingCargoRecordShape.PartialCurrent);
		}

		/// <summary>Retires one exactly reconciled callback attempt after the entry roster and
		/// custody walk. Later cargo, rack, and retirement proofs are deliberately separate: if one
		/// fails, the final drive stamps a fault while this retired attempt remains retired.</summary>
		internal static KingdomPurposeLandingTransactionVerdict DriveLandingEntryReconciliation(
			KingdomPurposeLandingTransactionState Before,
			out KingdomPurposeLandingTransactionState After)
		{
			After = Before;
			if (!TryDeliveredTarget(Before, out _, out _)
				|| Before.OperationPhase != KingdomPurposeOperationPhase.LandingPending
				|| !ValidFigures(Before) || !OperationRevisionCanAdvance(Before)
				|| !LandingRevisionHeadroomIsValid(Before)
				|| Before.FaultPresent
				|| Before.Cleanup != KingdomPurposeLandingCleanupStep.None
				|| Before.Attempt != KingdomPurposeLandingAttemptState.Settled)
				return KingdomPurposeLandingTransactionVerdict.Refused;
			bool proved = Before.RootPresent && Before.MeasuredRosterExact
				&& !Before.MalformedCoResidentEvidence
				&& LandingCustodyIsComplete(Before.EntryCustody)
				&& (Before.CargoRecord
						== KingdomPurposeLandingCargoRecordShape.PartialCurrent
					|| Before.CargoRecord
						== KingdomPurposeLandingCargoRecordShape.WholeCurrent);
			if (!proved) return KingdomPurposeLandingTransactionVerdict.Refused;
			After.Attempt = KingdomPurposeLandingAttemptState.Clear;
			After.Cleanup = KingdomPurposeLandingCleanupStep.AttemptRetired;
			return KingdomPurposeLandingTransactionVerdict.EntryProved;
		}

		/// <summary>Physical cleanup alone releases nothing. Another operation may rely on the old
		/// provision only after the pair receipt which consumes or replaces it has published.</summary>
		internal static bool RetiredCargoIsReleased(
			KingdomPurposeLandingTransactionState State)
		{
			return State.Cleanup == KingdomPurposeLandingCleanupStep.PairPublished
				&& !State.RootPresent && State.ExactServingMarks == 0
				&& State.CargoRecord == KingdomPurposeLandingCargoRecordShape.CleanLegacy
				&& (State.PairPhase == KingdomPurposePairPhase.Active
					|| State.PairPhase == KingdomPurposePairPhase.ReturnOutstanding
					|| State.PairPhase == KingdomPurposePairPhase.OperationOutstanding);
		}

		/// <summary>Composes delivered-credit or bootstrap-return cleanup with the pair CAS. All
		/// evidence is prevalidated before the first mutation. Once cleanup completes, a refused CAS
		/// leaves an idempotent physical state which the same unchanged pair can publish on retry.</summary>
		internal static KingdomPurposeLandingTransactionVerdict DriveLandingRetirement(
			KingdomPurposeLandingTransactionState Before, bool SemanticCasAccepted,
			out KingdomPurposeLandingTransactionState After)
		{
			After = Before;
			if (!TryRetirementTarget(Before.PairPhase, out KingdomPurposePairPhase target)
				|| Before.OperationPhase != KingdomPurposeOperationPhase.Delivered
				|| !ValidFigures(Before) || Before.FaultPresent
				|| Before.Attempt != KingdomPurposeLandingAttemptState.Clear
				|| !Before.MeasuredRosterExact || Before.MalformedCoResidentEvidence
				|| !LandingCustodyIsComplete(Before.Custody))
				return KingdomPurposeLandingTransactionVerdict.Refused;
			if (!LandingRevisionHeadroomIsValid(Before))
				return FaultAndQuarantineLanding(Before, SemanticCasAccepted, out After);

			bool cleaned = Before.Cleanup == KingdomPurposeLandingCleanupStep.RootRetired
				&& !Before.RootPresent && Before.ExactServingMarks == 0
				&& Before.CargoRecord
					== KingdomPurposeLandingCargoRecordShape.CleanLegacy;
			// PairPublished can be the immediately preceding Delivered publication: that checkpoint
			// retires marks but deliberately keeps the rooted cargo and its whole record for credit.
			// Retirement consumes that real output directly; callers never normalise the model state.
			bool current = (Before.Cleanup == KingdomPurposeLandingCleanupStep.None
					|| Before.Cleanup == KingdomPurposeLandingCleanupStep.PairPublished)
				&& Before.RootPresent
				&& (Before.CargoRecord
						== KingdomPurposeLandingCargoRecordShape.CleanLegacy
					|| Before.CargoRecord
						== KingdomPurposeLandingCargoRecordShape.WholeCurrent);
			if (!cleaned && !current) return KingdomPurposeLandingTransactionVerdict.Refused;
			bool startsOperation = target == KingdomPurposePairPhase.OperationOutstanding
				|| target == KingdomPurposePairPhase.ReturnOutstanding;
			if (startsOperation && Before.NextOperationOrdinal == int.MaxValue)
				return KingdomPurposeLandingTransactionVerdict.Refused;

			if (!cleaned)
			{
				After.Cleanup = KingdomPurposeLandingCleanupStep.Prevalidated;
				After.ExactServingMarks = 0;
				After.Cleanup = KingdomPurposeLandingCleanupStep.MarksRetired;
				After.CargoRecord = KingdomPurposeLandingCargoRecordShape.CleanLegacy;
				After.Cleanup = KingdomPurposeLandingCleanupStep.CargoRecordRetired;
				After.RootPresent = false;
				After.Cleanup = KingdomPurposeLandingCleanupStep.RootRetired;
			}
			if (!SemanticCasAccepted)
				return KingdomPurposeLandingTransactionVerdict.SemanticCasRefused;

			After.PairPhase = target;
			After.ResumePhase = KingdomPurposePairPhase.Invalid;
			After.OperationPhase = target == KingdomPurposePairPhase.Active
				? KingdomPurposeOperationPhase.Invalid
				: KingdomPurposeOperationPhase.Prepared;
			After.OperationRevision = 0;
			if (startsOperation) After.NextOperationOrdinal++;
			After.PairRevision++;
			After.Cleanup = KingdomPurposeLandingCleanupStep.PairPublished;
			if (!LandingRevisionHeadroomIsValid(After))
				return FaultAndQuarantineLanding(Before, SemanticCasAccepted, out After);
			return KingdomPurposeLandingTransactionVerdict.PairPublished;
		}

		/// <summary>Composes final cargo/root/store/custody proof, exact mark retirement, and the
		/// Delivered CAS. Any failed proof stamps a durable fault before quarantine publication is
		/// attempted. A refused quarantine therefore cannot become a fresh offer on retry.</summary>
		internal static KingdomPurposeLandingTransactionVerdict DriveFinalLandingCheckpoint(
			KingdomPurposeLandingTransactionState Before, bool SemanticCasAccepted,
			out KingdomPurposeLandingTransactionState After)
		{
			After = Before;
			if (!TryDeliveredTarget(Before, out KingdomPurposePairPhase target,
					out KingdomPurposePairPhase resumeTarget)
				|| Before.OperationPhase != KingdomPurposeOperationPhase.LandingPending
				|| !ValidFigures(Before))
				return KingdomPurposeLandingTransactionVerdict.Refused;
			if (Before.FaultPresent) return QuarantineLanding(Before,
				SemanticCasAccepted, out After);
			if (!LandingRevisionHeadroomIsValid(Before))
				return FaultAndQuarantineLanding(Before, SemanticCasAccepted, out After);

			bool retryAfterMarks = Before.Cleanup
				== KingdomPurposeLandingCleanupStep.MarksRetired
				&& Before.ExactServingMarks == 0;
			bool firstCompletion = Before.Cleanup == KingdomPurposeLandingCleanupStep.None;
			bool reconciledCompletion = Before.Cleanup
				== KingdomPurposeLandingCleanupStep.AttemptRetired;
			bool proved = Before.RootPresent && Before.MeasuredRosterExact
				&& !Before.MalformedCoResidentEvidence
				&& Before.Attempt == KingdomPurposeLandingAttemptState.Clear
				&& LandingCustodyIsComplete(Before.Custody)
				&& LandingStoreRackIsExact(Before.StoreRack)
				&& Before.CargoRecord
					== KingdomPurposeLandingCargoRecordShape.WholeCurrent
				&& (firstCompletion || reconciledCompletion || retryAfterMarks);
			if (!proved) return FaultAndQuarantineLanding(Before,
				SemanticCasAccepted, out After);
			if (!OperationRevisionCanAdvance(Before))
				return KingdomPurposeLandingTransactionVerdict.Refused;
			if (!retryAfterMarks)
			{
				After.ExactServingMarks = 0;
				After.Cleanup = KingdomPurposeLandingCleanupStep.MarksRetired;
			}
			if (!SemanticCasAccepted)
				return KingdomPurposeLandingTransactionVerdict.SemanticCasRefused;

			After.PairPhase = target;
			After.ResumePhase = resumeTarget;
			After.OperationPhase = KingdomPurposeOperationPhase.Delivered;
			After.OperationRevision++;
			After.PairRevision++;
			After.Cleanup = KingdomPurposeLandingCleanupStep.PairPublished;
			if (!LandingRevisionHeadroomIsValid(After))
				return FaultAndQuarantineLanding(Before, SemanticCasAccepted, out After);
			return KingdomPurposeLandingTransactionVerdict.PairPublished;
		}

		private static KingdomPurposeLandingTransactionVerdict FaultAndQuarantineLanding(
			KingdomPurposeLandingTransactionState Before, bool SemanticCasAccepted,
			out KingdomPurposeLandingTransactionState After)
		{
			Before.FaultPresent = true;
			return QuarantineLanding(Before, SemanticCasAccepted, out After);
		}

		private static KingdomPurposeLandingTransactionVerdict QuarantineLanding(
			KingdomPurposeLandingTransactionState Before, bool SemanticCasAccepted,
			out KingdomPurposeLandingTransactionState After)
		{
			After = Before;
			if (!SemanticCasAccepted)
				return KingdomPurposeLandingTransactionVerdict.SemanticCasRefused;
			After.PairPhase = KingdomPurposePairPhase.Quarantined;
			After.ResumePhase = KingdomPurposePairPhase.Invalid;
			After.PairRevision++;
			return KingdomPurposeLandingTransactionVerdict.Quarantined;
		}

		private static bool ValidFigures(KingdomPurposeLandingTransactionState State)
		{
			return State.PairRevision >= 0 && State.PairRevision < int.MaxValue
				&& State.OperationRevision >= 0 && State.NextOperationOrdinal >= 1
				&& State.Carried > 0
				&& State.ExactServingMarks >= 0
				&& State.ExactServingMarks <= State.Carried;
		}

		private static bool OperationRevisionCanAdvance(
			KingdomPurposeLandingTransactionState State)
		{
			return State.OperationRevision < int.MaxValue;
		}

		private static bool TryRetirementTarget(KingdomPurposePairPhase Phase,
			out KingdomPurposePairPhase Target)
		{
			Target = Phase == KingdomPurposePairPhase.CargoAwaitingConsumption
				? KingdomPurposePairPhase.Active
				: Phase == KingdomPurposePairPhase.CargoAwaitingActivation
					? KingdomPurposePairPhase.OperationOutstanding
					: Phase == KingdomPurposePairPhase.SecondPending
						? KingdomPurposePairPhase.ReturnOutstanding
						: KingdomPurposePairPhase.Invalid;
			return Target != KingdomPurposePairPhase.Invalid;
		}

		private static bool TryDeliveredTarget(KingdomPurposeLandingTransactionState State,
			out KingdomPurposePairPhase Target, out KingdomPurposePairPhase ResumeTarget)
		{
			bool orphaned = State.PairPhase == KingdomPurposePairPhase.Orphaned;
			KingdomPurposePairPhase phase = orphaned ? State.ResumePhase : State.PairPhase;
			KingdomPurposePairPhase delivered = phase == KingdomPurposePairPhase.BootstrapOutstanding
				? KingdomPurposePairPhase.SecondPending
				: phase == KingdomPurposePairPhase.ReturnOutstanding
					? KingdomPurposePairPhase.CargoAwaitingActivation
					: phase == KingdomPurposePairPhase.OperationOutstanding
						? KingdomPurposePairPhase.CargoAwaitingConsumption
						: KingdomPurposePairPhase.Invalid;
			Target = orphaned ? KingdomPurposePairPhase.Orphaned : delivered;
			ResumeTarget = orphaned ? delivered : KingdomPurposePairPhase.Invalid;
			return delivered != KingdomPurposePairPhase.Invalid;
		}
	}
}
