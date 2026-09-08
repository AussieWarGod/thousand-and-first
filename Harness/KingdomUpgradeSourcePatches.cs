using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using XRL;

namespace ThousandAndFirst.Harness
{
	[HarmonyPatch(typeof(XRLGame), "SaveGame")]
	internal static class KingdomUpgradeSourceSavePatch
	{
		// Void observers never suppress SaveGame, replace its Task, or swallow its exception.
		[HarmonyPrefix, HarmonyPriority(Priority.First)]
		internal static void Prefix(XRLGame __instance, string __0, bool __2, bool __3, out KingdomUpgradeState __state)
		{ __state = KingdomUpgradeSource.Begin(__instance, __0, __2, __3); }
		[HarmonyPostfix, HarmonyPriority(Priority.Last)]
		internal static void Postfix(XRLGame __instance, Task __result, KingdomUpgradeState __state)
		{ KingdomUpgradeSource.End(__instance, __state, __result); }
		[HarmonyFinalizer]
		internal static void Finalizer(XRLGame __instance, Exception __exception)
		{ if (__exception != null) KingdomUpgradeSource.Error(__instance, __exception); }
	}

	[HarmonyPatch(typeof(XRLGame), "SaveGameError")]
	internal static class KingdomUpgradeSourceErrorPatch
	{
		[HarmonyPrefix]
		internal static void Prefix(XRLGame __instance) { KingdomUpgradeSource.Error(__instance, null); }
	}

	[HarmonyPatch(typeof(XRLGame), "GetCacheDirectory", new Type[] { typeof(string) })]
	internal static class KingdomUpgradeSourceCachePatch
	{
		[HarmonyPostfix, HarmonyPriority(Priority.Last)]
		internal static void Postfix(XRLGame __instance, string __0, string __result)
		{ KingdomUpgradeSource.CacheResolved(__instance, __0, __result); }
	}

	[HarmonyPatch]
	internal static class KingdomUpgradeSourceWritePatch
	{
		[HarmonyTargetMethods]
		internal static IEnumerable<MethodBase> Targets()
		{
			yield return AccessTools.Method(typeof(KingdomInheritanceState), "Write");
			yield return AccessTools.Method(typeof(KingdomPolityRealmTransition), "Write");
			yield return AccessTools.Method(typeof(KingdomPolityLegacySnapshot), "Write");
		}
		[HarmonyPostfix]
		internal static void Postfix(object __instance) { KingdomUpgradeSource.Wrote(__instance); }
	}
}
