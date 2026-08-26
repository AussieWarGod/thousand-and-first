using System;

namespace ThousandAndFirst.Simulation.Kernel
{
	internal static partial class FixedPeriodToyRules
	{
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
