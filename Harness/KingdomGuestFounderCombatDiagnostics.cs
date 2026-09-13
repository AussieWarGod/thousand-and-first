using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using ThousandAndFirst.Simulation.City;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>Observe combat interrupted by civic posting; never alter goals or combat results.</summary>
	[HarmonyPatch]
	internal static class KingdomGuestFounderCombatDiagnostics
	{
		private static int Observations;
		[HarmonyTargetMethods]
		internal static IEnumerable<MethodBase> Targets()
		{
			yield return AccessTools.Method(typeof(KingdomStations), "Place");
			yield return AccessTools.Method(typeof(KingdomStations), "Release");
		}
		[HarmonyPrefix]
		internal static void Before(GameObject Settler, out string __state)
		{
			__state = null;
			if (!KingdomGuestActionsNativeChecks.Founders.Contains(Settler) || Observations >= 32) return;
			var goals = Settler.Brain?.Goals?.Items;
			if (goals == null) return;
			foreach (var goal in goals)
			{
				string name = goal?.GetType().Name ?? "none";
				if (name.IndexOf("Kill", StringComparison.Ordinal) < 0
					&& name.IndexOf("Flee", StringComparison.Ordinal) < 0
					&& name.IndexOf("Attack", StringComparison.Ordinal) < 0) continue;
				__state = Describe(Settler); Observations++; break;
			}
		}
		[HarmonyPostfix]
		internal static void After(GameObject Settler, MethodBase __originalMethod, bool __result, string __state)
		{
			if (__state == null) return;
			KingdomScenarioJournal.Append("guest-founder-combat-post", true,
				"tick=" + The.Game.TimeTicks + "; method=" + __originalMethod.Name + "; posted=" + __result
				+ "; before=" + __state + "; after=" + Describe(Settler) + "; observation-only=true");
		}
		internal static string Describe(GameObject body)
		{
			if (body == null) return "none";
			var text = new StringBuilder();
			text.Append(body.IDIfAssigned).Append(':').Append(body.Blueprint).Append('@')
				.Append(body.CurrentCell?.X).Append(',').Append(body.CurrentCell?.Y).Append(" goals=[");
			var goals = body.Brain?.Goals?.Items;
			for (int i = 0; goals != null && i < goals.Count && i < 12; i++)
				text.Append(i == 0 ? "" : ",").Append(goals[i]?.GetType().Name ?? "none");
			return text.Append(']').ToString();
		}
	}
}
