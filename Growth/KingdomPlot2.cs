using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;

// The engine resolves an XML <part Name="X"/> as the single type "XRL.World.Parts.X":
// GamePartBlueprint.Namespace defaults to that string (GamePartBlueprint.cs:178) and
// T => ModManager.ResolveType(Namespace, Name) (:240) tries only that one name.
// ModManager.ResolveType's doc comment promises a bare-TypeID fallback, but the code
// (ModManager.cs:307-321) does not do it. So a part named in XML MUST live in this
// namespace or the object is built without it, silently. Only the part moves; the
// settlement-side geometry and stamp stay where the rest of the mod's code lives.
namespace XRL.World.Parts
{
	/// <summary>
	/// One plot being raised. Stands in the middle of its own rect from the moment the ground is
	/// staked until the moment the finished work replaces it, and carries everything the raising
	/// needs to know: which rect, which design, which stage, and when the next one falls due.
	/// <para>
	/// A brand-new part, so its serialized field layout is free (STANDARDS 1 forbids APPENDING to
	/// an existing part, not declaring a new one). Nothing here is read positionally by any older
	/// save, because no older save has ever seen this part.
	/// </para>
	/// <para>
	/// It ticks on the ordinary turn clock the way <c>r_KingdomScaffold</c> does, and it advances
	/// by comparing the clock against thresholds rather than by counting ticks it was present for.
	/// That is what makes a long absence honest: come home after a hundred days and the plot is
	/// finished; come home after one and it is framed. Presence grants nothing and costs nothing.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomPlotWorks : IPart
	{
		/// <summary>Registry key of the design being raised.</summary>
		public string DesignKey;

		/// <summary>What the founder is told this is, when a stage is announced.</summary>
		public string DisplayName;

		/// <summary>Low corner of the plot, in cells.</summary>
		public int X1;

		/// <summary>Low corner of the plot, in cells.</summary>
		public int Y1;

		/// <summary>High corner of the plot, in cells. Inclusive.</summary>
		public int X2;

		/// <summary>High corner of the plot, in cells. Inclusive.</summary>
		public int Y2;

		/// <summary>Tick the ground was staked.</summary>
		public long StartTick;

		/// <summary>Ticks the whole raising takes, clearing and enclosure included.</summary>
		public long TotalTicks;

		/// <summary>Stage already applied, as <c>KingdomPlotRules.PlotStage</c>. Held as an int so
		/// the field's serialized type never depends on an enum's backing type.</summary>
		public int StageApplied;

		/// <summary>True for a plot that is never roofed.</summary>
		public bool Open;

		/// <summary>True when this plot is being carved rather than built: the rock is the wall.</summary>
		public bool Carved;

		/// <summary>Blueprint the enclosure is raised in. Empty on an open or carved plot.</summary>
		public string WallBlueprint;

		/// <summary>Population table the finished interior is furnished from. May be null.</summary>
		public string ContentsTable;

		/// <summary>Hands the finished work wants, carried through to the finished object.</summary>
		public int StaffNeeded;

		/// <summary>Whether those hands are a threshold rather than a scale.</summary>
		public bool ThresholdManning;

		/// <summary>Defence the finished work carries, already resolved at staking time.</summary>
		public int DefencePending;

		/// <summary>Whether this plot has a doorway at all. False for an open plot, and for a
		/// rect too small to have a border cell that is not a corner.</summary>
		public bool HasDoor;

		/// <summary>Doorway x, decided when the ground was staked rather than when the walls go
		/// up: which way a building faces is part of the plan, not an afterthought, and the
		/// carving has to know it before it cuts.</summary>
		public int DoorX;

		/// <summary>Doorway y. See <see cref="DoorX"/>.</summary>
		public int DoorY;

		public override bool WantTurnTick()
		{
			return true;
		}

		public override void TurnTick(long TimeTick, int Amount)
		{
			if (DesignKey != null)
			{
				KingdomPlots.Advance(this, TimeTick);
			}
			base.TurnTick(TimeTick, Amount);
		}

		/// <summary>The ground this plot holds.</summary>
		public KingdomPlotRules.PlotRect Rect()
		{
			return new KingdomPlotRules.PlotRect(X1, Y1, X2, Y2);
		}
	}
}

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	/// <summary>
	/// The engine-coupled half of plots: reading real ground into the pure geometry's terms,
	/// refusing ground the settlement may not take, and STAMPING a building over a rect in stages
	/// &mdash; staked, cleared, framed, walled, done.
	/// <para>
	/// This never calls vanilla's <c>PlaceHut</c>, and it never will. <c>PlaceHut</c> opens with
	/// <c>ClearRect</c> and lays its walls with <c>ClearAndAddObject</c>
	/// (<c>ZoneBuilderSandbox.cs:647-687</c>): it deletes whatever stands on the ground it is
	/// handed. That is correct for a zone builder running before a player has ever seen the zone,
	/// and it is the exact opposite of the protection law (STANDARDS 7). Every cell this file
	/// touches was surveyed first, and any cell holding something the settlement may not take
	/// refuses the whole plot by name and position (STANDARDS 7b).
	/// </para>
	/// <para>
	/// What the survey WILL clear is the ground itself: brush, trees, rock, marble seams, and
	/// somebody else's collapsed walls. That is the founder's own explicit designation &mdash;
	/// they commissioned a building on that ground &mdash; and it is the only source of building
	/// material a settlement without a mine has. Anything the table cannot name a yield for is
	/// <c>Held</c>, which refuses; the yield table is the allow-list, not a filter over one.
	/// </para>
	/// <para>
	/// Migration honesty: nothing already standing becomes a plot. A settlement raised before this
	/// existed is a scatter of single-cell works and stays exactly that, working exactly as it
	/// did. Plots begin with the next thing built.
	/// </para>
	/// </summary>
	public static class KingdomPlots
	{
		// --- Registry ---------------------------------------------------------------------

		// Plot specs live beside the catalog rather than inside KingdomRules.BuildEntry, the same
		// way zoning gates and upgrade chains do, so the registry parser needs one line of wiring
		// instead of a rewritten entry type. Keyed by building Key, which is what the registry
		// already overrides by (STANDARDS 6): a later file re-using a key registers its own spec
		// over the earlier one, including an entry that declares no plot attributes at all, which
		// correctly returns that design to the single-cell path.
		private static readonly Dictionary<string, KingdomPlotRules.PlotSpec> Specs = new Dictionary<string, KingdomPlotRules.PlotSpec>();

		/// <summary>Forgets every registered plot spec. Called by the registry loader before it
		/// re-reads the XML streams.</summary>
		public static void ClearSpecs()
		{
			Specs.Clear();
		}

		/// <summary>
		/// Registers one entry's plot attributes as the registry parses it. Call once per
		/// <c>&lt;building&gt;</c> element that parsed successfully, with the raw attribute
		/// strings; all four may be null, which registers "not a plot".
		/// </summary>
		/// <param name="Key">The entry's <c>Key</c>. Blank keys are ignored.</param>
		/// <param name="Plot">Raw <c>Plot</c> attribute.</param>
		/// <param name="Open">Raw <c>Open</c> attribute.</param>
		/// <param name="Sky">Raw <c>Sky</c> attribute.</param>
		/// <param name="Contents">Raw <c>Contents</c> attribute.</param>
		public static void RegisterSpec(string Key, string Plot, string Open, string Sky, string Contents)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			if (!KingdomPlotRules.TryParsePlotAttributes(Key, Plot, Open, Sky, Contents, out var spec, out var error))
			{
				MetricsManager.LogError("ThousandAndFirst KingdomBuildings: " + error);
				Specs.Remove(Key);
				return;
			}
			Specs[Key] = spec;
		}

		/// <summary>The plot spec a design was registered with, if it is a plot at all.</summary>
		/// <returns>False for a design that declares no <c>Plot</c> size, which is every design
		/// written before plots existed.</returns>
		public static bool TryGetSpec(string Key, out KingdomPlotRules.PlotSpec Spec)
		{
			Spec = null;
			if (string.IsNullOrEmpty(Key) || !Specs.TryGetValue(Key, out var spec) || spec == null
				|| spec.Size == KingdomPlotRules.PlotSize.None)
			{
				return false;
			}
			Spec = spec;
			return true;
		}

		/// <summary>Whether a design is raised as a plot. False sends the caller down the
		/// single-cell path it has always used, untouched.</summary>
		public static bool IsPlotDesign(string Key)
		{
			return TryGetSpec(Key, out _);
		}

		// --- Properties and blueprints ----------------------------------------------------

		/// <summary>Marks a wall, floor, door, or furnishing as belonging to a plot. Deliberately
		/// NOT <c>KingdomBuilt</c>: the building cap counts plots, not the hundred objects one
		/// plot is made of.</summary>
		public const string PlotPartProperty = "KingdomPlotPart";

		/// <summary>Groups every object of one plot, so a later striking can find all of them
		/// and take down exactly what the settlement raised and nothing else.</summary>
		public const string PlotIdProperty = "KingdomPlotId";

		/// <summary>Low corner x of a plot, stamped on the object that represents it.</summary>
		public const string PlotX1Property = "KingdomPlotX1";

		/// <summary>Low corner y of a plot, stamped on the object that represents it.</summary>
		public const string PlotY1Property = "KingdomPlotY1";

		/// <summary>High corner x of a plot, stamped on the object that represents it.</summary>
		public const string PlotX2Property = "KingdomPlotX2";

		/// <summary>High corner y of a plot, stamped on the object that represents it.</summary>
		public const string PlotY2Property = "KingdomPlotY2";

		/// <summary>Set once on a staked plan whose ground is blocked, so the reason is given
		/// once rather than every settlement pass (STANDARDS 7b).</summary>
		public const string BlockAnnouncedProperty = "KingdomPlotBlockSaid";

		/// <summary>Zone property carrying the rite ground's x, written where the rite was
		/// poured. Absent on a settlement founded before it was recorded, which simply has no
		/// rite seed for the heart.</summary>
		public const string RiteXProperty = "r_TAF_RiteX";

		/// <summary>Zone property carrying the rite ground's y. See <see cref="RiteXProperty"/>.</summary>
		public const string RiteYProperty = "r_TAF_RiteY";

		/// <summary>Game-state key prefix the realm's material stock is counted under. A generic,
		/// already-serialized slot on the game rather than a new field on <c>KingdomSystem</c>,
		/// exactly as <c>r_KingdomPlanMarker</c>'s ordering counter is &mdash; so clearance can
		/// earn material without touching any positionally-reflected field layout.</summary>
		public const string MaterialStatePrefix = "r_TAF_Material_";

		/// <summary>The object that stands in a plot while it is being raised.</summary>
		public const string WorksBlueprint = "r_KingdomPlotWorks";

		/// <summary>A corner post, standing only between the frame stage and the walls.</summary>
		public const string FrameBlueprint = "r_KingdomPlotFrame";

		/// <summary>Floor laid inside a roofed plot. Vanilla's own, the one <c>PlaceHut</c>
		/// uses.</summary>
		public const string FloorBlueprint = "DirtPath";

		/// <summary>The door cut in a roofed plot's wall.</summary>
		public const string DoorBlueprint = "Door";

		// --- Reading ground ---------------------------------------------------------------

		/// <summary>
		/// What one cell is, in the clearance table's terms, and what is standing there if the
		/// answer refuses the plot.
		/// <para>
		/// Creatures are not read at all: a settler standing on the ground walks off it, and a
		/// plot that refused every cell a wanderer happened to occupy would refuse forever for a
		/// reason the founder could never act on. Everything else that is not natural ground is
		/// <see cref="KingdomPlotRules.GroundKind.Held"/> &mdash; a dropped item, an owned thing,
		/// one of the settlement's own works, or anything this table simply cannot name.
		/// </para>
		/// </summary>
		/// <param name="C">The cell. Null reads as Held with no blocker named.</param>
		/// <param name="Blocker">What refuses the plot here, for the founder-facing sentence, or
		/// null when the ground is clearable.</param>
		public static KingdomPlotRules.GroundKind ReadGround(Cell C, out string Blocker)
		{
			Blocker = null;
			if (C == null)
			{
				Blocker = "the edge of the zone";
				return KingdomPlotRules.GroundKind.Held;
			}
			KingdomPlotRules.GroundKind kind = KingdomPlotRules.GroundKind.Bare;
			foreach (GameObject item in C.GetObjects())
			{
				if (item == null || item.IsCreature || item.IsPlayer())
				{
					continue;
				}
				KingdomPlotRules.GroundKind read = ReadObject(item);
				if (read == KingdomPlotRules.GroundKind.Bare)
				{
					continue;
				}
				if (KingdomPlotRules.Refuses(read))
				{
					Blocker = (read == KingdomPlotRules.GroundKind.Liquid) ? null : item.ShortDisplayNameStripped;
					return read;
				}
				if (KingdomPlotRules.ClearEffort(read) > KingdomPlotRules.ClearEffort(kind))
				{
					// The hardest thing standing in a cell is what clearing it costs. Compared by
					// effort rather than by enum order, so a marble seam under a fallen slab is
					// still read as marble.
					kind = read;
				}
			}
			return kind;
		}

		/// <summary>
		/// What one object makes of the cell it stands in. <see cref="KingdomPlotRules.GroundKind.Bare"/>
		/// means "this object is not in the way at all" &mdash; a floor, a cosmetic, a paint object.
		/// </summary>
		public static KingdomPlotRules.GroundKind ReadObject(GameObject Object)
		{
			if (Object == null)
			{
				return KingdomPlotRules.GroundKind.Bare;
			}
			if (Object.GetIntProperty("KingdomBuilt") == 1 || Object.GetIntProperty("KingdomStores") == 1
				|| Object.GetIntProperty("KingdomLarder") == 1 || Object.GetIntProperty("KingdomDefence") > 0
				|| Object.GetIntProperty(PlotPartProperty) == 1 || Object.HasPart("r_KingdomScaffold")
				|| Object.HasPart("r_KingdomPlanMarker") || Object.HasPart("r_KingdomPlotWorks"))
			{
				// The settlement's own works are not obstructions to be cleared; they are the
				// settlement. A plot never lands on one, and never takes one down to fit.
				return KingdomPlotRules.GroundKind.Held;
			}
			if (Object.HasPart("LiquidVolume"))
			{
				return KingdomPlotRules.GroundKind.Liquid;
			}
			GameObjectBlueprint blueprint = Object.GetBlueprint();
			if (blueprint != null && blueprint.InheritsFrom("Floor"))
			{
				return KingdomPlotRules.GroundKind.Bare;
			}
			if (Object.IsTakeable() || Object.IsOwned())
			{
				// A dropped waterskin is inviolate, and so is anything anybody's name is on.
				return KingdomPlotRules.GroundKind.Held;
			}
			if (Object.HasTag("Tree"))
			{
				return KingdomPlotRules.GroundKind.Trees;
			}
			if (Object.HasTag("Plant"))
			{
				return KingdomPlotRules.GroundKind.Brush;
			}
			if (Object.IsWall())
			{
				return WallGround(Object.Blueprint);
			}
			if (Object.IsDoor())
			{
				return KingdomPlotRules.GroundKind.Ruins;
			}
			return KingdomPlotRules.GroundKind.Held;
		}

		/// <summary>
		/// What kind of ground a standing wall is, by what it is made of. Marble is the rare seam
		/// the fine houses need; the manufactured walls &mdash; fulcrete, foamcrete, verdigris,
		/// plate &mdash; are somebody's ruin and yield scrap; everything else is rock.
		/// </summary>
		public static KingdomPlotRules.GroundKind WallGround(string Blueprint)
		{
			if (string.IsNullOrEmpty(Blueprint))
			{
				return KingdomPlotRules.GroundKind.Rock;
			}
			string blueprint = Blueprint.ToLowerInvariant();
			if (blueprint.Contains("marble"))
			{
				return KingdomPlotRules.GroundKind.Marble;
			}
			if (blueprint.Contains("crete") || blueprint.Contains("verdigris") || blueprint.Contains("metal")
				|| blueprint.Contains("plate") || blueprint.Contains("rubble") || blueprint.Contains("debris"))
			{
				return KingdomPlotRules.GroundKind.Ruins;
			}
			return KingdomPlotRules.GroundKind.Rock;
		}

		/// <summary>
		/// Every cell of a zone read once, with a running count of refusing cells so a rect can be
		/// rejected without walking it. Built once per siting pass: an XL plot has 280 cells and a
		/// surface zone has sixteen hundred anchors, and surveying each anchor's rect on its own
		/// would read the same cell hundreds of times.
		/// </summary>
		public sealed class GroundGrid
		{
			public int Width;

			public int Height;

			private readonly KingdomPlotRules.GroundKind[] Kinds;

			private readonly string[] Blockers;

			// Inclusive-exclusive prefix sums of refusing cells, (Width+1) by (Height+1).
			private readonly int[] Refusals;

			public GroundGrid(Zone Z)
			{
				Width = (Z == null) ? 0 : Z.Width;
				Height = (Z == null) ? 0 : Z.Height;
				Kinds = new KingdomPlotRules.GroundKind[Width * Height];
				Blockers = new string[Width * Height];
				Refusals = new int[(Width + 1) * (Height + 1)];
				for (int y = 0; y < Height; y++)
				{
					for (int x = 0; x < Width; x++)
					{
						KingdomPlotRules.GroundKind kind = ReadGround(Z.GetCell(x, y), out var blocker);
						Kinds[y * Width + x] = kind;
						Blockers[y * Width + x] = blocker;
						Refusals[(y + 1) * (Width + 1) + (x + 1)] =
							Refusals[y * (Width + 1) + (x + 1)]
							+ Refusals[(y + 1) * (Width + 1) + x]
							- Refusals[y * (Width + 1) + x]
							+ (KingdomPlotRules.Refuses(kind) ? 1 : 0);
					}
				}
			}

			public KingdomPlotRules.GroundKind KindAt(int X, int Y)
			{
				if (X < 0 || Y < 0 || X >= Width || Y >= Height)
				{
					return KingdomPlotRules.GroundKind.Held;
				}
				return Kinds[Y * Width + X];
			}

			/// <summary>Whether any cell of a rect refuses the plot. O(1).</summary>
			public bool AnyRefusal(KingdomPlotRules.PlotRect Rect)
			{
				if (Rect.X1 < 0 || Rect.Y1 < 0 || Rect.X2 >= Width || Rect.Y2 >= Height)
				{
					return true;
				}
				int stride = Width + 1;
				int total = Refusals[(Rect.Y2 + 1) * stride + (Rect.X2 + 1)]
					- Refusals[Rect.Y1 * stride + (Rect.X2 + 1)]
					- Refusals[(Rect.Y2 + 1) * stride + Rect.X1]
					+ Refusals[Rect.Y1 * stride + Rect.X1];
				return total > 0;
			}

			/// <summary>The first refusing cell of a rect, reading north-then-west, and what
			/// stands there. Walks the rect, so it is only ever called on the one rect whose
			/// refusal the founder is about to be told about.</summary>
			public bool TryFirstRefusal(KingdomPlotRules.PlotRect Rect, out int X, out int Y, out KingdomPlotRules.GroundKind Kind, out string Blocker)
			{
				for (int y = Rect.Y1; y <= Rect.Y2; y++)
				{
					for (int x = Rect.X1; x <= Rect.X2; x++)
					{
						KingdomPlotRules.GroundKind kind = KindAt(x, y);
						if (KingdomPlotRules.Refuses(kind))
						{
							X = x;
							Y = y;
							Kind = kind;
							Blocker = (x < 0 || y < 0 || x >= Width || y >= Height) ? "the edge of the zone" : Blockers[y * Width + x];
							return true;
						}
					}
				}
				X = 0;
				Y = 0;
				Kind = KingdomPlotRules.GroundKind.Bare;
				Blocker = null;
				return false;
			}

			/// <summary>Every cell of a rect, in the clearance table's terms.</summary>
			public List<KingdomPlotRules.GroundKind> CellsOf(KingdomPlotRules.PlotRect Rect)
			{
				List<KingdomPlotRules.GroundKind> cells = new List<KingdomPlotRules.GroundKind>(Rect.Area);
				for (int y = Rect.Y1; y <= Rect.Y2; y++)
				{
					for (int x = Rect.X1; x <= Rect.X2; x++)
					{
						cells.Add(KindAt(x, y));
					}
				}
				return cells;
			}
		}

		// --- Plots already laid -----------------------------------------------------------

		/// <summary>The rect an object carries, if it represents a plot.</summary>
		public static bool TryReadRect(GameObject Object, out KingdomPlotRules.PlotRect Rect)
		{
			Rect = default(KingdomPlotRules.PlotRect);
			if (Object == null)
			{
				return false;
			}
			r_KingdomPlotWorks works = Object.GetPart<r_KingdomPlotWorks>();
			if (works != null)
			{
				Rect = works.Rect();
				return true;
			}
			if (!Object.HasIntProperty(PlotX2Property))
			{
				return false;
			}
			Rect = new KingdomPlotRules.PlotRect(
				Object.GetIntProperty(PlotX1Property),
				Object.GetIntProperty(PlotY1Property),
				Object.GetIntProperty(PlotX2Property),
				Object.GetIntProperty(PlotY2Property));
			return true;
		}

		/// <summary>Every plot already laid out in a zone, finished or still rising. The road
		/// budget and the lane rule are both reckoned against this.</summary>
		public static List<KingdomPlotRules.PlotRect> ReadPlots(Zone Z)
		{
			List<KingdomPlotRules.PlotRect> plots = new List<KingdomPlotRules.PlotRect>();
			if (Z == null)
			{
				return plots;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (TryReadRect(item, out var rect))
				{
					plots.Add(rect);
				}
			}
			return plots;
		}

		/// <summary>Stamps a rect onto the object that represents a plot, so the ground reads as
		/// laid out for every later siting.</summary>
		public static void StampRect(GameObject Object, KingdomPlotRules.PlotRect Rect)
		{
			if (Object == null)
			{
				return;
			}
			Object.SetIntProperty(PlotX1Property, Rect.X1);
			Object.SetIntProperty(PlotY1Property, Rect.Y1);
			Object.SetIntProperty(PlotX2Property, Rect.X2);
			Object.SetIntProperty(PlotY2Property, Rect.Y2);
		}

		/// <summary>The rite ground of this zone, if it was recorded when the rite was poured.</summary>
		public static bool TryRiteGround(Zone Z, out int X, out int Y)
		{
			X = 0;
			Y = 0;
			if (Z == null)
			{
				return false;
			}
			return int.TryParse(Z.GetZoneProperty(RiteXProperty, null), out X)
				&& int.TryParse(Z.GetZoneProperty(RiteYProperty, null), out Y);
		}

		// --- Siting -----------------------------------------------------------------------

		/// <summary>
		/// Finds the ground for one plot: every rect of the right tier that fits the zone's
		/// interior, keeps its lane from every plot already laid, and holds nothing the settlement
		/// may not take &mdash; scored by the settlement's own layout grammar
		/// (<see cref="KingdomPlotRules.ChooseRect"/>).
		/// <para>
		/// Never silent. When no rect survives, <paramref name="Refusal"/> is the sentence the
		/// founder reads, and it names the ground that came closest and the exact thing standing
		/// in it (STANDARDS 7b).
		/// </para>
		/// </summary>
		/// <param name="Z">The zone to build in.</param>
		/// <param name="System">The realm, for its claim and its stage.</param>
		/// <param name="Entry">The design being raised.</param>
		/// <param name="Spec">Its plot spec.</param>
		/// <param name="Grid">The zone's ground, read once.</param>
		/// <param name="Prefer">A cell the founder has already chosen &mdash; a staked plan's own
		/// stake &mdash; which is taken as the plot's centre and scored as the founder's ground.
		/// Null falls back to wherever the founder is standing.</param>
		/// <param name="Rect">The chosen rect, meaningful only when this returns true.</param>
		/// <param name="Outcome">What the plan did, for the message the founder reads.</param>
		/// <param name="Refusal">Null on success; a founder-facing sentence otherwise.</param>
		public static bool TryFindRect(Zone Z, KingdomSystem System, KingdomRules.BuildEntry Entry, KingdomPlotRules.PlotSpec Spec, GroundGrid Grid, Cell Prefer, out KingdomPlotRules.PlotRect Rect, out KingdomLayoutRules.LayoutOutcome Outcome, out string Refusal)
		{
			Rect = default(KingdomPlotRules.PlotRect);
			Outcome = KingdomLayoutRules.LayoutOutcome.None;
			Refusal = null;
			if (Z == null || Entry == null || Spec == null || Grid == null)
			{
				Refusal = KingdomPlotRules.RefuseRoom((Spec == null) ? KingdomPlotRules.PlotSize.Small : Spec.Size);
				return false;
			}
			if (!KingdomPlotRules.TryInterior(Z.Width, Z.Height, out var interior)
				|| !KingdomPlotRules.TryDimensions(Spec.Size, out var plotWidth, out var plotHeight))
			{
				Refusal = KingdomPlotRules.RefuseRoom(Spec.Size);
				return false;
			}
			List<KingdomPlotRules.PlotRect> laid = ReadPlots(Z);
			List<KingdomLayoutRules.LayoutMark> marks = KingdomLayout.ReadMarks(Z);
			bool hasRite = TryRiteGround(Z, out var riteX, out var riteY);
			Cell founderCell = Prefer ?? The.Player?.CurrentCell;
			bool hasFounder = founderCell != null && founderCell.ParentZone == Z;
			int founderX = hasFounder ? founderCell.X : 0;
			int founderY = hasFounder ? founderCell.Y : 0;
			List<KingdomPlotRules.PlotRect> candidates = new List<KingdomPlotRules.PlotRect>();
			bool sawBlocked = false;
			KingdomPlotRules.PlotRect nearestBlocked = default(KingdomPlotRules.PlotRect);
			int nearestBlockedReach = 0;
			for (int y = interior.Y1; y + plotHeight - 1 <= interior.Y2; y++)
			{
				for (int x = interior.X1; x + plotWidth - 1 <= interior.X2; x++)
				{
					KingdomPlotRules.PlotRect rect = new KingdomPlotRules.PlotRect(x, y, x + plotWidth - 1, y + plotHeight - 1);
					if (KingdomPlotRules.CrowdsExisting(rect, laid))
					{
						continue;
					}
					if (Grid.AnyRefusal(rect))
					{
						int reach = hasFounder ? KingdomPlotRules.Reach(rect, founderX, founderY) : 0;
						if (!sawBlocked || reach < nearestBlockedReach)
						{
							sawBlocked = true;
							nearestBlocked = rect;
							nearestBlockedReach = reach;
						}
						continue;
					}
					candidates.Add(rect);
				}
			}
			if (candidates.Count == 0)
			{
				// The ground that came closest is the one the founder is told about: naming a
				// refusal on the far side of the zone would be true and useless.
				if (sawBlocked && Grid.TryFirstRefusal(nearestBlocked, out var blockX, out var blockY, out var blockKind, out var blocker))
				{
					Refusal = (blockKind == KingdomPlotRules.GroundKind.Liquid)
						? KingdomPlotRules.RefuseLiquid(blockX, blockY)
						: KingdomPlotRules.RefuseObstruction(blocker ?? "something", blockX, blockY);
				}
				else
				{
					Refusal = KingdomPlotRules.RefuseRoom(Spec.Size);
				}
				return false;
			}
			KingdomLayoutRules.LayoutPurpose purpose = KingdomLayout.PurposeOfEntry(Entry);
			KingdomRules.Frontier edges = (System != null)
				? KingdomRules.FrontierEdges(Z.ZoneID, System.ClaimedZones)
				: KingdomRules.Frontier.None;
			Outcome = KingdomPlotRules.ChooseRect(purpose, Spec.Size, Z.Width, Z.Height, edges, marks, candidates,
				hasFounder, founderX, founderY, hasRite, riteX, riteY, out var index);
			if (index < 0)
			{
				// The plan has nothing to say - empty ground, or a purpose it does not file. The
				// founder's own ground wins outright, exactly as it does everywhere else.
				index = NearestIndex(candidates, hasFounder, founderX, founderY);
				Outcome = KingdomLayoutRules.LayoutOutcome.Defer;
			}
			Rect = candidates[index];
			return true;
		}

		/// <summary>The candidate nearest the founder, or the lowest-positioned one when the
		/// founder is elsewhere. Deterministic either way.</summary>
		public static int NearestIndex(IList<KingdomPlotRules.PlotRect> Candidates, bool HasFounder, int FounderX, int FounderY)
		{
			int best = -1;
			int bestReach = 0;
			for (int i = 0; i < Candidates.Count; i++)
			{
				int reach = HasFounder ? KingdomPlotRules.Reach(Candidates[i], FounderX, FounderY) : 0;
				if (best < 0 || KingdomPlotRules.Beats(0, reach, Candidates[i], 0, bestReach, Candidates[best]))
				{
					best = i;
					bestReach = reach;
				}
			}
			return best;
		}

		// --- Staking ----------------------------------------------------------------------

		/// <summary>
		/// Issues one plot-sized commission. Runs every check a single-cell commission runs, in
		/// the same order and with the same refusals, plus the four a plot adds: the tier's stage
		/// gate, the weather gate underground, the zone's road budget, and the ground itself.
		/// </summary>
		/// <param name="System">The realm; founded, and holding this ground.</param>
		/// <param name="Z">The zone.</param>
		/// <param name="Entry">The design.</param>
		/// <param name="SkinKey">The founder's chosen look, or null.</param>
		/// <param name="Failure">A founder-facing sentence when this returns false; null
		/// otherwise. Every refusal names what would lift it.</param>
		/// <returns>True once the ground is staked and the water is spent.</returns>
		public static bool Commission(KingdomSystem System, Zone Z, KingdomRules.BuildEntry Entry, string SkinKey, out string Failure)
		{
			Failure = null;
			if (System == null || Z == null || Entry == null || !TryGetSpec(Entry.Key, out var spec))
			{
				Failure = "No such design.";
				return false;
			}
			if (!KingdomPlotRules.Allows(System.Stage, spec.Size))
			{
				Failure = KingdomPlotRules.RefuseStage(spec.Size, System.SeatName, System.Stage);
				return false;
			}
			bool carved = KingdomPlotRules.IsUnderground(Z.Z);
			if (carved && spec.RequiresSky)
			{
				Failure = KingdomPlotRules.RefuseSky(Entry.Name);
				return false;
			}
			if (Entry.Defence <= 0 && CountBuilt(Z) >= KingdomRules.MaxBuildingsForStage(System.Stage))
			{
				Failure = "There is no more room in the plan. " + System.SeatName + " is as built-up as this ground allows, until it grows into something larger.";
				return false;
			}
			if (KingdomPlotRules.WouldExceedBudget(ReadPlots(Z), spec.Size, Z.Width, Z.Height))
			{
				Failure = KingdomPlotRules.RefuseBudget(System.SeatName);
				return false;
			}
			if (KingdomGrowth.CountStoredWater(Z) < Entry.CostDrams)
			{
				Failure = "The work would cost {{C|" + Entry.CostDrams + " drams}} from the stores, and the stores cannot bear it.";
				return false;
			}
			GroundGrid grid = new GroundGrid(Z);
			if (!TryFindRect(Z, System, Entry, spec, grid, null, out var rect, out var outcome, out var refusal))
			{
				Failure = refusal;
				return false;
			}
			GameObject works = Stake(System, Z, rect, Entry, spec, grid, SkinKey, carved);
			if (works == null)
			{
				Failure = "The ground could not be staked.";
				return false;
			}
			// Staked before it is paid for, deliberately, for the same reason a scaffold is: if
			// the works cannot be raised the settlement must not already have spent the water on
			// nothing. There is no refund path because there is nothing to refund.
			KingdomGrowth.ConsumeStoredWater(Z, Entry.CostDrams);
			KingdomChronicle.Record(System, "ground was staked at " + System.KingdomDisplayName + " for " + XRL.Language.Grammar.A(Entry.Name));
			string clause = KingdomLayoutRules.PlacementClause(KingdomLayout.PurposeOfEntry(Entry), outcome);
			MessageQueue.AddPlayerMessage("{{G|A " + KingdomPlotRules.SizeName(spec.Size) + " plot is staked for the " + Entry.Name
				+ ((clause == null) ? "" : (" " + clause)) + ".}}");
			return true;
		}

		/// <summary>
		/// Raises the works on a rect that has already been chosen and surveyed. Spends nothing
		/// and refuses nothing &mdash; every judgement has been made by the time this is called.
		/// </summary>
		/// <returns>The works object, or null when the engine would not create it.</returns>
		public static GameObject Stake(KingdomSystem System, Zone Z, KingdomPlotRules.PlotRect Rect, KingdomRules.BuildEntry Entry, KingdomPlotRules.PlotSpec Spec, GroundGrid Grid, string SkinKey, bool Carved)
		{
			Cell cell = Z.GetCell(Rect.CenterX, Rect.CenterY);
			if (cell == null)
			{
				return null;
			}
			GameObject works = GameObject.Create(WorksBlueprint);
			if (works == null)
			{
				return null;
			}
			r_KingdomPlotWorks part = works.GetPart<r_KingdomPlotWorks>();
			if (part == null)
			{
				works.Destroy(null, Silent: true);
				return null;
			}
			part.DesignKey = Entry.Key;
			part.DisplayName = Entry.Name;
			part.X1 = Rect.X1;
			part.Y1 = Rect.Y1;
			part.X2 = Rect.X2;
			part.Y2 = Rect.Y2;
			part.StartTick = The.Game.TimeTicks;
			part.TotalTicks = KingdomPlotRules.RaiseTicks(
				KingdomCommission.CraftBuildTicks(Entry.BuildTicks, System.ZoneDistricts.Values),
				Grid.CellsOf(Rect), Rect, Carved, Spec.Open);
			part.StageApplied = (int)KingdomPlotRules.PlotStage.Staked;
			part.Open = Spec.Open;
			part.Carved = Carved;
			part.WallBlueprint = (Spec.Open || Carved) ? null : KingdomPlotRules.WallBlueprintFor(System.Style, System.FoundingRegionName);
			part.ContentsTable = Spec.Contents;
			part.StaffNeeded = Entry.Staff;
			part.ThresholdManning = KingdomRules.IsThresholdManning(Entry.Manning);
			if (Entry.Defence > 0)
			{
				bool hasTinkering = The.Player != null && The.Player.HasSkill("Tinkering");
				bool hasAdvancedTinkering = The.Player != null && The.Player.HasSkill("Tinkering_Tinker1");
				part.DefencePending = KingdomRules.WallDefence(Entry.Defence, System.FoundingTerrainBlueprint, System.FoundingRegionName, hasTinkering, hasAdvancedTinkering);
			}
			bool hasRite = TryRiteGround(Z, out var riteX, out var riteY);
			bool hasHeart = KingdomPlotRules.TryHeart(KingdomLayout.ReadMarks(Z), hasRite, riteX, riteY, out var heartX, out var heartY);
			bool foundDoor = KingdomPlotRules.TryDoor(Rect,
				hasHeart ? heartX : Rect.CenterX, hasHeart ? heartY : Rect.CenterY, out var doorX, out var doorY);
			part.HasDoor = foundDoor && !Spec.Open;
			part.DoorX = doorX;
			part.DoorY = doorY;
			works.DisplayName = "plot: " + Entry.Name;
			works.SetStringProperty(PlotIdProperty, Entry.Key + "@" + Rect.X1 + "." + Rect.Y1 + "." + The.Game.TimeTicks);
			works.SetStringProperty(KingdomUpgrade.BuildKeyProperty, Entry.Key);
			KingdomDesign.StageSkin(works, Entry, SkinKey);
			cell.AddObject(works);
			KingdomLog.Log("plot staked: " + Entry.Key + " " + Rect.X1 + "," + Rect.Y1 + " to " + Rect.X2 + "," + Rect.Y2
				+ (Carved ? " (carved)" : "") + " over " + part.TotalTicks + " ticks");
			return works;
		}

		/// <summary>
		/// Buildings and scaffolds this zone already carries, by the exact rule
		/// <c>KingdomCommission.Commission</c> uses for its own cap check: walls are exempt, work
		/// in progress already counts. Plot walls, floors, and furnishings are not counted &mdash;
		/// the cap counts plots, not the hundred objects one plot is made of.
		/// </summary>
		public static int CountBuilt(Zone Z)
		{
			int built = 0;
			if (Z == null)
			{
				return 0;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (item.GetIntProperty("KingdomDefence") > 0 || item.GetIntProperty(PlotPartProperty) == 1)
				{
					continue;
				}
				if (item.GetIntProperty("KingdomBuilt") == 1 || item.HasPart("r_KingdomScaffold") || item.HasPart("r_KingdomPlotWorks"))
				{
					built++;
				}
			}
			return built;
		}

		// --- The plan path ----------------------------------------------------------------

		/// <summary>
		/// Whether a staked plan for a plot-sized design must wait this pass, and why. Announces
		/// the reason once on the marker and never again until the block lifts (STANDARDS 7b's
		/// established idiom, carried on a property rather than a field so no part's serialized
		/// layout moves).
		/// <para>
		/// Called BEFORE the water is drawn, so a plan whose ground is blocked never spends
		/// anything: waiting is not failing, and a waiting plan has nothing to refund.
		/// </para>
		/// </summary>
		/// <returns>False for a design that is not a plot at all, which the caller then handles
		/// exactly as it always has.</returns>
		public static bool PlanBlocked(KingdomSystem System, GameObject Marker, KingdomRules.BuildEntry Entry)
		{
			if (System == null || Marker == null || Entry == null || !TryGetSpec(Entry.Key, out var spec))
			{
				return false;
			}
			Zone zone = Marker.CurrentZone;
			if (zone == null)
			{
				return true;
			}
			string refusal = null;
			KingdomSystem.Guard("plot plan", delegate
			{
				if (!KingdomPlotRules.Allows(System.Stage, spec.Size))
				{
					refusal = KingdomPlotRules.RefuseStage(spec.Size, System.SeatName, System.Stage);
					return;
				}
				if (KingdomPlotRules.IsUnderground(zone.Z) && spec.RequiresSky)
				{
					refusal = KingdomPlotRules.RefuseSky(Entry.Name);
					return;
				}
				if (KingdomPlotRules.WouldExceedBudget(ReadPlots(zone), spec.Size, zone.Width, zone.Height))
				{
					refusal = KingdomPlotRules.RefuseBudget(System.SeatName);
					return;
				}
				if (!TryFindRect(zone, System, Entry, spec, new GroundGrid(zone), Marker.CurrentCell, out _, out _, out var reason))
				{
					refusal = reason;
				}
			});
			if (refusal == null)
			{
				Marker.SetStringProperty(BlockAnnouncedProperty, null, RemoveIfNull: true);
				return false;
			}
			AnnounceOnce(System, Marker, "The plan staked at " + System.KingdomDisplayName + " waits. " + refusal);
			return true;
		}

		/// <summary>
		/// Turns a staked plan into a staked plot, at the ground the founder chose when they drove
		/// the stake. The marker's own cell is offered as the plot's centre, so a plan is the
		/// founder placing a building by hand and the grammar only breaks the tie.
		/// </summary>
		/// <returns>False for a design that is not a plot, leaving the caller's own
		/// scaffold path untouched.</returns>
		public static bool StakeFromPlan(KingdomSystem System, GameObject Marker, KingdomRules.BuildEntry Entry)
		{
			if (System == null || Marker == null || Entry == null || !TryGetSpec(Entry.Key, out var spec))
			{
				return false;
			}
			Zone zone = Marker.CurrentZone;
			Cell cell = Marker.CurrentCell;
			if (zone == null || cell == null)
			{
				return false;
			}
			bool staked = false;
			KingdomSystem.Guard("plot from plan", delegate
			{
				GroundGrid grid = new GroundGrid(zone);
				if (!TryFindRect(zone, System, Entry, spec, grid, cell, out var rect, out _, out _))
				{
					return;
				}
				string skinKey = Marker.GetStringProperty(KingdomDesign.PlannedSkinProperty);
				Marker.Destroy(null, Silent: true);
				staked = Stake(System, zone, rect, Entry, spec, grid, skinKey, KingdomPlotRules.IsUnderground(zone.Z)) != null;
			});
			if (staked)
			{
				KingdomChronicle.Record(System, "the ground staked at " + System.KingdomDisplayName + " was measured out for " + XRL.Language.Grammar.A(Entry.Name));
				System.Ledger.Note("{{G|The plan staked at " + System.KingdomDisplayName + " is under way: the ground for the " + Entry.Name + " is measured out.}}");
				MessageQueue.AddPlayerMessage("{{G|The plan staked at " + System.KingdomDisplayName + " is under way. The ground for the " + Entry.Name + " is measured out.}}");
			}
			return staked;
		}

		// --- The adoption path ------------------------------------------------------------

		/// <summary>
		/// Records the ground a founder-raised structure occupies when the design it was adopted
		/// under is a plot design. Nothing is stamped over what the founder built &mdash; their
		/// walls, their floor, their door, all untouched &mdash; the settlement simply learns that
		/// this much ground is spoken for, so later plots keep their lane from it and the road
		/// budget counts it.
		/// </summary>
		/// <returns>False when the design is not a plot design, which leaves adoption exactly as
		/// it was.</returns>
		public static bool StampAdopted(GameObject Adopted, KingdomRules.BuildEntry Entry)
		{
			if (Adopted == null || Entry == null || !TryGetSpec(Entry.Key, out var spec))
			{
				return false;
			}
			Cell cell = Adopted.CurrentCell;
			if (cell == null || !KingdomPlotRules.TryDimensions(spec.Size, out var width, out var height))
			{
				return false;
			}
			KingdomPlotRules.PlotRect rect = new KingdomPlotRules.PlotRect(
				cell.X - (width - 1) / 2, cell.Y - (height - 1) / 2,
				cell.X - (width - 1) / 2 + width - 1, cell.Y - (height - 1) / 2 + height - 1);
			StampRect(Adopted, rect);
			return true;
		}

		// --- The stamp --------------------------------------------------------------------

		/// <summary>
		/// Advances one plot to whatever stage the clock has honestly bought, applying every stage
		/// it crossed in order. Called from the works part's own turn tick, so it wraps its work
		/// rather than let a bad cell break the tick loop (STANDARDS 9).
		/// </summary>
		public static void Advance(r_KingdomPlotWorks Works, long TimeTick)
		{
			if (Works == null || Works.DesignKey == null)
			{
				return;
			}
			KingdomPlotRules.PlotStage target = KingdomPlotRules.StageAt(TimeTick - Works.StartTick, Works.TotalTicks);
			if ((int)target <= Works.StageApplied)
			{
				return;
			}
			KingdomSystem.Guard("plot raising", delegate
			{
				while (Works.StageApplied < (int)target && Works.DesignKey != null)
				{
					KingdomPlotRules.PlotStage next = (KingdomPlotRules.PlotStage)(Works.StageApplied + 1);
					if (!Apply(Works, next))
					{
						// The stage could not land -- a design a third-party mod withdrew between
						// staking and finishing, or a zone torn down under us. The plot stays
						// exactly where it is and tries again, which is the same "waiting is not
						// failing" contract a staked plan already holds.
						break;
					}
					Works.StageApplied = (int)next;
				}
			});
		}

		private static bool Apply(r_KingdomPlotWorks Works, KingdomPlotRules.PlotStage Stage)
		{
			GameObject parent = Works.ParentObject;
			Zone zone = parent?.CurrentZone;
			if (zone == null)
			{
				return false;
			}
			KingdomPlotRules.PlotRect rect = Works.Rect();
			switch (Stage)
			{
				case KingdomPlotRules.PlotStage.Cleared:
					ClearGround(Works, zone, rect);
					break;
				case KingdomPlotRules.PlotStage.Frame:
					RaiseFrame(Works, zone, rect);
					break;
				case KingdomPlotRules.PlotStage.Walls:
					RaiseWalls(Works, zone, rect);
					break;
				case KingdomPlotRules.PlotStage.Done:
					return Finish(Works, zone, rect);
			}
			string line = KingdomPlotRules.StageLine(Stage, Works.DisplayName ?? "work");
			if (line != null && parent.IsValid() && zone.IsActive())
			{
				MessageQueue.AddPlayerMessage("{{W|" + line + "}}");
			}
			return true;
		}

		/// <summary>
		/// Takes down what stood on the plot, and puts what it was worth into the realm's stock.
		/// Only ever touches cells the survey classified as clearable ground: everything else
		/// refused the plot before it was ever staked, so nothing here has to decide whether a
		/// thing may be destroyed &mdash; that decision was made, once, when the founder chose
		/// this ground.
		/// </summary>
		private static void ClearGround(r_KingdomPlotWorks Works, Zone Z, KingdomPlotRules.PlotRect Rect)
		{
			int[] yields = new int[5];
			// Carving cuts the room, not the hill. Everything on the plot's edge is left exactly
			// where it stands, because underground that rock IS the enclosure -- which is the
			// whole of the bargain that makes the doubled clearing cost worth paying. Only the
			// doorway is cut through it.
			bool carveOnly = Works.Carved && !Works.Open && Rect.Width > 2 && Rect.Height > 2;
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
			{
				for (int x = Rect.X1; x <= Rect.X2; x++)
				{
					if (carveOnly && Rect.IsBorder(x, y) && !(Works.HasDoor && x == Works.DoorX && y == Works.DoorY))
					{
						continue;
					}
					Cell cell = Z.GetCell(x, y);
					if (cell == null)
					{
						continue;
					}
					List<GameObject> standing = new List<GameObject>(cell.GetObjects());
					for (int i = 0; i < standing.Count; i++)
					{
						GameObject item = standing[i];
						if (item == null || item == Works.ParentObject || item.IsCreature || item.IsPlayer())
						{
							continue;
						}
						KingdomPlotRules.GroundKind kind = ReadObject(item);
						if (kind == KingdomPlotRules.GroundKind.Bare || KingdomPlotRules.Refuses(kind))
						{
							// Bare is a floor and stays; a refusing cell cannot exist inside a
							// rect that was surveyed, but if the world changed under us while the
							// founder was away, the honest answer is to leave it standing.
							continue;
						}
						yields[(int)KingdomPlotRules.YieldOf(kind, out var amount)] += amount;
						item.Destroy(null, Silent: true);
					}
				}
			}
			// Carving pays in stone because the rock IS the ground here, and it costs twice what
			// clearing the open costs (KingdomPlotRules.UndergroundClearPercent, already spent in
			// the raising time). Nothing is added on top of what came out: the compensation for
			// the doubled effort is that the rock the carving left is the enclosure, and no wall
			// is ever raised down here.
			CreditMaterials(yields, Works.DisplayName);
		}

		private static void CreditMaterials(int[] Yields, string Name)
		{
			System.Text.StringBuilder earned = new System.Text.StringBuilder();
			for (int i = 1; i < Yields.Length; i++)
			{
				if (Yields[i] <= 0)
				{
					continue;
				}
				KingdomPlotRules.Material material = (KingdomPlotRules.Material)i;
				The.Game.ModIntGameState(MaterialStatePrefix + material, Yields[i]);
				if (earned.Length > 0)
				{
					earned.Append(", ");
				}
				earned.Append(Yields[i]).Append(' ').Append(material.ToString().ToLowerInvariant());
			}
			if (earned.Length > 0)
			{
				MessageQueue.AddPlayerMessage("{{G|Clearing the ground for the " + (Name ?? "work") + " yields " + earned + ".}}");
			}
		}

		/// <summary>How much of a material the realm holds. Counted in a generic game-state slot
		/// until a stockpile with a dedicated mark of its own exists; nothing is ever minted, and
		/// nothing but clearance, salvage, or trade adds to it.</summary>
		public static int MaterialsHeld(KingdomPlotRules.Material Of)
		{
			if (Of == KingdomPlotRules.Material.None)
			{
				return 0;
			}
			return The.Game.GetIntGameState(MaterialStatePrefix + Of);
		}

		private static void RaiseFrame(r_KingdomPlotWorks Works, Zone Z, KingdomPlotRules.PlotRect Rect)
		{
			if (Works.Open || Works.Carved)
			{
				return;
			}
			PlaceMarked(Works, Z.GetCell(Rect.X1, Rect.Y1), FrameBlueprint);
			PlaceMarked(Works, Z.GetCell(Rect.X2, Rect.Y1), FrameBlueprint);
			PlaceMarked(Works, Z.GetCell(Rect.X1, Rect.Y2), FrameBlueprint);
			PlaceMarked(Works, Z.GetCell(Rect.X2, Rect.Y2), FrameBlueprint);
		}

		private static void RaiseWalls(r_KingdomPlotWorks Works, Zone Z, KingdomPlotRules.PlotRect Rect)
		{
			if (Works.Open)
			{
				// A field, a yard, a salt-pan, a reservoir. Same rect discipline, no enclosure.
				return;
			}
			TakeDownFrame(Works, Z, Rect);
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
			{
				for (int x = Rect.X1; x <= Rect.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null)
					{
						continue;
					}
					bool border = Rect.IsBorder(x, y);
					if (border && Works.HasDoor && x == Works.DoorX && y == Works.DoorY)
					{
						PlaceMarked(Works, cell, DoorBlueprint);
						continue;
					}
					if (border)
					{
						// Underground the rock IS the wall: what the carving left standing around
						// the plot is the enclosure, and raising a second one inside it would be
						// building a wall against a wall.
						if (!Works.Carved && !string.IsNullOrEmpty(Works.WallBlueprint))
						{
							PlaceMarked(Works, cell, Works.WallBlueprint);
						}
						continue;
					}
					PlaceMarked(Works, cell, FloorBlueprint);
				}
			}
		}

		private static void TakeDownFrame(r_KingdomPlotWorks Works, Zone Z, KingdomPlotRules.PlotRect Rect)
		{
			string id = Works.ParentObject?.GetStringProperty(PlotIdProperty);
			for (int y = Rect.Y1; y <= Rect.Y2; y++)
			{
				for (int x = Rect.X1; x <= Rect.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null)
					{
						continue;
					}
					List<GameObject> standing = new List<GameObject>(cell.GetObjects());
					for (int i = 0; i < standing.Count; i++)
					{
						// Only ever the posts this plot put up, and only this plot's: an object
						// we created and marked, which is the one thing STANDARDS 7 lets a
						// kingdom system destroy.
						if (standing[i] != null && standing[i].Blueprint == FrameBlueprint
							&& standing[i].GetStringProperty(PlotIdProperty) == id)
						{
							standing[i].Destroy(null, Silent: true);
						}
					}
				}
			}
		}

		private static GameObject PlaceMarked(r_KingdomPlotWorks Works, Cell C, string Blueprint)
		{
			if (C == null || string.IsNullOrEmpty(Blueprint))
			{
				return null;
			}
			GameObject placed = GameObject.Create(Blueprint);
			if (placed == null)
			{
				return null;
			}
			placed.SetIntProperty(PlotPartProperty, 1);
			string id = Works.ParentObject?.GetStringProperty(PlotIdProperty);
			if (!string.IsNullOrEmpty(id))
			{
				placed.SetStringProperty(PlotIdProperty, id);
			}
			C.AddObject(placed);
			return placed;
		}

		/// <summary>
		/// Finishes the plot: furnishes the interior from the design's own contents table the way
		/// vanilla huts furnish, raises the object that stands for the building, hands it every
		/// property the rest of the settlement reads a work by, and takes the works down.
		/// </summary>
		private static bool Finish(r_KingdomPlotWorks Works, Zone Z, KingdomPlotRules.PlotRect Rect)
		{
			GameObject parent = Works.ParentObject;
			Cell cell = parent?.CurrentCell;
			if (cell == null || !KingdomData.TryGetBuilding(Works.DesignKey, out var entry))
			{
				return false;
			}
			string id = parent.GetStringProperty(PlotIdProperty);
			string skinColorString = parent.GetStringProperty(KingdomDesign.StagedColorStringProperty);
			string skinDetailColor = parent.GetStringProperty(KingdomDesign.StagedDetailColorProperty);
			string skinRenderString = parent.GetStringProperty(KingdomDesign.StagedRenderStringProperty);
			string skinTile = parent.GetStringProperty(KingdomDesign.StagedTileProperty);
			int defence = Works.DefencePending;
			int staff = Works.StaffNeeded;
			bool threshold = Works.ThresholdManning;
			string contents = Works.ContentsTable;
			string displayName = Works.DisplayName ?? entry.Name;
			// Read before the works comes down, not after: everything the founder chose when they
			// staked this ground rides on the works object, and the works is about to stop being a
			// thing to read from.
			Works.DesignKey = null;
			GameObject building = GameObject.Create(entry.Blueprint);
			if (building == null)
			{
				Works.DesignKey = entry.Key;
				return false;
			}
			parent.Destroy(null, Silent: true);
			cell.AddObject(building);
			KingdomDesign.ApplyRenderOverrides(building, skinColorString, skinDetailColor, skinRenderString, skinTile);
			building.SetIntProperty("KingdomBuilt", 1);
			building.SetStringProperty(KingdomUpgrade.BuildKeyProperty, entry.Key);
			if (!string.IsNullOrEmpty(id))
			{
				building.SetStringProperty(PlotIdProperty, id);
			}
			StampRect(building, Rect);
			if (building.GetPart<LiquidVolume>() != null)
			{
				building.SetIntProperty("KingdomStores", 1);
			}
			else if (entry.Blueprint == r_KingdomScaffold.LarderBlueprint)
			{
				building.SetIntProperty("KingdomLarder", 1);
			}
			if (defence > 0)
			{
				building.SetIntProperty("KingdomDefence", defence);
			}
			if (staff > 0)
			{
				building.SetIntProperty("KingdomStaffNeeded", staff);
				if (threshold)
				{
					building.SetIntProperty("KingdomThresholdManning", 1);
				}
				if (building.GetPart<Capacitor>() != null)
				{
					building.SetIntProperty("KingdomHandCranked", 1);
				}
			}
			building.MakeActive();
			Furnish(Z, Rect, contents, id, entry.Key);
			KingdomLog.Log("plot complete: " + displayName + " (" + entry.Blueprint + ") over " + Rect.X1 + "," + Rect.Y1 + " to " + Rect.X2 + "," + Rect.Y2);
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (system.Founded)
			{
				system.RecordDeed("the " + displayName + " raised at " + system.KingdomDisplayName);
				KingdomChronicle.Record(system, "the " + displayName + " was raised at " + system.KingdomDisplayName);
			}
			MessageQueue.AddPlayerMessage("{{G|The " + displayName + " is complete.}}");
			return true;
		}

		/// <summary>
		/// Furnishes a finished plot from its design's own population table, the way vanilla huts
		/// are furnished (<c>ZoneBuilderSandbox.PlaceHut</c>'s own last step) &mdash; but only ever
		/// into interior cells the plot itself laid empty, never over anything.
		/// </summary>
		private static void Furnish(Zone Z, KingdomPlotRules.PlotRect Rect, string Table, string Id, string Key)
		{
			if (string.IsNullOrEmpty(Table) || Rect.Width <= 2 || Rect.Height <= 2)
			{
				return;
			}
			if (!TryGetSpec(Key, out var spec))
			{
				return;
			}
			List<Cell> open = new List<Cell>();
			for (int y = Rect.Y1 + 1; y <= Rect.Y2 - 1; y++)
			{
				for (int x = Rect.X1 + 1; x <= Rect.X2 - 1; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell != null && cell.IsEmpty() && cell.IsPassable())
					{
						open.Add(cell);
					}
				}
			}
			int rolls = KingdomPlotRules.ContentsRolls(spec.Size);
			for (int roll = 0; roll < rolls && open.Count > 0; roll++)
			{
				foreach (PopulationResult item in PopulationManager.Generate(Table, "zonetier", Z.NewTier.ToString()))
				{
					for (int n = 0; n < item.Number && open.Count > 0; n++)
					{
						Cell cell = open[0];
						open.RemoveAt(0);
						GameObject placed = GameObject.Create(item.Blueprint);
						if (placed == null)
						{
							continue;
						}
						placed.SetIntProperty(PlotPartProperty, 1);
						if (!string.IsNullOrEmpty(Id))
						{
							placed.SetStringProperty(PlotIdProperty, Id);
						}
						cell.AddObject(placed);
						if (placed.GetPart<LiquidVolume>() != null)
						{
							placed.SetIntProperty("KingdomStores", 1);
						}
					}
				}
			}
		}

		// --- Saying so --------------------------------------------------------------------

		private static void AnnounceOnce(KingdomSystem System, GameObject Marker, string Message)
		{
			if (Marker == null || Marker.GetStringProperty(BlockAnnouncedProperty) == Message)
			{
				return;
			}
			Marker.SetStringProperty(BlockAnnouncedProperty, Message);
			System?.Ledger.Note("{{K|" + Message + "}}");
			MessageQueue.AddPlayerMessage("{{K|" + Message + "}}");
		}
	}
}
