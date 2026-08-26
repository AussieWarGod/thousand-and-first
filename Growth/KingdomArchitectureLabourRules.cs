using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomArchitectureRules
	{
		// --- Time x labour x infrastructure ------------------------------------------------

		public static ArchitectureLabourProgress AdvanceLabour(long LastTick, long TimeTick,
			long RemainingTicks, int LabourPercent, int InfrastructurePercent)
		{
			ArchitectureLabourProgress result = new ArchitectureLabourProgress
			{
				PreviousTick = LastTick,
				NextTick = LastTick,
				RemainingTicks = RemainingTicks > 0 ? RemainingTicks : 0,
				WorkedTicks = 0,
				CompletionTick = RemainingTicks <= 0 ? LastTick : 0,
				Complete = RemainingTicks <= 0
			};
			if (result.Complete || TimeTick <= LastTick) return result;
			result.NextTick = TimeTick; // idle time is spent, never banked.
			int labour = ClampPercent(LabourPercent);
			int infrastructure = ClampPercent(InfrastructurePercent);
			int factor = labour * infrastructure;
			long elapsed = TimeTick - LastTick;
			long available = ScaleByTenThousand(elapsed, factor);
			if (available <= 0) return result;
			long worked = available < result.RemainingTicks ? available : result.RemainingTicks;
			result.WorkedTicks = worked;
			result.RemainingTicks -= worked;
			if (result.RemainingTicks > 0) return result;
			result.Complete = true;
			long low = 1;
			long high = elapsed;
			while (low < high)
			{
				long middle = low + (high - low) / 2;
				if (ScaleByTenThousand(middle, factor) >= worked) high = middle;
				else low = middle + 1;
			}
			result.CompletionTick = LastTick + low;
			return result;
		}

		private static int ClampPercent(int Value)
		{
			if (Value <= 0) return 0;
			return Value >= 100 ? 100 : Value;
		}

		private static long ScaleByTenThousand(long Ticks, int Factor)
		{
			if (Ticks <= 0 || Factor <= 0) return 0;
			if (Factor >= 10000) return Ticks;
			return Ticks / 10000L * Factor + Ticks % 10000L * Factor / 10000L;
		}
	}
}
