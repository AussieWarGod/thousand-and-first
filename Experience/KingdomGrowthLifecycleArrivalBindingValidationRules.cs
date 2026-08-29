using System;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		private static bool GrowthArrivalCandidateBindingShape(KingdomGrowthBook book,
			KingdomGrowthOperation operation, bool publication)
		{
			if (operation.Action != KingdomGrowthAction.Arrival)
				return operation.ArrivalCandidateId == null;
			if (!GrowthArrivalOperationOpportunityShape(book, operation)) return false;
			bool needsCandidate = operation.ArrivalDisposition ==
				KingdomGrowthArrivalDisposition.Joined
				|| operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.NoAcceptableHome
				|| operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.Declined
				|| operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.Departed;
			if (!needsCandidate)
				return operation.ArrivalCandidateId == null && book.ArrivalCandidate == null;
			KingdomGrowthArrivalCandidate candidate = book.ArrivalCandidate;
			if (candidate == null || !ReferenceEquals(book.ArrivalCandidate, candidate)
				|| !string.Equals(candidate.Id, operation.ArrivalCandidateId,
					StringComparison.Ordinal)
				|| candidate.Disposition != operation.ArrivalDisposition) return false;
			if (candidate.Disposition == KingdomGrowthArrivalDisposition.Joined)
			{
				if (!string.Equals(operation.TargetId, candidate.ObjectId, StringComparison.Ordinal)
					|| !string.Equals(operation.TargetMarker, candidate.Marker,
						StringComparison.Ordinal)
					|| !string.Equals(operation.Blueprint, candidate.Blueprint,
						StringComparison.Ordinal)
					|| operation.TargetTopology != KingdomLifecycleTopology.Cell
					|| operation.TargetLocation != KingdomGrowthLocationKind.Cell
					|| operation.TargetOwnerId != null
					|| !string.Equals(operation.ZoneId, candidate.LodgingZoneId,
						StringComparison.Ordinal)
					|| operation.TargetX != candidate.LodgingX
					|| operation.TargetY != candidate.LodgingY) return false;
			}
			if (publication)
				return (candidate.Disposition == KingdomGrowthArrivalDisposition.Declined
					? candidate.Phase == KingdomGrowthArrivalCandidatePhase.Declined
					: candidate.Disposition == KingdomGrowthArrivalDisposition.Departed
						? candidate.Phase == KingdomGrowthArrivalCandidatePhase.GuestTerminal
						: candidate.Phase == KingdomGrowthArrivalCandidatePhase.Observed)
					&& candidate.ConsumingOperationId == null
					&& candidate.ConsumingOperationSequence == 0L;
			if (candidate.Phase == KingdomGrowthArrivalCandidatePhase.Quarantined
				&& operation.Phase != KingdomGrowthPhase.Quarantined) return false;
			KingdomGrowthArrivalCandidatePhase effectivePhase = candidate.Phase ==
				KingdomGrowthArrivalCandidatePhase.Quarantined
					? candidate.EvidencePhase : candidate.Phase;
			if (effectivePhase == KingdomGrowthArrivalCandidatePhase.Observed)
				return candidate.ConsumingOperationId == null
					&& candidate.ConsumingOperationSequence == 0L
					&& (operation.Phase == KingdomGrowthPhase.Prepared
						|| operation.Phase == KingdomGrowthPhase.WaterIntent
						|| operation.Phase == KingdomGrowthPhase.WaterSettled
						|| operation.Phase == KingdomGrowthPhase.Quarantined);
			bool closed = candidate.Disposition == KingdomGrowthArrivalDisposition.Declined
				|| candidate.Disposition == KingdomGrowthArrivalDisposition.Departed;
			if (closed)
				return (effectivePhase == (candidate.Disposition ==
					KingdomGrowthArrivalDisposition.Declined
						? KingdomGrowthArrivalCandidatePhase.Declined
						: KingdomGrowthArrivalCandidatePhase.GuestTerminal)
					&& candidate.ConsumingOperationId == null
					&& candidate.ConsumingOperationSequence == 0L
					&& operation.Phase == KingdomGrowthPhase.Prepared)
					|| effectivePhase == KingdomGrowthArrivalCandidatePhase.Settled
						&& string.Equals(candidate.ConsumingOperationId, operation.Id,
							StringComparison.Ordinal)
						&& candidate.ConsumingOperationSequence == operation.Sequence;
			bool rightIntent = candidate.Disposition == KingdomGrowthArrivalDisposition.Joined
				? effectivePhase == KingdomGrowthArrivalCandidatePhase.ConsumeIntent
				: effectivePhase == KingdomGrowthArrivalCandidatePhase.RefusalIntent;
			return (rightIntent || effectivePhase == KingdomGrowthArrivalCandidatePhase.Settled)
				&& string.Equals(candidate.ConsumingOperationId, operation.Id,
					StringComparison.Ordinal)
				&& candidate.ConsumingOperationSequence == operation.Sequence;
		}
	}
}
