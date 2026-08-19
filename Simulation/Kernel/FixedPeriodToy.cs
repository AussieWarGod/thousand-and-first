using System;

namespace ThousandAndFirst.Simulation.Kernel
{
	/// <summary>
	/// One contiguous span of emitted pulse identities, stored as a range rather than a list.
	/// <para>
	/// This is historical identity proof, not a due-job queue: current scheduling lives in
	/// <see cref="FixedPeriodToyState.NextDueTick"/>. Storing a span is also what keeps an
	/// enormous but mathematically valid due count representable without enumerating anything.
	/// </para>
	/// </summary>
	internal readonly struct ToyPulseRange
	{
		internal readonly int RulesVersionAtCreation;

		internal readonly string EventStreamId;

		internal readonly uint EventKindCode;

		internal readonly ulong FirstOrdinal;

		internal readonly ulong Count;

		internal ToyPulseRange(
			int rulesVersionAtCreation,
			string eventStreamId,
			uint eventKindCode,
			ulong firstOrdinal,
			ulong count)
		{
			RulesVersionAtCreation = rulesVersionAtCreation;
			EventStreamId = eventStreamId;
			EventKindCode = eventKindCode;
			FirstOrdinal = firstOrdinal;
			Count = count;
		}
	}

	/// <summary>
	/// The whole persisted state of one contentless fixed-period clock.
	/// <para>
	/// Immutable by construction: every rule copies, validates, computes into locals, and publishes
	/// one new instance. Nothing is ever partially incremented, so a fault leaves the caller's
	/// state byte-identical.
	/// </para>
	/// </summary>
	internal sealed class FixedPeriodToyState
	{
		internal readonly int SchemaVersion;

		internal readonly int RulesVersion;

		internal readonly KernelSeed128 SimulationSeed;

		internal readonly string SettlementId;

		internal readonly long ProcessedThroughTick;

		internal readonly bool ClockScheduled;

		internal readonly long NextDueTick;

		internal readonly ulong NextOrdinal;

		internal readonly long IntervalTicks;

		internal readonly OptionLatchState OptionLatch;

		internal readonly bool HasEmittedRange;

		internal readonly ToyPulseRange EmittedRange;

		internal FixedPeriodToyState(
			int schemaVersion,
			int rulesVersion,
			KernelSeed128 simulationSeed,
			string settlementId,
			long processedThroughTick,
			bool clockScheduled,
			long nextDueTick,
			ulong nextOrdinal,
			long intervalTicks,
			OptionLatchState optionLatch,
			bool hasEmittedRange,
			ToyPulseRange emittedRange)
		{
			SchemaVersion = schemaVersion;
			RulesVersion = rulesVersion;
			SimulationSeed = simulationSeed;
			SettlementId = settlementId;
			ProcessedThroughTick = processedThroughTick;
			ClockScheduled = clockScheduled;
			NextDueTick = nextDueTick;
			NextOrdinal = nextOrdinal;
			IntervalTicks = intervalTicks;
			OptionLatch = optionLatch;
			HasEmittedRange = hasEmittedRange;
			EmittedRange = emittedRange;
		}
	}

	internal readonly struct ToyAdvanceResult
	{
		internal readonly FixedPeriodToyState State;

		internal readonly OptionTransitionKind OptionTransition;

		internal readonly KernelFaultCode Fault;

		internal ToyAdvanceResult(FixedPeriodToyState state, OptionTransitionKind optionTransition, KernelFaultCode fault)
		{
			State = state;
			OptionTransition = optionTransition;
			Fault = fault;
		}

		internal bool Succeeded
		{
			get { return Fault == KernelFaultCode.None && State != null; }
		}
	}

	/// <summary>
	/// A deliberately contentless fixed-period clock, built to prove the kernel algebra and
	/// nothing else.
	/// <para>
	/// <c>ToyPulse</c> has no water, item, citizen, standing, notification, or player-facing
	/// meaning, and must never be wired to live code. It is deleted or replaced once later pure
	/// modules prove the same algebra against real content.
	/// </para>
	/// </summary>
	internal static class FixedPeriodToyRules
	{
		internal const int ToySchemaVersion = 1;

