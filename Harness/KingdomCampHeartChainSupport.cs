using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	internal static partial class KingdomCampHeartNativeChecks
	{
		private sealed partial class Frame
		{
			private readonly List<GameObject> ChainHomes = new List<GameObject>();
			private readonly List<GameObject> ChainResidents = new List<GameObject>();

			private void SeedChainSupport()
			{
				long tick = Game.TimeTicks;
				SeedChainHomes();
				for (int i = 0; i < 2; i++)
					KingdomNativeCampFounding.Dedicate(Game, Zone, System, 1800, Owned.Add, RequirePair);
				int beforeFood = Census().FoodStored;
				for (int i = 0; i < 6; i++)
				{
					var pantry = ChainContainer("KingdomLarder");
					Require(pantry.Inventory.Objects.Count == 0, "synthetic pantry was not empty");
				}
				var food = Census();
				Require(food.StoreFood(1728, KingdomData.CropForStyle(System.Style)) == 1728
					&& Census().FoodStored == beforeFood + 1728, "synthetic pantry supply did not land exactly");
				ChainStore = ChainContainer("KingdomStockpile");
				Require(System.Population >= 2 && System.Population <= 6, "source camp population differs");
				EnrollResidents(50 - System.Population);
				ChainResidents.AddRange(Census().Settlers);
				Require(ChainResidents.Count == 50 && KingdomResidents.OnRollCount(System) == 50,
					"city support did not enroll fifty actual bodies");
				KingdomLodging.OnSettlementPass(System, Zone, Census());
				for (int i = 0; i < KingdomZoningRules.PointsForLevel(TechLevel.Foundry); i++)
					Require(KingdomZoning.Learn(System, "disk", "paid-heart-chain-fixture-" + i),
						"synthetic craft lesson already present or refused");
				Require(KingdomZoning.Tech(System) == TechLevel.Foundry, "synthetic lessons did not reach foundry craft");
				foreach (var root in Census().Built)
					if (root.GetIntProperty(KingdomPlots.HeartPlotProperty) != 1)
						root.RequirePart<r_KingdomImprovement>().Held = true;
				Require(Game.TimeTicks == tick, "city support fixture advanced the real clock");
				RequireChainSupport();
			}

			private GameObject ChainContainer(string Purpose)
			{
				var container = Create("r_KingdomGranary");
				Require(container.Inventory != null && container.Inventory.Objects.Count == 0,
					"synthetic granary has unexpected contents");
				container.SetIntProperty(Purpose, 1);
				var cell = KingdomNativeCampFounding.Clear(Zone);
				Require(cell != null && ReferenceEquals(cell.AddObject(container, NoStack: true), container)
					&& container.CurrentCell == cell && container.CurrentZone == Zone,
					"synthetic granary placement changed its custody");
				return container;
			}

			private void SeedChainHomes()
			{
				Require(KingdomData.TryGetBuilding("tentrow", out var entry), "authored tent row missing");
				Require(KingdomPlots.TryGetSpec("tentrow", out var spec), "authored tent row spec missing");
				string lastFailure = null;
				for (int side = 0; side < 2 && ChainHomes.Count < 18; side++)
					for (int y = 2; y <= 20 && ChainHomes.Count < 18; y += 6)
						for (int column = 0; column < 3 && ChainHomes.Count < 18; column++)
						{
							int x = (side == 0 ? 2 : 54) + column * 8;
							var rect = new KingdomPlotRules.PlotRect(x, y, x + 5, y + 3);
							if (!KingdomPlots.TryPreparePlotPayload(System, Zone, rect, entry.Key,
								entry.Category, null, out _, out _, out lastFailure)) continue;
							var work = KingdomPlots.Stake(System, Zone, rect, entry, spec,
								new KingdomPlots.GroundGrid(Zone), null, false);
							Require(work != null, "synthetic home stake refused after preflight");
							string plot = work.GetStringProperty(KingdomPlots.PlotIdProperty);
							var works = work.GetPart<r_KingdomPlotWorks>();
							Require(works != null && !string.IsNullOrEmpty(plot), "synthetic home lacks exact works");
							KingdomPlots.Advance(works, System, checked(works.StartTick + works.TotalTicks));
							GameObject home = null;
							foreach (var root in Census().Built)
								if (root.GetStringProperty(KingdomPlots.PlotIdProperty) == plot)
								{
									Require(home == null, "synthetic home plot has multiple outputs");
									home = root;
								}
							Require(home != null && KingdomUpgrade.DesignKeyOf(home) == "tentrow"
								&& KingdomUpgrade.IsFunctionallyBuilt(home)
								&& KingdomArchitectureStamper.TryVerifyComplete(home, Zone, out lastFailure),
								"synthetic housing completion refused: " + lastFailure);
							ChainHomes.Add(home);
						}
				Require(ChainHomes.Count == 18, "eighteen authored homes do not fit: count="
					+ ChainHomes.Count + "; last=" + lastFailure);
			}

			private void RequireChainSupport()
			{
				var survey = Census();
				Require(System.Population >= 50 && survey.Settlers.Count >= 50
					&& survey.StorageCapacity >= 1024 && survey.StoredWater > 0 && survey.FoodStored > 0,
					"city support lost population, water or food: population=" + System.Population
					+ "; water=" + survey.StoredWater + "; food=" + survey.FoodStored);
				foreach (var resident in ChainResidents)
					Require(GameObject.Validate(resident) && resident.IsAlive && resident.CurrentZone == Zone
						&& KingdomCitizenship.BelongsTo(System, resident) && KingdomResidents.TryLocate(System, resident, out _, out _)
						&& !string.IsNullOrEmpty(KingdomLodging.HomeDesignKeyOf(Zone, resident)),
						"an original city fixture resident lost their body, roll, citizenship or home");
			}
		}
	}
}
