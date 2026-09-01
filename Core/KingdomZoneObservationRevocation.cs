using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>One transition boundary for all realm-owned per-zone observation purposes.</summary>
	internal static class KingdomZoneObservationRevocation
	{
		internal static bool TryRevokeZones(IList<string> ZoneIds, out string Failure)
		{
			if (!KingdomReachObservationRuntime.TryRevokeZones(ZoneIds, out Failure)) return false;
			return KingdomEducationPostObservationRuntime.TryRevokeZones(ZoneIds, out Failure);
		}

		internal static bool TryRevokeOwned(KingdomSystem System, out string Failure)
		{
			if (!KingdomReachObservationRuntime.TryRevokeOwned(System, out Failure)) return false;
			return KingdomEducationPostObservationRuntime.TryRevokeOwned(System, out Failure);
		}
	}
}
