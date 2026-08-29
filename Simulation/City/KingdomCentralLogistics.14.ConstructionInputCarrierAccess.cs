using System;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		/// <summary>Resolves one already-rendered authority-2 source carrier. This accessor never
		/// loads, mints, moves, or substitutes a body; ambiguity leaves source custody untouched.</summary>
		internal static bool TryResolveConstructionInputSourceCarrier(KingdomSystem system,
			string ownerOperationId, int jobId, int tripId, int manifestVersion,
			string manifestDigest, long manifestRevision, out GameObject carrier,
			out KingdomCityFault fault)
		{
			carrier = null;
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable jobs;
			KingdomJobRow row;
			KingdomBindingTable bindings;
			KingdomBinding binding;
			if (system == null || system.Jobs == null || system.Bindings == null
				|| string.IsNullOrEmpty(ownerOperationId) || jobId <= 0 || tripId <= 0
				|| !system.Jobs.TryRead(out jobs, out fault) || !jobs.TryGet(jobId, out row)
				|| TripRows(jobs, tripId).Count != 1
				|| row.JobId != jobId || row.DeliveryTripId != tripId
				|| row.DeliveryCargoAuthority != KingdomDeliveryCargoAuthority.ConstructionInput
				|| row.Cargo != KingdomStockKind.OpaqueManifest
				|| row.DeliveryPhase != KingdomDeliveryPhase.SourceDebitPrepared
				|| row.DeliveryOwnerManifestVersion != manifestVersion
				|| row.DeliveryOwnerManifestDigest != manifestDigest
				|| row.DeliveryOwnerManifestRevision > manifestRevision
				|| !string.Equals(row.DeliveryOwnerOperationId, ownerOperationId,
					StringComparison.Ordinal) || !system.Bindings.TryRead(out bindings, out fault)
				|| !bindings.TryGet(tripId, KingdomBindingKind.Transient, out binding)
				|| binding.BindingKey != tripId || binding.Kind != KingdomBindingKind.Transient
				|| !string.Equals(binding.ZoneId, row.SourceZoneId, StringComparison.Ordinal)
				|| string.IsNullOrEmpty(binding.ObjectId) || The.ZoneManager == null)
				return false;

			Zone zone = The.ZoneManager.ActiveZone;
			if (zone == null || KingdomSurvey.ActiveFor(zone) == null
				|| !string.Equals(zone.ZoneID, row.SourceZoneId, StringComparison.Ordinal)
				|| !string.Equals(binding.ZoneId, zone.ZoneID, StringComparison.Ordinal))
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			KingdomSurvey survey = KingdomSurvey.ActiveFor(zone);
			GameObject exact = survey.FindBoundBody(binding.ObjectId, KingdomBindingKind.Transient);
			r_KingdomPorter porter = exact == null ? null : exact.GetPart<r_KingdomPorter>();
			if (!GameObject.Validate(exact) || !exact.IsAlive || exact.IsPlayer()
				|| exact.IsPlayerLed() || exact.CurrentZone != zone || exact.CurrentCell == null
				|| exact.Inventory == null || exact.IDIfAssigned != binding.ObjectId
				|| exact.GetIntProperty(KingdomResidents.JobIdProperty) != tripId
				|| porter == null || porter.JobId != tripId
				|| exact.CurrentCell.X != row.DeliverySourceX
				|| exact.CurrentCell.Y != row.DeliverySourceY)
			{
				fault = KingdomCityFault.DuplicateBinding;
				return false;
			}
			carrier = exact;
			fault = KingdomCityFault.None;
			return true;
		}

		internal static bool TryProveConstructionInputSourceRow(KingdomSystem system,
			string ownerOperationId, int jobId, int tripId, int manifestVersion,
			string manifestDigest, long manifestRevision, string sourceZoneId,
			out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable jobs;
			KingdomJobRow row;
			if (system?.Jobs == null || string.IsNullOrEmpty(ownerOperationId)
				|| jobId <= 0 || tripId <= 0 || manifestVersion <= 0
				|| string.IsNullOrEmpty(manifestDigest) || string.IsNullOrEmpty(sourceZoneId)
				|| !system.Jobs.TryRead(out jobs, out fault) || !jobs.TryGet(jobId, out row)
				|| TripRows(jobs, tripId).Count != 1
				|| !ConstructionTransitRow(row, ownerOperationId, jobId, tripId)
				|| row.DeliveryPhase != KingdomDeliveryPhase.SourceDebitPrepared
				|| row.SourceZoneId != sourceZoneId
				|| row.DeliveryOwnerManifestVersion != manifestVersion
				|| row.DeliveryOwnerManifestDigest != manifestDigest
				|| row.DeliveryOwnerManifestRevision > manifestRevision)
				return false;
			fault = KingdomCityFault.None; return true;
		}
	}
}
