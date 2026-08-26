using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	[Flags]
	internal enum KingdomInheritEngineCheck
	{
		None = 0,
		ConnectionCell = 1,
		Terrain = 2,
		ExistingObjects = 4,
		Stairs = 8,
		EntryToHeartPath = 16
	}

}
