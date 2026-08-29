using System;
using System.Collections.Generic;
using System.IO;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		/// <summary>Advances one fixed-rate epoch in O(1). With no frozen head, stops at
		/// first due occurrence so caller can freeze its catalog payload before remaining time folds.</summary>
		public static bool TryAdvanceGrowthArrivalCadence(KingdomGrowthBook Book, long Now,
			long DesiredIntervalTicks, int DesiredCohort, int RulesVersion, out string Failure)
		{
			Failure = null;
			if (!GrowthArrivalCadenceShape(Book) || Book.ArrivalCadenceMigrationPending
				|| Book.ArrivalCadenceResumePending
				|| Now < Book.ArrivalProcessedThroughTick || DesiredIntervalTicks <= 0L
				|| DesiredCohort < 0 || RulesVersion <= 0)
				return FailCadence("arrival cadence input is malformed", out Failure);
			if (Book.WorkPaused || Book.OptionState != KingdomLifecycleOptionState.Enabled
				|| Book.HealthState != KingdomGrowthHealthState.Healthy)
				return true;
			if (Book.ArrivalRateEpoch == 0L)
			{
				if (!StartArrivalRateEpoch(Book, Now, DesiredIntervalTicks, DesiredCohort,
					RulesVersion, false)) return FailCadence("arrival cadence could not start", out Failure);
			}
			if (DesiredIntervalTicks != Book.ArrivalIntervalTicks
				|| DesiredCohort != Book.ArrivalRateCohort
				|| RulesVersion != Book.ArrivalRulesVersion)
			{
				long ignored;
				if (Book.ArrivalRateEpoch == long.MaxValue
					|| !TryAddTick(Now, DesiredIntervalTicks, out ignored))
					return FailCadence("arrival cadence rate epoch overflowed", out Failure);
				if (!FoldArrivalEpoch(Book, Now, false, out Failure)
					|| !StartArrivalRateEpoch(Book, Now, DesiredIntervalTicks, DesiredCohort,
						RulesVersion, true)) return false;
			}
			else if (!FoldArrivalEpoch(Book, Now, Book.ArrivalOpportunity == null
				&& ArrivalDebtCount(Book) == 0UL, out Failure)) return false;
			Book.NextArrivalTick = ArrivalClockFrontier(Book);
			return GrowthArrivalCadenceShape(Book)
				|| FailCadence("arrival cadence publication was noncanonical", out Failure);
		}

		public static bool TryBindHistoricalGrowthArrivalCadence(KingdomGrowthBook Book,
			long Now, long IntervalTicks, int Cohort, int RulesVersion, out string Failure)
		{
			Failure = null;
			if (Book == null || !Book.ArrivalCadenceMigrationPending || Now < 0L
				|| IntervalTicks <= 0L || Cohort < 0 || RulesVersion <= 0
				|| Book.ArrivalCandidate != null || Book.ArrivalOp != null)
				return FailCadence("historical arrival cadence cannot bind yet", out Failure);
			long next;
			if (!TryAddTick(Now, IntervalTicks, out next))
				return FailCadence("historical arrival cadence deadline overflowed", out Failure);
			Book.ArrivalCadenceMigrationPending = false;
			Book.ArrivalCadenceResumePending = false;
			Book.ArrivalProcessedThroughTick = Now;
			Book.ArrivalRateEpochStartedTick = Now;
			Book.ArrivalRateEpoch = 1L;
			Book.ArrivalRateCohort = Cohort;
			Book.ArrivalRulesVersion = RulesVersion;
			Book.ArrivalIntervalTicks = IntervalTicks;
			Book.ArrivalCadenceNextDueTick = next;
			Book.NextArrivalTick = Book.ArrivalCadenceNextDueTick;
			return GrowthArrivalCadenceShape(Book)
				|| FailCadence("historical arrival cadence binding was noncanonical", out Failure);
		}

		/// <summary>Starts a new absolute-rate epoch at the exact resume observation. Debt
		/// already earned before the pause remains ahead of the new underlying deadline.</summary>
		public static bool TryRestartGrowthArrivalCadenceAfterPause(KingdomGrowthBook Book,
			long Now, long IntervalTicks, int Cohort, int RulesVersion, out string Failure)
		{
			Failure = null;
			if (!GrowthArrivalCadenceShape(Book) || Book.ArrivalCadenceMigrationPending
				|| !Book.ArrivalCadenceResumePending
				|| Book.WorkPaused || Book.OptionState != KingdomLifecycleOptionState.Enabled
				|| Book.HealthState != KingdomGrowthHealthState.Healthy
				|| Now < Book.ArrivalProcessedThroughTick || IntervalTicks <= 0L
				|| Cohort < 0 || RulesVersion <= 0
				|| !StartArrivalRateEpoch(Book, Now, IntervalTicks, Cohort, RulesVersion, true))
				return FailCadence("arrival cadence resume epoch could not start", out Failure);
			Book.ArrivalCadenceResumePending = false;
			Book.NextArrivalTick = ArrivalClockFrontier(Book);
			return GrowthArrivalCadenceShape(Book)
				|| FailCadence("arrival cadence resume epoch was noncanonical", out Failure);
		}

		private static bool FoldArrivalEpoch(KingdomGrowthBook book, long now, bool stopAtFirst,
			out string failure)
		{
			failure = null;
			long boundary = stopAtFirst && book.ArrivalCadenceNextDueTick <= now
				? book.ArrivalCadenceNextDueTick : now;
			ulong count; long following; KernelFaultCode fault;
			if (!TickMath.TryCountFixedPeriodDue(boundary, book.ArrivalCadenceNextDueTick,
				book.ArrivalIntervalTicks, out count, out following, out fault))
				return FailCadence("arrival cadence arithmetic refused: " + fault, out failure);
			if (count > 0UL && !AppendArrivalDebt(book, count, book.ArrivalCadenceNextDueTick))
				return FailCadence("arrival cadence debt range or ordinal overflowed", out failure);
			book.ArrivalCadenceNextDueTick = following;
			book.ArrivalProcessedThroughTick = boundary;
			return true;
		}

		private static bool AppendArrivalDebt(KingdomGrowthBook book, ulong count, long firstDue)
		{
			if (count == 0UL || book.ArrivalOrdinalHighWater > ulong.MaxValue - count) return false;
			ulong first = book.ArrivalOrdinalHighWater + 1UL;
			List<KingdomGrowthArrivalDebtRange> ranges = book.ArrivalDebtRanges;
			KingdomGrowthArrivalDebtRange last = ranges.Count == 0 ? null : ranges[ranges.Count - 1];
			bool contiguousDue = last != null && last.Count <= (ulong)long.MaxValue
				&& (long)last.Count <= (long.MaxValue - last.FirstDueTick) / last.IntervalTicks
				&& last.FirstDueTick + (long)last.Count * last.IntervalTicks == firstDue;
			if (last != null && last.RulesVersionAtCreation == book.ArrivalRulesVersion
				&& last.RateEpoch == book.ArrivalRateEpoch && last.Cohort == book.ArrivalRateCohort
				&& last.IntervalTicks == book.ArrivalIntervalTicks
				&& last.FirstOrdinal + last.Count == first && contiguousDue)
			{
				if (last.Count > ulong.MaxValue - count) return false;
				last.Count += count;
			}
			else
			{
				if (ranges.Count >= MaxGrowthArrivalDebtRanges) return false;
				ranges.Add(new KingdomGrowthArrivalDebtRange
				{
					RulesVersionAtCreation = book.ArrivalRulesVersion,
					RateEpoch = book.ArrivalRateEpoch, Cohort = book.ArrivalRateCohort,
					FirstOrdinal = first, Count = count, FirstDueTick = firstDue,
					IntervalTicks = book.ArrivalIntervalTicks
				});
			}
			book.ArrivalOrdinalHighWater += count;
			return true;
		}

		private static bool StartArrivalRateEpoch(KingdomGrowthBook book, long tick,
			long interval, int cohort, int rulesVersion, bool increment)
		{
			if (increment && book.ArrivalRateEpoch == long.MaxValue) return false;
			long epoch = increment ? book.ArrivalRateEpoch + 1L : 1L;
			long next;
			if (epoch <= 0L || !TryAddTick(tick, interval, out next)) return false;
			book.ArrivalRateEpoch = epoch; book.ArrivalRateEpochStartedTick = tick;
			book.ArrivalRateCohort = cohort; book.ArrivalRulesVersion = rulesVersion;
			book.ArrivalIntervalTicks = interval; book.ArrivalCadenceNextDueTick = next;
			book.ArrivalProcessedThroughTick = tick;
			return true;
		}

		private static bool TryAddTick(long origin, long interval, out long result)
		{
			KernelFaultCode fault;
			return TickMath.TryAddInterval(origin, interval, out result, out fault);
		}

		private static bool FailCadence(string failure, out string target)
		{
			target = failure;
			return false;
		}
	}
}
