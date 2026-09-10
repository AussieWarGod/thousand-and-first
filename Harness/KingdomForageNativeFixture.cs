using System;
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

			/// <summary>One real object per exclusion category, plus three eligible wild
			/// plants, all within forage radius of the rite (the in-plot plant sits inside the
			/// heart's own rect instead, which is a plot cell by construction).</summary>
			private void PlantExclusions()
			{
				Eligible = new[] { Plant("Plant", NearRite(1)), Plant("Plant", NearRite(2)),
					Plant("Plant", NearRite(3)) };
				Tree = Plant("Tree", NearRite(4));
				Owned = Plant("Plant", NearRite(5));
				Owned.GetPart<Physics>().Owner = "native-forage-fixture";
				Require(!string.IsNullOrEmpty(Owned.GetPart<Physics>().Owner),
					"the owned fixture plant did not publish an owner");
				Food = Plant("Yuckwheat", NearRite(6));
				Require(Food.HasPart("Harvestable"), "the food fixture plant carries no Harvestable part");
				Protected = Plant("Plant", NearRite(7));
				Protected.SetIntProperty("KingdomStores", 1);
				PlotPlant = Plant("Plant", InHeartRect());
			}

			/// <summary>One bare cell at exactly Offset paces east of the rite, inside the forage
			/// radius and outside the heart's own rect.</summary>
			private Cell NearRite(int Offset)
			{
				Cell cell = Zone.GetCell(RiteX + Offset, RiteY);
				Require(cell != null && Bare(cell), "no bare fixture cell at rite offset " + Offset);
				Require(KingdomMaterialRules.ForageDistance(cell.X, cell.Y, RiteX, RiteY)
					<= KingdomMaterialRules.ForageRadius, "fixture cell exceeds the forage radius");
				Require(cell.X < HeartRect.X1 || cell.X > HeartRect.X2
					|| cell.Y < HeartRect.Y1 || cell.Y > HeartRect.Y2,
					"fixture cell for an outdoor plant lies inside the heart's own rect");
				return cell;
			}

			/// <summary>One bare cell inside the heart's own rect, distinct from the rite.</summary>
			private Cell InHeartRect()
			{
				for (int y = HeartRect.Y1; y <= HeartRect.Y2; y++)
					for (int x = HeartRect.X1; x <= HeartRect.X2; x++)
					{
						if (x == RiteX && y == RiteY) continue;
						Cell cell = Zone.GetCell(x, y);
						if (cell != null && Bare(cell)) return cell;
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
