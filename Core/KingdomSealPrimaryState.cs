using System;
using System.Collections.Generic;
using System.IO;

namespace ThousandAndFirst
{
	/// <summary>
	/// What a bounded, synchronous look at one game's primary save proved. Absence is distinct
	/// from an I/O failure: only <see cref="Absent"/> may help prove an ended origin. Presence
	/// proves save durability, never that a lazy inherited zone was actually applied.
	/// </summary>
	internal enum KingdomSealPrimaryState
	{
		Unknown = 0,
		Absent = 1,
		Present = 2
	}
}
