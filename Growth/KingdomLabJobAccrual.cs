using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	internal readonly struct KingdomLabJobAccrual
	{
		public readonly long NextTick;
		public readonly int RemainingTicks;
		public readonly int WorkedTicks;
		public readonly KingdomLabJobPhase Phase;

		public KingdomLabJobAccrual(long NextTick, int RemainingTicks, int WorkedTicks,
			KingdomLabJobPhase Phase)
		{
			this.NextTick = NextTick;
			this.RemainingTicks = RemainingTicks;
			this.WorkedTicks = WorkedTicks;
			this.Phase = Phase;
		}
	}

}
