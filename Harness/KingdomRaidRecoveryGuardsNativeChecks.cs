using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomRaidRecoveryGuardsNativeChecks
	{
		private static Frame Retained;
		internal static string Run(XRLGame game, Zone zone, out bool ok)
		{
			ok = false; string failure = null;
			Require(Retained == null, "a recovery-guards attempt is already retained"); Retained = new Frame(game, zone);
			try { Retained.Run(); ok = true; }
			catch (Exception error) { failure = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message); }
			finally
			{
				try { Retained.Disarm(); }
				catch (Exception error) { ok = false; failure = "guard cleanup refused: " + error.GetType().Name; }
			}
			return "native-raid-recovery-guards cases=1 passed=" + (ok ? "1" : "0") + " failed=" + (ok ? "0" : "1")
				+ "; synthetic=true; ordinary-acceptance=false; save-load=untested; retained=true"
				+ "\nbound-scan-and-paused-turn-in=" + (ok ? "PASS" : "FAIL " + failure) + Retained.Evidence;
		}
		private static void Require(bool value, string failure) { KingdomRaidRecoveryGuardsNativeProvider.Require(value, failure); }
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
			private readonly List<KingdomRaidRecoveryGuardsScan> Scans = new List<KingdomRaidRecoveryGuardsScan>();
			private Zone Foreign;
			private string ForeignId, OperationId, PlanHash;
			private KingdomRaidLaunchNativeFixture Fixture;
			private KingdomLifecycleBook Book;
			private KingdomLifecycleOperation Operation;
			private KingdomRaidContactBody Store;
			private KingdomRaidDeathZoneEvidence Rows, ForeignRows;
			private KingdomRaidRecoveryQuestEvidence Quest;
			private KingdomRaidLedger ReadyLedger;
			private KingdomRaidIncident ReadyIncident;
			private byte[] ReadyWire;
			private long MasterTick, ResumeToken, AppliedToken;
			private bool Armed, PauseRequested, Paused;
			internal readonly StringBuilder Evidence = new StringBuilder();
			internal Frame(XRLGame game, Zone zone)
			{
				Game = game; Zone = zone; Manager = The.ZoneManager; Player = The.Player;
				GameId = game.GameID; ZoneId = zone.ZoneID; Turns = game.Turns; Tick = game.TimeTicks;
				Actions = game.ActionTicks; PlayerActions = game.PlayerActionTicks;
				Provenance = game.GetStringGameState(KingdomScenarioProvenanceRules.ProvenanceState, null);
			}
			internal void Run()
			{
				Owner(); Unbound(); ForeignId = Zone.GetZoneIDFromDirection("E");
				Require(!string.IsNullOrEmpty(ForeignId) && ForeignId != ZoneId, "adjacent zone identity unavailable");
				Foreign = Manager.GetZone(ForeignId); Owner(); Require(Foreign != null, "adjacent zone did not load");
				KingdomRaidDeathZoneEvidence.PrepareRetention(Game, Manager, Foreign, Evidence); ForeignRows = new KingdomRaidDeathZoneEvidence(Foreign);
				LaunchAndContact(); Recovery(KingdomRaidRecoveryState.Offered);
				var offered = Incident();
				Require(!Game.Quests.ContainsKey(offered.RecoveryQuestId) && !Game.FinishedQuests.ContainsKey(offered.RecoveryQuestId), "quest ID already present");
				long acceptSequence = Book.RaidNextSequence;
				Require(KingdomRaids.TryAcceptRecovery(Fixture.System, out string failure) && failure == null, "actual accept refused: " + failure);
				Quest = new KingdomRaidRecoveryQuestEvidence(Game, Fixture.System, Incident());
				Proof(KingdomLifecycleAction.RaidRecoveryAccept, 1, acceptSequence); byte[] active = Wire();
				for (int i = 0; i < 3; i++)
				{
					Recovery(KingdomRaidRecoveryState.Active); Quest.Exact(false); Require(Same(active, Wire()), "active death wire changed");
					Record(); Rows.At(Actors[i], Bodies[i].Cell);
					Require(!Actors[i].IsPlayer() && !Actors[i].IsPlayerLed(), "death subject is not an owned hostile original");
					Actors[i].Die(Force: true); Record(); Bodies[i].Identity(); Rows.Dead(Actors[i]); Dead[i] = true;
					Recovery(KingdomRaidRecoveryState.Active); Quest.Exact(false); Require(Same(active, Wire()), "pre-wake death finalized recovery");
				}
				long readySequence = Book.RaidNextSequence; Fixture.Activate(); Record(); Recovery(KingdomRaidRecoveryState.Ready); Quest.Exact(false);
				Proof(KingdomLifecycleAction.RaidRecoveryReady, 1, readySequence);
				ReadyLedger = Book.RaidLedger; ReadyIncident = Incident(); ReadyWire = Wire(); Stable();
				BoundCase(Zone, "same-zone"); BoundCase(Foreign, "foreign-zone");
				Stable(); Unbound(); Options.SetOption(KingdomMaster.OptionId, "No"); PauseRequested = true;
				Require(!KingdomMaster.ConfiguredEnabled && Options.GetOption(KingdomMaster.OptionId) == "No", "real master option did not become No");
				Stable(); Unbound();
				bool automatic = true;
				Watch("pause-transition", Zone, () => automatic = KingdomMaster.ObserveAutomaticWake(Fixture.System, Tick), 0, 0);
				Paused = true; Require(!automatic, "disable transition allowed automatic work"); Stable();
				Watch("paused-automatic-wake", Zone, () => automatic = KingdomMaster.ObserveAutomaticWake(Fixture.System, Tick), 0, 0);
				Require(!automatic && !KingdomMaster.AutomaticWorkAllowed(Fixture.System), "paused automatic wake was allowed"); Stable(); Unbound();
				long resolveSequence = Book.RaidNextSequence; bool resolved = false;
				Watch("paused-explicit-turn-in", Zone, () => resolved = KingdomRaids.TryResolveRecovery(Fixture.System, Zone, out failure), 1, 64);
				Require(resolved && failure == null, "paused explicit turn-in refused: " + failure);
				Recovery(KingdomRaidRecoveryState.Resolved); Quest.Exact(true); Record(); Unbound();
				Proof(KingdomLifecycleAction.RaidRecoveryResolve, 1, resolveSequence); Proof(KingdomLifecycleAction.RaidRecoveryReady, 1, readySequence);
				byte[] settled = Wire();
				Require(!KingdomRaids.TryResolveRecovery(Fixture.System, Zone, out failure) && !string.IsNullOrEmpty(failure), "repeat paused turn-in accepted");
				Recovery(KingdomRaidRecoveryState.Resolved); Quest.Exact(true); Record(); Unbound();
				Require(Same(settled, Wire()), "repeat turn-in changed resolved authority");
				Evidence.Append("\nready=earned-by-real-deaths bound-refusals=4 no-bound-residue=true paused=true explicit=Resolved wound=false water=216 plunder=24 mints=3 repeat=unchanged");
			}
			private void BoundCase(Zone boundZone, string label)
			{
				Stable(); Unbound(); KingdomSurvey survey = null;
				Watch(label + "-positive-control", boundZone, () => survey = KingdomSurvey.TakeCustodyOnly(boundZone), 1, 1);
				IList<GameObject> roots = null;
				Require(survey != null && ReferenceEquals(survey.Ground, boundZone) && survey.TryLoaded(out roots)
					&& ReferenceEquals(roots, survey.LoadedObjects) && roots.Count <= 16384 && survey.Objects.Count <= 16384, "real custody survey incomplete");
				GameObject[] before = survey.Objects.ToArray(), loadedBefore = survey.LoadedObjects.ToArray();
				Stable(); KingdomSurvey.PassScope scope = null;
				try
				{
					scope = survey.BindPass();
					Require(KingdomSurvey.HasBoundPass && ReferenceEquals(KingdomSurvey.ActiveFor(boundZone), survey)
						&& ReferenceEquals(KingdomSurvey.ActiveFor(Zone), ReferenceEquals(boundZone, Zone) ? survey : null), "actual bound survey identity differs");
					bool resolved = true; string failure = null; KingdomSurvey fresh = survey;
					Watch(label + "-turn-in", Zone, () => resolved = KingdomRaids.TryResolveRecovery(Fixture.System, Zone, out failure), 0, 0);
					Require(!resolved && !string.IsNullOrEmpty(failure), "bound turn-in did not refuse"); Stable();
					bool captured = true;
					Watch(label + "-unbound-capture", Zone, () => captured = KingdomSurvey.TryTakeUnboundRecovery(Zone, out fresh), 0, 0);
					Require(!captured && fresh == null, "bound fresh-recovery survey did not refuse"); Stable();
					Require(KingdomSurvey.HasBoundPass && ReferenceEquals(KingdomSurvey.ActiveFor(boundZone), survey)
						&& survey.Objects.Count == before.Length && survey.TryLoaded(out var after) && ReferenceEquals(roots, after)
						&& after.Count == loadedBefore.Length, "bound survey/root count changed");
					for (int i = 0; i < before.Length; i++) Require(ReferenceEquals(before[i], survey.Objects[i]), "bound original roots changed");
					for (int i = 0; i < loadedBefore.Length; i++) Require(ReferenceEquals(loadedBefore[i], survey.LoadedObjects[i]), "bound loaded roots changed");
				}
				finally { if (scope != null) scope.Dispose(); Unbound(); }
				Stable(); Evidence.Append('\n').Append(label).Append(" binding=disposed state=Ready quest=unchanged wound=true water=216 clock=unchanged");
			}
			private void Watch(string label, Zone zone, Action action, int minimum, int maximum)
			{
				Require(Scans.Count < 9, "scan witness count exceeds bound"); var scan = KingdomRaidRecoveryGuardsScan.Begin(Game, zone);
				try { Scans.Add(scan); action(); } finally { scan.Dispose(); }
				Evidence.Append('\n').Append(label).Append(' ').Append(scan.Verify(minimum, maximum));
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
				Owner(); actor.Exact(contact); Store.Exact(Store.OriginalCell, 240); Record(); Rows.At(actor.Body, contact);
				Require(Same(before, Wire()) && !KingdomSurvey.HasBoundPass, "movement changed authority or left bound survey");
				KingdomRaids.StepRaider(actor.Body, actor.Objective, Tick); Owner(); actor.Exact(contact); Store.Exact(Store.OriginalCell, 216);
				Require(Book.Raid == null && Operation.Phase == KingdomLifecyclePhase.Terminal && Operation.Fault == null
					&& Operation.EffectState == KingdomLifecyclePhysicalState.Proved && Operation.PlunderProved == 24, "contact did not settle exact debit");
				Proof(KingdomLifecycleAction.RaidAttack, 1, Operation.Sequence);
				Bodies[1].Exact(null); Bodies[2].Exact(null); Bodies[0] = new KingdomRaidRecoveryDeathSubject(Actors[0]); Record();
				Evidence.Append("\nactual-contact result=StoresPlundered recovery=Offered water=240->216 plunder=24");
			}
			private KingdomRaidIncident Incident() { return KingdomRaidIncidentRules.Incident(Book.RaidLedger, Fixture.Incident.Id); }
			private void Stable()
			{
				Recovery(KingdomRaidRecoveryState.Ready); Quest.Exact(false); Record();
				Require(ReferenceEquals(Book.RaidLedger, ReadyLedger) && ReferenceEquals(Incident(), ReadyIncident)
					&& Same(ReadyWire, Wire()), "guard changed exact Ready ledger/row/wire"); Proof(KingdomLifecycleAction.RaidRecoveryResolve, 0);
			}
			private void Recovery(KingdomRaidRecoveryState state)
			{
				Owner(); var row = Incident(); Store.Exact(Store.OriginalCell, 216);
				Require(KingdomLiquids.HasFreshWater(Store.Liquid) && r_TAF_RaidMintProbe.Retained == 3
					&& r_TAF_RaidMintProbe.Snapshot().Length == 3 && Book.Raid == null && Operation.Id == OperationId && Operation.PlanHash == PlanHash
					&& Operation.Fault == null && Operation.Phase == KingdomLifecyclePhase.Terminal && Operation.EffectState == KingdomLifecyclePhysicalState.Proved
					&& Operation.PlunderProved == 24 && row != null && row.AttackOperationId == OperationId && row.TargetZoneId == ZoneId
					&& row.State == KingdomRaidIncidentState.Resolved && row.Resolution == KingdomRaidResolution.StoresPlundered
					&& row.PlunderProved == 24 && row.RecoveryState == state, "recovery/water/mint authority differs from " + state);
				Require(KingdomRaids.HasWatchDisarray(Fixture.System) == (state != KingdomRaidRecoveryState.Resolved), "watch wound differs");
				for (int i = 0; i < 3; i++)
				{ if (Dead[i]) { Bodies[i].Identity(); Rows.Dead(Actors[i]); } else { Bodies[i].Exact(null); Require(!Actors[i].IsDying && Actors[i].IsAlive, "original not alive"); } }
			}
			private void Proof(KingdomLifecycleAction action, int expected, long sequence = 0)
			{
				int count = 0; foreach (var proof in Book.RecentProofs) if (proof.Action == action)
				{
					Require(proof.Lane == KingdomLifecycleLane.Raid && proof.Sequence == sequence && proof.Tick == Tick
						&& proof.Id == KingdomLifecycleRules.OperationId(Book.SettlementId, proof.Lane, sequence)
						&& !string.IsNullOrEmpty(proof.PlanHash) && (action != KingdomLifecycleAction.RaidAttack || proof.PlanHash == PlanHash), "terminal proof differs"); count++;
				}
				Require(count == expected, "unexpected " + action + " proof count " + count);
			}
			private void Owner()
			{
				Require(ReferenceEquals(The.Game, Game) && Game.GameID == GameId && ReferenceEquals(The.ZoneManager, Manager)
					&& ReferenceEquals(The.Player, Player) && ReferenceEquals(Player?.Physics?._CurrentCell?.ParentZone, Zone)
					&& ReferenceEquals(Manager.ActiveZone, Zone) && Zone.ZoneID == ZoneId
					&& Manager.CachedZones.TryGetValue(ZoneId, out var cached) && ReferenceEquals(cached, Zone)
					&& Game.Turns == Turns && Game.TimeTicks == Tick && Game.ActionTicks == Actions && Game.PlayerActionTicks == PlayerActions
					&& KingdomScenarioDurableState.ProvesExactText(KingdomRaidRecoveryGuardsNativeProvider.Receipt, "intent")
					&& KingdomScenarioDurableState.ProvesExactText(KingdomScenarioProvenanceRules.ProvenanceState, Provenance), "guard world/clock/intent changed");
				if (Foreign != null) Require(Foreign.ZoneID == ForeignId && !ReferenceEquals(Foreign, Zone)
					&& Manager.CachedZones.TryGetValue(ForeignId, out cached) && ReferenceEquals(cached, Foreign), "foreign zone owner changed");
				if (Book == null) return; var system = Fixture.System;
				Require(ReferenceEquals(KingdomRaidLaunchNativeFixture.LastAttempt, Fixture) && ReferenceEquals(Fixture.Game, Game)
					&& ReferenceEquals(Fixture.Zone, Zone) && ReferenceEquals(Game.GetSystem<KingdomSystem>(), system)
					&& ReferenceEquals(system.LifecycleBook, Book) && system.Founded && !system.LoadFailed && !system.RealmRetirementBlocksWork
					&& system.ClaimedZones.Contains(ZoneId) && ReferenceEquals(r_TAF_RaidMintProbe.Book, Book) && r_TAF_RaidMintProbe.Armed
					&& KingdomRaids.Enabled && KingdomLifecycleRules.CanOwnAuthority(Book) && !Book.Quarantined && !Book.WireRejected
					&& system.MasterResumeToken == ResumeToken && system.MasterAppliedResumeToken == AppliedToken
					&& system.MasterOption == (Paused ? KingdomMasterLatchValue.Disabled : KingdomMasterLatchValue.Enabled)
					&& system.MasterOptionTick == (Paused ? Tick : MasterTick)
					&& (PauseRequested ? !KingdomMaster.ConfiguredEnabled && !KingdomMaster.AutomaticWorkAllowed(system)
						: KingdomMaster.ConfiguredEnabled && KingdomMaster.AutomaticWorkAllowed(system)), "phase-specific guard owner changed");
			}
			private void Unbound()
			{ Require(!KingdomSurvey.HasBoundPass && KingdomSurvey.ActiveFor(Zone) == null && KingdomSurvey.ActiveFor(Foreign) == null, "bound survey residue remains"); }
			private void Record() { Rows.Record(Actors); ForeignRows.Record(Actors); }
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
				Exception failure = null;
				try { Unbound(); Require(KingdomRaidRecoveryGuardsScan.Vacant, "scan observer remains armed"); } catch (Exception error) { failure = error; }
				try
				{
					if (Armed)
					{
						Require(ReferenceEquals(r_TAF_RaidMintProbe.Book, Fixture?.System?.LifecycleBook)
							&& r_TAF_RaidMintProbe.SubstituteAtSequence == 0 && r_TAF_RaidMintProbe.SubstituteBlueprint == null, "foreign mint probe disarm refused");
						r_TAF_RaidMintProbe.Armed = false;
					}
				}
				catch (Exception error) { if (failure == null) failure = error; }
				if (failure != null) throw failure;
			}
		}
		private static bool Same(byte[] a, byte[] b)
		{ if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
	}
}
