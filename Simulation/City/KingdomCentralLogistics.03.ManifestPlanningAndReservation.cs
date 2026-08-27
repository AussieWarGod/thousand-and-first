using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		// Pull-based CarryBook seam -------------------------------------------------------

		/// <summary>Read-only destination proof for a carry-sign. The frozen spill cell is
		/// one endpoint already measured on the destination ground; an absent distance slice
		/// refuses work instead of inventing a heart coordinate.</summary>
		internal static bool TryManifestSpillAnchor(KingdomSystem system, string targetZoneId,
			out int targetX, out int targetY, out KingdomCityFault fault)
		{
			targetX = targetY = -1;
			KingdomDistanceCache cache = system == null || system.City == null
				? null : system.City.DistanceCache;
			int zoneIndex;
			KingdomDistanceZoneCache zone;
			if (cache == null || cache.Matrix == null || string.IsNullOrEmpty(targetZoneId)
				|| !cache.Matrix.Graph.TryIndexOf(targetZoneId, out zoneIndex)
				|| !cache.TryZone(zoneIndex, out zone) || !zone.Observed
				|| zone.Endpoints == null || zone.Endpoints.Length == 0)
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			int chosen = -1;
			for (int i = 0; i < zone.Endpoints.Length; i++)
			{
				KingdomDistanceEndpointState row = zone.Endpoints[i];
				if (row.X < 0 || row.Y < 0 || row.X >= KingdomJobRules.ZoneWidth
					|| row.Y >= KingdomJobRules.ZoneHeight) continue;
				if (chosen < 0 || row.EndpointId < zone.Endpoints[chosen].EndpointId)
					chosen = i;
			}
			if (chosen < 0)
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			targetX = zone.Endpoints[chosen].X;
			targetY = zone.Endpoints[chosen].Y;
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>Read-only route preview used before founder consent. It shares the exact
		/// planner with reservation, but publishes no job, binding, body, or cargo mutation.</summary>
		internal static bool TryPreviewManifestRoute(KingdomSystem system, Zone liveSourceZone,
			string ownerOperationId, string sourceObjectId, string sourceZoneId, int sourceX,
			int sourceY, string targetObjectId, string targetZoneId, int targetX, int targetY,
			long now, out long arrivalTick, out KingdomCityFault fault)
		{
			arrivalTick = now;
			KingdomLeg[] route;
			int legCount;
			int sourceEndpointId;
			int targetEndpointId;
			return TryBuildManifestRoute(system, liveSourceZone, ownerOperationId,
				sourceObjectId ?? "", sourceZoneId, sourceX, sourceY, targetObjectId ?? "",
				targetZoneId, targetX, targetY, now, out route, out legCount, out arrivalTick,
				out sourceEndpointId, out targetEndpointId, out fault);
		}

		/// <summary>Phase one of exact CarryBook authority. Freezes ids, complete itinerary and
		/// arrival before CarryBook publishes, but creates no body and moves no cargo.</summary>
		internal static bool TryPrepareManifestReservation(KingdomSystem system, Zone liveSourceZone,
			string ownerOperationId, string sourceObjectId, string sourceZoneId, int sourceX,
			int sourceY, string targetObjectId, string targetZoneId, int targetX, int targetY,
			int sourceObjectCount, long now, out KingdomManifestReservation reservation,
			out KingdomCityFault fault)
		{
			reservation = default(KingdomManifestReservation);
			fault = KingdomCityFault.NullArgument;
			if (system == null || system.Jobs == null || string.IsNullOrEmpty(ownerOperationId)
				|| string.IsNullOrEmpty(sourceZoneId) || string.IsNullOrEmpty(targetZoneId)
				|| liveSourceZone == null || liveSourceZone.ZoneID != sourceZoneId
				|| sourceX < 0 || sourceX >= liveSourceZone.Width || sourceY < 0
				|| sourceY >= liveSourceZone.Height || targetX < 0
				|| targetX >= KingdomJobRules.ZoneWidth || targetY < 0
				|| targetY >= KingdomJobRules.ZoneHeight || sourceObjectCount <= 0) return false;
			int expected = (sourceObjectCount + KingdomLogisticsRules.CarrierCapacity - 1)
				/ KingdomLogisticsRules.CarrierCapacity;
			KingdomJobTable table;
			if (!system.Jobs.TryRead(out table, out fault)) return false;
			List<KingdomJobRow> existing = OwnerRows(table, ownerOperationId);
			if (existing.Count > 0)
			{
				if (existing.Count != expected) { fault = KingdomCityFault.DuplicateBinding; return false; }
				int[] heldJobs = new int[existing.Count];
				int[] heldTrips = new int[existing.Count];
				long heldArrival = 0L;
				int heldCount = 0;
				for (int i = 0; i < existing.Count; i++)
				{
					KingdomJobRow row = existing[i];
					KingdomLeg last;
					if (row.DeliveryPhase != KingdomDeliveryPhase.ReservationPrepared
						|| row.SourceZoneId != sourceZoneId || row.DestZoneId != targetZoneId
						|| row.DeliverySourceX != sourceX || row.DeliverySourceY != sourceY
						|| row.DeliveryTargetX != targetX || row.DeliveryTargetY != targetY
						|| !string.Equals(row.DeliverySourceObjectId, sourceObjectId ?? "",
							StringComparison.Ordinal)
						|| !string.Equals(row.DeliveryTargetObjectId, targetObjectId ?? "",
							StringComparison.Ordinal) || !row.TryLeg(row.LegCount - 1, out last))
					{ fault = KingdomCityFault.DuplicateBinding; return false; }
					heldJobs[i] = row.JobId; heldTrips[i] = row.DeliveryTripId;
					heldCount += row.DeliveryManifestSourceCount;
					if (last.ArriveTick > heldArrival) heldArrival = last.ArriveTick;
				}
				if (heldCount != sourceObjectCount)
				{ fault = KingdomCityFault.DuplicateBinding; return false; }
				reservation = new KingdomManifestReservation(heldJobs, heldTrips, heldArrival);
				fault = KingdomCityFault.None;
				return true;
			}
			if (expected <= 0 || expected > KingdomJobRules.MaxOpenJobs
				|| table.Count + expected > KingdomJobRules.MaxOpenJobs)
			{ fault = KingdomCityFault.RowCapExceeded; return false; }
			KingdomLeg[] route;
			int legCount;
			long arrival;
			int sourceEndpointId;
			int targetEndpointId;
			if (!TryBuildManifestRoute(system, liveSourceZone, ownerOperationId,
				sourceObjectId ?? "", sourceZoneId, sourceX, sourceY, targetObjectId ?? "",
				targetZoneId, targetX, targetY, now, out route, out legCount, out arrival,
				out sourceEndpointId, out targetEndpointId, out fault)) return false;
			int[] jobIds = new int[expected];
			int[] tripIds = new int[expected];
			int start = 0;
			for (int i = 0; i < expected; i++)
			{
				jobIds[i] = system.Jobs.MintJobId(); tripIds[i] = jobIds[i];
				int count = sourceObjectCount - start;
				if (count > KingdomLogisticsRules.CarrierCapacity)
					count = KingdomLogisticsRules.CarrierCapacity;
				KingdomJobRow row = new KingdomJobRow(jobIds[i], KingdomJobKind.Delivery,
					KingdomStockKind.OpaqueManifest, count, sourceZoneId, targetZoneId, now,
					KingdomItineraryRules.WalkTicksPerCellDefault, KingdomJobStatus.Open, 0,
					legCount - 1, route, legCount, deliverySourceEndpointId: sourceEndpointId,
					deliverySourceObjectId: sourceObjectId,
					deliverySourceX: sourceX, deliverySourceY: sourceY,
					deliveryTargetEndpointId: targetEndpointId,
					deliveryTargetObjectId: targetObjectId,
					deliveryTargetX: targetX, deliveryTargetY: targetY,
					deliveryTripId: tripIds[i], deliveryStopOrdinal: 1,
					deliveryPhase: KingdomDeliveryPhase.ReservationPrepared,
					deliveryCargoAuthority: KingdomDeliveryCargoAuthority.CarryBookManifest,
					deliveryOwnerOperationId: ownerOperationId,
					deliveryManifestSourceStart: start,
					deliveryManifestSourceCount: count);
				KingdomJobTable next;
				if (!table.TryOpen(row, out next, out fault)) return false;
				table = next;
				start += count;
			}
			if (start != sourceObjectCount || !system.Jobs.TryPublish(table, out fault)) return false;
			reservation = new KingdomManifestReservation(jobIds, tripIds, arrival);
			return true;
		}
	}
}
