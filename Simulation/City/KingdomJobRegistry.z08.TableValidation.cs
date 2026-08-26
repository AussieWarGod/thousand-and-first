using System;
using System.Collections.Generic;
#if TAF_TESTS
using System.IO;
using System.Text;
#endif

using ThousandAndFirst.Simulation.Kernel;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst.Simulation.City
{
	internal sealed partial class KingdomJobTable
	{
		private static bool ValidDeliveryEnvelope(KingdomJobRow row)
		{
			bool neutral = row.DeliverySourceEndpointId == 0
				&& string.IsNullOrEmpty(row.DeliverySourceObjectId)
				&& row.DeliverySourceX == -1 && row.DeliverySourceY == -1
				&& row.DeliveryTargetEndpointId == 0
				&& string.IsNullOrEmpty(row.DeliveryTargetObjectId)
				&& row.DeliveryTargetX == -1 && row.DeliveryTargetY == -1
				&& row.DeliverySourceBeforeAmount == 0L && row.DeliveryTripId == 0
				&& row.DeliveryStopOrdinal == 0
				&& row.DeliveryPhase == KingdomDeliveryPhase.Legacy
				&& row.DeliveryCargoAuthority == KingdomDeliveryCargoAuthority.ScalarStock
				&& string.IsNullOrEmpty(row.DeliveryOwnerOperationId)
				&& row.DeliveryOwnerManifestVersion == 0
				&& string.IsNullOrEmpty(row.DeliveryOwnerManifestDigest)
				&& row.DeliveryOwnerManifestRevision == 0L
				&& row.DeliveryManifestSourceStart == 0
				&& row.DeliveryManifestSourceCount == 0
				&& row.DeliveryTargetBeforeAmount == 0L
				&& row.DeliveryTargetReceiptState == KingdomDeliveryTargetReceiptState.None;
			if (row.Kind != KingdomJobKind.Delivery) return neutral;
			if (row.DeliveryPhase == KingdomDeliveryPhase.Legacy) return neutral;
			if (!KingdomJobRules.IsDeliveryPhase((int)row.DeliveryPhase)
				|| row.CargoAmount < 0 || string.IsNullOrEmpty(row.SourceZoneId)
				|| string.IsNullOrEmpty(row.DestZoneId)
				|| row.DeliverySourceEndpointId <= 0
				|| row.DeliveryTargetEndpointId <= 0
				|| row.DeliverySourceX < 0 || row.DeliverySourceX >= KingdomJobRules.ZoneWidth
				|| row.DeliverySourceY < 0 || row.DeliverySourceY >= KingdomJobRules.ZoneHeight
				|| row.DeliveryTargetX < 0 || row.DeliveryTargetX >= KingdomJobRules.ZoneWidth
				|| row.DeliveryTargetY < 0 || row.DeliveryTargetY >= KingdomJobRules.ZoneHeight)
				return false;
			if (row.DeliveryTargetBeforeAmount < 0L
				|| (row.DeliveryTargetReceiptState != KingdomDeliveryTargetReceiptState.None
					&& row.DeliveryTargetReceiptState
						!= KingdomDeliveryTargetReceiptState.Prepared)) return false;
			bool scalar = row.DeliveryCargoAuthority
				== KingdomDeliveryCargoAuthority.ScalarStock;
			bool manifest = row.DeliveryCargoAuthority
				== KingdomDeliveryCargoAuthority.CarryBookManifest;
			if (!scalar && !manifest) return false;
			if (scalar && ((row.Cargo != KingdomStockKind.Water
					&& row.Cargo != KingdomStockKind.Food)
				|| string.IsNullOrEmpty(row.DeliverySourceObjectId)
				|| string.IsNullOrEmpty(row.DeliveryTargetObjectId)
				|| !string.IsNullOrEmpty(row.DeliveryOwnerOperationId)
				|| row.DeliveryOwnerManifestVersion != 0
				|| !string.IsNullOrEmpty(row.DeliveryOwnerManifestDigest)
				|| row.DeliveryOwnerManifestRevision != 0L
				|| row.DeliveryManifestSourceStart != 0
				|| row.DeliveryManifestSourceCount != 0)) return false;
			bool reservation = manifest && (row.DeliveryPhase
				== KingdomDeliveryPhase.ReservationPrepared
				|| row.DeliveryPhase == KingdomDeliveryPhase.Quarantined);
			if (manifest && (row.Cargo != KingdomStockKind.OpaqueManifest
				|| string.IsNullOrEmpty(row.DeliveryOwnerOperationId)
				|| row.DeliveryManifestSourceStart < 0
				|| row.DeliveryManifestSourceCount <= 0
				|| row.DeliveryManifestSourceCount > KingdomLogisticsRules.CarrierCapacity
				|| row.CargoAmount != row.DeliveryManifestSourceCount
				|| row.DeliverySourceBeforeAmount != 0L
				|| row.DeliveryTargetBeforeAmount != 0L
				|| row.DeliveryTargetReceiptState
					!= KingdomDeliveryTargetReceiptState.None
				|| (reservation && (row.DeliveryOwnerManifestVersion != 0
					|| !string.IsNullOrEmpty(row.DeliveryOwnerManifestDigest)
					|| row.DeliveryOwnerManifestRevision != 0L))
				|| (!reservation && (row.DeliveryOwnerManifestVersion <= 0
					|| string.IsNullOrEmpty(row.DeliveryOwnerManifestDigest)
					|| row.DeliveryOwnerManifestRevision < 0L)))) return false;
			if (row.DeliveryPhase == KingdomDeliveryPhase.Planned)
				return scalar && row.CargoAmount > 0 && row.DeliverySourceBeforeAmount == 0L
					&& row.DeliveryTripId == 0 && row.DeliveryStopOrdinal == 0
					&& row.LegCount == 0 && row.DeliveryTargetBeforeAmount == 0L
					&& row.DeliveryTargetReceiptState == KingdomDeliveryTargetReceiptState.None;
			if (row.DeliveryPhase == KingdomDeliveryPhase.SourceDebitPrepared
				&& row.DeliveryTargetReceiptState != KingdomDeliveryTargetReceiptState.None)
				return false;
			if (scalar && row.CargoAmount == 0
				&& row.DeliveryTargetReceiptState != KingdomDeliveryTargetReceiptState.Prepared)
				return false;
			return (manifest || row.DeliverySourceBeforeAmount > 0L)
				&& row.DeliveryTripId > 0
				&& row.DeliveryStopOrdinal > 0
				&& row.DeliveryStopOrdinal <= KingdomLogisticsRules.MaxStopsPerTrip
				&& row.LegCount > 0 && row.LegCount <= KingdomItineraryRules.MaxLegs;
		}

		private static bool ValidTrips(KingdomJobRow[] source)
		{
			for (int i = 0; i < source.Length; i++)
			{
				KingdomJobRow seed = source[i];
				if (!KingdomJobRules.IsCentralDelivery(seed)
					|| seed.DeliveryPhase == KingdomDeliveryPhase.Planned) continue;
				if (seed.DeliveryTripId != seed.JobId) continue;
				int count = 0;
				long load = 0L;
				long before = seed.DeliverySourceBeforeAmount;
				long priorArrival = -1L;
				string priorDestination = null;
				for (int ordinal = 1; ordinal <= KingdomLogisticsRules.MaxStopsPerTrip; ordinal++)
				{
					int found = -1;
					for (int j = 0; j < source.Length; j++)
						if (source[j].DeliveryTripId == seed.DeliveryTripId
							&& source[j].DeliveryStopOrdinal == ordinal) { found = j; break; }
					if (found < 0) break;
					KingdomJobRow row = source[found];
					KingdomLeg first;
					KingdomLeg last;
					if (row.DeliveryPhase != seed.DeliveryPhase
						|| row.DeliverySourceEndpointId != seed.DeliverySourceEndpointId
						|| !string.Equals(row.DeliverySourceObjectId,
							seed.DeliverySourceObjectId, StringComparison.Ordinal)
						|| row.DeliverySourceX != seed.DeliverySourceX
						|| row.DeliverySourceY != seed.DeliverySourceY
						|| !string.Equals(row.SourceZoneId, seed.SourceZoneId, StringComparison.Ordinal)
						|| row.Cargo != seed.Cargo || row.DeliverySourceBeforeAmount != before
						|| row.DeliveryCargoAuthority != seed.DeliveryCargoAuthority
						|| !string.Equals(row.DeliveryOwnerOperationId,
							seed.DeliveryOwnerOperationId, StringComparison.Ordinal)
						|| row.DeliveryOwnerManifestVersion != seed.DeliveryOwnerManifestVersion
						|| !string.Equals(row.DeliveryOwnerManifestDigest,
							seed.DeliveryOwnerManifestDigest, StringComparison.Ordinal)
						|| row.DeliveryOwnerManifestRevision != seed.DeliveryOwnerManifestRevision
						|| !row.TryLeg(0, out first) || !row.TryLeg(row.LegCount - 1, out last)
						|| !string.Equals(last.ZoneId, row.DestZoneId, StringComparison.Ordinal)
						|| (ordinal == 1 && !string.Equals(first.ZoneId,
							row.SourceZoneId, StringComparison.Ordinal))
						|| (ordinal > 1 && (!string.Equals(first.ZoneId, priorDestination,
							StringComparison.Ordinal) || first.DepartTick < priorArrival))) return false;
					load += row.DeliveryCargoAuthority
						== KingdomDeliveryCargoAuthority.CarryBookManifest
						? row.DeliveryManifestSourceCount : row.CargoAmount;
					if (load > KingdomLogisticsRules.CarrierCapacity) return false;
					priorDestination = row.DestZoneId;
					priorArrival = last.ArriveTick;
					count++;
				}
				if (count <= 0) return false;
				for (int j = 0; j < source.Length; j++)
					if (source[j].DeliveryTripId == seed.DeliveryTripId
						&& (source[j].DeliveryStopOrdinal < 1
							|| source[j].DeliveryStopOrdinal > count)) return false;
			}
			// Every prepared/in-flight row must point at one leader row; otherwise a child could
			// survive alone and mint a second body after reload.
			for (int i = 0; i < source.Length; i++)
			{
				KingdomJobRow row = source[i];
				if (!KingdomJobRules.IsCentralDelivery(row)
					|| row.DeliveryPhase == KingdomDeliveryPhase.Planned) continue;
				bool leader = false;
				for (int j = 0; j < source.Length; j++)
					if (source[j].JobId == row.DeliveryTripId
						&& source[j].DeliveryTripId == row.DeliveryTripId) { leader = true; break; }
				if (!leader) return false;
			}
			// One exact whole-stack source ordinal may belong to only one open trip. Overlap would
			// authorize two carriers to move the same GameObject reference after reload.
			for (int i = 0; i < source.Length; i++)
			{
				KingdomJobRow left = source[i];
				if (left.DeliveryCargoAuthority
					!= KingdomDeliveryCargoAuthority.CarryBookManifest) continue;
				long leftEnd = (long)left.DeliveryManifestSourceStart
					+ left.DeliveryManifestSourceCount;
				if (leftEnd > int.MaxValue) return false;
				for (int j = i + 1; j < source.Length; j++)
				{
					KingdomJobRow right = source[j];
					if (right.DeliveryCargoAuthority
						!= KingdomDeliveryCargoAuthority.CarryBookManifest
						|| !string.Equals(left.DeliveryOwnerOperationId,
							right.DeliveryOwnerOperationId, StringComparison.Ordinal)) continue;
					if (left.DeliveryOwnerManifestVersion != right.DeliveryOwnerManifestVersion
						|| !string.Equals(left.DeliveryOwnerManifestDigest,
							right.DeliveryOwnerManifestDigest, StringComparison.Ordinal)) return false;
					long rightEnd = (long)right.DeliveryManifestSourceStart
						+ right.DeliveryManifestSourceCount;
					if (rightEnd > int.MaxValue
						|| (left.DeliveryManifestSourceStart < rightEnd
							&& right.DeliveryManifestSourceStart < leftEnd)) return false;
				}
			}
			return true;
		}
	}
}
