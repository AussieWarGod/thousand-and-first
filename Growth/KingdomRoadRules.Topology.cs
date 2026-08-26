using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomRoadRules
	{
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
		public const string GatehouseKey = KingdomGatehouseRules.BuildKey;

		/// <summary>Whether a design is sited at the gate rather than anywhere along the wall.
		/// Case-folded, so a third-party file spelling the key differently still lands.</summary>
		public static bool SitesAtGate(string Key)
		{
			return KingdomGatehouseRules.IsGatehouse(Key);
		}

		/// <summary>
		/// Legacy deterministic ranking of offered frontier cells around a road endpoint.
		/// The multi-cell gatehouse no longer uses this fallback: its frozen topology is astride
		/// the exact <see cref="TryGate"/> cell and refuses if any owned/path cell is obstructed.
		/// <para>
		/// Retained as a pure coordinate helper for compatibility tests and third-party callers;
		/// handed an empty list it answers -1.
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

	}
}
