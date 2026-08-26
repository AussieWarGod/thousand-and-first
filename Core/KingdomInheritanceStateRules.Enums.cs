using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace ThousandAndFirst
{
	internal enum KingdomInheritancePhase
	{
		Empty = 0,
		Reserved = 1,
		SiteSelected = 2,
		WorldValidated = 3,
		Installed = 4,
		AppliedPendingDurability = 5,
		Committed = 6,
		Refused = 7,
		RepairRequired = 8
	}

	internal enum KingdomInheritanceStartFault
	{
		None = 0,
		MissingStart = 1,
		AlternateWorld = 2,
		TargetIsStart = 3
	}

	internal enum KingdomCommittedRewindAction
	{
		DeferUntilPrimary = 0,
		AdoptDurable = 1,
		AwaitLazyBuilder = 2,
		ReapplyCleanBuiltTarget = 3,
		RepairRequired = 4
	}

	internal enum KingdomInheritanceLoadKind
	{
		Unknown = 0,
		Primary = 1,
		SameGameRollback = 2
	}

}
