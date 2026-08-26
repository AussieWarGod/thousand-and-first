namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomLogisticsRules
	{
		/// <summary>
		/// One trip's stop order: nearest-neighbour seeded from the lowest id, improved by 2-opt in
		/// a fixed scan order to a hard iteration cap.
		/// <para>
		/// &sect;3.10(4), and every bound in it is a constant: &le; 8 stops a trip and &le; 50 swap
		/// tests, which is why <see cref="KingdomTripPlan.Operations"/> lands inside the
		/// <c>RoutePlan</c> lane's &asymp; 1,000 int ops rather than needing to be argued about.
		/// </para>
		/// <para>
		/// <paramref name="between"/> is the metric, handed in as a square matrix over
		/// <c>count + 1</c> nodes with the carrier's start at index 0 and stop <c>s</c> at index
		/// <c>s + 1</c>. Handing the metric in rather than reaching for a graph is what lets the
		/// same planner run on the level-1 zone distances and on a level-2 composition without two
		/// implementations of 2-opt existing.
		/// </para>
		/// </summary>
		internal static bool TryPlanTrip(int[] between, int count, out KingdomTripPlan plan, out KingdomCityFault fault)
		{
			plan = new KingdomTripPlan(new int[0], 0, 0, 0, 0);
			if (between == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (count < 0 || count > MaxStopsPerTrip)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			int nodes = count + 1;
			if (between.Length < nodes * nodes)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			fault = KingdomCityFault.None;
			if (count == 0)
			{
				return true;
			}
			int[] order = new int[count];
			bool[] taken = new bool[count];
			int ops = 0;
			// Input is ascending JobId. Stop zero is therefore the named seed, and 2-opt below
			// never moves it: "nearest-neighbour seeded from the lowest JobId" is literal.
			order[0] = 0;
			taken[0] = true;
			int at = 1;
			for (int filled = 1; filled < count; filled++)
			{
				int best = -1;
				int bestCells = 0;
				for (int candidate = 0; candidate < count; candidate++)
				{
					if (taken[candidate])
					{
						continue;
					}
					ops++;
					int cells = between[(at * nodes) + candidate + 1];
					// Ties keep the lower stop index, which is the lower job id: the seed order is
					// the tie-break, so the construction has no draw in it anywhere.
					if (best < 0 || cells < bestCells)
					{
						best = candidate;
						bestCells = cells;
					}
				}
				taken[best] = true;
				order[filled] = best;
				at = best + 1;
			}
			int improvements = 0;
			int tests = 0;
			// A fixed scan order, restarted on an improvement, to a hard test cap. Restarting is
			// what makes the result independent of how many improvements were found before the cap
			// was reached; the cap is what makes it bounded.
			bool improved = true;
			while (improved && tests < MaxSwapTests)
			{
				improved = false;
				for (int i = 1; i < count - 1 && tests < MaxSwapTests; i++)
				{
					for (int j = i + 1; j < count && tests < MaxSwapTests; j++)
					{
						tests++;
						ops += 4;
						if (Delta(between, nodes, order, count, i, j) >= 0)
						{
							continue;
						}
						Reverse(order, i, j);
						improvements++;
						improved = true;
					}
				}
			}
			plan = new KingdomTripPlan(order, count, Length(between, nodes, order, count), ops, improvements);
			return true;
		}

		/// <summary><b>Assertion 2 of &sect;3.10</b>, on real graph-route prefixes. Two trips
		/// from one exact holder carrying one kind may not remain separate when their combined cargo
		/// and stops fit one carrier and their frozen routes share the first edge.</summary>
		internal static bool TryNoTwoHalfEmptyTrips(KingdomLogisticsRequest[] requests,
			int requestCount, int[] tripIndexes, long[] carried, int[] stops, int[] seeds,
			int tripCount, long capacity, out bool held, out int offender,
			out KingdomCityFault fault)
		{
			held = false;
			offender = -1;
			if (requests == null || tripIndexes == null || carried == null
				|| stops == null || seeds == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (requestCount < 0 || requestCount > requests.Length
				|| requestCount > tripIndexes.Length || tripCount < 0
				|| tripCount > carried.Length || tripCount > stops.Length
				|| tripCount > seeds.Length)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (capacity <= 0L)
			{
				fault = KingdomCityFault.InvalidCapacity;
				return false;
			}
			fault = KingdomCityFault.None;
			for (int a = 0; a < tripCount; a++)
			{
				for (int b = a + 1; b < tripCount; b++)
				{
					if (carried[a] + carried[b] > capacity
						|| stops[a] + stops[b] > MaxStopsPerTrip
						|| !SharesRoutePrefix(requests[seeds[a]], requests[seeds[b]]))
					{
						continue;
					}
					offender = b;
					return true;
				}
			}
			held = true;
			return true;
		}

	}
}
