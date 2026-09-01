namespace ThousandAndFirst
{
	public static partial class KingdomRoadRules
	{
		// --- Paving ----------------------------------------------------------------------

		/// <summary>
		/// Legacy compatibility mapping from a settlement wall to its old paving surface.
		/// (<c>KingdomPlotRules.WallBlueprintFor</c>). Every one of these is a vanilla floor
		/// blueprint that already exists in <c>ZoneTerrain.xml</c>; the mod ships no path art of
		/// its own because each supported material already has a readable vanilla surface.
		/// </summary>
		/// <param name="WallBlueprint">New runtime orders use
		/// <see cref="KingdomRoadPaletteRules"/>; this public method remains stable for integrations
		/// and old callers. Unknown/null values fall back to the dirt path.</param>
		public static string PavedFloorFor(string WallBlueprint)
		{
			switch (WallBlueprint)
			{
				case "Marble":
					return "MarbleFloor";
				case "Black Marble":
					return "BlackMarbleWalkway";
				case "Limestone":
					return "SaltPath";
				case "BrinestalkWall":
					return "WoodFloor";
				case "Verdigris":
					return "GreenTile";
				case "Fulcrete":
				case "Foamcrete":
					return "FoamcreteFloor";
				// The two walls the material chain added (Addendum 7): a settlement that learned
				// to work metal or dress timber walks on what it makes, the same as every rung
				// above. Without these a settlement that built better paved in dirt.
				case "MetalWall":
					return "SmallHexFloor";
				case "WoodWall":
					return "WoodFloor";
				default:
					return "DirtPath";
			}
		}

		/// <summary>
		/// Legacy compatibility mapping from a settlement wall to old paving cost material.
		/// New runtime orders use <see cref="KingdomRoadPaletteRules"/>. Tied to the same wall
		/// blueprint the paving is laid as, so the cost is legible off the thing it buys: a
		/// marble city pays in marble, and a city of dressed limestone pays in cut stone.
		/// </summary>
		/// <param name="WallBlueprint">The settlement's wall blueprint.</param>
		/// <returns>The material, or <see cref="KingdomMaterial.Mud"/> for a wall this build does
		/// not know &mdash; which <see cref="CanPaveIn"/> then refuses, because paving a path in
		/// mud would be laying the ground on the ground.</returns>
		public static KingdomMaterial PaveMaterialFor(string WallBlueprint)
		{
			switch (WallBlueprint)
			{
				case "Marble":
				case "Black Marble":
					return KingdomMaterial.Marble;
				case "Limestone":
				case "Foamcrete":
					return KingdomMaterial.Stone;
				// The refined three, and the exact inverse of KingdomMaterials.WallBlueprint: a
				// settlement raises Fulcrete only out of dressed stone, MetalWall only out of
				// worked metal, WoodWall only out of dressed timber, so paving is priced in the
				// same material the wall beside it was. Before the chain existed Fulcrete was
				// priced as raw stone, which is what this pair corrects; MetalWall and WoodWall
				// were not priced at all and fell to Mud, which CanPaveIn refuses outright --
				// a settlement punished for having built better.
				case "Fulcrete":
					return KingdomMaterial.ShapedStone;
				case "MetalWall":
					return KingdomMaterial.WorkedMetal;
				case "WoodWall":
					return KingdomMaterial.ShapedTimber;
				case "Verdigris":
					return KingdomMaterial.Scrap;
				case "BrinestalkWall":
					return KingdomMaterial.Timber;
				default:
					return KingdomMaterial.Mud;
			}
		}

		/// <summary>Whether a material is something a path can be laid in at all. Mud is the
		/// ground; brush is not a floor.</summary>
		public static bool CanPaveIn(KingdomMaterial Material)
		{
			return Material != KingdomMaterial.Mud && Material != KingdomMaterial.Brush;
		}

		/// <summary>Units of material one paved cell costs.</summary>
		public const int PaveUnitsPerCell = 1;

		/// <summary>What paving a run of ground costs. Zero cells cost nothing, which is a
		/// refusal upstream rather than a free order.</summary>
		public static int PaveCost(int Cells)
		{
			return (Cells <= 0) ? 0 : Cells * PaveUnitsPerCell;
		}

		/// <summary>Cells one order covers: what is there, cut to
		/// <see cref="MaxPaveCellsPerOrder"/>.</summary>
		public static int PaveCells(int Available)
		{
			if (Available <= 0)
			{
				return 0;
			}
			return (Available > MaxPaveCellsPerOrder) ? MaxPaveCellsPerOrder : Available;
		}

		// --- Prose -----------------------------------------------------------------------

		/// <summary>
		/// What the ledger says the first time a settlement's own feet change the ground on a
		/// pass. Null for the rungs that say nothing: untouched ground is untouched, worn grass
		/// is not worth a line, and paving is announced by the order that bought it.
		/// </summary>
		/// <param name="State">The rung just reached.</param>
		/// <param name="SeatName">The settlement's name.</param>
		public static string WearLine(WearState State, string SeatName)
		{
			string seat = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			switch (State)
			{
				case WearState.Trodden:
					return "The grass between the works of {{C|" + seat + "}} is walked down to bare earth.";
				case WearState.Path:
					return "There are paths at {{C|" + seat + "}} now. Nobody laid them; they are only where people go.";
				default:
					return null;
			}
		}

		/// <summary>What the founder is told when a paving order lands.</summary>
		/// <param name="Cells">Cells paved.</param>
		/// <param name="Material">What they were paved in.</param>
		/// <param name="SeatName">The settlement's name.</param>
		public static string PavedLine(int Cells, KingdomMaterial Material, string SeatName)
		{
			string seat = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			return "{{W|" + Cells + ((Cells == 1) ? " cell" : " cells") + " of path}} at " + seat
				+ " are laid in " + KingdomMaterialRules.MaterialName(Material)
				+ ". The way people were already walking is the way the settlement is built now.";
		}

		/// <summary>The chronicle's own sentence for a paving.</summary>
		public static string PavedRecord(int Cells, KingdomMaterial Material, string KingdomName)
		{
			string realm = string.IsNullOrEmpty(KingdomName) ? "the settlement" : KingdomName;
			return "the ways worn through " + realm + " were laid in " + KingdomMaterialRules.MaterialName(Material)
				+ ", " + Cells + ((Cells == 1) ? " cell" : " cells") + " of them, exactly where the feet had already gone";
		}

		// --- Refusals (STANDARDS 7b: nothing stalls in silence) ---------------------------

		/// <summary>There is worn ground, but nothing walked hard enough to be worth laying.</summary>
		public static string RefuseNothingWorn(string SeatName)
		{
			string seat = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			return "Nothing at " + seat + " is walked hard enough to pave. A path is paved, never invented — let the place wear its own ways first.";
		}

		/// <summary>The settlement builds in something nothing can be paved with.</summary>
		public static string RefuseMaterialKind(KingdomMaterial Material)
		{
			return "This settlement builds in " + KingdomMaterialRules.MaterialName(Material)
				+ ", and you cannot pave ground with the ground. Quarry stone, cut timber, or bring back scrap, and the walls — and the paths — will follow.";
		}

		/// <summary>The stockpiles do not cover the order.</summary>
		public static string RefuseMaterial(KingdomMaterial Material, int Need, int Held)
		{
			return "Paving that much wants {{C|" + Need + "}} " + KingdomMaterialRules.MaterialName(Material)
				+ "; the stockpiles hold " + Held + ". Clear ground that has some in it, or trade for it.";
		}

		/// <summary>Nobody is free to lay it. Buildings are people, and so are roads.</summary>
		public static string RefuseHands(string SeatName)
		{
			string seat = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			return "There is nobody at " + seat + " free to lay it. Stand a settler down off the water or a work.";
		}

		/// <summary>Not the realm's ground.</summary>
		public static string RefuseNotOurGround()
		{
			return "Paths are paved on the kingdom's own claim, not in other people's yards.";
		}

		/// <summary>
		/// The tally is full: ground people are walking has stopped being recorded. Said once and
		/// not once a visit, and it names the thing that lifts it, because paving a path retires
		/// its cells from the tally and gives the rest of the settlement room to wear.
		/// </summary>
		public static string RefuseTallyFull(string SeatName)
		{
			string seat = string.IsNullOrEmpty(SeatName) ? "the settlement" : SeatName;
			return "{{r|" + seat + " has worn as much ground as its keepers can keep account of. Ways being walked now are not being recorded. Pave a path and the account has room again.}}";
		}
	}
}
