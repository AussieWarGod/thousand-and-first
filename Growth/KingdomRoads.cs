using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	/// <summary>
	/// The engine-coupled half of worn ground: reading which errands a settlement's own shape
	/// implies, laying the day's walking on the cells those errands cross, letting the ground
	/// show it, and &mdash; only when the founder says so &mdash; paving a path in the material
	/// the settlement builds its walls in.
	/// <para>
	/// No road is ever drawn. The plot grammar already keeps a lane around every plot
	/// (<see cref="KingdomPlotRules.RoadMargin"/>) and the gap between two reserved rects IS the
	/// road; all this does is notice which gaps people cross. Everything resolves on the attended
	/// <c>ZoneActivatedEvent</c> pass out of a stored tick stamp, so a settlement wears its ways
	/// at exactly the rate it was lived in, and a founder who was away for a season finds three
	/// days' worth of walking &mdash; the same absence cap as everything else
	/// (<c>KingdomRules.HeartbeatDays</c>).
	/// </para>
	/// <para>
	/// The protection law (STANDARDS 7) is the shape of this file, not a check inside it. Wear
	/// only ever ADDS a floor object of ours to a cell that <c>KingdomPlots.ReadGround</c> calls
	/// bare, that nobody owns, that holds no liquid, and that lies in no plot &mdash; and the only
	/// objects it ever destroys are its own, marked with <see cref="PathStateProperty"/>. The
	/// ground the cell already had is never cleared, exactly as vanilla's own
	/// <c>RoadBuilder</c> and <c>JoppaOutskirts</c> add a <c>DirtPath</c> over what is there
	/// rather than replacing it (<c>RoadBuilder.cs:168</c>, <c>JoppaOutskirts.cs:332</c>). Nothing
	/// here ever calls <c>PlaceHut</c> or <c>ClearRect</c>.
	/// </para>
	/// <para>
	/// State lives on the zone, not on the settlement, and deliberately: ways are a property of
	/// ground, and a realm's second city has ground of its own. Zone properties are serialized by
	/// <c>ZoneManager</c> (<c>ZoneManager.cs:507-516</c> and <c>:677-688</c>) and are the idiom
	/// the rite ground already uses (<c>KingdomPlots.RiteXProperty</c>), so this system adds no
	/// serialized field to any part or system and cannot move anyone's save layout.
	/// </para>
	/// </summary>
	public static class KingdomRoads
	{
		/// <summary>Zone property carrying the worn-ground tally, written by
		/// <c>KingdomRoadRules.Encode</c>.</summary>
		public const string TallyProperty = "r_TAF_Roads";

		/// <summary>Zone property carrying the tick the ground was last walked, as a string
		/// because zone properties hold strings and a tick is not one.</summary>
		public const string WalkedProperty = "r_TAF_RoadsWalked";

		/// <summary>Zone property set once the founder has been told the tally is full, so the
		/// reason is given once per stall rather than once per visit (STANDARDS 7b). Cleared the
		/// moment the tally has room again.</summary>
		public const string FullSaidProperty = "r_TAF_RoadsFull";

		/// <summary>Zone property carrying the highest rung of the ladder whose line has already
		/// been given, so the settlement remarks on its own paths once rather than every time a
		/// ninth cell crosses the same threshold.</summary>
		public const string SaidProperty = "r_TAF_RoadsSaid";

		/// <summary>
		/// Property marking a floor this system laid, and which rung of
		/// <c>KingdomRoadRules.WearState</c> it stands for. The whole of the mod's claim over
		/// these objects: nothing without this property is ever removed here, and everything with
		/// it was created here.
		/// </summary>
		public const string PathStateProperty = "KingdomPath";

		/// <summary>Vanilla's packed dirt floor, laid where the grass has gone
		/// (<c>ZoneTerrain.xml:932</c>).</summary>
		public const string TroddenBlueprint = "DirtFloor";

		/// <summary>Vanilla's dirt path, laid where a way has become a way
		/// (<c>ZoneTerrain.xml:937</c>) &mdash; the same floor <c>PlaceHut</c> lays inside a
		/// vanilla village hut.</summary>
		public const string PathBlueprint = "DirtPath";

		/// <summary>Homes one pass reasons about, so building the errand list stays cheap in a
		/// city of forty roofs.</summary>
		public const int MaxHomesConsidered = 12;

		/// <summary>Works one pass reasons about. See <see cref="MaxHomesConsidered"/>.</summary>
		public const int MaxWorksConsidered = 12;

		/// <summary>Plots one pass reasons about, for the doorway errands.</summary>
		public const int MaxPlotsConsidered = 12;

		/// <summary>Whether ground wears at all. Its own toggle, because a player who likes the
		/// grass exactly as the world generator drew it should be able to keep it (STANDARDS 3).
		/// Defaults to on when the option is missing, so a build whose XML has not landed yet
		/// behaves like the shipped one.</summary>
		public static bool Enabled => Options.GetOption("r_TAF_OptionRoads") != "No";

		/// <summary>One errand: two ends and a reason.</summary>
		private struct Errand
		{
			public int FromX;

			public int FromY;

			public int ToX;

			public int ToY;

			public KingdomRoadRules.RouteKind Kind;

			public Errand(int FromX, int FromY, int ToX, int ToY, KingdomRoadRules.RouteKind Kind)
			{
				this.FromX = FromX;
				this.FromY = FromY;
				this.ToX = ToX;
				this.ToY = ToY;
				this.Kind = Kind;
			}
		}

		// --- Reading and writing the zone's own record ------------------------------------

		/// <summary>The worn-ground tally of a zone. Never null; an unreadable or absent tally
		/// reads as an empty one and says so in the log.</summary>
		/// <param name="Z">The zone. Null yields an empty tally.</param>
		public static List<KingdomRoadRules.WornCell> ReadTally(Zone Z)
		{
			if (Z == null)
			{
				return new List<KingdomRoadRules.WornCell>();
			}
			if (!KingdomRoadRules.TryDecode(Z.GetZoneProperty(TallyProperty, null), out var cells, out var error) && error != null)
			{
				KingdomLog.Log(error);
			}
			return cells;
		}

		/// <summary>Writes a tally back to the zone. An empty tally writes the empty string, so a
		/// settlement nobody walks costs one short property and no bookkeeping.</summary>
		/// <param name="Z">The zone. Null does nothing.</param>
		/// <param name="Cells">The tally. Null writes the empty string.</param>
		public static void WriteTally(Zone Z, IList<KingdomRoadRules.WornCell> Cells)
		{
			if (Z == null)
			{
				return;
			}
			Z.SetZoneProperty(TallyProperty, KingdomRoadRules.Encode(Cells));
		}

		private static long ReadTick(Zone Z, string Property)
		{
			if (Z == null)
			{
				return 0L;
			}
			return long.TryParse(Z.GetZoneProperty(Property, null), out var ticks) ? ticks : 0L;
		}

		private static void WriteTick(Zone Z, string Property, long Ticks)
		{
			Z?.SetZoneProperty(Property, Ticks.ToString());
		}

		// --- Reading ground ---------------------------------------------------------------

		/// <summary>The floor this system laid on a cell, or null when it laid none.</summary>
		/// <param name="C">The cell. Null answers null.</param>
		public static GameObject OurFloor(Cell C)
		{
			if (C == null)
			{
				return null;
			}
			foreach (GameObject item in C.GetObjects())
			{
				if (item != null && item.GetIntProperty(PathStateProperty) > 0)
				{
					return item;
				}
			}
			return null;
		}

		/// <summary>The rung a cell has already been brought to by this system.</summary>
		public static KingdomRoadRules.WearState AppliedState(Cell C)
		{
			GameObject floor = OurFloor(C);
			if (floor == null)
			{
				return KingdomRoadRules.WearState.Untouched;
			}
			int state = floor.GetIntProperty(PathStateProperty);
			if (state < (int)KingdomRoadRules.WearState.Untouched || state > (int)KingdomRoadRules.WearState.Paved)
			{
				return KingdomRoadRules.WearState.Untouched;
			}
			return (KingdomRoadRules.WearState)state;
		}

		/// <summary>
		/// Whether feet may be allowed to show on this cell at all.
		/// <para>
		/// Deliberately stricter than "empty": the ground must be what
		/// <c>KingdomPlots.ReadGround</c> calls bare &mdash; open ground, or a floor, or this
		/// system's own earlier work &mdash; it must hold no liquid, nothing on it may be owned
		/// by anybody, and it must lie outside every plot, because the floor inside a building
		/// belongs to the building. Anything else and the ground is left exactly as it is.
		/// </para>
		/// </summary>
		/// <param name="C">The cell. Null is never wearable.</param>
		/// <param name="Plots">Plots laid in this zone, from <c>KingdomPlots.ReadPlots</c>. Null
		/// reads as none laid.</param>
		public static bool Wearable(Cell C, IList<KingdomPlotRules.PlotRect> Plots)
		{
			if (C == null)
			{
				return false;
			}
			// Cheapest question first: the ground inside a plot belongs to the building standing
			// on it, and no cell of a plot is ever worn however many people cross it.
			if (Plots != null)
			{
				for (int i = 0; i < Plots.Count; i++)
				{
					if (Plots[i].Contains(C.X, C.Y))
					{
						return false;
					}
				}
			}
			if (KingdomPlots.ReadGround(C, out _) != KingdomPlotRules.GroundKind.Bare)
			{
				return false;
			}
			foreach (GameObject item in C.GetObjects())
			{
				if (item == null || item.IsCreature)
				{
					continue;
				}
				if (item.GetIntProperty(PathStateProperty) == (int)KingdomRoadRules.WearState.Paved)
				{
					// Paved ground is finished ground. It never accrues again, and nothing is
					// ever laid on top of it.
					return false;
				}
				if (item.IsOwned())
				{
					// ReadGround reads a floor before it reads ownership, so a floor somebody
					// else laid would come back bare. Nothing anyone's name is on is walked over.
					return false;
				}
			}
			return true;
		}

		/// <summary>Whether an errand may be walked through a cell. Solid things turn feet aside;
		/// people do not, because a settler standing in a lane is standing in a lane and will move
		/// off it.</summary>
		public static bool Walkable(Cell C)
		{
			if (C == null)
			{
				return false;
			}
			if (C.HasObjectWithPart("LiquidVolume"))
			{
				return false;
			}
			return C.IsPassable(null, IncludeCombatObjects: false);
		}

		// --- The pass ---------------------------------------------------------------------

		/// <summary>
		/// Walks the settlement's own errands for the days since anyone last walked them, and
		/// lets the ground show it.
		/// <para>
		/// Called from <c>KingdomGrowth.OnZoneActivated</c> after everything that spends water and
		/// everything that spends hands, because wearing ground spends neither. Nobody is stood
		/// down off a work to make a path; the path is what is left behind by people going to the
		/// work they were already assigned to.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom. Does nothing when unfounded.</param>
		/// <param name="Z">The activated ground. Does nothing when it is not the kingdom's.</param>
		public static void OnSettlementPass(KingdomSystem System, Zone Z)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			long timeTicks = The.Game.TimeTicks;
			long walked = ReadTick(Z, WalkedProperty);
			if (walked <= 0L)
			{
				WriteTick(Z, WalkedProperty, timeTicks);
				return;
			}
			int days = KingdomRules.HeartbeatDays(timeTicks - walked);
			if (days <= 0)
			{
				return;
			}
			WriteTick(Z, WalkedProperty, KingdomRules.HeartbeatCheckpoint(walked, timeTicks));
			// Nobody living here is NOT a stall: an empty settlement has no errands, and an
			// announcement about it would be a complaint about a thing that is not wrong.
			if (System.Population <= 0)
			{
				return;
			}
			List<KingdomPlotRules.PlotRect> plots = KingdomPlots.ReadPlots(Z);
			List<Errand> errands = Errands(System, Z, plots);
			if (errands.Count == 0)
			{
				return;
			}
			List<KingdomRoadRules.WornCell> tally = ReadTally(Z);
			// One read per cell per pass however many errands cross it: 0 unknown, 1 walkable,
			// 2 not. Without this a settlement with eight errands over the same lane pays for
			// that lane eight times.
			byte[] cache = new byte[Z.Width * Z.Height];
			KingdomRoadRules.CellFilter passable = delegate(int x, int y)
			{
				int index = KingdomRoadRules.Pack(x, y, Z.Width);
				if (index < 0 || index >= cache.Length)
				{
					return false;
				}
				if (cache[index] == 0)
				{
					cache[index] = (byte)(Walkable(Z.GetCell(x, y)) ? 1 : 2);
				}
				return cache[index] == 1;
			};
			int start = KingdomRoadRules.RotationStart(timeTicks, errands.Count);
			int taken = (errands.Count < KingdomRoadRules.MaxRoutesPerPass) ? errands.Count : KingdomRoadRules.MaxRoutesPerPass;
			List<int> route = new List<int>();
			bool full = false;
			int laid = 0;
			for (int i = 0; i < taken; i++)
			{
				Errand errand = errands[(start + i) % errands.Count];
				int walkers = KingdomRoadRules.WalkersFor(errand.Kind, System.Population);
				int traffic = KingdomRoadRules.TrafficFor(walkers, days, errand.Kind);
				if (traffic <= 0)
				{
					continue;
				}
				if (!KingdomRoadRules.TryTrace(passable, Z.Width, Z.Height, errand.FromX, errand.FromY, errand.ToX, errand.ToY,
					KingdomRoadRules.MaxRouteCells, KingdomRoadRules.MaxExploreCells, route))
				{
					continue;
				}
				for (int c = 0; c < route.Count; c++)
				{
					int x = KingdomRoadRules.UnpackX(route[c], Z.Width);
					int y = KingdomRoadRules.UnpackY(route[c], Z.Width);
					if (!Wearable(Z.GetCell(x, y), plots))
					{
						continue;
					}
					if (!KingdomRoadRules.Accrue(tally, x, y, traffic, out _))
					{
						full = true;
						continue;
					}
					laid++;
				}
			}
			KingdomRoadRules.WearState reached = Apply(Z, tally, plots);
			WriteTally(Z, tally);
			Announce(System, Z, reached, full, tally.Count);
			if (KingdomLog.Enabled)
			{
				KingdomLog.Log("roads: days=" + days + " errands=" + errands.Count + " walked=" + taken
					+ " cells=" + laid + " tracked=" + tally.Count + " reached=" + reached);
			}
		}

		// Everything anyone in this settlement has a reason to walk between, in a stable order:
		// home to the nearest work, each work to the heart, the heart to the way out, and every
		// plot's own door to the lane beside it.
		private static List<Errand> Errands(KingdomSystem System, Zone Z, IList<KingdomPlotRules.PlotRect> Plots)
		{
			List<Errand> errands = new List<Errand>();
			if (Z == null)
			{
				return errands;
			}
			List<KingdomLayoutRules.LayoutMark> marks = KingdomLayout.ReadMarks(Z);
			List<KingdomLayoutRules.LayoutMark> homes = new List<KingdomLayoutRules.LayoutMark>();
			List<KingdomLayoutRules.LayoutMark> works = new List<KingdomLayoutRules.LayoutMark>();
			for (int i = 0; i < marks.Count; i++)
			{
				KingdomLayoutRules.LayoutMark mark = marks[i];
				if (mark.Purpose == KingdomLayoutRules.LayoutPurpose.Housing)
				{
					if (homes.Count < MaxHomesConsidered)
					{
						homes.Add(mark);
					}
				}
				else if (mark.Purpose == KingdomLayoutRules.LayoutPurpose.Civic
					|| mark.Purpose == KingdomLayoutRules.LayoutPurpose.Field
					|| mark.Purpose == KingdomLayoutRules.LayoutPurpose.Storage
					|| mark.Purpose == KingdomLayoutRules.LayoutPurpose.Sited)
				{
					if (works.Count < MaxWorksConsidered)
					{
						works.Add(mark);
					}
				}
			}
			bool hasRite = KingdomPlots.TryRiteGround(Z, out var riteX, out var riteY);
			bool hasHeart = KingdomPlotRules.TryHeart(marks, hasRite, riteX, riteY, out var heartX, out var heartY);
			for (int i = 0; i < homes.Count; i++)
			{
				int nearest = Nearest(works, homes[i].X, homes[i].Y);
				if (nearest >= 0)
				{
					errands.Add(new Errand(homes[i].X, homes[i].Y, works[nearest].X, works[nearest].Y,
						KingdomRoadRules.RouteKind.HomeToWork));
				}
			}
			if (hasHeart)
			{
				for (int i = 0; i < works.Count; i++)
				{
					errands.Add(new Errand(works[i].X, works[i].Y, heartX, heartY, KingdomRoadRules.RouteKind.WorkToHeart));
				}
				KingdomRules.Frontier edges = (System != null)
					? KingdomRules.FrontierEdges(Z.ZoneID, System.ClaimedZones)
					: KingdomRules.Frontier.None;
				if (KingdomRoadRules.TryGate(Z.Width, Z.Height, edges, heartX, heartY, out var gateX, out var gateY))
				{
					errands.Add(new Errand(heartX, heartY, gateX, gateY, KingdomRoadRules.RouteKind.HeartToGate));
				}
				if (Plots != null)
				{
					int considered = 0;
					for (int i = 0; i < Plots.Count && considered < MaxPlotsConsidered; i++)
					{
						if (!KingdomPlotRules.TryDoor(Plots[i], heartX, heartY, out var doorX, out var doorY))
						{
							continue;
						}
						if (!KingdomRoadRules.TryLane(Plots[i], doorX, doorY, out var laneX, out var laneY))
						{
							continue;
						}
						errands.Add(new Errand(doorX, doorY, laneX, laneY, KingdomRoadRules.RouteKind.DoorToLane));
						considered++;
					}
				}
			}
			return errands;
		}

		private static int Nearest(IList<KingdomLayoutRules.LayoutMark> Marks, int X, int Y)
		{
			int best = -1;
			int bestDistance = 0;
			for (int i = 0; i < Marks.Count; i++)
			{
				int distance = KingdomLayoutRules.Chebyshev(X, Y, Marks[i].X, Marks[i].Y);
				if (distance == 0)
				{
					continue;
				}
				if (best < 0 || distance < bestDistance)
				{
					best = i;
					bestDistance = distance;
				}
			}
			return best;
		}

		// --- Letting it show ---------------------------------------------------------------

		/// <summary>
		/// Brings the ground up to what the tally says, no more than
		/// <c>KingdomRoadRules.MaxFloorChangesPerPass</c> cells of it at a time, and retires every
		/// cell that has become a path &mdash; from then on the laid path is the record, and the
		/// tally has room for whatever the settlement wears next.
		/// </summary>
		/// <returns>The highest rung any cell was brought to this pass, for the ledger's one
		/// line.</returns>
		private static KingdomRoadRules.WearState Apply(Zone Z, IList<KingdomRoadRules.WornCell> Tally, IList<KingdomPlotRules.PlotRect> Plots)
		{
			KingdomRoadRules.WearState reached = KingdomRoadRules.WearState.Untouched;
			int changes = 0;
			for (int i = Tally.Count - 1; i >= 0; i--)
			{
				if (changes >= KingdomRoadRules.MaxFloorChangesPerPass)
				{
					break;
				}
				KingdomRoadRules.WornCell cell = Tally[i];
				KingdomRoadRules.WearState wanted = KingdomRoadRules.WearAt(cell.Traffic);
				if (wanted <= KingdomRoadRules.WearState.Worn)
				{
					continue;
				}
				Cell ground = Z.GetCell(cell.X, cell.Y);
				KingdomRoadRules.WearState applied = AppliedState(ground);
				if (applied >= wanted)
				{
					if (wanted == KingdomRoadRules.WearState.Path)
					{
						Tally.RemoveAt(i);
					}
					continue;
				}
				if (!Wearable(ground, Plots))
				{
					// Something has been set down here since the last pass. The ground keeps its
					// tally and waits; nothing is moved to make room for a floor.
					continue;
				}
				if (!Lay(ground, wanted, null))
				{
					continue;
				}
				changes++;
				if (wanted > reached)
				{
					reached = wanted;
				}
				if (wanted == KingdomRoadRules.WearState.Path)
				{
					Tally.RemoveAt(i);
				}
			}
			return reached;
		}

		/// <summary>
		/// Lays one floor, taking up the one this system laid before it. The only destruction
		/// anywhere in this file, and it is always of an object this file created and marked
		/// (STANDARDS 7).
		/// </summary>
		/// <param name="C">The cell. Must already have passed <see cref="Wearable"/>.</param>
		/// <param name="State">The rung to bring it to. <c>Untouched</c> and <c>Worn</c> lay
		/// nothing, because neither is a floor.</param>
		/// <param name="PavedBlueprint">The blueprint paving is laid as, from
		/// <c>KingdomRoadRules.PavedFloorFor</c>. Ignored except when
		/// <paramref name="State"/> is <c>Paved</c>.</param>
		/// <returns>False when nothing was laid, including when the blueprint does not exist in
		/// this install.</returns>
		public static bool Lay(Cell C, KingdomRoadRules.WearState State, string PavedBlueprint)
		{
			if (C == null)
			{
				return false;
			}
			string blueprint;
			switch (State)
			{
				case KingdomRoadRules.WearState.Trodden:
					blueprint = TroddenBlueprint;
					break;
				case KingdomRoadRules.WearState.Path:
					blueprint = PathBlueprint;
					break;
				case KingdomRoadRules.WearState.Paved:
					blueprint = string.IsNullOrEmpty(PavedBlueprint) ? PathBlueprint : PavedBlueprint;
					break;
				default:
					return false;
			}
			GameObject floor = GameObject.Create(blueprint);
			if (floor == null)
			{
				KingdomLog.Log("roads: no blueprint named " + blueprint + "; the ground was left as it was");
				return false;
			}
			GameObject previous = OurFloor(C);
			floor.SetIntProperty(PathStateProperty, (int)State);
			C.AddObject(floor);
			if (floor.CurrentCell != C)
			{
				// Measured rather than trusted (STANDARDS 1): if the engine declined the cell for
				// any reason, the ground keeps exactly what it had and nothing is taken up.
				floor.Obliterate();
				return false;
			}
			previous?.Obliterate();
			return true;
		}

		private static void Announce(KingdomSystem System, Zone Z, KingdomRoadRules.WearState Reached, bool Full, int Tracked)
		{
			if (Full)
			{
				if (Z.GetZoneProperty(FullSaidProperty, null) != "1")
				{
					Z.SetZoneProperty(FullSaidProperty, "1");
					System.Ledger.Note(KingdomRoadRules.RefuseTallyFull(System.SeatName));
				}
			}
			else if (Tracked < KingdomRoadRules.MaxTrackedCells)
			{
				Z.SetZoneProperty(FullSaidProperty, "0");
			}
			if (Reached <= KingdomRoadRules.WearState.Worn)
			{
				return;
			}
			int said = int.TryParse(Z.GetZoneProperty(SaidProperty, null), out var value) ? value : 0;
			if ((int)Reached <= said)
			{
				return;
			}
			Z.SetZoneProperty(SaidProperty, ((int)Reached).ToString());
			string line = KingdomRoadRules.WearLine(Reached, System.SeatName);
			if (line != null)
			{
				System.Ledger.Note(line);
			}
			if (Reached == KingdomRoadRules.WearState.Path)
			{
				KingdomChronicle.Record(System, "paths showed themselves through " + System.KingdomDisplayName
					+ ", worn by nothing but the going back and forth of the people who live there");
			}
		}

		// --- Paving -------------------------------------------------------------------------

		/// <summary>Cells of this zone that are a path and not yet paved, nearest a given cell
		/// first, ties broken north-then-west so the same order comes back every time.</summary>
		/// <param name="Z">The zone. Null yields an empty list.</param>
		/// <param name="From">Where the founder is standing, for the ordering. Null orders purely
		/// by position.</param>
		public static List<Cell> PathCells(Zone Z, Cell From)
		{
			List<Cell> cells = new List<Cell>();
			if (Z == null)
			{
				return cells;
			}
			foreach (GameObject item in Z.GetObjects())
			{
				if (item == null || item.GetIntProperty(PathStateProperty) != (int)KingdomRoadRules.WearState.Path)
				{
					continue;
				}
				Cell cell = item.CurrentCell;
				if (cell != null)
				{
					cells.Add(cell);
				}
			}
			cells.Sort(delegate(Cell a, Cell b)
			{
				if (From != null)
				{
					int da = KingdomLayoutRules.Chebyshev(a.X, a.Y, From.X, From.Y);
					int db = KingdomLayoutRules.Chebyshev(b.X, b.Y, From.X, From.Y);
					if (da != db)
					{
						return da - db;
					}
				}
				if (a.Y != b.Y)
				{
					return a.Y - b.Y;
				}
				return a.X - b.X;
			});
			return cells;
		}

		/// <summary>
		/// Lays the settlement's worn paths in the material it builds its walls in.
		/// <para>
		/// Consent before cost: the founder is shown the cells and the price and asked, and a
		/// refusal spends nothing and changes nothing. Nothing is paved that is not already a
		/// path &mdash; the founder formalises what the settlement decided by walking, and never
		/// decides it for them.
		/// </para>
		/// </summary>
		/// <param name="System">The kingdom; must be founded.</param>
		/// <param name="Z">The ground; must be the kingdom's own claim.</param>
		/// <param name="From">Where the founder stands, so a great city paves what is underfoot
		/// first. May be null.</param>
		/// <param name="Failure">A founder-facing reason when this returns false. Nothing is
		/// spent and nothing is laid when it does.</param>
		/// <returns>True once ground has actually been paved. A declined confirmation returns
		/// false with a null <paramref name="Failure"/>, because a refusal is free and is not an
		/// error.</returns>
		public static bool Pave(KingdomSystem System, Zone Z, Cell From, out string Failure)
		{
			Failure = null;
			if (System == null || !System.Founded)
			{
				Failure = "You rule nothing yet.";
				return false;
			}
			if (!Enabled)
			{
				Failure = "Ground here does not wear, so there is nothing worn to lay. (Options: the settlement's ways)";
				return false;
			}
			if (Z == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				Failure = KingdomRoadRules.RefuseNotOurGround();
				return false;
			}
			List<Cell> paths = PathCells(Z, From);
			if (paths.Count == 0)
			{
				Failure = KingdomRoadRules.RefuseNothingWorn(System.SeatName);
				return false;
			}
			string wall = KingdomPlotRules.WallBlueprintFor(System.Style, System.FoundingRegionName);
			KingdomMaterial material = KingdomRoadRules.PaveMaterialFor(wall);
			if (!KingdomRoadRules.CanPaveIn(material))
			{
				Failure = KingdomRoadRules.RefuseMaterialKind(material);
				return false;
			}
			if (KingdomMaterialRules.FreeHands(System.Population, System.AssignedCrew) <= 0)
			{
				Failure = KingdomRoadRules.RefuseHands(System.SeatName);
				return false;
			}
			int cells = KingdomRoadRules.PaveCells(paths.Count);
			int cost = KingdomRoadRules.PaveCost(cells);
			KingdomMaterials.MaterialStock stock = KingdomMaterials.Stock(Z);
			int held = stock.Tally.Get(material);
			if (held < cost)
			{
				Failure = KingdomRoadRules.RefuseMaterial(material, cost, held);
				return false;
			}
			if (Popup.ShowYesNo("Lay " + cells + ((cells == 1) ? " cell" : " cells") + " of worn path at " + System.SeatName
				+ " in {{C|" + KingdomMaterialRules.MaterialName(material) + "}}?\n\nIt costs " + cost + " of it, and no water. "
				+ ((paths.Count > cells) ? ("There is more worn ground than one order covers; " + (paths.Count - cells) + " more will wait for the next.\n\n") : "")
				+ "Nothing changes about where anyone walks. The settlement only stops pretending it has not decided.") != DialogResult.Yes)
			{
				return false;
			}
			string blueprint = KingdomRoadRules.PavedFloorFor(wall);
			List<KingdomRoadRules.WornCell> tally = ReadTally(Z);
			int laid = 0;
			for (int i = 0; i < cells; i++)
			{
				if (Lay(paths[i], KingdomRoadRules.WearState.Paved, blueprint))
				{
					KingdomRoadRules.Retire(tally, paths[i].X, paths[i].Y);
					laid++;
				}
			}
			if (laid <= 0)
			{
				// Nothing went down, so nothing is charged. The stockpiles are counted against
				// what was actually laid rather than what was asked for, because a price quoted
				// is not a price paid (STANDARDS 1: measure the state change).
				Failure = "The ground would not take the paving. Nothing was spent.";
				return false;
			}
			WriteTally(Z, tally);
			KingdomMaterialTally price = new KingdomMaterialTally();
			price.Add(material, KingdomRoadRules.PaveCost(laid));
			if (!stock.Spend(price))
			{
				KingdomLog.Log("roads: paving was laid at " + System.SeatName + " and the stockpiles could not be charged for it");
			}
			// Paving retires cells from the tally, so the ground the settlement is wearing now
			// has room to be recorded again, and the reason it stalled is over.
			Z.SetZoneProperty(FullSaidProperty, "0");
			MessageQueue.AddPlayerMessage(KingdomRoadRules.PavedLine(laid, material, System.SeatName));
			KingdomChronicle.Record(System, KingdomRoadRules.PavedRecord(laid, material, System.KingdomDisplayName));
			System.RecordDeed("the paving of the ways at " + System.SeatName);
			KingdomLog.Log("roads: paved " + laid + " cells in " + KingdomMaterialRules.MaterialKey(material) + " at " + System.SeatName);
			return true;
		}

		/// <summary>
		/// One line for the status report: what the settlement's own feet have made of its
		/// ground. Never null, and never silent about a full tally (STANDARDS 7b).
		/// </summary>
		/// <param name="Z">The zone. Null answers the line for ground nobody walks.</param>
		public static string WornLine(Zone Z)
		{
			if (!Enabled)
			{
				return "Ground here does not wear. (Options: the settlement's ways)";
			}
			if (Z == null)
			{
				return "No ground here is walked enough to show it.";
			}
			int paths = 0;
			int paved = 0;
			foreach (GameObject item in Z.GetObjects())
			{
				int state = (item == null) ? 0 : item.GetIntProperty(PathStateProperty);
				if (state == (int)KingdomRoadRules.WearState.Path)
				{
					paths++;
				}
				else if (state == (int)KingdomRoadRules.WearState.Paved)
				{
					paved++;
				}
			}
			int worn = ReadTally(Z).Count;
			if (paths == 0 && paved == 0 && worn == 0)
			{
				return "No ground here is walked enough to show it.";
			}
			string line = "The ground shows " + worn + ((worn == 1) ? " cell" : " cells") + " of wearing, "
				+ paths + ((paths == 1) ? " cell" : " cells") + " of path, and "
				+ paved + ((paved == 1) ? " cell" : " cells") + " of paving.";
			if (paths > 0)
			{
				line += " (Charter: pave a worn path)";
			}
			return line;
		}
	}
}