		// Reserved non-production diagnostic code; a later module registry must not reuse either
		// this kind or the stream ID below.
		internal const uint ToyPulseEventKind = 0xFFFF0001U;

		internal const string ToyPulseEventStreamId = "taf:stream:kernel-toy:v1";

		private static readonly byte[] StateTag = { 0x54, 0x41, 0x46, 0x4B, 0x53, 0x54, 0x30, 0x31 }; // "TAFKST01"

		private const byte TerminalMarker = 0x7E;

		internal static ToyAdvanceResult Create(
			KernelSeed128 seed,
			int rulesVersion,
			string settlementId,
			long now,
			long intervalTicks,
			bool configuredEnabled)
		{
			if (now < 0L)
			{
				return Failed(null, KernelFaultCode.InvalidTick);
			}
			if (intervalTicks <= 0L)
			{
				return Failed(null, KernelFaultCode.InvalidInterval);
			}
			if (rulesVersion < 1 || !KernelSemanticId.IsValid(settlementId))
			{
				return Failed(null, KernelFaultCode.InvalidToyState);
			}

			OptionLatchState latch = new OptionLatchState(
				configuredEnabled ? OptionLatchValue.Enabled : OptionLatchValue.Disabled,
				now);

			bool scheduled = false;
			long nextDue = 0L;
			if (configuredEnabled)
			{
				KernelFaultCode scheduleFault;
				if (!TickMath.TryAddInterval(now, intervalTicks, out nextDue, out scheduleFault))
				{
					return Failed(null, scheduleFault);
				}
				scheduled = true;
			}

			FixedPeriodToyState state = new FixedPeriodToyState(
				ToySchemaVersion,
				rulesVersion,
				seed,
				settlementId,
				now,
				scheduled,
				nextDue,
				0uL,
				intervalTicks,
				latch,
				false,
				default(ToyPulseRange));

			return new ToyAdvanceResult(
				state,
				configuredEnabled ? OptionTransitionKind.InitializedEnabled : OptionTransitionKind.InitializedDisabled,
				KernelFaultCode.None);
		}

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

		/// <summary>
		/// Expands one ordinal inside the emitted range into its stable event key. Ordinals
		/// outside the range have no identity, because nothing ever emitted them.
		/// </summary>
		internal static bool TryGetEventKey(
			FixedPeriodToyState state,
			ulong ordinal,
			out SemanticEventKey key,
			out KernelFaultCode fault)
		{
			key = default(SemanticEventKey);
			if (!IsCanonical(state, out fault))
			{
				return false;
			}
			if (!state.HasEmittedRange)
			{
				fault = KernelFaultCode.InvalidEventKey;
				return false;
			}
			ulong first = state.EmittedRange.FirstOrdinal;
			ulong count = state.EmittedRange.Count;
			if (ordinal < first || ordinal - first >= count)
			{
				fault = KernelFaultCode.InvalidEventKey;
				return false;
			}
			return SemanticEventKey.TryCreate(
				state.EmittedRange.RulesVersionAtCreation,
				state.SettlementId,
				state.EmittedRange.EventStreamId,
				state.EmittedRange.EventKindCode,
				ordinal,
				out key,
				out fault);
		}

