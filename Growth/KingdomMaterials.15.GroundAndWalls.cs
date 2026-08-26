using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomMaterials
	{

		// --- Reading the ground ---------------------------------------------------------------

		/// <summary>
		/// Whether one object standing in a rect stops the whole rect from being cleared, and
		/// why. The protection law in one method: the settlement's own works, the founder's own
		/// things, open water, and anything the mod cannot confidently name are all refusals.
		/// Creatures are not &mdash; a settler standing on the ground walks off it.
		/// </summary>
		/// <param name="Object">The object to judge.</param>
		/// <param name="Reason">A founder-facing sentence when this returns true.</param>
		public static bool IsProtected(GameObject Object, out string Reason)
		{
			Reason = null;
			if (Object == null || !GameObject.Validate(Object))
			{
				return false;
			}
			if (Object.IsCreature || Object.IsPlayer())
			{
				return false;
			}
			if (Object.GetPart<XRL.World.Parts.Physics>() == null)
			{
				return false;
			}
			if (Object.GetIntProperty("KingdomBuilt") == 1 || Object.GetIntProperty("KingdomCitizen") == 1
				|| Object.GetIntProperty("KingdomStores") == 1 || Object.GetIntProperty("KingdomLarder") == 1
				|| Object.GetIntProperty(StockpileProperty) == 1
				|| Object.HasPart("r_KingdomScaffold") || Object.HasPart("r_KingdomPlanMarker")
				|| Object.HasPart("r_KingdomPlot"))
			{
				Reason = "The " + Object.ShortDisplayName + " stands on that ground, and the settlement does not clear away its own. Strike it if you want the ground back.";
				return true;
			}
			XRL.World.Parts.LiquidVolume liquid = Object.GetPart<XRL.World.Parts.LiquidVolume>();
			if (liquid != null)
			{
				Reason = (liquid.MaxVolume < 0)
					? "There is open water on that ground. Water is an asset, not an obstacle, and the settlement will not fill it in."
					: ("The " + Object.ShortDisplayName + " stands on that ground, and it is yours, not the settlement's.");
				return true;
			}
			if (TryClassify(Object, out var kind))
			{
				return false;
			}
			if (Object.HasPart("HologramMaterial") || Object.IsDoor())
			{
				Reason = "The " + Object.ShortDisplayName + " on that ground is nothing the settlement knows how to take apart.";
				return true;
			}
			if (Object.IsTakeable() || Object.Inventory != null)
			{
				Reason = "The " + Object.ShortDisplayName + " lies on that ground, and nothing you put down is ever cleared away. Move it, and ask again.";
				return true;
			}
			if (Object.IsWall() || Object.HasTag("Wall"))
			{
				Reason = "The " + Object.ShortDisplayName + " stands on that ground, and the settlement cannot tell what it is made of.";
				return true;
			}
			return false;
		}

		/// <summary>
		/// What one object is worth clearing, by what it is made of. Reads vanilla's own
		/// vocabulary: the <c>Tree</c> and <c>Plant</c> tags, the <c>SemanticGeological</c> tag
		/// rock walls and boulders carry, the <c>Metal</c> part on every manufactured metal wall,
		/// and the <c>PaintedWall</c> and <c>BodyType</c> tags that tell marble from brinestalk
		/// from canvas.
		/// </summary>
		/// <param name="Object">The object to read.</param>
		/// <param name="Standing">Set on success.</param>
		/// <returns>False for anything that is not the settlement's to take &mdash; which is
		/// everything <see cref="IsProtected"/> refuses, plus everything nobody would call
		/// terrain.</returns>
		public static bool TryClassify(GameObject Object, out KingdomStanding Standing)
		{
			Standing = KingdomStanding.Nothing;
			if (Object == null || !GameObject.Validate(Object) || Object.IsCreature || Object.IsPlayer())
			{
				return false;
			}
			if (Object.HasPart("HologramMaterial") || Object.IsDoor())
			{
				return false;
			}
			if (Object.GetIntProperty("KingdomBuilt") == 1 || Object.HasPart("r_KingdomScaffold")
				|| Object.HasPart("r_KingdomPlanMarker") || Object.HasPart("r_KingdomPlot")
				|| Object.HasPart("r_KingdomClearance"))
			{
				return false;
			}
			if (Object.HasTag("Tree"))
			{
				Standing = KingdomStanding.Tree;
				return true;
			}
			bool isWall = Object.IsWall() || Object.HasTag("Wall");
			if (!isWall && (Object.HasTag("Plant") || Object.HasTag("LivePlant")))
			{
				Standing = KingdomStanding.Brush;
				return true;
			}
			if (Object.Blueprint == "Rubble" || Object.Blueprint == "Rubble Grey")
			{
				Standing = KingdomStanding.Rubble;
				return true;
			}
			bool geological = Object.HasTag("SemanticGeological");
			if (geological && !isWall && Object.IsTakeable())
			{
				// A boulder: naturally occurring, portable in principle, and the reason a rocky
				// site is a stone site.
				Standing = KingdomStanding.Rock;
				return true;
			}
			if (!isWall)
			{
				return false;
			}
			string painted = Object.GetTag("PaintedWall", "");
			string bodyType = Object.GetTag("BodyType", "");
			if (painted == "wall_marble")
			{
				Standing = KingdomStanding.MarbleSeam;
				return true;
			}
			if (Object.HasPart("Metal"))
			{
				Standing = KingdomStanding.Ruin;
				return true;
			}
			if (bodyType == "WoodWall" || painted == "wall_wood" || painted == "wall_wood_worn" || painted == "wall_brinestalk")
			{
				Standing = KingdomStanding.Tree;
				return true;
			}
			if (bodyType == "ClothWall" || bodyType == "PlantWall" || bodyType == "FungusWall" || painted == "wall_plant" || painted == "wall_mushroom")
			{
				Standing = KingdomStanding.Brush;
				return true;
			}
			if (geological || painted == "wall_rock" || painted == "wall_stone" || painted == "wall_brick" || painted == "wall_granite")
			{
				Standing = KingdomStanding.Rock;
				return true;
			}
			Standing = KingdomStanding.Ruin;
			return true;
		}

		/// <summary>The base Hitpoints a blueprint declares, which is how hard a thing is to bring
		/// down. Zero when the object carries no such stat.</summary>
		public static int BaseHitpoints(GameObject Object)
		{
			if (Object == null)
			{
				return 0;
			}
			Statistic stat = Object.GetStat("Hitpoints");
			return (stat != null) ? stat.BaseValue : 0;
		}

		/// <summary>Reads a tick stored as a string property. Zero for absent or unreadable,
		/// which every caller treats as "not stamped yet" rather than as an error.</summary>
		public static long ReadTick(GameObject Object, string Property)
		{
			string text = Object?.GetStringProperty(Property);
			if (string.IsNullOrEmpty(text) || !long.TryParse(text, out var tick) || tick < 0)
			{
				return 0L;
			}
			return tick;
		}

		/// <summary>Stamps a tick into a string property.</summary>
		public static void WriteTick(GameObject Object, string Property, long Tick)
		{
			Object?.SetStringProperty(Property, Tick.ToString());
		}

		private static bool Overlaps(r_KingdomClearance Order, int X1, int Y1, int X2, int Y2)
		{
			return Order.X1 <= X2 && Order.X2 >= X1 && Order.Y1 <= Y2 && Order.Y2 >= Y1;
		}

		// --- Wall material: the theme ---------------------------------------------------------

		/// <summary>
		/// The material this settlement's walls are made of, from its style's taste and what its
		/// own quarrying has actually earned it. Never fails: a settlement that has quarried
		/// nothing walls itself in mud, which is what a camp looks like.
		/// </summary>
		/// <param name="System">The kingdom, for its style. Null reads as styleless.</param>
		/// <param name="Z">The ground whose stockpiles are read. Null reads as empty.</param>
		public static KingdomMaterial WallMaterialFor(KingdomSystem System, Zone Z)
		{
			KingdomMaterial preferred = KingdomMaterial.Mud;
			bool hasPreference = System != null
				&& KingdomData.TryStyleWallMaterial(System.Style, out preferred);
			return KingdomMaterialRules.WallMaterialFor(Stock(Z).Tally,
				hasPreference, preferred);
		}

		/// <summary>
		/// The vanilla wall blueprint a material builds as, in this settlement's style. Every
		/// name here is one of vanilla's own settlement walls &mdash; the same vocabulary
		/// <c>Village_StructureWall_*Default</c> draws from &mdash; so a stamped plot reads as
		/// part of the world rather than as something the mod invented.
		/// </summary>
		/// <param name="Material">What the settlement has to build in.</param>
		/// <param name="Style">The city style key, or null.</param>
		/// <returns>A blueprint name; never null.</returns>
		public static string WallBlueprint(KingdomMaterial Material, string Style)
		{
			switch (Material)
			{
			case KingdomMaterial.Marble:
				return "Marble";
			case KingdomMaterial.ShapedStone:
				// Vanilla's own dressed-stone wall, and one of the six the game's village
				// generator already picks from (Village_StructureWall_*Default).
				return "Fulcrete";
			case KingdomMaterial.WorkedMetal:
				return "MetalWall";
			case KingdomMaterial.ShapedTimber:
				// Planks rather than stalks: a settlement with its own saw-pit builds in boards,
				// whatever grows nearby, so this one takes no style variant.
				return "WoodWall";
			case KingdomMaterial.Stone:
				return "Limestone";
			case KingdomMaterial.Scrap:
				return "Verdigris";
			case KingdomMaterial.Timber:
				return KingdomData.TimberWallForStyle(Style);
			case KingdomMaterial.Brush:
				return "CanvasWall";
			default:
				return "BrickWall";
			}
		}
	}
}
