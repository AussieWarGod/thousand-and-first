using System;
using System.Diagnostics;
using HarmonyLib;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomCampHeartChainTrace
	{
		internal static bool Active(KingdomSystem System)
			=> KingdomScenarioAutoRunner.ChainInputOwner() != null && System?.Population >= 25
				&& ReferenceEquals(System, The.Game?.GetSystem<KingdomSystem>());

		internal static void Assessment(KingdomSystem System, Zone Z, GameObject Work,
			KingdomSurvey Survey, KingdomUpgrade.Assessment Result)
		{
			if (!Active(System) || Work?.GetIntProperty(KingdomPlots.HeartPlotProperty) != 1
				|| !ReferenceEquals(KingdomSurvey.ActiveFor(Z), Survey)) return;
			try
			{
				bool complete = Survey.TryLoaded(out var loaded);
				var stock = KingdomMaterials.Stock(Z);
				string sources = "";
				foreach (var container in stock.Stockpiles)
				{
					int indexed = 0, held = 0;
					foreach (var item in container.Inventory.Objects)
					{
						held++;
						if (complete)
							foreach (var candidate in loaded)
								if (ReferenceEquals(candidate, item)) { indexed++; break; }
					}
					sources += "; source=" + container.IDIfAssigned + ":held=" + held + ":indexed=" + indexed;
				}
				KingdomLog.Log("chain assessment: tick=" + The.Game.TimeTicks + "; heart=" + Work.IDIfAssigned
					+ "; verdict=" + Result.Verdict + "; reason=" + Result.Reason + "; material-demand=" + Result.Demand.MaterialsInHand
					+ "; loaded-complete=" + complete + "; loaded=" + (loaded?.Count ?? -1)
					+ "; active-ground=" + ReferenceEquals(The.ZoneManager?.ActiveZone, Z)
					+ "; leases=" + stock.InputLeaseAuthorityExact + "; lease-failure=" + stock.InputLeaseFailure
					+ "; tally=" + stock.Tally.Describe() + sources + RoadGround(Z, Result.Reason));
			}
			catch (Exception error)
			{
				KingdomScenarioJournal.Append("camp-heart-chain-diagnostic", false,
					"assessment observer failed: " + error.GetType().Name + ": " + error.Message);
			}
		}

		private static string RoadGround(Zone Z, string Reason)
		{
			const string prefix = "plot-envelope growth would absorb public road ground at ";
			if (Reason == null || !Reason.StartsWith(prefix, StringComparison.Ordinal)) return "";
			string[] coordinates = Reason.Substring(prefix.Length).Split(',');
			if (coordinates.Length != 2 || !int.TryParse(coordinates[0], out int x)
				|| !int.TryParse(coordinates[1], out int y) || Z == null) return "; road-ground=unresolved";
			Cell cell = Z.GetCell(x, y);
			int traffic = KingdomRoadRules.TrafficAt(KingdomRoads.ReadTally(Z), x, y);
			var lookup = KingdomRoads.FindOurFloor(cell, out var floor);
			string detail = "; road-lookup=" + lookup + "; road-traffic=" + traffic
				+ "; road-tally-wear=" + KingdomRoadRules.WearAt(traffic);
			if (floor != null) detail += "; road-id=" + floor.IDIfAssigned + "; road-blueprint=" + floor.Blueprint
				+ "; road-state=" + floor.GetIntProperty(KingdomRoads.PathStateProperty)
				+ "; road-owned=" + floor.IsOwned()
				+ "; road-string-receipt=" + floor.HasStringProperty(KingdomConstruction.ReceiptProperty)
				+ "; road-int-receipt=" + floor.HasIntProperty(KingdomConstruction.ReceiptProperty)
				+ "; road-exact-unpaid=" + KingdomRoads.IsExactUnpaidTrack(cell, floor);
			return detail;
		}

		internal static void Time(string Step, Stopwatch Watch, bool Result)
		{
			if (Watch != null) KingdomLog.Log("chain timing: step=" + Step
				+ "; ms=" + Watch.ElapsedMilliseconds + "; completed=" + Result);
		}
	}

	[HarmonyPatch(typeof(KingdomUpgrade), nameof(KingdomUpgrade.Assess))]
	internal static class KingdomCampHeartChainAssessmentPatch
	{
		[HarmonyPostfix]
		internal static void Postfix(KingdomSystem System, Zone Z, GameObject Work,
			KingdomSurvey Survey, KingdomUpgrade.Assessment __result)
			=> KingdomCampHeartChainTrace.Assessment(System, Z, Work, Survey, __result);
	}

	[HarmonyPatch(typeof(KingdomSystem), "TrySemanticStep")]
	internal static class KingdomCampHeartChainStepTimingPatch
	{
		[HarmonyPrefix]
		internal static void Prefix(KingdomSystem __instance, out Stopwatch __state)
			=> __state = KingdomCampHeartChainTrace.Active(__instance) ? Stopwatch.StartNew() : null;
		[HarmonyPostfix]
		internal static void Postfix(string Step, Stopwatch __state, bool __result)
			=> KingdomCampHeartChainTrace.Time(Step, __state, __result);
	}

	[HarmonyPatch(typeof(KingdomSystem), "AttendSeatedSemantics")]
	internal static class KingdomCampHeartChainPassTimingPatch
	{
		[HarmonyPrefix]
		internal static void Prefix(KingdomSystem __instance, out Stopwatch __state)
			=> __state = KingdomCampHeartChainTrace.Active(__instance) ? Stopwatch.StartNew() : null;
		[HarmonyPostfix]
		internal static void Postfix(Stopwatch __state, bool __result)
			=> KingdomCampHeartChainTrace.Time("whole-pass", __state, __result);
	}
}
