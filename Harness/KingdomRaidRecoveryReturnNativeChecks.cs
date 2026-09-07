using System;
using System.IO;
using System.Text;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomRaidRecoveryReturnNativeChecks
	{
		private static Frame Retained;
		internal static string Run(XRLGame game, Zone zone, out bool ok)
		{
			ok = false; string failure = null;
			Require(Retained == null, "a recovery-return attempt is already retained"); Retained = new Frame(game, zone);
			try { Retained.Run(); ok = true; }
			catch (Exception error) { failure = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message); }
			finally
			{
				try { Retained.Disarm(); }
				catch (Exception error) { ok = false; failure = "return probe disarm refused: " + error.GetType().Name; }
			}
			return "native-raid-recovery-return cases=1 passed=" + (ok ? "1" : "0") + " failed=" + (ok ? "0" : "1")
				+ "; synthetic=true; ordinary-acceptance=false; save-load=untested; retained=true"
				+ "\nreturned-live-raider-turn-in=" + (ok ? "PASS" : "FAIL " + failure) + Retained.Evidence;
		}
		private static void Require(bool value, string failure) { KingdomRaidRecoveryReturnNativeProvider.Require(value, failure); }
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
			private Zone Foreign;
			private string ForeignId, OperationId, PlanHash;
			private KingdomRaidLaunchNativeFixture Fixture;
			private KingdomLifecycleBook Book;
			private KingdomLifecycleOperation Operation;
			private KingdomRaidContactBody Store, Survivor;
			private Cell SurvivorCell, AwayCell, ReturnCell;
			private KingdomRaidDeathZoneEvidence Rows, ForeignRows;
			private KingdomRaidRecoveryQuestEvidence Quest;
			private byte[] RefusedWire;
			private object[] RefusedQuest;
			private bool Armed;
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
				Owner(); ForeignId = Zone.GetZoneIDFromDirection("E");
				Require(!string.IsNullOrEmpty(ForeignId) && ForeignId != ZoneId, "adjacent zone identity unavailable");
				Foreign = Manager.GetZone(ForeignId); Owner();
				Require(Foreign != null && !ReferenceEquals(Foreign, Zone) && Foreign.ZoneID == ForeignId, "distinct adjacent zone did not load");
				KingdomRaidDeathZoneEvidence.PrepareRetention(Game, Manager, Foreign, Evidence);
				ForeignRows = new KingdomRaidDeathZoneEvidence(Foreign);
				Evidence.Append("\nforeign-loaded-before-founding=").Append(KingdomScenarioRules.Bounded(ForeignId));
				LaunchAndContact(); Recovery(KingdomRaidRecoveryState.Offered);
				var offered = Incident();
				Require(!Game.Quests.ContainsKey(offered.RecoveryQuestId) && !Game.FinishedQuests.ContainsKey(offered.RecoveryQuestId), "quest ID already present");
				long acceptSequence = Book.RaidNextSequence;
				Require(KingdomRaids.TryAcceptRecovery(Fixture.System, out string failure) && failure == null, "actual accept refused: " + failure);
				Quest = new KingdomRaidRecoveryQuestEvidence(Game, Fixture.System, Incident());
				Proof(KingdomLifecycleAction.RaidRecoveryAccept, 1, acceptSequence); byte[] active = Wire();
				Stable(KingdomRaidRecoveryState.Active, active); KillFirst(0, active); KillFirst(1, active);
				Survivor = new KingdomRaidContactBody(Actors[2]); SurvivorCell = Survivor.OriginalCell;
				AwayCell = EmptyInterior(Foreign); ReturnCell = EmptyInterior(Zone);
				Require(AwayCell != null && ReturnCell != null, "no empty retained movement cells; clearing is not authorized");
				Move(AwayCell, KingdomRaidRecoveryState.Active, active);
				Require(Clear(ReturnCell) && !ReferenceEquals(SurvivorCell.ParentZone, Zone), "return cell changed or original did not leave target");
				long readySequence = Book.RaidNextSequence;
				Fixture.Activate(); Record(); Recovery(KingdomRaidRecoveryState.Ready); Quest.Exact(false);
				Proof(KingdomLifecycleAction.RaidRecoveryReady, 1, readySequence); byte[] ready = Wire();
				Evidence.Append("\nactual-absence-wake recovery=Ready survivor-live-in-foreign=true wound=true water=216");
				Move(ReturnCell, KingdomRaidRecoveryState.Ready, ready);
				object[] readyQuest = Quest.Snapshot();
				// No wake or survey refresh between return and the real public turn-in boundary.
				bool resolved = KingdomRaids.TryResolveRecovery(Fixture.System, Zone, out failure);
				Owner(); Survivor.Exact(ReturnCell); Record(); Rows.At(Actors[2], ReturnCell); NoGrave();
				RefusedWire = Wire(); RefusedQuest = Quest.Snapshot();
				Evidence.Append("\nreturned-turn-in accepted=").Append(resolved).Append(" recovery=").Append(Incident()?.RecoveryState)
					.Append(" quest-finished=").Append(Quest.Quest.Finished).Append(" wound=").Append(KingdomRaids.HasWatchDisarray(Fixture.System))
					.Append(" live=true same-original=true target-cell=true graveyard=false water=").Append(Store.Liquid.Volume)
					.Append(" wire-unchanged=").Append(Same(ready, RefusedWire)).Append(" quest-unchanged=").Append(Same(readyQuest, RefusedQuest))
					.Append(" reason=").Append(KingdomScenarioRules.Bounded(failure ?? "(null)"));
				Require(!resolved && !string.IsNullOrEmpty(failure), "turn-in accepted while exact marked original had returned alive");
				Require(Same(readyQuest, RefusedQuest), "refused turn-in changed exact quest fields or roots");
				Stable(KingdomRaidRecoveryState.Ready, ready); Proof(KingdomLifecycleAction.RaidRecoveryResolve, 0);
				Bodies[2] = new KingdomRaidRecoveryDeathSubject(Actors[2]);
				Require(!Actors[2].IsPlayer() && !Actors[2].IsPlayerLed() && !Actors[2].IsDying, "final death subject is not an owned hostile original");
				Actors[2].Die(Force: true); Record(); Bodies[2].Identity(); Rows.Dead(Actors[2]); Dead[2] = true;
				Stable(KingdomRaidRecoveryState.Ready, ready); long resolveSequence = Book.RaidNextSequence;
				Require(KingdomRaids.TryResolveRecovery(Fixture.System, Zone, out failure) && failure == null, "post-removal turn-in refused: " + failure);
				Recovery(KingdomRaidRecoveryState.Resolved); Quest.Exact(true); Record();
				Proof(KingdomLifecycleAction.RaidRecoveryResolve, 1, resolveSequence); Proof(KingdomLifecycleAction.RaidRecoveryReady, 1, readySequence);
				byte[] settled = Wire();
				Require(!KingdomRaids.TryResolveRecovery(Fixture.System, Zone, out failure) && !string.IsNullOrEmpty(failure), "repeat turn-in accepted");
				Fixture.Activate(); Recovery(KingdomRaidRecoveryState.Resolved); Quest.Exact(true); Record();
				Require(Same(settled, Wire()), "repeat turn-in/wake changed resolved authority");
				for (int i = 0; i < 3; i++) { Bodies[i].Identity(); Rows.Dead(Actors[i]); }
				Evidence.Append("\nactual-final-removal origin-graveyard=true turn-in=Resolved wound=false ready-proofs=1 resolve-proofs=1 repeat=unchanged mints=3");
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
				Owner(); actor.Exact(contact); Store.Exact(Store.OriginalCell, 240); Record(); Rows.At(actor.Body, contact);
				Require(Same(before, Wire()) && KingdomSurvey.ActiveFor(Zone) == null, "movement changed authority or left a bound survey");
				KingdomRaids.StepRaider(actor.Body, actor.Objective, Tick); Owner(); actor.Exact(contact); Store.Exact(Store.OriginalCell, 216);
				Require(Book.Raid == null && Operation.Phase == KingdomLifecyclePhase.Terminal && Operation.Fault == null
					&& Operation.EffectState == KingdomLifecyclePhysicalState.Proved && Operation.PlunderProved == 24, "physical contact did not settle once");
				Proof(KingdomLifecycleAction.RaidAttack, 1, Operation.Sequence);
				Bodies[1].Exact(null); Bodies[2].Exact(null); Bodies[0] = new KingdomRaidRecoveryDeathSubject(Actors[0]); Record();
				Evidence.Append("\nactual-contact result=StoresPlundered recovery=Offered water=240->216 plunder=24");
			}
			private void KillFirst(int index, byte[] active)
			{
				Stable(KingdomRaidRecoveryState.Active, active); Record(); Rows.At(Actors[index], Bodies[index].Cell);
				Require(!Actors[index].IsPlayer() && !Actors[index].IsPlayerLed(), "death subject is not an owned hostile original");
				Actors[index].Die(Force: true); Record(); Bodies[index].Identity(); Rows.Dead(Actors[index]); Dead[index] = true;
				Stable(KingdomRaidRecoveryState.Active, active);
				Evidence.Append("\nprior-death=").Append(index).Append(" removed=true origin-graveyard=true recovery=Active wire=unchanged");
			}
			private void Move(Cell destination, KingdomRaidRecoveryState state, byte[] wire)
			{
				Stable(state, wire); Record(); Require(Clear(destination), "retained destination no longer clear");
				Require(Survivor.Body.SystemMoveTo(destination, energyCost: 0, forced: false, ignoreCombat: true, ignoreGravity: false, noStack: true), "survivor move refused");
				Owner(); Survivor.Exact(destination); SurvivorCell = destination; Record();
				(ReferenceEquals(destination.ParentZone, Zone) ? Rows : ForeignRows).At(Actors[2], destination);
				Stable(state, wire); NoGrave();
			}
			private KingdomRaidIncident Incident() { return KingdomRaidIncidentRules.Incident(Book.RaidLedger, Fixture.Incident.Id); }
			private void Recovery(KingdomRaidRecoveryState state)
			{
				Owner(); Physical(); var row = Incident();
				Require(Book.Raid == null && Operation.Id == OperationId && Operation.PlanHash == PlanHash && Operation.Fault == null
					&& Operation.Phase == KingdomLifecyclePhase.Terminal && Operation.EffectState == KingdomLifecyclePhysicalState.Proved
					&& Operation.PlunderProved == 24 && row != null && row.AttackOperationId == OperationId && row.TargetZoneId == ZoneId
					&& row.State == KingdomRaidIncidentState.Resolved && row.Resolution == KingdomRaidResolution.StoresPlundered
					&& row.PlunderProved == 24 && row.RecoveryState == state, "expected recovery " + state + "; observed " + row?.RecoveryState);
				Require(KingdomRaids.HasWatchDisarray(Fixture.System) == (state != KingdomRaidRecoveryState.Resolved), "watch wound differs from exact recovery state");
			}
			private void Stable(KingdomRaidRecoveryState state, byte[] wire)
			{ Recovery(state); Quest.Exact(false); Require(Same(wire, Wire()), "pending recovery wire changed"); }
			private void Physical()
			{
				Store.Exact(Store.OriginalCell, 216); Require(KingdomLiquids.HasFreshWater(Store.Liquid)
					&& r_TAF_RaidMintProbe.Retained == 3 && r_TAF_RaidMintProbe.Snapshot().Length == 3, "water changed or raid reminted");
				for (int i = 0; i < 3; i++) if (!Dead[i])
				{
					if (i == 2 && Survivor != null) Survivor.Exact(SurvivorCell); else Bodies[i].Exact(null);
					Require(Actors[i].IsAlive && !Actors[i].IsDying, "original is not alive outside death dispatch");
				}
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
					&& KingdomScenarioDurableState.ProvesExactText(KingdomRaidRecoveryReturnNativeProvider.Receipt, "intent")
					&& KingdomScenarioDurableState.ProvesExactText(KingdomScenarioProvenanceRules.ProvenanceState, Provenance), "return world/clock/intent changed");
				if (Foreign != null) Require(Foreign.ZoneID == ForeignId && !ReferenceEquals(Foreign, Zone)
					&& Manager.CachedZones.TryGetValue(ForeignId, out cached) && ReferenceEquals(cached, Foreign), "retained foreign zone changed");
				if (Book != null) Require(ReferenceEquals(KingdomRaidLaunchNativeFixture.LastAttempt, Fixture)
					&& ReferenceEquals(Fixture.Game, Game) && ReferenceEquals(Fixture.Zone, Zone)
					&& ReferenceEquals(Game.GetSystem<KingdomSystem>(), Fixture.System) && ReferenceEquals(Fixture.System.LifecycleBook, Book)
					&& ReferenceEquals(r_TAF_RaidMintProbe.Book, Book) && r_TAF_RaidMintProbe.Armed
					&& KingdomMaster.AutomaticWorkAllowed(Fixture.System) && KingdomRaids.Enabled
					&& KingdomLifecycleRules.CanOwnAuthority(Book) && !Book.Quarantined && !Book.WireRejected, "return raid owner changed");
			}
			private void Record() { Rows.Record(Actors); ForeignRows.Record(Actors); }
			private void NoGrave()
			{
				foreach (var grave in new[] { Zone.Graveyard, Foreign.Graveyard, Manager.Graveyard })
				{
					Require(grave?.Objects != null && grave.Objects.Count <= 65536, "graveyard observation exceeds bound");
					foreach (GameObject row in grave.Objects) Require(!ReferenceEquals(row, Actors[2]), "surviving original entered graveyard");
				}
			}
			private Cell EmptyInterior(Zone zone)
			{
				Require(zone.Width == 80 && zone.Height == 25, "movement zone dimensions differ");
				for (int x = 1; x < 79; x++) for (int y = 1; y < 24; y++)
				{ Cell cell = zone.GetCell(x, y); if (Clear(cell)) return cell; }
				return null;
			}
			private bool Clear(Cell cell)
			{
				if (cell == null || (!ReferenceEquals(cell.ParentZone, Zone) && !ReferenceEquals(cell.ParentZone, Foreign))
					|| !cell.IsPassable() || !cell.IsEmpty() || cell.HasOpenLiquidVolume()) return false;
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
				if (!Armed) return;
				Require(ReferenceEquals(r_TAF_RaidMintProbe.Book, Fixture?.System?.LifecycleBook)
					&& r_TAF_RaidMintProbe.SubstituteAtSequence == 0 && r_TAF_RaidMintProbe.SubstituteBlueprint == null, "foreign mint probe disarm refused");
				r_TAF_RaidMintProbe.Armed = false;
			}
		}
		private static bool Same(byte[] a, byte[] b)
		{ if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
		private static bool Same(object[] a, object[] b)
		{ if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (!Equals(a[i], b[i])) return false; return true; }
	}
}
