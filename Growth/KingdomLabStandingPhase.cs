using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	internal enum KingdomLabStandingPhase : byte
	{
		Pending = 0,
		Bound = 1,
		Intent = 2,
		Applied = 3,
		Quarantined = 4
	}

}
