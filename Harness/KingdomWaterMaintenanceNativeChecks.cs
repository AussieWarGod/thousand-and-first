using System;
using System.Collections.Generic;
using System.Text;
using XRL;
using XRL.Collections;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;
namespace ThousandAndFirst.Harness
{
	internal static class KingdomWaterMaintenanceNativeChecks
	{
		private static Frame Retained;
		internal static bool Vacant { get { return Retained == null; } }
		internal static string Run(string verb, XRLGame game, Zone zone, out bool complete)
		{
			complete = false;
			if (verb == KingdomWaterMaintenanceNativeProvider.SetupVerb)
			{ Require(Retained == null, "water attempt already retained"); Retained = new Frame(game, zone); Retained.Start(); }
			else { Require(Retained != null, "water setup absent"); Retained.Step(verb); }
			complete = Retained.Done;
			return (complete ? "native-water-maintenance cases=1 passed=1 failed=0" : "native-water-maintenance phase=" + Retained.Phase)
				+ "; synthetic-dedication-and-enrollment=true; ordinary-acceptance=false; save-load=untested; retained=true" + Retained.Evidence;
		}
		internal static string Fail(Exception error)
		{
			if (Retained != null) Retained.Armed = false;
			return "native-water-maintenance cases=1 passed=0 failed=1; evidence retained: "
				+ KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message)
				+ " observer-fault=" + Retained?.Fault + Retained?.Evidence;
		}
		internal static void Observe(int stage, KingdomSystem system = null, KingdomSurvey survey = null, Zone zone = null,
			long tick = -1, int amount = 0, GameObject body = null, KingdomResidentDepartureOperation departure = null, bool healthy = false)
		{
			var frame = Retained; if (frame == null || !frame.Armed) return;
			try { frame.Observe(stage, system, survey, zone, tick, amount, body, departure, healthy); }
			catch (Exception error) { if (frame.Fault == null) frame.Fault = KingdomScenarioRules.Bounded(error.GetType().Name + ": " + error.Message); }
		}
		private static void Require(bool value, string failure) { KingdomWaterMaintenanceNativeProvider.Require(value, failure); }
		private sealed class Frame
		{
			private readonly XRLGame Game;
			private readonly Zone Zone;
			private readonly ZoneManager Manager;
			private readonly GameObject Player;
			private readonly string GameId, ZoneId, Provenance;
			private readonly KingdomWaterMaintenanceSetup Setup;
			private KingdomSystem System;
			private KingdomCityBook City;
			private KingdomBindingRegistry Bindings;
			private KingdomLifecycleBook Lifecycle;
			private KingdomLedger Ledger;
			private Graveyard Origin;
			private RingDeque<GameObject> Graves;
			private string RealmId, SettlementId;
			private long LastTick, LastTurns, EnrollTick, EnrollTurns, DispatchTick, Checkpoint;
			private long MasterToken, MasterApplied;
			private int EndTurns, Heartbeats, DryBills, HealthyBills, HealthyDays, PaidTotal, StoreWater = 1, DonorWater = 16;
			private int PopulationBefore, DryBefore, LedgerBefore, DeparturesBefore, Days, Need, Paid, ConsumeStage;
			private KingdomSurvey ActiveSurvey;
			private KingdomResidentDepartureOperation Departure;
			private r_KingdomResidentDeparture DepartureMarker;
			private GameObject DepartedBody;
			private int Departures;
			private bool InTurn, InHeartbeat, DepartureStoryReported;
			private Dictionary<string, int> Brine;
			internal bool Armed, Done;
			internal int Phase;
			internal string Fault;
			internal readonly StringBuilder Evidence = new StringBuilder();
			internal Frame(XRLGame game, Zone zone)
			{
				Game = game; Zone = zone; Manager = The.ZoneManager; Player = The.Player; GameId = game.GameID; ZoneId = zone.ZoneID;
				Provenance = game.GetStringGameState(KingdomScenarioProvenanceRules.ProvenanceState, null);
				Setup = new KingdomWaterMaintenanceSetup(game, zone); SetClock();
			}
			internal void Start()
			{
				Owner(); Options.SetOption("r_TAF_OptionGrowth", "No"); Owner();
				Require(!KingdomGrowth.Enabled, "actual growth option write was not retained");
				Options.SetOption("r_TAF_OptionRaids", "No"); Owner();
				Require(!KingdomRaids.Enabled && !KingdomGrowth.Enabled && KingdomMaster.ConfiguredEnabled
					&& KingdomGrowth.ScarcityEnabled && !(Game.GetSystem<KingdomSystem>()?.Founded ?? false), "option callbacks changed fresh founding authority");
				System = Setup.Found(); Require(System != null && System.Founded, "real founding failed");
				City = System.City; Bindings = System.Bindings; Lifecycle = System.LifecycleBook; Ledger = System.Ledger;
				RealmId = System.CurrentRealmId; SettlementId = City?.SettlementId; Owner();
				Require(Game.TimeTicks == LastTick && Game.Turns == LastTurns && System.Population == 0 && City.ResidentCount == 0
					&& Bindings.Count == 0 && Ledger.UpkeepDrawn == 0 && Ledger.Departures == 0, "fresh founding is not an empty unpaid roll");
				Evidence.Append("\nactual-founding tick=").Append(LastTick).Append("; warmup=advance2400; no deadlines assigned");
			}
			internal void Step(string verb)
			{
				Owner(); Require(Fault == null && !Done && !KingdomScenarioAdvance.Pending && !InTurn && !InHeartbeat, "water phase/observer refused: " + Fault);
				if (verb == KingdomWaterMaintenanceNativeProvider.EnrollVerb)
				{
					Require(Phase == 0, "enrollment is not repeatable"); Span(2400, false);
					Require(System.Population == 0 && City.ResidentCount == 0 && Bindings.Count == 0 && System.DryStreak == 0
						&& Ledger.UpkeepDrawn == 0 && Ledger.Departures == 0 && System.LastHeartbeatTick > LastTick
						&& System.LastHeartbeatTick <= Game.TimeTicks, "actual warmup did not establish empty healthy heartbeat");
					KingdomWaterMaintenanceSealEvidence.Verify(System, Game, LastTick, () => Owner(), Evidence);
					KingdomRaidDeathZoneEvidence.PrepareRetention(Game, Manager, Zone, Evidence);
					Origin = Zone.Graveyard; Graves = Origin.Objects;
					long turns = Game.Turns, actions = Game.ActionTicks, playerActions = Game.PlayerActionTicks;
					Setup.Enroll(System, () => Owner(), () => Owner(true)); Owner();
					Require(Game.Turns == turns && Game.ActionTicks == actions && Game.PlayerActionTicks == playerActions, "enrollment advanced action clocks");
					Brine = new Dictionary<string, int>(Setup.Liquids[2].ComponentLiquids);
					Require(Setup.Owned.Count == 7 && !Setup.Liquids[2].IsFreshWater(), "brine/factory control refused");
					MasterToken = System.MasterResumeToken; MasterApplied = System.MasterAppliedResumeToken;
					EnrollTick = Game.TimeTicks; EnrollTurns = Game.Turns; Roster(3); Controls();
					Phase = 1; Armed = true; SetClock(); Evidence.Append("\nenrolled=3; dedicated=1; personal=12; brine=10; donor=16; dedication=actual-setup-check-in-before-baseline"); return;
				}
				Span(1200, true); Controls(); Roster(Departures == 0 ? 3 : 2);
				if (verb == KingdomWaterMaintenanceNativeProvider.RefillVerb)
				{
					Require(Phase == 3 && DryBills >= 3 && Departures == 1 && StoreWater == 0 && System.DryStreak >= 3, "third dry interval/loyal core not witnessed");
					long tick = Game.TimeTicks, turns = Game.Turns, actions = Game.ActionTicks, playerActions = Game.PlayerActionTicks; int bills = Heartbeats;
					long checkpoint = System.LastHeartbeatTick; int dry = System.DryStreak, upkeep = Ledger.UpkeepDrawn;
					Require(Setup.Liquids[0].MixWith(Setup.Liquids[3], PouredFrom: Setup.Vessels[3], Amount: 16), "actual donor MixWith refused");
					Owner(); Require(Fault == null && Heartbeats == bills && Game.TimeTicks == tick && Game.Turns == turns
						&& Game.ActionTicks == actions && Game.PlayerActionTicks == playerActions
						&& System.LastHeartbeatTick == checkpoint && System.DryStreak == dry && Ledger.UpkeepDrawn == upkeep && Ledger.Departures == 1
						&& Setup.Liquids[0].Volume == 16 && Setup.Liquids[3].Volume == 0, "refill callback or exact transfer changed authority");
					StoreWater = 16; DonorWater = 0; Controls(); Roster(2); Owner(); Phase = 4;
					Evidence.Append("\nactual-MixWith donor16->0 dedicated0->16; controls retained");
				}
				else
				{
					Require(verb == KingdomWaterMaintenanceNativeProvider.CheckVerb && (Phase == 1 || Phase == 2 || Phase == 4), "unexpected check phase");
					if (Phase < 3) { Require(DryBills >= Phase && HealthyBills == 0, "observed dry billing interval absent"); Phase++; }
					else
					{
						Require(HealthyBills >= 1 && Departures == 1 && System.DryStreak == 0 && !System.Withered
							&& PaidTotal == 1 + HealthyDays * 2 && StoreWater == 16 - HealthyDays * 2, "paid refill did not recover exactly once per bill");
						Owner(); Armed = false; Done = true; Phase = 5;
						Evidence.Append("\nPASS actual-upkeep partial=1 dry-bills=").Append(DryBills).Append(" recovery-bills=").Append(HealthyBills)
							.Append(" paid=").Append(PaidTotal).Append(" departures=1 population=2 turns=").Append(EndTurns);
					}
				}
				SetClock();
			}
			internal void Observe(int stage, KingdomSystem system, KingdomSurvey survey, Zone zone, long tick, int amount, GameObject body, KingdomResidentDepartureOperation departure, bool healthy)
			{
				if (Fault != null) return;
				Owner(stage != 0 && stage != 6);
				if (stage == 0)
				{
					Require(ReferenceEquals(system, System) && !InTurn && EndTurns < 5000
						&& Game.Turns == EnrollTurns + EndTurns && Game.TimeTicks == EnrollTick + EndTurns, "noncontiguous real EndTurn owner/clock");
					InTurn = true; DispatchTick = Game.TimeTicks; return;
				}
				Require(InTurn && Game.TimeTicks == DispatchTick, "maintenance escaped real EndTurn dispatch");
				if (stage == 6) { Require(ReferenceEquals(system, System) && !InHeartbeat, "EndTurn ended with open heartbeat"); InTurn = false; EndTurns++; return; }
				if (stage == 1)
				{
					Require(!InHeartbeat && ReferenceEquals(system, System) && ReferenceEquals(zone, Zone) && tick == DispatchTick
						&& ReferenceEquals(survey, KingdomSurvey.ActiveFor(Zone)) && ReferenceEquals(survey?.Ground, Zone)
						&& KingdomMaster.AutomaticWorkAllowed(System) && ++Heartbeats <= 16, "actual heartbeat binding/entry refused");
					Controls(stage); Roster(Departures == 0 ? 3 : 2); InHeartbeat = true; ActiveSurvey = survey;
					Checkpoint = System.LastHeartbeatTick; Require(Checkpoint > 0 && Checkpoint <= tick, "raw heartbeat checkpoint invalid");
					Days = checked((int)((tick - Checkpoint) / 1200)); Require(Days <= 2, "unexpected multi-day heartbeat backlog");
					PopulationBefore = System.Population; DryBefore = System.DryStreak; LedgerBefore = Ledger.UpkeepDrawn; DeparturesBefore = Departures;
					Need = PopulationBefore * Days; Paid = 0; ConsumeStage = 0;
					Require(survey.StoredWater == StoreWater && survey.Settlers.Count == PopulationBefore && survey.Citizens == PopulationBefore
						&& survey.Stores.Count == 2 && survey.Stores.Contains(Setup.Liquids[0]) && survey.Stores.Contains(Setup.Liquids[2]),
						"real active survey did not isolate enrolled people/dedicated fresh water"); return;
				}
				Require(InHeartbeat, "upkeep/departure lacked enclosing heartbeat");
				if (stage == 2)
				{ Require(ReferenceEquals(survey, ActiveSurvey) && ConsumeStage == 0 && Days > 0 && amount == Need, "real upkeep request differs from Camp bill"); ConsumeStage = 1; return; }
				if (stage == 3)
				{
					Require(ReferenceEquals(survey, ActiveSurvey) && ConsumeStage == 1 && amount == Math.Min(StoreWater, Need)
						&& Setup.Liquids[0].Volume == StoreWater - amount, "stocked water silently refused or physical payment differs");
					Paid = amount; StoreWater -= amount; PaidTotal += amount; ConsumeStage = 2; return;
				}
				if (stage == 5) { CaptureDeparture(system, body, departure); return; }
				Require(stage == 4 && ReferenceEquals(system, System) && ReferenceEquals(survey, ActiveSurvey) && ReferenceEquals(zone, Zone)
					&& tick == DispatchTick && ConsumeStage == (Days == 0 ? 0 : 2), "heartbeat exit lost exact debit witness");
				if (Days > 0) { if (Paid < Need) DryBills++; else { HealthyBills++; HealthyDays += Days; } }
				int dry = Days == 0 ? DryBefore : Paid < Need ? DryBefore + 1 : 0;
				int departed = Days > 0 && Paid < Need && dry >= 2 && PopulationBefore == 3 ? 1 : 0;
				Require(System.LastHeartbeatTick == Checkpoint + Days * 1200L && System.DryStreak == dry && !System.Withered
					&& Ledger.UpkeepDrawn == LedgerBefore + Paid && Ledger.UpkeepDrawn == PaidTotal
					&& Departures == DeparturesBefore + departed && Ledger.Departures == Departures && Departures <= 1
					&& healthy == (Days == 0 || Paid >= Need),
					"heartbeat checkpoint/scarcity/accounting or departure count diverged");
				Require((Phase < 4 && HealthyBills == 0) || (Phase == 4 && Paid == Need), "unexpected scarcity phase health");
				Controls(stage); Roster(PopulationBefore - departed); Owner(true);
				Evidence.Append("\nheartbeat tick=").Append(tick).Append(" elapsed-days=").Append(Days).Append(" requested=").Append(Need)
					.Append(" paid=").Append(Paid).Append(" dry=").Append(dry).Append(" population=").Append(System.Population).Append(" departures=").Append(Departures);
				InHeartbeat = false; ActiveSurvey = null;
			}
			private void CaptureDeparture(KingdomSystem system, GameObject body, KingdomResidentDepartureOperation operation)
			{
				Require(ReferenceEquals(system, System) && Departures == 0 && PopulationBefore == 3 && DryBefore == 1 && Paid == 0 && ConsumeStage == 2
					&& ReferenceEquals(operation, System.ResidentDeparture) && KingdomResidentDepartureRules.Valid(operation)
					&& operation.Phase == (int)KingdomResidentDeparturePhase.EffectsPublished && operation.PreparedTick == DispatchTick
					&& operation.RealmId == RealmId && operation.SettlementId == SettlementId && operation.ZoneId == ZoneId
					&& operation.DeparturesBefore == 0 && operation.Chronicled
					&& operation.Cause == "for wetter country, the cisterns having run dry", "departure lacks exact drought write-ahead authority");
				int index = Array.IndexOf(Setup.Residents, body);
				Require(index >= 0 && operation.ResidentId == Setup.Ids[index] && operation.BodyObjectId == Setup.BodyIds[index]
					&& body.GetPart<r_KingdomResidentDeparture>()?.Matches(operation, body) == true, "departure selected foreign resident/body marker");
				Departure = operation.Copy(); DepartedBody = body; DepartureMarker = body.GetPart<r_KingdomResidentDeparture>(); Departures++;
				Evidence.Append("\ndeparture operation=").Append(operation.OperationId).Append(" resident=").Append(operation.ResidentId).Append(" tick=").Append(operation.PreparedTick);
			}
			private void Roster(int expected)
			{
				Require(City.TryReadExact(out var city, out _), "canonical city read refused");
				Require(Bindings.TryReadExact(out var bindings, out _), "canonical bindings read refused");
				Require(City.ResidentCount == expected && Bindings.Count == expected && System.Population == expected, "canonical resident/binding count diverged");
				for (int i = 0; i < 3; i++)
				{
					GameObject body = Setup.Residents[i]; bool gone = ReferenceEquals(body, DepartedBody);
					Require(body != null && body._BaseID == Setup.BaseIds[i] && body.IDIfAssigned == Setup.BodyIds[i], "original resident identity changed");
					bool row = city.TryResidentIndex(Setup.Ids[i], out int index), bound = bindings.TryGet(Setup.Ids[i], KingdomBindingKind.Resident, out var binding);
					if (gone)
					{
						Require(!row && !bound && !GameObject.Validate(body) && body.IsInGraveyard() && Count(body, true) == 1 && Count(body, false) == 0
							&& body.Physics?._CurrentCell == null && ReferenceEquals(body.GetPart<r_KingdomResidentDeparture>(), DepartureMarker)
							&& DepartureMarker.TalliesClosed && KingdomResidentDepartureRules.Valid(Departure)
							&& KingdomResidentDepartureRules.IsEmpty(System.ResidentDeparture),
							"drought retirement mismatch: row=" + row + " bound=" + bound + " valid=" + GameObject.Validate(body)
							+ " grave=" + body.IsInGraveyard() + " origin-count=" + Count(body, true) + " live-count=" + Count(body, false)
							+ " has-cell=" + (body.Physics?._CurrentCell != null) + " same-marker=" + ReferenceEquals(body.GetPart<r_KingdomResidentDeparture>(), DepartureMarker)
							+ " tallies=" + DepartureMarker.TalliesClosed + " valid-operation=" + KingdomResidentDepartureRules.Valid(Departure)
							+ " journal-empty=" + KingdomResidentDepartureRules.IsEmpty(System.ResidentDeparture) + " notes=" + Ledger.Notes.Count + " exact-notes=" + Ledger.Notes.FindAll(x => x == Departure.LedgerLine).Count);
						KingdomWaterMaintenanceDepartureEvidence.Verify(System, Game, Ledger, Departure, () => Owner(KingdomSurvey.HasBoundPass), Evidence, ref DepartureStoryReported);
					}
					else Require(row && city.TryResident(index, out var resident) && resident.Standing == KingdomResidentStanding.Resident
						&& resident.BoundZoneId == ZoneId && resident.Name == "water fixture resident " + (i + 1)
						&& bound && binding.ZoneId == ZoneId && binding.ObjectId == Setup.BodyIds[i] && binding.MintedTick == EnrollTick
						&& GameObject.Validate(body) && body.IsAlive && KingdomCitizenship.BelongsTo(System, body)
						&& ReferenceEquals(body.Physics?._CurrentCell?.ParentZone, Zone) && Count(body, false) == 1, "live loyal resident lost exact row/body/binding");
				}
			}
			private void Controls(int stage = -1)
			{
				for (int i = 0; i < 4; i++)
				{
					try { Setup.VesselProofs[i].Exact(Setup.VesselCells[i], new[] { StoreWater, 12, 10, DonorWater }[i]); }
					catch { Setup.MarkerDiagnostics[i].Append(Evidence, i, stage, Game.TimeTicks); throw; }
					Require(Count(Setup.Vessels[i], false) == 1 && Setup.Vessels[i].GetIntProperty("KingdomStores") == (i == 0 || i == 2 ? 1 : 0), "dedicated/personal custody or markers diverged");
				}
				Require(Setup.Liquids[1].IsFreshWater() && !Setup.Liquids[2].IsFreshWater() && Setup.Liquids[2].ComponentLiquids.Count == Brine.Count, "personal/brine classification changed");
				foreach (var row in Brine) Require(Setup.Liquids[2].ComponentLiquids.TryGetValue(row.Key, out int value) && value == row.Value, "brine mixture changed");
				Require(StoreWater == 0 || Setup.Liquids[0].IsFreshWater(), "dedicated water contaminated");
			}
			private int Count(GameObject body, bool grave)
			{
				int count = 0;
				if (grave)
				{
					Require(ReferenceEquals(Zone.Graveyard, Origin) && ReferenceEquals(Origin.Objects, Graves) && Graves.Count <= 65536, "origin graveyard owner/bound changed");
					foreach (var row in Graves) if (ReferenceEquals(row, body)) count++;
					return count;
				}
				for (int x = 0; x < 80; x++) for (int y = 0; y < 25; y++)
				{
					var cell = Zone.GetCell(x, y); Require(cell.Objects.Count <= 512, "cell bound exceeded");
					foreach (var row in cell.Objects)
					{
						if (ReferenceEquals(row, body)) { Require(ReferenceEquals(body.Physics?._CurrentCell, cell), "body backlink differs"); count++; }
						else Require(string.IsNullOrEmpty(body.IDIfAssigned) || row?.IDIfAssigned != body.IDIfAssigned, "foreign body aliases retained ID");
					}
				}
				return count;
			}
			private void Owner(bool bound = false)
			{
				Require(ReferenceEquals(The.Game, Game) && Game.GameID == GameId && ReferenceEquals(The.Player, Player) && ReferenceEquals(Player.CurrentZone, Zone)
					&& ReferenceEquals(The.ZoneManager, Manager) && ReferenceEquals(Manager.ActiveZone, Zone) && Zone.ZoneID == ZoneId && Zone.Width == 80 && Zone.Height == 25
					&& Manager.CachedZones.TryGetValue(ZoneId, out var cached) && ReferenceEquals(cached, Zone)
					&& KingdomScenarioDurableState.ProvesExactText(KingdomWaterMaintenanceNativeProvider.Receipt, "intent")
					&& KingdomScenarioDurableState.ProvesExactText(KingdomScenarioProvenanceRules.ProvenanceState, Provenance), "water game/player/zone/provenance owner changed");
				if (City == null) return;
				Require(ReferenceEquals(Game.GetSystem<KingdomSystem>(), System) && System.Founded && !System.LoadFailed && !System.RealmRetirementBlocksWork
					&& ReferenceEquals(System.City, City) && ReferenceEquals(System.Bindings, Bindings) && ReferenceEquals(System.LifecycleBook, Lifecycle)
					&& ReferenceEquals(System.Ledger, Ledger) && System.CurrentRealmId == RealmId && City.SettlementId == SettlementId
					&& System.ClaimedZones.Count == 1 && System.ClaimedZones.Contains(ZoneId) && System.Stage == GrowthStage.Camp
					&& System.Stores == KingdomRules.StoresPolicy.Plenty && System.ZoneDistricts != null && System.ZoneDistricts.Count == 0
					&& KingdomLifecycleRules.CanOwnAuthority(Lifecycle) && !Lifecycle.Quarantined && !Lifecycle.WireRejected
					&& KingdomMaster.NewWorkAllowed(System) && KingdomGrowth.ScarcityEnabled && !KingdomGrowth.Enabled && !KingdomRaids.Enabled
					&& System.WaterCrew == 0 && Ledger.Fetched == 0 && Ledger.Arrivals == 0 && Ledger.ArrivalCost == 0
					&& (!KingdomSurvey.HasBoundPass || bound && ReferenceEquals(KingdomSurvey.ActiveFor(Zone)?.Ground, Zone)), "water settlement/option/accounting authority changed");
				if (Phase > 0) Require(System.MasterResumeToken == MasterToken && System.MasterAppliedResumeToken == MasterApplied, "master transition intruded on water test");
			}
			private void Span(int requested, bool observed)
			{
				long turns = Game.Turns - LastTurns;
				Require(turns >= requested && turns <= requested + 16 && Game.TimeTicks - LastTick == turns
					&& (!observed || Game.Turns - EnrollTurns == EndTurns && Game.TimeTicks - EnrollTick == EndTurns), "actual advance/observed dispatch counts differ");
			}
			private void SetClock() { LastTick = Game.TimeTicks; LastTurns = Game.Turns; }
		}
	}
}
