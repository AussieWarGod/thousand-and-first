using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomRoadRules
	{
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

	}
}
