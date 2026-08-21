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
	public static class KingdomPlotRules
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
		}

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

		// --- Zone interior and the road budget -------------------------------------------

		/// <summary>
		/// Cells kept clear of plots at every edge of a zone, so the settlement's own ground never
		/// starts hard against the zone seam. On Qud's 80x25 surface zone this leaves the 76x21
		/// interior the plot budget is reckoned against.
		/// </summary>
		public const int ZoneMargin = 2;

		/// <summary>
		/// The lane a plot reserves on every side. Roads are never drawn in this mod &mdash; the
		/// gap between two plots' reserved rects IS the road, and it is what settler routes wear
		/// into a path later. One cell: a settlement wants to walk between its buildings, not to
		/// hold a parade.
		/// </summary>
		public const int RoadMargin = 1;

		/// <summary>
		/// The share of a zone's interior that stays lane, yard, and open ground rather than plot.
		/// Reckoned from the brief's own mature budget on an 80x25 zone &mdash; one XL, two or
		/// three L, four or five M, six to eight S is about six parts plot to four parts
		/// everything else &mdash; and it is what stops a settlement tiling itself solid.
		/// </summary>
		public const int RoadBudgetPercent = 40;

		/// <summary>The rect plots may be laid in, inset from every edge by
		/// <see cref="ZoneMargin"/>.</summary>
		/// <returns>False for a zone too small to have an interior at all.</returns>
		public static bool TryInterior(int Width, int Height, out PlotRect Interior)
		{
			Interior = default(PlotRect);
			if (Width <= ZoneMargin * 2 || Height <= ZoneMargin * 2)
			{
				return false;
			}
			Interior = new PlotRect(ZoneMargin, ZoneMargin, Width - 1 - ZoneMargin, Height - 1 - ZoneMargin);
			return true;
		}

		/// <summary>Whether <paramref name="Rect"/> lies wholly inside <paramref name="Bounds"/>.</summary>
		public static bool Fits(PlotRect Rect, PlotRect Bounds)
		{
			return Rect.X1 >= Bounds.X1 && Rect.Y1 >= Bounds.Y1 && Rect.X2 <= Bounds.X2 && Rect.Y2 <= Bounds.Y2;
		}

		/// <summary>The rect plus its reserved lane. Two plots may not overlap each other's
		/// reserved rects, which is the whole of the road rule.</summary>
		public static PlotRect Reserved(PlotRect Rect)
		{
			return new PlotRect(Rect.X1 - RoadMargin, Rect.Y1 - RoadMargin, Rect.X2 + RoadMargin, Rect.Y2 + RoadMargin);
		}

		/// <summary>Whether two rects share any cell.</summary>
		public static bool Overlaps(PlotRect A, PlotRect B)
		{
			return A.X1 <= B.X2 && B.X1 <= A.X2 && A.Y1 <= B.Y2 && B.Y1 <= A.Y2;
		}

		/// <summary>
		/// Whether a proposed plot would crowd any plot already laid: its own rect against every
		/// existing plot's RESERVED rect, so the lane between them survives whichever plot was
		/// laid first.
		/// </summary>
		public static bool CrowdsExisting(PlotRect Rect, IList<PlotRect> Existing)
		{
			if (Existing == null)
			{
				return false;
			}
			for (int i = 0; i < Existing.Count; i++)
			{
				if (Overlaps(Rect, Reserved(Existing[i])))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Plot cells a zone's interior may hold before the lanes are eaten.</summary>
		public static int PlotAreaAllowance(int Width, int Height)
		{
			if (!TryInterior(Width, Height, out var interior))
			{
				return 0;
			}
			return interior.Area * (100 - RoadBudgetPercent) / 100;
		}

		/// <summary>How much plot a zone already carries, for the budget check.</summary>
		public static int LaidArea(IList<PlotRect> Existing)
		{
			if (Existing == null)
			{
				return 0;
			}
			int area = 0;
			for (int i = 0; i < Existing.Count; i++)
			{
				area += Existing[i].Area;
			}
			return area;
		}

		/// <summary>
		/// Whether laying one more plot of this tier would spend more of the zone than
		/// <see cref="RoadBudgetPercent"/> leaves for plots. Checked before the ground is walked,
		/// so a founder is told the ground is full rather than watching a search fail silently.
		/// </summary>
		public static bool WouldExceedBudget(IList<PlotRect> Existing, PlotSize Size, int Width, int Height)
		{
			if (!TryDimensions(Size, out var plotWidth, out var plotHeight))
			{
				return false;
			}
			return LaidArea(Existing) + plotWidth * plotHeight > PlotAreaAllowance(Width, Height);
		}

		// --- The heart -------------------------------------------------------------------

		/// <summary>
		/// How heavily the rite ground counts against the works when the heart is reckoned. One:
		/// the rite ground is where the settlement started and never stops mattering, but a city
		/// of forty buildings has moved, and the heart moves with it.
		/// </summary>
		public const int RiteHeartWeight = 1;

		/// <summary>
		/// The settled heart, seeded at the rite ground and drifting toward the built centre.
		/// <para>
		/// With nothing built, the heart IS the rite ground &mdash; which is what gives the very
		/// first plot something to be sited against. Each work raised pulls the mean one work's
		/// worth toward itself, so the heart drifts rather than jumping: pour the rite in a corner
		/// and build across the zone, and the heart walks after the city over a dozen buildings.
		/// Walls are left out of the mean for the same reason
		/// <c>KingdomLayoutRules.TryHeart</c> leaves them out: a wall is by definition at the edge
		/// and would drag the centre out to it.
		/// </para>
		/// </summary>
		/// <param name="Marks">Everything the settlement has standing here.</param>
		/// <param name="HasRite">Whether the rite ground in this zone is known. A settlement
		/// founded before the rite ground was recorded simply has none, and the heart falls back
		/// to <c>KingdomLayoutRules.TryHeart</c> unchanged.</param>
		/// <param name="RiteX">Rite ground x; ignored when HasRite is false.</param>
		/// <param name="RiteY">Rite ground y; ignored when HasRite is false.</param>
		/// <returns>False when there is neither a rite ground nor any shape to read, in which case
		/// both outputs are zero and mean nothing.</returns>
		public static bool TryHeart(IList<KingdomLayoutRules.LayoutMark> Marks, bool HasRite, int RiteX, int RiteY, out int X, out int Y)
		{
			X = 0;
			Y = 0;
			if (!HasRite)
			{
				return KingdomLayoutRules.TryHeart(Marks, out X, out Y);
			}
			int sumX = RiteX * RiteHeartWeight;
			int sumY = RiteY * RiteHeartWeight;
			int count = RiteHeartWeight;
			if (Marks != null)
			{
				for (int i = 0; i < Marks.Count; i++)
				{
					if (Marks[i].Purpose == KingdomLayoutRules.LayoutPurpose.Defence)
					{
						continue;
					}
					sumX += Marks[i].X;
					sumY += Marks[i].Y;
					count++;
				}
			}
			X = (sumX + count / 2) / count;
			Y = (sumY + count / 2) / count;
			return true;
		}

		/// <summary>
		/// Extra penalty per cell of distance from the heart, by tier. A hut may stand anywhere its
		/// own quarter allows; a great plot wants the heart, which is why a heart full of early
		/// huts eventually has to be struck to make room for one. Small and middling plots add
		/// nothing here and are sited purely by the layout grammar's own pulls.
		/// </summary>
		public static int HeartPull(PlotSize Size)
		{
			switch (Size)
			{
				case PlotSize.Large:
					return 1;
				case PlotSize.Huge:
					return 3;
				default:
					return 0;
			}
		}

		// --- Siting ----------------------------------------------------------------------

		/// <summary>Whether any cell of a rect lies in the zone's frontier band.</summary>
		public static bool TouchesFrontier(PlotRect Rect, int Width, int Height, KingdomRules.Frontier Edges)
		{
			if (Edges == KingdomRules.Frontier.None)
			{
				return false;
			}
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
			{
				for (int x = Rect.X1; x <= Rect.X2; x++)
				{
					if (KingdomRules.IsOnFrontier(x, y, Width, Height, Edges))
					{
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// What the plan thinks of one rect. The layout grammar scores the rect's centre exactly
		/// as it scores a single cell &mdash; so a plot gathers with its own kind, thickens the
		/// civic ground, and rings out past the last roof for the same reasons a lone cask does
		/// &mdash; with two terms a rect needs and a cell does not:
		/// <list type="bullet">
		/// <item>a plot whose CORNER lands in the frontier band pays the frontier penalty even
		/// when its centre is clear of it, because a house on the wall is a house on the wall
		/// whichever cell you measure;</item>
		/// <item>a big plot is pulled toward the heart by <see cref="HeartPull"/>, because the
		/// great works want the settled centre and the huts do not.</item>
		/// </list>
		/// Higher is better, and a rect the plan has no complaint about scores zero.
		/// <para>
		/// The grammar's own terms are left exactly as they are for a single cell &mdash; this
		/// never reaches into <c>ScoreCell</c> to change what a cask or a hut wants. Only the
		/// heart-pull term added here knows about the rite ground, because only the great works
		/// care where the settlement started.
		/// </para>
		/// </summary>
		public static int ScoreRect(KingdomLayoutRules.LayoutPurpose Purpose, PlotSize Size, PlotRect Rect, int Width, int Height, KingdomRules.Frontier Edges, IList<KingdomLayoutRules.LayoutMark> Marks, bool HasRite, int RiteX, int RiteY)
		{
			int centerX = Rect.CenterX;
			int centerY = Rect.CenterY;
			int score = KingdomLayoutRules.ScoreCell(Purpose, centerX, centerY, Width, Height, Edges, Marks);
			if (Purpose != KingdomLayoutRules.LayoutPurpose.Defence
				&& !KingdomRules.IsOnFrontier(centerX, centerY, Width, Height, Edges)
				&& TouchesFrontier(Rect, Width, Height, Edges))
			{
				score -= KingdomLayoutRules.FrontierPenalty;
			}
			int pull = HeartPull(Size);
			if (pull > 0 && TryHeart(Marks, HasRite, RiteX, RiteY, out var heartX, out var heartY))
			{
				score -= KingdomLayoutRules.Chebyshev(centerX, centerY, heartX, heartY) * pull;
			}
			return score;
		}

		/// <summary>Chebyshev distance from a cell to the nearest cell of a rect; zero inside
		/// it. This is how near the founder is to a plot they are standing at the edge of.</summary>
		public static int Reach(PlotRect Rect, int X, int Y)
		{
			int dx = (X < Rect.X1) ? (Rect.X1 - X) : ((X > Rect.X2) ? (X - Rect.X2) : 0);
			int dy = (Y < Rect.Y1) ? (Rect.Y1 - Y) : ((Y > Rect.Y2) ? (Y - Rect.Y2) : 0);
			return (dx > dy) ? dx : dy;
		}

		/// <summary>
		/// Whether one candidate rect should be preferred to another: the plan's opinion first,
		/// then the founder's own feet, then position, so a run always returns the same ground for
		/// the same settlement. Mirrors <c>KingdomLayoutRules.Beats</c> exactly, on rects.
		/// </summary>
		public static bool Beats(int ScoreA, int ReachA, PlotRect A, int ScoreB, int ReachB, PlotRect B)
		{
			if (ScoreA != ScoreB)
			{
				return ScoreA > ScoreB;
			}
			if (ReachA != ReachB)
			{
				return ReachA < ReachB;
			}
			if (A.Y1 != B.Y1)
			{
				return A.Y1 < B.Y1;
			}
			return A.X1 < B.X1;
		}

		/// <summary>
		/// How near the founder a rect must come to count as their own ground. Two cells rather
		/// than the layout grammar's one: a founder cannot stand inside a plot that does not exist
		/// yet, so "where you stand" for a rect means "the plot you are standing at the edge of".
		/// </summary>
		public const int FounderReachCells = 2;

		/// <summary>
		/// Choose the ground for one plot out of the rects the caller says are clear.
		/// <para>
		/// The same bargain the cell grammar strikes, on rects: the founder's own ground is scored
		/// by the same rules as everything else and wins whenever it comes within
		/// <c>KingdomLayoutRules.FounderTolerance</c> of the plan's best, so the plan picks the
		/// quarter and the founder picks the spot. Where the plan has no opinion at all it says
		/// <see cref="KingdomLayoutRules.LayoutOutcome.Defer"/> and the caller sites the plot its
		/// own way &mdash; which, on empty ground, is where the founder is standing.
		/// </para>
		/// </summary>
		/// <param name="Purpose">What is being raised.</param>
		/// <param name="Size">Which tier, for the heart pull.</param>
		/// <param name="Width">Zone width in cells.</param>
		/// <param name="Height">Zone height in cells.</param>
		/// <param name="Edges">Edges of this zone facing unclaimed ground.</param>
		/// <param name="Marks">Everything the settlement already has standing here.</param>
		/// <param name="Candidates">Rects the caller will accept, in any order. Ties break toward
		/// the founder and then by position, never by the order of this list.</param>
		/// <param name="HasFounder">Whether the founder is standing in this zone.</param>
		/// <param name="FounderX">Founder cell x; ignored when HasFounder is false.</param>
		/// <param name="FounderY">Founder cell y; ignored when HasFounder is false.</param>
		/// <param name="HasRite">Whether the rite ground is known here.</param>
		/// <param name="RiteX">Rite ground x; ignored when HasRite is false.</param>
		/// <param name="RiteY">Rite ground y; ignored when HasRite is false.</param>
		/// <param name="Index">Index into <paramref name="Candidates"/> of the chosen rect, or -1
		/// when the result is <c>Defer</c> or <c>None</c>.</param>
		public static KingdomLayoutRules.LayoutOutcome ChooseRect(KingdomLayoutRules.LayoutPurpose Purpose, PlotSize Size, int Width, int Height, KingdomRules.Frontier Edges, IList<KingdomLayoutRules.LayoutMark> Marks, IList<PlotRect> Candidates, bool HasFounder, int FounderX, int FounderY, bool HasRite, int RiteX, int RiteY, out int Index)
		{
			Index = -1;
			if (Candidates == null || Candidates.Count == 0)
			{
				return KingdomLayoutRules.LayoutOutcome.None;
			}
			if (!KingdomLayoutRules.HasOpinion(Purpose, Marks, Edges))
			{
				return KingdomLayoutRules.LayoutOutcome.Defer;
			}
			int best = -1;
			int bestScore = 0;
			int bestReach = 0;
			int near = -1;
			int nearScore = 0;
			int nearReach = 0;
			for (int i = 0; i < Candidates.Count; i++)
			{
				PlotRect rect = Candidates[i];
				int score = ScoreRect(Purpose, Size, rect, Width, Height, Edges, Marks, HasRite, RiteX, RiteY);
				int reach = HasFounder ? Reach(rect, FounderX, FounderY) : 0;
				if (best < 0 || Beats(score, reach, rect, bestScore, bestReach, Candidates[best]))
				{
					best = i;
					bestScore = score;
					bestReach = reach;
				}
				if (HasFounder && reach <= FounderReachCells && (near < 0 || Beats(score, reach, rect, nearScore, nearReach, Candidates[near])))
				{
					near = i;
					nearScore = score;
					nearReach = reach;
				}
			}
			if (near >= 0 && nearScore >= bestScore - KingdomLayoutRules.FounderTolerance)
			{
				Index = near;
				return KingdomLayoutRules.LayoutOutcome.Founder;
			}
			Index = best;
			return KingdomLayoutRules.LayoutOutcome.Grammar;
		}

		/// <summary>
		/// Where the door is cut: the border cell nearest the heart that is not a corner, so a
		/// house faces the settlement it belongs to rather than the empty ground behind it. Ties
		/// break north-then-west, so the same plot always opens the same way.
		/// </summary>
		/// <returns>False for a rect too small to have a non-corner border cell (anything under
		/// three cells on both spans), in which case both outputs are zero.</returns>
		public static bool TryDoor(PlotRect Rect, int HeartX, int HeartY, out int X, out int Y)
		{
			X = 0;
			Y = 0;
			bool found = false;
			int bestDistance = 0;
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
			{
				for (int x = Rect.X1; x <= Rect.X2; x++)
				{
					if (!Rect.IsBorder(x, y) || Rect.IsCorner(x, y))
					{
						continue;
					}
					int distance = KingdomLayoutRules.Chebyshev(x, y, HeartX, HeartY);
					if (!found || distance < bestDistance)
					{
						found = true;
						bestDistance = distance;
						X = x;
						Y = y;
					}
				}
			}
			return found;
		}

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
			if (Underground || Open)
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
			long ticks = BaseTicks + (long)ClearEffort(Ground, Underground) * TicksPerEffort + EnclosureTicks(Rect, Underground, Open);
			return (ticks < 1L) ? 1L : ticks;
		}

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
		/// The stage a plot has reached. A long absence lands on the stage the elapsed time
		/// honestly bought and no further, so coming home after a hundred days finds the thing
		/// finished and coming home after one finds it framed.
		/// </summary>
		/// <param name="Elapsed">Ticks since the plot was staked. Negative reads as zero.</param>
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
		public static readonly string[] WallMaterials = new string[6] { "Limestone", "BrinestalkWall", "Fulcrete", "Marble", "Verdigris", "Foamcrete" };

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

		// --- Parsing ---------------------------------------------------------------------

		/// <summary>
		/// Reads one design's plot attributes. Every one of them is optional, and a design that
		/// declares none is not a plot at all &mdash; which is how every design that already
		/// exists keeps the single-cell path it has always had.
		/// </summary>
		/// <param name="Key">The design's key. Blank is refused.</param>
		/// <param name="Plot">Raw <c>Plot</c>: S, M, L, XL, or the long spellings. Absent means
		/// not a plot.</param>
		/// <param name="Open">Raw <c>Open</c>: an unroofed plot.</param>
		/// <param name="Sky">Raw <c>Sky</c>: needs weather, so refuses underground.</param>
		/// <param name="Contents">Raw <c>Contents</c>: population table the interior is furnished
		/// from.</param>
		/// <param name="Spec">The parsed spec, or null on failure.</param>
		/// <param name="Error">A log-facing reason, or null on success.</param>
		public static bool TryParsePlotAttributes(string Key, string Plot, string Open, string Sky, string Contents, out PlotSpec Spec, out string Error)
		{
			Spec = null;
			Error = null;
			if (string.IsNullOrWhiteSpace(Key))
			{
				Error = "plot attributes need a Key";
				return false;
			}
			if (!TryParseSize(Plot, out var size))
			{
				Error = "building " + Key + " has a bad Plot (want S, M, L, or XL)";
				return false;
			}
			if (!TryParseFlag(Open, out var open))
			{
				Error = "building " + Key + " has a bad Open (want Yes or No)";
				return false;
			}
			if (!TryParseFlag(Sky, out var sky))
			{
				Error = "building " + Key + " has a bad Sky (want Yes or No)";
				return false;
			}
			if (size == PlotSize.None && (open || sky || !string.IsNullOrWhiteSpace(Contents)))
			{
				Error = "building " + Key + " declares plot attributes without a Plot size; they would do nothing";
				return false;
			}
			Spec = new PlotSpec
			{
				Key = Key.Trim(),
				Size = size,
				Open = open,
				RequiresSky = sky,
				Contents = string.IsNullOrWhiteSpace(Contents) ? null : Contents.Trim()
			};
			return true;
		}

		/// <summary>Parses a tier. Absent is <see cref="PlotSize.None"/> and not an error;
		/// anything unrecognised is.</summary>
		public static bool TryParseSize(string Raw, out PlotSize Size)
		{
			Size = PlotSize.None;
			if (string.IsNullOrWhiteSpace(Raw))
			{
				return true;
			}
			switch (Raw.Trim().ToLowerInvariant())
			{
				case "s":
				case "small":
					Size = PlotSize.Small;
					return true;
				case "m":
				case "medium":
					Size = PlotSize.Medium;
					return true;
				case "l":
				case "large":
					Size = PlotSize.Large;
					return true;
				case "xl":
				case "huge":
					Size = PlotSize.Huge;
					return true;
				default:
					return false;
			}
		}

		/// <summary>Parses a yes/no attribute. Absent is false and not an error; anything
		/// unrecognised is, rather than quietly reading as no.</summary>
		public static bool TryParseFlag(string Raw, out bool Value)
		{
			Value = false;
			if (string.IsNullOrWhiteSpace(Raw))
			{
				return true;
			}
			switch (Raw.Trim().ToLowerInvariant())
			{
				case "yes":
				case "true":
				case "1":
					Value = true;
					return true;
				case "no":
				case "false":
				case "0":
					return true;
				default:
					return false;
			}
		}

		// --- Refusals (STANDARDS 7b: nothing stalls in silence) ---------------------------

		/// <summary>Names the thing standing in the way and where it stands. The one refusal the
		/// protection law makes unavoidable: the settlement will not take that ground, ever, so
		/// the founder has to be told which ground and why.</summary>
		public static string RefuseObstruction(string What, int X, int Y)
		{
			return "{{C|" + What + "}} stands at " + X + ", " + Y + ". The plot would have to take that ground, and nothing standing there is the settlement's to take. Clear it yourself, or stake the work elsewhere.";
		}

		/// <summary>Water refuses the plot and is never filled. The river is why the site was
		/// chosen.</summary>
		public static string RefuseLiquid(int X, int Y)
		{
			return "There is open water at " + X + ", " + Y + ". A plot is never laid over water, and the water is never filled in.";
		}

		/// <summary>Names the stage that would lift a tier gate.</summary>
		public static string RefuseStage(PlotSize Size, string SeatName, GrowthStage Stage)
		{
			return "A " + SizeName(Size) + " plot is the work of a " + StageForSize(Size).ToString().ToLowerInvariant()
				+ ". " + SeatName + " is a " + Stage.ToString().ToLowerInvariant() + " yet.";
		}

		/// <summary>Names a weather-dependent design refused underground.</summary>
		public static string RefuseSky(string Name)
		{
			return "The " + Name + " wants weather, and there is none under the rock. Raise it under open sky.";
		}

		/// <summary>No rect of this tier fits any clear ground here.</summary>
		public static string RefuseRoom(PlotSize Size)
		{
			return "There is no clear ground here wide enough for a " + SizeName(Size) + " plot and the lanes a settlement keeps around one.";
		}

		/// <summary>The zone is laid out to its budget: more plot would leave no road.</summary>
		public static string RefuseBudget(string SeatName)
		{
			return "This ground is laid out. What already stands at " + SeatName + ", and the lanes between, leave no room for another plot until something is struck.";
		}
	}
}
