using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomRaidRecoveryTurnNativeChecks
	{
		private static Frame Retained;
		internal static string Setup(XRLGame game, Zone zone, out bool ok)
		{
			ok = false; Require(Retained == null, "a real-turn attempt is already retained"); Retained = new Frame(game, zone);
			try { Retained.Setup(); ok = true; return "native-raid-recovery-turn setup=Active originals-removed=3 awaiting=advance-1; synthetic=true; retained=true" + Retained.Evidence; }
			finally { if (!ok) Retained.Disarm(); }
		}
		internal static string Check(XRLGame game, Zone zone, out bool ok)
		{
			ok = false; string failure = null; Require(Retained != null, "no retained real-turn setup");
			try { Retained.Check(game, zone); ok = true; }
			catch (Exception error) { failure = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message); }
			finally
			{
				try { Retained.Disarm(); }
				catch (Exception error) { ok = false; failure = "turn cleanup refused: " + error.GetType().Name; }
			}
			return "native-raid-recovery-turn cases=1 passed=" + (ok ? "1" : "0") + " failed=" + (ok ? "0" : "1")
				+ "; synthetic=true; ordinary-acceptance=false; save-load=untested; retained=true\nreal-turn="
				+ (ok ? "PASS" : "FAIL " + failure) + Retained.Evidence;
		}
		internal static void Observe(int stage, KingdomSystem system, long tick, Zone zone)
		{ Retained?.Witness?.Observe(stage, system, tick, zone); }
		private static void Require(bool value, string failure) { KingdomRaidRecoveryTurnNativeProvider.Require(value, failure); }
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
			private object OriginGraveyard, OriginQueue;
			private GameObject[][] AfterTurnRoots;
			private GameObject[] AfterTurnGraves;
			private KingdomRaidRecoveryQuestEvidence Quest;
			private string OperationId, PlanHash;
			private long MasterTick, ResumeToken, AppliedToken, ReadySequence, CheckTick, CheckTurns, CheckActions, CheckPlayerActions;
			private byte[] ActiveWire, CheckedWire, SettledWire;
			private int CheckWater;
			private bool Armed, Awaiting, Checking, ReadySeen;
			internal KingdomRaidRecoveryTurnWitness Witness;
			internal readonly StringBuilder Evidence = new StringBuilder();
			internal Frame(XRLGame game, Zone zone)
			{
				Game = game; Zone = zone; Manager = The.ZoneManager; Player = The.Player;
				GameId = game.GameID; ZoneId = zone.ZoneID; Turns = game.Turns; Tick = game.TimeTicks;
				Actions = game.ActionTicks; PlayerActions = game.PlayerActionTicks;
				Provenance = game.GetStringGameState(KingdomScenarioProvenanceRules.ProvenanceState, null);
			}
			internal void Setup()
			{
				Owner(); LaunchAndContact(); Recovery(KingdomRaidRecoveryState.Offered, 216);
				var row = Incident(); Require(!Game.Quests.ContainsKey(row.RecoveryQuestId) && !Game.FinishedQuests.ContainsKey(row.RecoveryQuestId), "quest ID already present");
				long sequence = Book.RaidNextSequence;
				Require(KingdomRaids.TryAcceptRecovery(Fixture.System, out string failure) && failure == null, "actual acceptance refused: " + failure);
				Quest = new KingdomRaidRecoveryQuestEvidence(Game, Fixture.System, Incident());
				Proof(KingdomLifecycleAction.RaidRecoveryAccept, 1, sequence, Tick); ActiveWire = Wire();
				for (int i = 0; i < 3; i++)
				{
					Recovery(KingdomRaidRecoveryState.Active, 216); Quest.Exact(false); Require(Same(ActiveWire, Wire()), "active pre-death wire changed");
					Rows.Record(Actors); Rows.At(Actors[i], Bodies[i].Cell);
					Require(!Actors[i].IsPlayer() && !Actors[i].IsPlayerLed() && !Actors[i].IsDying, "death subject is not an owned hostile original");
					Actors[i].Die(Force: true); Rows.Record(Actors); Bodies[i].Identity(); Rows.Dead(Actors[i]); Dead[i] = true;
					Recovery(KingdomRaidRecoveryState.Active, 216); Quest.Exact(false); Require(Same(ActiveWire, Wire()), "death finalized recovery before real turn");
				}
				Proof(KingdomLifecycleAction.RaidRecoveryReady, 0); Proof(KingdomLifecycleAction.RaidRecoveryResolve, 0);
				Require(!KingdomScenarioAdvance.Pending, "advance was already active during setup");
				Evidence.Append("\nsetup contact=240->216 plunder=24 recovery=Active original-graves=3 clock=").Append(Turns).Append('/').Append(Tick);
				OriginGraveyard = Zone.Graveyard; OriginQueue = Zone.Graveyard.Objects;
				Witness = new KingdomRaidRecoveryTurnWitness(Game, Fixture.System, Zone, Boundary); Awaiting = true;
			}
			private void Boundary(int stage)
			{
				bool allowBound = ReadySeen && (stage == 1 || stage == 2); Owner(allowBound);
				Require(Awaiting && !Checking && Game.Turns == checked(Turns + Witness.Dispatches - 1)
					&& Game.TimeTicks == checked(Tick + Witness.Dispatches - 1), "event outside contiguous retained advance clocks");
				Recovery(ReadySeen || stage == 2 ? KingdomRaidRecoveryState.Ready : KingdomRaidRecoveryState.Active, Store.Liquid.Volume, allowBound); Quest.Exact(false);
				if (!ReadySeen && stage != 2) Proof(KingdomLifecycleAction.RaidRecoveryReady, 0);
				if (!ReadySeen && stage == 1) ReadySequence = Book.RaidNextSequence;
				if (ReadySeen || stage == 2) { Proof(KingdomLifecycleAction.RaidRecoveryReady, 1, ReadySequence, Witness.ReadyTick); ReadySeen = true; }
				Proof(KingdomLifecycleAction.RaidRecoveryResolve, 0); Require(Boundaries.Count < 576, "boundary wire witness count exceeds bound"); Boundaries.Add(Wire());
				Evidence.Append("\nobserved-dispatch=").Append(Witness.Dispatches).Append(" stage=").Append(stage).Append(" turns=").Append(Game.Turns).Append(" tick=").Append(Game.TimeTicks)
					.Append(" recovery=").Append(Incident().RecoveryState).Append(" water=").Append(Store.Liquid.Volume)
					.Append(" population=").Append(Fixture.System.Population).Append(" last-visit=").Append(Fixture.System.LastVisitTick);
			}
			internal void Check(XRLGame game, Zone zone)
			{
				Require(ReferenceEquals(game, Game) && ReferenceEquals(zone, Zone) && Awaiting && !Checking, "check does not own the one retained setup");
				Checking = true; Owner(); Witness.Verify();
				Require(!KingdomScenarioAdvance.Pending && ReadySeen && Witness.Dispatches >= 1
					&& Game.Turns == checked(Turns + Witness.Dispatches) && Game.Turns == checked(Witness.LastDispatchTurns + 1)
					&& Game.TimeTicks == checked(Tick + Witness.Dispatches) && Game.TimeTicks == checked(Witness.LastDispatchTick + 1)
					&& Game.ActionTicks >= Actions && Game.PlayerActionTicks >= PlayerActions, "actual elapsed clocks do not match every observed EndTurn");
				CheckTick = Game.TimeTicks; CheckTurns = Game.Turns; CheckActions = Game.ActionTicks; CheckPlayerActions = Game.PlayerActionTicks;
				CheckWater = Store.Liquid.Volume; Require(CheckWater >= 0 && CheckWater <= Store.Liquid.MaxVolume, "post-turn store volume invalid");
				Recovery(KingdomRaidRecoveryState.Ready, CheckWater); Quest.Exact(false); Proof(KingdomLifecycleAction.RaidRecoveryReady, 1, ReadySequence, Witness.ReadyTick);
				Proof(KingdomLifecycleAction.RaidRecoveryResolve, 0); CheckedWire = Wire();
				// Ordinary actors/accounting may change over the turn. Retain a fresh observation, never restore the old world.
				RetainWorld();
				Evidence.Append("\nadvance-complete turns=").Append(Turns).Append("->").Append(CheckTurns).Append(" ticks=").Append(Tick).Append("->").Append(CheckTick)
					.Append(" actions=").Append(Actions).Append("->").Append(CheckActions).Append(" player-actions=").Append(PlayerActions).Append("->").Append(CheckPlayerActions)
					.Append(" water=216->").Append(CheckWater).Append(" setup-wire-equal=").Append(Same(ActiveWire, CheckedWire))
					.Append(" observed-dispatches=").Append(Witness.Dispatches).Append(" observed-wakes=").Append(Witness.Wakes).Append(" first-ready-tick=").Append(Witness.ReadyTick);
				long resolveSequence = Book.RaidNextSequence; AtCheckClock();
				Require(KingdomRaids.TryResolveRecovery(Fixture.System, Zone, out string failure) && failure == null, "actual post-turn turn-in refused: " + failure);
				AtCheckClock(); Recovery(KingdomRaidRecoveryState.Resolved, CheckWater); Quest.Exact(true);
				Proof(KingdomLifecycleAction.RaidRecoveryResolve, 1, resolveSequence, CheckTick); SettledWire = Wire();
				Require(!KingdomRaids.TryResolveRecovery(Fixture.System, Zone, out failure) && !string.IsNullOrEmpty(failure), "repeat turn-in accepted");
				AtCheckClock(); Recovery(KingdomRaidRecoveryState.Resolved, CheckWater); Quest.Exact(true);
				Require(Same(SettledWire, Wire()), "repeat changed settled authority");
				Proof(KingdomLifecycleAction.RaidRecoveryReady, 1, ReadySequence, Witness.ReadyTick); Proof(KingdomLifecycleAction.RaidRecoveryResolve, 1, resolveSequence, CheckTick);
				OriginalGraves();
				Evidence.Append("\nheartbeat-ready=1 explicit-resolve=1 wound=false repeat=unchanged mints=3 original-graves=3 world-effects=retained");
			}
			private void LaunchAndContact()
			{
				Owner(); Evidence.Append('\n').Append(KingdomRaidLaunchNativeProvider.ProfilesLoaded(out int loaded, out _));
				Require(loaded == KingdomRaidLaunchNativeProvider.ShippedFactions.Length && r_TAF_RaidMintProbe.Vacant, "profile/probe setup refused");
				r_TAF_RaidMintProbe.ResetProbe(); r_TAF_RaidMintProbe.Arm(0, null); Armed = true;
				Require(KingdomRaidLaunchNativeFixture.TryCreate(Zone, out Fixture, out string failure), failure); Book = Fixture.System.LifecycleBook;
				MasterTick = Fixture.System.MasterOptionTick; ResumeToken = Fixture.System.MasterResumeToken; AppliedToken = Fixture.System.MasterAppliedResumeToken;
				Owner(); Require(r_TAF_RaidMintProbe.Snapshot().Length == 0, "premature mint"); Fixture.Activate(); Owner(); Operation = Book.Raid;
				Require(Operation != null && Operation.Action == KingdomLifecycleAction.RaidAttack && Operation.Phase == KingdomLifecyclePhase.EffectIntent
					&& Operation.EffectState == KingdomLifecyclePhysicalState.Prepared && Operation.PlunderProved == 0
					&& Operation.PlunderRequested == 24 && Operation.Fault == null, "launch lacks untouched contact boundary");
				OperationId = Operation.Id; PlanHash = Operation.PlanHash; var observations = r_TAF_RaidMintProbe.Snapshot();
				Require(observations.Length == 3 && r_TAF_RaidMintProbe.Retained == 3 && Operation.PartySize == 3
					&& Operation.Spawned == 3 && Operation.Projections.Count == 3, "launch did not retain three originals");
				for (int i = 0; i < 3; i++)
				{
					KingdomRaidLaunchNativeChecks.VerifyMint(observations[i], Operation, i);
					KingdomRaidLaunchNativeChecks.VerifyBody(Fixture, Operation, Operation.Projections[i], i); Actors[i] = observations[i].Original;
					Require(observations[i].Substitute == null && ReferenceEquals(Zone.FindObjectByID(Operation.Projections[i].ObjectId), Actors[i]), "foreign mint result");
					Bodies[i] = new KingdomRaidRecoveryDeathSubject(Actors[i]);
				}
				Store = new KingdomRaidContactBody(Fixture.Store); Store.Exact(Store.OriginalCell, 240);
				Require(Store.Body.IDIfAssigned == Operation.Origin && Store.OriginalCell.X == Operation.Target && Store.OriginalCell.Y == Operation.Count, "store target differs");
				KingdomRaidDeathZoneEvidence.PrepareRetention(Game, Manager, Zone, Evidence); Rows = new KingdomRaidDeathZoneEvidence(Zone);
				var actor = new KingdomRaidContactBody(Actors[0]); Cell contact = null;
				foreach (string direction in new[] { "N", "E", "S", "W" })
				{ Cell cell = Store.OriginalCell.GetLocalCellFromDirection(direction); if (Clear(cell)) { contact = cell; break; } }
				Require(contact != null, "no clear contact cell; clearing not authorized"); byte[] before = Wire(); Owner();
				Require(actor.Body.SystemMoveTo(contact, energyCost: 0, forced: false, ignoreCombat: true, ignoreGravity: false, noStack: true), "contact move refused");
				Owner(); actor.Exact(contact); Store.Exact(Store.OriginalCell, 240); Rows.Record(Actors); Rows.At(actor.Body, contact);
				Require(Same(before, Wire()) && !KingdomSurvey.HasBoundPass, "movement changed authority or left bound survey");
				KingdomRaids.StepRaider(actor.Body, actor.Objective, Tick); Owner(); actor.Exact(contact); Store.Exact(Store.OriginalCell, 216);
				Require(Book.Raid == null && Operation.Phase == KingdomLifecyclePhase.Terminal && Operation.Fault == null
					&& Operation.EffectState == KingdomLifecyclePhysicalState.Proved && Operation.PlunderProved == 24, "contact did not settle exact debit");
				Proof(KingdomLifecycleAction.RaidAttack, 1, Operation.Sequence, Tick);
				Bodies[1].Exact(null); Bodies[2].Exact(null); Bodies[0] = new KingdomRaidRecoveryDeathSubject(Actors[0]); Rows.Record(Actors);
			}
			private KingdomRaidIncident Incident() { return KingdomRaidIncidentRules.Incident(Book.RaidLedger, Fixture.Incident.Id); }
			private void Recovery(KingdomRaidRecoveryState state, int water, bool allowBound = false)
			{
				Owner(allowBound); var row = Incident(); Store.Exact(Store.OriginalCell, water);
				Require(KingdomLiquids.CanReceiveFreshWater(Store.Liquid) && r_TAF_RaidMintProbe.Retained == 3 && r_TAF_RaidMintProbe.Snapshot().Length == 3
					&& Book.Raid == null && Operation.Id == OperationId && Operation.PlanHash == PlanHash && Operation.Fault == null
					&& Operation.Phase == KingdomLifecyclePhase.Terminal && Operation.EffectState == KingdomLifecyclePhysicalState.Proved && Operation.PlunderProved == 24
					&& row != null && row.Id == Fixture.Incident.Id && row.SettlementId == Book.SettlementId && row.TargetZoneId == ZoneId
					&& row.AttackOperationId == OperationId && row.State == KingdomRaidIncidentState.Resolved && row.Resolution == KingdomRaidResolution.StoresPlundered
					&& row.PlunderProved == 24 && row.RecoveryState == state, "recovery/water/mint authority differs from " + state);
				Require(KingdomRaids.HasWatchDisarray(Fixture.System) == (state != KingdomRaidRecoveryState.Resolved), "watch wound differs");
				for (int i = 0; i < 3; i++)
				{ if (Dead[i]) { Bodies[i].Identity(); Rows.Dead(Actors[i]); } else Bodies[i].Exact(null); }
			}
			private void Proof(KingdomLifecycleAction action, int expected, long sequence = 0, long tick = 0)
			{
				int count = 0; foreach (var proof in Book.RecentProofs) if (proof.Action == action)
				{
					Require(proof.Lane == KingdomLifecycleLane.Raid && proof.Sequence == sequence && proof.Tick == tick
						&& proof.Id == KingdomLifecycleRules.OperationId(Book.SettlementId, proof.Lane, sequence)
						&& !string.IsNullOrEmpty(proof.PlanHash) && (action != KingdomLifecycleAction.RaidAttack || proof.PlanHash == PlanHash), "terminal proof differs"); count++;
				}
				Require(count == expected, "unexpected " + action + " proof count " + count);
			}
			private void Owner(bool allowBound = false)
			{
				Require(ReferenceEquals(The.Game, Game) && Game.GameID == GameId && ReferenceEquals(The.ZoneManager, Manager)
					&& ReferenceEquals(The.Player, Player) && ReferenceEquals(Player?.Physics?._CurrentCell?.ParentZone, Zone)
					&& ReferenceEquals(Manager.ActiveZone, Zone) && Zone.ZoneID == ZoneId
					&& Manager.CachedZones.TryGetValue(ZoneId, out var cached) && ReferenceEquals(cached, Zone)
					&& KingdomScenarioDurableState.ProvesExactText(KingdomRaidRecoveryTurnNativeProvider.Receipt, "intent")
					&& KingdomScenarioDurableState.ProvesExactText(KingdomScenarioProvenanceRules.ProvenanceState, Provenance), "turn world/intent changed");
				if (!Awaiting) Require(Game.Turns == Turns && Game.TimeTicks == Tick && Game.ActionTicks == Actions && Game.PlayerActionTicks == PlayerActions, "setup advanced the clock");
				if (Book == null) return; var system = Fixture.System;
				Require(ReferenceEquals(KingdomRaidLaunchNativeFixture.LastAttempt, Fixture) && ReferenceEquals(Fixture.Game, Game) && ReferenceEquals(Fixture.Zone, Zone)
					&& ReferenceEquals(Game.GetSystem<KingdomSystem>(), system) && ReferenceEquals(system.LifecycleBook, Book)
					&& system.Founded && !system.LoadFailed && !system.RealmRetirementBlocksWork && system.ClaimedZones.Contains(ZoneId)
					&& ReferenceEquals(r_TAF_RaidMintProbe.Book, Book) && r_TAF_RaidMintProbe.Armed && KingdomRaids.Enabled
					&& KingdomLifecycleRules.CanOwnAuthority(Book) && !Book.Quarantined && !Book.WireRejected
					&& (!KingdomSurvey.HasBoundPass || (allowBound && ReferenceEquals(KingdomSurvey.ActiveFor(Zone)?.Ground, Zone)))
					&& system.MasterOption == KingdomMasterLatchValue.Enabled && system.MasterOptionTick == MasterTick
					&& system.MasterResumeToken == ResumeToken && system.MasterAppliedResumeToken == AppliedToken
					&& KingdomMaster.ConfiguredEnabled && KingdomMaster.AutomaticWorkAllowed(system), "turn raid owner changed");
			}
			private void AtCheckClock()
			{ Owner(); Require(Game.Turns == CheckTurns && Game.TimeTicks == CheckTick && Game.ActionTicks == CheckActions && Game.PlayerActionTicks == CheckPlayerActions, "turn-in advanced the observed clock"); }
			private void OriginalGraves()
			{
				Require(ReferenceEquals(Zone.Graveyard, OriginGraveyard) && ReferenceEquals(Zone.Graveyard.Objects, OriginQueue), "origin graveyard was replaced over the real turn");
				for (int i = 0; i < 3; i++) { Bodies[i].Identity(); Rows.Dead(Actors[i]); }
			}
			private void RetainWorld()
			{
				OriginalGraves(); Require(Zone.Width == 80 && Zone.Height == 25, "post-turn zone dimensions differ");
				AfterTurnRoots = new GameObject[2000][]; int total = 0;
				for (int x = 0; x < 80; x++) for (int y = 0; y < 25; y++)
				{
					Cell cell = Zone.GetCell(x, y); Require(cell != null && ReferenceEquals(cell.ParentZone, Zone) && cell.Objects.Count <= 512, "post-turn cell exceeds bound");
					GameObject[] rows = cell.Objects.ToArray(); AfterTurnRoots[x * 25 + y] = rows; total += rows.Length;
					Require(total <= 20000, "post-turn roots exceed bound");
					foreach (GameObject row in rows)
					{
						Require(GameObject.Validate(row) && (row.Physics == null || ReferenceEquals(row.Physics._CurrentCell, cell)), "post-turn live root custody differs");
						foreach (GameObject actor in Actors) Require(!ReferenceEquals(actor, row), "removed original reappeared in live cell rows");
					}
				}
				var queue = Zone.Graveyard.Objects; Require(queue.Count <= 65536, "post-turn graveyard exceeds bound");
				AfterTurnGraves = new GameObject[queue.Count]; for (int i = 0; i < queue.Count; i++) AfterTurnGraves[i] = queue[i];
				AtCheckClock(); OriginalGraves();
			}
			private bool Clear(Cell cell)
			{
				if (cell == null || !ReferenceEquals(cell.ParentZone, Zone) || !cell.IsPassable() || !cell.IsEmpty() || cell.HasOpenLiquidVolume()) return false;
				foreach (GameObject body in cell.Objects)
					if (!GameObject.Validate(body) || body.IsCreature || KingdomPlots.ReadObject(body) != KingdomPlotRules.GroundKind.Bare) return false;
				return true;
			}
			private byte[] Wire()
			{
				using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
				{ KingdomLifecycleWireCodec.WriteLifecycle(writer, Book); writer.Flush(); return stream.ToArray(); }
			}
			internal void Disarm()
			{
				if (Witness != null) Witness.Armed = false;
				if (!Armed) return;
				Require(ReferenceEquals(r_TAF_RaidMintProbe.Book, Fixture?.System?.LifecycleBook)
					&& r_TAF_RaidMintProbe.SubstituteAtSequence == 0 && r_TAF_RaidMintProbe.SubstituteBlueprint == null, "foreign mint probe disarm refused");
				r_TAF_RaidMintProbe.Armed = false;
			}
		}
		private static bool Same(byte[] a, byte[] b)
		{ if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
	}
}
