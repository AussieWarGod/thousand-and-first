using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using XRL;

namespace ThousandAndFirst.Harness
{
	// Read-only observers, active only during this fixture's explicit production menu actions.
	[HarmonyPatch]
	internal static class KingdomGuestActionsPreparationDiagnostics
	{
		[HarmonyTargetMethods]
		internal static IEnumerable<MethodBase> Targets()
		{
			foreach (string name in new[] { "ExactCreatedCandidate", "PrepareArrivalWaterLegs",
				"PrepareArrivalDomainSteps", "AppendArrivalOutbox" })
				yield return AccessTools.Method(typeof(KingdomGrowth), name);
			yield return AccessTools.Method(typeof(KingdomLifecycleRules), "TryPublishGrowth");
		}
		[HarmonyPostfix]
		internal static void After(MethodBase __originalMethod, bool __result)
		{
			Record(__originalMethod.Name, __result.ToString());
		}
		internal static void Record(string step, string result)
		{
			if (The.Game != null && KingdomGuestActionsNativeChecks.ActionActive
				&& KingdomGuestActionsNativeChecks.Evidence.Length < 4000)
				KingdomGuestActionsNativeChecks.Evidence.Append("; ").Append(step).Append('=').Append(result);
		}
	}

	[HarmonyPatch(typeof(KingdomLifecycleRules), "PrepareGrowthOperation")]
	internal static class KingdomGuestActionsOperationDiagnostics
	{
		[HarmonyPostfix]
		internal static void After(KingdomGrowthOperation __result)
		{
			KingdomGuestActionsPreparationDiagnostics.Record("PrepareGrowthOperation",
				__result == null ? "null" : __result.Action + ":" + __result.Phase);
		}
	}
}
