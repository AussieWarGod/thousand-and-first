using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomScenarioSaveChecks
	{
		internal static string Run(XRLGame Game, Zone Zone, out bool Ok)
		{
			Ok = false;
			List<PartyHold> held = new List<PartyHold>();
			KingdomSubsidenceNativeFixture fixture = null;
			KingdomSurvey.PassScope scope = null;
			GameObject player = The.Player;
			try
			{
				string root = KingdomScenarioSaveFiles.Root();
				Check(!KingdomScenarioSaveFiles.LoadPresent()
					&& !File.Exists(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile))
					&& !File.Exists(Path.Combine(root, KingdomScenarioSaveFiles.SnapshotFile))
					&& !KingdomNativeRegressionContext.HasAnyState(Game, KingdomScenarioSaveFiles.SnapshotKey),
					"fresh save witness authority is not empty");
				Check(KingdomSubsidenceNativeFixture.TryCreate(Zone, out fixture, out string failure,
					CompleteFoundingHeart: true), failure);
				KingdomSystem system = fixture.System;
				scope = fixture.Survey.BindPass();
				KingdomResidentIdentity.Reconcile(system, fixture.Survey.Settlers);
				Check(system.SpeciesCounts != null && system.SpeciesCounts.Count == 1
					&& system.SpeciesCounts.TryGetValue("human", out int counted) && counted == 50,
					"fixture census lacks exactly fifty witnessed human residents");
				KingdomScenarioSaveAuthorityChecks.Prepare(system, Zone);
				long now = Game.TimeTicks, anchor = now - KingdomSubsidenceStepRules.StepTicks;
				Check(KingdomSubsidenceNativeClockSeed.TrySeed(system, Zone, fixture.Survey, anchor, out failure), failure);
				KingdomSubsidenceNativeClockChecks.Verify(fixture, Zone, fixture.Survey, now);
				Check(system.LastSubsidenceTick == anchor && system.Population == 50, "seeded checkpoint changed");
				int departures = system.Ledger.Departures;
				GameObject departing = fixture.Bodies[0];
				string missingId = departing.IDIfAssigned;
				int missingResident = fixture.ResidentIds[0];
				for (int i = 1; i < fixture.Bodies.Count; i++)
				{
					GameObject body = fixture.Bodies[i];
					Check(GameObject.Validate(body) && body.Brain != null && body.Brain.PartyLeader == null,
						"owned temporary party custody is unavailable");
					PartyHold hold = new PartyHold(body);
					held.Add(hold); hold.Brain.PartyLeader = player;
					Check(body.IsPlayerLed(), "owned resident did not become temporarily ineligible");
				}
				bool finished = KingdomSubsidence.TryReckon(system, Zone, fixture.Survey, now, out failure);
				Check(!finished && !string.IsNullOrEmpty(failure) && !GameObject.Validate(departing)
					&& KingdomResidents.DepartureCarriersAbsent(system, system.City, missingResident)
					&& system.Population == 49 && KingdomResidents.OnRollCount(system) == 49
					&& system.Ledger.Departures == departures + 1 && system.LastSubsidenceTick == anchor
					&& KingdomSubsidenceStepCodec.TryDecode(system.City.SubsidenceModel, out KingdomSubsidenceStepBook book)
					&& book.Sequence == 1 && book.Active != null && book.Active.Completed == 1
					&& book.Active.Quota == 5 && book.Active.DueTick == now && book.Active.AnchorTick == anchor
					&& book.Active.PendingDepartureId == "" && KingdomResidentDepartureRules.IsEmpty(system.ResidentDeparture),
					"actual one-of-five departure did not retain the original unpaid step");
				Release(held, player, Zone);
				int[] residentIds = new int[49]; string[] objectIds = new string[49];
				for (int i = 1; i < fixture.Bodies.Count; i++)
				{
					residentIds[i - 1] = fixture.ResidentIds[i]; objectIds[i - 1] = fixture.Bodies[i].IDIfAssigned;
					Check(GameObject.Validate(fixture.Bodies[i]) && !fixture.Bodies[i].IsPlayerLed()
						&& fixture.Bodies[i].CurrentZone == Zone, "save survivor custody changed");
				}
				KingdomScenarioSaveSnapshot snapshot = new KingdomScenarioSaveSnapshot(Game.GameID,
					system.City.SubsidenceModel, Zone.ZoneID, now, system.Ledger.Departures,
					missingResident, missingId, residentIds, objectIds);
				Check(KingdomScenarioSaveSnapshotCodec.TryEncode(snapshot, out string wire), "save snapshot cannot encode");
				Game.SetStringGameState(KingdomScenarioSaveFiles.SnapshotKey, wire);
				Check(KingdomScenarioDurableState.ProvesExactText(KingdomScenarioSaveFiles.SnapshotKey, wire),
					"snapshot key did not publish exactly");
				KingdomScenarioSaveAuthorityChecks.Capture(system, Zone);
				KingdomScenarioSaveFiles.WriteNew(Path.Combine(root, KingdomScenarioSaveFiles.SnapshotFile), wire);
				Check(Game.Running && !Game.Transient && !Game.DontSaveThisIsAReplay
					&& (Game.SaveTask == null || Game.SaveTask.IsCompleted), "game cannot perform a real save");
				string directory = KingdomScenarioSaveFiles.SaveDirectory(root, Game.GameID);
				Check(Directory.GetDirectories(directory).Length == 0, "fresh game has unexpected save subdirectories");
				foreach (string existing in Directory.GetFiles(directory))
					Check(Path.GetFileName(existing) == "Cache.db", "fresh game already has primary or backup save evidence");
				The.ZoneManager.CheckCached(true, true);
				int serializationCases = KingdomScenarioReceiptSerializationChecks.Run();
				Check(serializationCases == 8, "rich receipt serialization cases did not all complete");
				Task save = Game.SaveGame("Primary");
				save?.GetAwaiter().GetResult();
				Check(ReferenceEquals(The.Game, Game) && Game.TimeTicks == now
					&& system.City.SubsidenceModel == snapshot.StepWire && system.Population == 49
					&& KingdomScenarioDurableState.ProvesExactText(KingdomScenarioSaveFiles.SnapshotKey, wire),
					"saved source changed across serialization");
				KingdomScenarioSaveAuthorityChecks.VerifyExact(system, Zone);
				Check(Directory.GetFiles(directory).Length == 3 && Directory.GetDirectories(directory).Length == 0
					&& File.Exists(Path.Combine(directory, "Cache.db")), "save did not create exactly the three fresh artifacts");
				string primary = Path.Combine(directory, "Primary.sav.gz");
				using (FileStream file = KingdomScenarioSaveFiles.Open(primary, KingdomScenarioSaveFiles.MaxSaveBytes))
					Check(file.ReadByte() == 31 && file.ReadByte() == 139, "primary save is not gzip");
				string primaryHash = KingdomScenarioSaveFiles.HashFile(primary, KingdomScenarioSaveFiles.MaxSaveBytes);
				string infoHash = KingdomScenarioSaveFiles.HashFile(Path.Combine(directory, "Primary.json"), 1048576);
				// Cache.db remains engine-owned here. The stopped-profile copier hashes it after shutdown.
				string receipt = "taf-scenario-save-v1\n" + Game.GameID + "\n" + primaryHash + "\n" + infoHash
					+ "\n" + KingdomScenarioSaveFiles.HashText(wire) + "\n";
				KingdomScenarioSaveFiles.WriteNew(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile), receipt);
				Check(KingdomScenarioSaveFiles.ReadText(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile), 512) == receipt,
					"save receipt did not persist exactly");
				Ok = true;
				return "native-save cases=1 passed=1 failed=0; real-save=true; game-id=" + Game.GameID
					+ "; departed=1; quota=5; remaining=4; population=49; checkpoint=unpaid; synthetic-setup=true"
					+ "; receipt-roundtrips=8; legacy-format=fixture-generated; historical-save-compatibility=unproved"
					+ "; load=unproved; ordinary-acceptance=false; profile-and-effects-retained=true";
			}
			catch (Exception error)
			{
				return "native-save cases=1 passed=0 failed=1; evidence-retained=true; "
					+ KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
			}
			finally
			{
				try { Release(held, player, Zone); }
				catch (Exception) { Ok = false; }
				scope?.Dispose();
			}
		}

		private sealed class PartyHold
		{
			internal readonly GameObject Body;
			internal readonly Brain Brain;
			internal PartyHold(GameObject body) { Body = body; Brain = body.Brain; }
		}

		private static void Release(List<PartyHold> Held, GameObject Player, Zone Zone)
		{
			bool refused = false;
			for (int i = Held.Count - 1; i >= 0; i--)
			{
				PartyHold hold = Held[i];
				if (!GameObject.Validate(hold.Body) || hold.Body.CurrentZone != Zone
					|| !ReferenceEquals(hold.Body.Brain, hold.Brain) || !ReferenceEquals(hold.Brain.PartyLeader, Player))
				{ refused = true; continue; }
				hold.Brain.PartyLeader = null;
				if (hold.Brain.PartyLeader != null) { refused = true; continue; }
				Held.RemoveAt(i);
			}
			Check(!refused, "temporary party custody changed; only exact unchanged holds released");
		}

		private static void Check(bool Condition, string Failure)
		{
			KingdomScenarioSaveFiles.Require(Condition, Failure ?? "native save check refused");
		}
	}
}
