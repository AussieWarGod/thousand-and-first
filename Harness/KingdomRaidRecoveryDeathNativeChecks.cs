using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomRaidRecoveryDeathNativeChecks
	{
		private static Frame Retained;
		internal static string Run(XRLGame game, Zone zone, out bool ok)
		{
			ok = false; string failure = null;
			Require(Retained == null, "a recovery-death attempt is already retained"); Retained = new Frame(game, zone);
			try { Retained.Run(); ok = true; }
			catch (Exception error) { failure = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message); }
			finally
			{
				try { Retained.Disarm(); }
				catch (Exception error) { ok = false; failure = "recovery disarm refused: " + error.GetType().Name; }
			}
			return "native-raid-recovery-death cases=1 passed=" + (ok ? "1" : "0") + " failed=" + (ok ? "0" : "1")
				+ "; synthetic=true; ordinary-acceptance=false; save-load=untested; retained=true"
				+ "\nrecovery-final-destroy-veto=" + (ok ? "PASS" : "FAIL " + failure) + Retained.Evidence;
		}
		internal static void Observe(bool entry, GameObject actor, r_KingdomRaiderObjective part)
		{
			Frame frame = Retained; if (frame == null || !frame.Observing) return;
			try { frame.Observe(entry, actor, part); }
			catch (Exception error) { frame.Latch("dying observer: " + error.GetType().Name); }
		}
		internal static bool BeforeDestroy(KingdomRaidRecoveryDeathNativePart part, GameObject actor)
		{
			Frame frame = Retained; if (frame == null || !frame.Observing) return false;
			try { return frame.BeforeDestroy(part, actor); }
			catch (Exception error) { frame.Latch("destroy observer: " + error.GetType().Name); return false; }
		}
		private static void Require(bool value, string failure) { KingdomRaidRecoveryDeathNativeProvider.Require(value, failure); }
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
			private readonly bool[] Dead = new bool[3], Entry = new bool[2], Exit = new bool[2], Destroy = new bool[2];
			private readonly List<byte[]> BoundaryWires = new List<byte[]>();
			private readonly List<object[]> BoundaryQuests = new List<object[]>();
			private KingdomRaidLaunchNativeFixture Fixture;
			private KingdomLifecycleBook Book;
			private KingdomLifecycleOperation Operation;
			private KingdomRaidContactBody Store;
			private KingdomRaidDeathZoneEvidence Rows;
			private KingdomRaidRecoveryQuestEvidence Quest;
			private KingdomRaidRecoveryDeathNativePart Veto;
			private string OperationId, PlanHash, Fault;
			private byte[] PendingWire;
			private int Attempt = -1, Vetoes;
			private bool Armed;
			internal bool Observing;
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
				LaunchAndContact(); Recovery(KingdomRaidRecoveryState.Offered);
				var offered = Incident();
				Require(!Game.Quests.ContainsKey(offered.RecoveryQuestId) && !Game.FinishedQuests.ContainsKey(offered.RecoveryQuestId), "quest ID already present");
				long acceptSequence = Book.RaidNextSequence;
				Require(KingdomRaids.TryAcceptRecovery(Fixture.System, out string failure) && failure == null, "actual accept refused: " + failure);
				Quest = new KingdomRaidRecoveryQuestEvidence(Game, Fixture.System, Incident());
				Proof(KingdomLifecycleAction.RaidRecoveryAccept, 1, acceptSequence); PendingWire = Wire(); Pending();
				KillFirst(0); KillFirst(1);
				Require(Actors[2].GetPart<KingdomRaidRecoveryDeathNativePart>() == null, "final original already has a recovery veto");
				Veto = new KingdomRaidRecoveryDeathNativePart();
				Require(ReferenceEquals(Actors[2].AddPart(Veto), Veto), "veto attachment returned a different part");
				Bodies[2].Exact(Veto); Pending(); Veto.Armed = true;
				TryFinal(0); Bodies[2].Exact(Veto); NoGrave();
				Evidence.Append("\nvetoed-original live=true same-cell=true graveyard=false vetoes=").Append(Vetoes)
					.Append(" recovery=").Append(Incident()?.RecoveryState).Append(" water=").Append(Store.Liquid.Volume);
				Callbacks(0); Require(Vetoes == 1 && Veto.Armed, "expected one armed veto"); Pending();
				Fixture.Activate(); Rows.Record(Actors); Pending(); NoGrave();
				Evidence.Append("\nvetoed-wake recovery=Active ready-proofs=0 wire=unchanged");
				Veto.Armed = false; TryFinal(1); Bodies[2].Identity(); Rows.Dead(Actors[2]); Dead[2] = true;
				Callbacks(1); Require(Vetoes == 1 && !Veto.Armed, "retry altered veto count"); Pending();
				long readySequence = Book.RaidNextSequence;
				Fixture.Activate(); Rows.Record(Actors); Recovery(KingdomRaidRecoveryState.Ready); Quest.Exact(false);
				Proof(KingdomLifecycleAction.RaidRecoveryReady, 1, readySequence); byte[] ready = Wire();
				Fixture.Activate(); Recovery(KingdomRaidRecoveryState.Ready); Quest.Exact(false);
				Require(Same(ready, Wire()), "repeat wake changed Ready authority"); long resolveSequence = Book.RaidNextSequence;
				Require(KingdomRaids.TryResolveRecovery(Fixture.System, Zone, out failure) && failure == null, "actual seat turn-in refused: " + failure);
				Recovery(KingdomRaidRecoveryState.Resolved); Quest.Exact(true);
				Proof(KingdomLifecycleAction.RaidRecoveryResolve, 1, resolveSequence); Proof(KingdomLifecycleAction.RaidRecoveryReady, 1, readySequence);
				byte[] settled = Wire();
				Require(!KingdomRaids.TryResolveRecovery(Fixture.System, Zone, out failure) && !string.IsNullOrEmpty(failure), "repeat turn-in was accepted");
				Fixture.Activate(); Recovery(KingdomRaidRecoveryState.Resolved); Quest.Exact(true); Rows.Record(Actors);
				Require(Same(settled, Wire()), "repeat turn-in/wake changed resolved authority");
				for (int i = 0; i < 3; i++) { Bodies[i].Identity(); Rows.Dead(Actors[i]); }
				Evidence.Append("\nretry removed=true originating-graveyard=true wake=Ready turn-in=Resolved ready-proofs=1 resolve-proofs=1 repeat=unchanged mints=3");
			}
			private void LaunchAndContact()
			{
				Owner(); Evidence.Append('\n').Append(KingdomRaidLaunchNativeProvider.ProfilesLoaded(out int loaded, out _));
				Require(loaded == KingdomRaidLaunchNativeProvider.ShippedFactions.Length && r_TAF_RaidMintProbe.Vacant, "profile/probe setup refused");
				r_TAF_RaidMintProbe.ResetProbe(); r_TAF_RaidMintProbe.Arm(0, null); Armed = true;
				Require(KingdomRaidLaunchNativeFixture.TryCreate(Zone, out Fixture, out string failure), failure);
				Book = Fixture.System.LifecycleBook; Owner(); Require(r_TAF_RaidMintProbe.Snapshot().Length == 0, "premature mint");
				Fixture.Activate(); Owner(); Operation = Book.Raid;
				Require(Operation != null && Operation.Action == KingdomLifecycleAction.RaidAttack && Operation.Phase == KingdomLifecyclePhase.EffectIntent
					&& Operation.EffectState == KingdomLifecyclePhysicalState.Prepared && Operation.PlunderProved == 0
					&& Operation.PlunderRequested == 24 && Operation.Fault == null, "launch lacks exact untouched contact boundary");
				OperationId = Operation.Id; PlanHash = Operation.PlanHash; var observations = r_TAF_RaidMintProbe.Snapshot();
				Require(observations.Length == 3 && r_TAF_RaidMintProbe.Retained == 3 && Operation.PartySize == 3
					&& Operation.Spawned == 3 && Operation.Projections.Count == 3, "launch did not retain three originals");
				for (int i = 0; i < 3; i++)
				{
					KingdomRaidLaunchNativeChecks.VerifyMint(observations[i], Operation, i);
					KingdomRaidLaunchNativeChecks.VerifyBody(Fixture, Operation, Operation.Projections[i], i);
					Actors[i] = observations[i].Original;
					Require(observations[i].Substitute == null && ReferenceEquals(Zone.FindObjectByID(Operation.Projections[i].ObjectId), Actors[i]), "foreign mint result");
					Bodies[i] = new KingdomRaidRecoveryDeathSubject(Actors[i]);
				}
				Store = new KingdomRaidContactBody(Fixture.Store); Store.Exact(Store.OriginalCell, 240);
				Require(Store.Body.IDIfAssigned == Operation.Origin && Store.OriginalCell.X == Operation.Target && Store.OriginalCell.Y == Operation.Count,
					"store differs from frozen target");
				KingdomRaidDeathZoneEvidence.PrepareRetention(Game, Manager, Zone, Evidence); Rows = new KingdomRaidDeathZoneEvidence(Zone);
				var actor = new KingdomRaidContactBody(Actors[0]); Cell contact = null;
				foreach (string direction in new[] { "N", "E", "S", "W" })
				{ Cell cell = Store.OriginalCell.GetLocalCellFromDirection(direction); if (Clear(cell)) { contact = cell; break; } }
				Require(contact != null, "no clear local contact cell; clearing is not authorized"); byte[] before = Wire(); Owner();
				Require(actor.Body.SystemMoveTo(contact, energyCost: 0, forced: false, ignoreCombat: true, ignoreGravity: false, noStack: true), "contact move refused");
				Owner(); actor.Exact(contact); Store.Exact(Store.OriginalCell, 240); Rows.Record(Actors); Rows.At(actor.Body, contact);
				Require(Same(before, Wire()) && KingdomSurvey.ActiveFor(Zone) == null, "movement changed authority or left a bound survey");
				KingdomRaids.StepRaider(actor.Body, actor.Objective, Tick); Owner(); actor.Exact(contact); Store.Exact(Store.OriginalCell, 216);
				Require(Book.Raid == null && Operation.Phase == KingdomLifecyclePhase.Terminal && Operation.Fault == null
					&& Operation.EffectState == KingdomLifecyclePhysicalState.Proved && Operation.PlunderProved == 24, "physical contact did not settle once");
				Proof(KingdomLifecycleAction.RaidAttack, 1, Operation.Sequence);
				Bodies[1].Exact(null); Bodies[2].Exact(null); Bodies[0] = new KingdomRaidRecoveryDeathSubject(Actors[0]);
				Rows.Record(Actors); Evidence.Append("\nactual-contact result=StoresPlundered recovery=Offered water=240->216 plunder=24");
			}
			private void KillFirst(int index)
			{
				Pending(); Rows.Record(Actors); Rows.At(Actors[index], Bodies[index].Cell);
				Require(!Actors[index].IsPlayer() && !Actors[index].IsPlayerLed(), "death subject is not an owned hostile original");
				Actors[index].Die(Force: true); Rows.Record(Actors); Bodies[index].Identity(); Rows.Dead(Actors[index]); Dead[index] = true; Pending();
				Evidence.Append("\nprior-death=").Append(index).Append(" removed=true originating-graveyard=true recovery=Active wire=unchanged");
			}
			private void TryFinal(int attempt)
			{
				Pending(); Bodies[2].Exact(Veto); NoGrave();
				Require(!Actors[2].IsPlayer() && !Actors[2].IsPlayerLed() && !Actors[2].IsDying, "final original is not an eligible hostile body");
				Attempt = attempt; Observing = true;
				try { Actors[2].Die(Force: true); } finally { Observing = false; }
				Rows.Record(Actors);
			}
			private void Callbacks(int attempt)
			{ Require(Fault == null && Entry[attempt] && Exit[attempt] && Destroy[attempt], "callback evidence refused: " + Fault); }
			internal void Observe(bool entry, GameObject actor, r_KingdomRaiderObjective part)
			{
				Require(Attempt >= 0 && Attempt < 2 && ReferenceEquals(actor, Actors[2]) && ReferenceEquals(part, Bodies[2].Objective), "foreign dying callback");
				Require(!(entry ? Entry[Attempt] : Exit[Attempt]) && (entry || Entry[Attempt]) && !Destroy[Attempt], "duplicate/unordered dying callback");
				if (entry) Entry[Attempt] = true; else Exit[Attempt] = true;
				DeathOwner(); Require(actor.IsDying, "observer was outside actual Die");
				Boundary(entry ? "RaiderDying-entry" : "RaiderDying-exit");
			}
			internal bool BeforeDestroy(KingdomRaidRecoveryDeathNativePart part, GameObject actor)
			{
				Require(Attempt >= 0 && Attempt < 2 && ReferenceEquals(part, Veto) && ReferenceEquals(actor, Actors[2])
					&& ReferenceEquals(part.ParentObject, actor) && Entry[Attempt] && Exit[Attempt] && !Destroy[Attempt], "foreign/unordered Destroy callback");
				Destroy[Attempt] = true; DeathOwner(); Require(actor.IsDying, "Destroy outside actual Die");
				bool veto = part.Armed; Require(veto == (Attempt == 0) && Vetoes == (Attempt == 0 ? 0 : 1), "veto count/arm changed");
				if (veto) Vetoes++;
				// Wrong Ready/wire is evidence, not a reason to let this exact original escape the veto.
				try { Boundary("BeforeDestroy"); } catch (Exception error) { Latch("boundary snapshot: " + error.GetType().Name); }
				Evidence.Append(" veto=").Append(veto); return veto;
			}
			private void DeathOwner()
			{
				Owner(); Bodies[2].Exact(Veto);
				Require(Book.Raid == null && Operation.Id == OperationId && Operation.PlanHash == PlanHash && Operation.Fault == null
					&& Operation.Phase == KingdomLifecyclePhase.Terminal && Operation.PlunderProved == 24 && Incident()?.AttackOperationId == OperationId
					&& Game.Quests.TryGetValue(Quest.Quest.ID, out var current) && ReferenceEquals(current, Quest.Quest), "death callback lost original attack/quest roots");
			}
			private void Boundary(string label)
			{
				Require(BoundaryWires.Count < 6, "callback snapshot bound exceeded"); byte[] wire = Wire(); BoundaryWires.Add(wire);
				BoundaryQuests.Add(Quest.Snapshot());
				Evidence.Append('\n').Append(label).Append(" attempt=").Append(Attempt).Append(" recovery=").Append(Incident()?.RecoveryState)
					.Append(" quest-finished=").Append(Quest.Quest.Finished).Append(" wire-unchanged=").Append(Same(PendingWire, wire))
					.Append(" owner=true dying=true same-cell=true id=").Append(KingdomScenarioRules.Bounded(Bodies[2].Id)).Append(" base-id=").Append(Bodies[2].BaseId);
			}
			internal void Latch(string reason) { if (Fault == null) Fault = reason; }
			private KingdomRaidIncident Incident() { return KingdomRaidIncidentRules.Incident(Book.RaidLedger, Fixture.Incident.Id); }
			private void Recovery(KingdomRaidRecoveryState state)
			{
				Owner(); Physical(); var row = Incident();
				Require(Book.Raid == null && Operation.Id == OperationId && Operation.PlanHash == PlanHash && Operation.Fault == null
					&& Operation.Phase == KingdomLifecyclePhase.Terminal && Operation.EffectState == KingdomLifecyclePhysicalState.Proved
					&& Operation.PlunderProved == 24 && row != null && row.AttackOperationId == OperationId && row.TargetZoneId == ZoneId
					&& row.State == KingdomRaidIncidentState.Resolved && row.Resolution == KingdomRaidResolution.StoresPlundered
					&& row.PlunderProved == 24 && row.RecoveryState == state, "expected recovery " + state + "; observed " + row?.RecoveryState);
			}
			private void Pending()
			{
				Recovery(KingdomRaidRecoveryState.Active); Quest.Exact(false); Proof(KingdomLifecycleAction.RaidRecoveryReady, 0);
				Require(Same(PendingWire, Wire()), "active recovery wire changed before genuine removal/wake");
			}
			private void Physical()
			{
				Store.Exact(Store.OriginalCell, 216); Require(KingdomLiquids.HasFreshWater(Store.Liquid)
					&& r_TAF_RaidMintProbe.Retained == 3 && r_TAF_RaidMintProbe.Snapshot().Length == 3, "water changed or raid reminted");
				for (int i = 0; i < 3; i++) if (!Dead[i])
				{ Bodies[i].Exact(i == 2 ? Veto : null); Require(Actors[i].IsAlive && !Actors[i].IsDying, "survivor is not alive outside dispatch"); }
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
					&& KingdomScenarioDurableState.ProvesExactText(KingdomRaidRecoveryDeathNativeProvider.Receipt, "intent")
					&& KingdomScenarioDurableState.ProvesExactText(KingdomScenarioProvenanceRules.ProvenanceState, Provenance), "recovery world/clock/intent changed");
				if (Book != null) Require(ReferenceEquals(KingdomRaidLaunchNativeFixture.LastAttempt, Fixture)
					&& ReferenceEquals(Fixture.Game, Game) && ReferenceEquals(Fixture.Zone, Zone)
					&& ReferenceEquals(Game.GetSystem<KingdomSystem>(), Fixture.System) && ReferenceEquals(Fixture.System.LifecycleBook, Book)
					&& ReferenceEquals(r_TAF_RaidMintProbe.Book, Book) && r_TAF_RaidMintProbe.Armed
					&& KingdomMaster.AutomaticWorkAllowed(Fixture.System) && KingdomRaids.Enabled
					&& KingdomLifecycleRules.CanOwnAuthority(Book) && !Book.Quarantined && !Book.WireRejected, "recovery raid owner changed");
			}
			private void NoGrave()
			{
				Require(Zone.Graveyard.Objects.Count <= 65536 && Manager.Graveyard.Objects.Count <= 65536, "graveyard observation exceeds bound");
				foreach (GameObject row in Zone.Graveyard.Objects) Require(!ReferenceEquals(row, Actors[2]), "vetoed original entered origin graveyard");
				foreach (GameObject row in Manager.Graveyard.Objects) Require(!ReferenceEquals(row, Actors[2]), "vetoed original entered global graveyard");
			}
			private byte[] Wire()
			{
				using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
				{ KingdomLifecycleWireCodec.WriteLifecycle(writer, Book); writer.Flush(); return stream.ToArray(); }
			}
			private bool Clear(Cell cell)
			{
				if (cell == null || !ReferenceEquals(cell.ParentZone, Zone) || !cell.IsPassable() || !cell.IsEmpty() || cell.HasOpenLiquidVolume()) return false;
				foreach (GameObject body in cell.Objects)
					if (!GameObject.Validate(body) || body.IsCreature || KingdomPlots.ReadObject(body) != KingdomPlotRules.GroundKind.Bare) return false;
				return true;
			}
			internal void Disarm()
			{
				Observing = false; Exception failure = null;
				try { if (Veto != null) { Require(ReferenceEquals(Veto.ParentObject, Actors[2]), "foreign veto owner"); Veto.Armed = false; } }
				catch (Exception error) { failure = error; }
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
