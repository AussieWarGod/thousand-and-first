using System;
using HarmonyLib;
using XRL;
using XRL.UI;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomCampHeartChainLoad
	{
		private static XRLGame WitnessedGame, Resuming;
		private static int WitnessAttempts;
		private static string WitnessFailure;
		private static bool Finished, PriorPopup;
		internal static bool OwnsPopups;

		internal static void BeforeActivation()
		{
			try
			{
				Require(++WitnessAttempts == 1 && WitnessedGame == null && KingdomScenarioLoadEntry.Armed
					&& KingdomScenarioLoadReaderWitness.Releases == 1 && !KingdomScenarioLoadReaderWitness.HadErrors,
					"higher-heart primary reader or preactivation witness is not exact");
				var game = The.Game;
				var witness = KingdomScenarioLoadEntry.ChainSnapshot;
				Require(witness != null && witness.Rung == 4 && game?.GameID == witness.GameId
					&& game.GetSystem<KingdomScenarioAutoRunner>()?.HasConsideredScript == true
					&& KingdomScenarioDurableState.ProvesExactText(KingdomScenarioSaveFiles.SnapshotKey,
						KingdomScenarioLoadEntry.SnapshotWire), "higher-heart saved script or snapshot differs before activation");
				var observed = KingdomCampHeartNativeChecks.CaptureChainSaveWitness(game, The.ZoneManager?.ActiveZone,
					witness.ResidentId, witness.Track.Id, out var records);
				KingdomCampHeartChainLoadFacts.Retain(game, "preactivation", records);
				KingdomCampHeartChainLoadFacts.RequireExact(observed, KingdomScenarioLoadEntry.SnapshotWire);
				game.GetSystem<KingdomScenarioAutoRunner>().RearmLoadedChainInput(game);
				Require(KingdomScenarioJournal.Append("camp-heart-chain-preactivation", true,
					"before-AfterGameLoaded=true; " + KingdomCampHeartChainLoadFacts.Identity(observed)
					+ "; snapshot-sha256=" + KingdomScenarioSaveFiles.HashText(KingdomScenarioLoadEntry.SnapshotWire)) == null,
					"higher-heart preactivation journal unavailable");
				WitnessedGame = game;
			}
			catch (Exception error)
			{
				WitnessFailure = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
				KingdomScenarioJournal.Append("camp-heart-chain-preactivation", false, WitnessFailure);
			}
		}

		internal static void Prepare(XRLGame Game, bool OriginalPopup)
		{
			Require(Resuming == null && !Finished && KingdomScenarioLoadEntry.Armed && Game.Running
				&& WitnessAttempts == 1 && WitnessFailure == null && ReferenceEquals(WitnessedGame, Game)
				&& ReferenceEquals(The.Game, Game) && Game.GetSystem<KingdomScenarioAutoRunner>()?.HasConsideredScript == true
				&& ReferenceEquals(KingdomScenarioAutoRunner.ChainInputOwner(), Game.GetSystem<KingdomScenarioAutoRunner>())
				&& !KingdomScenarioAdvance.Pending && !KingdomScenarioFrames.Pending
				&& KingdomScenarioScript.TryRead(out var script, out _) && KingdomCampHeartChainScript.Matches(script, true),
				"higher-heart continuation is not the exact loaded save scenario");
			Require(!KingdomScenarioLoadReaderWitness.HadErrors, "engine reported higher-heart deserialization errors");
			string result = KingdomCampHeartNativeChecks.BeginLoadedChain(Game, KingdomScenarioLoadEntry.ChainSnapshot);
			Require(KingdomScenarioJournal.Append("camp-heart-chain-next", true, result) == null,
				"higher-heart next paid job journal unavailable");
			KingdomScenarioAdvance.ArmDriver();
			new Harmony("com.thousandandfirst.harness.heart-chain-load-turns").Patch(
				AccessTools.Method(typeof(KingdomScenarioAdvance), "Pump"),
				postfix: new HarmonyMethod(typeof(KingdomCampHeartChainLoad), nameof(AfterPump)));
			result = KingdomScenarioAdvance.Run("3600", out bool ok);
			Require(ok && KingdomScenarioAdvance.Pending, "loaded higher-heart ordinary turns refused: " + result);
			Require(KingdomScenarioJournal.Append("advance", true, result) == null,
				"higher-heart ordinary wait intent could not be journalled");
			Require(KingdomScenarioJournal.Append("camp-heart-chain-resume", true,
				"vanilla-Continue=true; saved-script-considered=true; requested-turns=3600") == null,
				"higher-heart resume journal unavailable");
			Resuming = Game; PriorPopup = OriginalPopup; OwnsPopups = true; Popup.Suppress = true;
		}

		private static void AfterPump(bool __result, bool Faulted)
		{
			if (Resuming == null || Finished || __result) return;
			Finished = true;
			try
			{
				try
				{
					Require(!Faulted && !KingdomScenarioLoadEntry.Armed && ReferenceEquals(The.Game, Resuming)
						&& Resuming.Running && !KingdomScenarioAdvance.Pending, "higher-heart wait failed or preceded Continue release");
					string result = KingdomCampHeartNativeChecks.CompleteLoadedChain(Resuming, KingdomScenarioLoadEntry.ChainSnapshot);
					Require(KingdomScenarioJournal.Append("camp-heart-chain-completed", true, result) == null,
						"higher-heart completion journal unavailable");
				}
				finally
				{
					if (OwnsPopups) Popup.Suppress = PriorPopup;
					OwnsPopups = false;
				}
				Require(Popup.Suppress == PriorPopup, "higher-heart continuation did not restore popup state");
				Require(KingdomScenarioJournal.Append("SCRIPT-COMPLETE", true,
					"native-heart-chain cold-load complete; real-save-quit-load=true; next-paid-job-complete=true"
					+ "; new-game-script-replayed=false; popup-restored=true; ordinary-acceptance=false") == null,
					"higher-heart terminal journal unavailable");
			}
			catch (Exception error)
			{
				KingdomScenarioJournal.Append("SCRIPT-STOPPED", false,
					"higher-heart cold-load refused: " + KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message));
			}
		}
		private static void Require(bool Value, string Failure) => KingdomCampHeartNativeProvider.Require(Value, Failure);
	}
}
