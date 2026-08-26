using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

#if !TAF_TESTS
using XRL;
using XRL.World;
using XRL.World.Parts;
#endif

namespace ThousandAndFirst
{
	/// <summary>The disposition of one reserved legacy after its site-builder ran.</summary>
	internal enum KingdomInheritApplyStatus
	{
		Applied = 0,
		AlreadyApplied = 1,
		Refused = 2,
		Failed = 3
	}

}
