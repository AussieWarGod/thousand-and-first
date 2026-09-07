using System;
using System.IO;
using System.Text;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomRaidDeathVetoNativeChecks
	{
		private static Frame Retained;
		internal static string Run(XRLGame game, Zone zone, out bool ok)
		{
			ok = false; string failure = null;
			Require(Retained == null, "a native veto attempt is already retained"); Retained = new Frame(game, zone);
			try { Retained.Run(); ok = true; }
			catch (Exception error) { failure = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message); }
			finally
			{
				try { Retained.Disarm(); }
				catch (Exception error) { ok = false; failure = "veto/probe disarm refused: " + error.GetType().Name; }
			}
			return "native-raid-death-veto cases=1 passed=" + (ok ? "1" : "0") + " failed=" + (ok ? "0" : "1")
				+ "; synthetic=true; ordinary-acceptance=false; save-load=untested; retained=true"
				+ "\nfinal-original-destroy-veto=" + (ok ? "PASS" : "FAIL " + failure) + Retained.Evidence;
		}
		internal static void Observe(bool entry, GameObject actor, r_KingdomRaiderObjective part)
		{
			Frame frame = Retained; if (frame == null || !frame.Observing) return;
			try { frame.Observe(entry, actor, part); }
			catch (Exception error) { frame.Latch("dying observer: " + error.GetType().Name); }
		}
		internal static bool BeforeDestroy(KingdomRaidDeathVetoNativePart part, GameObject actor)
		{
			Frame frame = Retained; if (frame == null || !frame.Observing) return false;
			try { return frame.BeforeDestroy(part, actor); }
			catch (Exception error) { frame.Latch("destroy observer: " + error.GetType().Name); return false; }
		}
		private static void Require(bool condition, string failure) { KingdomRaidDeathVetoNativeProvider.Require(condition, failure); }
		private sealed class Frame
		{
			private readonly XRLGame Game;
			private readonly Zone Zone;
			private readonly ZoneManager Manager;
			private readonly GameObject Player;
			private readonly string GameId, ZoneId, Provenance;
			private readonly long Turns, Tick, Actions, PlayerActions;
			private readonly GameObject[] Actors = new GameObject[3];
			private readonly KingdomRaidContactBody[] Bodies = new KingdomRaidContactBody[3];
			private readonly bool[] Dead = new bool[3];
			private readonly bool[] Entry = new bool[2], Exit = new bool[2], Destroy = new bool[2];
			private KingdomRaidLaunchNativeFixture Fixture;
			private KingdomLifecycleBook Book;
			private KingdomLifecycleOperation Operation;
			private KingdomRaidContactBody Store;
			private KingdomRaidDeathZoneEvidence Rows;
			private KingdomRaidDeathVetoSubject Final;
			private KingdomRaidDeathVetoNativePart Veto;
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
				Owner(); Evidence.Append('\n').Append(KingdomRaidLaunchNativeProvider.ProfilesLoaded(out int loaded, out _));
				Require(loaded == KingdomRaidLaunchNativeProvider.ShippedFactions.Length, "shipped raid profile refused");
				Require(r_TAF_RaidMintProbe.Vacant && !r_TAF_RaidMintProbe.Armed && r_TAF_RaidMintProbe.Book == null, "mint probe is not vacant");
				r_TAF_RaidMintProbe.ResetProbe(); r_TAF_RaidMintProbe.Arm(0, null); Armed = true;
				Require(KingdomRaidLaunchNativeFixture.TryCreate(Zone, out Fixture, out string failure), failure);
				Book = Fixture.System.LifecycleBook; Owner(); Require(r_TAF_RaidMintProbe.Snapshot().Length == 0, "premature raid mint");
				Fixture.Activate(); Owner(); Operation = Book.Raid;
				Require(Operation != null && Operation.Action == KingdomLifecycleAction.RaidAttack
					&& Operation.Phase == KingdomLifecyclePhase.EffectIntent && Operation.EffectState == KingdomLifecyclePhysicalState.Prepared
					&& Operation.PlunderProved == 0 && Operation.Fault == null, "launch lacks untouched attack boundary");
				OperationId = Operation.Id; PlanHash = Operation.PlanHash; var observations = r_TAF_RaidMintProbe.Snapshot();
				Require(observations.Length == 3 && r_TAF_RaidMintProbe.Retained == 3 && Operation.Spawned == 3
					&& Operation.PartySize == 3 && Operation.Projections.Count == 3, "launch did not retain three originals");
				for (int i = 0; i < 3; i++)
				{
					KingdomRaidLaunchNativeChecks.VerifyMint(observations[i], Operation, i);
					KingdomRaidLaunchNativeChecks.VerifyBody(Fixture, Operation, Operation.Projections[i], i);
					Actors[i] = observations[i].Original; Bodies[i] = new KingdomRaidContactBody(Actors[i]);
					Require(observations[i].Substitute == null && ReferenceEquals(Zone.FindObjectByID(Operation.Projections[i].ObjectId), Actors[i]),
						"raid projection is not its factory original");
				}
				Store = new KingdomRaidContactBody(Fixture.Store);
				Require(Store.Liquid != null && Store.Body.IDIfAssigned == Operation.Origin && Store.OriginalCell.X == Operation.Target
					&& Store.OriginalCell.Y == Operation.Count, "frozen store differs");
				KingdomRaidDeathZoneEvidence.PrepareRetention(Game, Manager, Zone, Evidence); Owner();
				Rows = new KingdomRaidDeathZoneEvidence(Zone); PendingWire = Wire(); Pending();
				KillFirst(0); KillFirst(1); Pending();
				Final = new KingdomRaidDeathVetoSubject(Actors[2]);
				Require(Actors[2].GetPart<KingdomRaidDeathVetoNativePart>() == null, "final original already has a veto part");
				Veto = new KingdomRaidDeathVetoNativePart();
				Require(ReferenceEquals(Actors[2].AddPart(Veto), Veto), "veto part attachment returned a different part");
				Final.Exact(Veto); Pending(); Veto.Armed = true;
				TryFinal(0); Final.Exact(Veto); NoGrave(); Pending();
				Require(Vetoes == 1 && Veto.Armed, "exactly one original Destroy was not vetoed");
				Evidence.Append("\nvetoed-original alive=true same-cell=true graveyard=false water=240 plunder=0 wire=unchanged");
				Fixture.Activate(); Final.Exact(Veto); NoGrave(); Pending();
				Evidence.Append("\nvetoed-production-wake pending=true wire=unchanged mints=3");
				Veto.Armed = false;
				TryFinal(1); Final.Identity(); Rows.Dead(Actors[2]); Dead[2] = true; Pending();
				Require(Vetoes == 1 && !Veto.Armed, "retry altered the one-shot veto count");
				Evidence.Append("\nretry-removed originating-graveyard=true water=240 plunder=0 wire=unchanged");
				Fixture.Activate(); Owner(); Physical();
				var result = KingdomRaidIncidentRules.Incident(Book.RaidLedger, Fixture.Incident.Id);
				Require(Book.Raid == null && Operation.Phase == KingdomLifecyclePhase.Terminal && Operation.Fault == null
					&& Operation.EffectState == KingdomLifecyclePhysicalState.Skipped && Operation.PlunderProved == 0
					&& result != null && result.State == KingdomRaidIncidentState.Resolved
					&& result.Resolution == KingdomRaidResolution.RaidersDefeated && result.PlunderProved == 0, "retry did not resolve exact RaidersDefeated");
				int proofs = 0;
				foreach (var proof in Book.RecentProofs) if (proof.Id == OperationId)
				{
					Require(proof.PlanHash == PlanHash && proof.Sequence == Operation.Sequence && proof.Lane == Operation.Lane
						&& proof.Action == KingdomLifecycleAction.RaidAttack && proof.Tick == Tick, "attack terminal proof differs"); proofs++;
				}
				Require(proofs == 1, "attack lacks unique exact terminal proof"); byte[] settled = Wire();
				Fixture.Activate(); Owner(); Physical(); Rows.Record(Actors);
				Require(Same(settled, Wire()), "repeat activation changed settled raid authority");
				for (int i = 0; i < 3; i++) Rows.Dead(Actors[i]);
				Evidence.Append("\nretry-wake result=RaidersDefeated water=240 plunder=0 attack-proofs=1 repeat-wire=unchanged mints=3 clock=unchanged");
			}
			private void KillFirst(int index)
			{
				Pending(); Rows.Record(Actors); Rows.At(Actors[index], Bodies[index].OriginalCell);
				Require(!Actors[index].IsPlayer() && !Actors[index].IsPlayerLed(), "first death subject is not an owned hostile original");
				Actors[index].Die(Force: true); Rows.Record(Actors); Rows.Dead(Actors[index]); Dead[index] = true; Pending();
				Evidence.Append("\nprior-death=").Append(index).Append(" removed=true originating-graveyard=true pending=true water=240");
			}
			private void TryFinal(int attempt)
			{
				Pending(); Final.Exact(Veto); NoGrave();
				Require(!Final.Body.IsPlayer() && !Final.Body.IsPlayerLed() && !Final.Body.IsDying, "final original is not an eligible hostile body");
				Attempt = attempt; Observing = true;
				try { Final.Body.Die(Force: true); }
				finally { Observing = false; }
				Rows.Record(Actors);
				Evidence.Append("\npost-Die attempt=").Append(attempt).Append(" valid=").Append(GameObject.Validate(Final.Body))
					.Append(" graveyard=").Append(Final.Body.IsInGraveyard()).Append(" vetoes=").Append(Vetoes)
					.Append(" phase=").Append(Operation.Phase).Append(" effect=").Append(Operation.EffectState)
					.Append(" water=").Append(Store.Liquid.Volume).Append(" fault=").Append(KingdomScenarioRules.Bounded(Operation.Fault ?? "(null)"));
				Require(Fault == null && Entry[attempt] && Exit[attempt] && Destroy[attempt], "veto callback evidence refused: " + Fault);
			}
			internal void Observe(bool entry, GameObject actor, r_KingdomRaiderObjective part)
			{
				Require(Attempt >= 0 && Attempt < 2 && ReferenceEquals(actor, Final.Body) && ReferenceEquals(part, Final.Objective), "foreign dying callback");
				Require(!(entry ? Entry[Attempt] : Exit[Attempt]) && (entry || Entry[Attempt]) && !Destroy[Attempt], "duplicate/unordered dying callback");
				if (entry) Entry[Attempt] = true; else Exit[Attempt] = true;
				Owner(); Final.Exact(Veto);
				Require(actor.IsDying && ReferenceEquals(Book.Raid, Operation) && Operation.Id == OperationId && Operation.PlanHash == PlanHash
					&& Operation.Phase == KingdomLifecyclePhase.EffectIntent && Operation.EffectState == KingdomLifecyclePhysicalState.Prepared,
					"pre-removal callback finalized or lost exact attack authority");
				Evidence.Append("\nRaiderDying attempt=").Append(Attempt).Append(entry ? " entry" : " exit")
					.Append(" dying=true owner=true pending=true id=").Append(KingdomScenarioRules.Bounded(Final.Id)).Append(" base-id=").Append(Final.BaseId);
			}
			internal bool BeforeDestroy(KingdomRaidDeathVetoNativePart part, GameObject actor)
			{
				Require(Attempt >= 0 && Attempt < 2 && ReferenceEquals(part, Veto) && ReferenceEquals(actor, Final.Body)
					&& ReferenceEquals(part.ParentObject, actor) && Entry[Attempt] && Exit[Attempt] && !Destroy[Attempt], "foreign or unordered Destroy callback");
				Destroy[Attempt] = true; Owner(); Final.Exact(Veto);
				Require(actor.IsDying && ReferenceEquals(Book.Raid, Operation) && Operation.Phase == KingdomLifecyclePhase.EffectIntent
					&& Operation.EffectState == KingdomLifecyclePhysicalState.Prepared && Same(PendingWire, Wire()), "Destroy lacks unchanged pending authority");
				bool veto = part.Armed;
				Require(veto == (Attempt == 0) && Vetoes == (Attempt == 0 ? 0 : 1), "veto state or count changed");
				if (veto) Vetoes++;
				Evidence.Append("\nBeforeDestroy attempt=").Append(Attempt).Append(" after-RaiderDying=true veto=").Append(veto)
					.Append(" dying=true same-cell=true owner=true");
				return veto;
			}
			internal void Latch(string reason) { if (Fault == null) Fault = reason; }
			private void Owner()
			{
				Require(ReferenceEquals(The.Game, Game) && Game.GameID == GameId && ReferenceEquals(The.ZoneManager, Manager)
					&& ReferenceEquals(The.Player, Player) && ReferenceEquals(Player?.Physics?._CurrentCell?.ParentZone, Zone)
					&& ReferenceEquals(Manager.ActiveZone, Zone) && Zone.ZoneID == ZoneId
					&& Manager.CachedZones.TryGetValue(ZoneId, out var cached) && ReferenceEquals(cached, Zone)
					&& Game.Turns == Turns && Game.TimeTicks == Tick && Game.ActionTicks == Actions && Game.PlayerActionTicks == PlayerActions
					&& KingdomScenarioDurableState.ProvesExactText(KingdomRaidDeathVetoNativeProvider.Receipt, "intent")
					&& KingdomScenarioDurableState.ProvesExactText(KingdomScenarioProvenanceRules.ProvenanceState, Provenance), "veto world/clock/intent owner changed");
				if (Book != null) Require(ReferenceEquals(KingdomRaidLaunchNativeFixture.LastAttempt, Fixture)
					&& ReferenceEquals(Fixture.Game, Game) && ReferenceEquals(Fixture.Zone, Zone)
					&& ReferenceEquals(Game.GetSystem<KingdomSystem>(), Fixture.System) && ReferenceEquals(Fixture.System.LifecycleBook, Book)
					&& ReferenceEquals(r_TAF_RaidMintProbe.Book, Book) && r_TAF_RaidMintProbe.Armed
					&& KingdomMaster.AutomaticWorkAllowed(Fixture.System) && KingdomRaids.Enabled
					&& KingdomLifecycleRules.CanOwnAuthority(Book) && !Book.Quarantined && !Book.WireRejected, "veto raid authority changed");
			}
			private void Pending()
			{
				Owner(); Physical(); Require(ReferenceEquals(Book.Raid, Operation) && Operation.Id == OperationId && Operation.PlanHash == PlanHash
					&& Operation.Phase == KingdomLifecyclePhase.EffectIntent && Operation.EffectState == KingdomLifecyclePhysicalState.Prepared
					&& Operation.PlunderProved == 0 && Operation.Fault == null && Same(PendingWire, Wire()), "veto attack did not remain exactly pending");
			}
			private void Physical()
			{
				Store.Exact(Store.OriginalCell, 240); Require(KingdomLiquids.HasFreshWater(Store.Liquid)
					&& r_TAF_RaidMintProbe.Retained == 3 && r_TAF_RaidMintProbe.Snapshot().Length == 3, "veto consumed water or reminted actors");
				for (int i = 0; i < 3; i++) if (!Dead[i])
				{
					if (i == 2 && Final != null) Final.Exact(Veto); else Bodies[i].Exact(Bodies[i].OriginalCell);
					Require(Actors[i].IsAlive && !Actors[i].IsDying, "veto survivor is not alive outside death dispatch");
				}
			}
			private void NoGrave()
			{
				Require(Zone.Graveyard.Objects.Count <= 65536 && Manager.Graveyard.Objects.Count <= 65536, "veto graveyard observation exceeds bound");
				foreach (GameObject row in Zone.Graveyard.Objects) Require(!ReferenceEquals(row, Final.Body), "vetoed original entered origin graveyard");
				foreach (GameObject row in Manager.Graveyard.Objects) Require(!ReferenceEquals(row, Final.Body), "vetoed original entered global graveyard");
			}
			private byte[] Wire()
			{
				using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
				{ KingdomLifecycleWireCodec.WriteLifecycle(writer, Book); writer.Flush(); return stream.ToArray(); }
			}
			internal void Disarm()
			{
				Observing = false; Exception failure = null;
				try { if (Veto != null) { Require(ReferenceEquals(Veto.ParentObject, Actors[2]), "veto part moved to foreign owner"); Veto.Armed = false; } }
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
