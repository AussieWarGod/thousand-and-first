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

	/// <summary>One exact, frozen delivery demand handed to the central planner. Endpoint ids are
	/// stable hashes of engine object ids; the runtime also persists the unhashed object ids and
	/// refuses a hash collision before a request reaches this rules layer. <see cref="ZoneRoute"/>
	/// is the complete level-1 route, source and destination included.</summary>
	internal readonly struct KingdomLogisticsRequest
	{
		internal readonly int JobId;
		internal readonly int SourceEndpointId;
		internal readonly int SourceZoneIndex;
		internal readonly int DestinationEndpointId;
		internal readonly int DestinationZoneIndex;
		internal readonly KingdomStockKind Cargo;
		internal readonly int Load;
		internal readonly int SourceToDestinationCells;
		internal readonly int[] ZoneRoute;
		internal readonly int ZoneRouteCount;

		internal readonly KingdomDeliveryCargoAuthority CargoAuthority;

		internal readonly string OwnerOperationId;

		internal KingdomLogisticsRequest(int jobId, int sourceEndpointId, int sourceZoneIndex,
			int destinationEndpointId, int destinationZoneIndex, KingdomStockKind cargo,
			int load, int sourceToDestinationCells, int[] zoneRoute, int zoneRouteCount)
		{
			JobId = jobId;
			SourceEndpointId = sourceEndpointId;
			SourceZoneIndex = sourceZoneIndex;
			DestinationEndpointId = destinationEndpointId;
			DestinationZoneIndex = destinationZoneIndex;
			Cargo = cargo;
			Load = load;
			SourceToDestinationCells = sourceToDestinationCells;
			ZoneRoute = zoneRoute;
			ZoneRouteCount = zoneRouteCount;
			CargoAuthority = KingdomDeliveryCargoAuthority.ScalarStock;
			OwnerOperationId = null;
		}

		internal KingdomLogisticsRequest(int jobId, int sourceEndpointId, int sourceZoneIndex,
			int destinationEndpointId, int destinationZoneIndex, KingdomStockKind cargo,
			int load, int sourceToDestinationCells, int[] zoneRoute, int zoneRouteCount,
			KingdomDeliveryCargoAuthority cargoAuthority, string ownerOperationId)
			: this(jobId, sourceEndpointId, sourceZoneIndex, destinationEndpointId,
				destinationZoneIndex, cargo, load, sourceToDestinationCells, zoneRoute,
				zoneRouteCount)
		{
			CargoAuthority = cargoAuthority;
			OwnerOperationId = ownerOperationId;
		}
	}

	/// <summary>Complete bounded answer for one frozen logistics slice. Request-indexed arrays
	/// preserve which exact source, stop, route and cargo the caller supplied; only trip grouping
	/// and ordered stop numbers are decided here.</summary>
	internal sealed class KingdomLogisticsSnapshotPlan
	{
		internal readonly int ConsideredCount;
		internal readonly int TripCount;
		internal readonly int Operations;
		internal readonly int[] TripIndexes;
		internal readonly int[] TripLeaderJobIds;
		internal readonly int[] StopOrdinals;
		internal readonly long[] TripLoads;
		internal readonly int[] TripStops;

		internal KingdomLogisticsSnapshotPlan(int consideredCount, int tripCount, int operations,
			int[] tripIndexes, int[] tripLeaderJobIds, int[] stopOrdinals,
			long[] tripLoads, int[] tripStops)
		{
			ConsideredCount = consideredCount;
			TripCount = tripCount;
			Operations = operations;
			TripIndexes = tripIndexes;
			TripLeaderJobIds = tripLeaderJobIds;
			StopOrdinals = stopOrdinals;
			TripLoads = tripLoads;
			TripStops = tripStops;
		}
	}

}
