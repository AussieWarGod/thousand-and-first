using System;
using System.Collections.Generic;
using System.Globalization;
using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Retained, synthetic native residency setup; never resets a founded profile.</summary>
	internal sealed class KingdomSubsidenceNativeFixture
	{
		internal const int ResidentCount = 50;
		internal const string StoreBlueprint = "r_KingdomReservoir";
		private readonly List<GameObject> Owned = new List<GameObject>();
		private readonly List<GameObject> Residents = new List<GameObject>();
		private readonly List<int> Ids = new List<int>();
		private readonly List<Cell> Cells = new List<Cell>();
		internal static KingdomSubsidenceNativeFixture LastAttempt { get; private set; }
		internal XRLGame Game { get; private set; }
		internal Zone Zone { get; private set; }
		internal KingdomSystem System { get; private set; }
		internal KingdomSurvey Survey { get; private set; }
		internal GameObject Store { get; private set; }
		internal IReadOnlyList<GameObject> Allocations { get { return Owned; } }
		internal IReadOnlyList<GameObject> Bodies { get { return Residents; } }
		internal IReadOnlyList<int> ResidentIds { get { return Ids; } }

		private KingdomSubsidenceNativeFixture(XRLGame Game, Zone Zone)
		{
			this.Game = Game;
			this.Zone = Zone;
		}

		internal static bool TryCreate(Zone Zone, out KingdomSubsidenceNativeFixture Fixture,
			out string Failure)
		{
			Fixture = LastAttempt;
			Failure = null;
			try
			{
				Require(Fixture == null, "a prior native subsidence fixture remains retained");
				XRLGame game = The.Game;
				Require(game != null && Zone != null && ReferenceEquals(The.Player?.CurrentZone, Zone)
					&& ReferenceEquals(The.ZoneManager?.ActiveZone, Zone), "exact active game/zone absent");
				KingdomScenarioPlan plan;
				KingdomScenarioProvenance stamp;
				string failure;
				Require(KingdomScenarioRealizer.TryBindStampedPlan(out plan, out stamp, out failure),
					"scenario provenance refused: " + failure);
				Require(plan.Key == "founding-first-city"
					&& plan.AuthorityClass == KingdomScenarioFoundingStep.FoundingAuthority,
					"fixture requires the stamped first-founding plan");
				string name = null;
				foreach (KingdomScenarioResolvedStep step in plan.Steps)
					if (step.Verb == KingdomScenarioVerb.FoundFirstCity)
					{
						Require(name == null && step.Arguments.TryGetValue("CityName", out name)
							&& !string.IsNullOrEmpty(name), "exact founding name missing or repeated");
					}
				Require(name != null && KingdomScenarioFoundingStep.TryProvePreconditions(Zone,
					name, out failure), "founding preconditions refused: " + failure);
				Require(KingdomMaster.ConfiguredEnabled && KingdomSubsidence.Enabled,
					"master and subsidence options must be enabled");
				Fixture = new KingdomSubsidenceNativeFixture(game, Zone);
				LastAttempt = Fixture;
				Require(KingdomScenarioTransactionMarker.TryBegin(out failure), failure);
				string line;
				Require(KingdomScenarioFoundingStep.TryFound(Zone, name, out line, out failure), failure);
				Require(KingdomScenarioTransactionMarker.TryCommit(out failure), failure);
				Fixture.System = game.GetSystem<KingdomSystem>();
				Fixture.Build();
				return true;
			}
			catch (Exception error)
			{
				Failure = "native subsidence fixture retained after " + error.GetType().Name;
				try { Failure += ": " + error.Message; } catch (Exception) { }
				return false;
			}
		}

		private void Build()
		{
			RequireWorld();
			Require(System.City != null && System.City.ResidentCount == 0
				&& KingdomResidents.OnRollCount(System) == 0 && System.Population == 0
				&& System.Bindings != null && System.Bindings.Count == 0
				&& System.ClaimedZones.Count == 1 && System.NonSeatSettlementCount == 0
				&& KingdomResidentDepartureRules.IsEmpty(System.ResidentDeparture),
				"founded fixture must have an empty exact resident authority");
			for (int y = 1; y < Zone.Height - 1 && Cells.Count <= ResidentCount; y++)
				for (int x = 1; x < Zone.Width - 1 && Cells.Count <= ResidentCount; x++)
				{
					Cell cell = Zone.GetCell(x, y);
					if (EmptyCell(cell)) Cells.Add(cell);
				}
			Require(Cells.Count == ResidentCount + 1, "51 clear physical fixture cells unavailable");
			Store = Create(StoreBlueprint);
			LiquidVolume liquid = Store.GetPart<LiquidVolume>();
			Require(liquid != null && ReferenceEquals(liquid.ParentObject, Store)
				&& liquid.MaxVolume == 1920 && liquid.Volume == 0
				&& KingdomLiquids.CanReceiveFreshWater(liquid), "empty finite reservoir shape changed");
			Store.SetIntProperty("KingdomStores", 1);
			Place(Store, Cells[ResidentCount]);
			long tick = Game.TimeTicks;
			Require(tick >= 0L, "fixture creation tick is invalid");
			for (int i = 0; i < ResidentCount; i++)
			{
				RequireWorld();
				GameObject body = Create("NPC");
				Residents.Add(body);
				Require(body.Brain != null && body.Body != null && body.Inventory != null
					&& body.IsAlive && !body.IsPlayer() && !body.IsPlayerLed()
					&& body.GetIntProperty("KingdomCitizen") == 0
					&& body.GetPart<r_KingdomCitizenship>() == null,
					"fresh NPC lacks eligible physical citizenship shape");
				string failure;
				Require(KingdomCitizenship.TryEnroll(System, body,
					KingdomCitizenshipEnrollmentReason.Arrival, tick, out failure), failure);
				body.SetIntProperty("KingdomBorn", 1);
				string name = "subsidence resident " + (i + 1).ToString("D2", CultureInfo.InvariantCulture);
				body.GiveProperName(name, Force: true);
				body.SetStringProperty("KingdomName", name);
				body.SetStringProperty("KingdomOrigin", "native fixture");
				Place(body, Cells[i]);
				KingdomCityBook book;
				int id;
				Require(KingdomResidents.TryEnsureRow(System, body, "native fixture", null, tick,
					out book, out id) && ReferenceEquals(book, System.City) && id > 0,
					"native enrollment did not publish exact row and binding");
				Ids.Add(id);
			}
			Verify();
		}

		private GameObject Create(string Blueprint)
		{
			GameObject captured = null;
			GameObject created = GameObject.Create(Blueprint, BeforeObjectCreated: body =>
			{
				Require(body != null, "factory supplied no allocation");
				Owned.Add(body);
				Require(captured == null, "factory exposed multiple roots");
				captured = body;
				body.SetIntProperty("NoLoot", 1);
			});
			Require(ReferenceEquals(created, captured) && GameObject.Validate(created)
				&& created.Blueprint == Blueprint && created.Count == 1
				&& created.CurrentCell == null && created.InInventory == null && created.Equipped == null
				&& created.GetIntProperty("NoLoot") == 1, "factory returned foreign or occupied custody");
			RequireEmptyContents(created);
			RequireWorld();
			return created;
		}

		private void Place(GameObject Body, Cell Cell)
		{
			RequireWorld();
			Require(EmptyCell(Cell), "fixture destination changed before placement");
			GameObject placed = Cell.AddObject(Body, NoStack: true);
			Require(ReferenceEquals(placed, Body) && ExactCell(Body, Cell),
				"native placement did not preserve exact custody");
			RequireEmptyContents(Body);
		}

		private void Verify()
		{
			RequireWorld();
			Survey = KingdomSurvey.Take(Zone, System);
			Require(ReferenceEquals(Survey.Ground, Zone) && Survey.Settlers.Count == ResidentCount
				&& Survey.Citizens == ResidentCount && System.Population == ResidentCount
				&& KingdomResidents.OnRollCount(System) == ResidentCount
				&& System.City.ResidentCount == ResidentCount && System.Bindings.Count == ResidentCount
				&& Survey.Stores.Count == 1 && ReferenceEquals(Survey.Stores[0].ParentObject, Store)
				&& Survey.StorageCapacity == 1920 && Survey.StoredWater == 0
				&& ExactCell(Store, Cells[ResidentCount]), "physical survey/roll/capacity does not match fixture");
			HashSet<int> ids = new HashSet<int>();
			HashSet<string> objects = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Residents.Count; i++)
			{
				GameObject body = Residents[i];
				Require(ids.Add(Ids[i]) && objects.Add(body.IDIfAssigned)
					&& ExactCell(body, Cells[i]) && Survey.Settlers.Contains(body)
					&& KingdomResidents.IdOf(body) == Ids[i]
					&& KingdomResidentTransitionAuthority.CanPrepareResidentBodyDestruction(System, body, Ids[i]),
					"resident is not an exact eligible departure candidate at " + i);
				RequireEmptyContents(body);
			}
			KingdomCatalogueRules.SupportTally support = KingdomSubsidence.ScopedSupports(System, Zone, Survey);
			Require(support.Water == 0 && support.Food == 0 && support.Roof == 0 && support.Lift == 0
				&& System.Shade == 0, "fixture unexpectedly supplies population support");
			GrowthStage stage = KingdomRules.StageFor(System.Population, Survey.StorageCapacity);
			Require(stage == GrowthStage.City, "measured fixture does not qualify for City");
			// Synthetic setup derives this field; it does not execute production stage advancement.
			System.Stage = stage;
		}

		private void RequireWorld()
		{
			Require(ReferenceEquals(The.Game, Game) && ReferenceEquals(The.ZoneManager?.ActiveZone, Zone)
				&& ReferenceEquals(The.Player?.CurrentZone, Zone)
				&& ReferenceEquals(Game.GetSystem<KingdomSystem>(), System) && System != null
				&& System.Founded && System.OwnedZone(Zone.ZoneID)
				&& !string.IsNullOrEmpty(System.CurrentRealmId)
				&& !string.IsNullOrEmpty(System.CurrentSettlementId)
				&& Factions.GetIfExists(System.KingdomFactionName) != null
				&& KingdomMaster.NewWorkAllowed(System), "founded native fixture authority changed");
		}

		private static bool EmptyCell(Cell Cell)
		{
			if (Cell == null || !Cell.IsPassable() || !Cell.IsEmpty() || Cell.HasOpenLiquidVolume()) return false;
			foreach (GameObject body in Cell.GetObjects())
				if (!GameObject.Validate(body) || body.IsCreature
					|| KingdomPlots.ReadObject(body) != KingdomPlotRules.GroundKind.Bare) return false;
			return true;
		}

		private bool ExactCell(GameObject Body, Cell Cell)
		{
			if (!GameObject.Validate(Body) || !ReferenceEquals(Body.CurrentCell, Cell)
				|| !ReferenceEquals(Body.CurrentZone, Zone) || Body.InInventory != null
				|| Body.Equipped != null || Body.Count != 1) return false;
			int found = 0;
			foreach (GameObject item in Cell.GetObjects()) if (ReferenceEquals(item, Body)) found++;
			return found == 1;
		}

		private static void RequireEmptyContents(GameObject Body)
		{
			List<GameObject> contents = Body.GetInventoryDirectAndEquipment();
			Require((Body.Inventory == null || Body.Inventory.Objects.Count == 0)
				&& (contents == null || contents.Count == 0), "unexpected native inventory/equipment retained");
		}

		private static void Require(bool Condition, string Failure)
		{
			if (!Condition) throw new InvalidOperationException(Failure ?? "native fixture refused");
		}
	}
}
