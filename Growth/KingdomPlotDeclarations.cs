using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPlotRules
	{
		/// <summary>
		/// How much ground one building takes. The tiers are vanilla's own size bands named: a
		/// hut, a common block, a large block, and the rare large structure.
		/// </summary>
		public enum PlotSize
		{
			/// <summary>Not a plot at all &mdash; a single-cell design, which keeps the old
			/// one-object-one-cell path untouched.</summary>
			None = 0,
			/// <summary>About 5x4. A hut, a yard, a cask shed.</summary>
			Small = 1,
			/// <summary>About 8x6. A house, a workshop, a field.</summary>
			Medium = 2,
			/// <summary>About 12x9. A hall, a market, a reservoir.</summary>
			Large = 3,
			/// <summary>About 20x14. One to a city, and it wants the heart.</summary>
			Huge = 4
		}

		/// <summary>
		/// The visible construction of a plot, in the order it happens. Every stage is something
		/// the founder can walk up to and look at, which is the whole point of raising a building
		/// over stages rather than swapping a scaffold for a hut in one tick.
		/// </summary>
		public enum PlotStage
		{
			/// <summary>Ground spoken for. Nothing is spent on the ground itself yet.</summary>
			Staked = 0,
			/// <summary>What stood here has come down, and what it was worth is in the stores.</summary>
			Cleared = 1,
			/// <summary>Posts at the corners. The shape of the thing is readable.</summary>
			Frame = 2,
			/// <summary>Walls up in the settlement's own material, floor laid, door cut.</summary>
			Walls = 3,
			/// <summary>Finished, furnished, and the settlement's.</summary>
			Done = 4
		}

		/// <summary>
		/// What one cell of a plot's ground is, which decides both what clearing it costs and
		/// what clearing it earns. Clearance IS extraction: removal is how a settlement with no
		/// mine gets timber, stone, and marble.
		/// </summary>
		public enum GroundKind
		{
			/// <summary>Open ground. Free, and worth nothing.</summary>
			Bare = 0,
			/// <summary>Scrub and grasses. Cheap, and worth a little.</summary>
			Brush = 1,
			/// <summary>Trees. Slow, and the settlement's only standing source of timber.</summary>
			Trees = 2,
			/// <summary>Shale, sandstone, granite. Slow, and yields stone.</summary>
			Rock = 3,
			/// <summary>A marble seam. Slowest rock there is, and the reason a marble house can
			/// ever be built.</summary>
			Marble = 4,
			/// <summary>Fulcrete, foamcrete, verdigris &mdash; somebody else's walls. Yields scrap
			/// metal.</summary>
			Ruins = 5,
			/// <summary>Open water. Never cleared, never filled, and refuses the plot outright:
			/// a river is an asset, not ground.</summary>
			Liquid = 6,
			/// <summary>Something the settlement may not take: anything the founder placed, any
			/// creature, any loose item, any work the settlement already holds, or anything this
			/// table cannot name. Refuses the plot and says what and where.</summary>
			Held = 7
		}

		/// <summary>What clearing a cell puts into the stockpile. Materials are never minted;
		/// they arrive by clearance, salvage, or trade.</summary>
		public enum Material
		{
			None = 0,
			Timber = 1,
			Stone = 2,
			Marble = 3,
			Scrap = 4
		}

		/// <summary>
		/// What stands over a building's own footprint. Declared by the design's TIER and never by
		/// the plot: the plot is an envelope of ground, and one envelope holds a tent under canvas
		/// this year and a stone house under a roof the next.
		/// </summary>
		public enum RoofState
		{
			/// <summary>No roof and no walls: a field, a salt-pan, a reservoir, a market square.</summary>
			Open = 0,

			/// <summary>Canvas, brinestalk, hide. Shelter enough to sleep under, standing as the
			/// design's own object rather than as walls the settlement raises, and it rolls back,
			/// so weather still reaches what is under it.</summary>
			Soft = 1,

			/// <summary>Walls in the settlement's own material, a floor, and a door.</summary>
			Walled = 2,

			/// <summary>Cut out of rock. What the carving left standing IS the enclosure, which is
			/// why nothing underground ever raises a wall.</summary>
			Carved = 3
		}

		/// <summary>An inclusive rectangle of cells. Both corners are part of the plot.</summary>
		public struct PlotRect
		{
			public int X1;

			public int Y1;

			public int X2;

			public int Y2;

			public PlotRect(int X1, int Y1, int X2, int Y2)
			{
				this.X1 = X1;
				this.Y1 = Y1;
				this.X2 = X2;
				this.Y2 = Y2;
			}

			public int Width => X2 - X1 + 1;

			public int Height => Y2 - Y1 + 1;

			public int Area => Width * Height;

			/// <summary>Cell the plan reads the rect's position from. Biased to the low corner on
			/// an even span so the answer never depends on rounding direction.</summary>
			public int CenterX => X1 + (Width - 1) / 2;

			/// <summary>See <see cref="CenterX"/>.</summary>
			public int CenterY => Y1 + (Height - 1) / 2;

			public bool Contains(int X, int Y)
			{
				return X >= X1 && X <= X2 && Y >= Y1 && Y <= Y2;
			}

			/// <summary>Whether a cell of this rect is one of its edge cells &mdash; where the
			/// walls go, on a roofed plot.</summary>
			public bool IsBorder(int X, int Y)
			{
				return Contains(X, Y) && (X == X1 || X == X2 || Y == Y1 || Y == Y2);
			}

			/// <summary>Whether a cell is one of the four corners, which never take a door.</summary>
			public bool IsCorner(int X, int Y)
			{
				return (X == X1 || X == X2) && (Y == Y1 || Y == Y2);
			}
		}

		/// <summary>
		/// What a design's plot attributes say about it. Registered per design key exactly the way
		/// zoning gates and upgrade chains are, so a third-party file that re-declares a key owns
		/// that design's whole plot spec &mdash; including a re-declaration that drops the
		/// attributes, which correctly un-plots the design back to the single-cell path.
		/// </summary>
		public sealed class PlotSpec
		{
			/// <summary>The design key this spec belongs to.</summary>
			public string Key;

			/// <summary><see cref="PlotSize.None"/> for an ordinary single-cell design.</summary>
			public PlotSize Size;

			/// <summary>True for a plot that is never roofed: a field, a yard, a salt-pan, a
			/// reservoir. Same rect discipline, no walls, no door.</summary>
			public bool Open;

			/// <summary>True for a design that needs weather &mdash; sun, wind, rain. Refused
			/// underground by name rather than silently sited somewhere useless.</summary>
			public bool RequiresSky;

			/// <summary>Population table the finished interior is furnished from, the way vanilla
			/// huts populate. Null furnishes nothing, which is correct for an open plot.</summary>
			public string Contents;

			/// <summary>
			/// Cells across that the design's own tier takes inside the plot. Zero means the tier
			/// declared no footprint of its own and fills the plot, which is what every design
			/// written before footprints existed reads as, and why not one of them changed.
			/// </summary>
			public int FootprintWidth;

			/// <summary>Cells down. See <see cref="FootprintWidth"/>.</summary>
			public int FootprintHeight;

			/// <summary>What stands over the footprint. When <see cref="RoofDeclared"/> is false
			/// this is the derived default and the design has made no claim of its own.</summary>
			public RoofState Roof;

			/// <summary>
			/// Whether the tier declared a roof state at all. The weather gate reads this rather
			/// than <see cref="Roof"/>: a design that never claimed a roof is raised exactly as it
			/// always was, and only a tier that declares itself walled can contradict a design
			/// that needs sky.
			/// </summary>
			public bool RoofDeclared;

			/// <summary>True when the tier takes the whole plot and there is no yard.</summary>
			public bool FillsPlot => FootprintWidth < 1 || FootprintHeight < 1;
		}
	}
}
