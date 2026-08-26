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

}
