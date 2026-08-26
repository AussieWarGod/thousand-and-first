using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomLayoutRules
	{
		/// <summary>Chebyshev distance, the one Qud walks in.</summary>
		public static int Chebyshev(int AX, int AY, int BX, int BY)
		{
			int dx = (AX > BX) ? (AX - BX) : (BX - AX);
			int dy = (AY > BY) ? (AY - BY) : (BY - AY);
			return (dx > dy) ? dx : dy;
		}

		/// <summary>
		/// The settled heart: the mean position of everything raised that is not a wall, because
		/// a wall is by definition at the edge and would drag the centre out to it. Falls back to
		/// the mean of the defensive works when there are at least
		/// <see cref="WallsFormHeartMinimum"/> of them and nothing else &mdash; the inside of a
		/// line you have already drawn is a meaningful centre, and one post is not.
		/// </summary>
		/// <returns>False when the settlement has no shape yet, in which case
		/// <paramref name="X"/> and <paramref name="Y"/> are zero and mean nothing.</returns>
		public static bool TryHeart(IList<LayoutMark> Marks, out int X, out int Y)
		{
			X = 0;
			Y = 0;
			if (Marks == null)
			{
				return false;
			}
			int sumX = 0;
			int sumY = 0;
			int count = 0;
			int wallX = 0;
			int wallY = 0;
			int walls = 0;
			for (int i = 0; i < Marks.Count; i++)
			{
				if (Marks[i].Purpose == LayoutPurpose.Defence)
				{
					wallX += Marks[i].X;
					wallY += Marks[i].Y;
					walls++;
					continue;
				}
				sumX += Marks[i].X;
				sumY += Marks[i].Y;
				count++;
			}
			if (count > 0)
			{
				X = (sumX + count / 2) / count;
				Y = (sumY + count / 2) / count;
				return true;
			}
			if (walls >= WallsFormHeartMinimum)
			{
				X = (wallX + walls / 2) / walls;
				Y = (wallY + walls / 2) / walls;
				return true;
			}
			return false;
		}

		/// <summary>Distance to the nearest work of one purpose.</summary>
		/// <returns>False when nothing of that purpose stands yet.</returns>
		public static bool TryNearest(IList<LayoutMark> Marks, LayoutPurpose Purpose, int X, int Y, out int Distance)
		{
			Distance = 0;
			if (Marks == null)
			{
				return false;
			}
			bool found = false;
			for (int i = 0; i < Marks.Count; i++)
			{
				if (Marks[i].Purpose != Purpose)
				{
					continue;
				}
				int distance = Chebyshev(X, Y, Marks[i].X, Marks[i].Y);
				if (!found || distance < Distance)
				{
					Distance = distance;
					found = true;
				}
			}
			return found;
		}

		/// <summary>Distance to the nearest work of any purpose.</summary>
		/// <returns>False when nothing stands yet.</returns>
		public static bool TryNearestAny(IList<LayoutMark> Marks, int X, int Y, out int Distance)
		{
			Distance = 0;
			if (Marks == null)
			{
				return false;
			}
			bool found = false;
			for (int i = 0; i < Marks.Count; i++)
			{
				int distance = Chebyshev(X, Y, Marks[i].X, Marks[i].Y);
				if (!found || distance < Distance)
				{
					Distance = distance;
					found = true;
				}
			}
			return found;
		}

		/// <summary>How many works of one purpose the settlement has standing here.</summary>
		public static int CountOf(IList<LayoutMark> Marks, LayoutPurpose Purpose)
		{
			if (Marks == null)
			{
				return 0;
			}
			int count = 0;
			for (int i = 0; i < Marks.Count; i++)
			{
				if (Marks[i].Purpose == Purpose)
				{
					count++;
				}
			}
			return count;
		}

		/// <summary>How many works of one purpose stand within <paramref name="Radius"/>.</summary>
		public static int CountWithin(IList<LayoutMark> Marks, LayoutPurpose Purpose, int X, int Y, int Radius)
		{
			if (Marks == null)
			{
				return 0;
			}
			int count = 0;
			for (int i = 0; i < Marks.Count; i++)
			{
				if (Marks[i].Purpose == Purpose && Chebyshev(X, Y, Marks[i].X, Marks[i].Y) <= Radius)
				{
					count++;
				}
			}
			return count;
		}

		/// <summary>
		/// Whether the plan has anything to say about siting this purpose here. False means the
		/// founder decides, and is the honest answer in three cases: nothing is built yet, the
		/// purpose is one the ground decides (<see cref="LayoutPurpose.Sited"/>) or one the plan
		/// does not recognise, or a wall is wanted where there is neither a frontier to put it on
		/// nor a line to extend.
		/// </summary>
		public static bool HasOpinion(LayoutPurpose Purpose, IList<LayoutMark> Marks, KingdomRules.Frontier Edges)
		{
			if (Marks == null || Marks.Count == 0)
			{
				return false;
			}
			if (Purpose == LayoutPurpose.Defence)
			{
				return Edges != KingdomRules.Frontier.None && CountOf(Marks, LayoutPurpose.Defence) > 0;
			}
			if (Purpose == LayoutPurpose.Unknown || Purpose == LayoutPurpose.Sited)
			{
				return false;
			}
			if (TryHeart(Marks, out _, out _))
			{
				return true;
			}
			return CountOf(Marks, Purpose) > 0;
		}
	}
}
