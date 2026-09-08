using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using XRL;

namespace ThousandAndFirst.Harness
{
	[KingdomScenarioVerbProvider]
	public sealed class KingdomUpgradeStageProvider : IKingdomScenarioVerbProvider
	{
		internal const string SetupVerb = "upgrade-stage-setup", SaveVerb = "upgrade-stage-save";
		internal const string MarkerFile = "upgrade-stage-source.txt";
		internal const string ReceiptFile = "upgrade-stage-receipt.txt", FailureFile = "upgrade-stage-failure.txt";
		private static readonly string[] Script = { "stagedigest", SetupVerb, "advance 2400", SaveVerb, "stagedigest" };
		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }
		public IEnumerable<string> ScenarioVerbs { get { return new[] { SetupVerb, SaveVerb }; } }
		public string RunScenarioVerb(string verb, string argument, out bool Ok)
		{
			Ok = false;
			try
			{
				Require(string.IsNullOrEmpty(argument) && (verb == SetupVerb || verb == SaveVerb), "stage verbs take no arguments");
				string result = KingdomUpgradeStageChecks.Run(verb);
				Ok = true; return result;
			}
			catch (Exception error) { return KingdomUpgradeStageChecks.Refuse(error); }
		}
		internal static void Request(out string root, out string pin)
		{
			root = KingdomUpgradeFiles.Root();
			Require(root.StartsWith(@"C:\taf-scenario.", StringComparison.Ordinal), "stage source needs exact C scenario root");
			Require(KingdomScenarioScript.TryRead(out var script, out string failure) && script.Count == Script.Length,
				failure ?? "exact stage script missing");
			for (int i = 0; i < Script.Length; i++) Require(script[i] == Script[i], "stage script differs");
			string marker = KingdomUpgradeFiles.Read(Path.Combine(root, "Local", MarkerFile), 128);
			Match match = Regex.Match(marker, @"\Ataf-upgrade-stage-source-v1\n([0-9a-f]{40})\n\z");
			Require(match.Success, "stage source marker is malformed"); pin = match.Groups[1].Value;
			foreach (string name in new[] { "upgrade-save-request.txt", "taf-downgrade-request.txt", "scenario-load.txt", "scenario-load-snapshot.txt" })
				KingdomUpgradeFiles.Vacant(Path.Combine(root, "Local", name));
		}
		internal static void Require(bool value, string failure)
		{ if (!value) throw new InvalidOperationException(failure ?? "native stage source refused"); }
	}

	[HarmonyPatch(typeof(XRLGame), "SaveGame")]
	// Confinement guards can stop an invalid test save; any violation permanently poisons evidence.
	internal static class KingdomUpgradeStageSaveObserver
	{
		[HarmonyPrefix, HarmonyPriority(Priority.First)]
		internal static void Prefix(XRLGame __instance, string __0, bool __2, bool __3)
		{ KingdomUpgradeStageChecks.SaveEntered(__instance, __0, __2, __3); }
	}
	[HarmonyPatch(typeof(XRLGame), "SaveGameError")]
	internal static class KingdomUpgradeStageSaveErrorObserver
	{
		[HarmonyPrefix]
		internal static void Prefix(XRLGame __instance) { KingdomUpgradeStageChecks.SaveError(__instance); }
	}
	[HarmonyPatch(typeof(XRLGame), "GetCacheDirectory", new Type[] { typeof(string) })]
	internal static class KingdomUpgradeStageCacheBoundary
	{
		[HarmonyPrefix, HarmonyPriority(Priority.First)]
		internal static void Prefix(XRLGame __instance) { KingdomUpgradeStageChecks.Cache(__instance, null, null, false); }
		[HarmonyPostfix, HarmonyPriority(Priority.Last)]
		internal static void Postfix(XRLGame __instance, string __0, string __result)
		{ KingdomUpgradeStageChecks.Cache(__instance, __0, __result, true); }
	}
	[HarmonyPatch]
	internal static class KingdomUpgradeStageWriteObserver
	{
		[HarmonyTargetMethods]
		internal static IEnumerable<MethodBase> Targets()
		{
			yield return AccessTools.Method(typeof(KingdomSystem), "Write");
			yield return AccessTools.Method(typeof(KingdomSeal), "Write");
		}
		[HarmonyPostfix]
		internal static void Postfix(object __instance) { KingdomUpgradeStageChecks.Wrote(__instance); }
	}
}
