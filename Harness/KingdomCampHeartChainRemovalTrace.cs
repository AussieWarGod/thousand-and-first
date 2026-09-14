using System;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	[HarmonyPatch(typeof(r_KingdomScaffold), nameof(r_KingdomScaffold.TryCommitScaffoldRemovalProof))]
	internal static class KingdomCampHeartChainRemovalTrace
	{
		[HarmonyPostfix]
		internal static void Postfix(KingdomSystem System, Zone Z, GameObject Successor,
			GameObject ExpectedPredecessor, string Blueprint, string ScaffoldId,
			KingdomConstructionJob Job, string Failure, bool __result)
		{
			if (!KingdomCampHeartChainTrace.Active(System) || Job?.TargetKey != "heartmoot") return;
			try
			{
				Cell cell = Z?.GetCell(Job.X, Job.Y);
				bool scaffoldRoute = (Job.Route == KingdomConstructionRoute.CommissionScaffold
					|| Job.Route == KingdomConstructionRoute.PlanScaffold) && Job.SubjectId == ScaffoldId;
				bool pending = r_KingdomScaffold.IsExactPendingImprovementSuccessor(Successor);
				bool improvement = Job.Route == KingdomConstructionRoute.Improvement
					&& Job.SubjectId != ScaffoldId && pending;
				var outputState = KingdomConstruction.FindExactId(Z, Job.OutputId, out var output);
				var scaffoldState = KingdomConstruction.FindGlobalLiveId(ScaffoldId, out var scaffold);
				string detail = "after-proof=true; accepted=" + __result + "; failure=" + Failure
					+ "; tick=" + The.Game.TimeTicks + "; job=" + Job.Id + "; phase=" + Job.Phase
					+ "; physical=" + Job.PhysicalPhase + "; projection=" + Job.Projection
					+ "; subject=" + Job.SubjectId + "; output=" + Job.OutputId
					+ "; cell=" + (cell == null ? "absent" : cell.X + "," + cell.Y)
					+ "; blueprint=" + Blueprint + "; scaffold-id=" + ScaffoldId
					+ "; scaffold-route=" + scaffoldRoute + "; improvement-route=" + improvement
					+ "; admitted-phase=" + KingdomConstructionRules.ScaffoldRemovalPhaseAdmitted(Job.Phase,
						Job.PhysicalPhase, improvement, r_KingdomScaffold.HasRemovalProof(Successor, ScaffoldId))
					+ "; owns=" + KingdomConstruction.Owns(System, Z, Job)
					+ "; current=" + KingdomConstruction.IsCurrent(Job)
					+ "; pending=" + pending
					+ "; exact-intent=" + r_KingdomScaffold.HasExactScaffoldRemovalIntent(Successor, ScaffoldId)
					+ "; exact-successor=" + r_KingdomScaffold.IsExactSuccessor(Successor, Z, cell, Job, Blueprint)
					+ "; final-build-truth=" + KingdomConstruction.FinalBuildTruthMatches(Successor, Job)
					+ "; output-lookup=" + outputState + "; same-output=" + ReferenceEquals(output, Successor)
					+ "; scaffold-lookup=" + scaffoldState + "; same-scaffold=" + ReferenceEquals(scaffold, ExpectedPredecessor)
					+ "; expected-predecessor=" + Body(ExpectedPredecessor)
					+ "; successor=" + Body(Successor)
					+ "; intent-schema=" + Property(Successor, r_KingdomScaffold.ScaffoldRemovalIntentSchemaProperty)
					+ "; intent-id=" + Property(Successor, r_KingdomScaffold.ScaffoldRemovalIntentIdProperty)
					+ "; removal-proof=" + Property(Successor, r_KingdomScaffold.RemovalProofProperty)
					+ "; callers=" + Callers();
				KingdomLog.Log("chain scaffold removal: " + detail);
				KingdomScenarioJournal.Append("camp-heart-chain-removal", __result, detail);
				KingdomCampHeartChainRetryFault.ObserveRemoval(Job, __result);
			}
			catch (Exception error)
			{
				KingdomScenarioJournal.Append("camp-heart-chain-removal", false,
					"removal observer failed: " + error.GetType().Name + ": " + error.Message);
			}
		}

		private static string Body(GameObject Item)
		{
			if (Item == null) return "null";
			return "valid:" + GameObject.Validate(Item) + ",id:" + Item.IDIfAssigned
				+ ",blueprint:" + Item.Blueprint + ",zone:" + Item.CurrentZone?.ZoneID
				+ ",cell:" + Item.CurrentCell?.X + "," + Item.CurrentCell?.Y;
		}

		private static string Property(GameObject Item, string Key)
		{
			if (Item == null) return "null";
			return "int:" + Item.HasIntProperty(Key) + ":" + Item.GetIntProperty(Key)
				+ ",text:" + Item.HasStringProperty(Key) + ":" + Item.GetStringProperty(Key);
		}

		private static string Callers()
		{
			var names = new List<string>();
			foreach (var frame in new StackTrace(false).GetFrames())
			{
				var method = frame.GetMethod();
				if (method == null || method.DeclaringType == typeof(KingdomCampHeartChainRemovalTrace)) continue;
				names.Add(method.DeclaringType?.FullName + "." + method.Name);
				if (names.Count == 6) break;
			}
			return string.Join(" > ", names);
		}
	}
}
