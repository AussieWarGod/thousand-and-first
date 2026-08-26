using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Why a city may not leave the realm, or that its leaving is warranted.</summary>
	public enum SecessionVerdict
	{
		Warranted = 0,
		OneCity = 1,
		NoClash = 2,
		DissentHolds = 3
	}
}
