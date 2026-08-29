using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		/// <summary>Removes neutral rows left by a crash before this exact parent published its
		/// receipt. An adopted/body-bearing row can never pass this sweep.</summary>
		internal static bool TrySweepUnadoptedConstructionInputOwner(KingdomSystem system,
			string ownerOperationId, out KingdomCityFault fault)
		{
			if (string.IsNullOrEmpty(ownerOperationId))
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			return SweepNeutralReservations(system, ownerOperationId, null, out fault);
		}

		/// <summary>Global crash recovery. Every parent with a durable input receipt is protected;
		/// all other still-neutral construction reservations have no adopting authority and close.
		/// This is called outside preparation, so it cannot observe an in-stack reservation.</summary>
		internal static bool TrySweepOrphanedConstructionInputReservations(
			KingdomSystem system, IList<string> adoptedOwnerOperationIds,
			out KingdomCityFault fault)
		{
			if (adoptedOwnerOperationIds == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			HashSet<string> protectedOwners = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < adoptedOwnerOperationIds.Count; i++)
				if (string.IsNullOrEmpty(adoptedOwnerOperationIds[i])
					|| !protectedOwners.Add(adoptedOwnerOperationIds[i]))
				{
					fault = KingdomCityFault.DuplicateBinding;
					return false;
				}
			return SweepNeutralReservations(system, null, protectedOwners, out fault);
		}

		private static bool SweepNeutralReservations(KingdomSystem system, string onlyOwner,
			HashSet<string> protectedOwners, out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			KingdomBindingTable bindings;
			if (system == null || system.Jobs == null || system.Bindings == null
				|| !system.Jobs.TryRead(out table, out fault)
				|| !system.Bindings.TryRead(out bindings, out fault)) return false;
			List<int> close = new List<int>();
			for (int i = 0; i < table.Count; i++)
			{
				KingdomJobRow row;
				if (!table.TryAt(i, out row)
					|| row.DeliveryCargoAuthority
						!= KingdomDeliveryCargoAuthority.ConstructionInput
					|| row.DeliveryPhase != KingdomDeliveryPhase.ReservationPrepared
					|| onlyOwner != null && !string.Equals(row.DeliveryOwnerOperationId,
						onlyOwner, StringComparison.Ordinal)
					|| protectedOwners != null
						&& protectedOwners.Contains(row.DeliveryOwnerOperationId)) continue;
				if (row.Cargo != KingdomStockKind.OpaqueManifest
					|| row.JobId != row.DeliveryTripId
					|| string.IsNullOrEmpty(row.DeliveryOwnerOperationId)
					|| row.DeliveryOwnerManifestVersion != 0
					|| !string.IsNullOrEmpty(row.DeliveryOwnerManifestDigest)
					|| row.DeliveryOwnerManifestRevision != 0L
					|| bindings.Holds(row.DeliveryTripId, KingdomBindingKind.Transient)
					|| ConstructionInputTransitRootExists(row.DeliveryOwnerOperationId,
						row.DeliveryTripId)
					|| LookupRetirement(row.DeliveryOwnerOperationId, row.DeliveryTripId,
						out string _) != KingdomPhysicalLookupState.Absent)
				{
					fault = KingdomCityFault.DuplicateBinding;
					return false;
				}
				close.Add(row.JobId);
			}
			for (int i = 0; i < close.Count; i++)
			{
				KingdomJobTable next;
				KingdomJobRow closed;
				if (!table.TryClose(close[i], out next, out closed, out fault)) return false;
				table = next;
			}
			if (close.Count == 0) { fault = KingdomCityFault.None; return true; }
			return system.Jobs.TryPublish(table, out fault);
		}
	}
}
