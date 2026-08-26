using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>One live-ground endpoint offered to the sparse distance cache. The stable id is
	/// the key retained by the matrix; coordinates are used only while this zone is rendered.</summary>
	internal readonly struct KingdomDistancePoint
	{
		internal readonly int Id;

		internal readonly short X;

		internal readonly short Y;

		internal KingdomDistancePoint(int id, int x, int y)
		{
			Id = id;
			X = (short)x;
			Y = (short)y;
		}
	}

	/// <summary>
	/// Live-ground measurement for one level-2 slice.
	/// <para>
	/// Six reverse Dijkstra fields answer every broad candidate's edge distances; one additional
	/// bounded pass per retained sparse endpoint answers same-zone pairs. Every pass covers at most
	/// one vanilla zone. Eight-way movement matches Qud walking. A step touching paved ground costs
	/// <see cref="KingdomItineraryRules.RoadDiscountPercent"/> percent; any other step costs 100.
	/// Thus roads participate in the search instead of being a decorative discount applied after
	/// an unrelated straight-line estimate.
	/// </para>
	/// <para>
	/// Pure and engine-free. The runtime supplies frozen passability/paving arrays while the zone
	/// is rendered; reckon never calls this and never receives a Zone, Cell or GameObject.
	/// </para>
	/// </summary>
	internal static class KingdomDistanceSliceRules
	{
		internal const int MaxCells = 80 * 25;

		internal const int MaxCandidateEndpoints = 512;

		private const int PlainStep = 100;

		private static readonly int[] StepX = new int[8] { 0, 1, 1, 1, 0, -1, -1, -1 };

		private static readonly int[] StepY = new int[8] { -1, -1, 0, 1, 1, 1, 0, -1 };

		/// <summary>Measures work-to-edge values for a possibly broad render-time candidate set.
		/// The returned array is temporary: callers select a bounded sparse working set before any
		/// value enters <see cref="KingdomDistanceMatrix"/>.</summary>
		internal static bool TryMeasureEdges(bool[] passable, bool[] paved, int width, int height,
			KingdomDistancePoint[] points, int count,
			int upX, int upY, int downX, int downY,
			out ushort[] edges, out long operations, out KingdomCityFault fault)
		{
			edges = null;
			operations = 0L;
			if (!ValidGrid(passable, paved, width, height) || points == null || count < 0
				|| count > points.Length || count > MaxCandidateEndpoints)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (!ValidPoints(points, count, width, height))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			// Roads are symmetric, so six reverse searches answer every candidate: four seeded
			// from the passable boundary cells and two from the exact shaft receipts. This keeps
			// the broad render scan bounded by zone area, not candidate count × zone area.
			int[][] fields = new int[KingdomDistanceRules.EdgesPerZone][];
			for (int edge = 0; edge < KingdomDistanceRules.EdgesPerZone; edge++)
			{
				int[] seeds;
				int seedCount;
				Seeds(passable, width, height, (KingdomZoneStep)edge,
					upX, upY, downX, downY, out seeds, out seedCount);
				long spent;
				if (!TryDistances(passable, paved, width, height, seeds, seedCount,
					out fields[edge], out spent))
				{
					fault = KingdomCityFault.OutsideItinerary;
					return false;
				}
				operations += spent;
			}
			ushort[] measured = new ushort[count * KingdomDistanceRules.EdgesPerZone];
			for (int i = 0; i < count; i++)
			{
				int offset = i * KingdomDistanceRules.EdgesPerZone;
				for (int edge = 0; edge < KingdomDistanceRules.EdgesPerZone; edge++)
				{
					measured[offset + edge] = Point(fields[edge], passable, paved,
						width, height, points[i].X, points[i].Y);
				}
			}
			edges = measured;
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>Measures a complete sparse slice: six edge values per retained endpoint and
		/// one triangular same-zone value per retained pair. Both arrays are published only after
		/// every bounded search succeeds.</summary>
		internal static bool TryMeasure(bool[] passable, bool[] paved, int width, int height,
			KingdomDistancePoint[] points, int count,
			int upX, int upY, int downX, int downY,
			out ushort[] edges, out ushort[] pairs, out long operations, out KingdomCityFault fault)
		{
			edges = null;
			pairs = null;
			operations = 0L;
			if (count < 0 || count * KingdomDistanceRules.EdgesPerZone
				> KingdomDistanceRules.MaxWorkEdgeEntries
				|| KingdomDistanceRules.PairSlots(count) > KingdomDistanceRules.MaxSamePairEntries)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			ushort[] measuredEdges;
			long edgeOperations;
			if (!TryMeasureEdges(passable, paved, width, height, points, count,
				upX, upY, downX, downY, out measuredEdges, out edgeOperations, out fault))
			{
				return false;
			}
			ushort[] measuredPairs = new ushort[KingdomDistanceRules.PairSlots(count)];
			long pairOperations = 0L;
			for (int i = 0; i < count; i++)
			{
				int[] distance;
				long spent;
				if (!TryDistances(passable, paved, width, height, points[i].X, points[i].Y,
					out distance, out spent))
				{
					fault = KingdomCityFault.OutsideItinerary;
					return false;
				}
				pairOperations += spent;
				for (int j = i + 1; j < count; j++)
				{
					int index;
					if (!KingdomDistanceRules.TryPairIndex(i, j, count, out index, out fault))
					{
						return false;
					}
					measuredPairs[index] = Point(distance, passable, paved, width, height,
						points[j].X, points[j].Y);
				}
			}
			edges = measuredEdges;
			pairs = measuredPairs;
			operations = edgeOperations + pairOperations;
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>Measures against the one structural portal frozen for each city edge. Unlike
		/// boundary-seeded estimates, every returned cost ends at the exact cell the itinerary uses.
		/// Portal-to-portal values price each intermediate zone's actually traversed paved segment.</summary>
		internal static bool TryMeasureExact(bool[] passable, bool[] paved, int width, int height,
			KingdomDistancePoint[] points, int count, short[] portalX, short[] portalY,
			bool includePairs, out ushort[] edges, out ushort[] pairs, out ushort[] portalPairs,
			out long operations, out KingdomCityFault fault)
		{
			edges = pairs = portalPairs = null;
			operations = 0L;
			if (!ValidGrid(passable, paved, width, height) || points == null
				|| count < 0 || count > points.Length || count > MaxCandidateEndpoints
				|| portalX == null || portalY == null
				|| portalX.Length < KingdomDistanceRules.EdgesPerZone
				|| portalY.Length < KingdomDistanceRules.EdgesPerZone
				|| !ValidPoints(points, count, width, height))
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			ushort[] measuredEdges = new ushort[count * KingdomDistanceRules.EdgesPerZone];
			ushort[] measuredPortals = new ushort[KingdomDistanceRules.EdgesPerZone
				* KingdomDistanceRules.EdgesPerZone];
			for (int i = 0; i < measuredEdges.Length; i++)
				measuredEdges[i] = (ushort)KingdomDistanceRules.NoRoute;
			for (int i = 0; i < measuredPortals.Length; i++)
				measuredPortals[i] = (ushort)KingdomDistanceRules.NoRoute;
			for (int edge = 0; edge < KingdomDistanceRules.EdgesPerZone; edge++)
			{
				int x = portalX[edge];
				int y = portalY[edge];
				if (x < 0 || y < 0 || x >= width || y >= height
					|| !passable[y * width + x]) continue;
				int[] field;
				long spent;
				if (!TryDistances(passable, paved, width, height, x, y, out field, out spent))
				{
					fault = KingdomCityFault.OutsideItinerary;
					return false;
				}
				operations += spent;
				for (int i = 0; i < count; i++)
					measuredEdges[i * KingdomDistanceRules.EdgesPerZone + edge]
						= Point(field, passable, paved, width, height, points[i].X, points[i].Y);
				for (int other = 0; other < KingdomDistanceRules.EdgesPerZone; other++)
					measuredPortals[edge * KingdomDistanceRules.EdgesPerZone + other]
						= Point(field, passable, paved, width, height,
							portalX[other], portalY[other]);
			}
			ushort[] measuredPairs = new ushort[includePairs
				? KingdomDistanceRules.PairSlots(count) : 0];
			if (includePairs)
			{
				for (int i = 0; i < count; i++)
				{
					int[] field;
					long spent;
					if (!TryDistances(passable, paved, width, height, points[i].X, points[i].Y,
						out field, out spent))
					{
						fault = KingdomCityFault.OutsideItinerary;
						return false;
					}
					operations += spent;
					for (int j = i + 1; j < count; j++)
					{
						int at;
						if (!KingdomDistanceRules.TryPairIndex(i, j, count, out at, out fault))
							return false;
						measuredPairs[at] = Point(field, passable, paved, width, height,
							points[j].X, points[j].Y);
					}
				}
			}
			edges = measuredEdges;
			pairs = measuredPairs;
			portalPairs = measuredPortals;
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>Exact live-origin path to one boundary. Used only while a coordinate source
		/// is rendered; the chosen boundary cell and paved cost are frozen into the reservation.</summary>
		internal static bool TryMeasurePointToEdge(bool[] passable, bool[] paved, int width,
			int height, int sourceX, int sourceY, KingdomZoneStep edge,
			out int cells, out short exitX, out short exitY, out long operations)
		{
			cells = 0; exitX = exitY = -1; operations = 0L;
			if (!ValidGrid(passable, paved, width, height) || sourceX < 0 || sourceY < 0
				|| sourceX >= width || sourceY >= height || (int)edge < 0
				|| (int)edge > (int)KingdomZoneStep.West) return false;
			int[] field;
			if (!TryDistances(passable, paved, width, height, sourceX, sourceY,
				out field, out operations)) return false;
			int limit = (edge == KingdomZoneStep.North || edge == KingdomZoneStep.South)
				? width : height;
			ushort best = (ushort)KingdomDistanceRules.NoRoute;
			for (int offset = 0; offset < limit; offset++)
			{
				int x = (edge == KingdomZoneStep.West) ? 0
					: ((edge == KingdomZoneStep.East) ? width - 1 : offset);
				int y = (edge == KingdomZoneStep.North) ? 0
					: ((edge == KingdomZoneStep.South) ? height - 1 : offset);
				ushort value = Point(field, passable, paved, width, height, x, y);
				if (value >= best) continue;
				best = value; exitX = (short)x; exitY = (short)y;
			}
			if (best >= KingdomDistanceRules.NoRoute) return false;
			cells = best;
			return true;
		}

		internal static bool TryMeasurePointToPoint(bool[] passable, bool[] paved, int width,
			int height, int sourceX, int sourceY, int targetX, int targetY,
			out int cells, out long operations)
		{
			cells = 0; operations = 0L;
			if (!ValidGrid(passable, paved, width, height) || sourceX < 0 || sourceY < 0
				|| targetX < 0 || targetY < 0 || sourceX >= width || targetX >= width
				|| sourceY >= height || targetY >= height) return false;
			int[] field;
			if (!TryDistances(passable, paved, width, height, sourceX, sourceY,
				out field, out operations)) return false;
			int value = Point(field, passable, paved, width, height, targetX, targetY);
			if (value >= KingdomDistanceRules.NoRoute) return false;
			cells = value;
			return true;
		}

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
