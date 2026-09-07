using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XRL;
using XRL.World;
using XRL.World.Parts;
namespace ThousandAndFirst.Harness
{
	internal static class KingdomRaidContactNativeChecks
	{
		private static Frame Retained;
		internal static string Run(XRLGame game, Zone zone, out bool ok)
		{
			ok = false; string failure = null;
			Require(Retained == null, "a native contact attempt is already retained");
			Retained = new Frame(game, zone);
			try { Retained.Run(); ok = true; }
			catch (Exception error) { failure = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message); }
			finally
			{
				try { Retained.Disarm(); }
				catch (Exception error) { ok = false; failure = "probe disarm refused: " + error.GetType().Name; }
			}
			return "native-raid-contact cases=1 passed=" + (ok ? "1" : "0") + " failed=" + (ok ? "0" : "1")
				+ "; synthetic=true; ordinary-acceptance=false; save-load=untested; retained=true"
				+ "\nforeign-objective-contact=" + (ok ? "PASS" : "FAIL " + failure) + Retained.Evidence;
		}
		private static void Require(bool condition, string failure) { KingdomRaidContactNativeProvider.Require(condition, failure); }
		private sealed class Frame
		{
			private readonly XRLGame Game;
			private readonly Zone Target;
			private readonly ZoneManager Manager;
			private readonly GameObject Player;
			private readonly string GameId, TargetId, Provenance;
			private readonly long Turns, Tick, Actions, PlayerActions;
			private Zone Foreign;
			private KingdomRaidLaunchNativeFixture Fixture;
			private KingdomLifecycleBook Book;
			private KingdomLifecycleOperation Operation;
			private string OperationId, PlanHash;
			private KingdomRaidContactBody Store, Actor;
			private ZoneRows TargetRows, ForeignRows;
			private Cell StoreCell, ActorCell;
			private byte[] PendingWire;
			private bool Armed;
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
				Owner();
				string foreignId = Target.GetZoneIDFromDirection("E");
				Require(!string.IsNullOrEmpty(foreignId) && foreignId != TargetId, "east adjacent zone identity unavailable");
				Foreign = Manager.GetZone(foreignId);
				Owner(); Require(Foreign != null && !ReferenceEquals(Foreign, Target) && Foreign.ZoneID == foreignId,
					"adjacent real zone did not load distinctly");
				ForeignRows = new ZoneRows(Foreign);
				Evidence.Append("\nforeign-loaded-before-founding=").Append(foreignId);
				string profiles = KingdomRaidLaunchNativeProvider.ProfilesLoaded(out int loaded, out _);
				Evidence.Append('\n').Append(profiles);
				Require(loaded == KingdomRaidLaunchNativeProvider.ShippedFactions.Length, "shipped raid profile refused");
				Require(r_TAF_RaidMintProbe.Vacant && !r_TAF_RaidMintProbe.Armed && r_TAF_RaidMintProbe.Book == null,
					"raid probe ceased to be vacant before arm");
				r_TAF_RaidMintProbe.ResetProbe(); r_TAF_RaidMintProbe.Arm(0, null); Armed = true;
				Require(KingdomRaidLaunchNativeFixture.TryCreate(Target, out Fixture, out string failure), failure);
				Book = Fixture.System.LifecycleBook; Owner();
				Require(r_TAF_RaidMintProbe.Snapshot().Length == 0, "a probed actor was minted before actual activation");
				Fixture.Activate(); Owner();
				Operation = Book.Raid;
				Require(Operation != null && Operation.Action == KingdomLifecycleAction.RaidAttack
					&& Operation.Phase == KingdomLifecyclePhase.EffectIntent && Operation.EffectState == KingdomLifecyclePhysicalState.Prepared
					&& Operation.PlunderProved == 0 && Operation.PlunderRequested > 0 && Operation.Fault == null,
					"actual launch did not leave an untouched actionable contact boundary");
				OperationId = Operation.Id; PlanHash = Operation.PlanHash;
				var observations = r_TAF_RaidMintProbe.Snapshot();
				Require(observations.Length == 3 && r_TAF_RaidMintProbe.Retained == 3
					&& Operation.PartySize == 3 && Operation.Spawned == 3 && Operation.Projections.Count == 3,
					"actual launch did not retain exactly three originals");
				for (int i = 0; i < observations.Length; i++)
				{
					KingdomRaidLaunchNativeChecks.VerifyMint(observations[i], Operation, i);
					KingdomRaidLaunchNativeChecks.VerifyBody(Fixture, Operation, Operation.Projections[i], i);
					Require(observations[i].Substitute == null && ReferenceEquals(observations[i].Original,
						Target.FindObjectByID(Operation.Projections[i].ObjectId)), "launched projection is not its factory original");
				}
				Store = new KingdomRaidContactBody(Fixture.Store); Actor = new KingdomRaidContactBody(observations[0].Original);
				StoreCell = Store.OriginalCell; ActorCell = Actor.OriginalCell;
				Require(Store.Liquid != null && Store.Liquid.Volume == 240 && KingdomLiquids.HasFreshWater(Store.Liquid)
					&& Store.Body.IDIfAssigned == Operation.Origin && StoreCell.X == Operation.Target && StoreCell.Y == Operation.Count
					&& Actor.Objective != null && Actor.Objective.OperationId == Operation.Id && Actor.Objective.IncidentId == Fixture.Incident.Id
					&& Actor.Objective.TargetObjectId == Operation.Origin && Actor.Objective.TargetX == Operation.Target
					&& Actor.Objective.TargetY == Operation.Count, "store or raider objective differs from frozen target");
				byte[] beforeSelectors = Wire();
				KingdomRaidContactWaterChecks.Prepare(Fixture, Clear, Evidence); Owner();
				Require(Same(beforeSelectors, Wire()), "water selector preparation changed frozen raid authority");
				TargetRows = new ZoneRows(Target); PendingWire = Wire();
				Cell awayStore = Foreign.GetCell(StoreCell.X, StoreCell.Y), homeContact = null, awayContact = null;
				Require(Clear(awayStore), "foreign store coordinate " + StoreCell.X + "," + StoreCell.Y
					+ " is occupied; no clearing is authorized");
				foreach (string direction in new[] { "N", "E", "S", "W" })
				{
					Cell candidate = StoreCell.GetLocalCellFromDirection(direction);
					if (candidate == null || !ReferenceEquals(candidate.ParentZone, Target)) continue;
					Cell foreign = Foreign.GetCell(candidate.X, candidate.Y);
					if (Clear(candidate) && Clear(foreign)) { homeContact = candidate; awayContact = foreign; break; }
				}
				Require(homeContact != null, "no matched clear adjacent contact cells; no clearing is authorized");
				Pending(); Move(Actor, awayContact); Move(Store, awayStore); Pending(); Contact(Foreign);
				KingdomRaids.StepRaider(Actor.Body, Actor.Objective, Tick); Pending();
				Evidence.Append("\nforeign-contact water=240 plunder=0 phase=EffectIntent wire=unchanged");
				Move(Store, Store.OriginalCell); Move(Actor, homeContact); Pending(); Contact(Target);
				int debit = Math.Min(Operation.PlunderRequested, 240);
				Require(KingdomSurvey.ActiveFor(Target) == null, "actual contact must own its production survey scope");
				KingdomRaids.StepRaider(Actor.Body, Actor.Objective, Tick);
				ContactDiagnostics();
				Owner(); Physical(240 - debit);
				var result = KingdomRaidIncidentRules.Incident(Book.RaidLedger, Fixture.Incident.Id);
				Require(Book.Raid == null && Operation.Phase == KingdomLifecyclePhase.Terminal && Operation.Fault == null
					&& Operation.EffectState == KingdomLifecyclePhysicalState.Proved && Operation.PlunderProved == debit
					&& result != null && result.State == KingdomRaidIncidentState.Resolved
					&& result.Resolution == KingdomRaidResolution.StoresPlundered && result.PlunderProved == debit,
					"target contact did not produce exactly one completed physical debit and raid result");
				int proofs = 0;
				foreach (var proof in Book.RecentProofs) if (proof.Id == OperationId)
				{
					Require(proof.PlanHash == PlanHash && proof.Sequence == Operation.Sequence && proof.Lane == Operation.Lane
						&& proof.Action == KingdomLifecycleAction.RaidAttack && proof.Tick == Tick, "raid terminal proof differs");
					proofs++;
				}
				Require(proofs == 1 && Operation.Id == OperationId && Operation.PlanHash == PlanHash, "raid has no unique exact terminal proof");
				byte[] settled = Wire();
				KingdomRaids.StepRaider(Actor.Body, Actor.Objective, Tick);
				Owner(); Physical(240 - debit); Require(Same(settled, Wire()), "repeated contact changed settled authority");
				Evidence.Append("\ntarget-contact debit=").Append(debit).Append(" remaining=").Append(240 - debit)
					.Append(" result=StoresPlundered repeat-wire=unchanged; clock=unchanged; mints=3");
			}
			private void ContactDiagnostics()
			{
				Note("LastAttempt", () => ReferenceEquals(KingdomRaidLaunchNativeFixture.LastAttempt, Fixture));
				Note("Game", () => ReferenceEquals(Fixture.Game, Game)); Note("Zone", () => ReferenceEquals(Fixture.Zone, Target));
				Note("System", () => ReferenceEquals(Game.GetSystem<KingdomSystem>(), Fixture.System));
				Note("Book", () => ReferenceEquals(Fixture.System.LifecycleBook, Book));
				Note("ProbeBook", () => ReferenceEquals(r_TAF_RaidMintProbe.Book, Book)); Note("Armed", () => r_TAF_RaidMintProbe.Armed);
				Note("AutomaticWorkAllowed", () => KingdomMaster.AutomaticWorkAllowed(Fixture.System));
				Note("Raids", () => KingdomRaids.Enabled); Note("CanOwnAuthority", () => KingdomLifecycleRules.CanOwnAuthority(Book));
				Note("ValidLedger", () => KingdomRaidIncidentRules.ValidLedger(Book.RaidLedger));
				Note("WireRejected", () => Book.WireRejected); Note("Quarantined", () => Book.Quarantined); Note("BookFault", () => Book.Fault);
				Note("CurrentRaidSame", () => ReferenceEquals(Book.Raid, Operation)); Note("CurrentRaidId", () => Book.Raid?.Id);
				Note("CurrentRaidPhase", () => Book.Raid?.Phase); Note("CurrentRaidEffect", () => Book.Raid?.EffectState);
				Note("CurrentRaidFault", () => Book.Raid?.Fault); Note("CurrentRaidPlunder", () => Book.Raid?.PlunderProved);
				Note("CapturedRaidPhase", () => Operation.Phase); Note("CapturedRaidEffect", () => Operation.EffectState);
				Note("CapturedRaidFault", () => Operation.Fault); Note("CapturedRaidPlunder", () => Operation.PlunderProved);
				Note("Water", () => Store.Liquid.Volume); Note("MasterConfigured", () => KingdomMaster.ConfiguredEnabled);
				Note("MasterLatch", () => Fixture.System.MasterOption); Note("MasterTick", () => Fixture.System.MasterOptionTick);
				Note("MasterResumeToken", () => Fixture.System.MasterResumeToken);
				Note("MasterAppliedResumeToken", () => Fixture.System.MasterAppliedResumeToken);
				Note("LoadFailed", () => Fixture.System.LoadFailed); Note("RetirementBlocksWork", () => Fixture.System.RealmRetirementBlocksWork);
			}
			private void Note(string key, Func<object> read)
			{
				Evidence.Append("\npost-contact.").Append(key).Append('=');
				try { Evidence.Append(KingdomScenarioRules.Bounded(read()?.ToString() ?? "(null)")); }
				catch (Exception error) { Evidence.Append("read-refused:").Append(error.GetType().Name); }
			}
			private void Owner()
			{
				Require(ReferenceEquals(The.Game, Game) && Game.GameID == GameId && ReferenceEquals(The.ZoneManager, Manager)
					&& ReferenceEquals(The.Player, Player) && ReferenceEquals(Player?.Physics?._CurrentCell?.ParentZone, Target)
					&& ReferenceEquals(Manager.ActiveZone, Target) && Target.ZoneID == TargetId
					&& Game.Turns == Turns && Game.TimeTicks == Tick && Game.ActionTicks == Actions && Game.PlayerActionTicks == PlayerActions
					&& KingdomScenarioDurableState.ProvesExactText(KingdomRaidContactNativeProvider.Receipt, "intent")
					&& KingdomScenarioDurableState.ProvesExactText(KingdomScenarioProvenanceRules.ProvenanceState, Provenance),
					"contact game, active zone, clock or owned intent changed");
				Require(Manager.CachedZones.TryGetValue(TargetId, out var target) && ReferenceEquals(target, Target)
					&& (Foreign == null || Manager.CachedZones.TryGetValue(Foreign.ZoneID, out var foreign)
						&& ReferenceEquals(foreign, Foreign)), "retained native zone cache identity changed");
				if (Book != null) Require(ReferenceEquals(KingdomRaidLaunchNativeFixture.LastAttempt, Fixture)
					&& ReferenceEquals(Fixture.Game, Game) && ReferenceEquals(Fixture.Zone, Target)
					&& ReferenceEquals(Game.GetSystem<KingdomSystem>(), Fixture.System) && r_TAF_RaidMintProbe.Armed
					&& ReferenceEquals(Fixture.System.LifecycleBook, Book) && ReferenceEquals(r_TAF_RaidMintProbe.Book, Book)
					&& KingdomMaster.AutomaticWorkAllowed(Fixture.System) && KingdomRaids.Enabled
					&& KingdomLifecycleRules.CanOwnAuthority(Book), "exact raid authority no longer allows contact");
			}
			private void Contact(Zone zone)
			{
				Require(ReferenceEquals(ActorCell.ParentZone, zone) && ReferenceEquals(StoreCell.ParentZone, zone)
					&& StoreCell.X == Operation.Target && StoreCell.Y == Operation.Count
					&& Math.Abs(ActorCell.X - Actor.Objective.TargetX) + Math.Abs(ActorCell.Y - Actor.Objective.TargetY) == 1,
					"marked original is not adjacent to its exact frozen target in the intended zone");
			}
			private void Pending()
			{
				Owner(); Physical(240);
				Require(ReferenceEquals(Book.Raid, Operation) && Operation.Phase == KingdomLifecyclePhase.EffectIntent
					&& Operation.EffectState == KingdomLifecyclePhysicalState.Prepared && Operation.PlunderProved == 0
					&& Operation.Fault == null && Same(PendingWire, Wire()), "displacement changed frozen raid authority");
			}
			private void Physical(int drams)
			{
				Store.Exact(StoreCell, drams); Actor.Exact(ActorCell);
				Require(Actor.Body.IsAlive && KingdomLiquids.HasFreshWater(Store.Liquid) && r_TAF_RaidMintProbe.Retained == 3
					&& r_TAF_RaidMintProbe.Snapshot().Length == 3, "contact lost fresh water or minted another actor");
				TargetRows.Exact(Store.Body, StoreCell, Actor.Body, ActorCell);
				ForeignRows.Exact(Store.Body, StoreCell, Actor.Body, ActorCell);
			}
			private void Move(KingdomRaidContactBody body, Cell destination)
			{
				Pending(); Require(Clear(destination), "contact destination became occupied"); Pending();
				bool moved = body.Body.SystemMoveTo(destination, energyCost: 0, forced: false,
					ignoreCombat: true, ignoreGravity: false, noStack: true);
				if (ReferenceEquals(body, Store)) StoreCell = destination; else ActorCell = destination;
				Require(moved, "native contact movement refused; physical evidence retained without rollback"); Pending();
				Evidence.Append("\nmove=").Append(ReferenceEquals(body, Store) ? "store" : "raider")
					.Append('@').Append(destination.ParentZone.ZoneID).Append(':').Append(destination.X).Append(',').Append(destination.Y)
					.Append(" custody=exact water=240 wire=unchanged");
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
					&& r_TAF_RaidMintProbe.SubstituteAtSequence == 0 && r_TAF_RaidMintProbe.SubstituteBlueprint == null,
					"refusing to disarm a replaced native raid probe");
				r_TAF_RaidMintProbe.Armed = false;
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
		{
			if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true;
		}
		private sealed class ZoneRows
		{
			private readonly Zone Zone;
			private readonly Cell[] Cells = new Cell[2000];
			private readonly Cell.ObjectRack[] Lists = new Cell.ObjectRack[2000];
			private readonly GameObject[][] Rows = new GameObject[2000][];
			private readonly List<KingdomRaidContactBody> Bodies = new List<KingdomRaidContactBody>();
			internal ZoneRows(Zone zone)
			{
				Zone = zone; Require(zone.Width == 80 && zone.Height == 25, "contact zone dimensions differ");
				var seen = new List<GameObject>();
				for (int x = 0; x < 80; x++) for (int y = 0; y < 25; y++)
				{
					int index = x * 25 + y; Cells[index] = zone.GetCell(x, y); Lists[index] = Cells[index].Objects;
					Rows[index] = Lists[index].ToArray(); Require(Rows[index].Length <= 512, "contact cell exceeds bound");
					foreach (GameObject body in Rows[index])
					{
						Require(seen.Count < 20000, "contact zone exceeds body bound");
						foreach (GameObject prior in seen) Require(!ReferenceEquals(prior, body), "contact baseline body duplicated");
						seen.Add(body); Bodies.Add(new KingdomRaidContactBody(body));
					}
				}
			}
			internal void Exact(GameObject store, Cell storeCell, GameObject actor, Cell actorCell)
			{
				for (int x = 0; x < 80; x++) for (int y = 0; y < 25; y++)
				{
					int index = x * 25 + y; Cell cell = Cells[index]; var list = cell.Objects;
					Require(ReferenceEquals(Zone.GetCell(x, y), cell) && ReferenceEquals(Lists[index], list), "contact cell/rack replaced");
					int original = 0, current = 0, stores = 0, actors = 0;
					foreach (GameObject body in Rows[index]) if (!ReferenceEquals(body, store) && !ReferenceEquals(body, actor)) original++;
					foreach (GameObject body in list)
					{
						if (ReferenceEquals(body, store)) { stores++; continue; }
						if (ReferenceEquals(body, actor)) { actors++; continue; }
						Require(body.IDIfAssigned != store.IDIfAssigned && body.IDIfAssigned != actor.IDIfAssigned,
							"another contact body claims the store or raider identity");
						while (current < Rows[index].Length && (ReferenceEquals(Rows[index][current], store)
							|| ReferenceEquals(Rows[index][current], actor))) current++;
						Require(current < Rows[index].Length && ReferenceEquals(Rows[index][current++], body), "unrelated contact cell rows changed");
					}
					Require(stores == (ReferenceEquals(cell, storeCell) ? 1 : 0) && actors == (ReferenceEquals(cell, actorCell) ? 1 : 0)
						&& list.Count == original + stores + actors, "contact body is absent, duplicated or in foreign custody");
				}
				foreach (var body in Bodies) if (!ReferenceEquals(body.Body, store) && !ReferenceEquals(body.Body, actor)) body.Exact(body.OriginalCell);
			}
		}
	}
}
