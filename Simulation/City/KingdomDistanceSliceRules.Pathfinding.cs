using System;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomDistanceSliceRules
	{
		private static bool TryDistances(bool[] passable, bool[] paved, int width, int height,
			int sourceX, int sourceY, out int[] distance, out long operations)
		{
			return TryDistances(passable, paved, width, height,
				new int[1] { sourceY * width + sourceX }, 1, out distance, out operations);
		}

		private static bool TryDistances(bool[] passable, bool[] paved, int width, int height,
			int[] sources, int sourceCount, out int[] distance, out long operations)
		{
			int area = width * height;
			if (sources == null || sourceCount < 0 || sourceCount > sources.Length)
			{
				distance = null;
				operations = 0L;
				return false;
			}
			distance = new int[area];
			operations = 0L;
			int[] heap = new int[area];
			int[] position = new int[area];
			for (int i = 0; i < area; i++)
			{
				distance[i] = int.MaxValue;
				position[i] = -1;
			}
			int heapCount = 0;
			for (int i = 0; i < sourceCount; i++)
			{
				int source = sources[i];
				if (source < 0 || source >= area) return false;
				if (distance[source] == 0) continue;
				distance[source] = 0;
				PushOrLower(heap, position, distance, ref heapCount, source);
			}
			while (heapCount > 0)
			{
				int current = Pop(heap, position, distance, ref heapCount);
				int cx = current % width;
				int cy = current / width;
				for (int d = 0; d < StepX.Length; d++)
				{
					operations++;
					int nx = cx + StepX[d];
					int ny = cy + StepY[d];
					if (nx < 0 || ny < 0 || nx >= width || ny >= height)
					{
						continue;
					}
					int next = ny * width + nx;
					if (!passable[next])
					{
						continue;
					}
					int cost = (paved[current] || paved[next])
						? KingdomItineraryRules.RoadDiscountPercent : PlainStep;
					int candidate = distance[current] + cost;
					if (candidate >= distance[next])
					{
						continue;
					}
					distance[next] = candidate;
					PushOrLower(heap, position, distance, ref heapCount, next);
				}
			}
			return true;
		}

		private static void Seeds(bool[] passable, int width, int height, KingdomZoneStep step,
			int upX, int upY, int downX, int downY, out int[] seeds, out int count)
		{
			if (step == KingdomZoneStep.North || step == KingdomZoneStep.South)
			{
				seeds = new int[width];
				count = 0;
				int y = (step == KingdomZoneStep.North) ? 0 : height - 1;
				for (int x = 0; x < width; x++)
				{
					int at = y * width + x;
					if (passable[at]) seeds[count++] = at;
				}
				return;
			}
			if (step == KingdomZoneStep.East || step == KingdomZoneStep.West)
			{
				seeds = new int[height];
				count = 0;
				int x = (step == KingdomZoneStep.West) ? 0 : width - 1;
				for (int y = 0; y < height; y++)
				{
					int at = y * width + x;
					if (passable[at]) seeds[count++] = at;
				}
				return;
			}
			int x_ = (step == KingdomZoneStep.Up) ? upX : downX;
			int y_ = (step == KingdomZoneStep.Up) ? upY : downY;
			if (x_ < 0 || y_ < 0 || x_ >= width || y_ >= height)
			{
				seeds = new int[0];
				count = 0;
				return;
			}
			seeds = new int[1] { y_ * width + x_ };
			count = 1;
		}

		private static ushort Point(int[] distance, bool[] passable, bool[] paved,
			int width, int height, int x, int y)
		{
			if (x < 0 || y < 0 || x >= width || y >= height)
			{
				return (ushort)KingdomDistanceRules.NoRoute;
			}
			int at = y * width + x;
			int best = distance[at];
			if (!passable[at] || best == int.MaxValue)
			{
				best = int.MaxValue;
				for (int d = 0; d < StepX.Length; d++)
				{
					int nx = x + StepX[d];
					int ny = y + StepY[d];
					if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
					int near = ny * width + nx;
					if (distance[near] == int.MaxValue) continue;
					int cost = (paved[near] || paved[at])
						? KingdomItineraryRules.RoadDiscountPercent : PlainStep;
					long candidate = (long)distance[near] + cost;
					if (candidate < best) best = (int)candidate;
				}
			}
			return Cells(best);
		}

		private static ushort Cells(int scaled)
		{
			if (scaled == int.MaxValue)
			{
				return (ushort)KingdomDistanceRules.NoRoute;
			}
			long cells = ((long)scaled + PlainStep - 1L) / PlainStep;
			return (cells >= KingdomDistanceRules.NoRoute)
				? (ushort)KingdomDistanceRules.NoRoute : (ushort)cells;
		}

		private static bool ValidGrid(bool[] passable, bool[] paved, int width, int height)
		{
			if (passable == null || paved == null || width <= 0 || height <= 0)
			{
				return false;
			}
			long area = (long)width * height;
			return area > 0L && area <= MaxCells && passable.Length == area && paved.Length == area;
		}

		private static bool ValidPoints(KingdomDistancePoint[] points, int count, int width, int height)
		{
			for (int i = 0; i < count; i++)
			{
				if (points[i].Id <= 0 || points[i].X < 0 || points[i].Y < 0
					|| points[i].X >= width || points[i].Y >= height)
				{
					return false;
				}
				for (int j = 0; j < i; j++)
				{
					if (points[i].Id == points[j].Id) return false;
				}
			}
			return true;
		}

		private static void PushOrLower(int[] heap, int[] position, int[] distance,
			ref int count, int node)
		{
			int at = position[node];
			if (at < 0)
			{
				at = count++;
				heap[at] = node;
				position[node] = at;
			}
			while (at > 0)
			{
				int parent = (at - 1) / 2;
				if (distance[heap[parent]] <= distance[heap[at]]) break;
				Swap(heap, position, parent, at);
				at = parent;
			}
		}

		private static int Pop(int[] heap, int[] position, int[] distance, ref int count)
		{
			int result = heap[0];
			position[result] = -1;
			count--;
			if (count <= 0) return result;
			heap[0] = heap[count];
			position[heap[0]] = 0;
			int at = 0;
			while (true)
			{
				int left = at * 2 + 1;
				if (left >= count) break;
				int right = left + 1;
				int child = (right < count && distance[heap[right]] < distance[heap[left]])
					? right : left;
				if (distance[heap[at]] <= distance[heap[child]]) break;
				Swap(heap, position, at, child);
				at = child;
			}
			return result;
		}

		private static void Swap(int[] heap, int[] position, int a, int b)
		{
			int value = heap[a];
			heap[a] = heap[b];
			heap[b] = value;
			position[heap[a]] = a;
			position[heap[b]] = b;
		}
	}
}
