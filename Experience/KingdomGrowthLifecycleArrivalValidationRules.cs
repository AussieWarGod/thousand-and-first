using System;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		private static bool GrowthArrivalCandidateShape(KingdomGrowthBook book,
			KingdomGrowthArrivalCandidate candidate, bool publication)
		{
			if (candidate == null) return IsExactSuccessor(book.ArrivalCandidateNextSequence,
				book.ArrivalCandidateRetiredThrough);
			if (!IsExactSuccessor(candidate.Sequence, book.ArrivalCandidateRetiredThrough)
				|| (publication ? book.ArrivalCandidateNextSequence != candidate.Sequence
					: !IsExactSuccessor(book.ArrivalCandidateNextSequence, candidate.Sequence))
				|| !string.Equals(candidate.Id, GrowthArrivalCandidateId(book.SettlementId,
					candidate.Sequence), StringComparison.Ordinal)
				|| !string.Equals(candidate.SettlementId, book.SettlementId,
					StringComparison.Ordinal) || candidate.CreatedTick < 0L
				|| candidate.UpdatedTick < candidate.CreatedTick
				|| !Enum.IsDefined(typeof(KingdomGrowthArrivalCandidatePhase), candidate.Phase)
				|| !Enum.IsDefined(typeof(KingdomGrowthArrivalDisposition), candidate.Disposition)
				|| !ValidRootId(candidate.Marker) || !ValidName(candidate.Blueprint)
					|| !ValidRootId(candidate.EscrowKey)
					|| !GrowthArrivalCandidateOpportunityShape(book, candidate)
					|| !GrowthArrivalSemanticPlanShape(candidate)
				|| candidate.CandidateLease == null || candidate.LodgingLease == null
				|| candidate.EscrowLease == null
				|| !GrowthLeaseShape(candidate.CandidateLease, candidate.Id, publication)
				|| !GrowthLeaseShape(candidate.LodgingLease, candidate.Id, publication)
				|| !GrowthLeaseShape(candidate.EscrowLease, candidate.Id, publication)
				|| candidate.CandidateLease.Kind != KingdomLifecycleResourceKind.GrowthArrivalCandidate
				|| candidate.LodgingLease.Kind != KingdomLifecycleResourceKind.GrowthArrivalCandidate
				|| candidate.EscrowLease.Kind != KingdomLifecycleResourceKind.GrowthEscrowRelease
				|| !string.Equals(candidate.CandidateLease.ScopeId, book.SettlementId,
					StringComparison.Ordinal)
				|| !string.Equals(candidate.CandidateLease.SubjectId, candidate.Id,
					StringComparison.Ordinal)
				|| !string.Equals(candidate.LodgingLease.ScopeId, book.SettlementId,
					StringComparison.Ordinal)
				|| !string.Equals(candidate.LodgingLease.SubjectId,
					ChildId(candidate.Id, "lodging-lease", 0), StringComparison.Ordinal)
				|| !string.Equals(candidate.EscrowLease.ScopeId, book.SettlementId,
					StringComparison.Ordinal)
				|| !string.Equals(candidate.EscrowLease.SubjectId, candidate.EscrowKey,
					StringComparison.Ordinal)
				|| TooLong(candidate.Fault, MaxTextChars)) return false;
			bool quarantined = candidate.Phase == KingdomGrowthArrivalCandidatePhase.Quarantined;
			if (quarantined ? string.IsNullOrEmpty(candidate.Fault)
				|| !Enum.IsDefined(typeof(KingdomGrowthArrivalCandidatePhase),
					candidate.EvidencePhase)
				|| candidate.EvidencePhase == KingdomGrowthArrivalCandidatePhase.Quarantined
				: candidate.Fault != null || (byte)candidate.EvidencePhase != 0) return false;
			KingdomGrowthArrivalCandidatePhase phase = quarantined
				? candidate.EvidencePhase : candidate.Phase;
			if (!GrowthFirstGuestShape(candidate, phase)
				|| publication && (candidate.FirstGuest == null
					? phase != KingdomGrowthArrivalCandidatePhase.Prepared
					: candidate.LegacyAutomaticRecovery
						|| phase != KingdomGrowthArrivalCandidatePhase.AwaitingChoice)) return false;
			string hash;
			bool legacyUnbound = candidate.LegacyGrowthV1UnboundZone;
			if (legacyUnbound)
			{
				if (publication || candidate.LodgingZoneId != null
					|| phase != KingdomGrowthArrivalCandidatePhase.Prepared
						&& phase != KingdomGrowthArrivalCandidatePhase.CreateIntent
						&& phase != KingdomGrowthArrivalCandidatePhase.Escrowed
					|| !TryLegacyGrowthArrivalCandidateBasePlanHash(candidate, out hash)
					|| !string.Equals(candidate.PlanHash, hash, StringComparison.Ordinal)) return false;
			}
			else if (!TryGrowthArrivalCandidatePlanHash(candidate, out hash)
				|| (publication ? candidate.PlanHash != null
					: !string.Equals(candidate.PlanHash, hash, StringComparison.Ordinal))) return false;
			if (!GrowthFirstGuestBodyLeasePhaseShape(book, candidate, phase)
				|| !GrowthArrivalCandidateLeaseStates(candidate, phase)
				|| !GrowthArrivalCreateStepShape(candidate, phase, legacyUnbound)) return false;
			if (phase == KingdomGrowthArrivalCandidatePhase.AwaitingChoice
				|| phase == KingdomGrowthArrivalCandidatePhase.Declined
				|| phase == KingdomGrowthArrivalCandidatePhase.Prepared
				|| phase == KingdomGrowthArrivalCandidatePhase.CreateIntent)
				return candidate.ObjectId == null && candidate.DispositionStep == null
					&& GrowthArrivalLodgingEmpty(candidate, legacyUnbound)
					&& GrowthArrivalDispositionReasonShape(candidate)
					&& candidate.ConsumingOperationId == null
					&& candidate.ConsumingOperationSequence == 0L;
			if (GrowthFirstGuestDeclinedSettled(candidate, phase))
				return candidate.ObjectId == null && candidate.DispositionStep == null
					&& GrowthArrivalLodgingEmpty(candidate)
					&& candidate.RefusalReason == KingdomGrowthArrivalRefusalReason.None
					&& candidate.ConsumingOperationSequence > 0L
					&& string.Equals(candidate.ConsumingOperationId,
						GrowthOperationId(candidate.SettlementId, KingdomGrowthSlotKind.Arrival,
							null, candidate.ConsumingOperationSequence), StringComparison.Ordinal);
			if (GrowthFirstGuestPhysicalTerminalSettled(candidate, phase))
				return candidate.DispositionStep == null && GrowthArrivalLodgingEmpty(candidate)
					&& candidate.RefusalReason == KingdomGrowthArrivalRefusalReason.None
					&& candidate.ConsumingOperationSequence > 0L
					&& string.Equals(candidate.ConsumingOperationId,
						GrowthOperationId(candidate.SettlementId, KingdomGrowthSlotKind.Arrival,
							null, candidate.ConsumingOperationSequence), StringComparison.Ordinal);
			if (!ValidRootId(candidate.ObjectId)) return false;
			if (phase == KingdomGrowthArrivalCandidatePhase.Escrowed
				|| phase == KingdomGrowthArrivalCandidatePhase.GuestHosted
				|| phase == KingdomGrowthArrivalCandidatePhase.GuestTerminal)
				return candidate.DispositionStep == null
					&& GrowthArrivalLodgingEmpty(candidate, legacyUnbound)
					&& GrowthArrivalDispositionReasonShape(candidate)
					&& candidate.ConsumingOperationId == null
					&& candidate.ConsumingOperationSequence == 0L;
			if (phase == KingdomGrowthArrivalCandidatePhase.LodgingIntent)
				return candidate.DispositionStep == null
					&& GrowthArrivalLodgingIntentShape(candidate)
					&& GrowthArrivalDispositionReasonShape(candidate)
					&& candidate.ConsumingOperationId == null
					&& candidate.ConsumingOperationSequence == 0L;
			if (!GrowthArrivalLodgingObservedShape(candidate)
				|| !GrowthArrivalDispositionReasonShape(candidate)
				|| candidate.Disposition == KingdomGrowthArrivalDisposition.None) return false;
			if (phase == KingdomGrowthArrivalCandidatePhase.Observed)
				return candidate.DispositionStep == null && candidate.ConsumingOperationId == null
					&& candidate.ConsumingOperationSequence == 0L;
			bool joined = candidate.Disposition == KingdomGrowthArrivalDisposition.Joined;
			if (phase == KingdomGrowthArrivalCandidatePhase.ConsumeIntent
				|| phase == KingdomGrowthArrivalCandidatePhase.RefusalIntent)
				return (phase == KingdomGrowthArrivalCandidatePhase.ConsumeIntent) == joined
					&& candidate.ConsumingOperationSequence > 0L
					&& string.Equals(candidate.ConsumingOperationId,
						GrowthOperationId(candidate.SettlementId, KingdomGrowthSlotKind.Arrival,
							null, candidate.ConsumingOperationSequence), StringComparison.Ordinal)
					&& GrowthArrivalDispositionStepShape(candidate, false);
			return phase == KingdomGrowthArrivalCandidatePhase.Settled
				&& candidate.ConsumingOperationSequence > 0L
				&& string.Equals(candidate.ConsumingOperationId,
					GrowthOperationId(candidate.SettlementId, KingdomGrowthSlotKind.Arrival,
						null, candidate.ConsumingOperationSequence), StringComparison.Ordinal)
				&& GrowthArrivalDispositionStepShape(candidate, true);
		}

		private static bool GrowthArrivalSemanticPlanShape(
			KingdomGrowthArrivalCandidate candidate)
		{
			if (candidate == null) return false;
			if (candidate.LegacySemanticPlan)
				return candidate.SemanticPlanVersion == 0 && candidate.SemanticStreamId == null
					&& candidate.SemanticEventKind == 0U && candidate.PlannedOrigin == null
					&& candidate.PlannedCreed == null && candidate.PlannedName == null
					&& candidate.PlannedArrived == null && candidate.ArrivalX == -1
					&& candidate.ArrivalY == -1;
			return candidate.SemanticPlanVersion == 1
				&& ValidRootId(candidate.SemanticStreamId)
				&& candidate.SemanticEventKind > 0U && ValidName(candidate.PlannedOrigin)
				&& ValidName(candidate.PlannedCreed) && ValidName(candidate.PlannedName)
				&& ValidName(candidate.PlannedArrived) && candidate.ArrivalX >= 0
				&& candidate.ArrivalX <= MaxCoordinate && candidate.ArrivalY >= 0
				&& candidate.ArrivalY <= MaxCoordinate;
		}

		private static bool GrowthArrivalCandidateLeaseStates(
			KingdomGrowthArrivalCandidate candidate, KingdomGrowthArrivalCandidatePhase phase)
		{
			bool declinedSettled = GrowthFirstGuestDeclinedSettled(candidate, phase);
			bool physicalSettled = GrowthFirstGuestPhysicalTerminalSettled(candidate, phase);
			KingdomLifecycleLeaseState create = declinedSettled
				? KingdomLifecycleLeaseState.Proved
				: phase == KingdomGrowthArrivalCandidatePhase.CreateIntent
				? KingdomLifecycleLeaseState.Intent
				: GrowthArrivalCreateProvedPhase(phase)
					? KingdomLifecycleLeaseState.Proved : KingdomLifecycleLeaseState.Prepared;
			KingdomLifecycleLeaseState lodging = declinedSettled || physicalSettled
				? KingdomLifecycleLeaseState.Proved
				: phase == KingdomGrowthArrivalCandidatePhase.LodgingIntent
				? KingdomLifecycleLeaseState.Intent
				: GrowthArrivalLodgingProvedPhase(phase)
					? KingdomLifecycleLeaseState.Proved : KingdomLifecycleLeaseState.Prepared;
			KingdomLifecycleLeaseState escrow = phase == KingdomGrowthArrivalCandidatePhase.ConsumeIntent
				|| phase == KingdomGrowthArrivalCandidatePhase.RefusalIntent
					? KingdomLifecycleLeaseState.Intent
					: phase == KingdomGrowthArrivalCandidatePhase.Settled
						? KingdomLifecycleLeaseState.Proved : KingdomLifecycleLeaseState.Prepared;
			return candidate.CandidateLease.State == create
				&& candidate.LodgingLease.State == lodging
				&& candidate.EscrowLease.State == escrow;
		}

		private static bool GrowthArrivalCreateStepShape(KingdomGrowthArrivalCandidate candidate,
			KingdomGrowthArrivalCandidatePhase phase, bool legacyV1 = false)
		{
			KingdomGrowthObjectCallbackStep step = candidate.CreateStep;
			if (step == null || step.Kind != KingdomGrowthObjectMutationKind.Create
				|| !string.Equals(step.EventId, ChildId(candidate.Id, "object-callback", 0),
					StringComparison.Ordinal)
				|| step.FromLocation != KingdomGrowthLocationKind.Absent
				|| step.ToLocation != KingdomGrowthLocationKind.Escrow
				|| !string.Equals(step.EscrowKey, candidate.EscrowKey, StringComparison.Ordinal)
				|| step.BeforeOwnerId != null || step.AfterOwnerId != null
				|| step.BeforeZoneId != null || step.AfterZoneId != null
				|| step.BeforeX != -1 || step.BeforeY != -1 || step.AfterX != -1 || step.AfterY != -1
				|| step.BeforeCount != 0 || step.AfterCount != 1 || !step.NoStack
				|| !GrowthWitnessHash(step.BeforeOwnerGraphHash)
				|| !GrowthWitnessHash(step.BeforeObjectGraphHash)
				|| !GrowthWitnessHash(step.BeforeTopologyHash)
				|| !string.Equals(step.ReceiptId,
					ChildId(candidate.Id, "object-callback-receipt", 0), StringComparison.Ordinal))
				return false;
			bool declinedSettled = GrowthFirstGuestDeclinedSettled(candidate, phase);
			bool proved = GrowthArrivalCreateProvedPhase(phase) && !declinedSettled;
			if (proved) return GrowthObjectCallbackStepShape(step, candidate.Id,
				candidate.ObjectId, candidate.Marker, 0)
				&& step.State == KingdomLifecyclePhysicalState.Proved
				&& string.Equals(step.ReceiptProofId,
					GrowthArrivalCandidateCallbackProof(candidate, step, 0, legacyV1),
					StringComparison.Ordinal);
			if (step.AfterOwnerGraphHash != null || step.AfterObjectGraphHash != null
				|| step.AfterTopologyHash != null) return false;
			if (phase == KingdomGrowthArrivalCandidatePhase.Prepared
				|| phase == KingdomGrowthArrivalCandidatePhase.AwaitingChoice
				|| phase == KingdomGrowthArrivalCandidatePhase.Declined || declinedSettled)
				return step.State == KingdomLifecyclePhysicalState.Prepared
					&& step.ReceiptState == KingdomLifecyclePhysicalState.Prepared
					&& step.ReceiptBeforeMatches == -1 && step.ReceiptAfterMatches == -1
					&& step.ReceiptBeforeCount == -1 && step.ReceiptAfterCount == -1
					&& GrowthObjectCallbackReceiptEmpty(step);
			return step.State == KingdomLifecyclePhysicalState.Intent
				&& step.ReceiptState == KingdomLifecyclePhysicalState.Intent
				&& step.ReceiptBeforeMatches == 0 && step.ReceiptBeforeCount == 0
				&& step.ReceiptAfterMatches == -1 && step.ReceiptAfterCount == -1
				&& GrowthObjectCallbackReceiptBeforeExact(step)
				&& step.ReceiptAfterOwnerGraphHash == null
				&& step.ReceiptAfterObjectGraphHash == null
				&& step.ReceiptAfterTopologyHash == null
				&& step.ReceiptCallbackObjectId == null && step.ReceiptCallbackMarker == null
				&& step.ReceiptCallbackReferenceHash == null && !step.ReceiptSameReference
				&& step.ReceiptProofId == null;
		}

		private static bool GrowthArrivalLodgingEmpty(KingdomGrowthArrivalCandidate candidate,
			bool allowLegacyUnbound = false)
		{
			return (allowLegacyUnbound ? candidate.LodgingZoneId == null
				: ValidName(candidate.LodgingZoneId)) && candidate.LodgingX == -1
				&& candidate.LodgingY == -1 && candidate.LodgingBeforeGraphHash == null
				&& candidate.LodgingDeclaredGraphHash == null
				&& candidate.LodgingReceiptGraphHash == null
				&& candidate.LodgingCallbackReferenceHash == null
				&& !candidate.LodgingSameReference && candidate.LodgingReceiptId == null
				&& candidate.LodgingState == KingdomLifecyclePhysicalState.None;
		}

		private static bool GrowthArrivalLodgingIntentShape(
			KingdomGrowthArrivalCandidate candidate)
		{
			return ValidName(candidate.LodgingZoneId) && candidate.LodgingX >= 0
				&& candidate.LodgingX <= MaxCoordinate && candidate.LodgingY >= 0
				&& candidate.LodgingY <= MaxCoordinate
				&& GrowthWitnessHash(candidate.LodgingBeforeGraphHash)
				&& candidate.LodgingDeclaredGraphHash == null
				&& candidate.LodgingReceiptGraphHash == null
				&& candidate.LodgingCallbackReferenceHash == null
				&& !candidate.LodgingSameReference
				&& string.Equals(candidate.LodgingReceiptId,
					ChildId(candidate.Id, "lodging-receipt", 0), StringComparison.Ordinal)
				&& candidate.LodgingState == KingdomLifecyclePhysicalState.Intent;
		}

		private static bool GrowthArrivalLodgingObservedShape(
			KingdomGrowthArrivalCandidate candidate)
		{
			return ValidName(candidate.LodgingZoneId) && candidate.LodgingX >= 0
				&& candidate.LodgingX <= MaxCoordinate && candidate.LodgingY >= 0
				&& candidate.LodgingY <= MaxCoordinate
				&& GrowthWitnessHash(candidate.LodgingBeforeGraphHash)
				&& GrowthWitnessHash(candidate.LodgingDeclaredGraphHash)
				&& GrowthWitnessHash(candidate.LodgingReceiptGraphHash)
				&& GrowthWitnessHash(candidate.LodgingCallbackReferenceHash)
				&& candidate.LodgingSameReference
				&& string.Equals(candidate.LodgingReceiptId,
					ChildId(candidate.Id, "lodging-receipt", 0), StringComparison.Ordinal)
				&& string.Equals(candidate.LodgingDeclaredGraphHash,
					GrowthArrivalLodgingProof(candidate), StringComparison.Ordinal)
				&& candidate.LodgingState == KingdomLifecyclePhysicalState.Proved;
		}

		private static bool GrowthArrivalDispositionReasonShape(
			KingdomGrowthArrivalCandidate candidate)
		{
			if (!Enum.IsDefined(typeof(KingdomGrowthArrivalRefusalReason),
				candidate.RefusalReason)) return false;
			return candidate.Disposition == KingdomGrowthArrivalDisposition.NoAcceptableHome
				? candidate.RefusalReason != KingdomGrowthArrivalRefusalReason.None
				: candidate.RefusalReason == KingdomGrowthArrivalRefusalReason.None;
		}

	}
}
