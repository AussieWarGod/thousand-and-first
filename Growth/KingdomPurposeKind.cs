using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Frozen wire identity for the five purposeful works. Values 1 and 2 shipped with the
	/// body-purpose trial; the reciprocal portfolio appends values and never renumbers them.
	/// </summary>
	public enum KingdomPurposeKind : byte
	{
		None = 0,
		Flesh = 1,
		Chrome = 2,
		Deep = 3,
		Forge = 4,
		Harvest = 5
	}
}
