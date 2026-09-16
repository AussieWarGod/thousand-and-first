using System.Collections.Generic;
using HarmonyLib;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>Read-only first-publication boundary values; never changes the freeze or result.</summary>
	[HarmonyPatch(typeof(KingdomFounding), "TryReadOrFreezeFoundingStandings")]
	internal static class KingdomFoundingRegardNativeCapture
	{
		[HarmonyPostfix]
		internal static void After(KingdomSystem System, Faction Realm,
			List<KeyValuePair<string, int>> Targets, int Version, bool __result)
		{
			if (!KingdomQuickstartBootTest.LifecycleRequested || The.Game == null || System == null) return;
			string captured = "absent";
			if (Targets != null)
				foreach (var row in Targets)
					if (row.Key == "Inanimate") captured = row.Value.ToString();
			bool polity = KingdomPolityRules.TryValidate(System.PolityLedger, out string failure);
			bool identity = (bool)AccessTools.Method(typeof(KingdomSystem),
				"CurrentRelationshipAuthorityHealthy").Invoke(System, null);
			UnityEngine.Debug.Log("[TAF] founding regard freeze boundary: result=" + __result
				+ "; version=" + Version + "; count=" + (Targets?.Count ?? -1)
				+ "; inanimate=" + captured + "; personal=" + The.Game.PlayerReputation.Get("Inanimate")
				+ "; eligible=" + System.CanReserveDirectionalRelationship("Inanimate")
				+ "; identity=" + identity + "; polity=" + polity + "; polity-failure=" + failure
				+ "; realm=" + Realm?.Name + "; system-realm=" + System.KingdomFactionName);
		}
	}
}
