using System;

namespace ThousandAndFirst
{
	/// <summary>Whether one pure plot-labour assessment is usable and which save lane owns it.</summary>
	public enum KingdomPlotLabourVerdict : byte
	{
		Invalid = 0,
		LegacyCalendar = 1,
		Attended = 2
	}

	/// <summary>Engine-free copy of one plot root's named labour properties.</summary>
	public sealed class KingdomPlotLabourReceipt
	{
		public int Schema;
		public bool HasRequiredTicks;
		public long RequiredTicks;
		public bool HasRemainingTicks;
		public long RemainingTicks;
		public bool HasLastTick;
		public long LastTick;
		public long LegacyStartTick;
		public long LegacyTotalTicks;
	}

	/// <summary>Pure receipt verdict and exact elapsed-work result for one settlement pass.</summary>
	public sealed class KingdomPlotLabourStep
	{
		public KingdomPlotLabourVerdict Verdict;
		public string Failure;
		public long RequiredTicks;
		public long CompletedTicks;
		public long RemainingTicks;
		public long NextTick;
		public long WorkedTicks;
		public long CompletionTick;
		public bool NeedsAttendance;
		public bool WriteReceipt;
		public bool Complete;
	}

	/// <summary>
	/// Pure authority for plot-labour receipt validation and elapsed attended work. Schema zero is
	/// the frozen compatibility clock. Current work consumes each forward interval even at zero
	/// effectiveness, so idle or queued time can never be banked for later hands.
	/// </summary>
	public static class KingdomPlotLabourRules
	{
		public const int LegacySchema = 0;
		public const int CurrentSchema = 2;

		public static KingdomPlotLabourStep Assess(KingdomPlotLabourReceipt Receipt,
			long TimeTick)
		{
			KingdomPlotLabourStep result = new KingdomPlotLabourStep
			{
				Verdict = KingdomPlotLabourVerdict.Invalid,
				Failure = "The plot work's labour receipt is absent."
			};
			if (Receipt == null) return result;
			if (Receipt.Schema == LegacySchema)
			{
				result.Verdict = KingdomPlotLabourVerdict.LegacyCalendar;
				result.Failure = null;
				result.RequiredTicks = Receipt.LegacyTotalTicks;
				result.CompletedTicks = TimeTick - Receipt.LegacyStartTick;
				return result;
			}
			if (Receipt.Schema != CurrentSchema)
			{
				result.Failure = "The plot work has an unknown labour receipt and cannot advance safely.";
				return result;
			}
			if (!Receipt.HasRequiredTicks || !Receipt.HasRemainingTicks || !Receipt.HasLastTick
				|| Receipt.RequiredTicks < 1L || Receipt.RemainingTicks < 0L
				|| Receipt.RemainingTicks > Receipt.RequiredTicks || Receipt.LastTick < 0L)
			{
				result.Failure = "The plot work's labour receipt is incomplete or contradictory; it has been left unchanged.";
				return result;
			}
			result.Verdict = KingdomPlotLabourVerdict.Attended;
			result.Failure = null;
			result.RequiredTicks = Receipt.RequiredTicks;
			result.RemainingTicks = Receipt.RemainingTicks;
			result.CompletedTicks = Receipt.RequiredTicks - Receipt.RemainingTicks;
			result.NextTick = Receipt.LastTick;
			result.Complete = Receipt.RemainingTicks == 0L;
			result.NeedsAttendance = !result.Complete && TimeTick > Receipt.LastTick;
			return result;
		}

		public static KingdomPlotLabourStep Advance(KingdomPlotLabourReceipt Receipt,
			long TimeTick, int LabourPercent, int InfrastructurePercent)
		{
			KingdomPlotLabourStep result = Assess(Receipt, TimeTick);
			if (result.Verdict != KingdomPlotLabourVerdict.Attended
				|| !result.NeedsAttendance) return result;
			ArchitectureLabourProgress progress = KingdomArchitectureRules.AdvanceLabour(
				Receipt.LastTick, TimeTick, Receipt.RemainingTicks, LabourPercent,
				InfrastructurePercent);
			result.RemainingTicks = progress.RemainingTicks;
			result.CompletedTicks = Receipt.RequiredTicks - progress.RemainingTicks;
			result.NextTick = progress.NextTick;
			result.WorkedTicks = progress.WorkedTicks;
			result.CompletionTick = progress.CompletionTick;
			result.Complete = progress.Complete;
			result.NeedsAttendance = false;
			result.WriteReceipt = true;
			return result;
		}
	}
}
