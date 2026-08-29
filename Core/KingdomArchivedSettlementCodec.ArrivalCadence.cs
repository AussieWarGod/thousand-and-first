using System;

namespace ThousandAndFirst
{
	internal static partial class KingdomArchivedSettlementCodec
	{
		/// <summary>Proves that omitting v17 cadence fields cannot discard authority.</summary>
		private static bool HistoricalArrivalCadenceValue(Type Type, object Value)
		{
			if (Type == typeof(KingdomGrowthBook))
			{
				KingdomGrowthBook book = (KingdomGrowthBook)Value;
				if (!book.ArrivalCadenceMigrationPending || book.ArrivalCadenceResumePending
					|| book.ArrivalCandidateRetiredThrough < 0L
					|| book.ArrivalRulesVersion != 0 || book.ArrivalRateEpoch != 0L
					|| book.ArrivalRateEpochStartedTick != 0L
					|| book.ArrivalProcessedThroughTick != 0L
					|| book.ArrivalCadenceNextDueTick != 0L || book.ArrivalRateCohort != 0
					|| book.ArrivalOpportunity != null || book.ArrivalDebtRanges == null
					|| book.ArrivalDebtRanges.Count != 0
					|| !string.Equals(book.ArrivalEventStreamId,
						KingdomLifecycleRules.GrowthArrivalEventStreamId,
						StringComparison.Ordinal)) return false;
				ulong retired = (ulong)book.ArrivalCandidateRetiredThrough;
				ulong high = book.ArrivalCandidate == null ? retired : retired + 1UL;
				return (book.ArrivalOrdinalRetiredThrough == 0UL
						|| book.ArrivalOrdinalRetiredThrough == retired)
					&& (book.ArrivalOrdinalHighWater == 0UL
						|| book.ArrivalOrdinalHighWater == high);
			}
			if (Type == typeof(KingdomGrowthArrivalCandidate))
			{
				KingdomGrowthArrivalCandidate candidate =
					(KingdomGrowthArrivalCandidate)Value;
				return candidate.ArrivalOpportunityOrdinal == 0UL
					&& candidate.ArrivalOpportunityDueTick == 0L
					&& candidate.ArrivalOpportunityRateEpoch == 0L
					&& candidate.ArrivalOpportunityPayloadHash == null;
			}
			if (Type == typeof(KingdomGrowthOperation))
			{
				KingdomGrowthOperation operation = (KingdomGrowthOperation)Value;
				return operation.ArrivalOpportunityOrdinal == 0UL
					&& operation.ArrivalOpportunityDueTick == 0L
					&& operation.ArrivalOpportunityRateEpoch == 0L
					&& operation.ArrivalOpportunityPayloadHash == null;
			}
			return true;
		}
	}
}
