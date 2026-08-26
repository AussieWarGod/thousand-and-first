using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free geometry for the settlement's building PLOTS: how big a plot of each tier is,
	/// which of them the settlement's stage may lay, where a rect goes on ground read through the
	/// layout grammar, what a lane between plots costs, where the heart is once the rite ground
	/// has drifted, which side the door is cut on, what clearing a cell earns, and what a refusal
	/// says.
	/// <para>
	/// A plot is the unit of building, grounded in vanilla's own village generator: huts 4-6
	/// square, common blocks 2-8, large 8-15, the rare ones larger still
	/// (<c>Village_InitialStructureSegmentation_*Default</c>). The four tiers below are those
	/// bands named. Not every plot is roofed &mdash; fields, yards, reservoirs, and salt-pans are
	/// <see cref="PlotSpec.Open"/>, and take the same rect discipline with no walls at all.
	/// </para>
	/// <para>
	/// Nothing here touches <c>XRL</c>. It never reads a cell, never places an object, and never
	/// destroys one; it answers questions about rectangles and hands back numbers and sentences.
	/// The engine-coupled half &mdash; surveying real ground, refusing real obstructions, raising
	/// real walls over stages &mdash; is <c>KingdomPlots</c>, in Growth/KingdomPlot2.cs.
	/// </para>
	/// <para>
	/// Migration honesty: none of this converts anything. A settlement that already stands is a
	/// scatter of single-cell furniture and stays exactly that; plots begin with the next thing
	/// built. Nothing existing moves, nothing is re-sited, and no old work is re-read as a plot.
	/// </para>
	/// </summary>
	public static partial class KingdomPlotRules
	{
		// --- Tier dimensions -------------------------------------------------------------

		public const int SmallWidth = 5;

		public const int SmallHeight = 4;

		public const int MediumWidth = 8;

		public const int MediumHeight = 6;

		public const int LargeWidth = 12;

		public const int LargeHeight = 9;

		public const int HugeWidth = 20;

		public const int HugeHeight = 14;

		/// <summary>The cells a plot of this tier occupies.</summary>
		/// <returns>False for <see cref="PlotSize.None"/> and for any value outside the tiers,
		/// in which case both outputs are zero.</returns>
		public static bool TryDimensions(PlotSize Size, out int Width, out int Height)
		{
			switch (Size)
			{
				case PlotSize.Small:
					Width = SmallWidth;
					Height = SmallHeight;
					return true;
				case PlotSize.Medium:
					Width = MediumWidth;
					Height = MediumHeight;
					return true;
				case PlotSize.Large:
					Width = LargeWidth;
					Height = LargeHeight;
					return true;
				case PlotSize.Huge:
					Width = HugeWidth;
					Height = HugeHeight;
					return true;
				default:
					Width = 0;
					Height = 0;
					return false;
			}
		}

		/// <summary>The rect a tier makes with its low corner at a given cell.</summary>
		/// <returns>False when <paramref name="Size"/> names no tier; <paramref name="Rect"/> is
		/// then a zero rect and means nothing.</returns>
		public static bool TryRectAt(int X, int Y, PlotSize Size, out PlotRect Rect)
		{
			Rect = default(PlotRect);
			if (!TryDimensions(Size, out var width, out var height))
			{
				return false;
			}
			Rect = new PlotRect(X, Y, X + width - 1, Y + height - 1);
			return true;
		}

		/// <summary>How the mod says a tier out loud.</summary>
		public static string SizeName(PlotSize Size)
		{
			switch (Size)
			{
				case PlotSize.Small:
					return "small";
				case PlotSize.Medium:
					return "middling";
				case PlotSize.Large:
					return "large";
				case PlotSize.Huge:
					return "great";
				default:
					return "";
			}
		}

		// --- Stage gating ----------------------------------------------------------------

		/// <summary>
		/// The largest plot a settlement of this stage lays. The city literally builds bigger as
		/// it grows, which composes with the district and tech gates without touching either.
		/// <para>
		/// Village shares Steading's ceiling on purpose. The brief names four tiers against four
		/// stages &mdash; Camp lays S, Steading M, Town L, City XL &mdash; and this mod has five;
		/// rather than invent a fifth tier or promote a stage the ruling did not promote, Village
		/// is the stage that consolidates instead of enlarging.
		/// </para>
		/// </summary>
		public static PlotSize MaxSizeForStage(GrowthStage Stage)
		{
			switch (Stage)
			{
				case GrowthStage.Camp:
					return PlotSize.Small;
				case GrowthStage.Steading:
				case GrowthStage.Village:
					return PlotSize.Medium;
				case GrowthStage.Town:
					return PlotSize.Large;
				default:
					return PlotSize.Huge;
			}
		}

		/// <summary>The earliest stage that lays a plot of this tier, for a refusal that names
		/// what would lift it.</summary>
		public static GrowthStage StageForSize(PlotSize Size)
		{
			switch (Size)
			{
				case PlotSize.Medium:
					return GrowthStage.Steading;
				case PlotSize.Large:
					return GrowthStage.Town;
				case PlotSize.Huge:
					return GrowthStage.City;
				default:
					return GrowthStage.Camp;
			}
		}

		/// <summary>Whether a settlement at <paramref name="Stage"/> may lay this tier.
		/// <see cref="PlotSize.None"/> is never allowed &mdash; it is not a plot.</summary>
		public static bool Allows(GrowthStage Stage, PlotSize Size)
		{
			if (Size == PlotSize.None)
			{
				return false;
			}
			return Size <= MaxSizeForStage(Stage);
		}
	}
}
