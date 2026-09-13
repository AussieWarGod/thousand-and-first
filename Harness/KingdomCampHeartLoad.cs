using System;
using HarmonyLib;
using XRL;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomCampHeartLoad
	{
		private static XRLGame Resuming;
		private static bool Finished;
		internal static void Prepare(XRLGame Game)
		{
			Require(Resuming == null && !Finished && KingdomScenarioLoadEntry.Armed && Game.Running
				&& ReferenceEquals(Game, The.Game) && Game.GetSystem<KingdomScenarioAutoRunner>()?.HasConsideredScript == true
				&& !KingdomScenarioAdvance.Pending && !KingdomScenarioFrames.Pending
				&& KingdomScenarioScript.TryRead(out var script, out _) && KingdomCampHeartScript.Matches(script, true),
				"camp continuation is not the exact running loaded scenario");
			Require(!KingdomScenarioLoadReaderWitness.HadErrors, "engine reported camp deserialization errors");
			string result = KingdomCampHeartNativeChecks.BeginLoaded(Game, KingdomScenarioLoadEntry.CampSnapshot);
			Require(KingdomScenarioJournal.Append("camp-heart-next", true, result) == null, "camp next-job journal unavailable");
			KingdomScenarioAdvance.ArmDriver();
			new Harmony("com.thousandandfirst.harness.camp-load-turns").Patch(
				AccessTools.Method(typeof(KingdomScenarioAdvance), "Pump"),
				postfix: new HarmonyMethod(typeof(KingdomCampHeartLoad), nameof(AfterPump)));
			result = KingdomScenarioAdvance.Run("3600", out bool ok);
			Require(ok && KingdomScenarioAdvance.Pending, "loaded camp advance refused: " + result);
			Require(KingdomScenarioJournal.Append("camp-heart-resume", true,
				"vanilla-Continue=true; saved-script-considered=true; requested-turns=3600") == null,
				"camp continuation journal unavailable");
			Resuming = Game;
		}

		private static void AfterPump(bool __result, bool Faulted)
		{
			if (Resuming == null || Finished || __result) return;
			Finished = true;
			try
			{
				Require(!Faulted && !KingdomScenarioLoadEntry.Armed && ReferenceEquals(The.Game, Resuming)
					&& Resuming.Running && !KingdomScenarioAdvance.Pending, "loaded camp advance failed or preceded Continue release");
				string result = KingdomCampHeartNativeChecks.CompleteLoaded(Resuming, KingdomScenarioLoadEntry.CampSnapshot);
				Require(KingdomScenarioJournal.Append("camp-heart-completed", true, result) == null, "camp completion journal unavailable");
				Require(KingdomScenarioJournal.Append("SCRIPT-COMPLETE", true,
					"native-camp-heart cold-load complete; real-save-quit-load=true; next-paid-job-complete=true"
					+ "; new-game-script-replayed=false; ordinary-acceptance=false") == null, "camp terminal journal unavailable");
			}
			catch (Exception error)
			{
				KingdomScenarioJournal.Append("SCRIPT-STOPPED", false,
					"camp cold-load continuation refused: " + KingdomScenarioRules.Bounded(error.Message));
			}
		}
		private static void Require(bool Value, string Failure) => KingdomCampHeartNativeProvider.Require(Value, Failure);
	}
}
