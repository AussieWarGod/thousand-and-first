namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{

		public const int FoundingCostDrams = 8;

		public const int FetchDramsPerSettler = 2;

		public static readonly string[] Origins = new string[6] { "the salt marshes", "the desert canyons", "the hills", "the flower fields", "the rust wells", "the banana grove" };

		public const long TicksPerDay = 1200L;

		// --- Where MaxUpkeepDaysCharged lived ---------------------------------------------------
		//
		// It was 3, and it did two unrelated jobs under one name: it CAPPED the elapsed days any
		// clock would charge (forgiving the rest by re-anchoring the checkpoint to now), and it
		// was borrowed as a QUANTITY - "three days of upkeep" - by the crop, upgrade and manifest
		// reserves. Addendum 8 clause 1 retires the first job: the settlement lives whether the
		// founder is there or not, so elapsed time is charged in full against what the
		// settlement's own supply carried through it, and what bounds the loss is subsidence
		// toward the supported equilibrium, never forgiveness. The second job survives under its
		// own honest name, <see cref="ReserveDays"/>.
		//
		// RELEASE-ERA HARNESS, READ THIS SEAM (CLOCK-REWORK-CHANGE-MAP.md 4.1). No save has ever
		// been written by a shipped build, so this wave ships NO migration machinery (Addendum 9)
		// and simply refuses pre-rework layouts at the version gate in KingdomSystem.Read. When
		// the first release makes migration real, the job here is: on reading a layout older than
		// the uncapping, re-anchor every clock stamp to The.Game.TimeTicks exactly once -
		// LastHeartbeatTick, LastFetchTick, LastVisitTick, LastDissentTick, NextArrivalTick, and
		// the per-object stamps KingdomRefineWorked, KingdomStrikeWorked, KingdomRepairWorked,
		// r_KingdomClearance.LastWorkedTick, r_KingdomPowerWork.LastResolvedTick,
		// r_KingdomPowerStore.LastResolvedTick, r_TAF_RoadsWalked. Without that, the first load
		// resolves a season of real elapsed against a stamp the old cap left hundreds of days
		// stale and bills it in one pass, which is the exact unchosen debt clause 4 forbids.
		//
		// DO NOT re-anchor r_KingdomNotice.PostedTick, KingdomCarryHaul.PlantedTick, or
		// r_KingdomNotice.TakenTick. Those three are not clocks: each is fed to
		// SemanticEventKey.TryCreate as a draw ordinal, and moving one silently re-rolls every
		// determinism question already answered against it.

		/// <summary>
		/// Days of upkeep a settlement keeps in hand before it spends water on anything
		/// discretionary - a planting, an upgrade, a manifest's outbound load. A QUANTITY, and
		/// only ever that: it says how deep the cushion is, never how much elapsed time a clock
		/// is willing to look at. It inherits the retired cap's value because three days of
		/// drinking was always the size of the cushion the reserves wanted; it no longer inherits
		/// its meaning.
		/// </summary>
		public const int ReserveDays = 3;

		// --- Where LegacyAbsenceCap lived -------------------------------------------------
		//
		// The forgiveness cap's last stand. It survived P1 as a named holding pen for the
		// counters that pass had not reached - road traffic, power works and stores, the three
		// KingdomMaterials workers, mending, and dissent - together with the HeartbeatDays and
		// HeartbeatCheckpoint pair that read it. Every one of those is now on ElapsedDays and
		// AdvanceCheckpoint, so the constant, the pair, and the pen are all gone.
		//
		// The list mattered for one reason and it is worth keeping: dissent could not be
		// uncapped alone, because secession fired on the pass dissent reached its threshold, and
		// uncapping accrual before the arrestable window existed would have made an absence lose
		// a city FASTER than presence does - clause 3 exactly inverted. P3 landed secession's
		// named window first and uncapped dissent behind it, which is the order every one of
		// these swaps was held to: the labour gate, or the window, before the clock.
		//
		// Nothing in this file caps elapsed time any more. A counter that must not run in
		// absence says so with a LABOUR term (ActivityDays, LabouredTicks, a crew gate) or with
		// a window that only attended passes spend (KingdomBrinkRules) - never by refusing to
		// look at the calendar.

		/// <summary>Daily draw per settler, per hundred, by stage. A camp lives thin; a city
		/// drinks like a city. Prosperity-scaled COST is the loved half of the pattern - what
		/// players resent is prosperity-scaled THREAT, which is a different rule and is keyed off
		/// provocation elsewhere in this file.</summary>
		public static readonly int[] StageUpkeepPercent = new int[5] { 100, 120, 150, 180, 220 };

		/// <summary>
		/// Water the settlement drinks in a day. Scales with population AND with what the
		/// settlement has become: a City asks more of each settler than a Camp does, so growing
		/// is a decision with a bill attached rather than a free ratchet.
		/// </summary>
		public static int UpkeepDrams(int Population, GrowthStage Stage)
		{
			if (Population <= 0)
			{
				return 0;
			}
			int percent = StageUpkeepPercent[(int)Stage];
			return Population * percent / 100;
		}

		/// <summary>Camp-rate upkeep, for callers that have no stage to hand.</summary>
		public static int UpkeepDrams(int Population)
		{
			return UpkeepDrams(Population, GrowthStage.Camp);
		}

		// --- The clock substrate -----------------------------------------------------------------
		//
		// Two primitives, and everything with a clock in it should end up reading them.
		// ElapsedDays says how much world time went by; AdvanceCheckpoint spends exactly the
		// whole days that were just charged and keeps the remainder, so a founder cannot buy a
		// free day by stepping in and out of the zone. Neither of them forgives anything, and
		// neither of them is a rate on its own: ActivityDays and LabouredTicks are how a caller
		// turns elapsed time into work done, which is Addendum 8 clause 2 - time x labour x
		// infrastructure, never time alone.
		//
		// The arithmetic is the kernel's (Simulation/Kernel/TickMath), which is checked,
		// overflow-safe, and pinned by a BigInteger oracle. Four subsystems hand-rolled it; this
		// is the first production caller.

		/// <summary>
		/// Whole days of world time in a stretch of elapsed ticks. Uncapped: a season away is a
		/// season, and what bounds its cost is what the settlement's own supply carried through
		/// it, never a forgiveness ceiling.
		/// <para>
		/// Fails closed at zero on anything that is not a real forward stretch - a negative
		/// elapsed, or a value so large the kernel's checked arithmetic refuses it. Zero is the
		/// safe answer in both cases because zero days mints no debt.
		/// </para>
		/// </summary>
		/// <param name="ElapsedTicks">Ticks since the stamp being resolved.</param>
		/// <returns>Whole days, 0 or more.</returns>
		public static int ElapsedDays(long ElapsedTicks)
		{
			if (ElapsedTicks < TicksPerDay)
			{
				return 0;
			}
			if (!Simulation.Kernel.TickMath.TryCountFixedPeriodDue(ElapsedTicks, TicksPerDay, TicksPerDay, out var count, out var _, out var _))
			{
				return 0;
			}
			return (count > int.MaxValue) ? int.MaxValue : (int)count;
		}

		/// <summary>
		/// Moves a "last resolved" stamp forward by exactly the whole days a caller just charged,
		/// keeping the part-day remainder so it counts toward the next one.
		/// <para>
		/// The retired <c>HeartbeatCheckpoint</c> re-anchored to <paramref name="CurrentTick"/>
		/// once the elapsed passed the absence cap, which is what "forgiveness" physically was.
		/// Nothing is re-anchored here. A stamp at or ahead of now is treated as a fresh start
		/// rather than repaired, matching the shipped shape: a clock that ran backwards is a
		/// corrupt reading, and re-billing from it would be worse than beginning again.
		/// </para>
		/// </summary>
		public static long AdvanceCheckpoint(long PreviousTick, long CurrentTick)
		{
			if (PreviousTick <= 0 || CurrentTick <= PreviousTick)
			{
				return CurrentTick;
			}
			int days = ElapsedDays(CurrentTick - PreviousTick);
			if (days <= 0)
			{
				return PreviousTick;
			}
			return PreviousTick + days * TicksPerDay;
		}

	}
}
