using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomBounty
	{
		/// <summary>Consumes opportunities nobody could have taken while unattended, without drawing
		/// an outcome from a later roster. False means the notice exhausted its bounded lane.</summary>
		private static bool ConsumeMissedAttempts(r_KingdomNotice Data, long LatestTick, long Skipped)
		{
			if (Skipped <= 0L)
			{
				Data.NextAttemptTick = LatestTick;
				return true;
			}
			long room = (long)KingdomBountyRules.MaxPasses - Data.Passes;
			if (Skipped >= room)
			{
				Data.Passes = KingdomBountyRules.MaxPasses;
				Data.AttemptScheduleExhausted = true;
				Data.NextAttemptTick = 0L;
				return false;
			}
			Data.Passes += (int)Skipped;
			Data.NextAttemptTick = LatestTick;
			KingdomLog.Log("bounty: skipped " + Skipped
				+ " unattended notice opportunities; latest=" + LatestTick);
			return true;
		}

		private static void ConsumeAttempt(r_KingdomNotice Data, long ScheduledTick)
		{
			if (Data.Passes < KingdomBountyRules.MaxPasses)
			{
				Data.Passes++;
			}
			long next;
			if (Data.Passes >= KingdomBountyRules.MaxPasses
				|| !KingdomBountyRules.TryAdvanceAttemptTick(ScheduledTick, out next))
			{
				Data.AttemptScheduleExhausted = true;
				Data.NextAttemptTick = 0L;
				return;
			}
			Data.NextAttemptTick = next;
		}
	}
}
