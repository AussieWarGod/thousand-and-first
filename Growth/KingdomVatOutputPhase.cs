using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Durable output-transfer receipt. Intent is never replayed after a reload.</summary>
	internal enum KingdomVatOutputPhase : byte
	{
		None = 0,
		AddIntent = 1,
		Added = 2,
		Quarantined = 3
	}

}