		/// <summary>
		/// The canonical diagnostic encoding. Absent optional values are written in exactly one
		/// canonical form, so two states that mean the same thing always produce the same bytes.
		/// </summary>
		internal static bool TryEncodeCanonical(FixedPeriodToyState state, out byte[] bytes, out KernelFaultCode fault)
		{
			bytes = null;
			if (!IsCanonical(state, out fault))
			{
				return false;
			}
			using (CanonicalByteWriter writer = new CanonicalByteWriter())
			{
				writer.WriteRawBytes(StateTag);
				writer.WriteInt32(state.SchemaVersion);
				writer.WriteInt32(state.RulesVersion);
				writer.WriteUInt64(state.SimulationSeed.High);
				writer.WriteUInt64(state.SimulationSeed.Low);
				writer.WriteRequiredUtf8(state.SettlementId);
				writer.WriteRequiredUtf8(ToyPulseEventStreamId);
				writer.WriteUInt32(ToyPulseEventKind);
				writer.WriteInt64(state.ProcessedThroughTick);
				writer.WriteBool(state.ClockScheduled);
				writer.WriteInt64(state.ClockScheduled ? state.NextDueTick : 0L);
				writer.WriteUInt64(state.NextOrdinal);
				writer.WriteInt64(state.IntervalTicks);
				writer.WriteByte((byte)state.OptionLatch.Value);
				writer.WriteInt64(state.OptionLatch.ChangedAtTick);
				writer.WriteBool(state.HasEmittedRange);
				if (state.HasEmittedRange)
				{
					writer.WriteInt32(state.EmittedRange.RulesVersionAtCreation);
					writer.WriteRequiredUtf8(state.EmittedRange.EventStreamId);
					writer.WriteUInt32(state.EmittedRange.EventKindCode);
					writer.WriteUInt64(state.EmittedRange.FirstOrdinal);
					writer.WriteUInt64(state.EmittedRange.Count);
				}
				writer.WriteByte(TerminalMarker);
				bytes = writer.ToArray();
			}
			fault = KernelFaultCode.None;
			return true;
		}

		/// <summary>
		/// Folds every occurrence due through <paramref name="boundary"/> into the range in one
		/// step. Never loops per occurrence: the count may legitimately be enormous, and the whole
		/// point of a range is that it stays representable when it is.
		/// </summary>
		private static bool TryFold(
			long boundary,
			long intervalTicks,
			ref bool scheduled,
			ref long nextDue,
			ref ulong nextOrdinal,
			ref bool hasRange,
			ref ulong rangeCount,
			out KernelFaultCode fault)
		{
			if (!scheduled || boundary < nextDue)
			{
				fault = KernelFaultCode.None;
				return true;
			}

			ulong dueCount;
			long following;
			if (!TickMath.TryCountFixedPeriodDue(boundary, nextDue, intervalTicks, out dueCount, out following, out fault))
			{
				return false;
			}
			if (dueCount == 0uL)
			{
				fault = KernelFaultCode.None;
				return true;
			}
			// Ordinal space and range length are both checked before publication; a lane that
			// exhausts its ordinals fails closed rather than wrapping onto identities already used.
			if (nextOrdinal > ulong.MaxValue - dueCount || rangeCount > ulong.MaxValue - dueCount)
			{
				fault = KernelFaultCode.ArithmeticOverflow;
				return false;
			}

			nextOrdinal += dueCount;
			rangeCount += dueCount;
			hasRange = true;
			nextDue = following;
			fault = KernelFaultCode.None;
			return true;
		}

