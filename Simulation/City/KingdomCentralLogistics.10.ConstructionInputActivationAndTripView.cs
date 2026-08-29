using System;
using System.Collections.Generic;

using XRL;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		/// <summary>Atomically adopts every neutral reservation owned by one construction-input
		/// receipt. Exact ids, trips, arrivals, digest, and revision are all caller proofs; a
		/// mismatch refuses byte-identically instead of selecting a convenient subset.</summary>
		internal static bool TryActivateConstructionInputReservations(KingdomSystem system,
			string ownerOperationId, int manifestVersion, string manifestDigest,
			long manifestRevision, int[] jobIds, int[] tripIds, long[] arrivalTicks,
			out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			if (system == null || system.Jobs == null || string.IsNullOrEmpty(ownerOperationId)
				|| manifestVersion <= 0 || string.IsNullOrEmpty(manifestDigest)
				|| manifestRevision < 0L || jobIds == null || tripIds == null
				|| arrivalTicks == null || jobIds.Length == 0
				|| jobIds.Length != tripIds.Length || jobIds.Length != arrivalTicks.Length
				|| !system.Jobs.TryRead(out table, out fault)) return false;

			List<KingdomJobRow> ownerRows = ConstructionInputRows(table, ownerOperationId);
			KingdomJobRow[] activated = new KingdomJobRow[jobIds.Length];
			bool exact = ownerRows.Count == jobIds.Length;
			for (int i = 0; exact && i < jobIds.Length; i++)
			{
				for (int j = 0; j < i; j++)
					if (jobIds[j] == jobIds[i] || tripIds[j] == tripIds[i])
					{ exact = false; break; }
				if (!exact) break;
				KingdomJobRow row;
				KingdomLeg last;
				bool neutral;
				if (!table.TryGet(jobIds[i], out row) || row.DeliveryTripId != tripIds[i]
					|| row.JobId != row.DeliveryTripId
					|| row.DeliveryCargoAuthority
						!= KingdomDeliveryCargoAuthority.ConstructionInput
					|| row.Cargo != KingdomStockKind.OpaqueManifest
					|| !string.Equals(row.DeliveryOwnerOperationId, ownerOperationId,
						StringComparison.Ordinal) || !row.TryLeg(row.LegCount - 1, out last)
					|| last.ArriveTick != arrivalTicks[i])
				{ exact = false; break; }
				neutral = row.DeliveryPhase == KingdomDeliveryPhase.ReservationPrepared;
				if ((!neutral && row.DeliveryPhase != KingdomDeliveryPhase.SourceDebitPrepared
						&& row.DeliveryPhase != KingdomDeliveryPhase.InFlight
						&& row.DeliveryPhase != KingdomDeliveryPhase.LandedAwaitingOwner)
					|| (neutral && (row.DeliveryOwnerManifestVersion != 0
						|| !string.IsNullOrEmpty(row.DeliveryOwnerManifestDigest)
						|| row.DeliveryOwnerManifestRevision != 0L))
					|| (!neutral && (row.DeliveryOwnerManifestVersion != manifestVersion
						|| !string.Equals(row.DeliveryOwnerManifestDigest, manifestDigest,
							StringComparison.Ordinal)
						|| row.DeliveryOwnerManifestRevision > manifestRevision)))
				{ exact = false; break; }
				activated[i] = neutral
					? row.WithManifestAuthority(manifestVersion, manifestDigest,
						manifestRevision, KingdomDeliveryPhase.SourceDebitPrepared) : row;
			}
			if (!exact)
			{
				fault = KingdomCityFault.DuplicateBinding;
				return false;
			}
			KingdomJobTable next;
			return table.TryRewrite(activated, activated.Length, out next, out fault)
				&& system.Jobs.TryPublish(next, out fault);
		}

		/// <summary>Cancels only the exact still-neutral ids supplied by the parent. Missing ids
		/// are an idempotent retry; a present foreign, adopted, or shared-trip row refuses all.</summary>
		internal static bool TryCancelConstructionInputReservations(KingdomSystem system,
			string ownerOperationId, int[] reservationJobIds, out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			if (system == null || system.Jobs == null || string.IsNullOrEmpty(ownerOperationId)
				|| reservationJobIds == null || reservationJobIds.Length == 0
				|| !system.Jobs.TryRead(out table, out fault)) return false;
			List<int> found = new List<int>();
			for (int i = 0; i < reservationJobIds.Length; i++)
			{
				if (reservationJobIds[i] <= 0) { fault = KingdomCityFault.InvalidIndex; return false; }
				for (int j = 0; j < i; j++)
					if (reservationJobIds[j] == reservationJobIds[i])
					{ fault = KingdomCityFault.DuplicateBinding; return false; }
				KingdomJobRow row;
				if (!table.TryGet(reservationJobIds[i], out row)) continue;
				if (row.DeliveryCargoAuthority
						!= KingdomDeliveryCargoAuthority.ConstructionInput
					|| row.DeliveryPhase != KingdomDeliveryPhase.ReservationPrepared
					|| row.JobId != row.DeliveryTripId
					|| !string.Equals(row.DeliveryOwnerOperationId, ownerOperationId,
						StringComparison.Ordinal))
				{ fault = KingdomCityFault.DuplicateBinding; return false; }
				found.Add(row.JobId);
			}
			for (int i = 0; i < found.Count; i++)
			{
				KingdomJobTable next;
				KingdomJobRow closed;
				if (!table.TryClose(found[i], out next, out closed, out fault)) return false;
				table = next;
			}
			if (found.Count == 0) { fault = KingdomCityFault.None; return true; }
			return system.Jobs.TryPublish(table, out fault);
		}

		internal static bool TryConstructionInputTrip(KingdomSystem system,
			string ownerOperationId, int sourceOrdinal, out KingdomManifestTripView view)
		{
			view = default(KingdomManifestTripView);
			KingdomJobTable jobs;
			KingdomCityFault fault;
			KingdomJobRow found = default(KingdomJobRow);
			bool held = false;
			if (system == null || system.Jobs == null || sourceOrdinal < 0
				|| string.IsNullOrEmpty(ownerOperationId)
				|| !system.Jobs.TryRead(out jobs, out fault)) return false;
			for (int i = 0; i < jobs.Count; i++)
			{
				KingdomJobRow row;
				long end;
				if (!jobs.TryAt(i, out row) || row.DeliveryCargoAuthority
						!= KingdomDeliveryCargoAuthority.ConstructionInput
					|| !string.Equals(row.DeliveryOwnerOperationId, ownerOperationId,
						StringComparison.Ordinal)) continue;
				end = (long)row.DeliveryManifestSourceStart + row.DeliveryManifestSourceCount;
				if (sourceOrdinal < row.DeliveryManifestSourceStart || sourceOrdinal >= end) continue;
				if (held) return false;
				found = row;
				held = true;
			}
			if (!held) return false;
			string objectId = null;
			string zoneId = null;
			int x = -1;
			int y = -1;
			bool available = false;
			KingdomBindingTable bindings;
			if (system.Bindings != null && system.Bindings.TryRead(out bindings, out fault))
			{
				KingdomBinding binding;
				if (bindings.TryGet(found.DeliveryTripId, KingdomBindingKind.Transient,
					out binding))
				{
					objectId = binding.ObjectId;
					zoneId = binding.ZoneId;
					Zone live = The.Player == null ? null : The.Player.CurrentZone;
					GameObject carrier = live != null && live.ZoneID == zoneId
						? live.FindObjectByID(objectId) : null;
					if (GameObject.Validate(carrier) && carrier.CurrentCell != null)
					{
						x = carrier.CurrentCell.X; y = carrier.CurrentCell.Y; available = true;
					}
				}
			}
			view = new KingdomManifestTripView(found.JobId, found.DeliveryTripId,
				found.DeliveryPhase, found.DeliveryManifestSourceStart,
				found.DeliveryManifestSourceCount, objectId, zoneId,
				KingdomLifecycleTopology.Cell, x, y, available);
			return true;
		}
	}
}
