using System;
using System.IO;
using XRL;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// The cold-load session for an unfounded save (#272, #271): a separate fresh profile loads
	/// the exact save the unfounded session wrote, proves the dormant lifecycle and carry books
	/// survived byte for byte both before and after activation, founds the first city through the
	/// production founding transaction, and writes a second real save. The witness is compared,
	/// never applied; any difference is a refusal.
	/// </summary>
	internal static class KingdomUnfoundedLoad
	{
		internal const string PreactivationRow = "unfounded-preactivation";
		internal const string LoadedRow = "unfounded-loaded";
		internal const string FoundedRow = "unfounded-founded";
		internal const string ResavedRow = "unfounded-resaved";
		private static XRLGame Witnessed;
		private static int Attempts;
		private static string Failure;

		internal static void BeforeActivation()
		{
			try
			{
				Attempts++;
				Check(Attempts == 1 && Witnessed == null, "unfounded pre-activation witness repeated");
				Check(KingdomScenarioLoadEntry.Armed && KingdomScenarioLoadReaderWitness.Releases == 1
					&& !KingdomScenarioLoadReaderWitness.HadErrors,
					"primary reader did not finish exactly once without errors");
				XRLGame game = The.Game;
				string observed = Compare(game, KingdomScenarioLoadEntry.UnfoundedSnapshot);
				Witnessed = game;
				Check(KingdomScenarioJournal.Append(PreactivationRow, true,
					"before-AfterGameLoaded=true; " + observed) == null, "unfounded pre-activation journal failed");
			}
			catch (Exception error)
			{
				Failure = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
				KingdomScenarioJournal.Append(PreactivationRow, false, Failure);
			}
		}

		internal static void VerifyLoaded(XRLGame Game, KingdomUnfoundedSaveSnapshot Snapshot, string ImportedPrimary)
		{
			Check(Failure == null && Attempts == 1 && ReferenceEquals(Witnessed, Game),
				"no exact unfounded pre-activation witness: " + Failure);
			Check(KingdomScenarioLoadReaderWitness.Releases == 1 && !KingdomScenarioLoadReaderWitness.HadErrors,
				"engine reported deserialization errors");
			string root = KingdomScenarioSaveFiles.Root();
			string directory = KingdomScenarioSaveFiles.SaveDirectory(root, Game.GameID);
			Check(KingdomScenarioSaveFiles.Same(Path.GetFullPath(Game.GetCacheDirectory()), directory),
				"loaded cache ownership differs");
			Journal(LoadedRow, "cold-load=true; after-activation=true; " + Compare(Game, Snapshot));
			Zone zone = The.ZoneManager?.ActiveZone;
			Check(zone != null && zone.ZoneID == Snapshot.ZoneId && ReferenceEquals(The.Player?.CurrentZone, zone),
				"the loaded player is not on the saved ground");
			KingdomSystem system = KingdomNativeCampFounding.Found(Game, zone, Check);
			Journal(FoundedRow, "production-founding=true; " + Founded(system)
				+ "; faction=" + system.KingdomFactionName);
			string primary = KingdomUnfoundedSave.SavePrimary(Game, directory);
			string backup = Path.Combine(directory, "Primary.sav.gz.bak");
			Check(File.Exists(backup) && KingdomScenarioSaveFiles.HashFile(backup, KingdomScenarioSaveFiles.MaxSaveBytes)
				== ImportedPrimary, "the engine backup is not the imported unfounded save");
			Check(primary != ImportedPrimary, "the second save did not replace the imported primary");
			int owned = 0;
			foreach (string path in Directory.GetFiles(directory))
				if (Path.GetFileName(path).StartsWith("Primary", StringComparison.Ordinal)) owned++;
			Check(owned == 3 && File.Exists(Path.Combine(directory, "Primary.json")),
				"the second save left other than primary, metadata and one backup");
			Journal(ResavedRow, "real-save=true; " + Founded(system) + "; save-error=false"
				+ "; backup-is-imported-save=true; primary-changed=true; second-save-reload=false"
				+ "; ordinary-acceptance=false");
			Journal("SCRIPT-COMPLETE", "native-unfounded cold-load session complete; real-save-quit-load=true"
				+ "; production-founding=true; second-real-save=true; new-game-script-replayed=false"
				+ "; ordinary-acceptance=false");
		}

		private static string Compare(XRLGame Game, KingdomUnfoundedSaveSnapshot Snapshot)
		{
			Check(Game != null && Snapshot != null && ReferenceEquals(The.Game, Game) && Game.GameID == Snapshot.GameId,
				"the loaded game is not the saved game");
			KingdomSystem system = Game.GetSystem<KingdomSystem>();
			KingdomUnfoundedSave.RequirePristine(system, "after the cold load");
			byte[] lifecycle = KingdomUnfoundedSave.Lifecycle(system.LifecycleBook);
			Check(KingdomUnfoundedSave.Frame(lifecycle) == Snapshot.LifecycleFrame
				&& KingdomUnfoundedSave.Sha(lifecycle) == Snapshot.LifecycleSha256,
				"the loaded dormant lifecycle book differs from the saved one");
			return "founded=false; load-failed=false; pristine-lifecycle-and-carry=true; lifecycle-frame="
				+ Snapshot.LifecycleFrame + "; lifecycle-sha256=" + Snapshot.LifecycleSha256
				+ "; carry-sha256=" + Snapshot.CarrySha256 + "; game-id=" + Game.GameID;
		}

		/// <summary>A founded book holds growth authority and keeps the current lifecycle frame.</summary>
		private static string Founded(KingdomSystem System)
		{
			Check(System != null && System.Founded && !System.LoadFailed
				&& KingdomLifecycleRules.CanOwnGrowthAuthority(System.LifecycleBook),
				"the founded realm lacks lifecycle growth authority");
			int frame = KingdomUnfoundedSave.Frame(KingdomUnfoundedSave.Lifecycle(System.LifecycleBook));
			Check(frame == KingdomLifecycleRules.CurrentFormatVersion, "the founded lifecycle book left the current frame");
			return "founded=true; lifecycle-frame=" + frame + "; growth-authority=true";
		}

		private static void Journal(string Row, string Message)
		{ Check(KingdomScenarioJournal.Append(Row, true, Message) == null, Row + " journal failed"); }

		private static void Check(bool Condition, string Failure)
		{ KingdomScenarioSaveFiles.Require(Condition, Failure ?? "unfounded cold load refused"); }
	}
}
