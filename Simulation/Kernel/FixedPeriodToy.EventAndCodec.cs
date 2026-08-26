using System;

namespace ThousandAndFirst.Simulation.Kernel
{
	internal static partial class FixedPeriodToyRules
	{
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

	}
}
