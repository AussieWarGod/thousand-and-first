using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	[HarmonyPatch(typeof(KingdomInheritanceState), "Read")]
	internal static class KingdomUpgradeInheritanceReadPatch
	{
		[HarmonyPrefix, HarmonyPriority(Priority.First)]
		internal static void Prefix(KingdomInheritanceState __instance, SerializationReader __0)
		{ KingdomUpgradeLoad.BeginRead(__instance, __0); }
		[HarmonyPostfix, HarmonyPriority(Priority.Last)]
		internal static void Postfix(KingdomInheritanceState __instance) { KingdomUpgradeLoad.EndRead(__instance); }
		[HarmonyFinalizer]
		internal static void Finalizer(Exception __exception) { KingdomUpgradeLoad.FinalizeRead(__exception); }
	}

	[HarmonyPatch(typeof(KingdomInheritanceStateRules), "TryValidateSavedShape")]
	internal static class KingdomUpgradeInheritedShapePatch
	{
		[HarmonyPrefix, HarmonyPriority(Priority.First)]
		internal static void Prefix(KingdomInheritanceSavedShape __0, string __1) { KingdomUpgradeLoad.BeforeShape(__0, __1); }
		[HarmonyPostfix]
		internal static void Postfix(bool __result) { KingdomUpgradeLoad.AfterShape(__result); }
	}

	[HarmonyPatch(typeof(SerializationReader), "ReadNamedFields")]
	internal static class KingdomUpgradeNamedReadPatch
	{
		[HarmonyPostfix, HarmonyPriority(Priority.First)]
		internal static void Postfix(object __0, Type __1) { KingdomUpgradeLoad.NamedRead(__0, __1); }
	}

	[HarmonyPatch]
	internal static class KingdomUpgradeRepairPatch
	{
		[HarmonyTargetMethods]
		internal static IEnumerable<MethodBase> Targets()
		{
			yield return AccessTools.Method(typeof(KingdomInheritanceState), "SetRepair");
			yield return AccessTools.Method(typeof(KingdomInheritanceState), "DisableRecovery");
		}
		[HarmonyPrefix]
		internal static void Prefix(KingdomInheritanceState __instance) { KingdomUpgradeLoad.Repair(__instance); }
	}
}
