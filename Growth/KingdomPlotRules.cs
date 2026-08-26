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

		// --- Roofs -----------------------------------------------------------------------

		/// <summary>How the mod says a roof state out loud.</summary>
		public static string RoofWord(RoofState Roof)
		{
			switch (Roof)
			{
				case RoofState.Open:
					return "open to the sky";
				case RoofState.Soft:
					return "under canvas";
				case RoofState.Carved:
					return "carved from the rock";
				default:
					return "walled";
			}
		}

		/// <summary>
		/// How much weather a roof state keeps off. Rock and raised wall shelter alike, so they
		/// share a rank rather than being ordered by the enum: nothing anywhere reads
		/// <see cref="RoofState"/> ordinally, and a comparison that did would quietly decide a
		/// carved chamber is better shelter than a house.
		/// </summary>
		public static int ShelterRank(RoofState Roof)
		{
			switch (Roof)
			{
				case RoofState.Open:
					return 0;
				case RoofState.Soft:
					return 1;
				default:
					return 2;
			}
		}

		/// <summary>The shelter a bed asks for. A settler sleeps under canvas and does not sleep
		/// in a field, which is the whole of the tent's argument for existing.</summary>
		public const int BedShelter = 1;

		/// <summary>Whether anyone would sleep under this roof.</summary>
		public static bool HoldsBeds(RoofState Roof)
		{
			return ShelterRank(Roof) >= BedShelter;
		}

		/// <summary>Whether weather reaches what stands under this roof. Wall and rock do not
		/// admit it; canvas does, because canvas rolls back.</summary>
		public static bool AdmitsSky(RoofState Roof)
		{
			return Roof == RoofState.Open || Roof == RoofState.Soft;
		}

		/// <summary>Whether the settlement raises an enclosure of its own here. Only
		/// <see cref="RoofState.Walled"/> does: canvas is the design's own object, rock is the
		/// hill's, and an open plot has none.</summary>
		public static bool RaisesWalls(RoofState Roof)
		{
			return Roof == RoofState.Walled;
		}

		/// <summary>Whether anything stands around this footprint at all, ours or the hill's.
		/// This is the roofed test, and <see cref="RoofFromEnclosure"/> is how a structure the
		/// founder built by hand answers it.</summary>
		public static bool Encloses(RoofState Roof)
		{
			return Roof == RoofState.Walled || Roof == RoofState.Carved;
		}

		/// <summary>
		/// The roof a design actually gets on the ground it is raised on. Underground, everything
		/// the settlement would otherwise have enclosed is carved instead: there is no weather to
		/// keep off, no wall worth raising, and the rock is already all four sides.
		/// <para>
		/// <b>An open plot is the exception, and it is not a special case.</b> Carving replaces
		/// the enclosure a design would have raised; it does not roof ground the design
		/// deliberately left unroofed. A field, a salt-pan, a market square or a reservoir taken
		/// underground is a field, a salt-pan, a market square or a reservoir cut into the rock
		/// &mdash; open ground with stone around it, not a sealed chamber. Forcing those to
		/// <see cref="RoofState.Carved"/> quietly made them shelter
		/// (<see cref="HoldsBeds"/> is true of carved and false of open), floored their whole rect
		/// and cut a door into ground that has no inside, and contradicted the measured half of
		/// the same rule &mdash; <see cref="RoofFromEnclosure"/> has always read unbounded ground
		/// underground as open. The two now agree, which is the invariant worth having: what the
		/// settlement declares and what the walls prove answer the same question the same way.
		/// </para>
		/// </summary>
		public static RoofState RoofOnGround(RoofState Declared, bool Underground)
		{
			if (!Underground || Declared == RoofState.Open)
			{
				return Declared;
			}
			return RoofState.Carved;
		}

		/// <summary>The roof state a tier that declares none reads as: an open plot is open and
		/// everything else is walled, which is exactly what every design written before footprints
		/// existed already got.</summary>
		public static RoofState DefaultRoof(bool Open)
		{
			return Open ? RoofState.Open : RoofState.Walled;
		}

		/// <summary>
		/// What a structure the founder raised themselves has over it, measured rather than
		/// declared. The adoption enclosure fill IS the roofed test
		/// (<see cref="KingdomAdoptRules.MeasureEnclosure"/>), and this is the only place its
		/// verdict is turned into a roof state, so the two can never drift apart.
		/// <para>
		/// A soft roof is never measured: canvas is not a wall and the fill runs straight past it,
		/// so a tent somebody pitched by hand honestly reads open. Soft is a thing a design
		/// declares about itself, never a thing walls prove.
		/// </para>
		/// </summary>
		public static RoofState RoofFromEnclosure(KingdomAdoptRules.EnclosureMeasurement Enclosure, bool Underground)
		{
			if (!Enclosure.Bounded)
			{
				return RoofState.Open;
			}
			return Underground ? RoofState.Carved : RoofState.Walled;
		}

		/// <summary>
		/// Whether a roof is enough for what a role needs: somewhere to sleep wants canvas at
		/// least, a work wants something around it, and a cask stands wherever it is put.
		/// Adoption and the catalogue ask the same question of the same table.
		/// </summary>
		public static bool RoofMeetsRole(KingdomAdoptRules.RoleKind Role, RoofState Roof)
		{
			switch (Role)
			{
				case KingdomAdoptRules.RoleKind.Housing:
					return HoldsBeds(Roof);
				case KingdomAdoptRules.RoleKind.Storage:
					return true;
				default:
					return Encloses(Roof);
			}
		}

		// --- Footprints: the building's own ground, inside the plot -----------------------

		/// <summary>
		/// The invariant the two layers owe each other, and the only one: a tier's footprint fits
		/// inside its plot. Checked when the catalogue loads and again, by name, at the moment an
		/// improvement would grow past it.
		/// </summary>
		public static bool FootprintFits(PlotSize Plot, int Width, int Height)
		{
			if (Width < 1 || Height < 1)
			{
				return false;
			}
			return TryDimensions(Plot, out var plotWidth, out var plotHeight) && Width <= plotWidth && Height <= plotHeight;
		}

		/// <summary>The cells a design's tier takes, which is the whole plot for a tier that
		/// declares no footprint of its own.</summary>
		/// <returns>False for a null spec and for one that is not a plot at all.</returns>
		public static bool TryFootprint(PlotSpec Spec, out int Width, out int Height)
		{
			Width = 0;
			Height = 0;
			if (Spec == null)
			{
				return false;
			}
			if (Spec.FillsPlot)
			{
				return TryDimensions(Spec.Size, out Width, out Height);
			}
			Width = Spec.FootprintWidth;
			Height = Spec.FootprintHeight;
			return true;
		}

		/// <summary>The smallest tier of plot that holds a footprint, or
		/// <see cref="PlotSize.None"/> when nothing a settlement lays does.</summary>
		public static PlotSize SmallestPlotFor(int Width, int Height)
		{
			PlotSize[] order = new PlotSize[4] { PlotSize.Small, PlotSize.Medium, PlotSize.Large, PlotSize.Huge };
			for (int i = 0; i < order.Length; i++)
			{
				if (FootprintFits(order[i], Width, Height))
				{
					return order[i];
				}
			}
			return PlotSize.None;
		}

		/// <summary>
		/// Where the building sits inside its plot: the position whose centre lies nearest the
		/// heart, so a building fronts the settlement and its yard lies behind it, and so a tier
		/// that grows eats the yard from the street inwards rather than jumping across the plot.
		/// Ties break north-then-west, exactly as <see cref="TryDoor"/> does, which is what makes
		/// the same plot lay out the same way every time it is read.
		/// </summary>
		/// <returns>False when the footprint is larger than the plot on either span, in which
		/// case <paramref name="Footprint"/> is a zero rect and means nothing.</returns>
		public static bool TryFootprintWithin(PlotRect Plot, int Width, int Height, int HeartX, int HeartY, out PlotRect Footprint)
		{
			Footprint = default(PlotRect);
			if (Width < 1 || Height < 1 || Width > Plot.Width || Height > Plot.Height)
			{
				return false;
			}
			bool found = false;
			int bestDistance = 0;
			for (int y = Plot.Y1; y + Height - 1 <= Plot.Y2; y++)
			{
				for (int x = Plot.X1; x + Width - 1 <= Plot.X2; x++)
				{
					PlotRect rect = new PlotRect(x, y, x + Width - 1, y + Height - 1);
					int distance = KingdomLayoutRules.Chebyshev(rect.CenterX, rect.CenterY, HeartX, HeartY);
					if (!found || distance < bestDistance)
					{
						found = true;
						bestDistance = distance;
						Footprint = rect;
					}
				}
			}
			return found;
		}

		/// <summary>Whether a footprint lies wholly inside a plot.</summary>
		public static bool Within(PlotRect Plot, PlotRect Footprint)
		{
			return Footprint.X1 >= Plot.X1 && Footprint.Y1 >= Plot.Y1
				&& Footprint.X2 <= Plot.X2 && Footprint.Y2 <= Plot.Y2;
		}

		// --- The yard: plot minus footprint, recomputed per tier ---------------------------

		/// <summary>
		/// The yard, as up to four rectangles: everything inside the plot the building does not
		/// stand on, north band and south band full width, then the two side bands beside the
		/// footprint. Recomputed from the CURRENT tier, so a building that grows takes its yard
		/// back a band at a time rather than the yard being a thing stored anywhere.
		/// </summary>
		/// <returns>An empty list for a footprint that fills its plot, and for one that is not
		/// inside the plot at all, which has no yard anybody can name.</returns>
		public static List<PlotRect> YardBands(PlotRect Plot, PlotRect Footprint)
		{
			List<PlotRect> bands = new List<PlotRect>();
			if (!Within(Plot, Footprint))
			{
				return bands;
			}
			if (Footprint.Y1 > Plot.Y1)
			{
				bands.Add(new PlotRect(Plot.X1, Plot.Y1, Plot.X2, Footprint.Y1 - 1));
			}
			if (Footprint.Y2 < Plot.Y2)
			{
				bands.Add(new PlotRect(Plot.X1, Footprint.Y2 + 1, Plot.X2, Plot.Y2));
			}
			if (Footprint.X1 > Plot.X1)
			{
				bands.Add(new PlotRect(Plot.X1, Footprint.Y1, Footprint.X1 - 1, Footprint.Y2));
			}
			if (Footprint.X2 < Plot.X2)
			{
				bands.Add(new PlotRect(Footprint.X2 + 1, Footprint.Y1, Plot.X2, Footprint.Y2));
			}
			return bands;
		}

		/// <summary>Cells of yard a tier leaves. Zero for a tier that fills its plot.</summary>
		public static int YardArea(PlotRect Plot, PlotRect Footprint)
		{
			List<PlotRect> bands = YardBands(Plot, Footprint);
			int area = 0;
			for (int i = 0; i < bands.Count; i++)
			{
				area += bands[i].Area;
			}
			return area;
		}

		/// <summary>Whether a cell is yard: inside the plot, and not under the building.</summary>
		public static bool InYard(PlotRect Plot, PlotRect Footprint, int X, int Y)
		{
			return Plot.Contains(X, Y) && !Footprint.Contains(X, Y);
		}

		/// <summary>
		/// Whether growing from one tier to the next takes ground the old one did not stand on.
		/// False for a tier that grows into the same rect, which stamps nothing and can never be
		/// refused for want of room.
		/// </summary>
		public static bool TakesNewGround(PlotRect Old, PlotRect Grown)
		{
			for (int y = Grown.Y1; y <= Grown.Y2; y++)
			{
				for (int x = Grown.X1; x <= Grown.X2; x++)
				{
					if (!Old.Contains(x, y))
					{
						return true;
					}
				}
			}
			return false;
		}

		// --- Staking foresight: the whole chain's ground, before the stake goes in ---------

		/// <summary>
		/// One tier of a design's improvement chain, reduced to what the founder needs to see
		/// before choosing how much ground to stake: what it is called, and how much of the plot
		/// it will stand on when the settlement gets that far.
		/// </summary>
		public readonly struct ChainStep
		{
			public readonly string Key;

			public readonly string Name;

			public readonly int Width;

			public readonly int Height;

			public readonly RoofState Roof;

			public ChainStep(string Key, string Name, int Width, int Height, RoofState Roof)
			{
				this.Key = Key;
				this.Name = Name;
				this.Width = Width;
				this.Height = Height;
				this.Roof = Roof;
			}

			public int Area => Width * Height;
		}

		/// <summary>Whether every tier of a chain stands on one plot of this tier.</summary>
		/// <param name="FirstUnfit">Index of the first tier that wants more ground than the plot
		/// holds, or -1 when they all fit. An empty chain fits everything.</param>
		public static bool ChainFits(PlotSize Plot, IList<ChainStep> Chain, out int FirstUnfit)
		{
			FirstUnfit = -1;
			if (Chain == null)
			{
				return true;
			}
			for (int i = 0; i < Chain.Count; i++)
			{
				if (!FootprintFits(Plot, Chain[i].Width, Chain[i].Height))
				{
					FirstUnfit = i;
					return false;
				}
			}
			return true;
		}

		/// <summary>The smallest plot that holds a whole chain, or <see cref="PlotSize.None"/>
		/// when no tier a settlement lays does.</summary>
		public static PlotSize SmallestPlotForChain(IList<ChainStep> Chain)
		{
			if (Chain == null || Chain.Count == 0)
			{
				return PlotSize.None;
			}
			int width = 0;
			int height = 0;
			for (int i = 0; i < Chain.Count; i++)
			{
				if (Chain[i].Width > width)
				{
					width = Chain[i].Width;
				}
				if (Chain[i].Height > height)
				{
					height = Chain[i].Height;
				}
			}
			return SmallestPlotFor(width, height);
		}

		/// <summary>
		/// The plot tiers a founder may actually choose to stake for a design: never smaller than
		/// the design's own declared plot or than its first tier needs, never larger than the
		/// settlement has grown into. The choice is the ceiling: stake big for room to grow, or
		/// tight for the yard trade sooner and take what that costs later.
		/// </summary>
		/// <returns>An empty list when the design is not a plot at all, when its own first tier
		/// fits no plot, or when the settlement is not yet a settlement enough to lay one.</returns>
		public static List<PlotSize> StakeableSizes(PlotSize Declared, GrowthStage Stage, IList<ChainStep> Chain)
		{
			List<PlotSize> sizes = new List<PlotSize>();
			PlotSize floor = Declared;
			if (Chain != null && Chain.Count > 0)
			{
				PlotSize needed = SmallestPlotFor(Chain[0].Width, Chain[0].Height);
				if (needed == PlotSize.None)
				{
					return sizes;
				}
				if (needed > floor)
				{
					floor = needed;
				}
			}
			if (floor == PlotSize.None)
			{
				return sizes;
			}
			PlotSize ceiling = MaxSizeForStage(Stage);
			for (int size = (int)floor; size <= (int)ceiling; size++)
			{
				sizes.Add((PlotSize)size);
			}
			return sizes;
		}

		/// <summary>How the mod says a rectangle's size out loud.</summary>
		public static string SpanWord(int Width, int Height)
		{
			return Width + " by " + Height;
		}

		/// <summary>The chain's ground, tier by tier, in the order the settlement will build
		/// it.</summary>
		/// <returns>Null for an empty chain, which has nothing to foresee.</returns>
		public static string ChainFootprintLine(IList<ChainStep> Chain)
		{
			if (Chain == null || Chain.Count == 0)
			{
				return null;
			}
			string line = null;
			for (int i = 0; i < Chain.Count; i++)
			{
				string piece = Chain[i].Name + " " + SpanWord(Chain[i].Width, Chain[i].Height);
				line = (line == null) ? piece : (line + ", then " + piece);
			}
			return line;
		}

		/// <summary>One line for one stakeable tier, as the founder reads it in the list: how big
		/// the ground is, how far up the chain it carries, and what is left over for a yard
		/// meanwhile.</summary>
		public static string StakeOptionLine(PlotSize Plot, IList<ChainStep> Chain)
		{
			if (!TryDimensions(Plot, out var width, out var height))
			{
				return null;
			}
			string ground = SizeName(Plot) + " ground, " + SpanWord(width, height);
			if (Chain == null || Chain.Count == 0)
			{
				return ground;
			}
			bool fits = ChainFits(Plot, Chain, out var unfit);
			if (unfit == 0)
			{
				return ground + ": too little ground for the work itself";
			}
			int yard = width * height - Chain[0].Area;
			return ground + (fits ? ": holds every tier" : (": holds as far as the " + Chain[unfit - 1].Name))
				+ ", " + yard + ((yard == 1) ? " cell" : " cells") + " of yard to begin with";
		}

		/// <summary>
		/// What the founder is told before the stake goes in: the ground they are about to claim,
		/// every tier that will ever stand on it, and where the ceiling falls if they stake tight.
		/// Foresight rather than a warning: staking tight is a real choice, not a mistake, and the
		/// sentence says so.
		/// </summary>
		public static string ForesightLine(PlotSize Plot, IList<ChainStep> Chain)
		{
			if (!TryDimensions(Plot, out var width, out var height) || Chain == null || Chain.Count == 0)
			{
				return null;
			}
			string line = "A " + SizeName(Plot) + " plot is " + SpanWord(width, height) + ". " + ChainFootprintLine(Chain) + ".";
			if (Chain.Count == 1)
			{
				return line + " It never grows: what it takes now is what it takes.";
			}
			if (ChainFits(Plot, Chain, out var unfit))
			{
				return line + " Every tier it grows into stands on this ground.";
			}
			PlotSize whole = SmallestPlotForChain(Chain);
			string ceiling = " The " + Chain[unfit].Name + " wants " + SpanWord(Chain[unfit].Width, Chain[unfit].Height)
				+ ", which this plot does not hold. "
				+ ((whole == PlotSize.None)
					? "No plot this settlement lays holds the whole chain."
					: ("A " + SizeName(whole) + " plot is the smallest that holds all of it."));
			return line + ceiling + " Stake larger ground for room to grow, or stake here and take"
				+ " the ceiling: what outgrows this plot waits until the ground is struck and staked again.";
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
		/// How heavily the rite ground counts against the works when the heart is reckoned with
		/// nothing standing on it. One: the rite ground is where the settlement started and never
		/// stops mattering, but a city of forty buildings has moved, and the heart moves with it.
		/// <para>
		/// This is the floor of the ladder, not the whole of it. Once the heart's own great work
		/// stands on that ground, <see cref="HeartWeightForRung"/> is what the rite ground counts
		/// for, and the settled centre is drawn back onto the monument as it rises.
		/// </para>
		/// </summary>
		public const int RiteHeartWeight = 1;

		// --- The heart's own ladder -------------------------------------------------------

		/// <summary>
		/// The four rungs of the heart, in order, by design key. The heart is ONE plot that grows
		/// with its rung &mdash; basin, then the waterstone laid around it, then the moot yard
		/// raised over that, then the great court raised around the yard &mdash; and each rung is
		/// built OVER the last rather than in place of it, so the ground reads as history.
		/// <para>
		/// Keys rather than an authored attribute, deliberately and for this wave only: the
		/// catalogue loader hands <c>KingdomPlots.RegisterSpec</c> a fixed set of attributes, and
		/// a fifth one is a change to the shared loader rather than to the heart. A third-party
		/// file re-declaring one of these keys owns that rung entirely (merge-by-key), which is
		/// how the ladder is retheme-able today; authoring a NEW rung wants the
		/// <c>Heart="yes"</c> attribute noted in the wave report.
		/// </para>
		/// </summary>
		public static readonly string[] HeartRungKeys = new string[4]
		{
			"heartbasin",
			"heartwaterstone",
			"heartmoot",
			"heartcourt"
		};

		/// <summary>Which rung of the heart a design key is, one-based.</summary>
		/// <returns>Zero for every design that is not the heart, which is all but four of
		/// them.</returns>
		public static int HeartRungOf(string Key)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return 0;
			}
			for (int i = 0; i < HeartRungKeys.Length; i++)
			{
				if (HeartRungKeys[i] == Key)
				{
					return i + 1;
				}
			}
			return 0;
		}

		/// <summary>The design key of one rung, one-based; null outside the ladder.</summary>
		public static string HeartKeyForRung(int Rung)
		{
			if (Rung < 1 || Rung > HeartRungKeys.Length)
			{
				return null;
			}
			return HeartRungKeys[Rung - 1];
		}

		/// <summary>
		/// The plot tier each rung stands on: S at the founding, then M, L, and XL. The same
		/// ladder the stage gate already climbs (<see cref="MaxSizeForStage"/>), which is why the
		/// heart needs no gate of its own &mdash; a settlement that cannot lay a great plot cannot
		/// raise the great court either, and is told so in the words it already knows.
		/// </summary>
		public static PlotSize HeartSizeForRung(int Rung)
		{
			switch (Rung)
			{
				case 1:
					return PlotSize.Small;
				case 2:
					return PlotSize.Medium;
				case 3:
					return PlotSize.Large;
				case 4:
					return PlotSize.Huge;
				default:
					return PlotSize.None;
			}
		}

		/// <summary>
		/// What the rite ground counts for when the heart is reckoned, by the rung standing on it.
		/// One at the basin &mdash; a tin bowl on bare ground is not a monument, and the heart
		/// still walks after the city, which is correct. Four, twelve, and forty as the great work
		/// rises, until the settled centre is drawn back onto it and the city visibly re-centres
		/// on the thing it built.
		/// <para>
		/// Qud's own shape: Ezra is described as an "archaeological and cultural outgrowth" of the
		/// Tomb of the Eaters &mdash; the village grew around the great work, not beside it.
		/// </para>
		/// </summary>
		public static int HeartWeightForRung(int Rung)
		{
			switch (Rung)
			{
				case 2:
					return 4;
				case 3:
					return 12;
				case 4:
					return 40;
				default:
					return RiteHeartWeight;
			}
		}

		/// <summary>
		/// The whole ground the heart is surveyed for at the founding rite: the final rung's plot,
		/// centred on the rite ground and slid whole until it lies inside the zone's interior.
		/// Nothing is claimed, spent, or reserved by this &mdash; it is the founder's ambition
		/// paced out, and every later rung is staked inside it.
		/// </summary>
		/// <returns>False for a zone with no interior to survey, in which case the settlement
		/// simply has no surveyed heart and every plot is sited exactly as it was before.</returns>
		public static bool TrySurveyedHeart(int RiteX, int RiteY, int Width, int Height, out PlotRect Survey)
		{
			Survey = default(PlotRect);
			if (!TryInterior(Width, Height, out var interior)
				|| !TryDimensions(HeartSizeForRung(HeartRungKeys.Length), out var surveyWidth, out var surveyHeight))
			{
				return false;
			}
			return TryCentred(interior, RiteX, RiteY, surveyWidth, surveyHeight, out Survey);
		}

		/// <summary>
		/// One rung's plot: a rect of that rung's tier, centred on the rite ground and slid whole
		/// until it lies inside the surveyed ground. The basin's own ground therefore stays inside
		/// every rung above it, which is what makes the rungs accrete rather than replace.
		/// </summary>
		/// <returns>False when the tier does not fit the surveyed ground at all.</returns>
		public static bool TryHeartRect(PlotRect Survey, int RiteX, int RiteY, PlotSize Size, out PlotRect Rect)
		{
			Rect = default(PlotRect);
			if (!TryDimensions(Size, out var width, out var height))
			{
				return false;
			}
			return TryCentred(Survey, RiteX, RiteY, width, height, out Rect);
		}

		/// <summary>
		/// A rect of the given span centred on a point and then slid &mdash; never shrunk &mdash;
		/// until it lies wholly inside Bounds. Deterministic: the same point and bounds always
		/// give the same rect.
		/// </summary>
		/// <returns>False when the span does not fit inside Bounds at all.</returns>
		public static bool TryCentred(PlotRect Bounds, int X, int Y, int Width, int Height, out PlotRect Rect)
		{
			Rect = default(PlotRect);
			if (Width < 1 || Height < 1 || Bounds.Width < Width || Bounds.Height < Height)
			{
				return false;
			}
			int x1 = X - (Width - 1) / 2;
			int y1 = Y - (Height - 1) / 2;
			if (x1 < Bounds.X1)
			{
				x1 = Bounds.X1;
			}
			if (y1 < Bounds.Y1)
			{
				y1 = Bounds.Y1;
			}
			if (x1 + Width - 1 > Bounds.X2)
			{
				x1 = Bounds.X2 - Width + 1;
			}
			if (y1 + Height - 1 > Bounds.Y2)
			{
				y1 = Bounds.Y2 - Height + 1;
			}
			Rect = new PlotRect(x1, y1, x1 + Width - 1, y1 + Height - 1);
			return true;
		}

		/// <summary>Cells two rects share; zero when they do not meet.</summary>
		public static int OverlapArea(PlotRect A, PlotRect B)
		{
			int x1 = (A.X1 > B.X1) ? A.X1 : B.X1;
			int y1 = (A.Y1 > B.Y1) ? A.Y1 : B.Y1;
			int x2 = (A.X2 < B.X2) ? A.X2 : B.X2;
			int y2 = (A.Y2 < B.Y2) ? A.Y2 : B.Y2;
			if (x1 > x2 || y1 > y2)
			{
				return 0;
			}
			return (x2 - x1 + 1) * (y2 - y1 + 1);
		}

		/// <summary>
		/// What the plan takes off a rect for standing in ground the heart is surveyed for. A
		/// PREFERENCE and never a refusal: the settlement will not volunteer to build there while
		/// clear ground is going, and the founder's own stake still beats the grammar anywhere,
		/// which is why this sits below <c>KingdomLayoutRules.FounderTolerance</c> (16) rather
		/// than above it &mdash; ground the founder is standing on still wins outright.
		/// </summary>
		public const int SurveyRepulsion = 12;

		/// <summary>
		/// The repulsion term itself, scaled by how much of the rect actually stands in surveyed
		/// ground, so a plot clipping one corner of the survey pays almost nothing and a plot
		/// squarely inside it pays the whole of <see cref="SurveyRepulsion"/>. Independent of
		/// tier on purpose: a hut in the heart's ground is as much in the way as a hall is.
		/// </summary>
		public static int SurveyPenalty(PlotRect Rect, PlotRect Survey)
		{
			int area = Rect.Area;
			if (area <= 0)
			{
				return 0;
			}
			int overlap = OverlapArea(Rect, Survey);
			if (overlap <= 0)
			{
				return 0;
			}
			return SurveyRepulsion * overlap / area;
		}

		/// <summary>
		/// The sentence a plot staked in surveyed heart ground carries from the moment it is
		/// staked, and forever after in its own description. Consent before cost, told up front:
		/// the ground is legal to build on, and the mark is the promise being made about it.
		/// </summary>
		public static string YieldingLine(string Name)
		{
			return "The " + Name + " is staked in the ground the heart was surveyed for, and is marked to yield: when the great work is called for this ground, this is what moves. Nothing is taken from it, and nothing is refused you for it.";
		}

		/// <summary>
		/// The same promise, read off the thing itself rather than heard once. Carried by
		/// <c>r_KingdomYielding</c> into the plot's own description, so consent given at placement
		/// is still legible a hundred days later.
		/// </summary>
		public const string YieldingMark = "Staked in the ground the heart was surveyed for. Marked to yield: when the great work is called for this ground, this is what moves.";

		/// <summary>
		/// The heart's next rung wants ground that something already laid is standing in. Named
		/// rather than quietly worked around, and named as a thing the founder can act on: the
		/// mark said this day would come.
		/// </summary>
		public static string RefuseHeartGround(string SuccessorName, string What)
		{
			return "The " + SuccessorName + " wants the surveyed ground, and the {{C|" + What
				+ "}} is standing in it. Nothing the settlement raised comes down on its own: clear it, and the heart can climb.";
		}

		/// <summary>
		/// What the founder is told at the rite, once, when the ground is paced out. Says the
		/// three things the mark is: how much ground, that it costs nothing, and that building
		/// inside it is allowed and marked.
		/// </summary>
		public static string SurveyLine(PlotRect Survey)
		{
			return "You pace out the ground while the water soaks in: {{C|" + Survey.Width + " by " + Survey.Height
				+ "}} cells around the basin, stakes at the corners. Nothing is claimed and nothing is spent: the settlement will simply build elsewhere while it can, and anything staked inside is marked to yield.";
		}

		/// <summary>
		/// A founder asking the settlement to raise one of the heart's own rungs somewhere else.
		/// There is one heart, standing where the water was poured, and it climbs by improvement
		/// rather than by being ordered a second time.
		/// </summary>
		public static string RefuseSecondHeart(string Name)
		{
			return "There is one heart at " + (string.IsNullOrEmpty(Name) ? "this settlement" : Name)
				+ ", and it stands on the ground the first water was poured on. It is not raised twice; it grows where it is.";
		}

		/// <summary>
		/// The blocker is a plot the founder was warned about at the moment they staked it. Said
		/// differently from the general case on purpose: the mark promised this day, and the
		/// founder is owed the promise being kept out loud &mdash; along with the honest truth
		/// about what the settlement can do about it TODAY, which is strike it and rebuild. Moving
		/// a plot whole is the ring call, and the ring call waits on the relocation verb.
		/// </summary>
		public static string RefuseHeartYielding(string SuccessorName, string What)
		{
			return "The {{C|" + What + "}} was marked to yield when it was staked, and the day it was marked for is here: the "
				+ SuccessorName + " wants that ground. Nothing carries a building whole yet, so it comes down and goes up again, or the heart waits. Neither happens on its own.";
		}

		/// <summary>The heart's next rung has no room inside the ground surveyed for it &mdash;
		/// a zone too small, or a rite poured against the edge of one.</summary>
		public static string RefuseHeartRoom(string SuccessorName)
		{
			return "The " + SuccessorName + " will not fit the ground surveyed at the rite. There is no room here for the heart to grow into.";
		}

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
		/// <param name="RiteWeight">How many votes the rite ground gets, from
		/// <see cref="HeartWeightForRung"/> once the heart's own great work stands on it. Clamped
		/// up to <see cref="RiteHeartWeight"/>, so no caller can vote the rite ground away.</param>
		/// <returns>False when there is neither a rite ground nor any shape to read, in which case
		/// both outputs are zero and mean nothing.</returns>
		public static bool TryHeart(IList<KingdomLayoutRules.LayoutMark> Marks, bool HasRite, int RiteX, int RiteY, out int X, out int Y, int RiteWeight = RiteHeartWeight)
		{
			X = 0;
			Y = 0;
			if (!HasRite)
			{
				return KingdomLayoutRules.TryHeart(Marks, out X, out Y);
			}
			int weight = (RiteWeight < RiteHeartWeight) ? RiteHeartWeight : RiteWeight;
			int sumX = RiteX * weight;
			int sumY = RiteY * weight;
			int count = weight;
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
		/// great works want the settled centre and the huts do not;</item>
		/// <item>a rect standing in ground the heart was surveyed for at the rite pays
		/// <see cref="SurveyPenalty"/> &mdash; a preference away from the great work's ground, and
		/// never a refusal of it.</item>
		/// </list>
		/// Higher is better, and a rect the plan has no complaint about scores zero.
		/// <para>
		/// The grammar's own terms are left exactly as they are for a single cell &mdash; this
		/// never reaches into <c>ScoreCell</c> to change what a cask or a hut wants. Only the
		/// heart-pull term added here knows about the rite ground, because only the great works
		/// care where the settlement started.
		/// </para>
		/// </summary>
		public static int ScoreRect(KingdomLayoutRules.LayoutPurpose Purpose, PlotSize Size, PlotRect Rect, int Width, int Height, KingdomRules.Frontier Edges, IList<KingdomLayoutRules.LayoutMark> Marks, bool HasRite, int RiteX, int RiteY, bool HasSurvey = false, PlotRect Survey = default(PlotRect), int RiteWeight = RiteHeartWeight)
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
			if (pull > 0 && TryHeart(Marks, HasRite, RiteX, RiteY, out var heartX, out var heartY, RiteWeight))
			{
				score -= KingdomLayoutRules.Chebyshev(centerX, centerY, heartX, heartY) * pull;
			}
			if (HasSurvey)
			{
				// The founder's ambition, paced out at the rite and standing in the ground as
				// stakes. The settlement reads it as a preference and nothing more: it will not
				// VOLUNTEER to build in the heart's ground while there is clear ground going, and
				// it never refuses to.
				score -= SurveyPenalty(Rect, Survey);
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
		public static KingdomLayoutRules.LayoutOutcome ChooseRect(KingdomLayoutRules.LayoutPurpose Purpose, PlotSize Size, int Width, int Height, KingdomRules.Frontier Edges, IList<KingdomLayoutRules.LayoutMark> Marks, IList<PlotRect> Candidates, bool HasFounder, int FounderX, int FounderY, bool HasRite, int RiteX, int RiteY, out int Index, bool HasSurvey = false, PlotRect Survey = default(PlotRect), int RiteWeight = RiteHeartWeight)
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
				int score = ScoreRect(Purpose, Size, rect, Width, Height, Edges, Marks, HasRite, RiteX, RiteY, HasSurvey, Survey, RiteWeight);
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

		// --- Parsing ---------------------------------------------------------------------

		/// <summary>
		/// Reads one design's plot attributes without a footprint or a roof, exactly as the
		/// schema read before tiers declared their own ground. Kept because it is supported API:
		/// a design read this way fills its plot and takes the walled default.
		/// </summary>
		public static bool TryParsePlotAttributes(string Key, string Plot, string Open, string Sky, string Contents, out PlotSpec Spec, out string Error)
		{
			return TryParsePlotAttributes(Key, Plot, Open, Sky, Contents, null, null, out Spec, out Error);
		}

		/// <summary>
		/// Reads one design's plot attributes, footprint and roof included. Every one of them is
		/// optional, and a design that declares none is not a plot at all &mdash; which is how
		/// every design that already exists keeps the single-cell path it has always had.
		/// <para>
		/// A design that declares a <c>Plot</c> and no <c>Footprint</c> fills that plot and is
		/// walled unless it is <c>Open</c>, which is exactly what it did before footprints
		/// existed: not one entry written against the old schema changes what it builds.
		/// </para>
		/// </summary>
		/// <param name="Key">The design's key. Blank is refused.</param>
		/// <param name="Plot">Raw <c>Plot</c>: S, M, L, XL, or the long spellings. Absent means
		/// not a plot.</param>
		/// <param name="Open">Raw <c>Open</c>: an unroofed plot.</param>
		/// <param name="Sky">Raw <c>Sky</c>: needs weather, so refuses underground and refuses a
		/// tier that declares itself walled.</param>
		/// <param name="Contents">Raw <c>Contents</c>: population table the interior is furnished
		/// from.</param>
		/// <param name="Footprint">Raw <c>Footprint</c>: <c>WxH</c>, the ground this TIER stands
		/// on inside the plot. Absent fills the plot. Larger than the plot is refused here and
		/// again by the whole-catalogue validator, which is the one that sees the merged value.
		/// </param>
		/// <param name="Roof">Raw <c>Roof</c>: Open, Soft, Walled, or Carved.</param>
		/// <param name="Spec">The parsed spec, or null on failure.</param>
		/// <param name="Error">A log-facing reason, or null on success.</param>
		public static bool TryParsePlotAttributes(string Key, string Plot, string Open, string Sky, string Contents, string Footprint, string Roof, out PlotSpec Spec, out string Error)
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
			if (!TryParseFootprint(Footprint, out var footprintWidth, out var footprintHeight))
			{
				Error = "building " + Key + " has a bad Footprint (want WxH, as in 6x4)";
				return false;
			}
			if (!TryParseRoof(Roof, out var roof, out var roofDeclared))
			{
				Error = "building " + Key + " has a bad Roof (want Open, Soft, Walled, or Carved)";
				return false;
			}
			bool footprintDeclared = footprintWidth > 0 && footprintHeight > 0;
			if (size == PlotSize.None && (open || sky || footprintDeclared || roofDeclared || !string.IsNullOrWhiteSpace(Contents)))
			{
				Error = "building " + Key + " declares plot attributes without a Plot size; they would do nothing";
				return false;
			}
			bool openDeclared = !string.IsNullOrWhiteSpace(Open);
			if (roofDeclared && openDeclared && open != (roof == RoofState.Open))
			{
				Error = "building " + Key + " declares Open=" + (open ? "Yes" : "No") + " and a Roof of "
					+ roof.ToString().ToLowerInvariant() + ", which disagree";
				return false;
			}
			if (!roofDeclared)
			{
				roof = DefaultRoof(open);
			}
			if (footprintDeclared && !FootprintFits(size, footprintWidth, footprintHeight))
			{
				TryDimensions(size, out var plotWidth, out var plotHeight);
				Error = "building " + Key + " wants a footprint of " + SpanWord(footprintWidth, footprintHeight)
					+ " on a " + SizeName(size) + " plot, which is " + SpanWord(plotWidth, plotHeight)
					+ "; a footprint never outgrows its plot";
				return false;
			}
			Spec = new PlotSpec
			{
				Key = Key.Trim(),
				Size = size,
				Open = (roof == RoofState.Open),
				RequiresSky = sky,
				Contents = string.IsNullOrWhiteSpace(Contents) ? null : Contents.Trim(),
				FootprintWidth = footprintDeclared ? footprintWidth : 0,
				FootprintHeight = footprintDeclared ? footprintHeight : 0,
				Roof = roof,
				RoofDeclared = roofDeclared
			};
			return true;
		}

		/// <summary>
		/// Reads a footprint. Absent is "fills the plot" and not an error; anything the shape
		/// <c>WxH</c> cannot be read out of is, rather than quietly filling the plot, because a
		/// mistyped footprint that silently became the whole plot would move a building's walls
		/// without saying so.
		/// </summary>
		public static bool TryParseFootprint(string Raw, out int Width, out int Height)
		{
			Width = 0;
			Height = 0;
			if (string.IsNullOrWhiteSpace(Raw))
			{
				return true;
			}
			string[] parts = Raw.Trim().ToLowerInvariant().Split(FootprintSeparator);
			if (parts.Length != 2 || !int.TryParse(parts[0].Trim(), out var width) || !int.TryParse(parts[1].Trim(), out var height)
				|| width < 1 || height < 1)
			{
				return false;
			}
			Width = width;
			Height = height;
			return true;
		}

		/// <summary>Between the two spans of a footprint: <c>6x4</c>. Case-folded before the
		/// split, so <c>6X4</c> reads the same.</summary>
		public const char FootprintSeparator = 'x';

		/// <summary>Parses a roof state. Absent leaves <paramref name="Declared"/> false and the
		/// design making no claim about its roof, which is what every entry written before roofs
		/// existed does; anything unrecognised is an error rather than a silent walled default.
		/// </summary>
		public static bool TryParseRoof(string Raw, out RoofState Roof, out bool Declared)
		{
			Roof = RoofState.Walled;
			Declared = false;
			if (string.IsNullOrWhiteSpace(Raw))
			{
				return true;
			}
			switch (Raw.Trim().ToLowerInvariant())
			{
				case "open":
					Roof = RoofState.Open;
					break;
				case "soft":
				case "canvas":
					Roof = RoofState.Soft;
					break;
				case "walled":
				case "walls":
					Roof = RoofState.Walled;
					break;
				case "carved":
					Roof = RoofState.Carved;
					break;
				default:
					return false;
			}
			Declared = true;
			return true;
		}

		/// <summary>
		/// Whether a design that needs weather is contradicted by its own tier. Only a tier that
		/// DECLARES itself walled or carved contradicts it: a design that never claimed a roof has
		/// made no claim to contradict, and is raised exactly as it always was.
		/// </summary>
		public static bool RoofRefusesSky(PlotSpec Spec)
		{
			return Spec != null && Spec.RequiresSky && Spec.RoofDeclared && !AdmitsSky(Spec.Roof);
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

		/// <summary>
		/// The improvement wants more ground than the plot it stands on holds. Refused BY NAME
		/// rather than by silently siting the larger tier somewhere else or quietly shrinking it:
		/// the ceiling was a choice the founder made when they staked this ground, and this is the
		/// sentence that tells them the choice has arrived.
		/// </summary>
		/// <param name="Name">What would be raised.</param>
		/// <param name="Width">Cells across it wants.</param>
		/// <param name="Height">Cells down it wants.</param>
		/// <param name="Plot">The tier of plot it stands on.</param>
		public static string RefuseFootprint(string Name, int Width, int Height, PlotSize Plot)
		{
			string ground = TryDimensions(Plot, out var plotWidth, out var plotHeight)
				? ("a " + SizeName(Plot) + " plot is " + SpanWord(plotWidth, plotHeight))
				: "this ground is less than that";
			return "The {{C|" + Name + "}} wants more ground than this plot holds: it stands "
				+ SpanWord(Width, Height) + ", and " + ground
				+ ". Strike what is here and stake larger ground, or leave it as it is.";
		}

		/// <summary>A design that needs weather, refused a tier that has declared itself
		/// closed.</summary>
		public static string RefuseRoofSky(string Name, RoofState Roof)
		{
			return "The " + Name + " wants weather, and this tier of it is " + RoofWord(Roof)
				+ ". Raise it under something that lets the sky in.";
		}

		/// <summary>
		/// The grown building would stand on the cell a yard trade is worked in. Never taken down
		/// on its own: the founder is told which trade is in the way and chooses, because a
		/// household's sideline is theirs and the settlement does not tidy it away to make room.
		/// </summary>
		public static string RefuseYardWork(string Name, string SuccessorName, string WorkName)
		{
			return "The " + Name + " could be raised into " + KingdomUpgradeRules.Article(SuccessorName)
				+ ", but the {{C|" + WorkName + "}} in its yard stands on ground the larger building needs."
				+ " Let the trade go first, and the work can begin. Nothing in the yard comes down on its own.";
		}

		/// <summary>A design people are meant to sleep in, on a tier with nothing over it.</summary>
		public static string RefuseBedRoof(string Name)
		{
			return "Nobody sleeps in the open. The " + Name + " is " + RoofWord(RoofState.Open)
				+ ", and a bed wants canvas over it at the very least.";
		}
	}
}
