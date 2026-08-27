using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		internal static bool TryActivateManifestReservation(KingdomSystem system,
			string ownerOperationId, int manifestVersion, string manifestDigest,
			long manifestRevision, int[] jobIds, int[] tripIds, long arrivalTick,
			out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			if (system == null || system.Jobs == null || string.IsNullOrEmpty(ownerOperationId)
				|| manifestVersion <= 0 || string.IsNullOrEmpty(manifestDigest)
				|| manifestRevision < 0L || jobIds == null || tripIds == null
				|| jobIds.Length == 0 || jobIds.Length != tripIds.Length
				|| !system.Jobs.TryRead(out table, out fault)) return false;
			KingdomJobRow[] activated = new KingdomJobRow[jobIds.Length];
			bool exact = true;
			for (int i = 0; i < jobIds.Length; i++)
			{
				KingdomJobRow row;
				KingdomLeg last;
				if (!table.TryGet(jobIds[i], out row) || row.DeliveryTripId != tripIds[i]
					|| (row.DeliveryPhase != KingdomDeliveryPhase.ReservationPrepared
						&& row.DeliveryPhase != KingdomDeliveryPhase.SourceDebitPrepared
						&& row.DeliveryPhase != KingdomDeliveryPhase.InFlight)
					|| !string.Equals(row.DeliveryOwnerOperationId, ownerOperationId,
						StringComparison.Ordinal) || !row.TryLeg(row.LegCount - 1, out last)
					|| last.ArriveTick != arrivalTick
					|| (row.DeliveryPhase != KingdomDeliveryPhase.ReservationPrepared
						&& (row.DeliveryOwnerManifestVersion != manifestVersion
							|| !string.Equals(row.DeliveryOwnerManifestDigest, manifestDigest,
								StringComparison.Ordinal)
							|| row.DeliveryOwnerManifestRevision > manifestRevision)))
				{ exact = false; break; }
				activated[i] = row.DeliveryPhase == KingdomDeliveryPhase.ReservationPrepared
					? row.WithManifestAuthority(manifestVersion, manifestDigest,
						manifestRevision, KingdomDeliveryPhase.SourceDebitPrepared) : row;
			}
			if (!exact)
			{
				QuarantineOwner(system, table, ownerOperationId);
				fault = KingdomCityFault.DuplicateBinding;
				return false;
			}
			KingdomJobTable next;
			return table.TryRewrite(activated, activated.Length, out next, out fault)
				&& system.Jobs.TryPublish(next, out fault);
		}

		internal static bool TryCancelManifestReservation(KingdomSystem system,
			string ownerOperationId, out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			if (system == null || system.Jobs == null || string.IsNullOrEmpty(ownerOperationId)
				|| !system.Jobs.TryRead(out table, out fault)) return false;
			List<KingdomJobRow> rows = OwnerRows(table, ownerOperationId);
			for (int i = 0; i < rows.Count; i++)
				if (rows[i].DeliveryPhase != KingdomDeliveryPhase.ReservationPrepared)
				{ fault = KingdomCityFault.DuplicateBinding; return false; }
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomJobTable next;
				KingdomJobRow closed;
				if (!table.TryClose(rows[i].JobId, out next, out closed, out fault)) return false;
				table = next;
			}
			return system.Jobs.TryPublish(table, out fault);
		}

		internal static bool TryManifestTrip(KingdomSystem system, string ownerOperationId,
			int sourceOrdinal, out KingdomManifestTripView view)
		{
			view = default(KingdomManifestTripView);
			KingdomJobTable jobs;
			KingdomCityFault fault;
			if (system == null || system.Jobs == null || sourceOrdinal < 0
				|| string.IsNullOrEmpty(ownerOperationId)
				|| !system.Jobs.TryRead(out jobs, out fault)) return false;
			for (int i = 0; i < jobs.Count; i++)
			{
				KingdomJobRow row;
				if (!jobs.TryAt(i, out row)
					|| row.DeliveryCargoAuthority != KingdomDeliveryCargoAuthority.CarryBookManifest
					|| !string.Equals(row.DeliveryOwnerOperationId, ownerOperationId,
						StringComparison.Ordinal)
					|| sourceOrdinal < row.DeliveryManifestSourceStart
					|| sourceOrdinal >= row.DeliveryManifestSourceStart
						+ row.DeliveryManifestSourceCount) continue;
				string objectId = null;
				string zoneId = null;
				int x = -1;
				int y = -1;
				bool available = false;
				KingdomBindingTable bindings;
				if (system.Bindings != null && system.Bindings.TryRead(out bindings, out fault))
				{
					KingdomBinding binding;
					if (bindings.TryGet(row.DeliveryTripId, KingdomBindingKind.Transient, out binding))
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
				view = new KingdomManifestTripView(row.JobId, row.DeliveryTripId,
					row.DeliveryPhase, row.DeliveryManifestSourceStart,
					row.DeliveryManifestSourceCount, objectId, zoneId,
					KingdomLifecycleTopology.Cell, x, y, available);
				return true;
			}
			return false;
		}
	}
}
