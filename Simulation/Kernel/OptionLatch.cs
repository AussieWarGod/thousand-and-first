using System;

namespace ThousandAndFirst.Simulation.Kernel
{
	internal enum OptionLatchValue : byte
	{
		Unobserved = 0,
		Disabled = 1,
		Enabled = 2
	}

	internal enum OptionTransitionKind : byte
	{
		None = 0,
		InitializedDisabled = 1,
		InitializedEnabled = 2,
		Disabled = 3,
		Enabled = 4
	}

	/// <summary>
	/// A persisted option observation: what the setting was last seen to be, and when.
	/// </summary>
	internal readonly struct OptionLatchState
	{
		internal readonly OptionLatchValue Value;

		internal readonly long ChangedAtTick;

		internal OptionLatchState(OptionLatchValue value, long changedAtTick)
		{
			Value = value;
			ChangedAtTick = changedAtTick;
		}
	}

	/// <summary>
	/// Detects that an external setting changed, and nothing more.
	/// <para>
	/// This primitive deliberately does not know whether a module cancels, freezes, refunds, or
	/// preserves work across the change — every later module supplies that policy explicitly,
	/// because the right answer differs per module and a shared guess would be wrong somewhere.
	/// </para>
	/// <para>
	/// Load may observe and then stop. The transition is persisted, but load never calls a module
	/// clock, which is what separates "the player changed a setting while away" from "this save
	/// was loaded again."
	/// </para>
	/// </summary>
	internal static class OptionLatchRules
	{
		/// <summary>
		/// Fault order is frozen: negative <paramref name="now"/> first, then malformed prior
		/// state, then clock regression.
		/// </summary>
		internal static bool TryObserve(
			OptionLatchState prior,
			bool configuredEnabled,
			long now,
			out OptionLatchState next,
			out OptionTransitionKind transition,
			out KernelFaultCode fault)
		{
			next = prior;
			transition = OptionTransitionKind.None;

			if (now < 0L)
			{
				fault = KernelFaultCode.InvalidTick;
				return false;
			}
			if (!IsWellFormed(prior))
			{
				fault = KernelFaultCode.InvalidOptionLatch;
				return false;
			}
			if (now < prior.ChangedAtTick)
			{
				fault = KernelFaultCode.ClockRegression;
				return false;
			}

			OptionLatchValue observed = configuredEnabled ? OptionLatchValue.Enabled : OptionLatchValue.Disabled;

			if (prior.Value == OptionLatchValue.Unobserved)
			{
				next = new OptionLatchState(observed, now);
				transition = configuredEnabled ? OptionTransitionKind.InitializedEnabled : OptionTransitionKind.InitializedDisabled;
				fault = KernelFaultCode.None;
				return true;
			}

			if (prior.Value == observed)
			{
				// Unchanged: hand back the prior latch exactly, tick included. Rewriting the tick
				// on every observation would make a setting that never changed look as though it
				// had just changed, on every single load.
				next = prior;
				transition = OptionTransitionKind.None;
				fault = KernelFaultCode.None;
				return true;
			}

			next = new OptionLatchState(observed, now);
			transition = configuredEnabled ? OptionTransitionKind.Enabled : OptionTransitionKind.Disabled;
			fault = KernelFaultCode.None;
			return true;
		}

		/// <summary>
		/// Canonical <see cref="OptionLatchValue.Unobserved"/> is exactly
		/// <c>(Unobserved, 0)</c>. An unobserved latch carrying some other tick is corrupt, not
		/// merely unusual: it claims a change time for a change that never happened.
		/// <para>
		/// Unnamed by the card but not narrowable: the toy's own canonical check calls it across
		/// files, so it cannot be private, and duplicating the rule in both places would let the
		/// two definitions of a well-formed latch drift apart.
		/// </para>
		/// </summary>
		internal static bool IsWellFormed(OptionLatchState state)
		{
			if (state.ChangedAtTick < 0L)
			{
				return false;
			}
			switch (state.Value)
			{
			case OptionLatchValue.Unobserved:
				return state.ChangedAtTick == 0L;
			case OptionLatchValue.Disabled:
			case OptionLatchValue.Enabled:
				return true;
			default:
				return false;
			}
		}
	}
}
