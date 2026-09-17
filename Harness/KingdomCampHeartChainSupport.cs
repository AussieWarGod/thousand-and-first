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
			private GameObject ChainTent;

			private void SeedChainSupport()
			{
				long tick = Game.TimeTicks;
				SeedChainHomes();
				for (int i = 0; i < 2; i++)
				{
					var water = KingdomNativeCampFounding.Dedicate(Game, Zone, System, 1800, Owned.Add,
						RequirePair, ChainSupplyCell);
					RequireChainStoreId(water.ParentObject);
				}
				SeedChainWaterSupport();
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
				RequireChainIngress();
				Require(System.Population >= 2 && System.Population <= 6, "source camp population differs");
				EnrollResidents(50 - System.Population);
				ChainResidents.AddRange(ChainResidentBodies(Census()));
				Require(ChainResidents.Count == 50 && KingdomResidents.OnRollCount(System) == 50,
					"city support did not enroll fifty actual bodies: bodies=" + ChainResidents.Count
					+ "; population=" + System.Population + "; on-roll=" + KingdomResidents.OnRollCount(System));
				Require(!KingdomSurvey.HasBoundPass, "housing setup found an outstanding survey pass");
				Require(KingdomSurvey.TryBindLocalOperation(Zone, System, out var scope,
					out string failure), failure);
				using (scope)
					KingdomLodging.OnSettlementPass(System, Zone, KingdomSurvey.ActiveFor(Zone));
				Require(!KingdomSurvey.HasBoundPass, "housing setup left its survey bound");
				// SYNTHETIC CRAFT, DISCLOSED. The four-rung chain needs foundry for the great
				// court; the arcology's own gate is MinTech="arclight"
				// (RuntimeData/KingdomBuildings.xml:1407), and research nodes are worth zero craft
				// points (Growth/KingdomZoningRules.cs:234), so the level is reached by disk
				// lessons and read BACK rather than written.
				TechLevel craft = ChainFinalRung == 5 ? TechLevel.Arclight : TechLevel.Foundry;
				for (int i = 0; i < KingdomZoningRules.PointsForLevel(craft); i++)
					Require(KingdomZoning.Learn(System, "disk", "paid-heart-chain-fixture-" + i),
						"synthetic craft lesson already present or refused");
				Require(KingdomZoning.Tech(System) == craft,
					"synthetic lessons did not reach the fixture craft level: " + KingdomZoning.Tech(System));
				foreach (var root in Census().Built)
					if (root.GetIntProperty(KingdomPlots.HeartPlotProperty) != 1)
						root.RequirePart<r_KingdomImprovement>().Held = true;
				Require(Game.TimeTicks == tick, "city support fixture advanced the real clock");
				RequireChainSupport();
			}

			private GameObject ChainContainer(string Purpose)
			{
				var container = Create("r_KingdomGranary");
				RequireChainStoreId(container);
				Require(container.Inventory != null && container.Inventory.Objects.Count == 0,
					"synthetic granary has unexpected contents");
				container.SetIntProperty(Purpose, 1);
				var cell = ChainSupplyCell();
				Require(cell != null && ReferenceEquals(cell.AddObject(container, NoStack: true), container)
					&& container.CurrentCell == cell && container.CurrentZone == Zone,
					"synthetic granary placement changed its custody");
				return container;
			}

			private void RequireChainStoreId(GameObject Store)
			{
				string id = Store.ID;
				Require(!string.IsNullOrEmpty(id) && Store.IDIfAssigned == id,
					"synthetic chain store has no assigned source identity");
			}

			private Cell ChainSupplyCell()
			{
				var plots = KingdomPlots.ReadPlots(Zone);
				Require(KingdomPlots.TryHeartRectFor(Zone, 4, out var heart), "final heart lot absent");
				plots.Add(heart);
				for (int y = 1; y < Zone.Height - 1; y++)
					for (int x = 1; x < Zone.Width - 1; x++)
					{
						var cell = Zone.GetCell(x, y);
						if (cell == null || !cell.IsEmpty() || !cell.IsPassable()
							|| cell.HasOpenLiquidVolume()) continue;
						var point = new KingdomPlotRules.PlotRect(x, y, x, y);
						bool clear = KingdomCampHeartChainGrid.ClearsWaterFootprints(point);
						foreach (var plot in plots)
							if (!KingdomCampHeartChainGrid.ClearsPaidApproach(point, plot)) clear = false;
						foreach (var item in cell.Objects)
							if (!GameObject.Validate(item) || item.IsCreature
								|| KingdomPlots.ReadObject(item) != KingdomPlotRules.GroundKind.Bare) clear = false;
						if (clear) return cell;
					}
				throw new InvalidOperationException("no bare supply cell outside authored approaches");
			}

			private void HoldChainTent()
			{
				Require(KingdomConstruction.TryRead(out var jobs, out string failure), failure);
				TentJobId = PaidTent(this, jobs);
				Require(KingdomConstruction.TryFind(TentJobId, out var job) && job != null
					&& job.Phase == KingdomConstructionPhase.Complete
					&& job.PhysicalPhase == KingdomPhysicalPhase.EffectsSettled,
					"source tent must complete through ordinary turns before heart materials arrive: phase="
					+ job?.Phase + "; physical=" + job?.PhysicalPhase + "; tick=" + Game.TimeTicks);
				Require(KingdomConstruction.FindExactId(Zone, job.OutputId, out var tent)
					== KingdomPhysicalLookupState.Exact, "completed source tent lacks exact physical output");
				tent.RequirePart<r_KingdomImprovement>().Held = true;
				Require(tent.GetPart<r_KingdomImprovement>().Held, "source tent improvement hold did not persist");
				RequireChainCustody();
				RequireChainSpatialPreflight();
			}

			private void SeedChainHomes()
			{
				Require(KingdomData.TryGetBuilding("tentrow", out var entry), "authored tent row missing");
				Require(KingdomPlots.TryGetSpec("tentrow", out var spec), "authored tent row spec missing");
				Require(KingdomPlotRules.TryInterior(Zone.Width, Zone.Height, out var interior),
					"city support ground has no plot interior");
				Require(KingdomConstruction.TryRead(out var jobs, out string failure), failure);
				TentJobId = PaidTent(this, jobs);
				Require(KingdomConstruction.TryFind(TentJobId, out var tentJob) && tentJob != null,
					"paid tent job absent before housing setup");
				Require(KingdomConstruction.FindExactId(Zone, tentJob.OutputId, out ChainTent)
					== KingdomPhysicalLookupState.Exact, "paid tent output absent before housing setup");
				Require(KingdomPlots.TryReadRect(ChainTent, out var tentRect), "paid tent lot absent");
				Require(KingdomPlots.TryHeartRectFor(Zone, 4, out var heartRect), "final heart lot absent");
				string lastFailure = null;
				foreach (var rect in KingdomCampHeartChainGrid.Candidates())
				{
					if (ChainHomes.Count == 18) break;
					if (!KingdomPlotRules.Fits(rect, interior)) continue;
					if (!KingdomCampHeartChainGrid.ClearsPaidApproach(rect, tentRect)
						|| !KingdomCampHeartChainGrid.ClearsPaidApproach(rect, heartRect)) continue;
					if (KingdomPlotRules.CrowdsExisting(rect, KingdomPlots.ReadPlots(Zone)))
					{
						lastFailure = "candidate crowds an existing plot's reserved lane";
						continue;
					}
					var grid = new KingdomPlots.GroundGrid(Zone);
					if (grid.AnyRefusal(rect))
					{
						lastFailure = "candidate contains protected or liquid ground";
						continue;
					}
					if (!KingdomPlots.TryPreparePlotPayload(System, Zone, rect, entry.Key,
						entry.Category, null, out _, out _, out lastFailure)) continue;
					var work = KingdomPlots.Stake(System, Zone, rect, entry, spec,
						grid, null, false);
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
				RequireChainIngress();
			}

			private void RequireChainIngress()
			{
				var roots = new List<GameObject>(ChainHomes) { ChainTent, StandingHeart() };
				foreach (var root in roots)
				{
					Require(KingdomArchitectureStamper.TryReadOwner(root, out var intent,
						out var snapshot, out _, out string failure), failure);
					Require(KingdomArchitectureRuntime.TryVerifyPhysicalIngressRoutes(Zone,
						intent.Rect, snapshot, out failure), "city fixture blocked ingress for "
						+ root.IDIfAssigned + ": " + failure);
				}
			}

		}
	}
}
