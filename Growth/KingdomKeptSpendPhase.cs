using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Observable phase of a prepared kept-parts debit.</summary>
	internal enum KingdomKeptSpendPhase : byte
	{
		RefusedClean = 0,
		ApplyCounts = 1,
		Finalize = 2,
		SpentExact = 3,
		Partial = 4
	}

}
