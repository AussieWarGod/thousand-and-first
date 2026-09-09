using System;
using System.Collections.Generic;
using HarmonyLib;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	[KingdomScenarioVerbProvider]
	public sealed class KingdomWaterMaintenanceNativeProvider : IKingdomScenarioVerbProvider
	{
		internal const string SetupVerb = "water-maintenance-setup", EnrollVerb = "water-maintenance-enroll";
		internal const string CheckVerb = "water-maintenance-check", RefillVerb = "water-maintenance-refill";
		internal const string Receipt = "r_TAF_ScenarioWaterMaintenanceNative_v1";
		private static readonly string[] Script = { "stagedigest", SetupVerb, "advance 2400", EnrollVerb,
			"advance 1200", CheckVerb, "advance 1200", CheckVerb, "advance 1200", RefillVerb,
			"advance 1200", CheckVerb, "stagedigest" };
		public int ScenarioVerbApiVersion { get { return KingdomScenarioVerbApi.Version; } }
		public IEnumerable<string> ScenarioVerbs { get { return new[] { SetupVerb, EnrollVerb, CheckVerb, RefillVerb }; } }
		public string RunScenarioVerb(string verb, string argument, out bool Ok)
		{
			Ok = false;
			try
			{
				Require(string.IsNullOrEmpty(argument) && (verb == SetupVerb || verb == EnrollVerb || verb == CheckVerb || verb == RefillVerb), "water verbs take no arguments");
				Require(KingdomScenarioScript.TryRead(out var script, out _) && script.Count == Script.Length, "exact sealed water script absent");
				for (int i = 0; i < Script.Length; i++) Require(script[i] == Script[i], "water script differs");
				XRLGame game = The.Game; Zone zone = The.Player?.CurrentZone;
				if (verb == SetupVerb)
				{
					Require(Eligible(game, zone), "requires fresh stamped marsh and enabled master/thirst");
					game.SetStringGameState(Receipt, "intent");
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"), "water intent failed exact readback");
				}
				Require(game != null && KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"), "water owner intent absent or torn");
				string result = KingdomWaterMaintenanceNativeChecks.Run(verb, game, zone, out bool complete);
				if (complete)
				{
					Require(ReferenceEquals(The.Game, game) && KingdomScenarioDurableState.ProvesExactText(Receipt, "intent"), "water report owner changed");
					game.SetStringGameState(Receipt, result);
					Require(KingdomScenarioDurableState.ProvesExactText(Receipt, result), "water report failed exact readback");
				}
				Ok = true; return result;
			}
			catch (Exception error) { return KingdomWaterMaintenanceNativeChecks.Fail(error); }
		}
		private static bool Eligible(XRLGame game, Zone zone)
		{
			return game != null && zone != null && ReferenceEquals(The.ZoneManager?.ActiveZone, zone)
				&& MessageQueue.Enabled && KingdomMaster.ConfiguredEnabled && KingdomGrowth.ScarcityEnabled
				&& !KingdomSurvey.HasBoundPass && !KingdomScenarioAdvance.Pending
				&& !(game.GetSystem<KingdomSystem>()?.Founded ?? false) && KingdomWaterMaintenanceNativeChecks.Vacant
				&& KingdomSubsidenceNativeFixture.LastAttempt == null && KingdomRaidLaunchNativeFixture.LastAttempt == null
				&& !KingdomNativeRegressionContext.HasQuickstartState(game) && !KingdomNativeRegressionContext.HasAnyState(game, Receipt)
				&& KingdomScenarioRealizer.TryBindStampedPlan(out var plan, out _, out _) && plan.Key == "founding-first-city"
				&& plan.AuthorityClass == KingdomScenarioFoundingStep.FoundingAuthority
				&& KingdomScenarioTransactionMarker.Observe(out _) == KingdomScenarioTransactionShape.None
				&& KingdomQuickstartRules.TryProfile("marsh", out var profile) && zone.ZoneID == profile.ZoneId;
		}
		internal static void Require(bool value, string failure)
		{ if (!value) throw new InvalidOperationException(failure ?? "native water evidence refused"); }
	}

	// Void observations only. No production argument, result, callback, or event is replaced.
	[HarmonyPatch(typeof(KingdomSystem), "HandleEvent", new Type[] { typeof(EndTurnEvent) })]
	internal static class KingdomWaterMaintenanceTurnObserver
	{
		[HarmonyPrefix] internal static void Prefix(KingdomSystem __instance)
		{ KingdomWaterMaintenanceNativeChecks.Observe(0, __instance); }
		[HarmonyPostfix] internal static void Postfix(KingdomSystem __instance)
		{ KingdomWaterMaintenanceNativeChecks.Observe(6, __instance); }
	}
	[HarmonyPatch(typeof(KingdomGrowth), "ResolveHeartbeat")]
	internal static class KingdomWaterMaintenanceHeartbeatObserver
	{
		[HarmonyPrefix] internal static void Prefix(KingdomSystem System, Zone Z, KingdomSurvey Survey, long TimeTicks)
		{ KingdomWaterMaintenanceNativeChecks.Observe(1, System, Survey, Z, TimeTicks); }
		[HarmonyPostfix] internal static void Postfix(KingdomSystem System, Zone Z, KingdomSurvey Survey, long TimeTicks, bool __result)
		{ KingdomWaterMaintenanceNativeChecks.Observe(4, System, Survey, Z, TimeTicks, healthy: __result); }
	}
	[HarmonyPatch(typeof(KingdomSurvey), "ConsumeUpkeep", new Type[] { typeof(int) })]
	internal static class KingdomWaterMaintenanceDebitObserver
	{
		[HarmonyPrefix] internal static void Prefix(KingdomSurvey __instance, int Drams)
		{ KingdomWaterMaintenanceNativeChecks.Observe(2, null, __instance, amount: Drams); }
		[HarmonyPostfix] internal static void Postfix(KingdomSurvey __instance, int __result)
		{ KingdomWaterMaintenanceNativeChecks.Observe(3, null, __instance, amount: __result); }
	}
	[HarmonyPatch(typeof(KingdomResidentDepartureRuntime), "TryDestroyBody")]
	internal static class KingdomWaterMaintenanceDepartureObserver
	{
		[HarmonyPrefix] internal static void Prefix(KingdomSystem System, GameObject leaver, KingdomResidentDepartureOperation Operation)
		{ KingdomWaterMaintenanceNativeChecks.Observe(5, System, body: leaver, departure: Operation); }
	}

	// Synthetic fixtures only: genuine founding/enrollment APIs, real bodies and native placement.
	internal sealed class KingdomWaterMaintenanceSetup
	{
		internal readonly XRLGame Game;
		internal readonly Zone Zone;
		internal readonly List<GameObject> Owned = new List<GameObject>();
		private readonly List<int> AllocationIds = new List<int>();
		private readonly List<Physics> AllocationPhysics = new List<Physics>();
		internal readonly GameObject[] Residents = new GameObject[3], Vessels = new GameObject[4];
		internal readonly int[] Ids = new int[3], BaseIds = new int[3];
		internal readonly string[] BodyIds = new string[3];
		internal readonly Cell[] VesselCells = new Cell[4];
		internal readonly LiquidVolume[] Liquids = new LiquidVolume[4];
		internal readonly KingdomRaidContactBody[] VesselProofs = new KingdomRaidContactBody[4];
		internal readonly KingdomWaterMaintenanceMarkerDiagnostic[] MarkerDiagnostics = new KingdomWaterMaintenanceMarkerDiagnostic[4];
		internal KingdomWaterMaintenanceSetup(XRLGame game, Zone zone) { Game = game; Zone = zone; }
		internal KingdomSystem Found()
		{
			Require(KingdomScenarioRealizer.TryBindStampedPlan(out var plan, out _, out string failure), failure);
			string name = null;
			foreach (var step in plan.Steps) if (step.Verb == KingdomScenarioVerb.FoundFirstCity)
				Require(name == null && step.Arguments.TryGetValue("CityName", out name) && !string.IsNullOrEmpty(name), "founding name missing/repeated");
			Require(name != null && KingdomScenarioFoundingStep.TryProvePreconditions(Zone, name, out failure), failure);
			Require(KingdomScenarioTransactionMarker.TryBegin(out failure), failure);
			Require(KingdomScenarioFoundingStep.TryFound(Zone, name, out _, out failure), failure);
			Require(KingdomScenarioTransactionMarker.TryCommit(out failure), failure);
			return Game.GetSystem<KingdomSystem>();
		}
		internal void Enroll(KingdomSystem system, Action prove, Action proveBound)
		{
			prove(); long tick = Game.TimeTicks;
			for (int i = 0; i < 4; i++)
			{
				Vessels[i] = Create("r_KingdomReservoir", prove); LiquidVolume liquid = Vessels[i].GetPart<LiquidVolume>();
				Require(liquid != null && ReferenceEquals(liquid.ParentObject, Vessels[i]) && liquid.MaxVolume == 1920
					&& liquid.Volume == 0 && KingdomLiquids.CanReceiveFreshWater(liquid), "reservoir is not exact empty finite custody");
				// Explicit synthetic dedication. Personal and refill-donor vessels remain unmarked.
				if (i == 0 || i == 2) Vessels[i].SetIntProperty("KingdomStores", 1);
				liquid.AddDrams("water", new[] { 1, 12, 6, 16 }[i]); prove(); Unplaced(Vessels[i]);
				if (i == 2) { liquid.AddDrams("salt", 4); prove(); Unplaced(Vessels[i]); }
				prove(); Liquids[i] = liquid; VesselCells[i] = Place(Vessels[i], prove);
			}
			// Finish the actual city's dedication setup before the first immutable custody baseline.
			// No resident exists yet, and this setup call is not evidence of a real-turn heartbeat.
			Dedicate(system, prove, proveBound);
			for (int i = 0; i < 4; i++)
			{
				VesselProofs[i] = new KingdomRaidContactBody(Vessels[i]);
				MarkerDiagnostics[i] = new KingdomWaterMaintenanceMarkerDiagnostic(Vessels[i]);
			}
			for (int i = 0; i < 3; i++)
			{
				GameObject body = Residents[i] = Create("NPC", prove);
				Require(body.Brain != null && body.Body != null && body.Inventory != null && body.IsAlive
					&& !body.IsPlayer() && !body.IsPlayerLed() && body.GetIntProperty("KingdomCitizen") == 0
					&& body.GetPart<r_KingdomCitizenship>() == null, "NPC lacks fresh eligible citizenship shape");
				body.SetStringProperty("Species", "human"); Require(body.GetSpecies() == "human", "synthetic species refused"); prove(); Unplaced(body);
				Require(KingdomCitizenship.TryEnroll(system, body, KingdomCitizenshipEnrollmentReason.Arrival, tick, out string failure), failure);
				prove(); Unplaced(body); Require(KingdomCitizenship.BelongsTo(system, body), "enrollment callback lost citizenship authority");
				body.SetIntProperty("KingdomBorn", 1);
				string name = "water fixture resident " + (i + 1);
				body.GiveProperName(name, Force: true); prove(); Unplaced(body);
				body.SetStringProperty("KingdomName", name); body.SetStringProperty("KingdomOrigin", "native water fixture");
				Place(body, prove);
				Require(KingdomResidents.TryEnsureRow(system, body, "native water fixture", null, tick, out var city, out int id)
					&& ReferenceEquals(city, system.City) && id > 0, "real enrollment did not publish row and binding");
				Ids[i] = id; BaseIds[i] = body._BaseID; BodyIds[i] = body.IDIfAssigned; prove();
			}
			Require(Game.TimeTicks == tick, "fixture enrollment advanced world clock");
		}
		private void Dedicate(KingdomSystem system, Action prove, Action proveBound)
		{
			prove(); Require(!KingdomSurvey.HasBoundPass && system.Population == 0
				&& system.City.ResidentCount == 0 && system.Bindings.Count == 0, "dedication requires empty unbound camp");
			long tick = Game.TimeTicks, turns = Game.Turns, actions = Game.ActionTicks, playerActions = Game.PlayerActionTicks;
			long heartbeat = system.LastHeartbeatTick; int counter = system.DedicationCounter;
			var ids = new string[4]; var parts = new IPart[4][];
			for (int i = 0; i < 4; i++) { ids[i] = Vessels[i].IDIfAssigned; parts[i] = Vessels[i].PartsList.ToArray(); }
			Require(counter >= 0 && counter < int.MaxValue - 2, "dedication counter is not bounded");
			for (int i = 0; i < 4; i++) Require(!Vessels[i].HasIntProperty(KingdomCity.DedicationOrderProperty), "fresh vessel already has dedication order");
			KingdomSurvey survey = KingdomSurvey.Take(Zone, system); prove();
			Require(survey.Stores.Count == 2 && ReferenceEquals(survey.Stores[0], Liquids[0])
				&& ReferenceEquals(survey.Stores[1], Liquids[2]) && survey.StoredWater == 1
				&& survey.Larders.Count == 0 && survey.Citizens == 0, "dedication survey differs from exact empty camp");
			using (survey.BindPass())
			{
				Require(ReferenceEquals(KingdomSurvey.ActiveFor(Zone), survey), "dedication survey did not bind");
				KingdomCity.CheckIn(system, Zone, survey, tick); proveBound();
				Require(ReferenceEquals(KingdomSurvey.ActiveFor(Zone), survey), "dedication changed bound survey");
			}
			Require(!KingdomSurvey.HasBoundPass && Game.TimeTicks == tick && Game.Turns == turns
				&& Game.ActionTicks == actions && Game.PlayerActionTicks == playerActions
				&& system.LastHeartbeatTick == heartbeat && system.DryStreak == 0 && system.Population == 0
				&& system.City.ResidentCount == 0 && system.Bindings.Count == 0
				&& system.Ledger.UpkeepDrawn == 0 && system.Ledger.Departures == 0
				&& system.DedicationCounter == counter + 2, "dedication changed clock, upkeep, population or counter unexpectedly");
			for (int i = 0; i < 4; i++)
			{
				Require(GameObject.Validate(Vessels[i]) && Vessels[i]._BaseID == AllocationIds[i] && Vessels[i].IDIfAssigned == ids[i]
				&& ReferenceEquals(Vessels[i].Physics, AllocationPhysics[i]) && ReferenceEquals(Vessels[i].CurrentCell, VesselCells[i])
				&& ReferenceEquals(Vessels[i].GetPart<LiquidVolume>(), Liquids[i]) && Liquids[i].Volume == new[] { 1, 12, 10, 16 }[i]
				&& Vessels[i].GetIntProperty("KingdomStores") == (i == 0 || i == 2 ? 1 : 0)
				&& (i == 0 || i == 2 ? Vessels[i].GetIntProperty(KingdomCity.DedicationOrderProperty) == counter + (i == 0 ? 1 : 2)
					: !Vessels[i].HasIntProperty(KingdomCity.DedicationOrderProperty)), "native dedication lost original vessel, stock or exact ordinal");
				Require(Vessels[i].PartsList.Count == parts[i].Length, "dedication changed vessel parts");
				for (int j = 0; j < parts[i].Length; j++) Require(ReferenceEquals(Vessels[i].PartsList[j], parts[i][j])
					&& ReferenceEquals(parts[i][j].ParentObject, Vessels[i]), "dedication changed original part or owner");
			}
			prove();
		}
		private GameObject Create(string blueprint, Action prove)
		{
			prove(); GameObject captured = null; string id = null; int baseId = 0;
			GameObject result = GameObject.Create(blueprint, BeforeObjectCreated: body =>
			{
				Require(body != null && captured == null && Owned.Count < 7, "factory original missing/repeated");
				Owned.Add(body); captured = body;
				// Qud assigns BaseID lazily; establish this fresh allocation's native ID before freezing it.
				id = body.ID; baseId = body._BaseID;
				Require(!string.IsNullOrEmpty(id) && baseId != 0, "native allocation identity was not established");
				AllocationIds.Add(baseId); AllocationPhysics.Add(body.Physics);
			});
			prove(); Require(ReferenceEquals(result, captured) && GameObject.Validate(result) && result.Blueprint == blueprint
				&& result.IDIfAssigned == id && result._BaseID == baseId && result.Count == 1 && result.CurrentCell == null
				&& result.InInventory == null && result.Equipped == null && result.Implantee == null
				&& (result.Inventory == null || result.Inventory.Objects.Count == 0), "factory returned foreign identity or custody");
			return result;
		}
		private void Unplaced(GameObject body)
		{
			int index = -1;
			for (int i = 0; i < Owned.Count; i++) if (ReferenceEquals(Owned[i], body)) { Require(index < 0, "allocation reference repeated"); index = i; }
			Require(index >= 0 && GameObject.Validate(body) && body._BaseID == AllocationIds[index]
				&& ReferenceEquals(body.Physics, AllocationPhysics[index]) && body.Count == 1 && body.CurrentCell == null
				&& body.InInventory == null && body.Equipped == null && body.Implantee == null, "allocation callback acquired foreign custody or identity");
		}
		private Cell Place(GameObject body, Action prove)
		{
			prove(); Unplaced(body);
			Cell target = null;
			for (int y = 1; y < Zone.Height - 1 && target == null; y++) for (int x = 1; x < Zone.Width - 1 && target == null; x++)
			{
				Cell cell = Zone.GetCell(x, y); bool clear = cell.IsEmpty() && cell.IsPassable() && !cell.HasOpenLiquidVolume();
				foreach (GameObject row in cell.Objects) if (!GameObject.Validate(row) || row.IsCreature
					|| KingdomPlots.ReadObject(row) != KingdomPlotRules.GroundKind.Bare) clear = false;
				if (clear) target = cell;
			}
			Require(target != null, "bounded empty native placement cell unavailable");
			var rack = target.Objects; var before = rack.ToArray(); prove(); Unplaced(body);
			Require(ReferenceEquals(target.AddObject(body, NoStack: true), body), "native placement substituted object");
			prove(); Require(ReferenceEquals(target.Objects, rack) && rack.Count == before.Length + 1, "placement changed target rack unexpectedly");
			int at = 0, found = 0;
			foreach (var row in rack) { if (ReferenceEquals(row, body)) found++; else Require(at < before.Length && ReferenceEquals(row, before[at++]), "placement changed existing rows"); }
			Require(found == 1 && at == before.Length && ReferenceEquals(body.Physics?._CurrentCell, target)
				&& body.Physics?._InInventory == null && body.Physics?._Equipped == null, "placement lacks exclusive original custody");
			return target;
		}
		private static void Require(bool value, string failure) { KingdomWaterMaintenanceNativeProvider.Require(value, failure); }
	}
}
