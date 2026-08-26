using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	internal enum KingdomLabMessagePhase : byte
	{
		Pending = 0,
		Intent = 1,
		Delivered = 2,
		Skipped = 3,
		Lost = 4
	}

}
