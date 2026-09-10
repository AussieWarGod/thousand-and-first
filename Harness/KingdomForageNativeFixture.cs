using System;
using System.Collections.Generic;
using System.Linq;
using XRL;
using XRL.World;
using XRL.World.Parts;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst.Harness
{
	/// <summary>Setup-only helpers for <see cref="KingdomForageNativeChecks"/>: real resident
	/// enrollment and real object planting. No assertion about forage's own behaviour lives
	/// here; this shard only builds the ground the checks shard reads.</summary>
	internal static partial class KingdomForageNativeChecks
	{
		private sealed partial class Frame
		{
			/// <summary>Four real NPC residents, enrolled through the production citizenship and
			/// roster APIs -- the same path <c>KingdomFirstGuestRuntime</c> uses for a genuine
			/// arrival, not a stamped field.</summary>
			private void EnrollFour()
			{
				long tick = Game.TimeTicks;
				for (int i = 0; i < 4; i++)
				{
					GameObject body = GameObject.Create("NPC");
					Require(body.Brain != null && body.Body != null && body.IsAlive
						&& !body.IsPlayer(), "fresh NPC lacks eligible physical shape");
					body.SetStringProperty("Species", "human");
					body.SetStringProperty("KingdomOrigin", "native forage fixture");
					string failure;
					Require(KingdomCitizenship.TryEnroll(System, body,
						KingdomCitizenshipEnrollmentReason.Arrival, tick, out failure), failure);
					KingdomCityBook book;
					int id;
					Require(KingdomResidents.TryEnsureRow(System, body, out book, out id)
						&& id > 0, "native enrollment did not publish a row");
					Cell cell = KingdomNativeCampFounding.Clear(Zone);
					Require(cell != null, "no clear cell for a fixture resident");
					Require(ReferenceEquals(cell.AddObject(body, NoStack: true), body),
						"native placement substituted a fixture resident");
				}
			}

			/// <summary>Every plot rect the plan currently declares (the heart's own, plus any
			/// other <c>ForagePlots</c> root) -- forage's own <c>ForagePlotCell</c> excludes a
			/// candidate standing in any of these, so the fixture must plant outdoor objects
			/// outside all of them, not just the heart's.</summary>
			private void CollectPlotRects()
			{
				foreach (GameObject root in Survey.ForagePlots)
				{
					if (!GameObject.Validate(root) || root.CurrentZone != Zone) continue;
					if (KingdomPlots.TryReadRect(root, out var rect))
						PlotRects.Add(new KingdomForageNativeGeometry.Rect(rect.X1, rect.Y1, rect.X2, rect.Y2));
				}
				Require(PlotRects.Any(r => r.X1 == HeartRect.X1 && r.Y1 == HeartRect.Y1
					&& r.X2 == HeartRect.X2 && r.Y2 == HeartRect.Y2),
					"the collected plot rects do not include the heart's own rect");
			}

			/// <summary>One real object per exclusion category, plus three eligible wild
			/// plants, all bare outdoor cells within forage radius and outside every plot rect
			/// (the in-plot plant sits inside the heart's own rect instead, which is a plot cell
			/// by construction).</summary>
			private void PlantExclusions()
			{
				Eligible = new[] { Plant("Plant", NextOutdoorCell("eligible plant 1")),
					Plant("Plant", NextOutdoorCell("eligible plant 2")),
					Plant("Plant", NextOutdoorCell("eligible plant 3")) };
				GameObject tree = Plant("Tree", NextOutdoorCell("tree"));
				TreeSnap = new ExclusionSnapshot(tree, "tree", item => item.HasTag("Tree"));
				GameObject owned = Plant("Plant", NextOutdoorCell("owned plant"));
				owned.GetPart<Physics>().Owner = "native-forage-fixture";
				OwnedSnap = new ExclusionSnapshot(owned, "owned plant",
					item => item.GetPart<Physics>()?.Owner == "native-forage-fixture");
				GameObject food = Plant("Yuckwheat", NextOutdoorCell("food plant"));
				FoodSnap = new ExclusionSnapshot(food, "food plant", item => item.HasPart("Harvestable"));
				GameObject protectedPlant = Plant("Plant", NextOutdoorCell("protected plant"));
				protectedPlant.SetIntProperty("KingdomStores", 1);
				ProtectedSnap = new ExclusionSnapshot(protectedPlant, "protected plant",
					item => item.GetIntProperty("KingdomStores") == 1);
				GameObject plotPlant = Plant("Plant", InHeartRect());
				PlotSnap = new ExclusionSnapshot(plotPlant, "in-plot plant",
					item => item.CurrentCell != null && KingdomForageNativeGeometry.InsideAnyRect(
						item.CurrentCell.X, item.CurrentCell.Y, PlotRects));
			}

			/// <summary>The next distinct, proved-bare cell outside every plot rect and outside
			/// any crop row, scanning outward ring by ring from the rite so the pick is
			/// deterministic. Never removes a world object to make room: a ring with no eligible
			/// cell is simply skipped, and a fully exhausted radius refuses by name.</summary>
			private Cell NextOutdoorCell(string Label)
			{
				for (int radius = 1; radius <= KingdomMaterialRules.ForageRadius; radius++)
					foreach ((int dx, int dy) in KingdomForageNativeGeometry.Ring(radius))
					{
						Cell cell = Zone.GetCell(RiteX + dx, RiteY + dy);
						if (cell == null || UsedOutdoorCells.Contains(cell) || !Bare(cell)) continue;
						if (KingdomForageNativeGeometry.InsideAnyRect(cell.X, cell.Y, PlotRects)) continue;
						if (IsCropRowCell(cell)) continue;
						UsedOutdoorCells.Add(cell);
						return cell;
					}
				Require(false, "no bare outdoor cell within radius " + KingdomMaterialRules.ForageRadius
					+ " for " + Label + " (" + UsedOutdoorCells.Count + " already claimed)");
				return null;
			}

			private bool IsCropRowCell(Cell Cell)
			{
				foreach (GameObject row in Survey.CropRows)
					if (GameObject.Validate(row) && row.CurrentCell == Cell) return true;
				return false;
			}

			/// <summary>One bare cell inside the heart's own rect, distinct from the rite.</summary>
			private Cell InHeartRect()
			{
				for (int y = HeartRect.Y1; y <= HeartRect.Y2; y++)
					for (int x = HeartRect.X1; x <= HeartRect.X2; x++)
					{
						if (x == RiteX && y == RiteY) continue;
						Cell cell = Zone.GetCell(x, y);
						if (cell != null && !UsedOutdoorCells.Contains(cell) && Bare(cell))
						{
							UsedOutdoorCells.Add(cell);
							return cell;
						}
					}
				Require(false, "no bare cell inside the heart's own rect for the plot fixture plant");
				return null;
			}

			private static bool Bare(Cell Cell)
			{
				if (Cell == null || !Cell.IsEmpty() || !Cell.IsPassable()
					|| Cell.HasOpenLiquidVolume()) return false;
				foreach (GameObject row in Cell.Objects)
					if (!GameObject.Validate(row) || row.IsCreature
						|| KingdomPlots.ReadObject(row) != KingdomPlotRules.GroundKind.Bare)
						return false;
				return true;
			}

			/// <summary>The camp's own canvas horseshoe wall, found rather than planted: the
			/// #107 founding transaction places it, and forage must leave it untouched.</summary>
			private GameObject FindCanvas()
			{
				for (int y = Math.Max(0, RiteY - KingdomMaterialRules.ForageRadius);
					y <= RiteY + KingdomMaterialRules.ForageRadius && y < Zone.Height; y++)
					for (int x = Math.Max(0, RiteX - KingdomMaterialRules.ForageRadius);
						x <= RiteX + KingdomMaterialRules.ForageRadius && x < Zone.Width; x++)
					{
						Cell cell = Zone.GetCell(x, y);
						if (cell == null) continue;
						foreach (GameObject item in cell.Objects)
							if (GameObject.Validate(item) && item.GetTag("BodyType") == "ClothWall")
								return item;
					}
				return null;
			}

			private GameObject Plant(string Blueprint, Cell Cell)
			{
				GameObject plant = GameObject.Create(Blueprint);
				Require(GameObject.Validate(plant), Blueprint + " blueprint produced no object");
				Require(plant.HasTag("Plant") && plant.HasTag("LivePlant") && !plant.IsWall()
					&& !plant.IsTakeable(), Blueprint + " fixture object is not a plain wild plant");
				Require(ReferenceEquals(Cell.AddObject(plant, NoStack: true), plant),
					"native placement substituted a fixture plant");
				return plant;
			}

			/// <summary>Physical brush standing in Container right now, counted by the same
			/// material recognition <c>MaterialStock.Tally</c> uses
			/// (<see cref="KingdomMaterials.TryMaterialOf"/>), but with no lease filter -- a
			/// direct census, never Tally-plus-arithmetic.</summary>
			private static int CensusBrushRaw(GameObject Container)
			{
				int total = 0;
				foreach (GameObject item in Container.Inventory.Objects)
					if (GameObject.Validate(item) && KingdomMaterials.TryMaterialOf(item, out var material)
						&& material == KingdomMaterial.Brush) total += item.Count;
				return total;
			}

			private static string EvidenceOf(GameObject Item)
			{
				return Item == null ? "-" : (Item.IDIfAssigned + "@(" + Item.CurrentCell?.X
					+ ',' + Item.CurrentCell?.Y + ')');
			}

			private static string EvidenceOf(GameObject[] Items)
			{
				if (Items == null || Items.Length == 0) return "-";
				string joined = EvidenceOf(Items[0]);
				for (int i = 1; i < Items.Length; i++) joined += "," + EvidenceOf(Items[i]);
				return joined;
			}
		}
	}
}
