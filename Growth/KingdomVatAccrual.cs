using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	internal readonly struct KingdomVatAccrual
	{
		public readonly long NextTick;
		public readonly int RemainingTicks;
		public readonly int WorkedTicks;
		public readonly bool Complete;

		public KingdomVatAccrual(long NextTick, int RemainingTicks, int WorkedTicks, bool Complete)
		{
			this.NextTick = NextTick;
			this.RemainingTicks = RemainingTicks;
			this.WorkedTicks = WorkedTicks;
			this.Complete = Complete;
		}
	}

}
