using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Persistable aggregate of every physical water attempt on one lab job.</summary>
	internal readonly struct KingdomLabWaterClaim
	{
		public readonly int Paid;
		public readonly int Lost;
		public readonly int Outstanding;
		public readonly bool Quarantined;
		public readonly bool Settled;

		public KingdomLabWaterClaim(int Paid, int Lost, int Outstanding,
			bool Quarantined, bool Settled)
		{
			this.Paid = Paid;
			this.Lost = Lost;
			this.Outstanding = Outstanding;
			this.Quarantined = Quarantined;
			this.Settled = Settled;
		}
	}

}
