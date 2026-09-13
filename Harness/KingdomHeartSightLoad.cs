using System;
using HarmonyLib;
using XRL;

namespace ThousandAndFirst.Harness
{
	/// <summary>Return the verified Continue result to vanilla RunGame, then observe its real frames.</summary>
	internal static class KingdomHeartSightLoad
	{
		private static XRLGame Resuming;
		private static bool Finished;
		internal static void Prepare(XRLGame game)
		{
			KingdomHeartSightNativeProvider.RequireScript();
			Require(Resuming == null && !Finished && KingdomScenarioLoadEntry.Armed && game.Running
				&& ReferenceEquals(game, The.Game) && game.GetSystem<KingdomScenarioAutoRunner>()?.HasConsideredScript == true,
				"heart render continuation is not the exact running loaded game");
			KingdomHeartSightWitness.Compare(game);
			KingdomHeartSightWitness.StartSight();
			KingdomScenarioFrames.ArmDriver();
			var harmony = new Harmony("com.thousandandfirst.harness.heart-load-frames");
			harmony.Patch(AccessTools.Method(typeof(KingdomScenarioFrames), "Pump"),
				postfix: new HarmonyMethod(typeof(KingdomHeartSightLoad), nameof(AfterPump)));
			string result = KingdomScenarioFrames.Run("3", out bool ok);
			Require(ok && KingdomScenarioFrames.Pending, "loaded frame yield refused: " + result);
			Require(KingdomScenarioJournal.Append("heart-load-resume", true,
				"vanilla-Continue=true; session-backup=false; saved-script-considered=true; requested-frames=3") == null,
				"heart continuation journal unavailable");
			Resuming = game;
		}
		private static void AfterPump(bool __result, bool Faulted)
		{
			if (Resuming == null || Finished || __result) return;
			Finished = true;
			try
			{
				Require(!Faulted && !KingdomScenarioLoadEntry.Armed && ReferenceEquals(The.Game, Resuming)
					&& Resuming.Running && !KingdomScenarioFrames.Pending, "loaded render continuation failed or ran before release");
				string physical = KingdomHeartSightWitness.Compare(Resuming);
				string sight = KingdomHeartSightWitness.Sight();
				Require(KingdomScenarioJournal.Append("heart-load-rendered", true,
					sight + "; " + physical + "; new-game-script-replayed=false") == null, "loaded frame proof journal unavailable");
				Require(KingdomScenarioJournal.Append("SCRIPT-COMPLETE", true,
					"native-lifecycle cold-load session complete; real-save-quit-load=true; real-loaded-frames=true"
					+ "; new-game-script-replayed=false; ordinary-acceptance=false") == null, "heart load terminal journal unavailable");
			}
			catch (Exception error)
			{
				KingdomScenarioJournal.Append("SCRIPT-STOPPED", false,
					"heart cold-load render refused: " + KingdomScenarioRules.Bounded(error.Message));
			}
		}
		private static void Require(bool value, string failure) => KingdomHeartSightNativeProvider.Require(value, failure);
	}
}