		/// <summary>
		/// Full structural validation. Every rule runs this on entry, because a caller can hand in
		/// any object and a malformed state must fail rather than propagate.
		/// <para>
		/// Private. An earlier revision left this internal and justified it by claiming the
		/// <c>InvalidToyState</c> / <c>InvalidOptionLatch</c> split was not observable through the
		/// named entry points. That was simply false — every one of them validates the state first
		/// and returns exactly this fault — so the justification did not survive being checked, and
		/// the tests now go through the entry points a caller actually has.
		/// </para>
		/// </summary>
		private static bool IsCanonical(FixedPeriodToyState state, out KernelFaultCode fault)
		{
			if (state == null)
			{
				fault = KernelFaultCode.InvalidToyState;
				return false;
			}
			if (state.SchemaVersion != ToySchemaVersion
				|| state.RulesVersion < 1
				|| !KernelSemanticId.IsValid(state.SettlementId)
				|| state.ProcessedThroughTick < 0L
				|| state.NextDueTick < 0L
				|| state.IntervalTicks <= 0L)
			{
				fault = KernelFaultCode.InvalidToyState;
				return false;
			}
			// Range invariants run before the latch because the frozen order puts any bad non-latch
			// state ahead of a bad latch, and nothing here consults the latch.
			if (!state.HasEmittedRange)
			{
				if (state.NextOrdinal != 0uL
					|| state.EmittedRange.RulesVersionAtCreation != 0
					|| state.EmittedRange.EventStreamId != null
					|| state.EmittedRange.EventKindCode != 0u
					|| state.EmittedRange.FirstOrdinal != 0uL
					|| state.EmittedRange.Count != 0uL)
				{
					fault = KernelFaultCode.InvalidToyState;
					return false;
				}
			}
			else
			{
				if (state.EmittedRange.Count == 0uL
					|| state.EmittedRange.RulesVersionAtCreation != state.RulesVersion
					|| !string.Equals(state.EmittedRange.EventStreamId, ToyPulseEventStreamId, StringComparison.Ordinal)
					|| state.EmittedRange.EventKindCode != ToyPulseEventKind
					|| state.EmittedRange.FirstOrdinal != 0uL)
				{
					fault = KernelFaultCode.InvalidToyState;
					return false;
				}
				if (state.EmittedRange.FirstOrdinal > ulong.MaxValue - state.EmittedRange.Count)
				{
					fault = KernelFaultCode.InvalidToyState;
					return false;
				}
				if (state.EmittedRange.FirstOrdinal + state.EmittedRange.Count != state.NextOrdinal)
				{
					fault = KernelFaultCode.InvalidToyState;
					return false;
				}
			}

			// The latch is checked in two parts, because only one of them blocks the schedule rule.
			//
			// The schedule invariant is selected by the latch's enum value: which constraint
			// applies depends on whether it reads enabled. But most of that invariant needs only
			// the value, not the change tick. So a latch whose enum is known but whose tick is
			// malformed still selects a rule, and the non-latch half of that rule must be judged
			// first -- otherwise a state that is wrong about its schedule *and* its latch tick
			// reports the latch, against the frozen order.
			//
			// An unknown or unobserved value is different in kind: it selects no rule at all, so
			// there is nothing to evaluate ahead of it and the latch fault is the only honest
			// answer.
			bool valueIsKnown = state.OptionLatch.Value == OptionLatchValue.Enabled
				|| state.OptionLatch.Value == OptionLatchValue.Disabled;
			if (!valueIsKnown)
			{
				fault = KernelFaultCode.InvalidOptionLatch;
				return false;
			}

			// Part one of the schedule rule: everything decidable from the value alone.
			if (state.OptionLatch.Value == OptionLatchValue.Enabled)
			{
				if (!state.ClockScheduled || state.NextDueTick <= state.ProcessedThroughTick)
				{
					fault = KernelFaultCode.InvalidToyState;
					return false;
				}
			}
			else if (state.ClockScheduled || state.NextDueTick != 0L)
			{
				fault = KernelFaultCode.InvalidToyState;
				return false;
			}

			// Now the rest of the latch, which the remaining comparison needs.
			if (!OptionLatchRules.IsWellFormed(state.OptionLatch))
			{
				fault = KernelFaultCode.InvalidOptionLatch;
				return false;
			}

			// Part two: the one comparison that reads the change tick, so it could not be made
			// before the tick was known good. Compared pairwise rather than against max(a, b) + 1,
			// which could overflow.
			if (state.OptionLatch.Value == OptionLatchValue.Enabled
				&& state.NextDueTick <= state.OptionLatch.ChangedAtTick)
			{
				fault = KernelFaultCode.InvalidToyState;
				return false;
			}

			fault = KernelFaultCode.None;
			return true;
		}

		private static ToyAdvanceResult Failed(FixedPeriodToyState source, KernelFaultCode fault)
		{
			return new ToyAdvanceResult(source, OptionTransitionKind.None, fault);
		}
	}
}
