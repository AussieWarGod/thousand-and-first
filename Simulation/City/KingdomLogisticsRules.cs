namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One container the city knows is holding something, as the planner sees it.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.10(1): an input job binds to the closest container
	/// <b>actually holding</b> the resource — not the nearest container of the right kind. The
	/// model knows which container holds what because &sect;3.9's dedication-ordered container
	/// index is exactly that fact, so this row carries the ordinal too: within one ground the
	/// oldest dedication still pays first (I4), and between grounds distance decides (I6).
	/// </para>
	/// </summary>
	internal readonly struct KingdomHolderRow
	{
		/// <summary>The holder's stable id. &sect;3.10(1)'s tie-break key: <i>"Ties break on lower
		/// <c>WorkId</c>: stored, stable, no draw."</i></summary>
		internal readonly int HolderId;

		internal readonly int ZoneIndex;

		/// <summary>The work slot inside the zone, for the level-2 half of the metric. Negative
		/// where only the zone is known, which is what the level-1 graph answers on.</summary>
		internal readonly int WorkSlot;

		/// <summary>Dedication order. Lower is older, and it is the tie-break beneath the id for
		/// two holders standing the same distance away on the same ground.</summary>
		internal readonly int DedicationOrdinal;

		internal readonly KingdomStockKind Holds;

		internal readonly long Amount;

		internal KingdomHolderRow(int holderId, int zoneIndex, int workSlot, int dedicationOrdinal, KingdomStockKind holds, long amount)
		{
			HolderId = holderId;
			ZoneIndex = zoneIndex;
			WorkSlot = workSlot;
			DedicationOrdinal = dedicationOrdinal;
			Holds = holds;
			Amount = amount;
		}
	}

	/// <summary>One trip, as the batcher plans it: the stop order and what the plan cost to make.</summary>
	internal readonly struct KingdomTripPlan
	{
		/// <summary>Indices into the caller's stop array, in visiting order.</summary>
		internal readonly int[] Order;

		internal readonly int StopCount;

		/// <summary>Route length in cells, on the same metric the stops were measured with.</summary>
		internal readonly int Cells;

		/// <summary>Integer operations spent. The <c>RoutePlan</c> lane's own counter
		/// (&sect;0.0: &lsquo;&le; 1,000 int ops a slice&rsquo;), measured rather than asserted.</summary>
		internal readonly int Operations;

		/// <summary>2-opt improvements actually taken.</summary>
		internal readonly int Improvements;

		internal KingdomTripPlan(int[] order, int stopCount, int cells, int operations, int improvements)
		{
			Order = order;
			StopCount = stopCount;
			Cells = cells;
			Operations = operations;
			Improvements = improvements;
		}
	}

	/// <summary>
	/// Central batch planning, not agent AI. LIVING-CITY-ARCHITECTURE &sect;3.10, items 1 and 4.
	/// <para>
	/// <b>The pathologies are unrepresentable here, not mitigated.</b> RimWorld's hauler walks
	/// past a nearer stack because each pawn decides per tick with local knowledge in a world that
	/// is changing while it decides. Nothing in this file decides anything per tick: jobs are
	/// planned at reckon, over a frozen snapshot, with global knowledge, and committed as
	/// itineraries. There is no per-carrier decision for a pathology to live in.
	/// </para>
	/// <para>
	/// <b>Deterministic, with no draw anywhere.</b> Routing is arithmetic. Every ordering here is
	/// resolved by a frozen key — distance, then holder id, then dedication ordinal — so a reload
	/// re-plans the same trip. Draws remain only for flavour, on <c>taf:stream:delivery</c>
	/// (&sect;2.4), and <c>KingdomBudgetRules.PlannerMaxDraws</c> is zero for this lane.
	/// </para>
	/// <para>
	/// <b>The bar is "never looks stupid", and it is asserted rather than hoped for.</b>
	/// <see cref="TryNoNearerHolder"/> and <see cref="TryNoTwoHalfEmptyTrips"/> are the two checks
	/// &sect;3.10 names, written as functions so the tests and the runtime ask <i>one</i>
	/// implementation of them rather than two that can drift.
	/// </para>
	/// <para>
	/// Pure, engine-free and total.
	/// </para>
	/// </summary>
	internal static class KingdomLogisticsRules
	{
		/// <summary>Open jobs one slice's planner will look at. &sect;3.10(4).</summary>
		internal const int MaxJobsConsidered = KingdomBudgetRules.PlannerMaxJobs;

		/// <summary>Stops one trip may make. &sect;3.10(4).</summary>
		internal const int MaxStopsPerTrip = KingdomBudgetRules.PlannerMaxStops;

		/// <summary>2-opt swap tests one plan may spend. &sect;3.10(4).</summary>
		internal const int MaxSwapTests = KingdomBudgetRules.PlannerMaxSwapTests;

		/// <summary>No route to this holder. Same sentinel the distance stores use.</summary>
		internal const int NoRoute = KingdomDistanceRules.NoRoute;

		// ==================================================================================
		// (1) Nearest-holder sourcing
		// ==================================================================================

		/// <summary>
		/// Measures every holder against one destination on the level-1 zone graph.
		/// <para>
		/// The graph is the half of the metric that needs no ground: it is composed from zone ids
		/// alone, so it may be built at reckon, which &sect;3.10(2) forbids the level-2 slices
		/// from being. A holder on unreachable ground reads <see cref="NoRoute"/> and is passed
		/// over rather than picked at an invented distance.
		/// </para>
		/// </summary>
		internal static bool TryMeasure(KingdomHolderRow[] holders, int count, KingdomZoneGraph graph, int destZoneIndex, int[] distances, out KingdomCityFault fault)
		{
			if (holders == null || distances == null || graph == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (count < 0 || count > holders.Length || count > distances.Length)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			fault = KingdomCityFault.None;
			for (int i = 0; i < count; i++)
			{
				int cells;
				distances[i] = graph.TryDistance(holders[i].ZoneIndex, destZoneIndex, out cells) ? cells : NoRoute;
			}
			return true;
		}

		/// <summary>
		/// The closest container actually holding the resource.
		/// <para>
		/// &sect;3.10(1), stated as an order and not a preference: strictly smaller distance wins;
		/// then the lower holder id; then the older dedication. Every key is a stored fact, so the
		/// same snapshot always yields the same holder — on this machine, on a reload, and in the
		/// test that asserts it.
		/// </para>
		/// <para>
		/// Returns <c>false</c> with <see cref="KingdomCityFault.None"/> when nothing of the kind
		/// is held anywhere reachable. That is an ordinary answer — the city has none — and not a
		/// fault.
		/// </para>
		/// </summary>
		internal static bool TryNearestHolder(KingdomHolderRow[] holders, int count, int[] distances, KingdomStockKind kind, out int chosen, out KingdomCityFault fault)
		{
			chosen = -1;
			if (holders == null || distances == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (count < 0 || count > holders.Length || count > distances.Length)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			fault = KingdomCityFault.None;
			for (int i = 0; i < count; i++)
			{
				if (!Eligible(holders[i], distances[i], kind))
				{
					continue;
				}
				if (chosen < 0 || Nearer(holders[i], distances[i], holders[chosen], distances[chosen]))
				{
					chosen = i;
				}
			}
			return chosen >= 0;
		}

		/// <summary>
		/// <b>Assertion 1 of &sect;3.10</b>: no carrier crosses the city past a nearer holder.
		/// <para>
		/// For a completed fetch, no container holding that resource had a strictly smaller
		/// <c>Dist</c> at plan time. Written against the chosen holder's <i>id</i> rather than its
		/// index, because that is what the job row persists and therefore what a check after a
		/// reload can actually ask about.
		/// </para>
		/// </summary>
		/// <param name="chosenHolderId">The holder the plan bound to.</param>
		/// <param name="offender">The holder id that was nearer, or <c>-1</c> when the check
		/// passes. Named, so the failure says which one rather than that one exists.</param>
		internal static bool TryNoNearerHolder(KingdomHolderRow[] holders, int count, int[] distances, KingdomStockKind kind, int chosenHolderId, out bool held, out int offender, out KingdomCityFault fault)
		{
			held = false;
			offender = -1;
			if (holders == null || distances == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (count < 0 || count > holders.Length || count > distances.Length)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			fault = KingdomCityFault.None;
			int chosen = -1;
			for (int i = 0; i < count; i++)
			{
				if (holders[i].HolderId == chosenHolderId)
				{
					chosen = i;
					break;
				}
			}
			if (chosen < 0 || !Eligible(holders[chosen], distances[chosen], kind))
			{
				// A fetch bound to a holder that is not in the index, or is not holding the kind
				// it was fetched for, is a worse failure than a long walk and says so.
				return true;
			}
			for (int i = 0; i < count; i++)
			{
				if (i == chosen || !Eligible(holders[i], distances[i], kind))
				{
					continue;
				}
				if (Nearer(holders[i], distances[i], holders[chosen], distances[chosen]))
				{
					offender = holders[i].HolderId;
					return true;
				}
			}
			held = true;
			return true;
		}

		// ==================================================================================
		// (4) Capacity-bound batching
		// ==================================================================================

		/// <summary>
		/// Groups open jobs into trips: same destination, inside one carrier's capacity, inside
		/// the stop cap.
		/// <para>
		/// &sect;3.10(4)'s <i>"group by carrier capacity and route overlap"</i>. Route overlap is
		/// taken at the honest granularity the model has, which is the ground: two loads bound for
		/// the same zone share a road for the whole of it, and a third bound elsewhere does not.
		/// Jobs are walked in ascending id so the grouping is seeded exactly where the
		/// nearest-neighbour construction below is seeded, and a load that fits no open trip opens
		/// the next one.
		/// </para>
		/// </summary>
		/// <param name="jobIds">Open job ids, ascending. The caller's order is the seed order.</param>
		/// <param name="destZone">Each job's destination zone index.</param>
		/// <param name="loads">Each job's load.</param>
		/// <param name="capacity">One carrier's capacity.</param>
		/// <param name="trip">Filled with the trip index each job was put on.</param>
		/// <param name="tripCount">How many trips the slice needs.</param>
		internal static bool TryBatch(int[] jobIds, int[] destZone, long[] loads, int count, long capacity, int[] trip, out int tripCount, out KingdomCityFault fault)
		{
			tripCount = 0;
			if (jobIds == null || destZone == null || loads == null || trip == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (count < 0 || count > jobIds.Length || count > destZone.Length || count > loads.Length || count > trip.Length)
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
			int considered = (count < MaxJobsConsidered) ? count : MaxJobsConsidered;
			for (int i = 0; i < count; i++)
			{
				trip[i] = -1;
			}
			long[] carried = new long[considered];
			int[] stops = new int[considered];
			int[] toward = new int[considered];
			for (int i = 0; i < considered; i++)
			{
				if (loads[i] <= 0L)
				{
					continue;
				}
				int found = -1;
				for (int t = 0; t < tripCount; t++)
				{
					if (toward[t] == destZone[i] && stops[t] < MaxStopsPerTrip && carried[t] + loads[i] <= capacity)
					{
						found = t;
						break;
					}
				}
				if (found < 0)
				{
					found = tripCount++;
					toward[found] = destZone[i];
					carried[found] = 0L;
					stops[found] = 0;
				}
				trip[i] = found;
				carried[found] += loads[i];
				stops[found]++;
			}
			return true;
		}

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
			int at = 0;
			for (int filled = 0; filled < count; filled++)
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
				for (int i = 0; i < count - 1 && tests < MaxSwapTests; i++)
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

		/// <summary>
		/// <b>Assertion 2 of &sect;3.10</b>: no two half-empty trips where one would do.
		/// <para>
		/// No two trips planned in the same slice share a route prefix while both run under
		/// capacity. The prefix is taken as the destination the trip is bound for, which is what
		/// <see cref="TryBatch"/> groups on: two carriers walking to the same ground with room to
		/// spare between them is precisely the second trip the batcher was supposed to have
		/// absorbed.
		/// </para>
		/// </summary>
		/// <param name="offender">The trip index that should have been folded into an earlier one,
		/// or <c>-1</c> when the check passes.</param>
		internal static bool TryNoTwoHalfEmptyTrips(int[] toward, long[] carried, int tripCount, long capacity, out bool held, out int offender, out KingdomCityFault fault)
		{
			held = false;
			offender = -1;
			if (toward == null || carried == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (tripCount < 0 || tripCount > toward.Length || tripCount > carried.Length)
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
					if (toward[a] != toward[b])
					{
						continue;
					}
					if (carried[a] + carried[b] <= capacity)
					{
						offender = b;
						return true;
					}
				}
			}
			held = true;
			return true;
		}

		// ==================================================================================
		// Internals
		// ==================================================================================

		private static bool Eligible(KingdomHolderRow holder, int distance, KingdomStockKind kind)
		{
			return holder.Holds == kind && holder.Amount > 0L && distance >= 0 && distance < NoRoute;
		}

		/// <summary>The frozen order of &sect;3.10(1): distance, then holder id, then dedication.
		/// Written out rather than delegated to a comparer, because the comparison IS the
		/// invariant.</summary>
		private static bool Nearer(KingdomHolderRow candidate, int candidateCells, KingdomHolderRow standing, int standingCells)
		{
			if (candidateCells != standingCells)
			{
				return candidateCells < standingCells;
			}
			if (candidate.HolderId != standing.HolderId)
			{
				return candidate.HolderId < standing.HolderId;
			}
			return candidate.DedicationOrdinal < standing.DedicationOrdinal;
		}

		private static int Length(int[] between, int nodes, int[] order, int count)
		{
			int total = 0;
			int at = 0;
			for (int i = 0; i < count; i++)
			{
				total += between[(at * nodes) + order[i] + 1];
				at = order[i] + 1;
			}
			return total;
		}

		/// <summary>What reversing <c>order[i..j]</c> would cost, as the two edges it replaces
		/// against the two it lays. A closed form rather than a re-measure of the whole route,
		/// which is what keeps a swap test at four operations.</summary>
		private static int Delta(int[] between, int nodes, int[] order, int count, int i, int j)
		{
			int before = (i == 0) ? 0 : (order[i - 1] + 1);
			int after = (j == count - 1) ? -1 : (order[j + 1] + 1);
			int head = order[i] + 1;
			int tail = order[j] + 1;
			int was = between[(before * nodes) + head];
			int now = between[(before * nodes) + tail];
			if (after >= 0)
			{
				was += between[(tail * nodes) + after];
				now += between[(head * nodes) + after];
			}
			return now - was;
		}

		private static void Reverse(int[] order, int i, int j)
		{
			while (i < j)
			{
				int swap = order[i];
				order[i] = order[j];
				order[j] = swap;
				i++;
				j--;
			}
		}
	}
}
