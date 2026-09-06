using System;
using XRL;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomQuickstartLoadTest
	{
		private static XRLGame Witnessed;
		private static int Attempts, BootstrapCalls;
		private static string Failure;

		internal static void BootstrapCalled(XRLGame Game)
		{
			if (KingdomScenarioLoadEntry.Armed && KingdomScenarioLoadEntry.QuickstartSnapshot != null)
				BootstrapCalls++;
		}

		internal static void BeforeActivation()
		{
			try
			{
				Attempts++;
				Check(Attempts == 1 && Witnessed == null && BootstrapCalls == 0, "load observation or bootstrap repeated");
				Check(KingdomScenarioLoadReaderWitness.Releases == 1 && !KingdomScenarioLoadReaderWitness.HadErrors,
					"primary reader did not finish exactly once without errors");
				XRLGame game = The.Game;
				KingdomQuickstartSaveState.Verify(game, KingdomScenarioLoadEntry.QuickstartSnapshot);
				Check(BootstrapCalls == 0, "bootstrap ran during restored-state verification");
				Witnessed = game;
				Check(KingdomScenarioJournal.Append("QUICKSTART-LOAD-PREACTIVATION", true,
					"exact-saved-heart-stock-and-IDs=true; before-player-GameRestored-and-activation=true") == null,
					"restored-state journal failed");
			}
			catch (Exception error)
			{
				Failure = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
				KingdomScenarioJournal.Append("QUICKSTART-LOAD-PREACTIVATION", false, Failure);
			}
		}

		internal static void VerifyLoaded(XRLGame Game)
		{
			Check(Failure == null && Attempts == 1 && ReferenceEquals(Witnessed, Game) && BootstrapCalls == 0,
				"no exact restored Quickstart witness: " + Failure);
			KingdomQuickstartSaveState.Verify(Game, KingdomScenarioLoadEntry.QuickstartSnapshot);
			Check(BootstrapCalls == 0 && KingdomScenarioLoadReaderWitness.Releases == 1
				&& !KingdomScenarioLoadReaderWitness.HadErrors, "load replayed bootstrap or recorded reader errors");
			string root = KingdomScenarioSaveFiles.Root();
			Check(KingdomScenarioSaveFiles.Same(System.IO.Path.GetFullPath(Game.GetCacheDirectory()),
				KingdomScenarioSaveFiles.SaveDirectory(root, Game.GameID)), "loaded cache ownership differs");
		}

		internal static void Finish(bool Verified)
		{
			try
			{
				Check(Verified, "actual LoadGame completion was not proved");
				VerifyLoaded(The.Game);
				Check(KingdomScenarioJournal.Append("QUICKSTART-LOAD-COMPLETE", true,
					"real-save-owned-stop-cold-load=true; graceful-quit=false; unchanged-heart-stock-and-IDs=true"
					+ "; bootstrap-replay=false; ordinary-acceptance=false") == null, "load completion journal failed");
			}
			catch (Exception error)
			{
				Failure = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
				KingdomScenarioJournal.Append("QUICKSTART-LOAD-COMPLETE", false, Failure);
			}
		}

		private static void Check(bool Condition, string Failure)
		{ KingdomScenarioSaveFiles.Require(Condition, Failure ?? "Quickstart load refused"); }
	}
}
