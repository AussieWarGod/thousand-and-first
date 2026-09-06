using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using XRL;
using XRL.Core;
using XRL.UI;

namespace ThousandAndFirst.Harness
{
	[HarmonyPatch(typeof(XRLCore), "NewGame", new Type[0])]
	internal static class KingdomQuickstartSaveTest
	{
		private static readonly TaskCompletionSource<bool> Parked = new TaskCompletionSource<bool>();
		private static XRLGame Claimed;
		private static string CacheDirectory, OwnedDirectory, Root, GameId;
		private static int BootstrapCalls;
		private static bool Saving, SaveError;

		internal static void BootstrapCalled(XRLGame Game)
		{ if (Saving) BootstrapCalls++; }

		internal static void NoteSaveError(XRLGame Game)
		{ if (Saving && ReferenceEquals(Game, Claimed)) SaveError = true; }

		internal static void VerifyCache(XRLGame Game)
		{
			if (!Saving || !ReferenceEquals(Game, Claimed)) return;
			Check(CacheDirectory != null && ReferenceEquals(The.Game, Game) && Game.GameID == GameId
				&& Game._CacheDirectory == CacheDirectory && KingdomScenarioSaveFiles.Root() == Root
				&& KingdomScenarioSaveFiles.Same(Path.GetFullPath(CacheDirectory), OwnedDirectory)
				&& KingdomScenarioSaveFiles.Same(KingdomScenarioSaveFiles.SaveDirectory(Root, GameId), OwnedDirectory),
				"actual save destination left its exact owned profile");
		}

