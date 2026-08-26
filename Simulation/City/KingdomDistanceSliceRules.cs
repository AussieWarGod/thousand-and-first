using System;

namespace ThousandAndFirst.Simulation.City
{
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
	internal static partial class KingdomDistanceSliceRules
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

	}
}
