using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using XRL;
using XRL.Core;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst.Harness
{
	// Two disposable worlds; no synthetic receipt, lifecycle field, clock, or population writes.
	internal sealed class KingdomUpgradeSourceDriver
	{
		internal readonly XRLGame Game;
		internal readonly Zone Zone;
		internal readonly long Tick;
		internal KingdomSystem System;
		private readonly object Player, Tables;
		private readonly KingdomInheritanceState Inheritance;
		private readonly string Root, GameId, Cache, Script, Verb;
		private readonly long Turns, Actions, PlayerActions;
		private bool Saving;
		private int PrimaryPaths, InfoPaths;
		private string SaveFault;
		private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
		internal KingdomUpgradeSourceDriver(XRLGame game, Zone zone, string verb)
		{
			Game = game; Zone = zone; Verb = verb; GameId = game.GameID; Player = game.Player.Body; Tables = game.ObjectGameState;
			System = game.GetSystem<KingdomSystem>(); Inheritance = KingdomUpgradeState.ReadInheritance(game);
			Tick = game.TimeTicks; Turns = game.Turns; Actions = game.ActionTicks; PlayerActions = game.PlayerActionTicks;
			Root = KingdomUpgradeFiles.Root(); Cache = KingdomUpgradeFiles.SaveDirectory(Root, GameId);
			Script = KingdomUpgradeFiles.Read(Path.Combine(Root, "Local", "scenario-script.txt"), 65536);
			Check(zone.Width == 80 && zone.Height == 25, "source requires bounded native zone");
			Check(!CapabilityManager.HasCapability(CapabilityManager.CapabilityType.RequiresStorageExpansion), "PC storage-expansion ambiguity");
			foreach (string name in new[] { KingdomUpgradeSourceProvider.DonorFile, KingdomUpgradeSourceProvider.LinkFile,
				KingdomUpgradeSnapshotCodec.SnapshotFile, KingdomUpgradeSnapshotCodec.ReceiptFile, KingdomUpgradeSnapshotCodec.FailureFile })
				KingdomUpgradeFiles.Vacant(Path.Combine(Root, name));
			Owner(); Pristine();
		}

		internal void Owner()
		{
			Check(ReferenceEquals(The.Game, Game) && Game.GameID == GameId && ReferenceEquals(Game.Player?.Body, Player)
				&& ReferenceEquals(The.Player, Player) && ReferenceEquals(Game.ZoneManager?.ActiveZone, Zone)
				&& ReferenceEquals(((GameObject)Player).Physics?.CurrentCell?.ParentZone, Zone)
				&& ReferenceEquals(Game.ObjectGameState, Tables) && ReferenceEquals(Game.GetSystem<KingdomSystem>(), System)
				&& (System == null || !System.LoadFailed) && ReferenceEquals(KingdomUpgradeState.ReadInheritance(Game), Inheritance)
				&& Game.TimeTicks == Tick && Game.Turns == Turns && Game.ActionTicks == Actions && Game.PlayerActionTicks == PlayerActions
				&& KingdomUpgradeFiles.Root() == Root && Game._CacheDirectory != null
				&& KingdomUpgradeFiles.Same(Path.GetFullPath(Game._CacheDirectory), Cache)
				&& KingdomUpgradeFiles.SaveDirectory(Root, GameId) == Cache
				&& KingdomUpgradeFiles.Read(Path.Combine(Root, "Local", "scenario-script.txt"), 65536) == Script,
				"upgrade source game, owner, clocks, script or selected cache changed");
		}
		internal void AdoptFoundedSystem()
		{
			KingdomSystem current = Game.GetSystem<KingdomSystem>();
			Check(current != null && (System == null || ReferenceEquals(current, System)) && current.Founded
				&& current.OwnedZone(Zone.ZoneID), "founding did not publish exact source system");
			System = current; Owner();
		}

		internal void Pristine()
		{
			Owner(); Check(Inheritance != null && Inheritance.Phase == KingdomInheritancePhase.Empty, "only pristine live Empty can initialize");
			KingdomUpgradeState.ValidateInheritance(Inheritance, GameId);
			foreach (string name in ("LegacyText ReceiptText CommittedReceiptText TargetZoneId TargetTerrainBlueprint SecretId SiteName "
				+ "FailureDetail ApplicationMarker ReservedTerrainTag").Split(' ')) Check((string)Field(name) == "", "nonempty inheritance field " + name);
			foreach (string name in ("FailureAnnounced ReleasePending OwnsSkipTerrainBuilders OwnsNoBiomes OwnsZoneName "
				+ "RecoveryDisabled RetryAuthorized ProfileReceiptWasCommitted").Split(' ')) Check(!(bool)Field(name), "owned inheritance flag " + name);
			foreach (string name in "TargetTerrainRank ApplyStatusValue ApplyFaultValue TargetX TargetY".Split(' '))
				Check((int)Field(name) == -1, "nonpristine inheritance coordinate/status " + name);
			foreach (string name in "ProfileCommittedReceipt ReservationLease ReservedMap ReservedWorldInfo".Split(' '))
				Check(Field(name) == null, "live inheritance authority " + name);
			FieldInfo lease = typeof(KingdomInheritanceLeaseOwner).GetField("Lease", BindingFlags.Static | BindingFlags.NonPublic);
			Check(lease != null && lease.GetValue(null) == null, "another process-local reservation is retained");
		}
		private object Field(string name)
		{ FieldInfo f = typeof(KingdomInheritanceState).GetField(name, Private); Check(f != null, "missing old inheritance field " + name); return f.GetValue(Inheritance); }

		internal void Donor()
		{
			Check(Verb == KingdomUpgradeSourceProvider.DonorVerb, "wrong donor verb");
			Owner(); KingdomUpgradeSourceProvider.BodyExact();
			Check(KingdomSeal.TryRetireGeneration(out string failure), failure);
			Owner(); KingdomUpgradeSourceProvider.BodyExact(); Pristine();
			KingdomSeal seal = Game.GetSystem<KingdomSeal>();
			Check(seal != null && seal.RetiredLegacyId == seal.CurrentLegacyId, "actual retirement is not sealed");
			KingdomSealStore store = Store();
			KingdomSealRecord stage = store.ReadStage(GameId);
			Check(stage != null && stage.Status == KingdomSealStatus.Retired && stage.OriginGameId == GameId
				&& stage.LegacyId == seal.CurrentLegacyId && stage.LineageId == seal.CurrentLineageId
				&& stage.Generation == seal.CurrentGeneration, "retirement stage identity differs");
			string stageWire = StageWire(store, GameId), legacyWire = KingdomUpgradeFiles.Read(store.LegacyPath(stage.LegacyId), 262144);
			Check(KingdomSealRecord.TryParse(legacyWire, out var legacy, out _, out _) && legacy.Compose() == legacyWire
				&& legacy.Status == KingdomSealStatus.Promoted && legacy.IsResolved
				&& KingdomSealRules.PromoteRetirement(stage).Compose() == legacyWire, "promoted legacy is not actual retired-stage output");
			Save(); Owner(); KingdomUpgradeSourceProvider.BodyExact(); Pristine();
			Check(ReferenceEquals(Game.GetSystem<KingdomSeal>(), seal) && seal.RetiredLegacyId == stage.LegacyId
				&& StageWire(store, GameId) == stageWire
				&& KingdomUpgradeFiles.Read(store.LegacyPath(stage.LegacyId), 262144) == legacyWire, "retired source changed during save");
			string primary = Hash("Primary.sav.gz"), info = Hash("Primary.json");
			string wire = "taf-upgrade-donor-receipt-v1\n" + KingdomUpgradeSnapshotCodec.OldPin + "\n" + GameId + "\n"
				+ stage.OriginGameId + "\n" + stage.LegacyId + "\n" + stage.LineageId + "\n" + Number(stage.Generation) + "\n"
				+ KingdomUpgradeFiles.HashText(stageWire) + "\n" + KingdomUpgradeFiles.HashText(legacyWire) + "\n"
				+ primary + "\n" + info + "\ncache-bind-after-quit\n";
			Owner(); Check(Hash("Primary.sav.gz") == primary && Hash("Primary.json") == info, "donor save changed during capture");
			Write(KingdomUpgradeSourceProvider.DonorFile, wire);
		}

		internal void Reserved()
		{
			Check(Verb == KingdomUpgradeSourceProvider.ReservedVerb && !(System?.Founded ?? false), "reserved source must remain unfounded");
			string input = KingdomUpgradeFiles.Read(Path.Combine(Root, "Local", KingdomUpgradeSourceProvider.InputFile), 2048);
			string[] donor = input.Split('\n'); int generation = -1;
			Check(donor.Length == 13 && donor[0] == "taf-upgrade-source-donor-v1" && donor[1] == KingdomUpgradeSnapshotCodec.OldPin
				&& donor[12] == "" && donor[2] == donor[3] && donor[2] != GameId && KingdomUpgradeSnapshotCodec.GameId(donor[2])
				&& KingdomSealReceipt.ValidId(donor[4]) && KingdomSealReceipt.ValidId(donor[5])
				&& int.TryParse(donor[6], NumberStyles.None, CultureInfo.InvariantCulture, out generation)
				&& generation >= 0 && Number(generation) == donor[6], "malformed sealed donor input");
			for (int i = 7; i < 12; i++) Check(KingdomUpgradeSnapshotCodec.Hash(donor[i]), "malformed donor hash");
			KingdomSealStore store = Store();
			string promoted = KingdomUpgradeFiles.Read(store.LegacyPath(donor[4]), 262144);
			Check(KingdomSealRecord.TryParse(promoted, out var legacy, out _, out _) && legacy.Compose() == promoted
				&& legacy.OriginGameId == donor[3] && legacy.LegacyId == donor[4] && legacy.LineageId == donor[5]
				&& legacy.Generation == generation && legacy.Status == KingdomSealStatus.Promoted && legacy.IsResolved
				&& KingdomUpgradeFiles.HashText(promoted) == donor[8], "copied donor promoted legacy differs");
			Check(KingdomUpgradeFiles.HashText(StageWire(store, donor[3])) == donor[7], "copied donor stage differs");
			var prior = store.ReadReceipts(out int refused);
			Check(refused == 0, "copied profile contains refused import receipts");
			foreach (var claim in prior) Check(claim.TargetGameId != GameId, "fresh source already owns an import receipt");
			Pristine(); Options.SetOption("r_TAF_OptionLegacyImport", "Yes"); Owner(); Pristine();
			Check(Options.GetOption("r_TAF_OptionLegacyImport", "No") == "Yes", "legacy import opt-in did not persist");
			Inheritance.Initialize(); Owner();
			Check(Inheritance.Phase == KingdomInheritancePhase.Reserved && (string)Field("LegacyText") == promoted
				&& KingdomSealReceipt.TryParse((string)Field("ReceiptText"), out var receipt)
				&& receipt.State == KingdomSealReceiptState.Reserved && receipt.TargetGameId == GameId
				&& receipt.LegacyId == donor[4] && receipt.LineageId == donor[5] && receipt.WrittenTick == Tick
				&& KingdomUpgradeFiles.Read(store.ReceiptPath(receipt.LegacyId, GameId), 262144) == receipt.Compose()
				&& Field("ReservationLease") is KingdomSealReservationLease lease && lease.IsHeld && lease.Matches(receipt)
				&& ReferenceEquals(KingdomInheritanceLeaseOwner.Get(GameId, receipt), lease), "actual live initialization did not reserve exact donor");
			KingdomUpgradeState state = new KingdomUpgradeState(Game, "inheritance");
			KingdomUpgradeSource.Arm(null);
			Check(SourceField("Fault") == null && (bool)SourceField("Armed"), "actual source arm refused");
			state.Exact(); Save(); state.Exact();
			Check(SourceField("Fault") == null && (bool)SourceField("Completed") && !(bool)SourceField("Armed"), "actual source capture failed");
			string snapshot = KingdomUpgradeFiles.Read(Path.Combine(Root, KingdomUpgradeSnapshotCodec.SnapshotFile), KingdomUpgradeSnapshotCodec.MaxWireChars);
			Check(KingdomUpgradeSnapshotCodec.TryDecode(snapshot, out var saved), "source snapshot cannot decode"); state.Matches(saved);
			string output = KingdomUpgradeFiles.Read(Path.Combine(Root, KingdomUpgradeSnapshotCodec.ReceiptFile), 1024);
			Check(output == KingdomUpgradeSnapshotCodec.Receipt(saved, Hash("Primary.sav.gz"), Hash("Primary.json"),
				KingdomUpgradeFiles.HashText(snapshot)), "actual save receipt is not selected source snapshot");
			Check(KingdomUpgradeFiles.Read(Path.Combine(Root, "Local", KingdomUpgradeSourceProvider.InputFile), 2048) == input
				&& (string)Field("LegacyText") == promoted && !(System?.Founded ?? false)
				&& KingdomUpgradeFiles.Read(store.LegacyPath(donor[4]), 262144) == promoted
				&& KingdomUpgradeFiles.HashText(StageWire(store, donor[3])) == donor[7], "source donor input or reserved world changed");
			Write(KingdomUpgradeSourceProvider.LinkFile, "taf-upgrade-source-link-v1\n" + donor[1] + "\n" + GameId + "\n"
				+ string.Join("\n", donor, 2, 7) + "\n" + donor[11] + "\n" + KingdomUpgradeFiles.HashText(snapshot) + "\n"
				+ KingdomUpgradeFiles.HashText(output) + "\n");
		}

		private void Save()
		{
			Owner(); Check(!Saving && Game.Running && !Game.Transient && !Game.DontSaveThisIsAReplay
				&& (Game.SaveTask == null || Game.SaveTask.IsCompleted), "source cannot enter synchronous primary save");
			KingdomPolityRealmTransition transition = System?.PolityTransition;
			List<object> before = new List<object>();
			string inherited = KingdomUpgradeGraph.Capture(Inheritance, before), transitioned = KingdomUpgradeGraph.Capture(transition, before);
			Saving = true;
			try
			{
				Task save = Game.SaveGame("Primary");
				Check(save == null || save.IsCompleted && !save.IsFaulted && !save.IsCanceled, "PC primary save did not finish synchronously");
				Check(SaveFault == null && PrimaryPaths > 0 && InfoPaths > 0, "actual primary save evidence refused: " + SaveFault);
				Owner();
				List<object> after = new List<object>();
				Check(ReferenceEquals(System?.PolityTransition, transition) && KingdomUpgradeGraph.Capture(Inheritance, after) == inherited
					&& KingdomUpgradeGraph.Capture(transition, after) == transitioned && before.Count == after.Count, "save changed retained raw authority");
				for (int i = 0; i < before.Count; i++) Check(ReferenceEquals(before[i], after[i]), "save replaced retained child authority");
				using (FileStream file = KingdomUpgradeFiles.Open(Path.Combine(Cache, "Primary.sav.gz"), KingdomUpgradeFiles.MaxSaveBytes))
					Check(file.ReadByte() == 31 && file.ReadByte() == 139, "actual primary is not gzip");
			}
			finally { Saving = false; }
		}
		internal void ObservePath(XRLGame game, string name, string path)
		{
			if (!Saving || !ReferenceEquals(game, Game)) return;
			try
			{
				Owner(); string expected = name == null ? Cache : Path.Combine(Cache, name);
				Check(path != null && KingdomUpgradeFiles.Same(Path.GetFullPath(path), expected)
					&& (KingdomUpgradeFiles.Same(expected, Cache) || Path.GetFullPath(expected).StartsWith(Cache + Path.DirectorySeparatorChar,
						StringComparison.OrdinalIgnoreCase)), "actual save path left owned cache");
				if (name == "Primary.sav.gz") PrimaryPaths++;
				if (name == "Primary.json") InfoPaths++;
				Check(PrimaryPaths <= 16 && InfoPaths <= 16, "actual primary path observer exceeded bound");
			}
			catch (Exception error) { if (SaveFault == null) SaveFault = error.GetType().Name + ": " + error.Message; }
		}
		internal void ObserveError(XRLGame game)
		{ if (Saving && ReferenceEquals(game, Game) && SaveFault == null) SaveFault = "actual SaveGameError"; }
		private KingdomSealStore Store()
		{
			string path = DataManager.SyncedPath("ThousandAndFirst");
			Check(KingdomUpgradeFiles.Same(path, Path.Combine(Root, "Synced", "ThousandAndFirst")), "seal store left owned profile");
			return new KingdomSealStore(path);
		}
		private static string StageWire(KingdomSealStore store, string origin)
		{
			KingdomSealRecord stage = store.ReadStage(origin);
			Check(stage != null && stage.OriginGameId == origin && stage.Status == KingdomSealStatus.Retired,
				"selected donor retired stage is unreadable");
			string wire = stage.Compose(); bool exact = false;
			foreach (char slot in new[] { 'a', 'b' })
			{
				string path = store.StagePath(origin, slot);
				if (File.Exists(path) && KingdomUpgradeFiles.Read(path, 262144) == wire) exact = true;
			}
			Check(exact, "selected donor stage lacks exact canonical stored bytes"); return wire;
		}
		private string Hash(string name) { return KingdomUpgradeFiles.HashFile(Path.Combine(Cache, name), name == "Primary.json" ? 1048576 : KingdomUpgradeFiles.MaxSaveBytes); }
		private void Write(string name, string wire)
		{ Owner(); KingdomUpgradeFiles.New(Path.Combine(Root, name), wire); Check(KingdomUpgradeFiles.Read(Path.Combine(Root, name), 2048) == wire, "immutable driver receipt differs"); }
		private static object SourceField(string name)
		{ FieldInfo f = typeof(KingdomUpgradeSource).GetField(name, BindingFlags.Static | BindingFlags.NonPublic); Check(f != null, "source observer API differs"); return f.GetValue(null); }
		private static string Number(int value) { return value.ToString(CultureInfo.InvariantCulture); }
		private static void Check(bool value, string failure) { KingdomUpgradeSourceProvider.Check(value, failure); }
	}
}
