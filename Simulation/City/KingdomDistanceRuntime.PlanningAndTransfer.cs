using System;
using System.Collections.Generic;

using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomDistanceRuntime
	{

		/// <summary>Plans one exact holder→target carry. Any source row with physical stock but no
		/// rendered holder observation makes the whole question refuse; row order is never used as
		/// a fake distance.</summary>
		internal static bool TryPlan(KingdomCityBook book, KingdomCityState state,
			string destinationZoneId, KingdomStockKind kind, long demand, long room,
			out KingdomDistanceTransferPlan plan, out KingdomCityFault fault)
		{
			plan = default(KingdomDistanceTransferPlan);
			fault = KingdomCityFault.NullArgument;
			KingdomDistanceCache cache = (book == null) ? null : book.DistanceCache;
			if (cache == null || cache.Matrix == null || state == null) return false;
			int destination;
			KingdomDistanceZoneCache destinationCache;
			if (!cache.Matrix.Graph.TryIndexOf(destinationZoneId, out destination)
				|| !cache.TryZone(destination, out destinationCache) || !destinationCache.Observed)
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			if (demand <= 0L || room <= 0L)
			{
				fault = KingdomCityFault.None;
				return true;
			}
			KingdomHolderRow[] holders = new KingdomHolderRow[state.ZoneCount];
			int[] distances = new int[state.ZoneCount];
			int[] targets = new int[state.ZoneCount];
			long[] available = new long[state.ZoneCount];
			int count = 0;
			for (int source = 0; source < state.ZoneCount; source++)
			{
				if (source == destination) continue;
				KingdomZoneRow zoneRow;
				KingdomStockPair stock;
				if (!state.TryZone(source, out zoneRow) || !zoneRow.Stocks.TryGet(kind, out stock))
				{
					fault = KingdomCityFault.InvalidIndex;
					return false;
				}
				long physical = stock.Level - Math.Max(zoneRow.OwedOf(kind), 0);
				if (physical <= 0L) continue;
				KingdomDistanceZoneCache sourceCache;
				KingdomZoneStep leaving;
				KingdomZoneStep arriving;
				if (!cache.TryZone(source, out sourceCache) || !sourceCache.Observed
					|| !cache.Matrix.Graph.TryRouteSteps(source, destination, out leaving, out arriving))
				{
					fault = KingdomCityFault.OutsideItinerary;
					return false;
				}
				int holder = Winner(sourceCache, kind, leaving, holder: true);
				int target = Winner(destinationCache, kind, arriving, holder: false);
				if (holder < 0 || target < 0)
				{
					fault = KingdomCityFault.OutsideItinerary;
					return false;
				}
				KingdomDistanceEndpointState sourceEndpoint = sourceCache.Endpoints[holder];
				KingdomDistanceEndpointState targetEndpoint = destinationCache.Endpoints[target];
				int cells;
				if (!cache.TryCompose(source, sourceEndpoint.EndpointId, destination,
					targetEndpoint.EndpointId, out cells, out fault)) return false;
				long amount = sourceEndpoint.Amount(kind);
				if (amount > physical) amount = physical;
				long targetRoom = targetEndpoint.Room(kind);
				if (amount > targetRoom) amount = targetRoom;
				if (amount <= 0L)
				{
					fault = KingdomCityFault.OutsideItinerary;
					return false;
				}
				holders[count] = new KingdomHolderRow(sourceEndpoint.EndpointId, source, -1,
					sourceEndpoint.DedicationOrdinal, kind, amount);
				distances[count] = cells;
				targets[count] = targetEndpoint.EndpointId;
				available[count] = amount;
				count++;
			}
			int chosen;
			if (!KingdomLogisticsRules.TryNearestHolder(holders, count, distances, kind,
				out chosen, out fault))
			{
				if (fault == KingdomCityFault.None) return true;
				return false;
			}
			bool held;
			int offender;
			if (!KingdomLogisticsRules.TryNoNearerHolder(holders, count, distances, kind,
				holders[chosen].HolderId, out held, out offender, out fault) || !held)
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			long amountChosen = available[chosen];
			if (amountChosen > demand) amountChosen = demand;
			if (amountChosen > room) amountChosen = room;
			KingdomDistanceEndpointState chosenSource;
			KingdomDistanceEndpointState chosenTarget;
			if (!cache.TryEndpoint(holders[chosen].ZoneIndex, holders[chosen].HolderId,
				out chosenSource) || !cache.TryEndpoint(destination, targets[chosen], out chosenTarget))
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			plan = new KingdomDistanceTransferPlan(holders[chosen].ZoneIndex,
				holders[chosen].HolderId, chosenSource.ObjectId,
				targets[chosen], chosenTarget.ObjectId,
				chosenSource.X, chosenSource.Y, chosenTarget.X, chosenTarget.Y,
				distances[chosen], amountChosen);
			fault = KingdomCityFault.None;
			return true;
		}

		internal static int Land(KingdomSurvey survey, KingdomDistanceTransferPlan plan,
			KingdomStockKind kind, string crop)
		{
			if (survey == null || plan.Amount <= 0L) return 0;
			int offer = (plan.Amount > int.MaxValue) ? int.MaxValue : (int)plan.Amount;
			if (kind == KingdomStockKind.Water)
			{
				for (int i = 0; i < survey.Stores.Count; i++)
				{
					LiquidVolume store = survey.Stores[i];
					GameObject owner = (store == null) ? null : store.ParentObject;
					if (GameObject.Validate(owner) && KingdomCityRules.StableId(owner.ID) == plan.TargetId)
						return survey.StoreIn(store, offer);
				}
			}
			else if (kind == KingdomStockKind.Food)
			{
				for (int i = 0; i < survey.Larders.Count; i++)
				{
					GameObject larder = survey.Larders[i];
					if (GameObject.Validate(larder) && KingdomCityRules.StableId(larder.ID) == plan.TargetId)
						return survey.StoreFoodIn(larder, offer, crop);
				}
			}
			return 0;
		}

		internal static bool Commit(KingdomCityBook book, string destinationZoneId,
			KingdomDistanceTransferPlan plan, KingdomStockKind kind, long amount)
		{
			KingdomDistanceCache cache = (book == null) ? null : book.DistanceCache;
			int destination;
			return cache != null && cache.Matrix != null && amount >= 0L
				&& cache.Matrix.Graph.TryIndexOf(destinationZoneId, out destination)
				&& cache.TrySpend(plan.SourceZoneIndex, plan.HolderId, kind, amount)
				&& cache.TryFill(destination, plan.TargetId, kind, amount);
		}

		/// <summary>Rehydrates one persisted Planned row into the exact immutable request consumed
		/// by the central batch planner. Every endpoint and every zone on the route must have a
		/// trusted post-load observation; cold load waits rather than substituting a row proxy.</summary>
		internal static bool TryFreezeRequest(KingdomCityBook book, KingdomJobRow row,
			out KingdomLogisticsRequest request, out KingdomCityFault fault)
		{
			request = default(KingdomLogisticsRequest);
			KingdomDistanceCache cache = book == null ? null : book.DistanceCache;
			if (cache == null || cache.Matrix == null)
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			int source;
			int target;
			KingdomDistanceEndpointState sourceEndpoint;
			KingdomDistanceEndpointState targetEndpoint;
			if (!cache.Matrix.Graph.TryIndexOf(row.SourceZoneId, out source)
				|| !cache.Matrix.Graph.TryIndexOf(row.DestZoneId, out target)
				|| !cache.TryEndpoint(source, row.DeliverySourceEndpointId, out sourceEndpoint)
				|| !cache.TryEndpoint(target, row.DeliveryTargetEndpointId, out targetEndpoint)
				|| (!string.IsNullOrEmpty(row.DeliverySourceObjectId)
					&& !string.Equals(sourceEndpoint.ObjectId, row.DeliverySourceObjectId,
						StringComparison.Ordinal))
				|| (!string.IsNullOrEmpty(row.DeliveryTargetObjectId)
					&& !string.Equals(targetEndpoint.ObjectId, row.DeliveryTargetObjectId,
						StringComparison.Ordinal))
				|| sourceEndpoint.X != row.DeliverySourceX
				|| sourceEndpoint.Y != row.DeliverySourceY
				|| targetEndpoint.X != row.DeliveryTargetX
				|| targetEndpoint.Y != row.DeliveryTargetY)
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			int[] route = new int[KingdomDistanceRules.MaxNodes];
			int routeCount;
			if (!cache.Matrix.Graph.TryPath(source, target, route, out routeCount, out fault)
				|| routeCount < 2) return false;
			for (int i = 0; i < routeCount; i++)
			{
				KingdomDistanceZoneCache observed;
				if (!cache.TryZone(route[i], out observed) || !observed.Observed)
				{
					fault = KingdomCityFault.OutsideItinerary;
					return false;
				}
			}
			int cells;
			if (!cache.TryCompose(source, row.DeliverySourceEndpointId, target,
				row.DeliveryTargetEndpointId, out cells, out fault)) return false;
			int load = row.DeliveryCargoAuthority == KingdomDeliveryCargoAuthority.CarryBookManifest
				? row.DeliveryManifestSourceCount : row.CargoAmount;
			request = new KingdomLogisticsRequest(row.JobId, row.DeliverySourceEndpointId,
				source, row.DeliveryTargetEndpointId, target, row.Cargo, load, cells,
				route, routeCount, row.DeliveryCargoAuthority, row.DeliveryOwnerOperationId);
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>Exact target-to-target metric for one frozen planner snapshot.</summary>
		internal static bool TryTargetMetric(KingdomCityBook book,
			KingdomLogisticsRequest[] requests, int count, out int[] between,
			out KingdomCityFault fault)
		{
			between = null;
			KingdomDistanceCache cache = book == null ? null : book.DistanceCache;
			if (cache == null || cache.Matrix == null || requests == null
				|| count < 0 || count > requests.Length)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			between = new int[count * count];
			for (int i = 0; i < count; i++)
			for (int j = 0; j < count; j++)
			{
				if (i == j)
				{
					between[i * count + j] = 0;
					continue;
				}
				int cells;
				if (!cache.TryCompose(requests[i].DestinationZoneIndex,
					requests[i].DestinationEndpointId, requests[j].DestinationZoneIndex,
					requests[j].DestinationEndpointId, out cells, out fault)) return false;
				between[i * count + j] = cells;
			}
			fault = KingdomCityFault.None;
			return true;
		}
	}
}
