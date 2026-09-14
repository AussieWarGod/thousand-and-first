using System;
using HarmonyLib;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	/// <summary>Native defensive engagement, using owned unplaced creatures without citizen identity.</summary>
	internal static class KingdomQuickstartDefensiveChecks
	{
		internal static void Probe()
		{
			GameObject civilian = null, attacker = null;
			try
			{
				// These gearless creatures isolate the engine's engagement behavior. Real founders
				// are observed separately; neither fixture is placed or enrolled in the city.
				civilian = GameObject.Create("NPC");
				attacker = GameObject.Create("NPC");
				Require(GameObject.Validate(civilian) && GameObject.Validate(attacker)
					&& civilian.CurrentCell == null && attacker.CurrentCell == null
					&& civilian.Brain != null && attacker.Brain != null,
					"defensive engagement fixtures were not fresh unplaced creatures");
				Require(!civilian.Brain.Passive && civilian.Brain.CanAcquireTarget()
					&& civilian.Brain.Target == null, "ordinary engagement counterexample refused");
				civilian.Brain.Passive = true;
				Require(!civilian.Brain.CanAcquireTarget() && civilian.Brain.CanFight(),
					"defensive engagement did not suppress acquisition while preserving combat");
				civilian.Brain.Attacked(attacker);
				Require(civilian.Brain.Passive && ReferenceEquals(civilian.Brain.Target, attacker),
					"defensive creature did not retaliate against its actual attacker");
				KingdomLog.Log("quickstart defensive probe: proactive-before=true; proactive-after=false; "
					+ "retaliation=true; synthetic-unplaced-creatures=2; synthetic-citizens=0");
			}
			finally
			{
				if (GameObject.Validate(civilian)) civilian.Obliterate(null, Silent: true);
				if (GameObject.Validate(attacker)) attacker.Obliterate(null, Silent: true);
				Require(!GameObject.Validate(civilian) && !GameObject.Validate(attacker),
					"defensive engagement fixture cleanup failed");
			}
		}

		private static void Require(bool Condition, string Failure)
		{
			if (!Condition) throw new InvalidOperationException(Failure);
		}
	}

	// Retain combat initiation evidence without changing targets, goals, damage or factions.
	[HarmonyPatch(typeof(Brain), nameof(Brain.WantToKill))]
	internal static class KingdomQuickstartCombatIntentDiagnostics
	{
		private static int Observations;

		[HarmonyPrefix]
		internal static void Before(Brain __instance, GameObject Subject, string Because, bool Directed)
		{
			GameObject actor = __instance?.ParentObject;
			if (!KingdomQuickstartBootTest.LifecycleRequested || The.Game == null || actor == null
				|| Subject == null || Observations >= 32
				|| !KingdomQuickstartRules.TryDecode(The.Game.GetStringGameState(
					KingdomQuickstartRules.ReceiptState), out var receipt)) return;
			foreach (string id in receipt.FounderObjectIds)
			{
				if (string.IsNullOrEmpty(id) || (actor.IDIfAssigned != id && Subject.IDIfAssigned != id)) continue;
				Observations++;
				KingdomLog.Log("quickstart combat intent: turn=" + The.Game.Turns
					+ "; actor=" + actor.IDIfAssigned + ":" + actor.Blueprint
					+ "; target=" + Subject.IDIfAssigned + ":" + Subject.Blueprint
					+ "; defensive=" + __instance.Passive + "; directed=" + Directed + "; reason=" + Because);
				return;
			}
		}
	}
}
