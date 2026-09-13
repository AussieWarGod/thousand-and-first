using System;
using System.IO;
using XRL;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		internal static string Save(XRLGame Game)
		{
			Require(Retained != null && Retained.Done && !Retained.Saved
				&& ReferenceEquals(Game, Retained.Game) && ReferenceEquals(Game, The.Game)
				&& !KingdomScenarioAdvance.Pending && !KingdomScenarioFrames.Pending
				&& Game.GetStringGameState(KingdomCampHeartNativeProvider.Receipt, "")
					.StartsWith("native-camp-heart cases=1 passed=1 failed=0;", StringComparison.Ordinal),
				"the exact successful paid camp attempt is absent or already saved");
			Retained.Saved = true;
			return Retained.SaveCompletedCamp();
		}

		private sealed partial class Frame
		{
			internal bool Saved;
			internal string SaveCompletedCamp()
			{
				Require(Game.Running && !Game.Transient && !Game.DontSaveThisIsAReplay
					&& (Game.SaveTask == null || Game.SaveTask.IsCompleted), "the camp cannot save now");
				string root = KingdomScenarioSaveFiles.Root();
				string directory = KingdomScenarioSaveFiles.SaveDirectory(root, Game.GameID);
				Require(!KingdomScenarioSaveFiles.LoadPresent()
					&& !File.Exists(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile))
					&& !File.Exists(Path.Combine(root, KingdomScenarioSaveFiles.SnapshotFile))
					&& !KingdomNativeRegressionContext.HasAnyState(Game, KingdomScenarioSaveFiles.SnapshotKey)
					&& Directory.GetDirectories(directory).Length == 0, "camp save evidence is not empty");
				foreach (string path in Directory.GetFiles(directory))
					Require(Path.GetFileName(path) == "Cache.db", "camp save already has primary or backup evidence");
				RequireStoreIdentity();
				var extra = new global::System.Collections.Generic.List<string>();
				Mint(KingdomMaterial.Timber, 1, extra);
				var witness = CaptureSaveWitness(Game, Zone);
				Require(witness.TimberId == extra[0] && witness.UpgradeJobId == JobId
					&& witness.BrushDigest == KingdomCampHeartSaveSnapshotCodec.CustodyDigest(RetainedBrush),
					"the save witness does not preserve the paid upgrade and unspent brush");
				Require(KingdomCampHeartSaveSnapshotCodec.TryEncode(witness, out string wire), "camp snapshot could not encode");
				Game.SetStringGameState(KingdomScenarioSaveFiles.SnapshotKey, wire);
				Require(KingdomScenarioDurableState.ProvesExactText(KingdomScenarioSaveFiles.SnapshotKey, wire),
					"the camp snapshot did not publish exactly");
				KingdomScenarioSaveFiles.WriteNew(Path.Combine(root, KingdomScenarioSaveFiles.SnapshotFile), wire);
				The.ZoneManager.CheckCached(true, true);
				Game.SaveGame("Primary")?.GetAwaiter().GetResult();
				Require(ReferenceEquals(The.Game, Game) && Game.Turns == witness.Turns,
					"the camp changed across serialization");
				string primary = Path.Combine(directory, "Primary.sav.gz");
				using (var file = KingdomScenarioSaveFiles.Open(primary, KingdomScenarioSaveFiles.MaxSaveBytes))
					Require(file.ReadByte() == 31 && file.ReadByte() == 139, "camp primary is not gzip");
				string receipt = "taf-scenario-save-v1\n" + Game.GameID + "\n"
					+ KingdomScenarioSaveFiles.HashFile(primary, KingdomScenarioSaveFiles.MaxSaveBytes) + "\n"
					+ KingdomScenarioSaveFiles.HashFile(Path.Combine(directory, "Primary.json"), 1048576) + "\n"
					+ KingdomScenarioSaveFiles.HashText(wire) + "\n";
				KingdomScenarioSaveFiles.WriteNew(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile), receipt);
				Require(KingdomScenarioSaveFiles.ReadText(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile), 512) == receipt,
					"camp save receipt changed");
				return "paid-camp-save=true; rung=2; synthetic-next-job-timber=1; brush=23; save="
					+ Game.GameID + "; heart=" + witness.HeartId + "; store=" + witness.StoreId
					+ "; fire=" + witness.FireId + "; snapshot-sha256=" + KingdomScenarioSaveFiles.HashText(wire);
			}
		}
	}
}
