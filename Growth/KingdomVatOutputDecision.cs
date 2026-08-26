using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Pure decision for an output identity frozen on a vat input.</summary>
	internal enum KingdomVatOutputDecision : byte
	{
		CreateAndFreeze = 0,
		UseExact = 1,
		QuarantineMissing = 2,
		QuarantineMismatch = 3
	}

}
