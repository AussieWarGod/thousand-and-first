using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPlotRules
	{
		// --- Stages ----------------------------------------------------------------------

		/// <summary>How far through the raising each stage lands, as a percent of the total.</summary>
		public static int StagePercent(PlotStage Stage)
		{
			switch (Stage)
			{
				case PlotStage.Cleared:
					return 25;
				case PlotStage.Frame:
					return 50;
				case PlotStage.Walls:
					return 75;
				case PlotStage.Done:
					return 100;
				default:
					return 0;
			}
		}

		/// <summary>
		/// The stage a plot has reached from completed work ticks. A long absence may supply elapsed
		/// time to the caller, but only labour converts that interval into the value passed here; an
		/// unstaffed frame therefore stays staked however old it is.
		/// </summary>
		/// <param name="Elapsed">Work ticks completed. Negative reads as zero. Legacy callers may
		/// still pass absolute elapsed ticks to preserve old in-flight saves.</param>
		/// <param name="Total">Ticks the whole raising takes. Zero or less reads as finished,
		/// because a raising with no duration has nothing left to do.</param>
		public static PlotStage StageAt(long Elapsed, long Total)
		{
			if (Total <= 0L)
			{
				return PlotStage.Done;
			}
			if (Elapsed <= 0L)
			{
				return PlotStage.Staked;
			}
			if (Elapsed >= Total)
			{
				return PlotStage.Done;
			}
			long percent = Elapsed * 100L / Total;
			if (percent >= StagePercent(PlotStage.Walls))
			{
				return PlotStage.Walls;
			}
			if (percent >= StagePercent(PlotStage.Frame))
			{
				return PlotStage.Frame;
			}
			if (percent >= StagePercent(PlotStage.Cleared))
			{
				return PlotStage.Cleared;
			}
			return PlotStage.Staked;
		}

		/// <summary>What the founder is told when a plot crosses into a stage. Null for
		/// <see cref="PlotStage.Staked"/>, which is announced by the staking itself, and for
		/// <see cref="PlotStage.Done"/>, which the raising ceremony tells.</summary>
		public static string StageLine(PlotStage Stage, string Name)
		{
			switch (Stage)
			{
				case PlotStage.Cleared:
					return "The ground for the " + Name + " is cleared.";
				case PlotStage.Frame:
					return "The frame of the " + Name + " stands.";
				case PlotStage.Walls:
					return "The walls of the " + Name + " are up.";
				default:
					return null;
			}
		}

		/// <summary>The word the ledger and a plan post use for a stage.</summary>
		public static string StageLabel(PlotStage Stage)
		{
			switch (Stage)
			{
				case PlotStage.Cleared:
					return "cleared";
				case PlotStage.Frame:
					return "framed";
				case PlotStage.Walls:
					return "walled";
				case PlotStage.Done:
					return "finished";
				default:
					return "staked";
			}
		}

		/// <summary>How many rolls of a design's contents table furnish a finished plot. A hut
		/// gets one thing worth walking in for; a great hall gets six.</summary>
		public static int ContentsRolls(PlotSize Size)
		{
			switch (Size)
			{
				case PlotSize.Small:
					return 1;
				case PlotSize.Medium:
					return 2;
				case PlotSize.Large:
					return 4;
				case PlotSize.Huge:
					return 6;
				default:
					return 0;
			}
		}

		// --- Wall material ---------------------------------------------------------------

		/// <summary>
		/// Wall blueprints a settlement builds in, which is vanilla's own list for its own
		/// villages (<c>Village_StructureWall_*Default</c>). Material is the theme: a settlement
		/// keeps one, and it is readable off the buildings without opening a menu.
		/// </summary>
		// Every blueprint WallBlueprint can actually return, so the guard test that walks this
		// list covers what the code does rather than what it did when the list was written.
		// MetalWall and WoodWall joined when the material chain gave them paving and a price.
		public static readonly string[] WallMaterials = new string[8] { "Limestone", "BrinestalkWall", "Fulcrete", "Marble", "Verdigris", "Foamcrete", "MetalWall", "WoodWall" };

		/// <summary>
		/// The wall a settlement builds in: its style's own material, unless it was founded in
		/// ruins, where the foamcrete already lying about is what gets reused. Deterministic and
		/// derived rather than stored, so it is the same answer every load and costs no serialized
		/// field.
		/// </summary>
		/// <param name="Style">The settlement's city style.</param>
		/// <param name="RegionName">The founding region, matched for ruins the way
		/// <c>KingdomRules</c> matches terrain elsewhere: by substring, case-insensitively.</param>
		public static string WallBlueprintFor(string Style, string RegionName)
		{
			if (!string.IsNullOrEmpty(RegionName) && RegionName.ToLowerInvariant().Contains("ruin"))
			{
				return "Foamcrete";
			}
			switch (Style)
			{
				case "verdant":
					return "BrinestalkWall";
				case "fungal":
					return "Fulcrete";
				case "gyre":
					return "Marble";
				case "eater":
					return "Verdigris";
				default:
					return "Limestone";
			}
		}
	}
}
