using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using XRL;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// #272/#271 native witness: one real engine save of a world that has never founded a city.
	/// The live lifecycle and carry books must be the pristine constructor-default dormant
	/// books, the engine must finish the save without SaveGameError, and an in-memory write of
	/// the same lifecycle book must choose the growth-free v5 frame. The receipt and snapshot
	/// let Tools/prepare-scenario-load.py carry this exact save into a separate fresh profile.
	/// </summary>
	[KingdomScenarioVerbProvider]
	public sealed class KingdomUnfoundedSaveProvider : IKingdomScenarioVerbProvider
	{
		internal const string Verb = "unfounded-save";
		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }
		public IEnumerable<string> ScenarioVerbs { get { return new[] { Verb }; } }

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			if (Verb != KingdomUnfoundedSaveProvider.Verb || !string.IsNullOrEmpty(Argument))
				return "unfounded-save takes no arguments";
			try
			{
				string report = KingdomUnfoundedSave.SaveFresh(The.Game);
				Ok = true;
				return report;
			}
			catch (Exception error)
			{
				return "unfounded-save refused; evidence retained; "
					+ KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
			}
		}
	}

	internal static class KingdomUnfoundedSave
	{
		private static XRLGame Claimed;
		private static bool Saving, SaveError;

		/// <summary>Called by the shared SaveGameError witness in KingdomQuickstartSaveTest.cs.</summary>
		internal static void NoteSaveError(XRLGame Game)
		{ if (Saving && ReferenceEquals(Game, Claimed)) SaveError = true; }

		internal static string SaveFresh(XRLGame Game)
		{
			Check(Game != null && ReferenceEquals(The.Game, Game), "requires the exact running game");
			Check(KingdomScenarioScript.TryRead(out IList<string> script, out string scriptFailure)
				&& script.Count == 3 && script[0] == "stagedigest"
				&& script[1] == KingdomUnfoundedSaveProvider.Verb && script[2] == "stagedigest",
				"requires the exact sealed script stagedigest, unfounded-save, stagedigest");
			KingdomSystem system = Game.GetSystem<KingdomSystem>();
			// No lifecycle write before the engine save: on a writer without the #272 fix the
			// engine save itself must be what refuses, with the SaveGameError witness armed.
			RequireDormant(system, "before the save");
			Zone zone = The.ZoneManager?.ActiveZone;
			Check(zone != null && ReferenceEquals(The.Player?.CurrentZone, zone),
				"the player is not on the exact active zone");
			string root = KingdomScenarioSaveFiles.Root();
			Check(!KingdomScenarioSaveFiles.LoadPresent() && Absent(root, KingdomScenarioSaveFiles.ReceiptFile)
				&& Absent(root, KingdomScenarioSaveFiles.SnapshotFile), "save evidence is not fresh");
			string directory = KingdomScenarioSaveFiles.SaveDirectory(root, Game.GameID);
			Check(Directory.GetDirectories(directory).Length == 0, "fresh save contains unexpected directories");
			foreach (string path in Directory.GetFiles(directory))
				Check(Path.GetFileName(path) == "Cache.db", "fresh game contains previous save evidence");
			string carry = Sha(Carry(system.CarryBook));
			long ticks = Game.TimeTicks;
			string primaryHash = SavePrimary(Game, directory);
			Check(Directory.GetFiles(directory).Length == 3 && Directory.GetDirectories(directory).Length == 0
				&& File.Exists(Path.Combine(directory, "Cache.db")), "save lacks exactly three fresh artifacts");
			Check(ReferenceEquals(The.Game, Game) && Game.TimeTicks == ticks
				&& ReferenceEquals(The.ZoneManager?.ActiveZone, zone), "the save moved the game, clock or ground");
			RequirePristine(system, "after the save");
			byte[] lifecycle = Lifecycle(system.LifecycleBook);
			Check(Frame(lifecycle) == KingdomLifecycleRules.LegacyLifecycleFormatVersion,
				"the live dormant book did not choose the growth-free v5 frame");
			KingdomUnfoundedSaveSnapshot snapshot = new KingdomUnfoundedSaveSnapshot(Game.GameID, zone.ZoneID,
				Frame(lifecycle), Sha(lifecycle), carry, ticks);
			Check(KingdomUnfoundedSaveSnapshot.TryEncode(snapshot, out string wire), "save snapshot cannot encode");
			KingdomScenarioSaveFiles.WriteNew(Path.Combine(root, KingdomScenarioSaveFiles.SnapshotFile), wire);
			string infoHash = KingdomScenarioSaveFiles.HashFile(Path.Combine(directory, "Primary.json"), 1048576);
			string receipt = "taf-scenario-save-v1\n" + Game.GameID + "\n" + primaryHash + "\n" + infoHash
				+ "\n" + KingdomScenarioSaveFiles.HashText(wire) + "\n";
			KingdomScenarioSaveFiles.WriteNew(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile), receipt);
			Check(KingdomScenarioSaveFiles.ReadText(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile), 512) == receipt
				&& KingdomScenarioSaveFiles.ReadText(Path.Combine(root, KingdomScenarioSaveFiles.SnapshotFile),
					KingdomUnfoundedSaveSnapshot.MaxWireChars) == wire, "save evidence did not persist exactly");
			return "real-save=true dormant-frame=5; founded=false; pristine-lifecycle-and-carry=true; game-id="
				+ Game.GameID + "; lifecycle-bytes=" + lifecycle.Length + "; lifecycle-sha256="
				+ snapshot.LifecycleSha256 + "; save-error=false; cold-load=false; ordinary-acceptance=false";
		}

		/// <summary>The founding-ready shape every new game has: KingdomSystem present and loaded,
		/// no realm founded, the production pristine (dormant, unquarantined) lifecycle book and
		/// the constructor-default carry book. Writes no lifecycle bytes. It uses only predicates
		/// that predate the #272 writer change, so the same harness bytes detect the defect on a
		/// build without the fix; with the fix, the later frame-5 check is the writer's own
		/// dormant admission.</summary>
		internal static void RequireDormant(KingdomSystem System, string When)
		{
			Check(System != null, "requires a KingdomSystem " + When);
			Check(!System.LoadFailed, "the KingdomSystem reports LoadFailed " + When);
			Check(!System.Founded, "a realm is already founded " + When);
			Check(KingdomLifecycleRules.IsPristineMasterResumeLifecycle(System.LifecycleBook),
				"the lifecycle book is not the dormant pristine shape " + When);
			Check(Sha(Carry(System.CarryBook)) == Sha(Carry(new KingdomCarryBook())),
				"the carry book is not the pristine constructor default " + When);
		}

		/// <summary>RequireDormant plus lifecycle bytes equal to a constructor-default book's.</summary>
		internal static void RequirePristine(KingdomSystem System, string When)
		{
			RequireDormant(System, When);
			Check(Sha(Lifecycle(System.LifecycleBook)) == Sha(Lifecycle(new KingdomLifecycleBook())),
				"the lifecycle book bytes are not the pristine dormant image " + When);
		}

		/// <summary>One real engine save of the exact owned game into its owned save directory,
		/// observed by the shared SaveGameError witness. Returns the primary save's SHA-256.</summary>
		internal static string SavePrimary(XRLGame Game, string Owned)
		{
			Check(Game.Running && !Game.Transient && !Game.DontSaveThisIsAReplay
				&& (Game.SaveTask == null || Game.SaveTask.IsCompleted), "game cannot perform a real save");
			Check(Game._CacheDirectory != null
				&& KingdomScenarioSaveFiles.Same(Path.GetFullPath(Game._CacheDirectory), Owned),
				"actual save cache directory is not the exact owned destination");
			bool priorPopup = Popup.Suppress;
			Claimed = Game;
			SaveError = false;
			Saving = true;
			try
			{
				if (!priorPopup) Popup.Suppress = true;
				Task task = Game.SaveGame("Primary");
				task?.GetAwaiter().GetResult();
			}
			finally
			{
				Saving = false;
				if (!priorPopup) Popup.Suppress = false;
			}
			Check(!SaveError, "actual SaveGameError observed");
			string primary = Path.Combine(Owned, "Primary.sav.gz");
			using (FileStream file = KingdomScenarioSaveFiles.Open(primary, KingdomScenarioSaveFiles.MaxSaveBytes))
				Check(file.ReadByte() == 31 && file.ReadByte() == 139, "primary save is not gzip");
			return KingdomScenarioSaveFiles.HashFile(primary, KingdomScenarioSaveFiles.MaxSaveBytes);
		}

		internal static byte[] Lifecycle(KingdomLifecycleBook Book)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream))
					KingdomLifecycleWireCodec.WriteLifecycle(writer, Book);
				return stream.ToArray();
			}
		}

		internal static int Frame(byte[] Lifecycle)
		{
			Check(Lifecycle != null && Lifecycle.Length >= 8, "lifecycle bytes lack a frame header");
			return BitConverter.ToInt32(Lifecycle, 4);
		}

		internal static string Sha(byte[] Bytes)
		{
			using (SHA256 sha = SHA256.Create())
				return BitConverter.ToString(sha.ComputeHash(Bytes)).Replace("-", "").ToLowerInvariant();
		}

		private static byte[] Carry(KingdomCarryBook Book)
		{
			using (MemoryStream stream = new MemoryStream())
			{
				using (BinaryWriter writer = new BinaryWriter(stream))
					KingdomLifecycleWireCodec.WriteCarry(writer, Book);
				return stream.ToArray();
			}
		}

		private static bool Absent(string Root, string Name)
		{
			string path = Path.Combine(Root, Name);
			return !File.Exists(path) && !Directory.Exists(path);
		}

		internal static void Check(bool Condition, string Failure)
		{ KingdomScenarioSaveFiles.Require(Condition, Failure ?? "unfounded save refused"); }
	}
}
