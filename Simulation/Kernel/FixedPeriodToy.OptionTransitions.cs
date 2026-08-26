using System;

namespace ThousandAndFirst.Simulation.Kernel
{
	internal static partial class FixedPeriodToyRules
	{
		/// <summary>
		/// The load-time rule: observe the setting, apply any detected change immediately, and
		/// emit nothing.
		/// <para>
		/// A change seen across a stopped process is not a backlog. The toy discards the old
		/// option's uncommitted overdue schedule and either disables or reanchors a full interval
		/// from load — deliberately representing "the player changed this while the game was not
		/// running," rather than inferring offline activity that never happened.
		/// </para>
		/// <para>
		/// <see cref="FixedPeriodToyState.ProcessedThroughTick"/> is untouched, so load stays an
		/// observation rather than a simulation step, and loading repeatedly changes nothing.
		/// </para>
		/// </summary>
		internal static ToyAdvanceResult ObserveOptionOnLoad(FixedPeriodToyState source, long now, bool configuredEnabled)
		{
			KernelFaultCode fault;
			if (!IsCanonical(source, out fault))
			{
				return Failed(source, fault);
			}
			if (now < 0L)
			{
				return Failed(source, KernelFaultCode.InvalidTick);
			}
			if (now < source.ProcessedThroughTick)
			{
				return Failed(source, KernelFaultCode.ClockRegression);
			}

			OptionLatchState nextLatch;
			OptionTransitionKind transition;
			KernelFaultCode latchFault;
			if (!OptionLatchRules.TryObserve(source.OptionLatch, configuredEnabled, now, out nextLatch, out transition, out latchFault))
			{
				return Failed(source, latchFault);
			}
			if (transition == OptionTransitionKind.None)
			{
				return new ToyAdvanceResult(source, OptionTransitionKind.None, KernelFaultCode.None);
			}

			bool scheduled = false;
			long nextDue = 0L;
			if (configuredEnabled)
			{
				KernelFaultCode scheduleFault;
				if (!TickMath.TryAddInterval(now, source.IntervalTicks, out nextDue, out scheduleFault))
				{
					return Failed(source, scheduleFault);
				}
				scheduled = true;
			}

			FixedPeriodToyState state = new FixedPeriodToyState(
				source.SchemaVersion,
				source.RulesVersion,
				source.SimulationSeed,
				source.SettlementId,
				source.ProcessedThroughTick,
				scheduled,
				nextDue,
				source.NextOrdinal,
				source.IntervalTicks,
				nextLatch,
				source.HasEmittedRange,
				source.EmittedRange);

			return new ToyAdvanceResult(state, transition, KernelFaultCode.None);
		}

		/// <summary>
		/// The running-game rule.
		/// <para>
		/// When the option changes at <paramref name="now"/>, the old enabled clock is first
		/// advanced only through <c>now - 1</c>, then the transition applies, then nothing is
		/// emitted at <c>now</c>. That ordering is what makes deadlines strictly before the
		/// transition survive a sparse wake, while disabling exactly <i>at</i> a deadline
		/// suppresses that pulse — and it must hold identically however the wakes are partitioned.
		/// </para>
		/// </summary>
		internal static ToyAdvanceResult AdvanceThrough(FixedPeriodToyState source, long now, bool configuredEnabled)
		{
			KernelFaultCode fault;
			if (!IsCanonical(source, out fault))
			{
				return Failed(source, fault);
			}
			if (now < 0L)
			{
				return Failed(source, KernelFaultCode.InvalidTick);
			}
			if (now < source.ProcessedThroughTick)
			{
				return Failed(source, KernelFaultCode.ClockRegression);
			}

			OptionLatchValue observed = configuredEnabled ? OptionLatchValue.Enabled : OptionLatchValue.Disabled;
			bool changing = source.OptionLatch.Value != observed;

			bool scheduled = source.ClockScheduled;
			long nextDue = source.NextDueTick;
			ulong nextOrdinal = source.NextOrdinal;
			bool hasRange = source.HasEmittedRange;
			ulong rangeCount = source.HasEmittedRange ? source.EmittedRange.Count : 0uL;

			if (changing)
			{
				// Fold the outgoing enabled clock up to, but not including, the transition tick.
				if (source.OptionLatch.Value == OptionLatchValue.Enabled && scheduled && now > 0L)
				{
					long boundary = now - 1L;
					if (boundary > source.ProcessedThroughTick)
					{
						if (!TryFold(boundary, source.IntervalTicks, ref scheduled, ref nextDue, ref nextOrdinal, ref hasRange, ref rangeCount, out fault))
						{
							return Failed(source, fault);
						}
					}
				}
			}
			else if (observed == OptionLatchValue.Enabled && scheduled)
			{
				if (!TryFold(now, source.IntervalTicks, ref scheduled, ref nextDue, ref nextOrdinal, ref hasRange, ref rangeCount, out fault))
				{
					return Failed(source, fault);
				}
			}

			OptionLatchState nextLatch;
			OptionTransitionKind transition;
			KernelFaultCode latchFault;
			if (!OptionLatchRules.TryObserve(source.OptionLatch, configuredEnabled, now, out nextLatch, out transition, out latchFault))
			{
				return Failed(source, latchFault);
			}

			if (changing)
			{
				if (configuredEnabled)
				{
					// Resuming schedules one full interval from now; disabled time is never replayed.
					KernelFaultCode scheduleFault;
					if (!TickMath.TryAddInterval(now, source.IntervalTicks, out nextDue, out scheduleFault))
					{
						return Failed(source, scheduleFault);
					}
					scheduled = true;
				}
				else
				{
					scheduled = false;
					nextDue = 0L;
				}
			}

			ToyPulseRange range = hasRange
				? new ToyPulseRange(source.RulesVersion, ToyPulseEventStreamId, ToyPulseEventKind, 0uL, rangeCount)
				: default(ToyPulseRange);

			FixedPeriodToyState state = new FixedPeriodToyState(
				source.SchemaVersion,
				source.RulesVersion,
				source.SimulationSeed,
				source.SettlementId,
				now,
				scheduled,
				nextDue,
				nextOrdinal,
				source.IntervalTicks,
				nextLatch,
				hasRange,
				range);

			return new ToyAdvanceResult(state, transition, KernelFaultCode.None);
		}

	}
}
