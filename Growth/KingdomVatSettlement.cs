using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	internal enum KingdomVatSettlement : byte
	{
		Wait = 0,
		CreateOutput = 1,
		ConsumeInput = 2,
		CollectOutput = 3,
		ReturnInput = 4,
		Missing = 5
	}

}
