using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomRaidMasterTurnNativeChecks
	{
		private static Frame Retained;
		internal static string Run(string verb, XRLGame game, Zone zone, out bool ok)
		{
			ok = false; string failure = null; bool final = verb == KingdomRaidMasterTurnNativeProvider.CheckVerb;
			try
			{
				if (verb == KingdomRaidMasterTurnNativeProvider.SetupVerb)
				{ Require(Retained == null, "master turn attempt already retained"); Retained = new Frame(game, zone); Retained.Setup(); }
				else
				{
					Require(Retained != null, "master turn setup absent"); Retained.ExactCaller(game, zone);
					if (final) Retained.Check(); else Retained.Resume();
				}
				ok = true;
			}
			catch (Exception error) { failure = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message); }
			finally
			{
				if (final || !ok) try { Retained?.Disarm(); }
					catch (Exception error) { ok = false; failure = "master turn cleanup refused: " + error.GetType().Name; }
			}
			return "native-raid-master-turn " + (final ? "cases=1 passed=" + (ok ? "1" : "0") + " failed=" + (ok ? "0" : "1") : "step=" + verb + " ok=" + ok)
				+ "; synthetic=true; ordinary-acceptance=false; save-load=untested; focused-7a-7c-only=true; retained=true"
				+ (failure == null ? "" : "\nFAIL " + failure) + Retained?.Evidence;
		}
		internal static void Observe(int stage, KingdomSystem system, long tick, Zone zone, bool result)
		{ Retained?.Witness?.Observe(stage, system, tick, zone, result); }
		private static void Require(bool value, string failure) { KingdomRaidMasterTurnNativeProvider.Require(value, failure); }
		private sealed class Frame
		{
			private readonly XRLGame Game;
			private readonly Zone Zone;
			private readonly ZoneManager Manager;
			private readonly GameObject Player;
			private readonly string GameId, ZoneId, Provenance;
			private readonly long Turns, Tick, Actions, PlayerActions;
			private readonly GameObject[] Actors = new GameObject[3];
			private readonly KingdomRaidRecoveryDeathSubject[] Bodies = new KingdomRaidRecoveryDeathSubject[3];
			private readonly bool[] Dead = new bool[3];
			private readonly List<byte[]> Boundaries = new List<byte[]>();
			private KingdomRaidLaunchNativeFixture Fixture;
			private KingdomLifecycleBook Book;
			private KingdomLifecycleOperation Operation;
			private KingdomRaidContactBody Store;
			private KingdomRaidDeathZoneEvidence Rows;
			private KingdomRaidRecoveryQuestEvidence Quest;
			private object OriginGraveyard, OriginQueue;
			private GameObject[][] FinalRoots;
			private GameObject[] FinalGraves;
			private byte[] ActiveWire, PausedWire, CheckedWire, SettledWire;
			private string OperationId, PlanHash;
			private long ReadyTick, ReadySequence, CheckTick, CheckTurns, CheckActions, CheckPlayerActions;
			private int Phase, BeforeMasterWater, CheckWater;
			private bool Armed, ReadySeen;
			internal KingdomRaidMasterTurnWitness Witness;
			internal readonly StringBuilder Evidence = new StringBuilder();
			internal Frame(XRLGame game, Zone zone)
			{
				Game = game; Zone = zone; Manager = The.ZoneManager; Player = The.Player;
				GameId = game.GameID; ZoneId = zone.ZoneID; Turns = game.Turns; Tick = game.TimeTicks;
				Actions = game.ActionTicks; PlayerActions = game.PlayerActionTicks;
				Provenance = game.GetStringGameState(KingdomScenarioProvenanceRules.ProvenanceState, null);
			}
			internal void ExactCaller(XRLGame game, Zone zone)
			{ Require(ReferenceEquals(game, Game) && ReferenceEquals(zone, Zone), "caller lost exact master turn owner"); Owner(); }
			internal void Setup()
			{
				Owner(); LaunchAndContact(); Recovery(KingdomRaidRecoveryState.Offered, 216);
				var row = Incident(); Require(!Game.Quests.ContainsKey(row.RecoveryQuestId) && !Game.FinishedQuests.ContainsKey(row.RecoveryQuestId), "quest ID already present");
				long sequence = Book.RaidNextSequence;
				Require(KingdomRaids.TryAcceptRecovery(Fixture.System, out string failure) && failure == null, "actual accept refused: " + failure);
				Quest = new KingdomRaidRecoveryQuestEvidence(Game, Fixture.System, Incident()); Proof(KingdomLifecycleAction.RaidRecoveryAccept, 1, sequence, Tick); ActiveWire = Wire();
				for (int i = 0; i < 3; i++)
				{
					Recovery(KingdomRaidRecoveryState.Active, 216); Quest.Exact(false); Require(Same(ActiveWire, Wire()), "active pre-death wire changed");
					Rows.Record(Actors); Rows.At(Actors[i], Bodies[i].Cell);
					Require(!Actors[i].IsPlayer() && !Actors[i].IsPlayerLed() && !Actors[i].IsDying, "death subject is not an owned hostile original");
					Actors[i].Die(Force: true); Rows.Record(Actors); Bodies[i].Identity(); Rows.Dead(Actors[i]); Dead[i] = true;
					Recovery(KingdomRaidRecoveryState.Active, 216); Quest.Exact(false); Require(Same(ActiveWire, Wire()), "death finalized recovery before an allowed wake");
				}
				Proof(KingdomLifecycleAction.RaidRecoveryReady, 0); Proof(KingdomLifecycleAction.RaidRecoveryResolve, 0);
				OriginGraveyard = Zone.Graveyard; OriginQueue = Zone.Graveyard.Objects; Phase = 1;
				Witness = new KingdomRaidMasterTurnWitness(Game, Fixture.System, Zone, Boundary); Witness.SetOption(false);
				Owner(); Recovery(KingdomRaidRecoveryState.Active, 216); Require(Same(ActiveWire, Wire()), "option callback changed retained Active raid");
				Evidence.Append("\nsetup contact=240->216 plunder=24 recovery=Active graves=3 option=No awaiting=advance-1");
			}
			internal void Resume()
			{
				Require(Phase == 1, "resume verb repeated/out of order"); Owner(); Witness.Verify(1);
				Recovery(KingdomRaidRecoveryState.Active, Store.Liquid.Volume); Quest.Exact(false); OriginalGraves();
				PausedWire = Wire(); Require(Same(ActiveWire, PausedWire) && Witness.Raids == 0, "paused turns changed Active raid or entered raid wake");
				Evidence.Append("\npaused-dispatches=").Append(Witness.Dispatches).Append(" raids=0 option=No disabled-tick=").Append(Witness.DisabledTick)
					.Append(" turns=").Append(Game.Turns).Append(" tick=").Append(Game.TimeTicks).Append(" water=").Append(Store.Liquid.Volume);
				int beforeOptionWater = Store.Liquid.Volume; Phase = 2; Witness.BeginResume(); Owner();
				Recovery(KingdomRaidRecoveryState.Active, beforeOptionWater); Quest.Exact(false);
				Evidence.Append("\noption=Yes awaiting=advance-2; first transition remains separately observed");
			}
			private void Boundary(int stage)
			{
				bool allowBound = ReadySeen && stage >= 1 && stage <= 4; Owner(allowBound);
				Require(Phase == 1 || Phase == 2, "observation outside retained pause/resume phase");
				if (stage == 1) BeforeMasterWater = Store.Liquid.Volume;
				if (stage == 2) Require(Store.Liquid.Volume == BeforeMasterWater, "master observation physically changed exact store");
				Recovery(ReadySeen || stage == 4 ? KingdomRaidRecoveryState.Ready : KingdomRaidRecoveryState.Active, Store.Liquid.Volume, allowBound); Quest.Exact(false);
				if (!ReadySeen && stage == 3) { ReadySequence = Book.RaidNextSequence; ReadyTick = Game.TimeTicks; }
				if (ReadySeen || stage == 4) { Proof(KingdomLifecycleAction.RaidRecoveryReady, 1, ReadySequence, ReadyTick); ReadySeen = true; }
				else Proof(KingdomLifecycleAction.RaidRecoveryReady, 0);
				Proof(KingdomLifecycleAction.RaidRecoveryResolve, 0); byte[] wire = Wire();
				if (!Witness.Resuming) Require(Same(ActiveWire, wire), "disabled callback changed canonical Active raid wire");
				Require(Boundaries.Count < 2304, "master boundary evidence exceeds bound"); Boundaries.Add(wire);
				Evidence.Append("\nphase=").Append(Phase).Append(" stage=").Append(stage).Append(" dispatch=").Append(Witness.Dispatches)
					.Append(" turns=").Append(Game.Turns).Append(" tick=").Append(Game.TimeTicks).Append(" recovery=").Append(Incident().RecoveryState)
					.Append(" latch=").Append(Fixture.System.MasterOption).Append(" token=").Append(Fixture.System.MasterResumeToken)
					.Append(" applied=").Append(Fixture.System.MasterAppliedResumeToken).Append(" water=").Append(Store.Liquid.Volume);
			}
			internal void Check()
			{
				Require(Phase == 2, "check verb repeated/out of order"); Owner(); Witness.Verify(2);
				Require(Witness.ResumeApplications == 1 && Witness.ResumeTick >= 0 && ReadySeen && ReadyTick > Witness.ResumeTick
					&& Game.ActionTicks >= Actions && Game.PlayerActionTicks >= PlayerActions, "resume/later-ready transition or action clock differs");
				Phase = 3; CheckTick = Game.TimeTicks; CheckTurns = Game.Turns; CheckActions = Game.ActionTicks; CheckPlayerActions = Game.PlayerActionTicks;
				CheckWater = Store.Liquid.Volume; Require(CheckWater >= 0 && CheckWater <= Store.Liquid.MaxVolume, "post-turn store volume invalid");
				Recovery(KingdomRaidRecoveryState.Ready, CheckWater); Quest.Exact(false); Proof(KingdomLifecycleAction.RaidRecoveryReady, 1, ReadySequence, ReadyTick);
				CheckedWire = Wire(); RetainWorld(); long sequence = Book.RaidNextSequence; AtCheckClock();
				Require(KingdomRaids.TryResolveRecovery(Fixture.System, Zone, out string failure) && failure == null, "actual explicit completion refused: " + failure);
				AtCheckClock(); Recovery(KingdomRaidRecoveryState.Resolved, CheckWater); Quest.Exact(true); Proof(KingdomLifecycleAction.RaidRecoveryResolve, 1, sequence, CheckTick);
				SettledWire = Wire(); Require(!KingdomRaids.TryResolveRecovery(Fixture.System, Zone, out failure) && !string.IsNullOrEmpty(failure), "repeat turn-in accepted");
				AtCheckClock(); Recovery(KingdomRaidRecoveryState.Resolved, CheckWater); Quest.Exact(true); Require(Same(SettledWire, Wire()), "repeat changed settled authority");
				Proof(KingdomLifecycleAction.RaidRecoveryReady, 1, ReadySequence, ReadyTick); Proof(KingdomLifecycleAction.RaidRecoveryResolve, 1, sequence, CheckTick); OriginalGraves(); Witness.Verify(2);
				Evidence.Append("\nresume-dispatches=").Append(Witness.Dispatches).Append(" resume-token-applications=1 transition-tick=").Append(Witness.ResumeTick)
					.Append(" ready-tick=").Append(ReadyTick).Append(" ready=1 explicit=1 repeat=unchanged graves=3 mints=3 option=Yes world-effects=retained");
			}
			private void LaunchAndContact()
			{
				Owner(); Evidence.Append('\n').Append(KingdomRaidLaunchNativeProvider.ProfilesLoaded(out int loaded, out _));
				Require(loaded == KingdomRaidLaunchNativeProvider.ShippedFactions.Length && r_TAF_RaidMintProbe.Vacant, "profile/probe setup refused");
				r_TAF_RaidMintProbe.ResetProbe(); r_TAF_RaidMintProbe.Arm(0, null); Armed = true;
				Require(KingdomRaidLaunchNativeFixture.TryCreate(Zone, out Fixture, out string failure), failure); Book = Fixture.System.LifecycleBook;
				Owner(); Require(r_TAF_RaidMintProbe.Snapshot().Length == 0, "premature mint"); Fixture.Activate(); Owner(); Operation = Book.Raid;
				Require(Operation != null && Operation.Action == KingdomLifecycleAction.RaidAttack && Operation.Phase == KingdomLifecyclePhase.EffectIntent
					&& Operation.EffectState == KingdomLifecyclePhysicalState.Prepared && Operation.PlunderProved == 0
					&& Operation.PlunderRequested == 24 && Operation.Fault == null, "launch lacks untouched contact boundary");
				OperationId = Operation.Id; PlanHash = Operation.PlanHash; var observations = r_TAF_RaidMintProbe.Snapshot();
				Require(observations.Length == 3 && r_TAF_RaidMintProbe.Retained == 3 && Operation.PartySize == 3 && Operation.Spawned == 3 && Operation.Projections.Count == 3, "launch lacks three originals");
				for (int i = 0; i < 3; i++)
				{
					KingdomRaidLaunchNativeChecks.VerifyMint(observations[i], Operation, i); KingdomRaidLaunchNativeChecks.VerifyBody(Fixture, Operation, Operation.Projections[i], i);
					Actors[i] = observations[i].Original; Require(observations[i].Substitute == null && ReferenceEquals(Zone.FindObjectByID(Operation.Projections[i].ObjectId), Actors[i]), "foreign mint result");
					Bodies[i] = new KingdomRaidRecoveryDeathSubject(Actors[i]);
				}
				Store = new KingdomRaidContactBody(Fixture.Store); Store.Exact(Store.OriginalCell, 240);
				Require(Store.Body.IDIfAssigned == Operation.Origin && Store.OriginalCell.X == Operation.Target && Store.OriginalCell.Y == Operation.Count, "store target differs");
				KingdomRaidDeathZoneEvidence.PrepareRetention(Game, Manager, Zone, Evidence); Rows = new KingdomRaidDeathZoneEvidence(Zone);
				var actor = new KingdomRaidContactBody(Actors[0]); Cell contact = null;
				foreach (string direction in new[] { "N", "E", "S", "W" }) { Cell cell = Store.OriginalCell.GetLocalCellFromDirection(direction); if (Clear(cell)) { contact = cell; break; } }
				Require(contact != null, "no clear contact cell; clearing not authorized"); byte[] before = Wire(); Owner();
				Require(actor.Body.SystemMoveTo(contact, energyCost: 0, forced: false, ignoreCombat: true, ignoreGravity: false, noStack: true), "contact move refused");
				Owner(); actor.Exact(contact); Store.Exact(Store.OriginalCell, 240); Rows.Record(Actors); Rows.At(actor.Body, contact); Require(Same(before, Wire()), "movement changed raid authority");
				KingdomRaids.StepRaider(actor.Body, actor.Objective, Tick); Owner(); actor.Exact(contact); Store.Exact(Store.OriginalCell, 216);
				Require(Book.Raid == null && Operation.Phase == KingdomLifecyclePhase.Terminal && Operation.Fault == null
					&& Operation.EffectState == KingdomLifecyclePhysicalState.Proved && Operation.PlunderProved == 24, "contact did not settle exact debit");
				Proof(KingdomLifecycleAction.RaidAttack, 1, Operation.Sequence, Tick); Bodies[1].Exact(null); Bodies[2].Exact(null);
				Bodies[0] = new KingdomRaidRecoveryDeathSubject(Actors[0]); Rows.Record(Actors);
			}
			private KingdomRaidIncident Incident() { return KingdomRaidIncidentRules.Incident(Book.RaidLedger, Fixture.Incident.Id); }
			private void Recovery(KingdomRaidRecoveryState state, int water, bool bound = false)
			{
				Owner(bound); Store.Exact(Store.OriginalCell, water); var row = Incident();
				Require(KingdomLiquids.CanReceiveFreshWater(Store.Liquid) && r_TAF_RaidMintProbe.Retained == 3 && r_TAF_RaidMintProbe.Snapshot().Length == 3
					&& Book.Raid == null && Operation.Id == OperationId && Operation.PlanHash == PlanHash && Operation.Fault == null
					&& Operation.Phase == KingdomLifecyclePhase.Terminal && Operation.EffectState == KingdomLifecyclePhysicalState.Proved && Operation.PlunderProved == 24
					&& row != null && row.Id == Fixture.Incident.Id && row.SettlementId == Book.SettlementId && row.TargetZoneId == ZoneId && row.AttackOperationId == OperationId
					&& row.State == KingdomRaidIncidentState.Resolved && row.Resolution == KingdomRaidResolution.StoresPlundered && row.PlunderProved == 24 && row.RecoveryState == state, "master turn recovery authority differs");
				Require(KingdomRaids.HasWatchDisarray(Fixture.System) == (state != KingdomRaidRecoveryState.Resolved), "watch wound differs");
				for (int i = 0; i < 3; i++) { if (Dead[i]) { Bodies[i].Identity(); Rows.Dead(Actors[i]); } else Bodies[i].Exact(null); }
			}
			private void Proof(KingdomLifecycleAction action, int count, long sequence = 0, long tick = 0)
			{
				int found = 0; foreach (var proof in Book.RecentProofs) if (proof.Action == action)
				{ Require(proof.Lane == KingdomLifecycleLane.Raid && proof.Sequence == sequence && proof.Tick == tick
					&& proof.Id == KingdomLifecycleRules.OperationId(Book.SettlementId, proof.Lane, sequence) && !string.IsNullOrEmpty(proof.PlanHash)
					&& (action != KingdomLifecycleAction.RaidAttack || proof.PlanHash == PlanHash), "terminal proof differs"); found++; }
				Require(found == count, "unexpected " + action + " proof count " + found);
			}
			private void Owner(bool bound = false)
			{
				Require(ReferenceEquals(The.Game, Game) && Game.GameID == GameId && ReferenceEquals(The.ZoneManager, Manager) && ReferenceEquals(The.Player, Player)
					&& ReferenceEquals(Player?.Physics?._CurrentCell?.ParentZone, Zone) && ReferenceEquals(Manager.ActiveZone, Zone) && Zone.ZoneID == ZoneId
					&& Manager.CachedZones.TryGetValue(ZoneId, out var cached) && ReferenceEquals(cached, Zone)
					&& KingdomScenarioDurableState.ProvesExactText(KingdomRaidMasterTurnNativeProvider.Receipt, "intent")
					&& KingdomScenarioDurableState.ProvesExactText(KingdomScenarioProvenanceRules.ProvenanceState, Provenance), "master turn world/intent changed");
				if (Phase == 0) Require(Game.Turns == Turns && Game.TimeTicks == Tick && Game.ActionTicks == Actions && Game.PlayerActionTicks == PlayerActions, "setup advanced clocks");
				if (Book == null) return; var system = Fixture.System;
				bool owned = ReferenceEquals(KingdomRaidLaunchNativeFixture.LastAttempt, Fixture) && ReferenceEquals(Fixture.Game, Game) && ReferenceEquals(Fixture.Zone, Zone)
					&& ReferenceEquals(Game.GetSystem<KingdomSystem>(), system) && ReferenceEquals(system.LifecycleBook, Book) && system.Founded && !system.LoadFailed
					&& !system.RealmRetirementBlocksWork && system.ClaimedZones.Contains(ZoneId) && ReferenceEquals(r_TAF_RaidMintProbe.Book, Book) && r_TAF_RaidMintProbe.Armed
					&& KingdomRaids.Enabled && KingdomLifecycleRules.CanOwnAuthority(Book) && !Book.Quarantined && !Book.WireRejected
					&& (!KingdomSurvey.HasBoundPass || (bound && ReferenceEquals(KingdomSurvey.ActiveFor(Zone)?.Ground, Zone)));
				if (!owned) RecordOwnerFailure(system);
				Require(owned, "master turn raid owner changed");
				if (Witness != null) Witness.CurrentLatch(); else Require(KingdomMaster.AutomaticWorkAllowed(system), "setup automatic authority absent");
			}
			private void RecordOwnerFailure(KingdomSystem system)
			{
				var growth = Book.Growth;
				Evidence.Append("\nowner-failure book-same=").Append(ReferenceEquals(system.LifecycleBook, Book))
					.Append(" can-own=").Append(KingdomLifecycleRules.CanOwnAuthority(Book)).Append(" quarantined=").Append(Book.Quarantined)
					.Append(" wire-rejected=").Append(Book.WireRejected).Append(" fault=").Append(Book.Fault)
					.Append(" loaded=").Append(!system.LoadFailed).Append(" retiring=").Append(system.RealmRetirementBlocksWork)
					.Append(" bound=").Append(KingdomSurvey.HasBoundPass).Append(" probe-armed=").Append(r_TAF_RaidMintProbe.Armed)
					.Append(" latch=").Append(system.MasterOption).Append(" latch-tick=").Append(system.MasterOptionTick)
					.Append(" token=").Append(system.MasterResumeToken).Append(" applied=").Append(system.MasterAppliedResumeToken)
					.Append(" tick=").Append(Game.TimeTicks).Append(" witness-fault=").Append(Witness?.Fault);
				if (growth == null) { Evidence.Append(" growth=null"); return; }
				Evidence.Append("\ngrowth-failure interval=").Append(growth.ArrivalIntervalTicks).Append(" next=").Append(growth.NextArrivalTick)
					.Append(" cadence-pending=").Append(growth.ArrivalCadenceMigrationPending).Append(" cadence-next=").Append(growth.ArrivalCadenceNextDueTick)
					.Append(" processed=").Append(growth.ArrivalProcessedThroughTick).Append(" opportunity-due=").Append(growth.ArrivalOpportunity?.DueTick)
					.Append(" debt-ranges=").Append(growth.ArrivalDebtRanges?.Count).Append(" candidate=").Append(growth.ArrivalCandidate != null)
					.Append(" arrival-op=").Append(growth.ArrivalOp != null).Append(" growth-quarantined=").Append(growth.Quarantined)
					.Append(" growth-fault=").Append(growth.Fault);
			}
			private void AtCheckClock()
			{ Owner(); Require(Game.Turns == CheckTurns && Game.TimeTicks == CheckTick && Game.ActionTicks == CheckActions && Game.PlayerActionTicks == CheckPlayerActions, "explicit turn-in changed clock"); }
			private void OriginalGraves()
			{ Require(ReferenceEquals(Zone.Graveyard, OriginGraveyard) && ReferenceEquals(Zone.Graveyard.Objects, OriginQueue), "origin graveyard changed"); for (int i = 0; i < 3; i++) { Bodies[i].Identity(); Rows.Dead(Actors[i]); } }
			private void RetainWorld()
			{
				OriginalGraves(); Require(Zone.Width == 80 && Zone.Height == 25, "final zone dimensions differ"); FinalRoots = new GameObject[2000][]; int count = 0;
				for (int x = 0; x < 80; x++) for (int y = 0; y < 25; y++)
				{
					Cell cell = Zone.GetCell(x, y); Require(cell != null && ReferenceEquals(cell.ParentZone, Zone) && cell.Objects.Count <= 512, "final cell exceeds bound");
					FinalRoots[x * 25 + y] = cell.Objects.ToArray(); count += cell.Objects.Count; Require(count <= 20000, "final roots exceed bound");
					foreach (GameObject row in FinalRoots[x * 25 + y])
					{
						Require(GameObject.Validate(row) && (row.Physics == null || ReferenceEquals(row.Physics._CurrentCell, cell)), "final live custody differs");
						foreach (GameObject actor in Actors) Require(!ReferenceEquals(actor, row), "original reappeared in live roots");
					}
				}
				var queue = Zone.Graveyard.Objects; Require(queue.Count <= 65536, "final graveyard exceeds bound"); FinalGraves = new GameObject[queue.Count];
				for (int i = 0; i < queue.Count; i++) FinalGraves[i] = queue[i]; AtCheckClock(); OriginalGraves();
			}
			private bool Clear(Cell cell)
			{
				if (cell == null || !ReferenceEquals(cell.ParentZone, Zone) || !cell.IsPassable() || !cell.IsEmpty() || cell.HasOpenLiquidVolume()) return false;
				foreach (GameObject body in cell.Objects) if (!GameObject.Validate(body) || body.IsCreature || KingdomPlots.ReadObject(body) != KingdomPlotRules.GroundKind.Bare) return false;
				return true;
			}
			private byte[] Wire()
			{
				using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
				{ KingdomLifecycleWireCodec.WriteLifecycle(writer, Book); writer.Flush(); return stream.ToArray(); }
			}
			internal void Disarm()
			{
				if (Witness != null) Witness.Armed = false; if (!Armed) return;
				Require(ReferenceEquals(r_TAF_RaidMintProbe.Book, Fixture?.System?.LifecycleBook) && r_TAF_RaidMintProbe.SubstituteAtSequence == 0
					&& r_TAF_RaidMintProbe.SubstituteBlueprint == null, "foreign mint probe disarm refused"); r_TAF_RaidMintProbe.Armed = false;
			}
		}
		private static bool Same(byte[] a, byte[] b)
		{ if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
	}
}
