using System;

namespace ThousandAndFirst.Harness
{
	/// <summary>Independent arithmetic oracle for an active, modern, unleased growth resume.
	/// Expected values never call production resume/cadence helpers. Leased/debt-bearing or
	/// unhealthy growth is outside this focused fixture, not silently treated as pristine.</summary>
	internal static class KingdomScenarioPauseOracle
	{
		internal readonly struct Expected
		{
			internal readonly long PausedTicks, EffectiveTick, ArrivalDeadline, ArrivalEpoch, ResumeToken, DayDeadline;
			internal Expected(long paused, long effective, long arrival, long epoch, long token, long day)
			{ PausedTicks = paused; EffectiveTick = effective; ArrivalDeadline = arrival; ArrivalEpoch = epoch; ResumeToken = token; DayDeadline = day; }
		}

		internal static bool TryResume(long DisabledAt, long Now, bool LocalPaused, long LocalStart,
			long AlreadyPaused, long EffectiveTick, long ArrivalInterval, long ArrivalEpoch,
			long ResumeToken, long DayInterval, out Expected Result)
		{
			Result = default(Expected);
			if (DisabledAt < 0 || Now <= DisabledAt || LocalStart < 0 || LocalStart > Now
				|| (!LocalPaused && LocalStart != 0) || AlreadyPaused < 0 || EffectiveTick < 0
				|| EffectiveTick > Now || ArrivalInterval <= 0 || DayInterval <= 0
				|| ArrivalEpoch <= 0 || ResumeToken < 0) return false;
			// Both open intervals end at Now. Their union begins at the earlier start.
			long start = LocalPaused && LocalStart < DisabledAt ? LocalStart : DisabledAt;
			try
			{
				Result = new Expected(checked(AlreadyPaused + (Now - start)), EffectiveTick,
					checked(Now + ArrivalInterval), checked(ArrivalEpoch + 1),
					checked(ResumeToken + 1), checked(Now + DayInterval));
				return true;
			}
			catch (OverflowException) { return false; }
		}

		internal static bool TryCommitted(long Before, long DisabledAt, long Now, out long Deadline)
		{
			Deadline = Before;
			if (Before < 0 || DisabledAt < 0 || Now < DisabledAt) return false;
			if (Before <= DisabledAt) return true; // Already due work keeps its history.
			try { Deadline = checked(Before + (Now - DisabledAt)); return true; }
			catch (OverflowException) { return false; }
		}
	}
}