		[HarmonyPostfix, HarmonyPriority(Priority.Last)]
		internal static void Postfix(XRLCore __instance, XRLGame __result)
		{
			XRLGame candidate = __instance?.Game;
			if (!KingdomQuickstartBootTest.ClaimsSave(candidate)) return;
			if (!ReferenceEquals(The.Core, __instance)
				|| Thread.CurrentThread != XRLCore.CoreThread || Claimed != null)
			{
				KingdomScenarioJournal.Append("QUICKSTART-SAVE-COMPLETE", false, "unowned save dispatch refused");
				return;
			}
			Claimed = candidate;
			bool priorPopup = Popup.Suppress;
			string failure = null;
			KingdomQuickstartSaveSnapshot snapshot = null;
			try
			{
				Saving = true;
				if (!priorPopup) Popup.Suppress = true;
				Check(ReferenceEquals(__result, Claimed), "owned boot returned no exact successful game");
				snapshot = Save(__result);
			}
			catch (Exception error) { failure = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message); }
			finally
			{
				if (!priorPopup && Popup.Suppress) Popup.Suppress = false;
			}
			try
			{
				Check(failure == null && snapshot != null && BootstrapCalls == 0 && !SaveError, failure);
				KingdomQuickstartBootTest.VerifyForSave(__result, out _, out _);
				KingdomQuickstartSaveState.Verify(__result, snapshot);
				VerifyCache(__result);
				Check(BootstrapCalls == 0 && !SaveError && Popup.Suppress == priorPopup,
					"saved-world terminal state or popup ownership changed");
			}
			catch (Exception error) { failure = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message); }
			finally { Saving = false; }
			KingdomScenarioJournal.Append("QUICKSTART-SAVE-COMPLETE", failure == null,
				failure ?? "real-save=true; exact-owner-and-stock=true; no-bootstrap-replay=true; cold-load=false; ordinary-acceptance=false");
			// Core.NewGame's caller cannot reach RunGame. Only the exact owned process stop ends this fixture.
			Parked.Task.GetAwaiter().GetResult();
		}

		private static KingdomQuickstartSaveSnapshot Save(XRLGame Game)
		{
			KingdomQuickstartBootTest.VerifyForSave(Game, out string seed, out var request);
			string root = KingdomScenarioSaveFiles.Root();
			Check(!KingdomScenarioSaveFiles.LoadPresent()
				&& !File.Exists(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile))
				&& !Directory.Exists(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile))
				&& !File.Exists(Path.Combine(root, KingdomScenarioSaveFiles.SnapshotFile))
				&& !Directory.Exists(Path.Combine(root, KingdomScenarioSaveFiles.SnapshotFile)), "save evidence is not fresh");
			Check(Game.Running && !Game.Transient && !Game.DontSaveThisIsAReplay
				&& (Game.SaveTask == null || Game.SaveTask.IsCompleted), "game cannot perform a real save");
			string directory = KingdomScenarioSaveFiles.SaveDirectory(root, Game.GameID);
			Check(Game._CacheDirectory != null
				&& KingdomScenarioSaveFiles.Same(Path.GetFullPath(Game._CacheDirectory), directory),
				"actual save cache directory is not the exact owned destination");
			CacheDirectory = Game._CacheDirectory; OwnedDirectory = directory; Root = root; GameId = Game.GameID;
			VerifyCache(Game);
			Check(Directory.GetDirectories(directory).Length == 0, "fresh save contains unexpected directories");
			foreach (string path in Directory.GetFiles(directory))
				Check(Path.GetFileName(path) == "Cache.db", "fresh game contains previous save evidence");
			KingdomQuickstartSaveSnapshot snapshot = KingdomQuickstartSaveState.Capture(Game, seed, request);
			Check(KingdomQuickstartSaveSnapshotCodec.TryEncode(snapshot, out string wire), "save snapshot cannot encode");
			KingdomScenarioSaveFiles.WriteNew(Path.Combine(root, KingdomScenarioSaveFiles.SnapshotFile), wire);
			Check(KingdomScenarioJournal.Append("QUICKSTART-SAVE-BEGIN", true,
				"genuine-world=true; game-id=" + Game.GameID + "; synthetic-state=false") == null, "save intent journal failed");
			VerifyCache(Game);
			Task task = Game.SaveGame("Primary");
			task?.GetAwaiter().GetResult();
			VerifyCache(Game);
			Check(!SaveError && BootstrapCalls == 0, "save error or bootstrap replay observed");
			KingdomQuickstartBootTest.VerifyForSave(Game, out _, out _);
			KingdomQuickstartSaveState.Verify(Game, snapshot);
			Check(Directory.GetFiles(directory).Length == 3 && Directory.GetDirectories(directory).Length == 0
				&& File.Exists(Path.Combine(directory, "Cache.db")), "save lacks exactly three fresh artifacts");
			string primary = Path.Combine(directory, "Primary.sav.gz");
			using (FileStream file = KingdomScenarioSaveFiles.Open(primary, KingdomScenarioSaveFiles.MaxSaveBytes))
				Check(file.ReadByte() == 31 && file.ReadByte() == 139, "primary save is not gzip");
			string primaryHash = KingdomScenarioSaveFiles.HashFile(primary, KingdomScenarioSaveFiles.MaxSaveBytes);
			string infoHash = KingdomScenarioSaveFiles.HashFile(Path.Combine(directory, "Primary.json"), 1048576);
			string receipt = "taf-scenario-save-v1\n" + Game.GameID + "\n" + primaryHash + "\n" + infoHash
				+ "\n" + KingdomScenarioSaveFiles.HashText(wire) + "\n";
			KingdomScenarioSaveFiles.WriteNew(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile), receipt);
			Check(KingdomScenarioSaveFiles.ReadText(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile), 512) == receipt
				&& KingdomScenarioSaveFiles.ReadText(Path.Combine(root, KingdomScenarioSaveFiles.SnapshotFile),
					KingdomQuickstartSaveSnapshotCodec.MaxWireChars) == wire, "save evidence did not persist exactly");
			return snapshot;
		}

		private static void Check(bool Condition, string Failure)
		{ KingdomScenarioSaveFiles.Require(Condition, Failure ?? "Quickstart save refused"); }
	}

	[HarmonyPatch(typeof(XRLGame), "SaveGameError")]
	internal static class KingdomQuickstartSaveErrorWitness
	{
		[HarmonyPrefix]
		internal static void Prefix(XRLGame __instance) { KingdomQuickstartSaveTest.NoteSaveError(__instance); }
	}

	[HarmonyPatch(typeof(XRLGame), "GetCacheDirectory", new Type[] { typeof(string) })]
	internal static class KingdomQuickstartSaveCacheBoundary
	{
		[HarmonyPrefix, HarmonyPriority(Priority.First)]
		internal static void Prefix(XRLGame __instance) { KingdomQuickstartSaveTest.VerifyCache(__instance); }
	}
}
