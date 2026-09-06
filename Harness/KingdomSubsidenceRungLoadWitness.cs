using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Cold witness for the D5 rung release cut. Prefix() is called by the harness's one
	/// existing GameRestored hook, before AfterGameLoaded handlers and zone activation. It proves the
	/// saved bytes, receipt and live identities, then arms the passive release observer.</summary>
	internal static class KingdomSubsidenceRungLoadWitness
	{
		internal const string PreactivationLine =
			"exact ss5/sr2 bytes and 35 loaded bodies; release=Intent; native-write-cut=2; wear-fields=10"
			+ "; autoonce=true; before-AfterGameLoaded-handlers-and-zone-activation=true";
		internal const string RecoveryLine =
			"release=Released; population=35; original-step-retired=true; replay=false; status=clear"
			+ "; activation-already-recovered=";
		internal const string ActivationRoute = "native-zone-activation";
		internal const string PrepassRoute = "explicit-production-prepass";

		private static int Attempts;
		private static XRLGame WitnessedGame;
		private static GameObject WitnessedWork;
		private static r_KingdomWear WitnessedWear;
		private static readonly List<GameObject> Bodies = new List<GameObject>();
		private static readonly List<string> Expected = new List<string>();
		private static string Failure;

		internal static void Prefix()
		{
			try
			{
				Attempts++;
				Check(Attempts == 1 && WitnessedGame == null, "rung saved-state witness fired more than once");
				Check(KingdomScenarioLoadReaderWitness.Releases == 1 && !KingdomScenarioLoadReaderWitness.HadErrors,
					"primary reader did not complete exactly once without errors");
				KingdomSubsidenceRungSaveSnapshot snapshot = KingdomScenarioLoadEntry.RungSnapshot;
				Check(snapshot != null, "no sealed rung snapshot was bound for this load");
				XRLGame game = The.Game;
				Check(game != null && game.GameID == snapshot.GameId && game.TimeTicks == snapshot.Now
					&& game.GetSystem<KingdomScenarioAutoRunner>()?.HasConsideredScript == true
					&& KingdomSubsidenceRungSaveSnapshotCodec.MatchesCurrentPrefix(KingdomScenarioLoadEntry.SnapshotWire)
					&& KingdomScenarioDurableState.ProvesExactText(KingdomScenarioSaveFiles.SnapshotKey,
						KingdomScenarioLoadEntry.SnapshotWire),
					"loaded game, clock, snapshot or autoonce field changed");
				KingdomSystem system = game.GetSystem<KingdomSystem>();
				Zone zone = game.ZoneManager?.ActiveZone;
				Check(system != null && zone != null && zone.ZoneID == snapshot.ZoneId
					&& ReferenceEquals(The.Player?.CurrentZone, zone)
					&& KingdomScenarioSaveFiles.Same(System.IO.Path.GetFullPath(game.GetCacheDirectory()),
						KingdomScenarioSaveFiles.SaveDirectory(KingdomScenarioSaveFiles.Root(), snapshot.GameId)),
					"loaded ground or rebased cache directory is not exact");
				KingdomSubsidenceRungPlan plan = Frozen(system, snapshot);
				KingdomScenarioSaveAuthorityChecks.VerifyExact(system, zone);
				Census(system, zone, snapshot);
				Receipt(system, zone, snapshot, plan);
				Check(system.ChronicleEntries.Count == snapshot.ChronicleCount
					&& system.OutsiderEntries.Count == snapshot.OutsiderCount
					&& KingdomSubsidenceRungSaveLiveProof.Telling(system) == snapshot.TellingDigest,
					"the loaded telling prefix is not the exact saved prefix");
				Expected.Clear();
				Expected.AddRange(KingdomSubsidenceRungSaveLiveProof.Reports(snapshot.StepWire, plan));
				KingdomSubsidenceRungLoadHeartWitness.Arm(game, system, zone, snapshot, plan);
				WitnessedGame = game;
				Check(KingdomScenarioJournal.Append("LOAD-PREACTIVATION", true, PreactivationLine) == null,
					"pre-activation journal unavailable");
			}
			catch (Exception error)
			{
				Failure = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message);
				KingdomScenarioJournal.Append("LOAD-PREACTIVATION", false, Failure);
			}
		}

		internal static string VerifyRecovered(XRLGame Game, KingdomSubsidenceRungSaveSnapshot Snapshot)
		{
			Check(Failure == null && Attempts == 1 && Game != null && ReferenceEquals(WitnessedGame, Game)
				&& Snapshot != null && ReferenceEquals(Snapshot, KingdomScenarioLoadEntry.RungSnapshot)
				&& Bodies.Count == KingdomSubsidenceRungSaveSnapshot.SurvivorCount
				&& Expected.Count == KingdomSubsidenceRungLoadHeartWitness.Reports
				&& GameObject.Validate(WitnessedWork) && WitnessedWear != null
				&& KingdomSubsidenceRungReleaseCut.Armed,
				"no explicit exact-save rung pre-activation witness: " + Failure);
			KingdomSystem system = Game.GetSystem<KingdomSystem>();
			Zone zone = Game.ZoneManager.ActiveZone;
			KingdomScenarioSaveAuthorityChecks.VerifyReconciled(system, zone);
			bool activationRecovered = !KingdomSubsidenceStepRuntime.HasPending(system);
			try
			{
				KingdomSurvey survey = KingdomSurvey.Take(zone, system);
				using (survey.BindPass())
				{
					if (!activationRecovered)
						Check(KingdomSubsidenceStepRuntime.TryBeforePass(system, zone, survey, out string refusal),
							refusal);
					VerifyAfter(system, zone, Snapshot);
					string wire = system.City.SubsidenceModel;
					string[] official = system.ChronicleEntries.ToArray();
					string[] outsider = system.OutsiderEntries.ToArray();
					Check(KingdomSubsidenceStepRuntime.TryBeforePass(system, zone, survey, out string retry), retry);
					VerifyAfter(system, zone, Snapshot);
					Check(system.City.SubsidenceModel == wire
						&& KingdomSubsidenceRungSaveLiveProof.SameLines(system.ChronicleEntries, official)
						&& KingdomSubsidenceRungSaveLiveProof.SameLines(system.OutsiderEntries, outsider)
						&& Game.TimeTicks == Snapshot.Now,
						"post-load same-tick retry replayed state or telling");
				}
			}
			finally
			{
				KingdomSubsidenceRungReleaseCut.Disarm();
				KingdomSubsidenceRungLoadHeartWitness.Disarm();
			}
			Check(KingdomScenarioJournal.Append("LOAD-RECOVERY", true,
				RecoveryLine + (activationRecovered ? "true" : "false")) == null,
				"post-load recovery journal unavailable");
			return activationRecovered ? ActivationRoute : PrepassRoute;
		}

		private static KingdomSubsidenceRungPlan Frozen(KingdomSystem System,
			KingdomSubsidenceRungSaveSnapshot Snapshot)
		{
			KingdomSubsidenceStepBook book = null;
			KingdomSubsidenceRungPlan plan = null;
			Check(System.Founded && System.City != null && System.City.HasValidSubsidenceStorage()
				&& System.City.TryReadExact(out _, out _)
				&& System.City.SubsidenceModel == Snapshot.StepWire
				&& KingdomSubsidenceStepCodec.TryDecode(Snapshot.StepWire, out book)
				&& book.Sequence == Snapshot.Sequence && book.Active != null && book.Active.Id == Snapshot.StepId
				&& book.Active.Phase == KingdomSubsidenceStepPhase.Settling
				&& book.Active.RungModel == Snapshot.RungWire && book.Active.AnchorTick == Snapshot.AnchorTick
				&& book.Active.DueTick == Snapshot.DueTick && book.Active.PendingDepartureId == ""
				&& KingdomResidentDepartureRules.IsEmpty(System.ResidentDeparture)
				&& System.LastSubsidenceTick == Snapshot.LastSubsidenceTick
				&& System.Ledger.Departures == Snapshot.LedgerDepartures && (int)System.Stage == Snapshot.Stage,
				"the loaded parent step is not the exact saved ss5 bytes at its release cut");
			Check(KingdomSubsidenceRungCodec.TryDecode(Snapshot.RungWire, out plan)
				&& plan.StepId == Snapshot.StepId && plan.ZoneId == Snapshot.ZoneId
				&& plan.DueTick == Snapshot.DueTick && plan.Works.Count >= 1
				&& plan.Works[0].ReleasePhase == KingdomSubsidenceReleasePhase.Intent
				&& plan.Works[0].WearPhase == KingdomSubsidenceEffectPhase.Proved
				&& plan.Works[0].ObjectId == Snapshot.Work.ObjectId
				&& plan.Works[0].Blueprint == Snapshot.Work.Blueprint
				&& plan.Works[0].PlotId == Snapshot.Work.PlotId
				&& plan.Works[0].DesignStamp == Snapshot.Work.DesignStamp
				&& plan.Works[0].BeforeWear == Snapshot.Work.BeforeWear
				&& plan.Works[0].AfterWear == Snapshot.Work.AfterWear && plan.Works[0].Roofs.Count == 1
				&& plan.Works[0].Roofs[0].Phase == KingdomSubsidenceEffectPhase.Proved
				&& plan.Works[0].Roofs[0].ResidentId == Snapshot.Roof.ResidentId
				&& plan.Works[0].Roofs[0].BodyObjectId == Snapshot.Roof.BodyObjectId,
				"the loaded rung plan is not the exact saved sr2 bytes at release=Intent");
			return plan;
		}

		private static void Census(KingdomSystem System, Zone Zone, KingdomSubsidenceRungSaveSnapshot Snapshot)
		{
			Check(System.Population == KingdomSubsidenceRungSaveSnapshot.SurvivorCount
				&& KingdomResidents.OnRollCount(System) == KingdomSubsidenceRungSaveSnapshot.SurvivorCount,
				"the loaded population is not the saved thirty-five");
			HashSet<string> ids = new HashSet<string>(Snapshot.ObjectIds, StringComparer.Ordinal);
			foreach (string absent in Snapshot.AbsentObjectIds) ids.Add(absent);
			ids.Add(Snapshot.Work.ObjectId);
			Dictionary<string, GameObject> live = null;
			Check(KingdomPlots.TryCaptureGlobalLiveIds(ids, out live)
				&& live.Count == KingdomSubsidenceRungSaveSnapshot.SurvivorCount + 1,
				"the loaded live-body census disagrees with the saved thirty-five and one work");
			Bodies.Clear();
			for (int i = 0; i < Snapshot.ObjectIds.Count; i++)
			{
				GameObject body = null;
				Check(live.TryGetValue(Snapshot.ObjectIds[i], out body)
					&& KingdomSubsidenceRungSaveLiveProof.ExactBody(System, Zone, body,
						Snapshot.ResidentIds[i], Snapshot.ObjectIds[i]),
					"a loaded resident row, body or binding changed");
				Bodies.Add(body);
			}
			for (int i = 0; i < Snapshot.AbsentObjectIds.Count; i++)
				Check(!live.ContainsKey(Snapshot.AbsentObjectIds[i])
					&& KingdomResidents.DepartureCarriersAbsent(System, System.City, Snapshot.AbsentResidentIds[i]),
					"an already-departed resident returned in the loaded authority");
		}

		private static void Receipt(KingdomSystem System, Zone Zone,
			KingdomSubsidenceRungSaveSnapshot Snapshot, KingdomSubsidenceRungPlan Plan)
		{
			KingdomSubsidenceRungSaveWork saved = Snapshot.Work;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal) { saved.ObjectId };
			GameObject work = null;
			Check(KingdomPlots.TryCaptureGlobalLiveIds(ids, out Dictionary<string, GameObject> live)
				&& live.Count == 1 && live.TryGetValue(saved.ObjectId, out work)
				&& GameObject.Validate(work) && work.IDIfAssigned == saved.ObjectId
				&& work.Blueprint == saved.Blueprint && work.CurrentZone == Zone
				&& work.CurrentCell == Zone.GetCell(saved.X, saved.Y) && work.Count == 1
				&& work.InInventory == null && work.Equipped == null,
				"the loaded work identity or ground is not exact");
			r_KingdomWear wear = work.GetPart<r_KingdomWear>();
			KingdomSubsidenceRungSaveLiveProof.ProveDesignation(work, wear, saved.PlotId, saved.DesignStamp,
				"the loaded built designation, plot or build-key property is not the exact saved row");
			Check(KingdomSubsidenceRungSaveLiveProof.WearCopies(work) == saved.PartCopies,
				"the loaded wear attachment is not the single exact part");
			Check(wear.IncidentPhase == saved.IncidentPhase && wear.IncidentId == saved.IncidentId
				&& wear.IncidentCause == saved.IncidentCause
				&& wear.IncidentBeforeWear == saved.IncidentBeforeWear
				&& wear.IncidentAfterWear == saved.IncidentAfterWear && wear.Wear == saved.Wear
				&& wear.LastCause == saved.LastCause
				&& wear.LastCompletedIncidentId == saved.LastCompletedIncidentId
				&& wear.IncidentLine == saved.IncidentLine
				&& wear.IncidentMessageState == saved.IncidentMessageState
				&& wear.LifecycleQuarantined == saved.Quarantined && !wear.LifecycleQuarantined
				&& wear.IncidentPhase == (int)KingdomWearIncidentPhase.None
				&& wear.IncidentId == Snapshot.StepId && wear.IncidentLine == Plan.Works[0].ReleaseBefore.Line
				&& wear.LastCompletedIncidentId == Snapshot.StepId && wear.Wear == saved.AfterWear,
				"the loaded ten wear-receipt fields are not the exact saved cut");
			KingdomSubsidenceRungWork row = Plan.Works[0];
			Check(KingdomSubsidenceReleaseRules.TryNextWrite(row.ReleaseBefore, row.ReleaseAfter,
				new KingdomSubsidenceWearReceipt(wear.IncidentPhase, wear.IncidentId, wear.IncidentCause,
					wear.IncidentBeforeWear, wear.IncidentAfterWear, wear.Wear, wear.LastCause,
					wear.LastCompletedIncidentId, wear.IncidentLine, wear.IncidentMessageState),
				out int field) && field == KingdomSubsidenceRungSaveSnapshot.WriteCut,
				"the loaded receipt does not stand at production's own second write cut");
			Check(System.City.TryCaptureSubsidenceRoof(Snapshot.Roof.ResidentId,
				out KingdomCityBook.SubsidenceRoofRow roof) && roof.HomeWorkId == Snapshot.Roof.HomeWorkId
				&& roof.HomeWorkId == KingdomCityRules.StableId(saved.ObjectId)
				&& roof.Standing == Snapshot.Roof.Standing && roof.ZoneId == Snapshot.Roof.ZoneId
				&& roof.RoofStanding == Snapshot.Roof.RoofStanding && roof.RoofStanding
				&& roof.Reached == Snapshot.Roof.Reached && roof.Reached == Plan.DueTick
				&& roof.Warned == Snapshot.Roof.Warned && roof.Warned == KingdomBrinkRules.Unwarned,
				"the loaded roof tuple is not exact");
			WitnessedWork = work; WitnessedWear = wear;
		}

		private static void VerifyAfter(KingdomSystem System, Zone Zone,
			KingdomSubsidenceRungSaveSnapshot Snapshot)
		{
			Check(KingdomSubsidenceRungReleaseCut.PublishedReleased
				&& KingdomSubsidenceRungReleaseCut.Fault == null
				&& KingdomSubsidenceRungReleaseCut.Throws == 0
				&& KingdomSubsidenceRungReleaseCut.Writes == (Snapshot.Work.IncidentLine == null ? 1 : 2)
				&& KingdomSubsidenceRungReleaseCut.Fields == (Snapshot.Work.IncidentLine == null ? "2" : "2,3")
				&& KingdomSubsidenceRungReleaseCut.StepId == Snapshot.StepId,
				"the durable parent publication of the release was never observed");
			Check(System.Founded && System.Population == KingdomSubsidenceRungSaveSnapshot.SurvivorCount
				&& KingdomResidents.OnRollCount(System) == KingdomSubsidenceRungSaveSnapshot.SurvivorCount
				&& (int)System.Stage == Snapshot.Stage
				&& System.Ledger.Departures == Snapshot.LedgerDepartures
				&& System.LastSubsidenceTick == Snapshot.Now
				&& KingdomResidentDepartureRules.IsEmpty(System.ResidentDeparture)
				&& KingdomSubsidenceStepCodec.TryDecode(System.City.SubsidenceModel,
					out KingdomSubsidenceStepBook book) && book.Active == null
				&& book.Sequence == Snapshot.Sequence && book.LastRetiredTick == Snapshot.DueTick
				&& book.BatchModel == KingdomSubsidenceBatchRules.None
				&& book.FailureModel == KingdomSubsidenceReportArchive.None,
				"the loaded step did not retire its original release exactly");
			KingdomSubsidenceRungSaveWork saved = Snapshot.Work;
			KingdomSubsidenceRungSaveLiveProof.ProveDesignation(WitnessedWork, WitnessedWear, saved.PlotId,
				saved.DesignStamp, "the released designation, plot or build-key property left the saved row");
			Check(WitnessedWork.IDIfAssigned == saved.ObjectId
				&& WitnessedWear.Wear == saved.Wear && WitnessedWear.Wear == saved.AfterWear
				&& WitnessedWear.IncidentCause == saved.IncidentCause
				&& WitnessedWear.IncidentBeforeWear == saved.IncidentBeforeWear
				&& WitnessedWear.IncidentAfterWear == saved.IncidentAfterWear
				&& WitnessedWear.LastCause == saved.LastCause
				&& WitnessedWear.IncidentMessageState == saved.IncidentMessageState
				&& !WitnessedWear.LifecycleQuarantined
				&& WitnessedWear.IncidentPhase == (int)KingdomWearIncidentPhase.None
				&& WitnessedWear.IncidentId == null && WitnessedWear.IncidentLine == null
				&& WitnessedWear.LastCompletedIncidentId == Snapshot.StepId,
				"the released receipt did not reach its exact target and preserve retained fields");
			Check(System.City.TryCaptureSubsidenceRoof(Snapshot.Roof.ResidentId,
				out KingdomCityBook.SubsidenceRoofRow roof) && roof.ResidentId == Snapshot.Roof.ResidentId
				&& roof.HomeWorkId == Snapshot.Roof.HomeWorkId
				&& roof.HomeWorkId == KingdomCityRules.StableId(saved.ObjectId)
				&& roof.Standing == Snapshot.Roof.Standing && roof.ZoneId == Snapshot.Roof.ZoneId
				&& roof.RoofStanding == Snapshot.Roof.RoofStanding && roof.RoofStanding
				&& roof.Reached == Snapshot.Roof.Reached && roof.Reached == Snapshot.DueTick
				&& roof.Warned == Snapshot.Roof.Warned && roof.Warned == KingdomBrinkRules.Unwarned,
				"the recovered roof tuple changed");
			for (int i = 0; i < Bodies.Count; i++)
				Check(KingdomSubsidenceRungSaveLiveProof.ExactBody(System, Zone, Bodies[i],
					Snapshot.ResidentIds[i], Snapshot.ObjectIds[i]),
					"a loaded survivor lost its exact row, body or binding");
			KingdomSubsidenceRungLoadHeartWitness.Recovered(Zone, Snapshot.StepId);
			KingdomSubsidenceRungSaveLiveProof.ProveTelling(System, Snapshot, Expected,
				KingdomSubsidenceRungLoadHeartWitness.Reports);
			Check(KingdomSubsidenceStepRuntime.Status(System) == "",
				"the recovered subsidence account is not clear");
		}

		private static void Check(bool Condition, string Detail)
		{
			KingdomScenarioSaveFiles.Require(Condition, Detail ?? "native rung load witness refused");
		}
	}
}
