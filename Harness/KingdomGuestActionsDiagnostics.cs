using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using XRL;
using XRL.World;

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
	[HarmonyPatch(typeof(KingdomGrowth), "ReconcileArrivalDomains")]
	internal static class KingdomGuestActionsDomainDiagnostics
	{
		[HarmonyPostfix]
		internal static void After(KingdomGrowthOperation operation, GameObject settler, bool __result)
		{
			if (!KingdomGuestActionsNativeChecks.ActionActive || operation == null) return;
			int cursor = operation.DomainCursor;
			KingdomGuestActionsPreparationDiagnostics.Record("arrival-body",
				"citizen=" + settler.GetIntProperty("KingdomCitizen") + " display=" + settler.DisplayName
				+ " stored-name=" + settler.GetStringProperty("KingdomName"));
			KingdomGuestActionsPreparationDiagnostics.Record("ReconcileArrivalDomains",
				__result + " cursor=" + cursor + " kind="
				+ (cursor < operation.DomainSteps.Count ? operation.DomainSteps[cursor].Kind.ToString() : "complete"));
		}
	}

	[HarmonyPatch(typeof(KingdomGrowth), "WriteCitizenshipReceiptGraph")]
	internal static class KingdomGuestActionsEnrollmentGraphDiagnostics
	{
		private static int Captures;
		[HarmonyPostfix]
		internal static void After(BinaryWriter writer, bool projectedAfter)
		{
			if (!KingdomGuestActionsNativeChecks.ActionActive || Captures >= 16
				|| !(writer?.BaseStream is MemoryStream stream)) return;
			try
			{
				string name = "guest-enrollment-" + Captures++ + "-" + projectedAfter + ".bin";
				byte[] bytes = stream.ToArray();
				using (var file = new FileStream(Path.Combine(KingdomScenarioJournal.ProfileRoot(), name),
					FileMode.CreateNew, FileAccess.Write)) file.Write(bytes, 0, bytes.Length);
				KingdomGuestActionsPreparationDiagnostics.Record("enrollment-graph", name + ":" + bytes.Length);
			}
			catch (Exception error)
			{
				KingdomGuestActionsPreparationDiagnostics.Record("graph-capture-error", error.GetType().Name);
			}
		}
	}

}
