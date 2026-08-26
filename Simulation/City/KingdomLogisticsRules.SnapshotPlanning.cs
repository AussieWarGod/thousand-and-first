namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomLogisticsRules
	{
		// ==================================================================================
		// (4) Capacity-bound batching
		// ==================================================================================

		/// <summary>Plans one production slice over an immutable request snapshot. Batching requires
		/// one exact pickup holder, one cargo kind, spare capacity, spare stop count, and a genuine
		/// shared graph-route prefix (same first level-1 edge). Destination equality is neither
		/// required nor sufficient. Each trip is then ordered by the bounded nearest-neighbour plus
		/// fixed 2-opt planner below.</summary>
		internal static bool TryPlanSnapshot(KingdomLogisticsRequest[] requests, int count,
			int[] betweenDestinations, long capacity, out KingdomLogisticsSnapshotPlan plan,
			out KingdomCityFault fault)
		{
			plan = null;
			if (requests == null || betweenDestinations == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (count < 0 || count > requests.Length
				|| betweenDestinations.Length < count * count)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (capacity <= 0L)
			{
				fault = KingdomCityFault.InvalidCapacity;
				return false;
			}
			int considered = (count < MaxJobsConsidered) ? count : MaxJobsConsidered;
			int[] tripIndexes = new int[count];
			int[] leaders = new int[count];
			int[] ordinals = new int[count];
			for (int i = 0; i < count; i++) tripIndexes[i] = -1;
			for (int i = 0; i < considered; i++)
			{
				KingdomLogisticsRequest row = requests[i];
				if (row.JobId <= 0 || (i > 0 && row.JobId <= requests[i - 1].JobId)
					|| row.SourceEndpointId <= 0 || row.DestinationEndpointId <= 0
					|| row.SourceZoneIndex < 0 || row.DestinationZoneIndex < 0
					|| (row.CargoAuthority != KingdomDeliveryCargoAuthority.ScalarStock
						&& row.CargoAuthority != KingdomDeliveryCargoAuthority.CarryBookManifest)
					|| (row.CargoAuthority == KingdomDeliveryCargoAuthority.ScalarStock
						&& (row.Cargo != KingdomStockKind.Water && row.Cargo != KingdomStockKind.Food
							|| !string.IsNullOrEmpty(row.OwnerOperationId)))
					|| (row.CargoAuthority == KingdomDeliveryCargoAuthority.CarryBookManifest
						&& (row.Cargo != KingdomStockKind.OpaqueManifest
							|| string.IsNullOrEmpty(row.OwnerOperationId)))
					|| row.Load <= 0 || row.Load > capacity
					|| row.SourceToDestinationCells < 0
					|| row.SourceToDestinationCells >= NoRoute
					|| row.ZoneRoute == null || row.ZoneRouteCount < 2
					|| row.ZoneRouteCount > row.ZoneRoute.Length
					|| row.ZoneRouteCount > KingdomDistanceRules.MaxNodes
					|| row.ZoneRoute[0] != row.SourceZoneIndex
					|| row.ZoneRoute[row.ZoneRouteCount - 1] != row.DestinationZoneIndex)
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				for (int j = 0; j < considered; j++)
				{
					int cells = betweenDestinations[i * count + j];
					if (cells < 0 || cells >= NoRoute)
					{
						fault = KingdomCityFault.OutsideItinerary;
						return false;
					}
				}
			}

			long[] tripLoads = new long[considered];
			int[] tripStops = new int[considered];
			int[] tripSeeds = new int[considered];
			int tripCount = 0;
			int operations = 0;
			for (int i = 0; i < considered; i++)
			{
				int found = -1;
				for (int t = 0; t < tripCount; t++)
				{
					operations++;
					if (tripStops[t] < MaxStopsPerTrip
						&& tripLoads[t] + requests[i].Load <= capacity
						&& SharesRoutePrefix(requests[tripSeeds[t]], requests[i]))
					{
						found = t;
						break;
					}
				}
				if (found < 0)
				{
					found = tripCount++;
					tripSeeds[found] = i;
				}
				tripIndexes[i] = found;
				tripLoads[found] += requests[i].Load;
				tripStops[found]++;
			}

			for (int t = 0; t < tripCount; t++)
			{
				int stopCount = tripStops[t];
				int[] members = new int[stopCount];
				int memberCount = 0;
				for (int i = 0; i < considered; i++)
					if (tripIndexes[i] == t) members[memberCount++] = i;
				int nodes = stopCount + 1;
				int[] local = new int[nodes * nodes];
				for (int i = 0; i < stopCount; i++)
				{
					int request = members[i];
					local[i + 1] = requests[request].SourceToDestinationCells;
					local[(i + 1) * nodes] = requests[request].SourceToDestinationCells;
					for (int j = 0; j < stopCount; j++)
						local[(i + 1) * nodes + j + 1]
							= betweenDestinations[request * count + members[j]];
				}
				KingdomTripPlan route;
				if (!TryPlanTrip(local, stopCount, out route, out fault)) return false;
				operations += route.Operations;
				int leader = requests[members[0]].JobId;
				for (int stop = 0; stop < route.StopCount; stop++)
				{
					int request = members[route.Order[stop]];
					leaders[request] = leader;
					ordinals[request] = stop + 1;
				}
			}
			bool held;
			int offender;
			if (!TryNoTwoHalfEmptyTrips(requests, considered, tripIndexes, tripLoads,
				tripStops, tripSeeds, tripCount, capacity, out held, out offender, out fault)
				|| !held)
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			plan = new KingdomLogisticsSnapshotPlan(considered, tripCount, operations,
				tripIndexes, leaders, ordinals, tripLoads, tripStops);
			fault = KingdomCityFault.None;
			return true;
		}

	}
}
