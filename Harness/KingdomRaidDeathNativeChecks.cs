using System;
using System.IO;
using System.Text;
using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomRaidDeathNativeChecks
	{
		private static Frame Retained;
		internal static string Run(XRLGame game, Zone zone, out bool ok)
		{
			ok = false; string failure = null;
			Require(Retained == null, "a native death attempt is already retained"); Retained = new Frame(game, zone);
			try { Retained.Run(); ok = true; }
			catch (Exception error) { failure = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message); }
			finally
			{
				try { Retained.Disarm(); }
				catch (Exception error) { ok = false; failure = "probe disarm refused: " + error.GetType().Name; }
			}
			return "native-raid-death cases=1 passed=" + (ok ? "1" : "0") + " failed=" + (ok ? "0" : "1")
				+ "; synthetic=true; ordinary-acceptance=false; save-load=untested; retained=true"
				+ "\nmarked-raider-death=" + (ok ? "PASS" : "FAIL " + failure) + Retained.Evidence;
		}
		internal static void Observe(bool entry, GameObject actor, r_KingdomRaiderObjective part)
		{
			Frame frame = Retained;
			if (frame == null || !frame.Observing) return;
			try { frame.Observe(entry, actor, part); }
			catch (Exception error) { frame.Latch("observer exception: " + error.GetType().Name); }
		}
		private static void Require(bool value, string failure) { KingdomRaidDeathNativeProvider.Require(value, failure); }
		private sealed class Frame
		{
			private readonly XRLGame Game;
			private readonly Zone Target;
			private readonly ZoneManager Manager;
			private readonly GameObject Player;
			private readonly string GameId, TargetId, Provenance;
			private readonly long Turns, Tick, Actions, PlayerActions;
			private readonly GameObject[] Actors = new GameObject[3];
			private readonly int[] BaseIds = new int[3];
			private readonly string[] ActorIds = new string[3], Blueprints = new string[3];
			private readonly KingdomRaidContactBody[] Bodies = new KingdomRaidContactBody[3];
			private readonly Cell[] Locations = new Cell[3];
			private readonly bool[] Dead = new bool[3];
			private readonly Boundary[] Entries = new Boundary[3], Exits = new Boundary[3];
			private Zone Foreign;
			private KingdomRaidLaunchNativeFixture Fixture;
			private KingdomLifecycleBook Book;
			private KingdomLifecycleOperation Operation;
			private KingdomRaidContactBody Store;
			private KingdomRaidDeathZoneEvidence TargetRows, ForeignRows;
			private string OperationId, PlanHash, ObserverFault;
			private byte[] PendingWire;
			private bool Armed;
			private int Expected = -1;
			internal bool Observing;
			internal readonly StringBuilder Evidence = new StringBuilder();
			internal Frame(XRLGame game, Zone target)
			{
				Game = game; Target = target; Manager = The.ZoneManager; Player = The.Player;
				GameId = game.GameID; TargetId = target.ZoneID; Turns = game.Turns; Tick = game.TimeTicks;
				Actions = game.ActionTicks; PlayerActions = game.PlayerActionTicks;
				Provenance = game.GetStringGameState(KingdomScenarioProvenanceRules.ProvenanceState, null);
			}
			internal void Run()
			{
				Owner(); string foreignId = Target.GetZoneIDFromDirection("E");
				Require(!string.IsNullOrEmpty(foreignId) && foreignId != TargetId, "distinct adjacent zone unavailable");
				Foreign = Manager.GetZone(foreignId); Owner();
				Require(Foreign != null && !ReferenceEquals(Foreign, Target) && Foreign.ZoneID == foreignId, "adjacent real zone differs");
				KingdomRaidDeathZoneEvidence.PrepareRetention(Game, Manager, Foreign, Evidence); Owner();
				ForeignRows = new KingdomRaidDeathZoneEvidence(Foreign);
				Evidence.Append("\nforeign-loaded-before-founding=").Append(foreignId);
				Evidence.Append('\n').Append(KingdomRaidLaunchNativeProvider.ProfilesLoaded(out int loaded, out _));
				Require(loaded == KingdomRaidLaunchNativeProvider.ShippedFactions.Length, "shipped raid profile refused");
				Require(r_TAF_RaidMintProbe.Vacant && !r_TAF_RaidMintProbe.Armed && r_TAF_RaidMintProbe.Book == null, "mint probe is not vacant");
				r_TAF_RaidMintProbe.ResetProbe(); r_TAF_RaidMintProbe.Arm(0, null); Armed = true;
				Require(KingdomRaidLaunchNativeFixture.TryCreate(Target, out Fixture, out string failure), failure);
				Book = Fixture.System.LifecycleBook; Owner();
				Require(r_TAF_RaidMintProbe.Snapshot().Length == 0, "mint occurred before activation");
				Fixture.Activate(); Owner(); Operation = Book.Raid;
				Require(Operation != null && Operation.Action == KingdomLifecycleAction.RaidAttack
					&& Operation.Phase == KingdomLifecyclePhase.EffectIntent && Operation.EffectState == KingdomLifecyclePhysicalState.Prepared
					&& Operation.PlunderProved == 0 && Operation.Fault == null, "launch lacks untouched attack boundary");
				OperationId = Operation.Id; PlanHash = Operation.PlanHash;
				var observations = r_TAF_RaidMintProbe.Snapshot();
				Require(observations.Length == 3 && r_TAF_RaidMintProbe.Retained == 3 && Operation.Spawned == 3
					&& Operation.PartySize == 3 && Operation.Projections.Count == 3, "launch did not retain exactly three originals");
				for (int i = 0; i < 3; i++)
				{
					KingdomRaidLaunchNativeChecks.VerifyMint(observations[i], Operation, i);
					KingdomRaidLaunchNativeChecks.VerifyBody(Fixture, Operation, Operation.Projections[i], i);
					Actors[i] = observations[i].Original; BaseIds[i] = Actors[i]._BaseID;
					ActorIds[i] = Actors[i].IDIfAssigned; Blueprints[i] = Actors[i].Blueprint;
					Bodies[i] = new KingdomRaidContactBody(Actors[i]); Locations[i] = Bodies[i].OriginalCell;
					Require(observations[i].Substitute == null && ReferenceEquals(Target.FindObjectByID(Operation.Projections[i].ObjectId), Actors[i])
						&& Bodies[i].Objective != null && Bodies[i].Objective.OperationId == Operation.Id, "projection is not its marked factory original");
				}
				Store = new KingdomRaidContactBody(Fixture.Store);
				Require(Store.Liquid != null && Store.Body.IDIfAssigned == Operation.Origin
					&& Store.OriginalCell.X == Operation.Target && Store.OriginalCell.Y == Operation.Count, "frozen store differs");
				KingdomRaidDeathZoneEvidence.PrepareRetention(Game, Manager, Target, Evidence); Owner();
				TargetRows = new KingdomRaidDeathZoneEvidence(Target); PendingWire = Wire(); Pending();
				Cell away = null;
				for (int x = 1; x < 79 && away == null; x++) for (int y = 1; y < 24 && away == null; y++)
					if (Clear(Foreign.GetCell(x, y))) away = Foreign.GetCell(x, y);
				Require(away != null, "foreign zone has no clear cell; clearing is not authorized"); Pending();
				bool moved = Actors[0].SystemMoveTo(away, energyCost: 0, forced: false, ignoreCombat: true, ignoreGravity: false, noStack: true);
				Locations[0] = away; Require(moved, "native foreign move refused; no rollback"); Pending();
				Require(Occurrences(Bodies[0].OriginalCell, Actors[0]) == 0, "moved original remains in its prior cell");
				ForeignRows.Record(Actors); ForeignRows.At(Actors[0], away);
				Evidence.Append("\nforeign-move custody=exact water=240 wire=unchanged");
				Kill(0, ForeignRows); Pending();
				Evidence.Append("\nforeign-death pending=true water=240 plunder=0 wire=unchanged quarantine=false");
				Kill(1, TargetRows); Pending();
				Evidence.Append("\nfirst-target-death pending=true water=240 plunder=0 wire=unchanged quarantine=false");
				Kill(2, TargetRows); Pending();
				Evidence.Append("\nlast-target-removed pending=true water=240 plunder=0 wire=unchanged; resolution-awaits-production-wake");
				Fixture.Activate(); Owner(); Physical();
				var result = KingdomRaidIncidentRules.Incident(Book.RaidLedger, Fixture.Incident.Id);
				Require(Book.Raid == null && Operation.Phase == KingdomLifecyclePhase.Terminal && Operation.Fault == null
					&& Operation.EffectState == KingdomLifecyclePhysicalState.Skipped && Operation.PlunderProved == 0
					&& result != null && result.State == KingdomRaidIncidentState.Resolved
					&& result.Resolution == KingdomRaidResolution.RaidersDefeated && result.PlunderProved == 0,
					"post-removal production wake did not resolve RaidersDefeated without quarantine or plunder");
				int proofs = 0;
				foreach (var proof in Book.RecentProofs) if (proof.Id == OperationId)
				{
					Require(proof.PlanHash == PlanHash && proof.Sequence == Operation.Sequence && proof.Lane == Operation.Lane
						&& proof.Action == KingdomLifecycleAction.RaidAttack && proof.Tick == Tick, "attack terminal proof differs"); proofs++;
				}
				Require(proofs == 1, "attack has no unique exact terminal proof"); byte[] settled = Wire();
				Fixture.Activate(); Owner(); Physical(); TargetRows.Record(Actors); ForeignRows.Record(Actors);
				Require(Same(settled, Wire()), "repeat activation changed settled authority");
				for (int i = 0; i < 3; i++) (i == 0 ? ForeignRows : TargetRows).Dead(Actors[i]);
				Evidence.Append("\npost-removal-wake result=RaidersDefeated water=240 plunder=0 attack-proofs=1 repeat-wire=unchanged mints=3 clock=unchanged");
			}
			private void Kill(int index, KingdomRaidDeathZoneEvidence rows)
			{
				Pending(); rows.Record(Actors); GameObject actor = Actors[index]; Cell cell = Locations[index]; rows.At(actor, cell);
				Require(ReferenceEquals(cell.ParentZone, rows.Zone) && !actor.IsPlayer() && !actor.IsPlayerLed()
					&& !actor.IsDying && Entries[index] == null && Exits[index] == null, "death subject is not the untouched original");
				Expected = index; Observing = true;
				try { actor.Die(Force: true); }
				finally { Observing = false; }
				Evidence.Append("\npost-Die=").Append(index).Append(" valid=").Append(GameObject.Validate(actor))
					.Append(" graveyard=").Append(actor.IsInGraveyard()).Append(" has-cell=").Append(actor.Physics?._CurrentCell != null);
				Require(actor._BaseID == BaseIds[index] && actor.IDIfAssigned == ActorIds[index]
					&& actor.Blueprint == Blueprints[index], "death changed original physical identity");
				rows.Record(Actors); rows.Dead(actor); Dead[index] = true;
				Evidence.Append("\ndeath=").Append(index).Append(" engine-removed=true originating-graveyard=true")
					.Append(" phase=").Append(Operation.Phase).Append(" effect=").Append(Operation.EffectState)
					.Append(" quarantine=").Append(Book.Quarantined).Append(" fault=").Append(KingdomScenarioRules.Bounded(Operation.Fault ?? "(null)"));
				Require(ObserverFault == null && Entries[index] != null && Exits[index] != null, "death callback observer refused: " + ObserverFault);
			}
			internal void Observe(bool entry, GameObject actor, r_KingdomRaiderObjective part)
			{
				Require(Expected >= 0 && Expected < 3 && ReferenceEquals(actor, Actors[Expected])
					&& ReferenceEquals(part, Bodies[Expected].Objective) && ReferenceEquals(part.ParentObject, actor), "unexpected dying callback subject");
				Boundary[] records = entry ? Entries : Exits;
				Require(records[Expected] == null && (entry || Entries[Expected] != null), "duplicate or unordered dying callback");
				var record = new Boundary(actor, part, Ownership(), Operation, Book); records[Expected] = record;
				Evidence.Append("\ncallback=").Append(Expected).Append(entry ? ":entry" : ":exit")
					.Append(" owner=").Append(record.Owner).Append(" zone=").Append(KingdomScenarioRules.Bounded(record.Zone?.ZoneID))
					.Append(" base-id=").Append(record.BaseId).Append(" id=").Append(KingdomScenarioRules.Bounded(record.Id))
					.Append(" operation=").Append(KingdomScenarioRules.Bounded(record.OperationId))
					.Append(" published-operation=").Append(KingdomScenarioRules.Bounded(record.PublishedId ?? "(null)"))
					.Append(" dying=").Append(record.Dying).Append(" alive=").Append(record.Alive)
					.Append(" in-cell=").Append(record.InCell).Append(" phase=").Append(record.Phase)
					.Append(" effect=").Append(record.Effect).Append(" quarantine=").Append(record.Quarantined)
					.Append(" fault=").Append(KingdomScenarioRules.Bounded(record.Fault ?? "(null)"));
				Require(record.Owner && record.Dying && record.InCell && record.BaseId == BaseIds[Expected]
					&& record.Id == ActorIds[Expected] && actor.Blueprint == Blueprints[Expected] && ReferenceEquals(record.Operation, Operation)
					&& record.OperationId == OperationId && record.PlanHash == PlanHash && part.OperationId == OperationId
					&& (!entry || ReferenceEquals(record.PublishedOperation, Operation))
					&& ReferenceEquals(actor.GetPart<r_KingdomRaiderObjective>(), part) && ReferenceEquals(record.Cell, Locations[Expected])
					&& ReferenceEquals(record.Zone, Expected == 0 ? Foreign : Target), "dying callback lost original ownership or pre-removal custody");
			}
			internal void Latch(string reason) { if (ObserverFault == null) ObserverFault = reason; }
			private bool Ownership()
			{
				return ReferenceEquals(The.Game, Game) && Game.GameID == GameId && ReferenceEquals(The.ZoneManager, Manager)
					&& ReferenceEquals(The.Player, Player) && ReferenceEquals(Player?.Physics?._CurrentCell?.ParentZone, Target)
					&& ReferenceEquals(Manager.ActiveZone, Target) && Target.ZoneID == TargetId
					&& Game.Turns == Turns && Game.TimeTicks == Tick && Game.ActionTicks == Actions && Game.PlayerActionTicks == PlayerActions
					&& KingdomScenarioDurableState.ProvesExactText(KingdomRaidDeathNativeProvider.Receipt, "intent")
					&& KingdomScenarioDurableState.ProvesExactText(KingdomScenarioProvenanceRules.ProvenanceState, Provenance)
					&& (Book == null || ReferenceEquals(KingdomRaidLaunchNativeFixture.LastAttempt, Fixture)
						&& ReferenceEquals(Fixture.Game, Game) && ReferenceEquals(Fixture.Zone, Target)
						&& ReferenceEquals(Game.GetSystem<KingdomSystem>(), Fixture.System) && ReferenceEquals(Fixture.System.LifecycleBook, Book)
						&& ReferenceEquals(r_TAF_RaidMintProbe.Book, Book) && r_TAF_RaidMintProbe.Armed);
			}
			private void Owner()
			{
				Require(Ownership(), "death game, owner, clock or intent changed");
				Require(Manager.CachedZones.TryGetValue(TargetId, out var target) && ReferenceEquals(target, Target)
					&& (Foreign == null || Manager.CachedZones.TryGetValue(Foreign.ZoneID, out var foreign) && ReferenceEquals(foreign, Foreign)),
					"native zone cache identity changed");
				if (Book != null) Require(KingdomMaster.AutomaticWorkAllowed(Fixture.System) && KingdomRaids.Enabled
					&& KingdomLifecycleRules.CanOwnAuthority(Book) && !Book.Quarantined && !Book.WireRejected,
					"exact raid authority no longer allows death; retained callback facts identify the cut");
			}
			private void Pending()
			{
				Owner(); Physical(); Require(ReferenceEquals(Book.Raid, Operation) && Operation.Id == OperationId && Operation.PlanHash == PlanHash
					&& Operation.Phase == KingdomLifecyclePhase.EffectIntent && Operation.EffectState == KingdomLifecyclePhysicalState.Prepared
					&& Operation.PlunderProved == 0 && Operation.Fault == null && Same(PendingWire, Wire()), "death changed pending attack authority");
			}
			private void Physical()
			{
				Store.Exact(Store.OriginalCell, 240);
				Require(KingdomLiquids.HasFreshWater(Store.Liquid) && r_TAF_RaidMintProbe.Retained == 3
					&& r_TAF_RaidMintProbe.Snapshot().Length == 3, "death consumed water or minted another actor");
				for (int i = 0; i < 3; i++) if (!Dead[i])
				{ Bodies[i].Exact(Locations[i]); Require(Actors[i].IsAlive && !Actors[i].IsDying && Occurrences(Locations[i], Actors[i]) == 1,
					"surviving original is not alive with unique cell membership"); }
			}
			private byte[] Wire()
			{
				using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
				{ KingdomLifecycleWireCodec.WriteLifecycle(writer, Book); writer.Flush(); return stream.ToArray(); }
			}
			internal void Disarm()
			{
				Observing = false; if (!Armed) return;
				Require(ReferenceEquals(r_TAF_RaidMintProbe.Book, Fixture?.System?.LifecycleBook)
					&& r_TAF_RaidMintProbe.SubstituteAtSequence == 0 && r_TAF_RaidMintProbe.SubstituteBlueprint == null, "refusing foreign probe disarm");
				r_TAF_RaidMintProbe.Armed = false;
			}
		}
		private sealed class Boundary
		{
			internal readonly GameObject Body;
			internal readonly r_KingdomRaiderObjective Part;
			internal readonly KingdomLifecycleOperation Operation, PublishedOperation;
			internal readonly string Id, OperationId, PlanHash, PublishedId;
			internal readonly int BaseId;
			internal readonly Cell Cell;
			internal readonly Zone Zone;
			internal readonly bool Owner, Dying, Alive, InCell, Quarantined;
			internal readonly KingdomLifecyclePhase Phase;
			internal readonly KingdomLifecyclePhysicalState Effect;
			internal readonly string Fault;
			internal Boundary(GameObject body, r_KingdomRaiderObjective part, bool owner, KingdomLifecycleOperation op, KingdomLifecycleBook book)
			{
				Body = body; Part = part; Cell = body.Physics?._CurrentCell; Zone = Cell?.ParentZone;
				Id = body.IDIfAssigned; BaseId = body._BaseID; Operation = op; OperationId = op.Id; PlanHash = op.PlanHash;
				PublishedOperation = book.Raid; PublishedId = PublishedOperation?.Id;
				Owner = owner; Dying = body.IsDying; Alive = body.IsAlive; Phase = op.Phase; Effect = op.EffectState;
				Quarantined = book.Quarantined; Fault = op.Fault; InCell = Occurrences(Cell, body) == 1;
			}
		}
		private static bool Clear(Cell cell)
		{
			if (cell == null || !cell.IsPassable() || !cell.IsEmpty() || cell.HasOpenLiquidVolume()) return false;
			foreach (GameObject body in cell.Objects)
				if (!GameObject.Validate(body) || body.IsCreature || KingdomPlots.ReadObject(body) != KingdomPlotRules.GroundKind.Bare) return false;
			return true;
		}
		private static bool Same(byte[] a, byte[] b)
		{ if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
		private static int Occurrences(Cell cell, GameObject body)
		{ int count = 0; if (cell != null) foreach (GameObject row in cell.Objects) if (ReferenceEquals(row, body)) count++; return count; }
	}
}
