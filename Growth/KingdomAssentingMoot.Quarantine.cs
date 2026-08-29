using XRL;
using XRL.World;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomAssentingMoot
	{
		private static bool Quarantine(KingdomCityBook Book,
			KingdomAssentingMootReceipt Receipt, string Reason, out string Failure)
		{
			Failure = string.IsNullOrEmpty(Reason)
				? "Assenting-moot evidence diverged." : Reason;
			KingdomAssentingMootReceipt quarantined =
				KingdomAssentingMootRules.Quarantined(Receipt, Failure);
			if (Book != null && quarantined != null) Book.AssentingMoot = quarantined;
			CleanupLoaded(quarantined ?? Receipt);
			KingdomLog.Log("assenting moot: " + Failure);
			return false;
		}

		private static void CleanupLoaded(KingdomAssentingMootReceipt Receipt)
		{
			if (Receipt == null) return;
			if (TryCachedZone(Receipt.ZoneId, out Zone zone))
			{
				string ignored;
				RemoveZoneProjection(zone, Receipt, out ignored);
			}
			RemoveMemberProjections(Receipt);
		}
	}
}
