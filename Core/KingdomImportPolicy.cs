using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>What the next world does about a sealed realm, before anyone is asked anything.</summary>
	internal enum KingdomImportPolicy
	{
		/// <summary>Nothing crosses. The next world is clean, and is never asked about it.</summary>
		Off = 0,
		/// <summary>The most recent eligible seal is offered, once. Addendum 22 C10's default.</summary>
		LatestEligible = 1
	}
}
