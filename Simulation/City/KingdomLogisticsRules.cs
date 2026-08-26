namespace ThousandAndFirst.Simulation.City
{
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
	internal static partial class KingdomLogisticsRules
	{
		/// <summary>Open jobs one slice's planner will look at. &sect;3.10(4).</summary>
		internal const int MaxJobsConsidered = KingdomBudgetRules.PlannerMaxJobs;

		/// <summary>Stops one trip may make. &sect;3.10(4).</summary>
		internal const int MaxStopsPerTrip = KingdomBudgetRules.PlannerMaxStops;

		/// <summary>2-opt swap tests one plan may spend. &sect;3.10(4).</summary>
		internal const int MaxSwapTests = KingdomBudgetRules.PlannerMaxSwapTests;

		/// <summary>One visible carrier's physical load, in drams or food units. Capacity belongs to
		/// planner rules, not renderer budget: every persisted trip and every body use this value.</summary>
		internal const int CarrierCapacity = 12;

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

	}
}
