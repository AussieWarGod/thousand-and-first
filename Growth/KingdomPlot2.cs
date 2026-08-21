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
	/// <summary>
	/// The yielding mark, carried by a plot the founder deliberately staked in the ground the
	/// heart was surveyed for. It does nothing at all on its own &mdash; it is the sentence, kept
	/// where the founder can read it back.
	/// <para>
	/// Consent before cost, the carry-sign idiom: the ground is legal to build on and building
	/// there is never refused, but the promise made at the moment the ground was spoken for is
	/// readable on the thing forever, rather than living in a message that scrolled away.
	/// </para>
	/// <para>
	/// A brand-new part, so its serialized field layout is free (STANDARDS 1). It carries no
	/// fields at all, and no turn tick.
	/// </para>
	/// </summary>
	[Serializable]
	public class r_KingdomYielding : IPart
	{
		public override bool WantEvent(int ID, int cascade)
		{
			return base.WantEvent(ID, cascade) || ID == GetShortDescriptionEvent.ID;
		}

		public override bool HandleEvent(GetShortDescriptionEvent E)
		{
			E.Postfix.Append("\n{{rules|").Append(ThousandAndFirst.KingdomPlotRules.YieldingMark).Append("}}");
			return base.HandleEvent(E);
		}
	}

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
			RegisterSpec(Key, Plot, Open, Sky, Contents, null, null);
		}

		/// <summary>
		/// Registers one entry's plot attributes, the tier's own footprint and roof included. Call
		/// once per <c>&lt;building&gt;</c> element that parsed successfully, with the raw
		/// attribute strings; all of them may be null, which registers "not a plot".
		/// </summary>
		/// <param name="Key">The entry's <c>Key</c>. Blank keys are ignored.</param>
		/// <param name="Plot">Raw <c>Plot</c> attribute: the envelope of ground.</param>
		/// <param name="Open">Raw <c>Open</c> attribute.</param>
		/// <param name="Sky">Raw <c>Sky</c> attribute.</param>
		/// <param name="Contents">Raw <c>Contents</c> attribute.</param>
		/// <param name="Footprint">Raw <c>Footprint</c> attribute: the ground THIS TIER stands on
		/// inside the plot. Absent fills the plot, which is what every entry written before
		/// footprints existed does.</param>
		/// <param name="Roof">Raw <c>Roof</c> attribute.</param>
		public static void RegisterSpec(string Key, string Plot, string Open, string Sky, string Contents, string Footprint, string Roof)
		{
			if (string.IsNullOrEmpty(Key))
			{
				return;
			}
			if (!KingdomPlotRules.TryParsePlotAttributes(Key, Plot, Open, Sky, Contents, Footprint, Roof, out var spec, out var error))
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

		/// <summary>
		/// Low corner x of the FOOTPRINT: the ground the building itself stands on, inside the
		/// plot. Stamped separately from the plot rect because the two are different questions
		/// &mdash; the plot is the envelope the founder staked and never changes, the footprint is
		/// the current tier's own ground and grows when the work does.
		/// <para>
		/// Absent on anything raised before tiers declared footprints, which is exactly why
		/// <see cref="TryReadFootprint"/> falls back to the plot rect: a building that already
		/// stands filled its plot, and still does.
		/// </para>
		/// </summary>
		public const string FootX1Property = "KingdomFootX1";

		/// <summary>Low corner y of the footprint. See <see cref="FootX1Property"/>.</summary>
		public const string FootY1Property = "KingdomFootY1";

		/// <summary>High corner x of the footprint, inclusive. See <see cref="FootX1Property"/>.</summary>
		public const string FootX2Property = "KingdomFootX2";

		/// <summary>High corner y of the footprint, inclusive. See <see cref="FootX1Property"/>.</summary>
		public const string FootY2Property = "KingdomFootY2";

		/// <summary>
		/// The tier's roof state as an int, so the stamped value never depends on an enum's
		/// backing type. Absent is read through <see cref="RoofOf"/>, which derives the same three
		/// states the open and carved flags always meant.
		/// </summary>
		public const string PlotRoofProperty = "KingdomPlotRoof";

		/// <summary>Set once on a staked plan whose ground is blocked, so the reason is given
		/// once rather than every settlement pass (STANDARDS 7b).</summary>
		public const string BlockAnnouncedProperty = "KingdomPlotBlockSaid";

		/// <summary>Zone property carrying the rite ground's x, written where the rite was
		/// poured. Absent on a settlement founded before it was recorded, which simply has no
		/// rite seed for the heart.</summary>
		public const string RiteXProperty = "r_TAF_RiteX";

		/// <summary>Zone property carrying the rite ground's y. See <see cref="RiteXProperty"/>.</summary>
		public const string RiteYProperty = "r_TAF_RiteY";

		// --- The heart -------------------------------------------------------------------

		/// <summary>Zone property carrying the low corner x of the ground the heart was surveyed
		/// for at the founding rite. Absent means this zone has no surveyed heart, and every plot
		/// in it is sited exactly as it was before the survey existed.</summary>
		public const string SurveyX1Property = "r_TAF_HeartSurveyX1";

		/// <summary>Low corner y of the surveyed heart. See <see cref="SurveyX1Property"/>.</summary>
		public const string SurveyY1Property = "r_TAF_HeartSurveyY1";

		/// <summary>High corner x of the surveyed heart, inclusive.</summary>
		public const string SurveyX2Property = "r_TAF_HeartSurveyX2";

		/// <summary>High corner y of the surveyed heart, inclusive.</summary>
		public const string SurveyY2Property = "r_TAF_HeartSurveyY2";

		/// <summary>Zone property carrying which rung of the heart currently stands on the rite
		/// ground, one-based. Absent or zero means nothing has been raised there yet, and the rite
		/// ground counts for exactly one vote as it always did.</summary>
		public const string HeartRungProperty = "r_TAF_HeartRung";

		/// <summary>Marks the works and the building of the heart's own plot, so the one plot
		/// that grows with its rung is told apart from the forty that do not.</summary>
		public const string HeartPlotProperty = "r_TAF_HeartPlot";

		/// <summary>Marks a survey stake. Read as bare ground by <see cref="ReadObject"/>: a
		/// stake is a mark, never a claim, and an ordinary plot may be built straight over
		/// it.</summary>
		public const string HeartStakeProperty = "r_TAF_HeartStake";

		/// <summary>Marks the founder's own basin, set down where the first water was poured and
		/// never taken up again. Read as bare ground for the same reason a stake is, so every rung
		/// of the heart is raised around it rather than refused by it.</summary>
		public const string HeartRelicProperty = "r_TAF_HeartRelic";

		/// <summary>Marks a plot staked in surveyed heart ground: told at placement, carried in
		/// the plot's own description forever, and read by the ring call when the great work is
		/// finally called for that ground.</summary>
		public const string YieldingProperty = "r_TAF_Yielding";

		/// <summary>The stake driven at the corners of the surveyed ground.</summary>
		public const string SurveyStakeBlueprint = "r_KingdomHeartStake";

		/// <summary>The founder's basin, kept at the middle of every rung.</summary>
		public const string HeartRelicBlueprint = "r_KingdomFirstBasin";

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
			if (Object.GetIntProperty(HeartStakeProperty) == 1 || Object.GetIntProperty(HeartRelicProperty) == 1)
			{
				// A survey stake is the founder's ambition paced out, and the basin is what the
				// first water was poured from. Neither is an obstruction and neither is ever
				// cleared: reading them as bare ground is what lets ordinary plots be built over
				// surveyed ground (the mark is a preference, not a claim) and what lets every rung
				// of the heart be raised AROUND the basin rather than refused by it.
				return KingdomPlotRules.GroundKind.Bare;
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

		/// <summary>Stamps the current tier's own ground, and what stands over it, on the object
		/// that represents a plot. Read back by <see cref="TryReadFootprint"/> and
		/// <see cref="RoofOf"/>.</summary>
		public static void StampFootprint(GameObject Object, KingdomPlotRules.PlotRect Footprint, KingdomPlotRules.RoofState Roof)
		{
			if (Object == null)
			{
				return;
			}
			Object.SetIntProperty(FootX1Property, Footprint.X1);
			Object.SetIntProperty(FootY1Property, Footprint.Y1);
			Object.SetIntProperty(FootX2Property, Footprint.X2);
			Object.SetIntProperty(FootY2Property, Footprint.Y2);
			Object.SetIntProperty(PlotRoofProperty, (int)Roof);
		}

		/// <summary>
		/// The ground the building itself stands on. Falls back to the plot rect for anything
		/// raised before tiers declared footprints, which is the honest answer: it filled its
		/// plot, and it still does.
		/// </summary>
		/// <returns>False for an object that is not a plot at all.</returns>
		public static bool TryReadFootprint(GameObject Object, out KingdomPlotRules.PlotRect Footprint)
		{
			Footprint = default(KingdomPlotRules.PlotRect);
			if (Object == null)
			{
				return false;
			}
			if (Object.HasIntProperty(FootX2Property))
			{
				Footprint = new KingdomPlotRules.PlotRect(
					Object.GetIntProperty(FootX1Property),
					Object.GetIntProperty(FootY1Property),
					Object.GetIntProperty(FootX2Property),
					Object.GetIntProperty(FootY2Property));
				return true;
			}
			return TryReadRect(Object, out Footprint);
		}

		/// <summary>
		/// What stands over a plot. Derived rather than defaulted when nothing was stamped: the
		/// open and carved flags a works part already carries are the same three states roofs
		/// name, so a plot staked before roofs existed reads as exactly what it was staked as.
		/// </summary>
		public static KingdomPlotRules.RoofState RoofOf(GameObject Object)
		{
			if (Object == null)
			{
				return KingdomPlotRules.RoofState.Walled;
			}
			if (Object.HasIntProperty(PlotRoofProperty))
			{
				return (KingdomPlotRules.RoofState)Object.GetIntProperty(PlotRoofProperty);
			}
			r_KingdomPlotWorks works = Object.GetPart<r_KingdomPlotWorks>();
			if (works == null)
			{
				return KingdomPlotRules.RoofState.Walled;
			}
			return KingdomPlotRules.RoofOnGround(KingdomPlotRules.DefaultRoof(works.Open), works.Carved);
		}

		/// <summary>
		/// The heart this plot faces, which decides where its door is cut and which side of the
		/// plot the building fronts. A zone with no heart yet faces its own centre, so a first
		/// building is never sited by an answer nobody gave.
		/// </summary>
		public static void HeartFor(Zone Z, KingdomPlotRules.PlotRect Plot, out int X, out int Y)
		{
			bool hasRite = TryRiteGround(Z, out var riteX, out var riteY);
			// The rite ground's own weight rises with the rung standing on it, so the settled
			// centre is drawn back onto the great work as it rises rather than walking away from
			// it (KingdomPlotRules.HeartWeightForRung).
			if (KingdomPlotRules.TryHeart(KingdomLayout.ReadMarks(Z), hasRite, riteX, riteY, out var heartX, out var heartY, RiteWeight(Z)))
			{
				X = heartX;
				Y = heartY;
				return;
			}
			X = Plot.CenterX;
			Y = Plot.CenterY;
		}

		/// <summary>
		/// The ground one tier stands on inside a staked plot: the design's own footprint, sited
		/// against the heart-facing side so the yard lies behind the building. A tier that
		/// declares none fills the plot, exactly as every design did before footprints existed.
		/// </summary>
		public static KingdomPlotRules.PlotRect FootprintFor(KingdomPlotRules.PlotRect Plot, KingdomPlotRules.PlotSpec Spec, int HeartX, int HeartY)
		{
			if (Spec != null && !Spec.FillsPlot
				&& KingdomPlotRules.TryFootprintWithin(Plot, Spec.FootprintWidth, Spec.FootprintHeight, HeartX, HeartY, out var footprint))
			{
				return footprint;
			}
			return Plot;
		}

		/// <summary>
		/// The ground one rung of the heart stands on inside its plot: the tier's own footprint,
		/// centred on the RITE GROUND rather than sited against the heart-facing side.
		/// <para>
		/// This is what makes the rungs accrete. The ordinary rule puts a building on the side of
		/// its plot nearest the settled centre, which is right for a house and wrong for the one
		/// building the settled centre is measured from: a rung sited off-centre would not enclose
		/// the rung below it, and the kerb would end up outside the hall instead of under its
		/// floor. Centred on the rite ground, every rung contains the one before it, and the basin
		/// stays in the middle of all of them.
		/// </para>
		/// </summary>
		public static KingdomPlotRules.PlotRect HeartFootprintFor(Zone Z, KingdomPlotRules.PlotRect Plot, KingdomPlotRules.PlotSpec Spec)
		{
			if (Spec != null && !Spec.FillsPlot && TryRiteGround(Z, out var riteX, out var riteY)
				&& KingdomPlotRules.TryCentred(Plot, riteX, riteY, Spec.FootprintWidth, Spec.FootprintHeight, out var footprint))
			{
				return footprint;
			}
			return FootprintFor(Plot, Spec, Plot.CenterX, Plot.CenterY);
		}

		/// <summary>
		/// The yard of a standing plot: everything inside the plot the building does not stand on,
		/// recomputed from the current tier every time it is asked rather than stored anywhere.
		/// A tier that fills its plot has no yard OUTSIDE it, so the answer falls back to the
		/// building's own interior &mdash; which is the ground yard trades have always used, and
		/// is why nothing already standing changes.
		/// </summary>
		public static List<KingdomPlotRules.PlotRect> YardRects(GameObject Building)
		{
			List<KingdomPlotRules.PlotRect> bands = new List<KingdomPlotRules.PlotRect>();
			if (Building == null || !TryReadRect(Building, out var plot) || !TryReadFootprint(Building, out var footprint))
			{
				return bands;
			}
			bands = KingdomPlotRules.YardBands(plot, footprint);
			if (bands.Count == 0 && KingdomYardRules.TryYardInterior(footprint, out var interior))
			{
				bands.Add(interior);
			}
			return bands;
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

		// --- The heart's own ground -------------------------------------------------------

		/// <summary>The ground this zone's heart was surveyed for at the founding rite.</summary>
		/// <returns>False for a zone with no survey, which is every zone but the one the rite was
		/// poured in and every settlement founded before the survey shipped.</returns>
		public static bool TrySurveyedHeart(Zone Z, out KingdomPlotRules.PlotRect Survey)
		{
			Survey = default(KingdomPlotRules.PlotRect);
			if (Z == null)
			{
				return false;
			}
			if (!int.TryParse(Z.GetZoneProperty(SurveyX1Property, null), out var x1)
				|| !int.TryParse(Z.GetZoneProperty(SurveyY1Property, null), out var y1)
				|| !int.TryParse(Z.GetZoneProperty(SurveyX2Property, null), out var x2)
				|| !int.TryParse(Z.GetZoneProperty(SurveyY2Property, null), out var y2))
			{
				return false;
			}
			Survey = new KingdomPlotRules.PlotRect(x1, y1, x2, y2);
			return true;
		}

		/// <summary>Which rung of the heart stands on this zone's rite ground, one-based; zero
		/// when nothing has been raised there yet.</summary>
		public static int HeartRung(Zone Z)
		{
			if (Z == null || !int.TryParse(Z.GetZoneProperty(HeartRungProperty, null), out var rung))
			{
				return 0;
			}
			return (rung < 0) ? 0 : rung;
		}

		/// <summary>How many votes this zone's rite ground gets when the heart is reckoned: one
		/// on bare ground, and the standing rung's own weight once the great work is on it.</summary>
		public static int RiteWeight(Zone Z)
		{
			return KingdomPlotRules.HeartWeightForRung(HeartRung(Z));
		}

		/// <summary>
		/// Paces out the heart's whole future extent at the founding rite and stakes its first
		/// rung on the ground the water was poured on.
		/// <para>
		/// The survey COSTS NOTHING and CLAIMS NOTHING. It stamps four corner stakes a founder can
		/// walk up to and read, and a rect the layout grammar reads as a preference
		/// (<c>KingdomPlotRules.SurveyPenalty</c>) &mdash; the settlement will not volunteer to
		/// build in the heart's ground, and it never refuses to. A plot staked there anyway is
		/// marked yielding at placement and says so forever.
		/// </para>
		/// </summary>
		/// <param name="System">The realm, freshly founded.</param>
		/// <param name="Z">The zone the rite was poured in.</param>
		/// <param name="RiteX">Rite ground x.</param>
		/// <param name="RiteY">Rite ground y.</param>
		/// <returns>False when the zone has no interior wide enough to survey a great plot in, in
		/// which case nothing is stamped and the settlement simply has no surveyed heart.</returns>
		public static bool SurveyHeart(KingdomSystem System, Zone Z, int RiteX, int RiteY)
		{
			if (Z == null || !KingdomPlotRules.TrySurveyedHeart(RiteX, RiteY, Z.Width, Z.Height, out var survey))
			{
				return false;
			}
			Z.SetZoneProperty(SurveyX1Property, survey.X1.ToString());
			Z.SetZoneProperty(SurveyY1Property, survey.Y1.ToString());
			Z.SetZoneProperty(SurveyX2Property, survey.X2.ToString());
			Z.SetZoneProperty(SurveyY2Property, survey.Y2.ToString());
			PlaceHeartMark(Z, RiteX, RiteY, HeartRelicBlueprint, HeartRelicProperty);
			PlaceHeartMark(Z, survey.X1, survey.Y1, SurveyStakeBlueprint, HeartStakeProperty);
			PlaceHeartMark(Z, survey.X2, survey.Y1, SurveyStakeBlueprint, HeartStakeProperty);
			PlaceHeartMark(Z, survey.X1, survey.Y2, SurveyStakeBlueprint, HeartStakeProperty);
			PlaceHeartMark(Z, survey.X2, survey.Y2, SurveyStakeBlueprint, HeartStakeProperty);
			KingdomLog.Log("heart surveyed: " + survey.X1 + "," + survey.Y1 + " to " + survey.X2 + "," + survey.Y2
				+ " around rite " + RiteX + "," + RiteY);
			MessageQueue.AddPlayerMessage("{{W|" + KingdomPlotRules.SurveyLine(survey) + "}}");
			StakeHeartRung(System, Z, 1, survey, RiteX, RiteY);
			return true;
		}

		/// <summary>Sets one stake or the basin down on ground, marked so the plot machinery reads
		/// it as bare. Silently does nothing where the engine will not take the object, which is
		/// the honest answer for a mark nobody can see.</summary>
		private static void PlaceHeartMark(Zone Z, int X, int Y, string Blueprint, string Mark)
		{
			Cell cell = Z?.GetCell(X, Y);
			if (cell == null)
			{
				return;
			}
			GameObject placed = GameObject.Create(Blueprint);
			if (placed == null)
			{
				return;
			}
			placed.SetIntProperty(Mark, 1);
			cell.AddObject(placed);
		}

		/// <summary>
		/// Stakes one rung of the heart on the surveyed ground. Used once at the founding, for the
		/// basin; every rung above it climbs through the ordinary improvement machinery instead,
		/// which is what makes the heart's gates the same gates every other design answers to.
		/// </summary>
		/// <returns>The works object, or null when the rung's design is missing, its ground will
		/// not take it, or the engine refuses the object.</returns>
		public static GameObject StakeHeartRung(KingdomSystem System, Zone Z, int Rung, KingdomPlotRules.PlotRect Survey, int RiteX, int RiteY)
		{
			string key = KingdomPlotRules.HeartKeyForRung(Rung);
			if (key == null || Z == null || !KingdomData.TryGetBuilding(key, out var entry) || !TryGetSpec(key, out var spec))
			{
				return null;
			}
			if (!KingdomPlotRules.TryHeartRect(Survey, RiteX, RiteY, KingdomPlotRules.HeartSizeForRung(Rung), out var rect))
			{
				return null;
			}
			GroundGrid grid = new GroundGrid(Z);
			// The rite ground is not chosen by the plan; it is chosen by where the water was
			// poured, and the heart is laid on it whatever else is standing there. That is safe
			// because clearing a plot never takes down what the settlement may not take down --
			// ClearGround leaves every Held cell exactly as it found it -- and because the first
			// rung raises no wall at all. Anything inviolate simply stands inside the ring, and
			// the founder is told about it by name at the rung where it actually blocks something
			// (HeartGrowRefused).
			// Open water is the one exception, and it is fatal rather than awkward: a plot is
			// never laid over liquid and liquid is never filled in.
			for (int y = rect.Y1; y <= rect.Y2; y++)
			{
				for (int x = rect.X1; x <= rect.X2; x++)
				{
					if (grid.KindAt(x, y) == KingdomPlotRules.GroundKind.Liquid)
					{
						MessageQueue.AddPlayerMessage("{{K|" + KingdomPlotRules.RefuseLiquid(x, y) + " The basin is set down all the same, and the heart is laid when the ground is.}}");
						return null;
					}
				}
			}
			GameObject works = Stake(System, Z, rect, entry, spec, grid, null, KingdomPlotRules.IsUnderground(Z.Z));
			if (works != null)
			{
				works.SetIntProperty(HeartPlotProperty, 1);
			}
			return works;
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
			return TryFindRect(Z, System, Entry, Spec, (Spec == null) ? KingdomPlotRules.PlotSize.None : Spec.Size, Grid, Prefer, out Rect, out Outcome, out Refusal);
		}

		/// <summary>
		/// Finds the ground for one plot at a tier the founder chose, which is never smaller than
		/// the design's own but may be larger: staking wide is how a founder buys a building room
		/// to grow into and a yard to work in meanwhile. Otherwise identical to the overload above,
		/// which stakes exactly the ground the design asks for.
		/// </summary>
		/// <param name="Stake">The tier of plot to lay. <see cref="KingdomPlotRules.PlotSize.None"/>
		/// falls back to the design's own.</param>
		public static bool TryFindRect(Zone Z, KingdomSystem System, KingdomRules.BuildEntry Entry, KingdomPlotRules.PlotSpec Spec, KingdomPlotRules.PlotSize Stake, GroundGrid Grid, Cell Prefer, out KingdomPlotRules.PlotRect Rect, out KingdomLayoutRules.LayoutOutcome Outcome, out string Refusal)
		{
			Rect = default(KingdomPlotRules.PlotRect);
			Outcome = KingdomLayoutRules.LayoutOutcome.None;
			Refusal = null;
			if (Z == null || Entry == null || Spec == null || Grid == null)
			{
				Refusal = KingdomPlotRules.RefuseRoom((Spec == null) ? KingdomPlotRules.PlotSize.Small : Spec.Size);
				return false;
			}
			KingdomPlotRules.PlotSize staked = StakedSize(Spec, Stake);
			if (!KingdomPlotRules.TryInterior(Z.Width, Z.Height, out var interior)
				|| !KingdomPlotRules.TryDimensions(staked, out var plotWidth, out var plotHeight))
			{
				Refusal = KingdomPlotRules.RefuseRoom(staked);
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
					Refusal = KingdomPlotRules.RefuseRoom(staked);
				}
				return false;
			}
			KingdomLayoutRules.LayoutPurpose purpose = KingdomLayout.PurposeOfEntry(Entry);
			KingdomRules.Frontier edges = (System != null)
				? KingdomRules.FrontierEdges(Z.ZoneID, System.ClaimedZones)
				: KingdomRules.Frontier.None;
			bool hasSurvey = TrySurveyedHeart(Z, out var survey) && KingdomPlotRules.HeartRungOf(Entry.Key) == 0;
			Outcome = KingdomPlotRules.ChooseRect(purpose, staked, Z.Width, Z.Height, edges, marks, candidates,
				hasFounder, founderX, founderY, hasRite, riteX, riteY, out var index, hasSurvey, survey, RiteWeight(Z));
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
			return Commission(System, Z, Entry, SkinKey, KingdomPlotRules.PlotSize.None, out Failure);
		}

		/// <summary>
		/// Issues one plot-sized commission on ground of the founder's own choosing. Identical to
		/// the overload above in every check it runs, with one decision added: how much ground is
		/// staked. Never less than the design asks for, never more than the settlement has grown
		/// into, and the ceiling that choice sets is refused BY NAME later rather than quietly
		/// worked around (<see cref="GrowRefused"/>).
		/// </summary>
		/// <param name="Stake">The tier of plot to lay, from
		/// <see cref="KingdomPlotRules.StakeableSizes"/>.
		/// <see cref="KingdomPlotRules.PlotSize.None"/> stakes the design's own.</param>
		public static bool Commission(KingdomSystem System, Zone Z, KingdomRules.BuildEntry Entry, string SkinKey, KingdomPlotRules.PlotSize Stake, out string Failure)
		{
			Failure = null;
			if (System == null || Z == null || Entry == null || !TryGetSpec(Entry.Key, out var spec))
			{
				Failure = "No such design.";
				return false;
			}
			if (KingdomPlotRules.HeartRungOf(Entry.Key) > 0)
			{
				// The heart is founded, not commissioned. Its first rung is staked by the rite and
				// every rung above it climbs through the ordinary improvement machinery on the same
				// ground; a second one ordered across the zone would be a second heart, which is
				// the one thing this ladder is not.
				Failure = KingdomPlotRules.RefuseSecondHeart(System.SeatName);
				return false;
			}
			KingdomPlotRules.PlotSize staked = StakedSize(spec, Stake);
			if (!KingdomPlotRules.Allows(System.Stage, staked))
			{
				Failure = KingdomPlotRules.RefuseStage(staked, System.SeatName, System.Stage);
				return false;
			}
			bool carved = KingdomPlotRules.IsUnderground(Z.Z);
			if (carved && spec.RequiresSky)
			{
				Failure = KingdomPlotRules.RefuseSky(Entry.Name);
				return false;
			}
			if (KingdomPlotRules.RoofRefusesSky(spec))
			{
				// A tier that declared itself walled, for a design that needs weather. Refused by
				// name rather than raised into something that could never work.
				Failure = KingdomPlotRules.RefuseRoofSky(Entry.Name, spec.Roof);
				return false;
			}
			if (Entry.Defence <= 0 && CountBuilt(Z) >= KingdomRules.MaxBuildingsForStage(System.Stage))
			{
				Failure = "There is no more room in the plan. " + System.SeatName + " is as built-up as this ground allows, until it grows into something larger.";
				return false;
			}
			if (KingdomPlotRules.WouldExceedBudget(ReadPlots(Z), staked, Z.Width, Z.Height))
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
			if (!TryFindRect(Z, System, Entry, spec, staked, grid, null, out var rect, out var outcome, out var refusal))
			{
				Failure = refusal;
				return false;
			}
			// Qualified: this overload's own Stake parameter (the tier the founder chose) shadows
			// the Stake method by name inside this body.
			GameObject works = KingdomPlots.Stake(System, Z, rect, Entry, spec, grid, SkinKey, carved);
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
			MessageQueue.AddPlayerMessage("{{G|A " + KingdomPlotRules.SizeName(staked) + " plot is staked for the " + Entry.Name
				+ ((clause == null) ? "" : (" " + clause)) + ".}}");
			SayYielding(System, works.GetIntProperty(YieldingProperty) == 1, Entry.Name);
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
			HeartFor(Z, Rect, out var heartX, out var heartY);
			KingdomPlotRules.RoofState roof = KingdomPlotRules.RoofOnGround(Spec.Roof, Carved);
			bool heartRung = KingdomPlotRules.HeartRungOf(Entry.Key) > 0;
			KingdomPlotRules.PlotRect footprint = heartRung
				? HeartFootprintFor(Z, Rect, Spec)
				: FootprintFor(Rect, Spec, heartX, heartY);
			part.StartTick = The.Game.TimeTicks;
			// The whole PLOT is cleared and the FOOTPRINT is walled: staking wide is paid for in
			// clearing, earned back in material and yard, and never in a longer wall than the
			// building actually has.
			part.TotalTicks = KingdomPlotRules.RaiseTicks(
				KingdomCommission.CraftBuildTicks(Entry.BuildTicks, System.ZoneDistricts.Values),
				Grid.CellsOf(Rect), footprint, roof, Carved);
			part.StageApplied = (int)KingdomPlotRules.PlotStage.Staked;
			part.Open = Spec.Open;
			part.Carved = Carved;
			part.WallBlueprint = KingdomPlotRules.RaisesWalls(roof) ? KingdomPlotRules.WallBlueprintFor(System.Style, System.FoundingRegionName) : null;
			part.ContentsTable = Spec.Contents;
			part.StaffNeeded = Entry.Staff;
			part.ThresholdManning = KingdomRules.IsThresholdManning(Entry.Manning);
			if (Entry.Defence > 0)
			{
				bool hasTinkering = The.Player != null && The.Player.HasSkill("Tinkering");
				bool hasAdvancedTinkering = The.Player != null && The.Player.HasSkill("Tinkering_Tinker1");
				part.DefencePending = KingdomRules.WallDefence(Entry.Defence, System.FoundingTerrainBlueprint, System.FoundingRegionName, hasTinkering, hasAdvancedTinkering);
			}
			bool foundDoor = KingdomPlotRules.TryDoor(footprint, heartX, heartY, out var doorX, out var doorY);
			part.HasDoor = foundDoor && KingdomPlotRules.Encloses(roof);
			part.DoorX = doorX;
			part.DoorY = doorY;
			works.DisplayName = "plot: " + Entry.Name;
			// Consent before cost, at the moment the ground is spoken for: a plot the founder puts
			// down inside the ground the heart was surveyed for is marked yielding here, says so in
			// its own description from this moment on, and says so out loud in the sentence the
			// commission or the plan prints. The heart's own rungs are never marked -- the ground
			// is theirs.
			if (KingdomPlotRules.HeartRungOf(Entry.Key) == 0
				&& TrySurveyedHeart(Z, out var survey)
				&& KingdomPlotRules.OverlapArea(Rect, survey) > 0)
			{
				works.SetIntProperty(YieldingProperty, 1);
				works.RequirePart<r_KingdomYielding>();
			}
			works.SetStringProperty(PlotIdProperty, Entry.Key + "@" + Rect.X1 + "." + Rect.Y1 + "." + The.Game.TimeTicks);
			StampRect(works, Rect);
			StampFootprint(works, footprint, roof);
			works.SetStringProperty(KingdomUpgrade.BuildKeyProperty, Entry.Key);
			KingdomDesign.StageSkin(works, Entry, SkinKey);
			cell.AddObject(works);
			KingdomLog.Log("plot staked: " + Entry.Key + " " + Rect.X1 + "," + Rect.Y1 + " to " + Rect.X2 + "," + Rect.Y2
				+ " footprint " + footprint.X1 + "," + footprint.Y1 + " to " + footprint.X2 + "," + footprint.Y2
				+ " " + roof.ToString().ToLowerInvariant() + " over " + part.TotalTicks + " ticks");
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
				if (KingdomPlotRules.HeartRungOf(Entry.Key) > 0)
				{
					refusal = KingdomPlotRules.RefuseSecondHeart(System.SeatName);
					return;
				}
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
				if (KingdomPlotRules.RoofRefusesSky(spec))
				{
					refusal = KingdomPlotRules.RefuseRoofSky(Entry.Name, spec.Roof);
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
			bool yielding = false;
			KingdomSystem.Guard("plot from plan", delegate
			{
				GroundGrid grid = new GroundGrid(zone);
				if (!TryFindRect(zone, System, Entry, spec, grid, cell, out var rect, out _, out _))
				{
					return;
				}
				string skinKey = Marker.GetStringProperty(KingdomDesign.PlannedSkinProperty);
				// Read before the marker comes down and carried after the works stands, because a
				// plot measures its rect out of the marker's own cell and cannot leave it standing
				// while it does. Same fact the single-cell path transfers in one step
				// (KingdomPlanMarker.Realize), and it is what lets the chronicle quote a plan for a
				// house rather than only for a wall.
				string planQuote = KingdomCeremony.ReadPlanQuote(Marker);
				Marker.Destroy(null, Silent: true);
				GameObject works = Stake(System, zone, rect, Entry, spec, grid, skinKey, KingdomPlotRules.IsUnderground(zone.Z));
				KingdomCeremony.CarryPlanQuote(planQuote, works);
				staked = works != null;
				yielding = works != null && works.GetIntProperty(YieldingProperty) == 1;
			});
			if (staked)
			{
				KingdomChronicle.Record(System, "the ground staked at " + System.KingdomDisplayName + " was measured out for " + XRL.Language.Grammar.A(Entry.Name));
				System.Ledger.Note("{{G|The plan staked at " + System.KingdomDisplayName + " is under way: the ground for the " + Entry.Name + " is measured out.}}");
				MessageQueue.AddPlayerMessage("{{G|The plan staked at " + System.KingdomDisplayName + " is under way. The ground for the " + Entry.Name + " is measured out.}}");
				SayYielding(System, yielding, Entry.Name);
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

		// --- Staking foresight ------------------------------------------------------------

		/// <summary>The tier actually staked: the founder's choice, floored at the ground the
		/// design itself asks for. A plot is never staked smaller than the building on it.</summary>
		public static KingdomPlotRules.PlotSize StakedSize(KingdomPlotRules.PlotSpec Spec, KingdomPlotRules.PlotSize Stake)
		{
			if (Spec == null)
			{
				return Stake;
			}
			return (Stake == KingdomPlotRules.PlotSize.None || Stake < Spec.Size) ? Spec.Size : Stake;
		}

		/// <summary>
		/// Every tier a design will ever grow into, in order, with the ground each one stands on.
		/// This is what the founder is shown before the stake goes in: the whole chain's
		/// footprints, so staking wide or staking tight is a decision made with the ceiling in
		/// view rather than discovered years later.
		/// <para>
		/// Walks the improvement chain by key and stops the moment it repeats one, so a
		/// third-party chain that rings does not hang the commission screen. The catalogue
		/// validator reports the ring separately; this just refuses to walk it.
		/// </para>
		/// </summary>
		/// <returns>An empty list for a design that is not a plot at all.</returns>
		public static List<KingdomPlotRules.ChainStep> ChainOf(KingdomRules.BuildEntry Entry)
		{
			List<KingdomPlotRules.ChainStep> steps = new List<KingdomPlotRules.ChainStep>();
			List<string> walked = new List<string>();
			KingdomRules.BuildEntry at = Entry;
			while (at != null && !walked.Contains(at.Key))
			{
				walked.Add(at.Key);
				if (!TryGetSpec(at.Key, out var spec) || !KingdomPlotRules.TryFootprint(spec, out var width, out var height))
				{
					break;
				}
				steps.Add(new KingdomPlotRules.ChainStep(at.Key, at.Name, width, height, spec.Roof));
				if (!KingdomUpgrade.TryGetChain(at.Key, out var chain) || chain == null || !chain.Defined
					|| !KingdomData.TryGetBuilding(chain.SuccessorKey, out var next))
				{
					break;
				}
				at = next;
			}
			return steps;
		}

		/// <summary>The tiers of plot a founder may stake for this design right now, smallest
		/// first, for a picker. Empty when the design is not a plot or the settlement cannot lay
		/// one yet, in which case the ordinary stage refusal says why.</summary>
		public static List<KingdomPlotRules.PlotSize> StakeableSizes(KingdomSystem System, KingdomRules.BuildEntry Entry)
		{
			if (System == null || Entry == null || !TryGetSpec(Entry.Key, out var spec))
			{
				return new List<KingdomPlotRules.PlotSize>();
			}
			return KingdomPlotRules.StakeableSizes(spec.Size, System.Stage, ChainOf(Entry));
		}

		/// <summary>What the founder reads before choosing how much ground to stake: this plot's
		/// span, the whole chain's footprints, and where the ceiling falls. Null for a design that
		/// is not a plot.</summary>
		public static string ForesightFor(KingdomRules.BuildEntry Entry, KingdomPlotRules.PlotSize Stake)
		{
			if (Entry == null || !TryGetSpec(Entry.Key, out var spec))
			{
				return null;
			}
			return KingdomPlotRules.ForesightLine(StakedSize(spec, Stake), ChainOf(Entry));
		}

		// --- Growing in place -------------------------------------------------------------

		/// <summary>
		/// Whether the next tier has room on the ground this one was staked on, and what the
		/// founder is told when it does not. Two ways it can fail, and each names the thing that
		/// would lift it: the tier wants more ground than the plot holds, or the ground it would
		/// take is where a household's yard trade stands.
		/// <para>
		/// A yard work is never taken down to make room. The founder is told which trade is in the
		/// way and chooses &mdash; let it go, or leave the building as it is &mdash; because the
		/// trade was their decision and tidying it away silently would be the settlement making it
		/// for them.
		/// </para>
		/// </summary>
		/// <param name="Work">The standing work.</param>
		/// <param name="SuccessorKey">The design it would become.</param>
		/// <param name="Refusal">A founder-facing sentence when this returns true; null
		/// otherwise.</param>
		/// <returns>False for anything that is not a plot, for a successor that is not a plot, and
		/// for a tier that has room &mdash; all three of which leave the improvement alone.</returns>
		public static bool GrowRefused(GameObject Work, string SuccessorKey, out string Refusal)
		{
			Refusal = null;
			if (Work == null || string.IsNullOrEmpty(SuccessorKey) || !TryGetSpec(SuccessorKey, out var spec))
			{
				return false;
			}
			if (!TryReadRect(Work, out var plot) || !TryReadFootprint(Work, out var footprint))
			{
				return false;
			}
			if (!KingdomPlotRules.TryFootprint(spec, out var width, out var height))
			{
				return false;
			}
			string name = KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName);
			string successorName = KingdomUpgrade.DisplayNameOf(SuccessorKey);
			Zone zone = Work.CurrentZone;
			if (zone == null)
			{
				return false;
			}
			// The heart is the one plot whose GROUND grows with its rung. Every other design
			// climbs inside the envelope the founder staked; this one was surveyed for its whole
			// extent at the founding rite and takes the next ring of it each time it rises, so the
			// question is not "does the tier fit the plot" but "is the surveyed ground clear".
			if (IsHeartPlot(Work) && KingdomPlotRules.HeartRungOf(SuccessorKey) > 0)
			{
				return HeartGrowRefused(Work, zone, SuccessorKey, successorName, out Refusal);
			}
			HeartFor(zone, plot, out var heartX, out var heartY);
			if (!KingdomPlotRules.TryFootprintWithin(plot, width, height, heartX, heartY, out var grown))
			{
				Refusal = KingdomPlotRules.RefuseFootprint(successorName, width, height,
					KingdomPlotRules.SmallestPlotFor(plot.Width, plot.Height));
				return true;
			}
			if (!KingdomPlotRules.TakesNewGround(footprint, grown))
			{
				return false;
			}
			for (int y = grown.Y1; y <= grown.Y2; y++)
			{
				for (int x = grown.X1; x <= grown.X2; x++)
				{
					if (footprint.Contains(x, y))
					{
						continue;
					}
					Cell cell = zone.GetCell(x, y);
					if (cell == null)
					{
						continue;
					}
					foreach (GameObject item in cell.GetObjects())
					{
						if (item != null && item.GetIntProperty(KingdomYards.YardWorkProperty) == 1)
						{
							Refusal = KingdomPlotRules.RefuseYardWork(name, successorName, item.ShortDisplayNameStripped);
							return true;
						}
					}
				}
			}
			return false;
		}

		/// <summary>Whether one object is the heart's own plot &mdash; the works while it is being
		/// raised, or the building once it stands.</summary>
		public static bool IsHeartPlot(GameObject Object)
		{
			return Object != null && Object.GetIntProperty(HeartPlotProperty) == 1;
		}

		/// <summary>
		/// Whether one plot was staked in ground the heart was surveyed for, and told so at the
		/// time. The mark is a stored fact: this wave informs and steers with it, and the ring
		/// call that moves a yielding plot whole reads exactly this.
		/// </summary>
		public static bool IsYielding(GameObject Object)
		{
			return Object != null && Object.GetIntProperty(YieldingProperty) == 1;
		}

		/// <summary>
		/// Every plot in a zone carrying the yielding mark, works and finished buildings alike, in
		/// the engine's own object order so two reads of an unchanged zone agree.
		/// </summary>
		public static List<GameObject> FindYielding(Zone Z)
		{
			List<GameObject> found = new List<GameObject>();
			if (Z == null)
			{
				return found;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (IsYielding(item) && TryReadRect(item, out _))
				{
					found.Add(item);
				}
			}
			return found;
		}

		/// <summary>
		/// The ground one rung of the heart would stand on: that rung's tier, centred on the rite
		/// ground and slid whole until it lies inside the ground surveyed at the founding.
		/// </summary>
		/// <returns>False when this zone has no survey, no rite ground, or no room for the
		/// rung.</returns>
		public static bool TryHeartRectFor(Zone Z, int Rung, out KingdomPlotRules.PlotRect Rect)
		{
			Rect = default(KingdomPlotRules.PlotRect);
			if (!TrySurveyedHeart(Z, out var survey) || !TryRiteGround(Z, out var riteX, out var riteY))
			{
				return false;
			}
			return KingdomPlotRules.TryHeartRect(survey, riteX, riteY, KingdomPlotRules.HeartSizeForRung(Rung), out Rect);
		}

		/// <summary>
		/// Whether the heart's next rung has ground to climb into, and the sentence the founder is
		/// owed when it does not. Two things can stand in the way, and both are named: another
		/// plot laid inside the surveyed ground, and anything the settlement may not take down.
		/// <para>
		/// A plot marked YIELDING is exactly the first case, and this is where the mark's promise
		/// comes due &mdash; this wave says so by name and stops. Moving it whole is the ring call,
		/// which waits on the relocation verb.
		/// </para>
		/// </summary>
		private static bool HeartGrowRefused(GameObject Work, Zone Z, string SuccessorKey, string SuccessorName, out string Refusal)
		{
			Refusal = null;
			int rung = KingdomPlotRules.HeartRungOf(SuccessorKey);
			if (!TryHeartRectFor(Z, rung, out var grown))
			{
				Refusal = KingdomPlotRules.RefuseHeartRoom(SuccessorName);
				return true;
			}
			string id = Work.GetStringProperty(PlotIdProperty);
			foreach (GameObject item in Z.GetObjects())
			{
				if (item == null || item == Work || !TryReadRect(item, out var laid))
				{
					continue;
				}
				// The heart's own earlier rungs are the ground it is growing out of, never an
				// obstruction in it.
				if (!string.IsNullOrEmpty(id) && item.GetStringProperty(PlotIdProperty) == id)
				{
					continue;
				}
				if (KingdomPlotRules.Overlaps(grown, KingdomPlotRules.Reserved(laid)))
				{
					string what = KingdomDesign.ReferenceFor(item, item.ShortDisplayNameStripped);
					Refusal = IsYielding(item)
						? KingdomPlotRules.RefuseHeartYielding(SuccessorName, what)
						: KingdomPlotRules.RefuseHeartGround(SuccessorName, what);
					return true;
				}
			}
			// Walked by hand rather than through GroundGrid, because the grid reads the heart's
			// own standing rung -- its building, its walls, its floor -- as ground that refuses a
			// plot, which is correct for every other plot and exactly wrong for the one growing
			// out of it.
			for (int y = grown.Y1; y <= grown.Y2; y++)
			{
				for (int x = grown.X1; x <= grown.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null)
					{
						Refusal = KingdomPlotRules.RefuseHeartRoom(SuccessorName);
						return true;
					}
					foreach (GameObject item in cell.GetObjects())
					{
						if (item == null || item == Work || item.IsCreature || item.IsPlayer())
						{
							continue;
						}
						if (!string.IsNullOrEmpty(id) && item.GetStringProperty(PlotIdProperty) == id)
						{
							continue;
						}
						if (!KingdomPlotRules.Refuses(ReadObject(item)))
						{
							continue;
						}
						Refusal = KingdomPlotRules.RefuseHeartGround(SuccessorName, KingdomDesign.ReferenceFor(item, item.ShortDisplayNameStripped));
						return true;
					}
				}
			}
			return false;
		}

		/// <summary>
		/// Carries a plot across an improvement and stamps the new tier's footprint inside it. The
		/// plot itself never moves: the ground the founder staked is the ground the building keeps,
		/// and the yard is whatever the grown footprint leaves.
		/// <para>
		/// Called with the predecessor still standing, so everything the ground was recorded as is
		/// still readable. A single-cell design carries nothing and this does nothing.
		/// </para>
		/// </summary>
		public static void GrowInPlace(GameObject Predecessor, GameObject Successor, string SuccessorKey)
		{
			if (Predecessor == null || Successor == null || !TryReadRect(Predecessor, out var plot))
			{
				return;
			}
			string id = Predecessor.GetStringProperty(PlotIdProperty);
			if (!string.IsNullOrEmpty(id))
			{
				Successor.SetStringProperty(PlotIdProperty, id);
			}
			if (Predecessor.GetIntProperty(YieldingProperty) == 1)
			{
				// The mark is a fact about the ground, not about the building standing on it, so
				// it survives every improvement raised there. Told once, readable forever.
				Successor.SetIntProperty(YieldingProperty, 1);
				Successor.RequirePart<r_KingdomYielding>();
			}
			Zone zone = Predecessor.CurrentZone ?? Successor.CurrentZone;
			// The heart's plot GROWS with its rung: the next rung takes the next ring of the
			// ground surveyed at the founding rite, and the rung below it stays underfoot -- the
			// kerb becomes the hall's floor, and the basin stands in the middle of all of it.
			// Every other plot keeps exactly the envelope the founder staked.
			bool heart = IsHeartPlot(Predecessor) && KingdomPlotRules.HeartRungOf(SuccessorKey) > 0;
			if (heart)
			{
				Successor.SetIntProperty(HeartPlotProperty, 1);
				if (TryHeartRectFor(zone, KingdomPlotRules.HeartRungOf(SuccessorKey), out var climbed))
				{
					plot = climbed;
				}
			}
			StampRect(Successor, plot);
			KingdomPlotRules.PlotRect old = TryReadFootprint(Predecessor, out var read) ? read : plot;
			KingdomPlotRules.RoofState roof = RoofOf(Predecessor);
			if (zone == null || !TryGetSpec(SuccessorKey, out var spec))
			{
				// Nothing known about what it became: carry forward only what was actually
				// recorded. A building raised before footprints existed has no roof stamped on it
				// and gets none invented for it -- it filled its plot, and it still does.
				if (Predecessor.HasIntProperty(PlotRoofProperty))
				{
					StampFootprint(Successor, old, roof);
				}
				return;
			}
			HeartFor(zone, plot, out var heartX, out var heartY);
			KingdomPlotRules.RoofState grownRoof = KingdomPlotRules.RoofOnGround(spec.Roof, KingdomPlotRules.IsUnderground(zone.Z));
			KingdomPlotRules.PlotRect grown = heart
				? HeartFootprintFor(zone, plot, spec)
				: FootprintFor(plot, spec, heartX, heartY);
			StampFootprint(Successor, grown, grownRoof);
			KingdomSystem system = The.Game?.RequireSystem<KingdomSystem>();
			// The settlement's wall material is derived and not stored, exactly as it is when a
			// plot is first raised, so a grown building is walled in the same stone as its
			// neighbours whether or not anybody wrote that stone down.
			string wall = (system == null) ? null : KingdomPlotRules.WallBlueprintFor(system.Style, system.FoundingRegionName);
			// The heart alone keeps the rung below it standing. Every other improvement takes down
			// the walls that end up inside the bigger building, because one building has one
			// enclosure; the heart is not one building growing but four layers accreting, and the
			// moot hall is meant to stand inside the great court, beams, door and all.
			Restamp(zone, id, wall, old, grown, grownRoof, heartX, heartY, heart);
		}

		/// <summary>
		/// Raises the enclosure the grown tier needs and takes down the part of the old one that
		/// now stands inside the room. Only ever touches objects this plot itself created and
		/// marked, which is the one thing the protection law lets a kingdom system take down: a
		/// wall the settlement raised on the settlement's own plot, now in the middle of a bigger
		/// building. Anything else standing on the ground the building grew onto stops the cell
		/// being built on rather than being cleared out of the way.
		/// <para>
		/// <paramref name="KeepInner"/> is the heart's exception and nothing else's: its rungs
		/// ACCRETE, so the rung below is left standing inside the rung above rather than taken
		/// down to make one room of it.
		/// </para>
		/// </summary>
		private static void Restamp(Zone Z, string Id, string Wall, KingdomPlotRules.PlotRect Old, KingdomPlotRules.PlotRect Grown, KingdomPlotRules.RoofState Roof, int HeartX, int HeartY, bool KeepInner = false)
		{
			for (int y = Old.Y1; y <= Old.Y2 && !KeepInner; y++)
			{
				for (int x = Old.X1; x <= Old.X2; x++)
				{
					if (Grown.Contains(x, y) && Grown.IsBorder(x, y))
					{
						continue;
					}
					TakeDownEnclosure(Z.GetCell(x, y), Id);
				}
			}
			if (!KingdomPlotRules.Encloses(Roof))
			{
				return;
			}
			bool hasDoor = KingdomPlotRules.TryDoor(Grown, HeartX, HeartY, out var doorX, out var doorY);
			for (int y = Grown.Y1; y <= Grown.Y2; y++)
			{
				for (int x = Grown.X1; x <= Grown.X2; x++)
				{
					Cell cell = Z.GetCell(x, y);
					if (cell == null || BlockedForPlot(cell))
					{
						continue;
					}
					bool border = Grown.IsBorder(x, y);
					if (border && hasDoor && x == doorX && y == doorY)
					{
						PlaceForPlot(cell, DoorBlueprint, Id);
						continue;
					}
					if (border)
					{
						if (KingdomPlotRules.RaisesWalls(Roof))
						{
							PlaceForPlot(cell, Wall, Id);
						}
						continue;
					}
					PlaceForPlot(cell, FloorBlueprint, Id);
				}
			}
		}

		/// <summary>Takes down this plot's own walls, doors, and posts in one cell, and nothing
		/// else standing there: the floor stays because a floor outside a grown building is a swept
		/// yard, and furniture, yard trades, and everything the founder put down are left exactly
		/// where they are.</summary>
		private static void TakeDownEnclosure(Cell C, string Id)
		{
			if (C == null || string.IsNullOrEmpty(Id))
			{
				return;
			}
			List<GameObject> standing = new List<GameObject>(C.GetObjects());
			for (int i = 0; i < standing.Count; i++)
			{
				GameObject item = standing[i];
				if (item == null || item.GetIntProperty(PlotPartProperty) != 1
					|| item.GetStringProperty(PlotIdProperty) != Id
					|| item.GetIntProperty(KingdomYards.YardWorkProperty) == 1)
				{
					continue;
				}
				if (item.IsWall() || item.IsDoor() || item.Blueprint == FrameBlueprint)
				{
					item.Destroy(null, Silent: true);
				}
			}
		}

		/// <summary>Whether anything in a cell stops the settlement building on it: something the
		/// founder owns or placed, another work, open water, or a household's own yard trade. This
		/// plot's own floor and walls do not, which is what lets a grown tier build through what
		/// its smaller self left standing.</summary>
		private static bool BlockedForPlot(Cell C)
		{
			if (C == null)
			{
				return true;
			}
			foreach (GameObject item in C.GetObjects())
			{
				if (item == null || item.IsCreature || item.IsPlayer())
				{
					continue;
				}
				if (item.GetIntProperty(KingdomYards.YardWorkProperty) == 1)
				{
					return true;
				}
				if (item.GetIntProperty(PlotPartProperty) == 1)
				{
					continue;
				}
				if (ReadObject(item) != KingdomPlotRules.GroundKind.Bare)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>Places one object and marks it as this plot's, so a later striking takes down
		/// exactly what the settlement raised and nothing else. Does nothing when the cell already
		/// holds one of this plot's own objects of that blueprint, so re-stamping a grown tier
		/// never doubles a wall or lays a second floor.</summary>
		private static GameObject PlaceForPlot(Cell C, string Blueprint, string Id)
		{
			if (C == null || string.IsNullOrEmpty(Blueprint))
			{
				return null;
			}
			foreach (GameObject item in C.GetObjects())
			{
				if (item != null && item.Blueprint == Blueprint)
				{
					return null;
				}
			}
			GameObject placed = GameObject.Create(Blueprint);
			if (placed == null)
			{
				return null;
			}
			placed.SetIntProperty(PlotPartProperty, 1);
			if (!string.IsNullOrEmpty(Id))
			{
				placed.SetStringProperty(PlotIdProperty, Id);
			}
			C.AddObject(placed);
			return placed;
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
			KingdomPlotRules.PlotRect plot = Works.Rect();
			KingdomPlotRules.PlotRect footprint = TryReadFootprint(parent, out var stamped) ? stamped : plot;
			KingdomPlotRules.RoofState roof = RoofOf(parent);
			switch (Stage)
			{
				case KingdomPlotRules.PlotStage.Cleared:
					ClearGround(Works, zone, plot, footprint, roof);
					break;
				case KingdomPlotRules.PlotStage.Frame:
					RaiseFrame(Works, zone, footprint, roof);
					break;
				case KingdomPlotRules.PlotStage.Walls:
					RaiseWalls(Works, zone, footprint, roof);
					break;
				case KingdomPlotRules.PlotStage.Done:
					return Finish(Works, zone, plot, footprint, roof);
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
		private static void ClearGround(r_KingdomPlotWorks Works, Zone Z, KingdomPlotRules.PlotRect Plot, KingdomPlotRules.PlotRect Footprint, KingdomPlotRules.RoofState Roof)
		{
			int[] yields = new int[5];
			// Carving cuts the room, not the hill. Everything on the building's own edge is left
			// exactly where it stands, because underground that rock IS the enclosure -- which is
			// the whole of the bargain that makes the doubled clearing cost worth paying. Only the
			// doorway is cut through it. The yard around it is cleared like any other ground, and
			// pays in stone for being cut out of rock.
			bool carveOnly = Roof == KingdomPlotRules.RoofState.Carved && Footprint.Width > 2 && Footprint.Height > 2;
			for (int y = Plot.Y1; y <= Plot.Y2; y++)
			{
				for (int x = Plot.X1; x <= Plot.X2; x++)
				{
					if (carveOnly && Footprint.IsBorder(x, y) && !(Works.HasDoor && x == Works.DoorX && y == Works.DoorY))
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

		private static void RaiseFrame(r_KingdomPlotWorks Works, Zone Z, KingdomPlotRules.PlotRect Rect, KingdomPlotRules.RoofState Roof)
		{
			if (!KingdomPlotRules.RaisesWalls(Roof))
			{
				return;
			}
			PlaceMarked(Works, Z.GetCell(Rect.X1, Rect.Y1), FrameBlueprint);
			PlaceMarked(Works, Z.GetCell(Rect.X2, Rect.Y1), FrameBlueprint);
			PlaceMarked(Works, Z.GetCell(Rect.X1, Rect.Y2), FrameBlueprint);
			PlaceMarked(Works, Z.GetCell(Rect.X2, Rect.Y2), FrameBlueprint);
		}

		private static void RaiseWalls(r_KingdomPlotWorks Works, Zone Z, KingdomPlotRules.PlotRect Rect, KingdomPlotRules.RoofState Roof)
		{
			if (!KingdomPlotRules.Encloses(Roof))
			{
				// A field, a yard, a salt-pan, a reservoir, a tent: nothing the settlement raises
				// stands round these. Same rect discipline, no enclosure and no floor.
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
						if (KingdomPlotRules.RaisesWalls(Roof) && !string.IsNullOrEmpty(Works.WallBlueprint))
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
			return PlaceForPlot(C, Blueprint, Works.ParentObject?.GetStringProperty(PlotIdProperty));
		}

		/// <summary>
		/// Finishes the plot: furnishes the interior from the design's own contents table the way
		/// vanilla huts furnish, raises the object that stands for the building, hands it every
		/// property the rest of the settlement reads a work by, and takes the works down.
		/// </summary>
		private static bool Finish(r_KingdomPlotWorks Works, Zone Z, KingdomPlotRules.PlotRect Rect, KingdomPlotRules.PlotRect Footprint, KingdomPlotRules.RoofState Roof)
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
			bool heart = parent.GetIntProperty(HeartPlotProperty) == 1;
			bool yielding = parent.GetIntProperty(YieldingProperty) == 1;
			int staff = Works.StaffNeeded;
			bool threshold = Works.ThresholdManning;
			string contents = Works.ContentsTable;
			string displayName = Works.DisplayName ?? entry.Name;
			// Read before the works comes down, not after: everything the founder chose when they
			// staked this ground rides on the works object, and the works is about to stop being a
			// thing to read from. The plan quote and the due tick are the raising ceremony's own
			// two facts, and they are read here for exactly the same reason.
			string planQuote = KingdomCeremony.ReadPlanQuote(parent);
			long completeTick = Works.StartTick + Works.TotalTicks;
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
			if (heart)
			{
				building.SetIntProperty(HeartPlotProperty, 1);
			}
			if (yielding)
			{
				building.SetIntProperty(YieldingProperty, 1);
				building.RequirePart<r_KingdomYielding>();
			}
			if (!string.IsNullOrEmpty(id))
			{
				building.SetStringProperty(PlotIdProperty, id);
			}
			StampRect(building, Rect);
			StampFootprint(building, Footprint, Roof);
			if (building.GetPart<LiquidVolume>() != null)
			{
				building.SetIntProperty("KingdomStores", 1);
			}
			else if (KingdomRules.IsCivicLarderBlueprint(entry.Blueprint))
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
			Furnish(Z, Footprint, contents, id, entry.Key);
			KingdomLog.Log("plot complete: " + displayName + " (" + entry.Blueprint + ") over " + Rect.X1 + "," + Rect.Y1 + " to " + Rect.X2 + "," + Rect.Y2);
			KingdomSystem system = The.Game.RequireSystem<KingdomSystem>();
			if (system.Founded)
			{
				// The same close a single-cell scaffold has always had (r_KingdomScaffold.Complete):
				// attended, the crew gathers and a measure of water is shared; unattended, the
				// homecoming tells it. A house is not a lesser thing to raise than a palisade.
				KingdomCeremony.OnBuildingRaised(system, cell, displayName, completeTick, planQuote);
				// And the heart's own rung gets the chronicle's own voice on top of it: the same
				// crew, the same shared water, one more sentence about what the ground has become.
				KingdomCeremonyHeart.OnRungRaised(system, Z, entry.Key, heart);
			}
			else
			{
				MessageQueue.AddPlayerMessage("{{G|The " + displayName + " is complete.}}");
			}
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

		/// <summary>
		/// Says the yielding mark out loud at the moment the ground is spoken for, and files it in
		/// the ledger so a founder who was elsewhere reads it too. Told UP FRONT and once: the plot
		/// carries the same sentence in its own description from here on, so consent is given
		/// knowing what was promised and can be read back at any time.
		/// </summary>
		private static void SayYielding(KingdomSystem System, bool Yielding, string Name)
		{
			if (!Yielding || string.IsNullOrEmpty(Name))
			{
				return;
			}
			string line = KingdomPlotRules.YieldingLine(Name);
			MessageQueue.AddPlayerMessage("{{W|" + line + "}}");
			System?.Ledger.Note("{{W|" + line + "}}");
		}

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
