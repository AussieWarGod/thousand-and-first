using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		internal static bool TryAcknowledgeConstructionInputPickup(KingdomSystem system,
			string ownerOperationId, int jobId, int tripId, int manifestVersion,
			string manifestDigest, long provedManifestRevision,
			out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			if (system == null || system.Jobs == null || jobId <= 0 || tripId <= 0
				|| string.IsNullOrEmpty(ownerOperationId) || provedManifestRevision <= 0L
				|| manifestVersion <= 0 || string.IsNullOrEmpty(manifestDigest)
				|| !system.Jobs.TryRead(out table, out fault)) return false;
			List<KingdomJobRow> rows = TripRows(table, tripId);
			if (rows.Count != 1) { fault = KingdomCityFault.DuplicateBinding; return false; }
			KingdomJobRow[] nextRows = new KingdomJobRow[rows.Count];
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomJobRow row = rows[i];
				if (row.DeliveryCargoAuthority
						!= KingdomDeliveryCargoAuthority.ConstructionInput
					|| row.JobId != jobId || row.DeliveryTripId != tripId
					|| row.Cargo != KingdomStockKind.OpaqueManifest
					|| !string.Equals(row.DeliveryOwnerOperationId, ownerOperationId,
						StringComparison.Ordinal)
					|| row.DeliveryOwnerManifestVersion != manifestVersion
					|| row.DeliveryOwnerManifestDigest != manifestDigest
					|| (row.DeliveryPhase != KingdomDeliveryPhase.SourceDebitPrepared
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

		/// <summary>Projects exact rooted transit custody onto current active target ground.
		/// No source zone is loaded and no item is created, substituted, or unpacked.</summary>
		internal static bool TryMaterializeConstructionInputArrival(KingdomSystem system,
			string ownerOperationId, int jobId, int tripId, int manifestVersion,
			string manifestDigest, KingdomConstructionJob ownerJob,
			KingdomConstructionInputReceipt receipt, int childOrdinal,
			Zone liveDestination, long now,
			out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			if (system == null || system.Jobs == null || system.Bindings == null
				|| string.IsNullOrEmpty(ownerOperationId) || jobId <= 0 || tripId <= 0
				|| manifestVersion <= 0 || string.IsNullOrEmpty(manifestDigest)
				|| receipt == null || receipt.Schema != manifestVersion
				|| receipt.PlanDigest != manifestDigest || childOrdinal < 0
				|| childOrdinal >= receipt.ChildCount
				|| liveDestination == null || The.ZoneManager == null
				|| !ReferenceEquals(The.ZoneManager.ActiveZone, liveDestination)
				|| KingdomSurvey.ActiveFor(liveDestination) == null
				|| !system.Jobs.TryRead(out table, out fault)) return false;
			List<KingdomJobRow> rows = TripRows(table, tripId);
			if (rows.Count != 1) { fault = KingdomCityFault.DuplicateBinding; return false; }
			KingdomJobRow row = rows[0];
			KingdomLeg last;
			if (row.DeliveryCargoAuthority
					!= KingdomDeliveryCargoAuthority.ConstructionInput
				|| row.JobId != jobId || row.DeliveryTripId != tripId
				|| row.Cargo != KingdomStockKind.OpaqueManifest
				|| row.DeliveryOwnerManifestVersion != manifestVersion
				|| row.DeliveryOwnerManifestDigest != manifestDigest
				|| row.DeliveryPhase != KingdomDeliveryPhase.InFlight
				|| !string.Equals(row.DeliveryOwnerOperationId, ownerOperationId,
					StringComparison.Ordinal)
				|| !string.Equals(row.DestZoneId, liveDestination.ZoneID,
					StringComparison.Ordinal)
				|| !row.TryLeg(row.LegCount - 1, out last) || now < last.ArriveTick)
			{ fault = KingdomCityFault.OutsideItinerary; return false; }

			Cell target = liveDestination.GetCell(row.DeliveryTargetX, row.DeliveryTargetY);
			KingdomBindingTable bindings;
			KingdomBinding binding;
			if (target == null || !system.Bindings.TryRead(out bindings, out fault)
				|| !bindings.TryGet(tripId, KingdomBindingKind.Transient, out binding)
				|| string.IsNullOrEmpty(binding.ObjectId)
				|| binding.ZoneId != row.SourceZoneId && binding.ZoneId != row.DestZoneId)
			{ fault = KingdomCityFault.UnknownBinding; return false; }
			GameObject rooted = null;
			KingdomPhysicalLookupState rootState = LookupTransitRoot(ownerOperationId,
				tripId, out rooted);
			if (rootState == KingdomPhysicalLookupState.Ambiguous)
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			GameObject targetBody = KingdomSurvey.ActiveFor(liveDestination)
				.FindBoundBody(binding.ObjectId, KingdomBindingKind.Transient);
			if (GameObject.Validate(rooted) && GameObject.Validate(targetBody)
				&& !ReferenceEquals(rooted, targetBody))
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			// A save may cut after placement but before binding/root release. Same reference wins.
			GameObject body = GameObject.Validate(targetBody) ? targetBody : rooted;
			if (!GameObject.Validate(body) || !body.IsAlive || body.Inventory == null
				|| body.IsPlayer() || body.IsPlayerLed()
				|| body.IDIfAssigned != binding.ObjectId
				|| body.GetIntProperty(KingdomResidents.JobIdProperty) != tripId
				|| body.GetPart<r_KingdomPorter>()?.JobId != tripId
				|| !ExactConstructionInputTransitManifest(body, ownerJob, receipt, childOrdinal))
			{ fault = KingdomCityFault.UnknownBinding; return false; }
			if (body.CurrentCell == null)
			{
				GameObject accepted = null;
				try { accepted = target.AddObject(body, Silent: true, NoStack: true); }
				catch { }
				finally { KingdomSurvey.ObserveAddResultInActive(liveDestination, body, accepted); }
				if (!ReferenceEquals(accepted, body) || !ReferenceEquals(body.CurrentCell, target))
				{ fault = KingdomCityFault.OutsideItinerary; return false; }
				body.MakeActive();
			}
			else if (!ReferenceEquals(body.CurrentCell, target))
			{ fault = KingdomCityFault.OutsideItinerary; return false; }
			if (!KingdomResidents.Bind(system, tripId, KingdomBindingKind.Transient,
				liveDestination.ZoneID, body, now))
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			if (!ReleaseTransitRoot(ownerOperationId, tripId, body))
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			return TryConstructionInputCarrierAtTarget(system, row, liveDestination,
				out body, out fault);
		}

		internal static bool ExactConstructionInputTransitManifest(GameObject body,
			KingdomConstructionJob ownerJob, KingdomConstructionInputReceipt receipt,
			int childOrdinal)
		{
			if (receipt == null || childOrdinal < 0 || childOrdinal >= receipt.ChildCount)
				return false;
			KingdomConstructionInputChild child = receipt.ChildAt(childOrdinal);
			if (child.JobId != child.TripId
				|| !KingdomOrdinaryCustody.TryCollect(body,
					out List<GameObject> graph, out string _)
				|| graph.Count != child.CargoCount + 1) return false;
			for (int i = child.CargoStart; i < child.CargoStart + child.CargoCount; i++)
			{
				KingdomConstructionInputCargoLine cargo = receipt.CargoAt(i);
				GameObject exact = null;
				int matches = 0;
				for (int j = 1; j < graph.Count; j++)
					if (graph[j].IDIfAssigned == cargo.ObjectId) { exact = graph[j]; matches++; }
				string marker = cargo.Kind == KingdomConstructionInputKind.Water
					? cargo.CreationMarker : cargo.CargoKey;
				if (matches != 1 || !ReferenceEquals(exact.InInventory, body)
					|| DirectManifestReferenceCount(body, exact) != 1
					|| exact.Blueprint != cargo.Blueprint
					|| exact.GetStringProperty(KingdomConstruction.InputMarkerProperty) != marker
					|| exact.HasIntProperty(KingdomConstruction.InputMarkerProperty)
					|| !KingdomPurpose.HasProtectedCargoEvidence(exact)
						&& exact.GetIntProperty("NeverStack") != 1
					|| exact.IsImportant() || exact.Equipped != null
					|| !exact.IsTakeable() || exact.HasTag("AlwaysStack")
					|| !KingdomConstruction.RoutedInputItemAuthorized(ownerJob, receipt, exact))
					return false;
				if (cargo.Kind == KingdomConstructionInputKind.Water)
				{
					LiquidVolume liquid = exact.GetPart<LiquidVolume>();
					if (liquid == null || liquid.Sealed || liquid.MaxVolume != cargo.Capacity
						|| liquid.Volume != cargo.Amount || !KingdomLiquids.HasFreshWater(liquid)
						|| exact.GetIntProperty(KingdomPorters.StockProperty) != 1)
						return false;
				}
				else if (exact.Count != cargo.Amount
					|| !KingdomConstruction.TryInputClassification(exact,
						out KingdomConstructionInputKind kind, out string classification)
					|| kind != cargo.Kind || classification != cargo.Classification) return false;
			}
			return true;
		}

		private static int DirectManifestReferenceCount(GameObject body, GameObject wanted)
		{
			int count = 0;
			for (int i = 0; body?.Inventory != null && i < body.Inventory.Objects.Count; i++)
				if (ReferenceEquals(body.Inventory.Objects[i], wanted)) count++;
			return count;
		}

		/// <summary>Publishes landing only after the same bound body is observed at the exact
		/// frozen cell. Cargo remains in that body and under the parent receipt's authority.</summary>
		internal static bool TryAcknowledgeConstructionInputLanded(KingdomSystem system,
			string ownerOperationId, int jobId, int tripId, int manifestVersion,
			string manifestDigest, Zone liveDestination,
			long provedManifestRevision, out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			if (system == null || system.Jobs == null || jobId <= 0 || tripId <= 0
				|| string.IsNullOrEmpty(ownerOperationId) || liveDestination == null
				|| manifestVersion <= 0 || string.IsNullOrEmpty(manifestDigest)
				|| provedManifestRevision <= 0L
				|| !system.Jobs.TryRead(out table, out fault)) return false;
			List<KingdomJobRow> rows = TripRows(table, tripId);
			if (rows.Count != 1) { fault = KingdomCityFault.DuplicateBinding; return false; }
			KingdomJobRow row = rows[0];
			if (row.DeliveryCargoAuthority
					!= KingdomDeliveryCargoAuthority.ConstructionInput
				|| row.JobId != jobId || row.DeliveryTripId != tripId
				|| row.Cargo != KingdomStockKind.OpaqueManifest
				|| row.DeliveryOwnerManifestVersion != manifestVersion
				|| row.DeliveryOwnerManifestDigest != manifestDigest
				|| !string.Equals(row.DeliveryOwnerOperationId, ownerOperationId,
					StringComparison.Ordinal)
				|| (row.DeliveryPhase != KingdomDeliveryPhase.InFlight
					&& row.DeliveryPhase != KingdomDeliveryPhase.LandedAwaitingOwner)
				|| provedManifestRevision < row.DeliveryOwnerManifestRevision)
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			GameObject body;
			if (!TryConstructionInputCarrierAtTarget(system, row, liveDestination,
				out body, out fault)) return false;
			KingdomJobTable next;
			KingdomJobRow landed = row.WithManifestRevision(provedManifestRevision,
				KingdomDeliveryPhase.LandedAwaitingOwner);
			return table.TryRewrite(new[] { landed }, 1, out next, out fault)
				&& system.Jobs.TryPublish(next, out fault);
		}

		private static bool TryConstructionInputCarrierAtTarget(KingdomSystem system,
			KingdomJobRow row, Zone liveDestination, out GameObject body,
			out KingdomCityFault fault)
		{
			body = null;
			fault = KingdomCityFault.UnknownBinding;
			KingdomBindingTable bindings;
			KingdomBinding binding;
			Cell target;
			if (system == null || system.Bindings == null || liveDestination == null
				|| The.ZoneManager == null
				|| !ReferenceEquals(The.ZoneManager.ActiveZone, liveDestination)
				|| KingdomSurvey.ActiveFor(liveDestination) == null
				|| !string.Equals(row.DestZoneId, liveDestination.ZoneID,
					StringComparison.Ordinal)
				|| (target = liveDestination.GetCell(row.DeliveryTargetX,
					row.DeliveryTargetY)) == null
				|| !system.Bindings.TryRead(out bindings, out fault)
				|| !bindings.TryGet(row.DeliveryTripId, KingdomBindingKind.Transient,
					out binding)
				|| !string.Equals(binding.ZoneId, liveDestination.ZoneID,
					StringComparison.Ordinal) || string.IsNullOrEmpty(binding.ObjectId)) return false;
			body = KingdomSurvey.ActiveFor(liveDestination).FindBoundBody(binding.ObjectId,
				KingdomBindingKind.Transient);
			if (!GameObject.Validate(body) || !body.IsAlive || body.Inventory == null
				|| body.IsPlayer() || body.IsPlayerLed()
				|| body.IDIfAssigned != binding.ObjectId
				|| body.CurrentCell == null || !ReferenceEquals(body.CurrentCell, target)
				|| body.GetIntProperty(KingdomResidents.JobIdProperty) != row.DeliveryTripId
				|| body.GetPart<r_KingdomPorter>()?.JobId != row.DeliveryTripId)
			{ body = null; fault = KingdomCityFault.UnknownBinding; return false; }
			fault = KingdomCityFault.None;
			return true;
		}
	}
}
