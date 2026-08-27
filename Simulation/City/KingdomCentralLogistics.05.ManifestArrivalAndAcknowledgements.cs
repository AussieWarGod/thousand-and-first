using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		/// <summary>At the frozen arrival tick, thaws and moves the same central porter body to
		/// the exact destination cell. This is the off-screen half of the itinerary: cargo remains
		/// in the body's real inventory and no replacement porter or item is minted.</summary>
		internal static bool TryMaterializeManifestArrival(KingdomSystem system,
			string ownerOperationId, int tripId, Zone liveDestination, long now,
			out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			if (system == null || system.Jobs == null || system.Bindings == null
				|| string.IsNullOrEmpty(ownerOperationId) || tripId <= 0
				|| liveDestination == null || The.ZoneManager == null
				|| !system.Jobs.TryRead(out table, out fault)) return false;
			List<KingdomJobRow> rows = TripRows(table, tripId);
			if (rows.Count != 1)
			{
				fault = KingdomCityFault.DuplicateBinding;
				return false;
			}
			KingdomJobRow row = rows[0];
			KingdomLeg last;
			if (row.DeliveryCargoAuthority != KingdomDeliveryCargoAuthority.CarryBookManifest
				|| row.DeliveryPhase != KingdomDeliveryPhase.InFlight
				|| !string.Equals(row.DeliveryOwnerOperationId, ownerOperationId,
					StringComparison.Ordinal)
				|| !string.Equals(row.DestZoneId, liveDestination.ZoneID,
					StringComparison.Ordinal)
				|| !row.TryLeg(row.LegCount - 1, out last) || now < last.ArriveTick)
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			Cell target = liveDestination.GetCell(row.DeliveryTargetX, row.DeliveryTargetY);
			KingdomBindingTable bindings;
			KingdomBinding binding;
			if (target == null || !system.Bindings.TryRead(out bindings, out fault)
				|| !bindings.TryGet(tripId, KingdomBindingKind.Transient, out binding)
				|| string.IsNullOrEmpty(binding.ObjectId) || string.IsNullOrEmpty(binding.ZoneId))
			{
				fault = KingdomCityFault.UnknownBinding;
				return false;
			}
			Zone heldZone = null;
			if (The.ZoneManager.CachedZones != null)
				The.ZoneManager.CachedZones.TryGetValue(binding.ZoneId, out heldZone);
			if (heldZone == null)
			{
				try { heldZone = The.ZoneManager.GetZone(binding.ZoneId); }
				catch { fault = KingdomCityFault.OutsideItinerary; return false; }
			}
			GameObject body = heldZone == null ? null : heldZone.FindObjectByID(binding.ObjectId);
			if (!GameObject.Validate(body) || !body.IsAlive || body.Inventory == null
				|| body.CurrentCell == null
				|| body.GetIntProperty(KingdomResidents.JobIdProperty) != tripId)
			{
				fault = KingdomCityFault.UnknownBinding;
				return false;
			}
			if (!ReferenceEquals(body.CurrentCell, target))
			{
				try
				{
					if (!body.SystemLongDistanceMoveTo(target, 0, forced: true,
						ignoreCombat: true) || !ReferenceEquals(body.CurrentCell, target))
					{
						fault = KingdomCityFault.OutsideItinerary;
						return false;
					}
				}
				catch { fault = KingdomCityFault.OutsideItinerary; return false; }
			}
			if (!KingdomResidents.Bind(system, tripId, KingdomBindingKind.Transient,
				liveDestination.ZoneID, body, now))
			{
				fault = KingdomCityFault.DuplicateBinding;
				return false;
			}
			fault = KingdomCityFault.None;
			return ReferenceEquals(body.CurrentCell, target)
				&& string.Equals(body.CurrentZone == null ? null : body.CurrentZone.ZoneID,
					liveDestination.ZoneID, StringComparison.Ordinal);
		}

		internal static bool TryAcknowledgeManifestPickup(KingdomSystem system,
			string ownerOperationId, int tripId, long provedManifestRevision,
			out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			if (system == null || system.Jobs == null || tripId <= 0
				|| string.IsNullOrEmpty(ownerOperationId) || provedManifestRevision <= 0L
				|| !system.Jobs.TryRead(out table, out fault)) return false;
			List<KingdomJobRow> rows = TripRows(table, tripId);
			if (rows.Count <= 0) { fault = KingdomCityFault.InvalidIndex; return false; }
			KingdomJobRow[] nextRows = new KingdomJobRow[rows.Count];
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomJobRow row = rows[i];
				if (row.DeliveryCargoAuthority != KingdomDeliveryCargoAuthority.CarryBookManifest
					|| !string.Equals(row.DeliveryOwnerOperationId, ownerOperationId,
						StringComparison.Ordinal) || (row.DeliveryPhase
						!= KingdomDeliveryPhase.SourceDebitPrepared
						&& row.DeliveryPhase != KingdomDeliveryPhase.InFlight)
					|| provedManifestRevision < row.DeliveryOwnerManifestRevision)
				{ fault = KingdomCityFault.DuplicateBinding; return false; }
				nextRows[i] = row.WithManifestRevision(provedManifestRevision,
					KingdomDeliveryPhase.InFlight);
			}
			KingdomJobTable next;
			return table.TryRewrite(nextRows, nextRows.Length, out next, out fault)
				&& system.Jobs.TryPublish(next, out fault);
		}

		internal static bool TryAcknowledgeManifestDelivered(KingdomSystem system,
			string ownerOperationId, int tripId, long provedManifestRevision,
			out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			if (system == null || system.Jobs == null || tripId <= 0
				|| string.IsNullOrEmpty(ownerOperationId) || provedManifestRevision <= 0L
				|| !system.Jobs.TryRead(out table, out fault)) return false;
			List<KingdomJobRow> rows = TripRows(table, tripId);
			if (rows.Count <= 0) { fault = KingdomCityFault.InvalidIndex; return false; }
			for (int i = 0; i < rows.Count; i++)
				if (rows[i].DeliveryCargoAuthority
						!= KingdomDeliveryCargoAuthority.CarryBookManifest
					|| rows[i].DeliveryPhase != KingdomDeliveryPhase.InFlight
					|| !string.Equals(rows[i].DeliveryOwnerOperationId, ownerOperationId,
						StringComparison.Ordinal)
					|| provedManifestRevision < rows[i].DeliveryOwnerManifestRevision)
				{ fault = KingdomCityFault.DuplicateBinding; return false; }
			KingdomJobTable next;
			KingdomJobRow[] closed;
			if (!table.TryCloseTrip(tripId, out next, out closed, out fault)
				|| !system.Jobs.TryPublish(next, out fault)) return false;
			KingdomPorters.RetireCentralCarrier(system, tripId);
			return true;
		}
	}
}
