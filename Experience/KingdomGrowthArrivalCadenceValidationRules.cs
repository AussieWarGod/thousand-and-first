using System;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		internal static bool GrowthArrivalCadenceShape(KingdomGrowthBook book)
		{
			if (book == null || book.ArrivalDebtRanges == null
				|| book.ArrivalDebtRanges.Count > MaxGrowthArrivalDebtRanges
				|| !string.Equals(book.ArrivalEventStreamId, GrowthArrivalEventStreamId,
					StringComparison.Ordinal)
				|| book.ArrivalProcessedThroughTick < 0L || book.ArrivalRateEpochStartedTick < 0L
				|| book.ArrivalRateCohort < 0 || book.ArrivalOrdinalRetiredThrough
					> book.ArrivalOrdinalHighWater) return false;
			if (book.ArrivalCadenceMigrationPending)
				return HistoricalArrivalCadenceShape(book);
			if (book.ArrivalRateEpoch <= 0L || book.ArrivalRulesVersion <= 0
				|| book.ArrivalIntervalTicks <= 0L || book.ArrivalCadenceNextDueTick <= 0L
				|| book.ArrivalRateEpochStartedTick > book.ArrivalProcessedThroughTick
				|| book.ArrivalCadenceNextDueTick <= book.ArrivalProcessedThroughTick) return false;
			if (book.ArrivalOrdinalRetiredThrough == ulong.MaxValue)
				return book.ArrivalOrdinalHighWater == ulong.MaxValue
					&& book.ArrivalOpportunity == null && book.ArrivalDebtRanges.Count == 0
					&& book.NextArrivalTick == book.ArrivalCadenceNextDueTick;
			ulong expected = book.ArrivalOrdinalRetiredThrough + 1UL;
			if (book.ArrivalOpportunity != null)
			{
				if (!GrowthArrivalOpportunityShape(book.ArrivalOpportunity)
					|| !string.Equals(book.ArrivalOpportunity.SettlementId, book.SettlementId,
						StringComparison.Ordinal)
					|| book.ArrivalOpportunity.Ordinal != expected) return false;
				expected = expected == ulong.MaxValue ? 0UL : expected + 1UL;
				if (expected == 0UL && book.ArrivalDebtRanges.Count > 0) return false;
			}
			long priorDue = book.ArrivalOpportunity?.DueTick ?? -1L;
			for (int i = 0; i < book.ArrivalDebtRanges.Count; i++)
			{
				KingdomGrowthArrivalDebtRange range = book.ArrivalDebtRanges[i];
				if (!GrowthArrivalRangeShape(range) || range.FirstOrdinal != expected
					|| range.FirstDueTick <= priorDue
					|| range.Count - 1UL > ulong.MaxValue - expected)
					return false;
				expected = range.Count - 1UL == ulong.MaxValue - expected
					? 0UL : expected + range.Count;
				if (expected == 0UL && i + 1 < book.ArrivalDebtRanges.Count) return false;
				long span = range.Count > 1UL && range.Count - 1UL > (ulong)long.MaxValue
					? -1L : (long)(range.Count - 1UL);
				if (span < 0L || span > 0L && span > (long.MaxValue - range.FirstDueTick)
					/ range.IntervalTicks) return false;
				priorDue = range.FirstDueTick + span * range.IntervalTicks;
			}
			ulong observedHigh = expected == 0UL ? ulong.MaxValue : expected - 1UL;
			if (observedHigh != book.ArrivalOrdinalHighWater) return false;
			long frontier = ArrivalClockFrontier(book);
			return frontier > 0L && book.NextArrivalTick == frontier;
		}

		private static bool HistoricalArrivalCadenceShape(KingdomGrowthBook book)
		{
			ulong retired = book.ArrivalCandidateRetiredThrough < 0L
				? ulong.MaxValue : (ulong)book.ArrivalCandidateRetiredThrough;
			ulong high = book.ArrivalCandidate == null ? retired : retired + 1UL;
			return book.ArrivalRateEpoch == 0L && book.ArrivalRulesVersion == 0
				&& book.ArrivalRateEpochStartedTick == 0L && book.ArrivalProcessedThroughTick == 0L
				&& book.ArrivalRateCohort == 0 && book.ArrivalCadenceNextDueTick == 0L
				&& book.ArrivalDebtRanges.Count == 0 && book.ArrivalOpportunity == null
				&& !book.ArrivalCadenceResumePending
				&& book.ArrivalOrdinalRetiredThrough == retired
				&& book.ArrivalOrdinalHighWater == high;
		}

		private static bool GrowthArrivalRangeShape(KingdomGrowthArrivalDebtRange range)
		{
			return range != null && range.RulesVersionAtCreation > 0 && range.RateEpoch > 0L
				&& range.Cohort >= 0 && range.FirstOrdinal > 0UL && range.Count > 0UL
				&& range.FirstDueTick >= 0L && range.IntervalTicks > 0L;
		}

		private static bool GrowthArrivalOpportunityShape(KingdomGrowthArrivalOpportunity opportunity)
		{
			string hash;
			return TryGrowthArrivalOpportunityPayloadHash(opportunity, out hash)
				&& string.Equals(hash, opportunity.PayloadHash, StringComparison.Ordinal);
		}

		private static bool GrowthArrivalCandidateOpportunityShape(KingdomGrowthBook book,
			KingdomGrowthArrivalCandidate candidate)
		{
			if (book.ArrivalCadenceMigrationPending)
				return candidate.ArrivalOpportunityOrdinal == 0UL
					&& candidate.ArrivalOpportunityDueTick == 0L
					&& candidate.ArrivalOpportunityRateEpoch == 0L
					&& candidate.ArrivalOpportunityPayloadHash == null;
			KingdomGrowthArrivalOpportunity opportunity = book.ArrivalOpportunity;
			return opportunity != null && candidate.ArrivalOpportunityOrdinal == opportunity.Ordinal
				&& candidate.ArrivalOpportunityDueTick == opportunity.DueTick
				&& candidate.ArrivalOpportunityRateEpoch == opportunity.RateEpoch
				&& string.Equals(candidate.ArrivalOpportunityPayloadHash,
					opportunity.PayloadHash, StringComparison.Ordinal)
				&& candidate.SemanticPlanVersion == opportunity.RulesVersionAtCreation
				&& string.Equals(candidate.SemanticStreamId, opportunity.EventStreamId,
					StringComparison.Ordinal)
				&& candidate.SemanticEventKind == opportunity.EventKindCode
				&& string.Equals(candidate.Blueprint, opportunity.Blueprint, StringComparison.Ordinal)
				&& string.Equals(candidate.PlannedOrigin, opportunity.Origin, StringComparison.Ordinal)
				&& string.Equals(candidate.PlannedCreed, opportunity.Creed, StringComparison.Ordinal)
				&& string.Equals(candidate.PlannedName, opportunity.PersonName, StringComparison.Ordinal)
				&& string.Equals(candidate.PlannedArrived, opportunity.Arrived,
					StringComparison.Ordinal);
		}

		private static bool GrowthArrivalOperationOpportunityShape(KingdomGrowthBook book,
			KingdomGrowthOperation operation)
		{
			if (book.ArrivalCadenceMigrationPending)
				return operation.ArrivalOpportunityOrdinal == 0UL
					&& operation.ArrivalOpportunityDueTick == 0L
					&& operation.ArrivalOpportunityRateEpoch == 0L
					&& operation.ArrivalOpportunityPayloadHash == null;
			KingdomGrowthArrivalOpportunity opportunity = book.ArrivalOpportunity;
			return opportunity != null && operation.ArrivalOpportunityOrdinal == opportunity.Ordinal
				&& operation.ArrivalOpportunityDueTick == opportunity.DueTick
				&& operation.ArrivalOpportunityRateEpoch == opportunity.RateEpoch
				&& string.Equals(operation.ArrivalOpportunityPayloadHash,
					opportunity.PayloadHash, StringComparison.Ordinal);
		}
	}
}
