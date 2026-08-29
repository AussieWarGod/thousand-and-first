using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		/// <summary>v1-v6 migration boundary. Existing operation/candidate bytes keep their old
		/// meaning. No overdue interval is inferred; first current-format observation plants epoch 1.</summary>
		internal static bool UpgradeHistoricalGrowthArrivalCadence(KingdomGrowthBook Book)
		{
			if (Book == null || Book.ArrivalCandidateRetiredThrough < 0L) return false;
			ulong retired = (ulong)Book.ArrivalCandidateRetiredThrough;
			ulong high = retired;
			if (Book.ArrivalCandidate != null)
			{
				if (retired == ulong.MaxValue || Book.ArrivalCandidate.Sequence <= 0L
					|| (ulong)Book.ArrivalCandidate.Sequence != retired + 1UL) return false;
				high = retired + 1UL;
			}
			Book.ArrivalEventStreamId = GrowthArrivalEventStreamId;
			Book.ArrivalRulesVersion = 0; Book.ArrivalRateEpoch = 0L;
			Book.ArrivalRateEpochStartedTick = 0L; Book.ArrivalProcessedThroughTick = 0L;
			Book.ArrivalCadenceNextDueTick = 0L; Book.ArrivalRateCohort = 0;
			Book.ArrivalOrdinalRetiredThrough = retired; Book.ArrivalOrdinalHighWater = high;
			Book.ArrivalCadenceMigrationPending = true;
			Book.ArrivalCadenceResumePending = false;
			Book.ArrivalOpportunity = null;
			Book.ArrivalDebtRanges = new List<KingdomGrowthArrivalDebtRange>();
			return GrowthArrivalCadenceShape(Book);
		}
	}
}
