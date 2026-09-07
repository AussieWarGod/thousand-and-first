using System;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		internal static bool IsPristineMasterResumeLifecycle(KingdomLifecycleBook book)
		{ return PristineLifecycleBook(book); }

		// Only a detached, current-authority proposal is admitted here. Health is evidence;
		// the master option cannot claim that an unobserved or unhealthy settlement is healthy.
		internal static bool PrepareMasterGrowthResume(KingdomGrowthBook book, long disabledAt,
			long now, bool growthEnabled, bool scarcityEnabled, long interval, int cohort,
			int rulesVersion, out string failure)
		{
			failure = null;
			if (disabledAt < 0L || now < disabledAt || interval <= 0L || cohort < 0
				|| rulesVersion <= 0 || !CanOwnGrowthAuthority(book, book?.SettlementId)
				|| !MasterGrowthClockBounded(book, now))
				return FailCadence("master growth observation regressed or is malformed", out failure);
			bool active = growthEnabled && book.HealthState == KingdomGrowthHealthState.Healthy;
			// The ongoing local pause and the master interval overlap, not add. Completed
			// local pauses remain represented only by the already-earned cumulative count.
			long pauseStart = book.WorkPaused ? Math.Min(disabledAt, book.WorkPauseStartedTick) : disabledAt;
			long paused = book.WorkPausedTicks;
			if (active && !CheckedAdd(paused, now - pauseStart, out paused))
				return FailCadence("master growth paused duration overflowed", out failure);
			book.OptionState = growthEnabled ? KingdomLifecycleOptionState.Enabled : KingdomLifecycleOptionState.Disabled;
			book.OptionTick = now;
			book.ScarcityOptionState = scarcityEnabled ? KingdomLifecycleOptionState.Enabled : KingdomLifecycleOptionState.Disabled;
			book.ScarcityOptionTick = now;
			book.WorkPaused = !active; book.WorkPauseStartedTick = active ? 0L : pauseStart;
			book.WorkPausedTicks = paused;
			if (book.HeartbeatOp == null) book.LastHeartbeatTick = now;
			if (book.FetchOp == null) book.LastFetchTick = now;
			if (book.MillOp == null) book.LastMillTick = now;
			if (book.DeliveryOp == null) book.LastDeliveryTick = now;
			if (book.DepartureOp == null) book.LastDepartureTick = now;
			bool openArrival = book.ArrivalOp != null || book.ArrivalCandidate != null;
			if (book.ArrivalCadenceMigrationPending)
			{
				// Historical open work retains its original clock lease. The native cadence
				// owner binds a complete new interval only after that work retires.
				if (!openArrival)
				{
					book.ArrivalIntervalTicks = interval;
					long next = 0L;
					if (active && !TryAddTick(now, interval, out next))
						return FailCadence("master historical arrival deadline overflowed", out failure);
					book.NextArrivalTick = next;
				}
			}
			else
			{
				book.ArrivalCadenceResumePending = active;
				if (active && !openArrival && !TryRestartGrowthArrivalCadenceAfterPause(book,
					now, interval, cohort, rulesVersion, out failure)) return false;
			}
			return CanOwnGrowthAuthority(book, book.SettlementId)
				|| FailCadence("master growth proposal would invalidate retained authority", out failure);
		}

		private static bool MasterGrowthClockBounded(KingdomGrowthBook book, long now)
		{
			if (book.OptionTick > now || book.HealthTick > now || book.ScarcityOptionTick > now
				|| book.MigrationTick > now || book.WorkPauseStartedTick > now || book.EffectiveWorkTick > now
				|| book.LastHeartbeatTick > now || book.LastFetchTick > now || book.LastMillTick > now
				|| book.LastSubsidenceTick > now || book.LastDeliveryTick > now || book.LastDepartureTick > now
				|| book.ArrivalProcessedThroughTick > now || book.ArrivalRateEpochStartedTick > now
				|| book.ArrivalCandidate?.UpdatedTick > now || book.FirstGuestTerminal?.TerminalTick > now) return false;
			KingdomGrowthOperation[] direct = { book.HeartbeatOp, book.ArrivalOp, book.DepartureOp,
				book.DeliveryOp, book.FetchOp, book.MillOp };
			foreach (KingdomGrowthOperation operation in direct) if (operation?.UpdatedTick > now) return false;
			foreach (KingdomGrowthFieldSlot field in book.FieldOps)
				if (field.ClockTick > now || field.Operation?.UpdatedTick > now) return false;
			return true;
		}

		// No health, effective field clock, child reference, receipt, lease, ordinal, or
		// subsidence checkpoint belongs to this publication's scalar write set.
		internal static void CopyMasterGrowthResumeScalars(KingdomGrowthBook from, KingdomGrowthBook to)
		{
			to.OptionState = from.OptionState; to.OptionTick = from.OptionTick;
			to.ScarcityOptionState = from.ScarcityOptionState; to.ScarcityOptionTick = from.ScarcityOptionTick;
			to.WorkPaused = from.WorkPaused; to.WorkPauseStartedTick = from.WorkPauseStartedTick;
			to.WorkPausedTicks = from.WorkPausedTicks;
			to.LastHeartbeatTick = from.LastHeartbeatTick; to.LastFetchTick = from.LastFetchTick;
			to.LastMillTick = from.LastMillTick; to.LastDeliveryTick = from.LastDeliveryTick;
			to.LastDepartureTick = from.LastDepartureTick;
			to.NextArrivalTick = from.NextArrivalTick; to.ArrivalIntervalTicks = from.ArrivalIntervalTicks;
			to.ArrivalRulesVersion = from.ArrivalRulesVersion; to.ArrivalRateEpoch = from.ArrivalRateEpoch;
			to.ArrivalRateEpochStartedTick = from.ArrivalRateEpochStartedTick;
			to.ArrivalProcessedThroughTick = from.ArrivalProcessedThroughTick;
			to.ArrivalCadenceNextDueTick = from.ArrivalCadenceNextDueTick;
			to.ArrivalRateCohort = from.ArrivalRateCohort;
			to.ArrivalCadenceResumePending = from.ArrivalCadenceResumePending;
		}
	}
}
