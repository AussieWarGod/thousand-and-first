using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Durable raw-destruction receipt, mirrored onto the surviving output.</summary>
	internal enum KingdomVatRawPhase : byte
	{
		Present = 0,
		DestroyIntent = 1,
		Destroyed = 2,
		Quarantined = 3
	}

}
