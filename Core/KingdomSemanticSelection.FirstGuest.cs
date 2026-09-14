using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;
using XRL.World;

namespace ThousandAndFirst
{
	internal static partial class KingdomSemanticSelection
	{
		/// <summary>First guests share reputation-weighted recruitment, restricted to the exact
		/// owned one-body catalogue admitted by durable first-guest authority.</summary>
		internal static bool TryPrepareGrowthFirstGuest(KingdomSystem system, Zone zone,
			long sequence, long createdTick, out KingdomSemanticPersonPlan plan,
			out string failure)
		{
			if (!TryPrepareGrowthFirstGuestPayload(system, (ulong)sequence, createdTick,
				out plan, out failure)) return false;
			Cell cell;
			if (!TryLocateGrowthArrival(system, zone, plan.RulesVersion, (ulong)sequence,
				out cell, out failure)) return false;
			plan.X = cell.X; plan.Y = cell.Y;
			return true;
		}

		internal static bool TryPrepareGrowthFirstGuestPayload(KingdomSystem system,
			ulong ordinal, long dueTick, out KingdomSemanticPersonPlan plan, out string failure)
		{
			return TryPrepareSettlerPayload(system, ordinal, dueTick, true, out plan, out failure);
		}
	}
}
