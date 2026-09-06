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
	/// <summary>Drives the real fifteen-departure subsidence rung to its D5 release, cuts strictly
	/// after the second actual receipt write, disarms the cut, and saves the resulting cold state
	/// through the engine's own SaveGame. Nothing is fabricated: the parent Intent, the two local
	/// writes and every captured value are production's own.</summary>
	internal static class KingdomSubsidenceRungSaveChecks
	{
		internal const int Index = 0;
		internal const long Sequence = 3L;

		internal static string Run(XRLGame Game, Zone Zone, out bool Ok)
		{
			Ok = false;
			KingdomSubsidenceRungNativeFixture fixture = null;
			KingdomSurvey.PassScope scope = null;
			try
			{
				string root = KingdomScenarioSaveFiles.Root();
				Check(!KingdomScenarioSaveFiles.LoadPresent()
					&& !File.Exists(Path.Combine(root, KingdomScenarioSaveFiles.ReceiptFile))
					&& !File.Exists(Path.Combine(root, KingdomScenarioSaveFiles.SnapshotFile))
					&& !KingdomNativeRegressionContext.HasAnyState(Game, KingdomScenarioSaveFiles.SnapshotKey)
					&& !KingdomSubsidenceRungReleaseCut.Armed, "fresh rung save witness authority is not empty");
				Check(KingdomSubsidenceRungNativeFixture.TryCreate(Zone, out fixture, out string failure,
					CompleteFoundingHeart: true), failure);
				KingdomSystem system = fixture.Base.System;
				scope = fixture.Survey.BindPass();
				KingdomResidentIdentity.Reconcile(system, fixture.Survey.Settlers);
				Check(system.SpeciesCounts != null && system.SpeciesCounts.Count == 1
					&& system.SpeciesCounts.TryGetValue("human", out int counted) && counted == 50,
					"fixture census lacks exactly fifty witnessed human residents");
				KingdomScenarioSaveAuthorityChecks.Prepare(system, Zone);
				long now = fixture.Now, anchor = now - 3L * KingdomSubsidenceStepRules.StepTicks;
				Check(KingdomSubsidenceNativeClockSeed.TrySeed(system, Zone, fixture.Survey, anchor, out failure), failure);
				KingdomSubsidenceNativeClockChecks.Verify(fixture.Base, Zone, fixture.Survey, now);
				Check(Game.TimeTicks == now && system.LastSubsidenceTick == anchor && system.Population == 50
					&& system.Stage == GrowthStage.City, "three elapsed steps were not seeded on a still clock");
				int departures = system.Ledger.Departures;
				string[] before = new string[fixture.Base.Bodies.Count];
				for (int i = 0; i < before.Length; i++)
				{
					before[i] = fixture.Base.Bodies[i].IDIfAssigned;
					Check(!string.IsNullOrEmpty(before[i]), "a fixture body carries no assigned identity");
				}
				string workId = fixture.Work.IDIfAssigned;
				Check(!string.IsNullOrEmpty(workId), "the fixture work carries no assigned identity");
				KingdomSubsidenceRungHeart heart = KingdomSubsidenceRungSaveHeartProof.Bind(Zone,
					system.CurrentSettlementId, now, GrowthStage.City);
				KingdomSubsidenceRungSaveHeartProof.ProveFreshHeartAbsent(heart.Final,
					KingdomSubsidenceRungSaveHeartProof.PrePass);
				KingdomSubsidenceRungSaveHeartProof.ProveOrdinal(workId, heart.Id);
				bool ran;
				KingdomSubsidenceRungReleaseCut.ArmCut(Game, system, workId, Index, Sequence);
				try { ran = KingdomSubsidence.TryReckon(system, Zone, fixture.Survey, now, out failure); }
				finally { KingdomSubsidenceRungReleaseCut.Disarm(); }
				Check(!ran && !string.IsNullOrEmpty(failure) && !KingdomSubsidenceRungReleaseCut.Armed,
					"the real rung pass did not stop at the armed native release cut: " + failure);
				Check(KingdomSubsidenceRungReleaseCut.Writes == 2 && KingdomSubsidenceRungReleaseCut.Fields == "0,1"
					&& KingdomSubsidenceRungReleaseCut.Throws == 1 && KingdomSubsidenceRungReleaseCut.Fault == null
					&& KingdomSubsidenceRungReleaseCut.PublishedIntent
					&& !KingdomSubsidenceRungReleaseCut.PublishedReleased
					&& !string.IsNullOrEmpty(KingdomSubsidenceRungReleaseCut.StepId),
					"the cut did not land after exactly two admitted writes under a durable parent intent");
				Check(system.Population == 35 && KingdomResidents.OnRollCount(system) == 35
					&& system.Stage == GrowthStage.Town && Game.TimeTicks == now
					&& system.Ledger.Departures == departures + 15
					&& system.LastSubsidenceTick == now - KingdomSubsidenceStepRules.StepTicks
					&& KingdomResidentDepartureRules.IsEmpty(system.ResidentDeparture),
					"fifteen actual departures or the unpaid original checkpoint were not proved");
				Check(fixture.TryReleaseHeld(out string cleanup), cleanup);
				int[] residents = new int[KingdomSubsidenceRungSaveSnapshot.SurvivorCount];
				string[] objects = new string[KingdomSubsidenceRungSaveSnapshot.SurvivorCount];
				int[] absentIds = new int[KingdomSubsidenceRungSaveSnapshot.AbsentCount];
				string[] absentObjects = new string[KingdomSubsidenceRungSaveSnapshot.AbsentCount];
				Sort(fixture, before, residents, objects, absentIds, absentObjects);
				KingdomSubsidenceRungSaveSnapshot snapshot = Capture(Game, Zone, fixture, heart,
					residents, objects, absentIds, absentObjects);
				Check(KingdomSubsidenceRungSaveSnapshotCodec.TryEncode(snapshot, out string wire)
					&& KingdomSubsidenceRungSaveSnapshotCodec.MatchesCurrentPrefix(wire), "current ss5 rung save snapshot cannot encode");
				Game.SetStringGameState(KingdomScenarioSaveFiles.SnapshotKey, wire);
				Check(KingdomScenarioDurableState.ProvesExactText(KingdomScenarioSaveFiles.SnapshotKey, wire),
					"snapshot key did not publish exactly");
				KingdomScenarioSaveAuthorityChecks.Capture(system, Zone);
				KingdomSubsidenceRungSaveHeartProof.ProveHeartStill(heart, Zone,
					KingdomSubsidenceRungSaveHeartProof.BeforeSave);
				KingdomScenarioSaveFiles.WriteNew(Path.Combine(root, KingdomScenarioSaveFiles.SnapshotFile), wire);
				string directory = Save(Game, root);
				Check(ReferenceEquals(The.Game, Game) && Game.TimeTicks == now
					&& system.City.SubsidenceModel == snapshot.StepWire && system.Population == 35
					&& KingdomScenarioDurableState.ProvesExactText(KingdomScenarioSaveFiles.SnapshotKey, wire),
					"saved source changed across serialization");
				KingdomScenarioSaveAuthorityChecks.VerifyExact(system, Zone);
				KingdomSubsidenceRungSaveHeartProof.ProveHeartStill(heart, Zone,
					KingdomSubsidenceRungSaveHeartProof.AfterSave);
				KingdomSubsidenceRungSaveSnapshot after = Capture(Game, Zone, fixture, heart,
					residents, objects, absentIds, absentObjects);
				Check(KingdomSubsidenceRungSaveSnapshotCodec.TryEncode(after, out string second)
					&& second == wire, "the after-write capture is not byte-identical to the saved snapshot");
				Receipt(Game, root, directory, wire);
				Ok = true;
				return "native-rung-save cases=1 passed=1 failed=0; real-save=true; game-id=" + Game.GameID
					+ "; departed=15; native-write-cut=2; release=Intent; wear-fields=10; population=35"
					+ "; stage=Town; quarantine=none; synthetic-setup=true; load=unproved"
					+ "; ordinary-acceptance=false; profile-and-effects-retained=true";
			}
			catch (Exception error)
			{
				return "native-rung-save cases=1 passed=0 failed=1; evidence-retained=true; "
					+ KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
			}
			finally
			{
				try { KingdomSubsidenceRungReleaseCut.Disarm(); }
				catch (Exception error)
				{
					Ok = false;
					KingdomScenarioJournal.Append("RUNG-SAVE-CLEANUP", false,
						KingdomScenarioRules.Bounded("release scope retained after " + error.GetType().Name));
				}
				if (fixture != null && !fixture.TryReleaseHeld(out string retained))
				{
					Ok = false;
					KingdomScenarioJournal.Append("RUNG-SAVE-CLEANUP", false, KingdomScenarioRules.Bounded(retained));
				}
				scope?.Dispose();
			}
		}

		private static void Sort(KingdomSubsidenceRungNativeFixture Fixture, string[] Before,
			int[] Residents, string[] Objects, int[] AbsentIds, string[] AbsentObjects)
		{
			int alive = 0, gone = 0;
			KingdomSystem system = Fixture.Base.System;
			for (int i = 0; i < Before.Length; i++)
			{
				GameObject body = Fixture.Base.Bodies[i];
				int id = Fixture.Base.ResidentIds[i];
				if (GameObject.Validate(body))
				{
					Check(alive < Residents.Length && body.IsAlive && body.IDIfAssigned == Before[i]
						&& body.CurrentZone == Fixture.Base.Zone && !body.IsPlayerLed()
						&& KingdomCitizenship.BelongsTo(system, body) && KingdomResidents.IdOf(body) == id
						&& system.City.TryResidentRow(id, out _), "a saved survivor lost its exact row or ground");
					Residents[alive] = id; Objects[alive] = Before[i]; alive++;
					continue;
				}
				Check(gone < AbsentIds.Length
					&& KingdomResidents.DepartureCarriersAbsent(system, system.City, id),
					"a departed resident retains a row or binding");
				AbsentIds[gone] = id; AbsentObjects[gone] = Before[i]; gone++;
			}
			Check(alive == Residents.Length && gone == AbsentIds.Length,
				"the saved population is not exactly thirty-five survivors and fifteen absences");
		}

		private static KingdomSubsidenceRungSaveSnapshot Capture(XRLGame Game, Zone Zone,
			KingdomSubsidenceRungNativeFixture Fixture, KingdomSubsidenceRungHeart Heart,
			int[] Residents, string[] Objects, int[] AbsentIds, string[] AbsentObjects)
		{
			KingdomSystem system = Fixture.Base.System;
			string stepWire = system.City.SubsidenceModel;
			KingdomSubsidenceStepBook book = null;
			KingdomSubsidenceRungPlan plan = null;
			GameObject work = null;
			KingdomCityBook.SubsidenceRoofRow roof = null;
			Check(system.City.HasValidSubsidenceStorage() && system.City.TryReadExact(out _, out _)
				&& KingdomSubsidenceStepCodec.TryDecode(stepWire, out book)
				&& book.Sequence == Sequence && book.Active != null
				&& book.Active.Phase == KingdomSubsidenceStepPhase.Settling
				&& book.Active.Id == KingdomSubsidenceRungReleaseCut.StepId
				&& book.Active.PendingDepartureId == "" && book.Active.DueTick == Fixture.Now,
				"the saved parent step is not the exact settling operation at its cut");
			string rungWire = book.Active.RungModel;
			Check(KingdomSubsidenceRungCodec.TryDecode(rungWire, out plan),
				"the saved rung wire did not decode");
			Check(plan.StepId == book.Active.Id && plan.ZoneId == Zone.ZoneID
				&& plan.DueTick == book.Active.DueTick && plan.From == book.Active.FromStage,
				"the saved rung plan identity is not the settling operation's own");
			KingdomSubsidenceRungWork companion = KingdomSubsidenceRungSaveHeartProof.ProveShape(
				plan, Fixture.Work.IDIfAssigned, Heart);
			Check(KingdomSubsidenceRungSaveHeartProof.ExpectedWorkSet(plan.SettlementId, Heart.Id,
					plan.DueTick, plan.From, out int rolled) == Heart.Selected && rolled == Heart.AfterWear
				&& (companion == null || companion.AfterWear == rolled),
				"the frozen rung plan disagrees with production's own re-drawn heart ruin");
			Check(plan.Works[Index].ReleasePhase == KingdomSubsidenceReleasePhase.Intent
				&& plan.Works[Index].WearPhase == KingdomSubsidenceEffectPhase.Proved
				&& plan.Works[Index].BeforeWear == Fixture.BeforeWear
				&& plan.Works[Index].AfterWear == Fixture.AfterWear,
				"the saved primary work does not stand proved under its own release intent");
			Check(plan.Works[Index].Roofs.Count == 1
				&& plan.Works[Index].Roofs[0].Phase == KingdomSubsidenceEffectPhase.Proved,
				"the saved primary roof obligation is not proved");
			Check(KingdomSubsidenceRungSaveLiveProof.Reports(stepWire, plan).Count
				== KingdomSubsidenceRungSaveLiveProof.ExpectedReports(plan),
				"the saved rung does not owe two reports and one per planned work");
			KingdomSubsidenceRungWork row = plan.Works[Index];
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal) { row.ObjectId };
			Check(KingdomPlots.TryCaptureGlobalLiveIds(ids, out Dictionary<string, GameObject> live)
				&& live.Count == 1 && live.TryGetValue(row.ObjectId, out work)
				&& ReferenceEquals(work, Fixture.Work) && GameObject.Validate(work)
				&& work.Blueprint == row.Blueprint && work.CurrentZone == Zone
				&& work.CurrentCell == Zone.GetCell(row.X, row.Y) && work.Count == 1
				&& work.InInventory == null && work.Equipped == null
				&& ReferenceEquals(work.GetPart<r_KingdomWear>(), Fixture.Wear)
				&& ReferenceEquals(Fixture.Wear.ParentObject, work),
				"the saved work identity, ground or wear attachment is not exact");
			int copies = KingdomSubsidenceRungSaveLiveProof.WearCopies(work);
			r_KingdomWear wear = Fixture.Wear;
			KingdomSubsidenceRungSaveLiveProof.ProveDesignation(work, wear, row.PlotId, row.DesignStamp,
				"the live built designation, plot or build-key property is not the exact frozen row");
			KingdomSubsidenceWearReceipt observed = new KingdomSubsidenceWearReceipt(wear.IncidentPhase,
				wear.IncidentId, wear.IncidentCause, wear.IncidentBeforeWear, wear.IncidentAfterWear,
				wear.Wear, wear.LastCause, wear.LastCompletedIncidentId, wear.IncidentLine,
				wear.IncidentMessageState);
			Check(copies == 1 && !wear.LifecycleQuarantined && wear.RepairEffortLeft == 0
				&& wear.LeakPhase == (int)KingdomWearLeakPhase.None
				&& wear.Wear == row.AfterWear && wear.IncidentPhase == (int)KingdomWearIncidentPhase.None
				&& wear.IncidentId == plan.StepId && wear.IncidentLine == row.ReleaseBefore.Line
				&& wear.LastCompletedIncidentId == plan.StepId
				&& KingdomSubsidenceReleaseRules.TryNextWrite(row.ReleaseBefore, row.ReleaseAfter,
					observed, out int field) && field == KingdomSubsidenceRungSaveSnapshot.WriteCut,
				"the saved receipt does not stand at production's own second write cut");
			Check(system.City.TryCaptureSubsidenceRoof(Fixture.HomeResidentId, out roof)
				&& roof.HomeWorkId == KingdomCityRules.StableId(row.ObjectId) && roof.ZoneId == Zone.ZoneID
				&& roof.RoofStanding && roof.Reached == plan.DueTick
				&& roof.Warned == KingdomBrinkRules.Unwarned, "the saved roof tuple is not exact");
			KingdomSubsidenceRungSaveWork frozen = new KingdomSubsidenceRungSaveWork(row.ObjectId,
				row.Blueprint, row.PlotId, row.DesignStamp, row.X, row.Y, copies, row.BeforeWear,
				row.AfterWear, wear.IncidentPhase, wear.IncidentId, wear.IncidentCause,
				wear.IncidentBeforeWear, wear.IncidentAfterWear, wear.Wear, wear.LastCause,
				wear.LastCompletedIncidentId, wear.IncidentLine, wear.IncidentMessageState,
				wear.LifecycleQuarantined);
			KingdomSubsidenceRungSaveRoof carrier = new KingdomSubsidenceRungSaveRoof(roof.ResidentId,
				Fixture.HomeResident.IDIfAssigned, roof.HomeWorkId, roof.Standing, roof.ZoneId,
				roof.RoofStanding, roof.Reached, roof.Warned);
			return new KingdomSubsidenceRungSaveSnapshot(Game.GameID, Zone.ZoneID, stepWire, rungWire,
				plan.StepId, KingdomSubsidenceRungSaveLiveProof.Telling(system), Fixture.Now,
				book.Active.AnchorTick, plan.DueTick,
				book.Sequence, system.LastSubsidenceTick, system.Ledger.Departures, system.Population,
				(int)system.Stage, system.ChronicleEntries.Count, system.OutsiderEntries.Count,
				frozen, carrier, Residents, Objects, AbsentIds, AbsentObjects);
		}

		private static string Save(XRLGame Game, string Root)
		{
			Check(Game.Running && !Game.Transient && !Game.DontSaveThisIsAReplay
				&& (Game.SaveTask == null || Game.SaveTask.IsCompleted), "game cannot perform a real save");
			string directory = KingdomScenarioSaveFiles.SaveDirectory(Root, Game.GameID);
			Check(Directory.GetDirectories(directory).Length == 0, "fresh game has unexpected save subdirectories");
			foreach (string existing in Directory.GetFiles(directory))
				Check(Path.GetFileName(existing) == "Cache.db", "fresh game already has primary or backup save evidence");
			The.ZoneManager.CheckCached(true, true);
			Task save = Game.SaveGame("Primary");
			save?.GetAwaiter().GetResult();
			Check(Directory.GetFiles(directory).Length == 3 && Directory.GetDirectories(directory).Length == 0
				&& File.Exists(Path.Combine(directory, "Cache.db")), "save did not create exactly the three fresh artifacts");
			return directory;
		}

		private static void Receipt(XRLGame Game, string Root, string Folder, string Wire)
		{
			string primary = Path.Combine(Folder, "Primary.sav.gz");
			using (FileStream file = KingdomScenarioSaveFiles.Open(primary, KingdomScenarioSaveFiles.MaxSaveBytes))
				Check(file.ReadByte() == 31 && file.ReadByte() == 139, "primary save is not gzip");
			// Cache.db remains engine-owned here. The stopped-profile copier hashes it after shutdown.
			string receipt = "taf-scenario-save-v1\n" + Game.GameID + "\n"
				+ KingdomScenarioSaveFiles.HashFile(primary, KingdomScenarioSaveFiles.MaxSaveBytes) + "\n"
				+ KingdomScenarioSaveFiles.HashFile(Path.Combine(Folder, "Primary.json"), 1048576) + "\n"
				+ KingdomScenarioSaveFiles.HashText(Wire) + "\n";
			KingdomScenarioSaveFiles.WriteNew(Path.Combine(Root, KingdomScenarioSaveFiles.ReceiptFile), receipt);
			Check(KingdomScenarioSaveFiles.ReadText(Path.Combine(Root, KingdomScenarioSaveFiles.ReceiptFile), 512)
				== receipt, "save receipt did not persist exactly");
		}

		private static void Check(bool Condition, string Failure)
		{
			KingdomScenarioSaveFiles.Require(Condition, Failure ?? "native rung save check refused");
		}
	}
}
