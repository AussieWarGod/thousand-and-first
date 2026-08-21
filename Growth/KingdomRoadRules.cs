using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free arithmetic for the ground people wear walking on it: which routes a
	/// settlement's shape implies, which cells one of those routes crosses, how much traffic a
	/// day of walking lays on a cell, which of four states that traffic has reached, what paving
	/// a worn path is made of and costs, and how the whole tally is written to a string short
	/// enough to keep on a zone.
	/// <para>
	/// Roads are never drawn here. The plot grammar already reserves a lane on every side of
	/// every plot (<see cref="KingdomPlotRules.RoadMargin"/>), and the gap between two plots'
	/// reserved rects IS the road; what this file does is work out which of those gaps people
	/// actually cross, and let the crossing show. Nothing is placed, nothing is planned, and no
	/// route is ever a decision the founder made &mdash; the only decision in the whole system is
	/// paving, and paving is only ever offered for ground that is already a path.
	/// </para>
	/// <para>
	/// Wear only ever climbs, and ground never un-wears: nothing in this file subsides, because a
	/// path is not supply-carried level and a founder is never punished for having been elsewhere.
	/// What the doctrine did change is the denominator underneath it &mdash; errands are walked on
	/// the full elapsed now, not on a capped three days (Addendum 8 clause 1), so a settlement
	/// lived in for a season is found with a season's ways rather than three days' worth. The
	/// bounds that keep this from turning a zone into one flat road are reach and labour, never
	/// decay: only cells on a real route between two real things accrue at all, only
	/// <see cref="MaxRoutesPerPass"/> routes are walked on any one pass, the walkers on a route
	/// come out of a real population, and the tally saturates at <see cref="MaxTraffic"/>.
	/// </para>
	/// <para>
	/// Nothing here touches <c>XRL</c>. It never reads a cell, never places an object, and never
	/// destroys one; the engine-coupled half &mdash; reading real ground, laying real floors,
	/// spending real material &mdash; is <c>KingdomRoads</c>, in Growth/KingdomRoads.cs.
	/// </para>
	/// </summary>
	public static class KingdomRoadRules
	{
		/// <summary>
		/// How hard a cell has been walked. The ladder is four rungs deep and climbs one rung at
		/// a time, so a founder who leaves at grass and comes home to a path has watched it
		/// happen in the record even if not with their own eyes.
		/// </summary>
		public enum WearState
		{
			/// <summary>Nobody crosses here, or not often enough to show.</summary>
			Untouched = 0,
			/// <summary>The grass is bent and the ground shows through. Nothing is laid: Qud has
			/// no blueprint for trodden grass, and inventing one to sit under every settler's
			/// feet would be a lie told in art.</summary>
			Worn = 1,
			/// <summary>Packed dirt. The first rung that changes what the ground looks like.</summary>
			Trodden = 2,
			/// <summary>A path, and everyone in the settlement knows where it goes.</summary>
			Path = 3,
			/// <summary>Laid in the settlement's own wall material, because the founder said so.
			/// Never reached by walking &mdash; only by an order, and only over a path.</summary>
			Paved = 4
		}

		/// <summary>
		/// Why anyone is on a given piece of ground. Four errands, and they are the four the
		/// settlement actually simulates: sleeping and working, working and gathering, gathering
		/// and leaving, and getting out of your own doorway.
		/// </summary>
		public enum RouteKind
		{
			/// <summary>Where settlers sleep, to the nearest thing they crew. The daily walk, and
			/// the heaviest.</summary>
			HomeToWork = 0,
			/// <summary>A work to the settled heart, where water is shared and news is had.</summary>
			WorkToHeart = 1,
			/// <summary>The heart to whatever edge faces the world. Everybody uses it and nobody
			/// uses it often.</summary>
			HeartToGate = 2,
			/// <summary>A plot's own door to the lane the plot grammar reserved beside it. Short,
			/// and it wears first, because everything else starts here.</summary>
			DoorToLane = 3
		}

		/// <summary>One cell of ground and the traffic laid on it so far. A plain value: the
		/// state is <see cref="WearAt"/> of the traffic, never stored beside it, so the two can
		/// never disagree.</summary>
		public struct WornCell
		{
			public int X;

			public int Y;

			public int Traffic;

			public WornCell(int X, int Y, int Traffic)
			{
				this.X = X;
				this.Y = Y;
				this.Traffic = Traffic;
			}
		}

		/// <summary>Whether one cell may be walked through. Supplied by the caller so the routing
		/// below never learns what a cell is; a test passes a lambda over a string map, and the
		/// engine half passes a memoised reader of real ground.</summary>
		/// <param name="X">Cell x.</param>
		/// <param name="Y">Cell y.</param>
		public delegate bool CellFilter(int X, int Y);

		// --- The ladder ------------------------------------------------------------------

		/// <summary>Traffic at which the grass gives up. Nothing is laid at this rung.</summary>
		public const int WornTraffic = 40;

		/// <summary>Traffic at which the ground is packed dirt.</summary>
		public const int TroddenTraffic = 120;

		/// <summary>Traffic at which the ground is a path, and the founder may pave it.</summary>
		public const int PathTraffic = 300;

		/// <summary>Ceiling on a cell's tally, so a century of walking cannot overflow the field
		/// it is written to. Well above <see cref="PathTraffic"/>, which is the last rung walking
		/// can reach anyway.</summary>
		public const int MaxTraffic = 4000;

		/// <summary>What a tally has worn the ground to.</summary>
		/// <returns>Never <see cref="WearState.Paved"/>: paving is an order, not a tally.</returns>
		public static WearState WearAt(int Traffic)
		{
			if (Traffic >= PathTraffic)
			{
				return WearState.Path;
			}
			if (Traffic >= TroddenTraffic)
			{
				return WearState.Trodden;
			}
			if (Traffic >= WornTraffic)
			{
				return WearState.Worn;
			}
			return WearState.Untouched;
		}

		/// <summary>Traffic one rung of the ladder asks for. Zero for
		/// <see cref="WearState.Untouched"/>, and <see cref="int.MaxValue"/> for
		/// <see cref="WearState.Paved"/>, which walking never reaches.</summary>
		public static int ThresholdFor(WearState State)
		{
			switch (State)
			{
				case WearState.Worn:
					return WornTraffic;
				case WearState.Trodden:
					return TroddenTraffic;
				case WearState.Path:
					return PathTraffic;
				case WearState.Paved:
					return int.MaxValue;
				default:
					return 0;
			}
		}

		/// <summary>How the mod says a rung out loud.</summary>
		public static string WearName(WearState State)
		{
			switch (State)
			{
				case WearState.Worn:
					return "worn grass";
				case WearState.Trodden:
					return "trodden earth";
				case WearState.Path:
					return "a path";
				case WearState.Paved:
					return "paving";
				default:
					return "untouched ground";
			}
		}

		// --- Traffic ---------------------------------------------------------------------

		/// <summary>What one walker on one route lays on each cell of it in a day.</summary>
		public const int TrafficPerWalkerDay = 6;

		/// <summary>Walkers one route is ever credited with, however many people live here. A
		/// route is an errand, not a parade: past four, more settlers means more routes rather
		/// than a deeper rut in this one.</summary>
		public const int MaxWalkersPerRoute = 4;

		/// <summary>
		/// How much of a full day's walking each errand is worth, as a percent. The doorway and
		/// the walk to work are daily; the heart is most days; the gate is whenever there is
		/// somewhere to go.
		/// </summary>
		public static int RouteWeightPercent(RouteKind Kind)
		{
			switch (Kind)
			{
				case RouteKind.HomeToWork:
					return 100;
				case RouteKind.DoorToLane:
					return 100;
				case RouteKind.WorkToHeart:
					return 70;
				case RouteKind.HeartToGate:
					return 50;
				default:
					return 0;
			}
		}

		/// <summary>
		/// How many people an errand puts on the ground. Nobody walks anywhere in a settlement
		/// with nobody in it, and no errand is ever credited with more than
		/// <see cref="MaxWalkersPerRoute"/> however large the settlement grows.
		/// </summary>
		/// <param name="Kind">The errand.</param>
		/// <param name="Population">Settlers living here. Zero or less means nobody walks.</param>
		public static int WalkersFor(RouteKind Kind, int Population)
		{
			if (Population <= 0)
			{
				return 0;
			}
			int walkers;
			switch (Kind)
			{
				case RouteKind.HomeToWork:
				case RouteKind.DoorToLane:
					walkers = 2;
					break;
				case RouteKind.WorkToHeart:
					walkers = 1;
					break;
				case RouteKind.HeartToGate:
					walkers = Population;
					break;
				default:
					return 0;
			}
			if (walkers > Population)
			{
				walkers = Population;
			}
			return (walkers > MaxWalkersPerRoute) ? MaxWalkersPerRoute : walkers;
		}

		/// <summary>
		/// Traffic one route lays on each of its cells over a stretch of days. Days come from
		/// <c>KingdomRules.ElapsedDays</c>, uncapped: people walk to work whether or not the
		/// founder is standing there to see them do it (Addendum 8 clause 1).
		/// <para>
		/// The bound is the labour term, not a ceiling on the calendar. Traffic is WALKERS times
		/// days, walkers are drawn from the settlement's own population, and a settlement with
		/// nobody in it lays nothing however long the stretch (clause 2). Past that, the tally
		/// itself saturates at <see cref="MaxTraffic"/>, which is what stops a decade of honest
		/// walking from reading any differently from a year of it.
		/// </para>
		/// </summary>
		/// <param name="Walkers">People on the errand; clamped to
		/// <see cref="MaxWalkersPerRoute"/>. Zero or less lays nothing.</param>
		/// <param name="Days">Whole days walked. Zero or less lays nothing.</param>
		/// <param name="Kind">The errand, for its weight.</param>
		public static int TrafficFor(int Walkers, int Days, RouteKind Kind)
		{
			if (Walkers <= 0 || Days <= 0)
			{
				return 0;
			}
			long walkers = (Walkers > MaxWalkersPerRoute) ? MaxWalkersPerRoute : Walkers;
			// Widened before the multiply and saturated after it: an uncapped day count reaches
			// int.MaxValue on a stamp nobody has resolved since the world was made, and a wrapped
			// negative would read as ground that had been UNwalked.
			long traffic = walkers * Days * TrafficPerWalkerDay * RouteWeightPercent(Kind) / 100L;
			return (traffic > MaxTraffic) ? MaxTraffic : (int)traffic;
		}

		// --- Bounds ----------------------------------------------------------------------

		/// <summary>Errands walked on any one pass. A settlement of forty buildings implies far
		/// more than this; <see cref="RotationStart"/> is what makes sure the rest are not
		/// forgotten, only queued.</summary>
		public const int MaxRoutesPerPass = 8;

		/// <summary>Cells a single errand may cross before it stops being an errand. Nobody walks
		/// the long way round a zone to get to work; a route longer than this is discarded, and
		/// the ground between stays as it was.</summary>
		public const int MaxRouteCells = 48;

		/// <summary>Cells the search may look at before it gives up on one errand. Bounds the
		/// per-pass cost against the shape of a zone rather than against its area.</summary>
		public const int MaxExploreCells = 400;

		/// <summary>Cells of part-worn ground one settlement's ways are remembered on. A cell
		/// that reaches <see cref="WearState.Path"/> leaves this tally for good &mdash; the laid
		/// path itself is the record from then on &mdash; so this fills only with ground that is
		/// on its way somewhere.</summary>
		public const int MaxTrackedCells = 240;

		/// <summary>Floors laid on any one pass. A hundred days away resolves into eight cells
		/// changing and the rest keeping their tallies, which is a bounded consequence per visit
		/// and not a lost one.</summary>
		public const int MaxFloorChangesPerPass = 8;

		/// <summary>Cells one paving order covers. Past this the founder pays for what is nearest
		/// and orders again, rather than spending a stockpile on a single unread confirmation.</summary>
		public const int MaxPaveCellsPerOrder = 40;

		/// <summary>
		/// Where in a settlement's list of errands this pass starts walking. Turns on the day
		/// rather than on a draw, so it is the same answer on every load of the same save, and
		/// so a settlement with thirty errands walks all of them over a handful of visits
		/// instead of wearing the first eight into canyons.
		/// </summary>
		/// <param name="TimeTicks">Game time. Negative reads as zero.</param>
		/// <param name="Count">Errands there are. Zero or less answers zero.</param>
		public static int RotationStart(long TimeTicks, int Count)
		{
			if (Count <= 0)
			{
				return 0;
			}
			long ticks = (TimeTicks < 0L) ? 0L : TimeTicks;
			return (int)((ticks / KingdomRules.TicksPerDay) % Count);
		}

		// --- Routing ---------------------------------------------------------------------

		/// <summary>Whether a cell is inside a grid.</summary>
		public static bool InBounds(int X, int Y, int Width, int Height)
		{
			return X >= 0 && Y >= 0 && X < Width && Y < Height;
		}

		/// <summary>A cell packed into one int, the way a route is carried.</summary>
		public static int Pack(int X, int Y, int Width)
		{
			return Y * Width + X;
		}

		/// <summary>The x of a packed cell.</summary>
		public static int UnpackX(int Packed, int Width)
		{
			return (Width <= 0) ? 0 : Packed % Width;
		}

		/// <summary>The y of a packed cell.</summary>
		public static int UnpackY(int Packed, int Width)
		{
			return (Width <= 0) ? 0 : Packed / Width;
		}

		// Eight ways, in a fixed order. Qud walks diagonally, so a desire path does too; the
		// order is fixed rather than natural because a breadth-first search that reaches a cell
		// by two equally short ways must always pick the same one, or the same settlement would
		// wear different ground on different loads of the same save.
		private static readonly int[] StepX = new int[8] { 0, 1, 1, 1, 0, -1, -1, -1 };

		private static readonly int[] StepY = new int[8] { -1, -1, 0, 1, 1, 1, 0, -1 };

		/// <summary>
		/// The ground between two things, as feet would find it: the shortest walk, ties broken
		/// the same way every time.
		/// <para>
		/// Both endpoints are enterable whether or not they are passable &mdash; a home and a
		/// work are solid objects, and the errand still starts and ends at them &mdash; but
		/// neither is returned, because wear is only ever laid on the ground BETWEEN, never
		/// under the thing itself.
		/// </para>
		/// </summary>
		/// <param name="Passable">Whether a cell may be walked through. Called at most
		/// <paramref name="MaxExplore"/> times per call; the caller is expected to memoise
		/// anything expensive.</param>
		/// <param name="Width">Grid width in cells.</param>
		/// <param name="Height">Grid height in cells.</param>
		/// <param name="FromX">Start x.</param>
		/// <param name="FromY">Start y.</param>
		/// <param name="ToX">End x.</param>
		/// <param name="ToY">End y.</param>
		/// <param name="MaxCells">Ground cells the errand may cross. A shortest walk longer than
		/// this is refused whole, and nothing is written to <paramref name="Cells"/>.</param>
		/// <param name="MaxExplore">Cells the search may reach before giving up.</param>
		/// <param name="Cells">Receives the packed cells between the endpoints, in walking order.
		/// Cleared first, and left empty on any failure and on two adjacent endpoints.</param>
		/// <returns>False when the arguments do not describe a grid, when the endpoints are the
		/// same cell, when no walk exists inside <paramref name="MaxExplore"/>, or when the
		/// shortest walk is longer than <paramref name="MaxCells"/>.</returns>
		public static bool TryTrace(CellFilter Passable, int Width, int Height, int FromX, int FromY, int ToX, int ToY, int MaxCells, int MaxExplore, IList<int> Cells)
		{
			if (Cells == null)
			{
				return false;
			}
			Cells.Clear();
			if (Passable == null || Width <= 0 || Height <= 0 || MaxCells < 0 || MaxExplore <= 0)
			{
				return false;
			}
			if (!InBounds(FromX, FromY, Width, Height) || !InBounds(ToX, ToY, Width, Height))
			{
				return false;
			}
			int from = Pack(FromX, FromY, Width);
			int to = Pack(ToX, ToY, Width);
			if (from == to)
			{
				return false;
			}
			int area = Width * Height;
			int[] parent = new int[area];
			for (int i = 0; i < area; i++)
			{
				parent[i] = -1;
			}
			int[] queue = new int[area];
			int head = 0;
			int tail = 0;
			parent[from] = from;
			queue[tail++] = from;
			int explored = 0;
			bool found = false;
			while (head < tail && !found)
			{
				int current = queue[head++];
				if (++explored > MaxExplore)
				{
					break;
				}
				int cx = UnpackX(current, Width);
				int cy = UnpackY(current, Width);
				for (int d = 0; d < 8; d++)
				{
					int nx = cx + StepX[d];
					int ny = cy + StepY[d];
					if (!InBounds(nx, ny, Width, Height))
					{
						continue;
					}
					int next = Pack(nx, ny, Width);
					if (parent[next] != -1)
					{
						continue;
					}
					// The far end is enterable whatever stands on it: an errand ends at a door,
					// and a door is not empty ground.
					if (next != to && !Passable(nx, ny))
					{
						continue;
					}
					parent[next] = current;
					if (next == to)
					{
						found = true;
						break;
					}
					queue[tail++] = next;
				}
			}
			if (!found)
			{
				return false;
			}
			// Walked back from the far end, so the list has to be turned round before it is
			// handed over; a route reads from where you set out.
			List<int> reversed = new List<int>();
			int step = parent[to];
			while (step != from)
			{
				reversed.Add(step);
				if (reversed.Count > MaxCells)
				{
					return false;
				}
				step = parent[step];
			}
			for (int i = reversed.Count - 1; i >= 0; i--)
			{
				Cells.Add(reversed[i]);
			}
			return true;
		}

		/// <summary>
		/// Where the settlement's ground meets the world: the cell of the frontier band nearest
		/// the heart. Not a gatehouse and not a decision &mdash; simply the way out, which is
		/// what a road to the horizon is aimed at.
		/// </summary>
		/// <param name="Width">Zone width in cells.</param>
		/// <param name="Height">Zone height in cells.</param>
		/// <param name="Edges">Edges of this zone facing unclaimed ground. <c>None</c> means the
		/// realm surrounds this ground and there is no way out of it to aim at.</param>
		/// <param name="HeartX">The settled heart's x.</param>
		/// <param name="HeartY">The settled heart's y.</param>
		/// <param name="X">The gate's x, meaningful only when this returns true.</param>
		/// <param name="Y">The gate's y, meaningful only when this returns true.</param>
		/// <returns>False when there is no frontier edge, or no zone to have one.</returns>
		public static bool TryGate(int Width, int Height, KingdomRules.Frontier Edges, int HeartX, int HeartY, out int X, out int Y)
		{
			X = 0;
			Y = 0;
			if (Width <= 0 || Height <= 0 || Edges == KingdomRules.Frontier.None)
			{
				return false;
			}
			bool found = false;
			int best = 0;
			int bestStraight = 0;
			for (int y = 0; y < Height; y++)
			{
				for (int x = 0; x < Width; x++)
				{
					if (!KingdomRules.IsOnFrontier(x, y, Width, Height, Edges))
					{
						continue;
					}
					int distance = KingdomLayoutRules.Chebyshev(x, y, HeartX, HeartY);
					// The band is two cells deep and eight-way distance ties across all of it, so
					// Chebyshev alone would put the gate in a corner. The tie is broken by the
					// walk itself: of the cells equally near, the one most nearly straight out
					// from the heart, and then the northmost and westmost, so the same settlement
					// always aims at the same way out.
					int straight = ((x > HeartX) ? (x - HeartX) : (HeartX - x)) + ((y > HeartY) ? (y - HeartY) : (HeartY - y));
					if (!found || distance < best || (distance == best && straight < bestStraight))
					{
						found = true;
						best = distance;
						bestStraight = straight;
						X = x;
						Y = y;
					}
				}
			}
			return found;
		}

		/// <summary>
		/// The one design in the catalogue that is sited by a rule rather than by a size: the
		/// gatehouse belongs on the frontier wall, astride the road, and nowhere else.
		/// <para>
		/// The catalogue's own note beside the entry said it would be "sited as an ordinary
		/// length of wall" only "until roads exist to meet". Roads exist: <see cref="TryGate"/>
		/// already names the cell where the settlement's ground meets the world, and
		/// <c>KingdomRoads</c> already walks a <c>HeartToGate</c> errand at it, so the way out is
		/// a real, worn route rather than a compass direction. This joins the design to that
		/// cell.
		/// </para>
		/// <para>
		/// A key, not a blueprint and not a footprint. Naming it here is the one hardcoded thing
		/// in the rule and it is deliberately the smallest one: an author who re-keys the design
		/// keeps every other property and loses only the siting, and swapping this for an
		/// authored <c>Sited="gate"</c> attribute is a one-line change to
		/// <see cref="SitesAtGate"/> when the schema grows one.
		/// </para>
		/// </summary>
		public const string GatehouseKey = "gatehouse";

		/// <summary>Whether a design is sited at the gate rather than anywhere along the wall.
		/// Case-folded, so a third-party file spelling the key differently still lands.</summary>
		public static bool SitesAtGate(string Key)
		{
			return !string.IsNullOrEmpty(Key) && Key.Trim().ToLowerInvariant() == GatehouseKey;
		}

		/// <summary>
		/// Which of the offered cells the gate design takes: the one nearest the gate cell, ties
		/// broken northmost then westmost, so the same settlement puts its gatehouse in the same
		/// place every time it is asked.
		/// <para>
		/// Nearest rather than exactly-the-gate deliberately. The gate cell may be occupied, and
		/// the protection law forbids taking ground that holds anything (STANDARDS 7) &mdash; so
		/// the rule aims at the way out and settles for the nearest ground beside it, which is
		/// still the wall astride the road. Handed an empty list it answers -1 and the caller
		/// falls back to its own placement, exactly as it does when the plan has no opinion.
		/// </para>
		/// </summary>
		/// <param name="Xs">Candidate cell xs, already filtered to buildable frontier ground.</param>
		/// <param name="Ys">Candidate cell ys, index-matched to <paramref name="Xs"/>.</param>
		/// <param name="GateX">The gate cell's x, from <see cref="TryGate"/>.</param>
		/// <param name="GateY">The gate cell's y.</param>
		/// <returns>An index into the candidate lists, or -1 when there is nothing to choose.</returns>
		public static int NearestToGate(IList<int> Xs, IList<int> Ys, int GateX, int GateY)
		{
			if (Xs == null || Ys == null)
			{
				return -1;
			}
			int count = (Xs.Count < Ys.Count) ? Xs.Count : Ys.Count;
			int best = -1;
			int bestDistance = 0;
			for (int i = 0; i < count; i++)
			{
				int distance = KingdomLayoutRules.Chebyshev(Xs[i], Ys[i], GateX, GateY);
				if (best < 0 || distance < bestDistance)
				{
					best = i;
					bestDistance = distance;
					continue;
				}
				if (distance != bestDistance)
				{
					continue;
				}
				// Ties are broken by the ground itself rather than by the order the engine
				// happened to enumerate cells in, so a reload sites the same gatehouse.
				if (Ys[i] < Ys[best] || (Ys[i] == Ys[best] && Xs[i] < Xs[best]))
				{
					best = i;
				}
			}
			return best;
		}

		/// <summary>
		/// The lane a plot's door opens onto: two cells straight out from the doorway, which
		/// clears the plot's own reserved margin (<see cref="KingdomPlotRules.RoadMargin"/>) and
		/// lands in the gap the plot grammar keeps between buildings.
		/// </summary>
		/// <param name="Rect">The plot.</param>
		/// <param name="DoorX">The doorway, from <c>KingdomPlotRules.TryDoor</c>.</param>
		/// <param name="DoorY">The doorway.</param>
		/// <param name="X">The lane cell, meaningful only when this returns true.</param>
		/// <param name="Y">The lane cell.</param>
		/// <returns>False when the doorway is not on the plot's border, or is a corner &mdash;
		/// neither of which can say which way the door faces.</returns>
		public static bool TryLane(KingdomPlotRules.PlotRect Rect, int DoorX, int DoorY, out int X, out int Y)
		{
			X = 0;
			Y = 0;
			if (!Rect.IsBorder(DoorX, DoorY) || Rect.IsCorner(DoorX, DoorY))
			{
				return false;
			}
			int stepX = 0;
			int stepY = 0;
			if (DoorX == Rect.X1)
			{
				stepX = -1;
			}
			else if (DoorX == Rect.X2)
			{
				stepX = 1;
			}
			else if (DoorY == Rect.Y1)
			{
				stepY = -1;
			}
			else
			{
				stepY = 1;
			}
			int reach = KingdomPlotRules.RoadMargin + 1;
			X = DoorX + stepX * reach;
			Y = DoorY + stepY * reach;
			return true;
		}

		// --- The tally -------------------------------------------------------------------

		/// <summary>The traffic recorded on one cell, or zero when it is not tracked.</summary>
		public static int TrafficAt(IList<WornCell> Cells, int X, int Y)
		{
			if (Cells == null)
			{
				return 0;
			}
			for (int i = 0; i < Cells.Count; i++)
			{
				if (Cells[i].X == X && Cells[i].Y == Y)
				{
					return Cells[i].Traffic;
				}
			}
			return 0;
		}

		/// <summary>Index of a cell in the tally, or -1.</summary>
		public static int IndexOf(IList<WornCell> Cells, int X, int Y)
		{
			if (Cells == null)
			{
				return -1;
			}
			for (int i = 0; i < Cells.Count; i++)
			{
				if (Cells[i].X == X && Cells[i].Y == Y)
				{
					return i;
				}
			}
			return -1;
		}

		/// <summary>
		/// Lays traffic on a cell, admitting it to the tally if this is the first time anyone
		/// walked there.
		/// </summary>
		/// <param name="Cells">The tally, which is modified in place.</param>
		/// <param name="X">Cell x.</param>
		/// <param name="Y">Cell y.</param>
		/// <param name="Traffic">Traffic to lay. Zero or less does nothing and succeeds.</param>
		/// <param name="Total">The cell's tally afterward, or zero when nothing was laid.</param>
		/// <returns>False only when a NEW cell could not be admitted because the tally already
		/// holds <see cref="MaxTrackedCells"/> &mdash; the one condition in this file the founder
		/// has to be told about, because it is the one that stops ground wearing for a reason
		/// they can act on (STANDARDS 7b).</returns>
		public static bool Accrue(IList<WornCell> Cells, int X, int Y, int Traffic, out int Total)
		{
			Total = 0;
			if (Cells == null)
			{
				return false;
			}
			int index = IndexOf(Cells, X, Y);
			if (index < 0)
			{
				if (Traffic <= 0)
				{
					return true;
				}
				if (Cells.Count >= MaxTrackedCells)
				{
					return false;
				}
				Total = (Traffic > MaxTraffic) ? MaxTraffic : Traffic;
				Cells.Add(new WornCell(X, Y, Total));
				return true;
			}
			WornCell cell = Cells[index];
			Total = cell.Traffic + ((Traffic > 0) ? Traffic : 0);
			if (Total > MaxTraffic)
			{
				Total = MaxTraffic;
			}
			cell.Traffic = Total;
			Cells[index] = cell;
			return true;
		}

		/// <summary>Forgets one cell &mdash; what a cell that has become a path does, because the
		/// laid path is the record from then on and the tally is only ever for ground still on
		/// its way.</summary>
		/// <returns>False when the cell was not tracked.</returns>
		public static bool Retire(IList<WornCell> Cells, int X, int Y)
		{
			int index = IndexOf(Cells, X, Y);
			if (index < 0)
			{
				return false;
			}
			Cells.RemoveAt(index);
			return true;
		}

		// --- Writing the tally down ------------------------------------------------------

		/// <summary>Separator between cells in the written tally.</summary>
		public const char CellSeparator = ';';

		/// <summary>Separator between a cell's three numbers.</summary>
		public const char FieldSeparator = ',';

		/// <summary>Largest coordinate the written tally accepts. Far past any Qud zone, and
		/// present so a corrupt or hostile string cannot ask for an array nobody has.</summary>
		public const int MaxCoordinate = 999;

		/// <summary>
		/// The tally as one short string: <c>x,y,traffic</c> for each cell, separated by
		/// semicolons. Cells with nothing on them are left out, so an untouched settlement
		/// writes the empty string and costs nothing to carry.
		/// </summary>
		/// <param name="Cells">The tally. Null writes the empty string.</param>
		public static string Encode(IList<WornCell> Cells)
		{
			if (Cells == null || Cells.Count == 0)
			{
				return "";
			}
			StringBuilder text = new StringBuilder();
			int written = 0;
			for (int i = 0; i < Cells.Count && written < MaxTrackedCells; i++)
			{
				WornCell cell = Cells[i];
				if (cell.Traffic <= 0 || cell.X < 0 || cell.Y < 0 || cell.X > MaxCoordinate || cell.Y > MaxCoordinate)
				{
					continue;
				}
				if (written > 0)
				{
					text.Append(CellSeparator);
				}
				int traffic = (cell.Traffic > MaxTraffic) ? MaxTraffic : cell.Traffic;
				text.Append(cell.X).Append(FieldSeparator).Append(cell.Y).Append(FieldSeparator).Append(traffic);
				written++;
			}
			return text.ToString();
		}

		/// <summary>
		/// Reads a written tally back. Hostile-input discipline throughout (STANDARDS 9): a
		/// malformed entry is dropped rather than believed and rather than thrown over, every
		/// number is clamped to a range this build can act on, a repeated cell keeps its largest
		/// tally, and a tally longer than <see cref="MaxTrackedCells"/> is cut to length.
		/// </summary>
		/// <param name="Raw">The written tally. Null and blank read as an empty tally, which is
		/// success: a settlement that has never been walked has nothing to say.</param>
		/// <param name="Cells">Receives the tally. Never null on return.</param>
		/// <param name="Error">A log-facing note when anything was dropped or clamped, or null
		/// when the string was read whole. Never a reason to fail.</param>
		/// <returns>False only when something was dropped, so a caller that wants to log can;
		/// <paramref name="Cells"/> is usable either way.</returns>
		public static bool TryDecode(string Raw, out List<WornCell> Cells, out string Error)
		{
			Cells = new List<WornCell>();
			Error = null;
			if (string.IsNullOrWhiteSpace(Raw))
			{
				return true;
			}
			int dropped = 0;
			string[] entries = Raw.Split(CellSeparator);
			for (int i = 0; i < entries.Length; i++)
			{
				string entry = entries[i];
				if (string.IsNullOrWhiteSpace(entry))
				{
					continue;
				}
				string[] fields = entry.Split(FieldSeparator);
				if (fields.Length != 3
					|| !int.TryParse(fields[0].Trim(), out var x)
					|| !int.TryParse(fields[1].Trim(), out var y)
					|| !int.TryParse(fields[2].Trim(), out var traffic))
				{
					dropped++;
					continue;
				}
				if (x < 0 || y < 0 || x > MaxCoordinate || y > MaxCoordinate || traffic <= 0)
				{
					dropped++;
					continue;
				}
				if (traffic > MaxTraffic)
				{
					traffic = MaxTraffic;
				}
				int index = IndexOf(Cells, x, y);
				if (index >= 0)
				{
					dropped++;
					if (traffic > Cells[index].Traffic)
					{
						Cells[index] = new WornCell(x, y, traffic);
					}
					continue;
				}
				if (Cells.Count >= MaxTrackedCells)
				{
					dropped++;
					continue;
				}
				Cells.Add(new WornCell(x, y, traffic));
			}
			if (dropped > 0)
			{
				Error = "roads: " + dropped + " unreadable or repeated cells were dropped from a settlement's worn ground";
				return false;
			}
			return true;
		}

		// --- Paving ----------------------------------------------------------------------

		/// <summary>
		/// The floor a settlement's paving is laid as, by the wall it builds in
		/// (<c>KingdomPlotRules.WallBlueprintFor</c>). Every one of these is a vanilla floor
		/// blueprint that already exists in <c>ZoneTerrain.xml</c>; the mod ships no path art of
		/// its own, because the game already has six kinds of paved ground and a seventh would
		/// only be ours.
		/// </summary>
		/// <param name="WallBlueprint">The settlement's wall blueprint. Unknown and null fall
		/// back to the dirt path, which is what an unpaved path already is.</param>
		public static string PavedFloorFor(string WallBlueprint)
		{
			switch (WallBlueprint)
			{
				case "Marble":
					return "MarbleFloor";
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
		/// What the settlement spends to pave in its own material. Tied to the same wall
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
