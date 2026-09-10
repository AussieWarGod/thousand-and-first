using System;
using HarmonyLib;
using XRL;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Observes the actual master wake before/after publication; never substitutes a
	/// decision, suppresses a call, or writes the schedule being measured.</summary>
	internal sealed class KingdomScenarioPauseWitness
	{
		internal static KingdomScenarioPauseWitness Current;
		internal readonly XRLGame Game;
		internal readonly KingdomSystem System;
		internal readonly KingdomGrowthBook Growth;
		internal readonly KingdomCityBook City;
		internal string Fault;
		internal bool Armed = true;
		internal int ResumeApplications;
		internal long DisabledTick = -1, ResumeTick = -1, ObservedPaused, ObservedArrival;
		internal long LocalStart, PriorPaused, ArrivalInterval;
		internal KingdomScenarioPauseOracle.Expected Expected;
		private readonly long InitialToken;
		private bool Pending;
		private int[] ClockKinds, ClockOrdinals;
		private int Works;
		private long HealthTick, SubsidenceTick, OrdinalHigh;

		internal KingdomScenarioPauseWitness(KingdomSystem system)
		{
			Game = The.Game; System = system; City = system?.City; Growth = system?.LifecycleBook?.Growth;
			Require(Current == null && Game != null && system != null && City != null && Growth != null,
				"pause observer already retained or lacks exact city/growth");
			Require(system.MasterOption == KingdomMasterLatchValue.Enabled
				&& system.MasterResumeToken == system.MasterAppliedResumeToken, "master is not steadily enabled");
			InitialToken = system.MasterResumeToken;
			ClosedHealthy(); Current = this;
		}

		private static void Require(bool value, string reason)
		{ if (!value) throw new InvalidOperationException("taf-pause-oracle-refused: " + reason); }

		private void Owner()
		{
			Require(Armed && ReferenceEquals(The.Game, Game) && ReferenceEquals(Game.GetSystem<KingdomSystem>(), System)
				&& ReferenceEquals(System.City, City) && ReferenceEquals(System.LifecycleBook?.Growth, Growth),
				"pause observation owner changed");
			Require(Fault == null, Fault);
		}

		private void ClosedHealthy()
		{
			Require(KingdomLifecycleRules.CanOwnAuthority(System.LifecycleBook)
				&& KingdomLifecycleRules.CanOwnGrowthAuthority(Growth, System.LifecycleBook.SettlementId)
				&& !Growth.ArrivalCadenceMigrationPending && Growth.ArrivalRateEpoch > 0
				&& Growth.HealthState == KingdomGrowthHealthState.Healthy && Growth.ArrivalIntervalTicks > 0,
				"requires healthy modern growth authority, never a fabricated health observation");
			Require(Growth.HeartbeatOp == null && Growth.ArrivalOp == null && Growth.ArrivalCandidate == null
				&& Growth.DepartureOp == null && Growth.DeliveryOp == null && Growth.FetchOp == null && Growth.MillOp == null
				&& Growth.ArrivalOpportunity == null && Growth.ArrivalDebtRanges.Count == 0
				&& System.LifecycleBook.PlainGuest == null && System.LifecycleBook.NotableGuest == null,
				"open work/debt owns a schedule lease outside this focused oracle");
			Require(System.NativeTravelSemanticPauseReady() && Growth.ArrivalOrdinalHighWater <= long.MaxValue,
				"unfinished or unpublished semantic pass, or ordinal outside fixture bounds");
			foreach (var field in Growth.FieldOps) Require(field.Operation == null, "field operation remains open");
		}

		private void Before(long tick)
		{
			Owner(); Require(tick == Game.TimeTicks && !Pending, "nested or wrong-tick master call");
			if (System.MasterOption != KingdomMasterLatchValue.Disabled || !KingdomMaster.ConfiguredEnabled) return;
			Require(ResumeApplications == 0 && DisabledTick == System.MasterOptionTick
				&& Growth.WorkPaused && Growth.WorkPauseStartedTick <= DisabledTick,
				"resume lacks an observed overlapping local/master pause");
			ClosedHealthy();
			LocalStart = Growth.WorkPauseStartedTick; PriorPaused = Growth.WorkPausedTicks;
			ArrivalInterval = Growth.ArrivalIntervalTicks;
			Require(KingdomGrowth.Enabled && KingdomScenarioPauseOracle.TryResume(DisabledTick, tick,
				Growth.WorkPaused, Growth.WorkPauseStartedTick, Growth.WorkPausedTicks, Growth.EffectiveWorkTick,
				Growth.ArrivalIntervalTicks, Growth.ArrivalRateEpoch, InitialToken, KingdomRules.TicksPerDay, out Expected),
				"independent resume arithmetic refused");
			Require(City.TryReadExact(out _, out _), "city shape is not exact");
			ClockKinds = City.ClockKinds.ToArray(); ClockOrdinals = City.ClockOrdinals.ToArray();
			Works = City.WorkNextTicks.Count; HealthTick = Growth.HealthTick;
			SubsidenceTick = Growth.LastSubsidenceTick; OrdinalHigh = (long)Growth.ArrivalOrdinalHighWater;
			ResumeTick = tick; Pending = true;
		}

		private void After(long tick, bool allowed)
		{
			Owner(); Require(tick == Game.TimeTicks, "master returned at a different tick");
			if (System.MasterOption == KingdomMasterLatchValue.Disabled)
			{
				Require(!allowed && System.MasterResumeToken == InitialToken
					&& System.MasterAppliedResumeToken == InitialToken, "disabled master spent a resume token");
				if (DisabledTick < 0) DisabledTick = System.MasterOptionTick;
				Require(DisabledTick == System.MasterOptionTick, "disabled interval was restarted");
			}
			if (!Pending) return;
			Pending = false;
			Require(!allowed && System.MasterOption == KingdomMasterLatchValue.Enabled
				&& System.MasterOptionTick == ResumeTick && System.MasterResumeToken == Expected.ResumeToken
				&& System.MasterAppliedResumeToken == Expected.ResumeToken, "resume publication/token differs");
			Require(!Growth.WorkPaused && Growth.WorkPauseStartedTick == 0
				&& Growth.WorkPausedTicks == Expected.PausedTicks && Growth.EffectiveWorkTick == Expected.EffectiveTick
				&& Growth.NextArrivalTick == Expected.ArrivalDeadline && System.NextArrivalTick == Expected.ArrivalDeadline
				&& Growth.ArrivalCadenceNextDueTick == Expected.ArrivalDeadline
				&& Growth.ArrivalRateEpoch == Expected.ArrivalEpoch && Growth.ArrivalRateEpochStartedTick == ResumeTick
				&& Growth.ArrivalProcessedThroughTick == ResumeTick && !Growth.ArrivalCadenceResumePending
				&& Growth.HealthTick == HealthTick && Growth.LastSubsidenceTick == SubsidenceTick
				&& Growth.ArrivalOrdinalHighWater == (ulong)OrdinalHigh,
				"actual growth resume differs from independent interval-union/deadline oracle");
			Require(City.ProcessedThroughTick == ResumeTick && System.LastWaterWorkTick == ResumeTick
				&& System.LastSemanticTick == ResumeTick && City.WorkNextTicks.Count == Works
				&& City.WorkRanThroughTicks.Count == Works && City.ClockKinds.Count == ClockKinds.Length,
				"settlement resume clocks or row counts differ");
			for (int i = 0; i < Works; i++) Require(City.WorkRanThroughTicks[i] == ResumeTick
				&& City.WorkNextTicks[i] == Expected.DayDeadline, "work schedule is not one full interval after resume");
			for (int i = 0; i < ClockKinds.Length; i++) Require(City.ClockKinds[i] == ClockKinds[i]
				&& City.ClockOrdinals[i] == ClockOrdinals[i] && City.ClockNextDueTicks[i] == Expected.DayDeadline,
				"clock schedule reset spent an ordinal or double-shifted its deadline");
			ObservedPaused = Growth.WorkPausedTicks; ObservedArrival = Growth.NextArrivalTick;
			ResumeApplications++;
			KingdomScenarioTravelSchedule.Resume(this);
		}

		internal void Check()
		{
			Owner(); Require(!Pending && ResumeApplications == 1 && ResumeTick > DisabledTick
				&& System.MasterResumeToken == Expected.ResumeToken && System.MasterAppliedResumeToken == Expected.ResumeToken
				&& Growth.WorkPausedTicks == Expected.PausedTicks, "resume was missing, repeated, or pause counted again");
		}

		internal static void Observe(KingdomSystem system, long tick, bool after, bool allowed)
		{
			var current = Current;
			if (current == null || !current.Armed || !ReferenceEquals(system, current.System)) return;
			try { if (after) current.After(tick, allowed); else current.Before(tick); }
			catch (Exception error) { current.Fault = error.Message; }
		}
	}

	[HarmonyPatch(typeof(KingdomMaster), "ObserveAutomaticWake", new Type[] { typeof(KingdomSystem), typeof(long) })]
	internal static class KingdomScenarioPauseMasterObserver
	{
		internal static void Prefix(KingdomSystem system, long now)
		{ KingdomScenarioPauseWitness.Observe(system, now, false, false); }
		internal static void Postfix(KingdomSystem system, long now, bool __result)
		{ KingdomScenarioPauseWitness.Observe(system, now, true, __result); }
	}
}
