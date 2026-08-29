using System;

using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		/// <summary>Resolves the one authority-2 body after its frozen arrival. The owner remains
		/// responsible for every object in the opaque inventory and for all economic accounting.</summary>
		internal static bool TryResolveConstructionInputTargetCarrier(KingdomSystem system,
			string ownerOperationId, int jobId, int tripId, int manifestVersion,
			string manifestDigest, long manifestRevision, Zone destination,
			out GameObject carrier, out KingdomCityFault fault)
		{
			carrier = null;
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable jobs;
			KingdomJobRow row;
			if (system == null || system.Jobs == null || destination == null
				|| string.IsNullOrEmpty(ownerOperationId) || jobId <= 0 || tripId <= 0
				|| !system.Jobs.TryRead(out jobs, out fault) || !jobs.TryGet(jobId, out row)
				|| row.JobId != jobId || row.DeliveryTripId != tripId
				|| row.DeliveryCargoAuthority != KingdomDeliveryCargoAuthority.ConstructionInput
				|| row.Cargo != KingdomStockKind.OpaqueManifest
				|| TripRows(jobs, tripId).Count != 1
				|| row.DeliveryOwnerManifestVersion != manifestVersion
				|| row.DeliveryOwnerManifestDigest != manifestDigest
				|| row.DeliveryOwnerManifestRevision > manifestRevision
				|| (row.DeliveryPhase != KingdomDeliveryPhase.InFlight
					&& row.DeliveryPhase != KingdomDeliveryPhase.LandedAwaitingOwner)
				|| !string.Equals(row.DeliveryOwnerOperationId, ownerOperationId,
					StringComparison.Ordinal)) return false;
			return TryConstructionInputCarrierAtTarget(system, row, destination,
				out carrier, out fault);
		}
	}
}
