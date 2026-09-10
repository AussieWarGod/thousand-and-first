using System;
using System.Collections.Generic;
using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>
	/// Synthetic physical-capacity fixture, not 220 lawfully commissioned buildings. Real
	/// containers each have one unit of room; controlled model debt must touch every water
	/// vessel. Food debt retires inert instead of landing on a container
	/// (<c>Simulation/City/KingdomCity.z05.Reify.cs:41-61</c>, the physical-food ruling in
	/// <c>docs/STATUS.md</c>'s "Food and water are separate physical flows" paragraph), so the
	/// eight larders are proved CONSERVED -- exact identity, exact holder, exact raw count,
	/// before and after -- never delivered a unit. Retains IDs only so the witness cannot keep
	/// the home zone cached.
	/// </summary>
	[KingdomScenarioVerbProvider]
	public sealed partial class KingdomScenarioContainerStress : IKingdomScenarioVerbProvider
	{
		private const int WaterCount = 244, FoodCount = 8;
		// LIVING-CITY-ARCHITECTURE 0.0(b) / Core/KingdomRules.cs MaxCivicContainersPerZone: 220
		// commissioned root containers + 24 water + 8 food = 252 is the container ENVELOPE. This
		// fixture keeps that physical envelope (244 water + 8 food = 252) unchanged; only the
		// DEMANDED catch-up thirds shrinks, because food debt retires to zero before container
		// catch-up ever measures it (z05.Reify.cs:41-53 runs before :56-61's TryMeasure), so its
		// demand is always zero. 244 water containers * 3 thirds/medium unit = 732, not 252*3=756.
		private const int ThirdsPerUnit = 3;
		private const int InitialThirds = WaterCount * ThirdsPerUnit;
		private static readonly List<string> WaterIds = new List<string>(), FoodIds = new List<string>();
		private static XRLGame Game;
		private static KingdomSystem System;
		private static string Home;
		private static bool Attempted, Ready, ReturnDebtProved;
		public int ScenarioVerbApiVersion => KingdomScenarioVerbApi.Version;
		public IEnumerable<string> ScenarioVerbs => new[] { "beta-stress" };

		public string RunScenarioVerb(string Verb, string Argument, out bool Ok)
		{
			Ok = false;
			try
			{
				Require(Verb == "beta-stress" && string.IsNullOrEmpty(Argument) && !Attempted, "fixture already attempted or verb differs");
				Require(KingdomScenarioScript.TryRead(out var script, out _)
					&& (KingdomScenarioPauseController.Recipe(script, "beta-away")
						|| KingdomScenarioPauseController.Recipe(script, "beta-present")), "stress requires sealed pause travel recipe");
				var pause = KingdomScenarioPauseWitness.Current;
				Require(KingdomScenarioPauseController.Active && pause != null && pause.Fault == null
					&& pause.DisabledTick >= 0 && !KingdomMaster.ConfiguredEnabled
					&& pause.System.MasterOption == KingdomMasterLatchValue.Disabled, "master must be observably paused");
				Game = The.Game; System = Game.GetSystem<KingdomSystem>();
				Zone zone = The.Player?.CurrentZone; Home = zone?.ZoneID;
				Require(ReferenceEquals(Game, pause.Game) && ReferenceEquals(System, pause.System)
					&& zone != null && zone.Width == 80 && zone.Height == 25 && System.ClaimedZones.Contains(Home)
					&& !KingdomSurvey.HasBoundPass && System.City.TryReadExact(out var state, out _)
					&& state.ZoneCount == 1, "requires one exact founded home outside a survey pass");
				Attempted = true; // Partial physical fixtures remain retained, never silently retried.
				Populate(zone); SeedDebt(zone); Ready = true; Ok = true;
				return "taf-container-stress-ready; containers=252; water=244; food=8; initial-thirds="
					+ InitialThirds + "; synthetic=true; resident-stress=false";
			}
			catch (Exception error) { return KingdomScenarioRefusal.Message("taf-container-stress-refused", error.Message); }
		}

		/// <summary>
		/// Setup only -- not a conservation observation. <c>HeldIn</c> here is the ordinary,
		/// dispatching read used only to size how much capacity remains to fill; the raw seam
		/// (<see cref="KingdomMaterials.RawCensusCountOf"/>) is reserved for the conservation
		/// proof in <c>FoodConservation.cs</c>. Likewise <c>item.ID</c> below intentionally MINTS
		/// an identity for a container this fixture just created (<see cref="GameObject.Create"/>
		/// gives back an unassigned object) so it can be retained and re-found by id after the
		/// pause; this is the one legitimate assignment-time mint, distinct from every later
		/// observation, which reads <c>IDIfAssigned</c> and refuses rather than mints.
		/// </summary>
		private static void Populate(Zone zone)
		{
			var survey = KingdomSurvey.Take(zone, System);
			Require(survey.Stores.Count <= WaterCount && survey.Larders.Count <= FoodCount, "existing containers exceed fixture bounds");
			int waters = survey.Stores.Count, foods = survey.Larders.Count;
			for (int i = waters; i < WaterCount; i++) Create(zone, "r_KingdomReservoir", "KingdomStores");
			for (int i = foods; i < FoodCount; i++) Create(zone, "r_KingdomLarder", "KingdomLarder");
			survey = KingdomSurvey.Take(zone, System);
			Require(survey.Stores.Count == WaterCount && survey.Larders.Count == FoodCount, "physical container census differs");
			var ids = new HashSet<string>(StringComparer.Ordinal);
			foreach (var liquid in survey.Stores)
			{
				GameObject item = liquid.ParentObject;
				Require(GameObject.Validate(item) && item.CurrentZone == zone && liquid.MaxVolume > 1
					&& liquid.MaxVolume <= 1920 && liquid.Volume < liquid.MaxVolume
					&& KingdomLiquids.CanReceiveFreshWater(liquid), "water container is full, foreign, or not finite fresh water");
				int amount = liquid.MaxVolume - 1 - liquid.Volume;
				Require(amount >= 0 && (amount == 0 || KingdomLiquids.Fill(liquid, "water", amount) == amount)
					&& liquid.Volume == liquid.MaxVolume - 1 && KingdomLiquids.HasFreshWater(liquid), "water population differs");
				Require(!string.IsNullOrEmpty(item.ID) && ids.Add(item.ID), "container identity repeated"); WaterIds.Add(item.ID);
			}
			string crop = KingdomData.CropForStyle(System.Style);
			foreach (GameObject item in survey.Larders)
			{
				int capacity = KingdomSurvey.CapacityOf(item), before = KingdomSurvey.HeldIn(item);
				Require(item.CurrentZone == zone && capacity > 1 && capacity <= 256 && before < capacity,
					"larder is full, foreign, or outside fixture capacity");
				int amount = capacity - 1 - before;
				Require((amount == 0 || survey.StoreFoodIn(item, amount, crop) == amount)
					&& KingdomSurvey.HeldIn(item) == capacity - 1, "food population differs");
				Require(!string.IsNullOrEmpty(item.ID) && ids.Add(item.ID), "container identity repeated"); FoodIds.Add(item.ID);
				BindFoodBodies(item, crop);
			}
		}

		private static void Create(Zone zone, string blueprint, string marker)
		{
			Cell target = null;
			// Keep the whole travel row clear. No clearing or moving existing objects.
			for (int y = 1; y < zone.Height - 1 && target == null; y++)
				for (int x = 1; x < zone.Width - 1 && target == null; x++)
				{
					Cell cell = zone.GetCell(x, y);
					if (y != The.Player.CurrentCell.Y && cell != null && cell.GetObjects().Count == 0) target = cell;
				}
			Require(target != null, "insufficient empty physical cells; no clearing fallback");
			GameObject item = GameObject.Create(blueprint);
			Require(GameObject.Validate(item) && item.CurrentCell == null && item.InInventory == null, "factory did not give private object");
			item.SetIntProperty(marker, 1);
			Require(ReferenceEquals(target.AddObject(item, NoStack: true), item) && item.CurrentCell == target
				&& item.CurrentZone == zone, "container placement lost exact custody");
		}

		private static void SeedDebt(Zone zone)
		{
			var book = System.City;
			KingdomZoneRow row = default(KingdomZoneRow);
			Require(book.TryReadExact(out var state, out _) && state.ZoneCount == 1 && state.TryZone(0, out row)
				&& row.ZoneId == Home && row.OwedWater == 0 && row.OwedFood == 0 && row.OwedMaterials == 0,
				"preexisting physical debt cannot be overwritten");
			var survey = KingdomSurvey.Take(zone, System);
			long waterCapacity = 0;
			foreach (var liquid in survey.Stores) waterCapacity += liquid.MaxVolume;
			var stocks = new KingdomStocks(new KingdomStockPair(waterCapacity, waterCapacity),
				new KingdomStockPair(survey.FoodCapacity, survey.FoodCapacity), row.Stocks.Materials);
			// FoodCount is seeded as LEGACY debt on purpose: the point of this fixture's food arm
			// is to prove that debt retires inert (z05.Reify.cs:41-53), not to demand a delivery.
			var nextRow = row.WithReading(Game.TimeTicks, stocks, row.Roofs, row.Defence, row.WaterCarry, row.FoodCarry)
				.WithOwed(WaterCount, FoodCount, 0);
			Require(state.TryWithZone(0, nextRow, out var next, out _) && next.TryWithStocks(stocks, out next, out _)
				&& book.TryPublish(next, out _) && book.TryReadExact(out _, out _)
				&& book.ZoneOwedWater[0] == WaterCount && book.ZoneOwedFood[0] == FoodCount,
				"controlled fixture debt did not publish exactly");
		}

		internal static string Check()
		{
			if (!Attempted) return "; full-envelope-stress=false";
			Require(Ready && ReturnDebtProved && ReferenceEquals(The.Game, Game) && ReferenceEquals(Game.GetSystem<KingdomSystem>(), System)
				&& The.Player?.CurrentZone?.ZoneID == Home, "stress owner or return differs");
			Zone zone = The.Player.CurrentZone;
			var survey = KingdomSurvey.Take(zone, System);
			Require(survey.Stores.Count == WaterCount && survey.Larders.Count == FoodCount
				&& WaterIds.Count == WaterCount && FoodIds.Count == FoodCount, "full physical envelope not retained");
			foreach (string id in WaterIds)
			{
				var item = zone.FindObjectByID(id); var liquid = item?.GetPart<LiquidVolume>();
				Require(liquid != null && survey.Stores.Contains(liquid) && liquid.Volume == liquid.MaxVolume
					&& KingdomLiquids.HasFreshWater(liquid), "a water container did not receive its owed unit");
			}
			Require(System.City.TryReadExact(out var state, out _) && System.City.TryZoneRow(Home, out int row)
				&& System.City.ZoneOwedFood[row] == 0, "legacy food debt did not retire inert");
			VerifyFoodConserved(zone, "final");
			return "; full-envelope-stress=true; stress-initial-thirds=" + InitialThirds
				+ "; stress-residents=0; synthetic-fixture=true";
		}

		internal static void BeforeResume()
		{
			if (!Attempted) return;
			Require(Ready && !ReturnDebtProved && ReferenceEquals(The.Game, Game)
				&& ReferenceEquals(Game.GetSystem<KingdomSystem>(), System) && The.Player?.CurrentZone?.ZoneID == Home
				&& !KingdomMaster.ConfiguredEnabled && System.MasterOption == KingdomMasterLatchValue.Disabled,
				"return stress debt must be proved before master resume");
			Require(System.City.TryReadExact(out _, out _) && System.City.TryZoneRow(Home, out int row)
				&& System.City.ZoneOwedWater[row] == WaterCount && System.City.ZoneOwedFood[row] == FoodCount
				&& System.City.ZoneOwedMaterials[row] == 0, "stress debt changed while master was disabled");
			Zone zone = The.Player.CurrentZone;
			foreach (string id in WaterIds)
			{
				var liquid = zone.FindObjectByID(id)?.GetPart<LiquidVolume>();
				Require(liquid != null && liquid.Volume == liquid.MaxVolume - 1 && KingdomLiquids.HasFreshWater(liquid),
					"water debt landed or changed before return resume");
			}
			VerifyFoodConserved(zone, "pre-resume");
			ReturnDebtProved = true;
		}

		private static void Require(bool value, string why) { KingdomScenarioTravel.Require(value, why); }
	}
}
