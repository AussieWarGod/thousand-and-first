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
	public static partial class KingdomRoadRules
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

	}
}
