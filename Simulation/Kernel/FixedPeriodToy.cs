using System;

namespace ThousandAndFirst.Simulation.Kernel
{
	/// <summary>
	/// A deliberately contentless fixed-period clock, built to prove the kernel algebra and
	/// nothing else.
	/// <para>
	/// <c>ToyPulse</c> has no water, item, citizen, standing, notification, or player-facing
	/// meaning, and must never be wired to live code. It is deleted or replaced once later pure
	/// modules prove the same algebra against real content.
	/// </para>
	/// </summary>
	internal static partial class FixedPeriodToyRules
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

	}
}
