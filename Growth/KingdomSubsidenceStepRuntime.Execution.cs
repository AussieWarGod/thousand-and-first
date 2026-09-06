using XRL;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRuntime
	{
		private static bool TryExecutionFrame(KingdomSystem system, long now,
			out OptionFrame frame, out string refusal)
		{
			frame = null;
			refusal = "Subsidence waits for the actual world clock and its exact saved account.";
			if (The.Game == null || now < 0 || now != The.Game.TimeTicks
				|| !TryOptionFrame(system, out OptionFrame owner)) return false;
			if (!KingdomSubsidenceExecutionClockRules.TryValidate(now, system.LastSubsidenceTick,
				owner.Owner.Step, out refusal)) return false;
			if (!ExecutionExact(owner, now))
			{
				refusal = "The subsidence clock or its saved owner changed during admission.";
				return false;
			}
			frame = owner;
			refusal = null;
			return true;
		}

		private static bool ExecutionExact(OptionFrame frame, long now)
		{
			return OptionExact(frame) && frame.Game.TimeTicks == now
				&& KingdomSubsidenceExecutionClockRules.TryValidate(now, frame.System.LastSubsidenceTick,
					frame.Owner.Step, out _);
		}

		// Homecoming retains the structural publisher: acknowledging reports must remain possible
		// when execution is quarantined. Work and option changes must prove both candidate clocks.
		private static bool SaveExecutingOption(OptionFrame frame, KingdomSubsidenceStepBook next,
			long now, bool admit = false)
		{
			return ExecutionExact(frame, now)
				&& KingdomSubsidenceExecutionClockRules.TryValidate(now, frame.System.LastSubsidenceTick,
					next, out _)
				&& SaveOption(frame, next, admit) && ExecutionExact(frame, now);
		}
	}
}
