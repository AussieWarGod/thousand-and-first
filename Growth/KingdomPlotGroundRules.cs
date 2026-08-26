using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPlotRules
	{
		// --- Clearance is extraction -----------------------------------------------------

		/// <summary>Effort of clearing one cell of each ground, in effort points. Bare ground is
		/// free; a marble seam is the slowest thing a settlement ever cuts.</summary>
		public static int ClearEffort(GroundKind Kind)
		{
			switch (Kind)
			{
				case GroundKind.Brush:
					return 1;
				case GroundKind.Ruins:
					return 3;
				case GroundKind.Trees:
					return 4;
				case GroundKind.Rock:
					return 6;
				case GroundKind.Marble:
					return 8;
				default:
					return 0;
			}
		}

		/// <summary>What clearing one cell of each ground puts in the stockpile. Effort scales
		/// with hardness and removal earns: this is the settlement's only standing source of
		/// timber, stone, and marble.</summary>
		public static Material YieldOf(GroundKind Kind, out int Amount)
		{
			switch (Kind)
			{
				case GroundKind.Brush:
					Amount = 1;
					return Material.Timber;
				case GroundKind.Trees:
					Amount = 6;
					return Material.Timber;
				case GroundKind.Rock:
					Amount = 4;
					return Material.Stone;
				case GroundKind.Marble:
					Amount = 3;
					return Material.Marble;
				case GroundKind.Ruins:
					Amount = 2;
					return Material.Scrap;
				default:
					Amount = 0;
					return Material.None;
			}
		}

		/// <summary>Whether a ground refuses the plot outright rather than being cleared.</summary>
		public static bool Refuses(GroundKind Kind)
		{
			return Kind == GroundKind.Liquid || Kind == GroundKind.Held;
		}

		/// <summary>
		/// The stratum a zone is in. The engine's own number, not ours:
		/// <c>Zone.GetTerrainDisplayName</c> answers "the deep underground" for any Z above ten,
		/// so ten is the surface and larger is deeper.
		/// </summary>
		public static bool IsUnderground(int ZLevel)
		{
			return ZLevel > KingdomRules.SurfaceZLevel;
		}

		/// <summary>
		/// How much harder clearing is underground, as a percent. Carving a plot out of rock costs
		/// twice what clearing the same ground in the open costs &mdash; and pays for it, because
		/// what comes out is stone and the surrounding rock is the enclosure.
		/// </summary>
		public const int UndergroundClearPercent = 200;

		/// <summary>Effort points per tick of raising, so the clearing a plot needs shows up in
		/// how long the plot takes rather than only in a number nobody sees.</summary>
		public const int TicksPerEffort = 100;

		/// <summary>Ticks a single cell of enclosure costs to raise. Zero underground and zero on
		/// an open plot, which is where the whole of the carve bargain lives.</summary>
		public const int TicksPerWallCell = 50;

		/// <summary>Total clearing effort for a surveyed rect.</summary>
		public static int ClearEffort(IList<GroundKind> Ground, bool Underground)
		{
			if (Ground == null)
			{
				return 0;
			}
			int effort = 0;
			for (int i = 0; i < Ground.Count; i++)
			{
				effort += ClearEffort(Ground[i]);
			}
			if (Underground)
			{
				effort = effort * UndergroundClearPercent / 100;
			}
			return effort;
		}

		/// <summary>How much of one material a surveyed rect yields when it comes down.</summary>
		public static int YieldFor(IList<GroundKind> Ground, Material Of)
		{
			if (Ground == null || Of == Material.None)
			{
				return 0;
			}
			int total = 0;
			for (int i = 0; i < Ground.Count; i++)
			{
				if (YieldOf(Ground[i], out var amount) == Of)
				{
					total += amount;
				}
			}
			return total;
		}

		/// <summary>Edge cells of a rect &mdash; the enclosure, on a roofed plot.</summary>
		public static int Perimeter(PlotRect Rect)
		{
			if (Rect.Width <= 1 || Rect.Height <= 1)
			{
				return Rect.Area;
			}
			return 2 * (Rect.Width + Rect.Height) - 4;
		}

		/// <summary>
		/// Ticks the enclosure costs. Free for an open plot, which has none, and free underground,
		/// where the rock the carving left IS the wall &mdash; the compensation for
		/// <see cref="UndergroundClearPercent"/>.
		/// </summary>
		public static long EnclosureTicks(PlotRect Rect, bool Underground, bool Open)
		{
			return EnclosureTicks(Rect, RoofOnGround(DefaultRoof(Open), Underground));
		}

		/// <summary>
		/// Ticks the enclosure costs, read off the roof the tier actually declared. Free for
		/// everything the settlement does not raise itself: an open plot has no enclosure, canvas
		/// is the design's own object, and underground the rock the carving left IS the wall
		/// &mdash; which is the compensation for <see cref="UndergroundClearPercent"/>.
		/// </summary>
		/// <param name="Rect">The FOOTPRINT, not the plot: walls go round the building, and the
		/// yard is the ground left outside them.</param>
		/// <param name="Roof">The roof state on the ground it is raised on, from
		/// <see cref="RoofOnGround"/>.</param>
		public static long EnclosureTicks(PlotRect Rect, RoofState Roof)
		{
			if (!RaisesWalls(Roof))
			{
				return 0L;
			}
			return (long)Perimeter(Rect) * TicksPerWallCell;
		}

		/// <summary>
		/// How long raising this plot on this ground takes: the design's own time, plus what
		/// clearing what stands there costs, plus the enclosure. Floors at one tick so a plot
		/// never completes in the same instant it is staked.
		/// </summary>
		public static long RaiseTicks(long BaseTicks, IList<GroundKind> Ground, PlotRect Rect, bool Underground, bool Open)
		{
			return RaiseTicks(BaseTicks, Ground, Rect, RoofOnGround(DefaultRoof(Open), Underground), Underground);
		}

		/// <summary>
		/// How long raising this tier on this ground takes: the design's own time, plus what
		/// clearing the whole PLOT costs, plus the enclosure round the FOOTPRINT. Staking wide is
		/// paid for in clearing and earned back in material and yard; the walls are only ever as
		/// long as the building is.
		/// </summary>
		/// <param name="Ground">Every cell of the plot, from the survey.</param>
		/// <param name="Footprint">The ground the building itself stands on.</param>
		/// <param name="Roof">The roof state on this ground, from <see cref="RoofOnGround"/>.</param>
		/// <param name="Underground">Whether the plot is carved rather than cleared.</param>
		public static long RaiseTicks(long BaseTicks, IList<GroundKind> Ground, PlotRect Footprint, RoofState Roof, bool Underground)
		{
			long ticks = BaseTicks + (long)ClearEffort(Ground, Underground) * TicksPerEffort + EnclosureTicks(Footprint, Roof);
			return (ticks < 1L) ? 1L : ticks;
		}
	}
}
