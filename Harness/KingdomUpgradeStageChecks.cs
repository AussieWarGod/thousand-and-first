using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XRL;
using XRL.Core;
using XRL.Messages;
using XRL.UI;
using XRL.World;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	// Synthetic founding ground; real founding, elapsed turns, automatic staging and Primary save.
	// No saved fixture intent: the shared founding transaction is already committed before SaveGame.
	internal static class KingdomUpgradeStageChecks
	{
		private static Frame Retained;
		private static int Active;
		private static bool Attempted;
		internal static string Run(string verb)
		{
			Check(Interlocked.CompareExchange(ref Active, 1, 0) == 0, "nested stage verb refused");
			try
			{
				if (verb == KingdomUpgradeStageProvider.SetupVerb)
				{ Check(!Attempted && Retained == null, "stage setup cannot repeat"); Attempted = true; Retained = new Frame(); return Retained.Start(); }
				Check(Retained != null, "stage setup absent"); return Retained.Save();
			}
			finally { Volatile.Write(ref Active, 0); }
		}
		internal static string Refuse(Exception error)
		{
			string failure = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
			if (Retained != null)
			{
				Retained.Fault = Retained.Fault ?? failure;
				try { Check(Retained.Root == KingdomUpgradeFiles.Root(), "failure artifact root changed");
					KingdomUpgradeFiles.New(Path.Combine(Retained.Root, KingdomUpgradeStageProvider.FailureFile),
					"taf-upgrade-stage-failure-v1\n" + Retained.Fault.Replace('\r', ' ').Replace('\n', ' ') + "\n"); }
				catch (Exception write) { failure += "; failure-artifact=" + write.GetType().Name; }
			}
			return "native-upgrade-stage cases=1 passed=0 failed=1; retained=true; " + failure;
		}
		internal static void SaveEntered(XRLGame game, string name, bool copyCache, bool copyPrimary)
		{
			var f = Retained; if (f == null || !f.Saving) return;
			// Enforcing boundary, not a passive observer: do not dispatch an unowned save.
			try
			{
				Check(ReferenceEquals(game, f.Game) && name == "Primary" && !copyCache && !copyPrimary && ++f.SaveCalls == 1,
					"save reentry or destination differs"); f.SaveOwner();
			}
			catch (Exception error) { f.Fault = f.Fault ?? error.GetType().Name + ": " + error.Message; throw; }
		}
		internal static void SaveError(XRLGame game)
		{ if (Retained != null && ReferenceEquals(game, Retained.Game) && (Retained.Saving || Retained.Done)) Refuse(new InvalidOperationException("actual SaveGameError")); }
		internal static void Cache(XRLGame game, string name, string path, bool after)
		{
			var f = Retained; if (f == null || !f.Saving) return;
			// Same confinement law before and after cache resolution. A violation is permanent,
			// even if an engine caller catches this exception and subsequently retries safely.
			try
			{
				Check(ReferenceEquals(game, f.Game), "foreign save cache dispatch"); f.SaveOwner();
				if (!after) return;
				string expected = name == null ? f.Directory : Path.Combine(f.Directory, name);
				Check(path != null && KingdomUpgradeFiles.Same(Path.GetFullPath(path), expected)
					&& (KingdomUpgradeFiles.Same(expected, f.Directory) || expected.StartsWith(f.Directory + "\\", StringComparison.OrdinalIgnoreCase)),
					"save cache result escaped owned directory");
				if (name == "Primary.sav.gz") Check(++f.PrimaryPaths <= 16, "primary path repeated excessively");
				if (name == "Primary.json") Check(++f.InfoPaths <= 16, "metadata path repeated excessively");
			}
			catch (Exception error) { f.Fault = f.Fault ?? error.GetType().Name + ": " + error.Message; throw; }
		}
		internal static void Wrote(object value)
		{
			var f = Retained; if (f == null || !f.Saving) return;
			try
			{
				f.SaveOwner();
				if (ReferenceEquals(value, f.System)) Check(++f.SystemWrites <= 16, "kingdom writer repeated excessively");
				if (ReferenceEquals(value, f.Coordinator)) Check(++f.SealWrites <= 16, "seal writer repeated excessively");
			}
			catch (Exception error) { f.Fault = f.Fault ?? error.GetType().Name + ": " + error.Message; }
		}
		private static void Check(bool value, string failure) { KingdomUpgradeStageProvider.Require(value, failure); }
		private sealed class Frame
		{
			internal readonly XRLGame Game;
			internal readonly string Root, Pin;
			private readonly Zone Zone;
			private readonly ZoneManager Manager;
			private readonly GameObject Player;
			private readonly string GameId, ZoneId, Provenance, Request;
			private readonly object[] Tables;
			private readonly long FoundedTick, FoundedTurns;
			internal KingdomSystem System;
			internal KingdomSeal Coordinator;
			private KingdomCityBook City;
			private KingdomBindingRegistry Bindings;
			private KingdomPolityLedger Polity;
			private string Realm, Settlement, Legacy, Lineage, CacheDirectory;
			private int Generation;
			private long SaveTick, SaveTurns, SaveActions, SavePlayerActions;
			internal string Directory, Fault;
			internal bool Saving, Done;
			internal int SaveCalls, PrimaryPaths, InfoPaths, SystemWrites, SealWrites;
			internal Frame()
			{
				KingdomUpgradeStageProvider.Request(out string root, out string pin); Root = root; Pin = pin;
				KingdomUpgradeState.Engine(); Game = The.Game; Zone = The.ZoneManager?.ActiveZone; Manager = The.ZoneManager; Player = The.Player;
				Check(Game != null && Zone != null && Player != null, "stage source has no live game");
				GameId = Game.GameID; ZoneId = Zone.ZoneID; FoundedTick = Game.TimeTicks; FoundedTurns = Game.Turns;
				Tables = new object[] { Game.StringGameState, Game.IntGameState, Game.Int64GameState, Game.BooleanGameState, Game.ObjectGameState };
				Provenance = Game.GetStringGameState(KingdomScenarioProvenanceRules.ProvenanceState, null);
				Request = Game.GetStringGameState(KingdomScenarioNewGameGate.RequestState, null);
				Owner();
				Check(KingdomScenarioRealizer.TryBindStampedPlan(out var plan, out _, out string failure)
					&& plan.Key == "founding-first-city" && plan.AuthorityClass == KingdomScenarioFoundingStep.FoundingAuthority,
					failure ?? "stage source requires exact founding plan");
				Check(!KingdomScenarioAdvance.Pending && !(Game.GetSystem<KingdomSystem>()?.Founded ?? false)
					&& KingdomScenarioTransactionMarker.Observe(out _) == KingdomScenarioTransactionShape.None
					&& KingdomSubsidenceNativeFixture.LastAttempt == null && KingdomRaidLaunchNativeFixture.LastAttempt == null,
					"stage source is not a fresh unclaimed fixture");
				KingdomUpgradeFiles.Vacant(Path.Combine(Root, KingdomUpgradeStageProvider.ReceiptFile));
				KingdomUpgradeFiles.Vacant(Path.Combine(Root, KingdomUpgradeStageProvider.FailureFile));
			}
			internal string Start()
			{
				Owner(); Options.SetOption("r_TAF_OptionGrowth", "No"); Owner();
				Options.SetOption("r_TAF_OptionRaids", "No"); Owner();
				Check(!KingdomGrowth.Enabled && !KingdomRaids.Enabled && KingdomMaster.ConfiguredEnabled && KingdomGrowth.ScarcityEnabled,
					"stage source options differ");
				System = new KingdomWaterMaintenanceSetup(Game, Zone).Found();
				Check(System != null && System.Founded, "actual founding failed");
				City = System.City; Bindings = System.Bindings; Polity = System.PolityLedger; Coordinator = Game.GetSystem<KingdomSeal>();
				Realm = System.CurrentRealmId; Settlement = System.CurrentSettlementId;
				Legacy = Coordinator?.CurrentLegacyId; Lineage = Coordinator?.CurrentLineageId; Generation = Coordinator?.CurrentGeneration ?? 0;
				Owner(); Check(Game.TimeTicks == FoundedTick && Game.Turns == FoundedTurns, "founding changed world clocks");
				return "native-upgrade-stage phase=setup; actual-founding=true; population=0; synthetic-ground=true; ordinary-acceptance=false";
			}
			internal string Save()
			{
				Owner(); Check(!Done && !Saving && Fault == null && !KingdomScenarioAdvance.Pending, "stage save is repeated or poisoned");
				long elapsed = Game.Turns - FoundedTurns;
				Check(elapsed >= 2400 && elapsed <= 2416 && Game.TimeTicks - FoundedTick == elapsed, "actual advance2400 absent");
				var evidence = new StringBuilder(); KingdomWaterMaintenanceSealEvidence.Verify(System, Game, FoundedTick, Owner, evidence);
				KingdomSealRecord stage = ReadStage(); string stageWire = stage.Compose(); string[] slots = ReadSlots(stageWire);
				SaveTick = Game.TimeTicks; SaveTurns = Game.Turns; SaveActions = Game.ActionTicks; SavePlayerActions = Game.PlayerActionTicks;
				Check(Game.Running && !Game.Transient && !Game.DontSaveThisIsAReplay && (Game.SaveTask == null || Game.SaveTask.IsCompleted)
					&& !CapabilityManager.HasCapability(CapabilityManager.CapabilityType.RequiresStorageExpansion), "save path is not synchronous native PC");
				Directory = KingdomUpgradeFiles.SaveDirectory(Root, GameId); CacheDirectory = Game._CacheDirectory;
				string primary = Path.Combine(Directory, "Primary.sav.gz"), info = Path.Combine(Directory, "Primary.json");
				foreach (string name in new[] { primary, info, primary + ".bak", info + ".bak" }) KingdomUpgradeFiles.Vacant(name);
				Saving = true;
				try
				{
					SaveOwner(); Task task = Game.SaveGame("Primary");
					Check(task == null || task.IsCompleted && !task.IsFaulted && !task.IsCanceled, "native Primary save did not finish synchronously");
					SaveOwner(); Check(SaveCalls == 1 && SystemWrites > 0 && SealWrites > 0 && PrimaryPaths > 0 && InfoPaths > 0,
						"actual save did not write both original systems through owned paths");
					Check(ReadStage().Compose() == stageWire, "save changed selected automatic stage"); ReproveSlots(slots, stageWire);
					using (FileStream file = KingdomUpgradeFiles.Open(primary, KingdomUpgradeFiles.MaxSaveBytes))
						Check(file.ReadByte() == 31 && file.ReadByte() == 139, "Primary is not native gzip");
					string primaryHash = KingdomUpgradeFiles.HashFile(primary, KingdomUpgradeFiles.MaxSaveBytes);
					string infoHash = KingdomUpgradeFiles.HashFile(info, 1048576);
					string receipt = "taf-upgrade-stage-receipt-v1\n" + Pin + "\n" + GameId + "\n" + stage.OriginGameId + "\n"
						+ Legacy + "\n" + Lineage + "\n" + Generation.ToString(CultureInfo.InvariantCulture) + "\n"
						+ KingdomUpgradeFiles.HashText(stageWire) + "\n" + primaryHash + "\n" + infoHash + "\ncache-bind-after-quit\n";
					SaveOwner(); KingdomUpgradeFiles.New(Path.Combine(Root, KingdomUpgradeStageProvider.ReceiptFile), receipt);
					Check(KingdomUpgradeFiles.Read(Path.Combine(Root, KingdomUpgradeStageProvider.ReceiptFile), 2048) == receipt
						&& KingdomUpgradeFiles.HashFile(primary, KingdomUpgradeFiles.MaxSaveBytes) == primaryHash
						&& KingdomUpgradeFiles.HashFile(info, 1048576) == infoHash, "save or receipt readback changed");
					Check(ReadStage().Compose() == stageWire, "stage changed after receipt"); ReproveSlots(slots, stageWire); SaveOwner();
					Done = true;
					return "native-upgrade-stage cases=1 passed=1 failed=0; actual-primary-save=true; schema=2; population=0; origin=" + GameId
						+ "; receipt-sha256=" + KingdomUpgradeFiles.HashText(receipt) + "; synthetic-ground=true; ordinary-acceptance=false; cold-load=false"
						+ "; rendered-ui=false; popup-suppression=existing-runner" + evidence;
				}
				finally { Saving = false; }
			}
			internal void SaveOwner()
			{
				Owner(); Check(Fault == null && Game.TimeTicks == SaveTick && Game.Turns == SaveTurns
					&& Game.ActionTicks == SaveActions && Game.PlayerActionTicks == SavePlayerActions, "save changed clock or reported an error");
				Check(CacheDirectory != null && Game._CacheDirectory == CacheDirectory
					&& KingdomUpgradeFiles.Same(Path.GetFullPath(CacheDirectory), Directory)
					&& KingdomUpgradeFiles.Same(KingdomUpgradeFiles.SaveDirectory(Root, GameId), Directory), "actual cache ownership changed");
			}
			private KingdomSealRecord ReadStage()
			{
				Owner(); var store = new KingdomSealStore(DataManager.SyncedPath("ThousandAndFirst"));
				KingdomSealRecord stage = store.ReadStage(GameId); Owner();
				Check(stage != null && stage.Status == KingdomSealStatus.Living && stage.OriginGameId == GameId
					&& stage.LegacyId == Legacy && stage.LineageId == Lineage && stage.Generation == Generation
					&& stage.RealmId == Realm && stage.SettlementId == Settlement && stage.Population == 0 && stage.Stage == 0
					&& stage.Revision > 0 && stage.WrittenTick > FoundedTick && stage.WrittenTick <= Game.TimeTicks
					&& stage.ProfileSchema == KingdomPolityProfileRules.CommittedUnresolvedLegacyProfileSchema
					&& KingdomPolityProfileRules.IsUnresolvedBodyPool(stage.CanonicalBodyKeys), "automatic stage identity or empty schema2 differs");
				Check(KingdomSealProfileCaptureRules.StillMatches(Polity, Realm, stage, Polity.Revision, out string failure), failure);
				string wire = stage.Compose(); Check(KingdomSealRecord.TryParse(wire, out var parsed, out _, out failure)
					&& parsed.Compose() == wire && wire.StartsWith("taf-seal 6\n", StringComparison.Ordinal), "stage canonical current frame refused: " + failure);
				Owner(); return stage;
			}
			private string[] ReadSlots(string selected)
			{
				string directory = Path.Combine(Root, "Synced", "ThousandAndFirst", "Stages");
				KingdomUpgradeFiles.DirectoryExact(Path.GetDirectoryName(directory)); KingdomUpgradeFiles.DirectoryExact(directory);
				string[] result = new string[2]; bool found = false;
				for (int i = 0; i < 2; i++)
				{
					string path = Path.Combine(directory, GameId + (i == 0 ? ".a.seal" : ".b.seal"));
					Check(!SystemIOIsDirectory(path), "stage slot is a directory");
					if (!File.Exists(path)) continue;
					result[i] = KingdomUpgradeFiles.Read(path, 262144);
					Check(KingdomSealRecord.TryParse(result[i], out var record, out _, out _) && record.OriginGameId == GameId
						&& record.Compose() == result[i], "stage sibling is noncanonical or foreign");
					found |= result[i] == selected;
				}
				Check(found, "selected stage lacks exact on-disk bytes"); return result;
			}
			private static bool SystemIOIsDirectory(string path) { return global::System.IO.Directory.Exists(path); }
			private void ReproveSlots(string[] prior, string selected)
			{ string[] current = ReadSlots(selected); Check(prior[0] == current[0] && prior[1] == current[1], "save changed stage slot bytes or absence"); }
			private void Owner()
			{
				Check(Thread.CurrentThread == XRLCore.CoreThread && ReferenceEquals(The.Game, Game) && Game.GameID == GameId
					&& GameObject.Validate(Player) && ReferenceEquals(The.Player, Player) && ReferenceEquals(Game.Player?.Body, Player)
					&& ReferenceEquals(The.ZoneManager, Manager) && ReferenceEquals(Game.ZoneManager, Manager)
					&& ReferenceEquals(Manager.ActiveZone, Zone) && ReferenceEquals(Player.CurrentZone, Zone)
					&& Player.CurrentCell.X == 40 && Player.CurrentCell.Y == 12 && ZoneId == "JoppaWorld.8.22.1.1.10"
					&& Zone.ZoneID == ZoneId && Zone.Width == 80 && Zone.Height == 25 && Manager.CachedZones.TryGetValue(ZoneId, out var cached)
					&& ReferenceEquals(cached, Zone) && MessageQueue.Enabled && !KingdomSurvey.HasBoundPass
					&& !KingdomNativeRegressionContext.HasQuickstartState(Game), "stage game/player/ground authority changed");
				object[] tables = { Game.StringGameState, Game.IntGameState, Game.Int64GameState, Game.BooleanGameState, Game.ObjectGameState };
				for (int i = 0; i < tables.Length; i++) Check(tables[i] != null && ReferenceEquals(tables[i], Tables[i]), "durable table identity changed");
				Check(KingdomScenarioDurableState.ProvesExactText(KingdomScenarioProvenanceRules.ProvenanceState, Provenance)
					&& KingdomScenarioDurableState.ProvesExactText(KingdomScenarioNewGameGate.RequestState, Request), "scenario provenance or request changed");
				KingdomUpgradeStageProvider.Request(out string root, out string pin); Check(root == Root && pin == Pin, "sealed source marker changed");
				if (City == null) return;
				Check(ReferenceEquals(Game.GetSystem<KingdomSystem>(), System) && System.Founded && !System.LoadFailed && !System.RealmRetirementBlocksWork
					&& ReferenceEquals(System.City, City) && ReferenceEquals(System.Bindings, Bindings) && ReferenceEquals(System.PolityLedger, Polity)
					&& ReferenceEquals(Game.GetSystem<KingdomSeal>(), Coordinator) && Coordinator != null && Coordinator.CurrentLegacyId == Legacy
					&& Coordinator.CurrentLineageId == Lineage && Coordinator.CurrentGeneration == Generation && !string.IsNullOrEmpty(Legacy)
					&& !string.IsNullOrEmpty(Lineage) && System.CurrentRealmId == Realm && System.CurrentSettlementId == Settlement
					&& System.Stage == GrowthStage.Camp && System.Population == 0 && City.ResidentCount == 0 && Bindings.Count == 0
					&& KingdomMaster.NewWorkAllowed(System) && !KingdomGrowth.Enabled && !KingdomRaids.Enabled && KingdomGrowth.ScarcityEnabled
					&& System.ClaimedZones.Count == 1 && System.ClaimedZones.Contains(ZoneId)
					&& KingdomScenarioTransactionMarker.Observe(out _) == KingdomScenarioTransactionShape.Committed, "empty-camp coordinator or founding authority changed");
			}
		}
	}
}
