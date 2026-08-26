using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Bounded canonical cross-receipt state for one application job.</summary>
	internal enum KingdomLabRegistryStatus : byte
	{
		Active = 0,
		Complete = 1,
		Cancelled = 2,
		Abandoned = 3,
		Quarantined = 4
	}

}
