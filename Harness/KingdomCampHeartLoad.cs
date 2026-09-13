using System;
using HarmonyLib;
using XRL;
using XRL.UI;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomCampHeartLoad
	{
		private static XRLGame Resuming;
		private static bool Finished;
		private static XRLGame WitnessedGame;
		private static int WitnessAttempts;
		private static string WitnessFailure;
		private static bool PriorPopup;
		internal static bool OwnsPopups;

		internal static void BeforeActivation()
		{
			try
			{
				Require(++WitnessAttempts == 1 && WitnessedGame == null && KingdomScenarioLoadEntry.Armed
					&& KingdomScenarioLoadReaderWitness.Releases == 1 && !KingdomScenarioLoadReaderWitness.HadErrors,
					"camp primary reader or preactivation witness is not exact");
				XRLGame game = The.Game;
				Require(game?.GetSystem<KingdomScenarioAutoRunner>()?.HasConsideredScript == true
					&& KingdomScenarioDurableState.ProvesExactText(KingdomScenarioSaveFiles.SnapshotKey,
						KingdomScenarioLoadEntry.SnapshotWire), "camp saved script or snapshot changed before activation");
				var observed = KingdomCampHeartNativeChecks.CaptureSaveWitness(game, The.ZoneManager?.ActiveZone);
				Require(KingdomCampHeartSaveSnapshotCodec.TryEncode(observed, out string wire)
					&& wire == KingdomScenarioLoadEntry.SnapshotWire, "camp state changed before activation");
				Require(KingdomScenarioJournal.Append("camp-heart-preactivation", true,
					"before-AfterGameLoaded=true; snapshot-sha256=" + KingdomScenarioSaveFiles.HashText(wire)
					+ "; heart=" + observed.HeartId + "; store=" + observed.StoreId + "; fire=" + observed.FireId
					+ "; tent-job=" + observed.TentJobId + "; time-ticks=" + observed.TimeTicks) == null,
					"camp preactivation journal unavailable");
				WitnessedGame = game;
			}
			catch (Exception error)
			{
				WitnessFailure = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
				KingdomScenarioJournal.Append("camp-heart-preactivation", false, WitnessFailure);
			}
		}

		internal static void Prepare(XRLGame Game, bool OriginalPopup)
		{
			Require(Resuming == null && !Finished && KingdomScenarioLoadEntry.Armed && Game.Running
				&& WitnessAttempts == 1 && WitnessFailure == null && ReferenceEquals(WitnessedGame, Game)
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
			PriorPopup = OriginalPopup;
			OwnsPopups = true;
			Popup.Suppress = true;
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
			finally
			{
				if (OwnsPopups) Popup.Suppress = PriorPopup;
				OwnsPopups = false;
			}
		}
		private static void Require(bool Value, string Failure) => KingdomCampHeartNativeProvider.Require(Value, Failure);
	}
}
