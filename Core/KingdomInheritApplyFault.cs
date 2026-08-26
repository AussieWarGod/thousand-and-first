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
	internal enum KingdomInheritApplyFault
	{
		None = 0,
		NullInput = 1,
		LegacyNotPromoted = 2,
		ReceiptNotReserved = 3,
		ReceiptMismatch = 4,
		TargetGameMismatch = 5,
		TargetZoneMismatch = 6,
		PlanInvalid = 7,
		WrongZoneSize = 8,
		ApplicationConflict = 9,
		PartialApplication = 10,
		BlueprintMissing = 11,
		InvalidCell = 12,
		ConnectionCell = 13,
		Terrain = 14,
		Occupied = 15,
		Stairs = 16,
		EntryToHeartPath = 17,
		ObjectCreation = 18,
		ObjectNotEmpty = 19,
		ObjectPlacement = 20,
		MarkerWrite = 21
	}

}
