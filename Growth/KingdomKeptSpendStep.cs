using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>One source in an exact kept-parts debit. <see cref="Remaining"/> is written first;
	/// a zero remainder is instead finalized only after every such source passed preflight.</summary>
	internal readonly struct KingdomKeptSpendStep
	{
		public readonly int Source;
		public readonly int Original;
		public readonly int Taken;
		public readonly int Remaining;

		public bool NeedsFinalization => Remaining == 0;

		public KingdomKeptSpendStep(int Source, int Original, int Taken)
		{
			this.Source = Source;
			this.Original = Original;
			this.Taken = Taken;
			Remaining = Original - Taken;
		}
	}

}
