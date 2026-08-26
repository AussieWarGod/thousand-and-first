using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Observation of the one effect identity a removal receipt owns.</summary>
	internal enum KingdomLabOwnedTargetState : byte
	{
		Present = 0,
		Absent = 1,
		Uncertain = 2
	}

}
