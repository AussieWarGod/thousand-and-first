using System.Collections.Generic;
using XRL;
using XRL.World;

namespace ThousandAndFirst
{
	public static partial class KingdomRelocation
	{
		/// <summary>Closes loaded ring calls before the owning city leaves realm topology.</summary>
		public static void BeforeOwnershipLoss(KingdomSystem System,
			IEnumerable<string> ZoneIds, string Reason)
		{
			if (System == null || ZoneIds == null || The.ZoneManager?.CachedZones == null) return;
			foreach (string id in ZoneIds)
			{
				if (!The.ZoneManager.CachedZones.TryGetValue(id, out Zone zone)
					|| !HasActive(zone) || !TryRead(zone, out KingdomRelocationReceipt receipt,
						out string expected, out _)) continue;
				RollbackAndQuarantine(zone, expected, receipt,
					Reason ?? "The owning settlement left the realm.");
			}
		}
	}
}
