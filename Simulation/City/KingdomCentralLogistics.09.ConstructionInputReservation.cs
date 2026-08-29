using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		/// <summary>Freezes one globally-ordinalled construction cargo range and its complete
		/// route before the parent receipt adopts it. The row is deliberately manifest-neutral:
		/// no body exists and no physical object moves in ReservationPrepared.</summary>
		internal static bool TryPrepareConstructionInputReservation(KingdomSystem system,
			KingdomConstructionInputZoneObservation sourceObservation,
			string ownerOperationId, string sourceObjectId,
			string sourceZoneId, int sourceX, int sourceY, string targetObjectId,
			string targetZoneId, int targetX, int targetY, int sourceStart,
			int sourceCount, long now, out KingdomManifestReservation reservation,
			out KingdomCityFault fault)
		{
			reservation = default(KingdomManifestReservation);
			fault = KingdomCityFault.NullArgument;
			long sourceEnd = (long)sourceStart + sourceCount;
			if (system == null || system.Jobs == null || string.IsNullOrEmpty(ownerOperationId)
				|| string.IsNullOrEmpty(sourceZoneId) || string.IsNullOrEmpty(targetZoneId)
				|| sourceObservation == null || sourceObservation.ZoneId != sourceZoneId || now < 0L
				|| sourceX < 0 || sourceX >= sourceObservation.Width || sourceY < 0
				|| sourceY >= sourceObservation.Height || sourceX >= KingdomJobRules.ZoneWidth
				|| sourceY >= KingdomJobRules.ZoneHeight || targetX < 0
				|| targetX >= KingdomJobRules.ZoneWidth || targetY < 0
				|| targetY >= KingdomJobRules.ZoneHeight || sourceStart < 0 || sourceCount <= 0
				|| sourceCount > KingdomLogisticsRules.CarrierCapacity || sourceEnd > int.MaxValue)
				return false;

			KingdomJobTable table;
			if (!system.Jobs.TryRead(out table, out fault)) return false;
			List<KingdomJobRow> rows = ConstructionInputRows(table, ownerOperationId);
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomJobRow held = rows[i];
				if (!ConstructionInputRangesOverlap(held.DeliveryManifestSourceStart,
					held.DeliveryManifestSourceCount, sourceStart, sourceCount)) continue;
				KingdomLeg last;
				if (!SameConstructionInputReservation(held, sourceObjectId, sourceZoneId,
					sourceX, sourceY, targetObjectId, targetZoneId, targetX, targetY,
					sourceStart, sourceCount, now) || !held.TryLeg(held.LegCount - 1, out last))
				{
					fault = KingdomCityFault.DuplicateBinding;
					return false;
				}
				reservation = new KingdomManifestReservation(new[] { held.JobId },
					new[] { held.DeliveryTripId }, last.ArriveTick);
				fault = KingdomCityFault.None;
				return true;
			}
			if (table.Count >= KingdomJobRules.MaxOpenJobs)
			{
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}

			KingdomLeg[] route;
			int legCount;
			long arrival;
			int sourceEndpointId;
			int targetEndpointId;
			if (!TryBuildObservedManifestRoute(system, sourceObservation, ownerOperationId,
				sourceObjectId ?? "", sourceZoneId, sourceX, sourceY, targetObjectId ?? "",
				targetZoneId, targetX, targetY, now, out route, out legCount, out arrival,
				out sourceEndpointId, out targetEndpointId, out fault)) return false;

			if (!KingdomExperienceRuntime.TryAdmitNewFoundationTransientClaims(system, 1,
				out KingdomExperienceCapacityFault _, out string _))
			{ fault = KingdomCityFault.RowCapExceeded; return false; }
			int jobId = system.Jobs.MintJobId();
			KingdomJobRow row = new KingdomJobRow(jobId, KingdomJobKind.Delivery,
				KingdomStockKind.OpaqueManifest, sourceCount, sourceZoneId, targetZoneId, now,
				KingdomItineraryRules.WalkTicksPerCellDefault, KingdomJobStatus.Open, 0,
				legCount - 1, route, legCount, deliverySourceEndpointId: sourceEndpointId,
				deliverySourceObjectId: sourceObjectId ?? "", deliverySourceX: sourceX,
				deliverySourceY: sourceY, deliveryTargetEndpointId: targetEndpointId,
				deliveryTargetObjectId: targetObjectId ?? "", deliveryTargetX: targetX,
				deliveryTargetY: targetY, deliveryTripId: jobId, deliveryStopOrdinal: 1,
				deliveryPhase: KingdomDeliveryPhase.ReservationPrepared,
				deliveryCargoAuthority: KingdomDeliveryCargoAuthority.ConstructionInput,
				deliveryOwnerOperationId: ownerOperationId,
				deliveryManifestSourceStart: sourceStart,
				deliveryManifestSourceCount: sourceCount);
			KingdomJobTable next;
			if (!table.TryOpen(row, out next, out fault)
				|| !system.Jobs.TryPublish(next, out fault)) return false;
			reservation = new KingdomManifestReservation(new[] { jobId },
				new[] { jobId }, arrival);
			return true;
		}

		private static List<KingdomJobRow> ConstructionInputRows(KingdomJobTable table,
			string ownerOperationId)
		{
			List<KingdomJobRow> rows = new List<KingdomJobRow>();
			for (int i = 0; table != null && i < table.Count; i++)
			{
				KingdomJobRow row;
				if (table.TryAt(i, out row) && row.DeliveryCargoAuthority
						== KingdomDeliveryCargoAuthority.ConstructionInput
					&& string.Equals(row.DeliveryOwnerOperationId, ownerOperationId,
						StringComparison.Ordinal)) rows.Add(row);
			}
			rows.Sort(delegate(KingdomJobRow a, KingdomJobRow b)
			{
				return a.JobId.CompareTo(b.JobId);
			});
			return rows;
		}

		private static bool SameConstructionInputReservation(KingdomJobRow row,
			string sourceObjectId, string sourceZoneId, int sourceX, int sourceY,
			string targetObjectId, string targetZoneId, int targetX, int targetY,
			int sourceStart, int sourceCount, long now)
		{
			return row.DeliveryCargoAuthority == KingdomDeliveryCargoAuthority.ConstructionInput
				&& row.Cargo == KingdomStockKind.OpaqueManifest
				&& row.DeliveryPhase == KingdomDeliveryPhase.ReservationPrepared
				&& row.JobId == row.DeliveryTripId && row.DeliveryStopOrdinal == 1
				&& row.CargoAmount == sourceCount
				&& row.SourceZoneId == sourceZoneId && row.DestZoneId == targetZoneId
				&& row.DeliverySourceX == sourceX && row.DeliverySourceY == sourceY
				&& row.DeliveryTargetX == targetX && row.DeliveryTargetY == targetY
				&& string.Equals(row.DeliverySourceObjectId, sourceObjectId ?? "",
					StringComparison.Ordinal)
				&& string.Equals(row.DeliveryTargetObjectId, targetObjectId ?? "",
					StringComparison.Ordinal)
				&& row.DeliveryOwnerManifestVersion == 0
				&& string.IsNullOrEmpty(row.DeliveryOwnerManifestDigest)
				&& row.DeliveryOwnerManifestRevision == 0L
				&& row.DeliveryManifestSourceStart == sourceStart
				&& row.DeliveryManifestSourceCount == sourceCount;
		}

		private static bool ConstructionInputRangesOverlap(int leftStart, int leftCount,
			int rightStart, int rightCount)
		{
			long leftEnd = (long)leftStart + leftCount;
			long rightEnd = (long)rightStart + rightCount;
			return leftStart < rightEnd && rightStart < leftEnd;
		}
	}
}
