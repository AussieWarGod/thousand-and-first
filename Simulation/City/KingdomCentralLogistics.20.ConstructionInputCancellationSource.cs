using System;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		internal static bool TryInspectConstructionInputCancellationCarrier(
			KingdomSystem system, string ownerOperationId, int receiptSchema,
			string manifestDigest, long manifestRevision, int jobId, int tripId,
			KingdomConstructionJob ownerJob, KingdomConstructionInputReceipt receipt,
			int childOrdinal, Zone liveSource,
			out GameObject body, out KingdomCityFault fault)
		{
			body = null; fault = KingdomCityFault.NullArgument;
			KingdomConstructionInputChild expected = receipt != null && childOrdinal >= 0
				&& childOrdinal < receipt.ChildCount ? receipt.ChildAt(childOrdinal) : null;
			KingdomJobTable jobs;
			KingdomJobRow row;
			KingdomBindingTable bindings;
			KingdomBinding binding;
			if (!ActiveZone(liveSource) || system?.Jobs == null || system.Bindings == null
				|| !system.Jobs.TryRead(out jobs, out fault) || !jobs.TryGet(jobId, out row)
				|| !ConstructionTransitRow(row, ownerOperationId, jobId, tripId)
				|| TripRows(jobs, tripId).Count != 1 || row.SourceZoneId != liveSource.ZoneID
				|| row.DeliveryOwnerManifestVersion != receiptSchema
				|| row.DeliveryOwnerManifestDigest != manifestDigest
				|| row.DeliveryOwnerManifestRevision > manifestRevision
				|| !system.Bindings.TryRead(out bindings, out fault)
				|| !bindings.TryGet(tripId, KingdomBindingKind.Transient, out binding)
				|| binding.ZoneId != row.SourceZoneId && binding.ZoneId != row.DestZoneId
				|| receipt == null || receipt.Schema != receiptSchema
				|| receipt.PlanDigest != manifestDigest || ownerJob == null
				|| ownerJob.Id != ownerOperationId
				|| receipt.ConstructionJobId != ownerOperationId || expected == null
				|| expected.JobId != jobId || expected.TripId != tripId) return false;
			KingdomPhysicalLookupState rootState = LookupTransitRoot(ownerOperationId,
				tripId, out GameObject rooted);
			if (rootState == KingdomPhysicalLookupState.Ambiguous)
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			GameObject visible = KingdomSurvey.ActiveFor(liveSource)
				.FindBoundBody(binding.ObjectId, KingdomBindingKind.Transient);
			if (GameObject.Validate(rooted) && rooted.IDIfAssigned != binding.ObjectId
				|| GameObject.Validate(visible) && visible.IDIfAssigned != binding.ObjectId
				|| GameObject.Validate(rooted) && GameObject.Validate(visible)
					&& !ReferenceEquals(rooted, visible))
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			body = GameObject.Validate(rooted) ? rooted : visible;
			if (!ExactCancellationBody(body, binding.ObjectId, tripId))
			{ body = null; fault = KingdomCityFault.UnknownBinding; return false; }
			if (!ExactConstructionInputCancellationManifest(body, ownerJob, receipt,
				childOrdinal, liveSource))
			{ body = null; fault = KingdomCityFault.DuplicateBinding; return false; }
			bool attendedProjection = rootState == KingdomPhysicalLookupState.Absent
				&& ReferenceEquals(body, visible) && binding.ZoneId == liveSource.ZoneID
				&& body.CurrentZone == liveSource
				&& body.CurrentCell == liveSource.GetCell(row.DeliverySourceX,
					row.DeliverySourceY);
			if (rootState == KingdomPhysicalLookupState.Absent && !attendedProjection)
			{ body = null; fault = KingdomCityFault.UnknownBinding; return false; }
			fault = KingdomCityFault.None; return true;
		}

		internal static bool TryAdoptSchemaTwoConstructionInputTransit(
			KingdomSystem system, string ownerOperationId, string manifestDigest,
			long manifestRevision, int jobId, int tripId,
			KingdomConstructionJob ownerJob, KingdomConstructionInputReceipt receipt,
			int childOrdinal, Zone attendedSource,
			out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			if (!TryInspectConstructionInputCancellationCarrier(system, ownerOperationId,
				2, manifestDigest, manifestRevision, jobId, tripId, ownerJob, receipt, childOrdinal,
				attendedSource,
				out GameObject body, out fault)
				|| receipt == null || receipt.Schema != 2 || receipt.PlanDigest != manifestDigest
				|| !ExactConstructionInputTransitManifest(body, ownerJob, receipt,
					childOrdinal)) return false;
			if (LookupTransitRoot(ownerOperationId, tripId, out _) == KingdomPhysicalLookupState.Exact)
			{ fault = KingdomCityFault.None; return true; }
			// The root is the write-ahead identity publication. Context removal follows it.
			if (!RootTransit(ownerOperationId, tripId, body))
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			if (body.CurrentZone != attendedSource || body.CurrentCell == null
				|| !body.TryRemoveFromContext())
			{ fault = KingdomCityFault.OutsideItinerary; return false; }
			body.RemoveFromContext();
			KingdomSurvey.ObserveCurrentTopologyInActive(attendedSource, body);
			if (LookupTransitRoot(ownerOperationId, tripId, out GameObject exact)
				!= KingdomPhysicalLookupState.Exact || !ReferenceEquals(exact, body)
				|| body.CurrentCell != null || body.CurrentZone != null)
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			fault = KingdomCityFault.None; return true;
		}

		/// <summary>Materializes the exact durable carrier on its attended source. This is
		/// the only cancellation bridge from semantic transit to callback-safe physical custody.
		/// Schema-2 InFlight saves without a root are adopted only from the exact bound body
		/// already visible on that source; the root is published before any physical change.</summary>
		internal static bool TryMaterializeConstructionInputCancellationSource(
			KingdomSystem system, string ownerOperationId, int receiptSchema,
			string manifestDigest, long manifestRevision, int jobId, int tripId,
			KingdomConstructionJob ownerJob, KingdomConstructionInputReceipt receipt,
			int childOrdinal, Zone liveSource,
			out GameObject body, out KingdomCityFault fault)
		{
			body = null;
			fault = KingdomCityFault.NullArgument;
			KingdomConstructionInputChild expected = receipt != null && childOrdinal >= 0
				&& childOrdinal < receipt.ChildCount ? receipt.ChildAt(childOrdinal) : null;
			KingdomJobTable jobs;
			KingdomJobRow row;
			KingdomBindingTable bindings;
			KingdomBinding binding;
			if (!ActiveZone(liveSource) || system?.Jobs == null || system.Bindings == null
				|| string.IsNullOrEmpty(ownerOperationId) || receiptSchema < 1
				|| string.IsNullOrEmpty(manifestDigest) || manifestRevision < 0L
				|| jobId <= 0 || tripId <= 0
				|| !system.Jobs.TryRead(out jobs, out fault) || !jobs.TryGet(jobId, out row)
				|| !ConstructionTransitRow(row, ownerOperationId, jobId, tripId)
				|| TripRows(jobs, tripId).Count != 1
				|| row.SourceZoneId != liveSource.ZoneID
				|| row.DeliveryOwnerManifestVersion != receiptSchema
				|| !string.Equals(row.DeliveryOwnerManifestDigest, manifestDigest,
					StringComparison.Ordinal)
				|| row.DeliveryOwnerManifestRevision > manifestRevision
				|| (row.DeliveryPhase != KingdomDeliveryPhase.SourceDebitPrepared
					&& row.DeliveryPhase != KingdomDeliveryPhase.InFlight
					&& row.DeliveryPhase != KingdomDeliveryPhase.LandedAwaitingOwner
					&& row.DeliveryPhase != KingdomDeliveryPhase.Quarantined)
				|| !system.Bindings.TryRead(out bindings, out fault)
				|| !bindings.TryGet(tripId, KingdomBindingKind.Transient, out binding)
				|| binding.BindingKey != tripId || string.IsNullOrEmpty(binding.ObjectId)
				|| binding.ZoneId != row.SourceZoneId && binding.ZoneId != row.DestZoneId
				|| receipt == null || receipt.Schema != receiptSchema
				|| receipt.PlanDigest != manifestDigest || ownerJob == null
				|| ownerJob.Id != ownerOperationId
				|| receipt.ConstructionJobId != ownerOperationId || expected == null
				|| expected.JobId != jobId || expected.TripId != tripId)
				return false;

			KingdomPhysicalLookupState rootState = LookupTransitRoot(ownerOperationId,
				tripId, out GameObject rooted);
			if (rootState == KingdomPhysicalLookupState.Ambiguous)
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			bool hasRoot = rootState == KingdomPhysicalLookupState.Exact;
			GameObject visible = KingdomSurvey.ActiveFor(liveSource)
				.FindBoundBody(binding.ObjectId, KingdomBindingKind.Transient);
			if (GameObject.Validate(rooted) && rooted.IDIfAssigned != binding.ObjectId
				|| GameObject.Validate(visible) && visible.IDIfAssigned != binding.ObjectId
				|| GameObject.Validate(rooted) && GameObject.Validate(visible)
					&& !ReferenceEquals(rooted, visible))
			{
				fault = KingdomCityFault.DuplicateBinding;
				return false;
			}
			body = GameObject.Validate(visible) ? visible : rooted;
			if (!ExactCancellationBody(body, binding.ObjectId, tripId))
			{
				body = null;
				fault = KingdomCityFault.UnknownBinding;
				return false;
			}
			if (!ExactConstructionInputCancellationManifest(body, ownerJob, receipt,
				childOrdinal, liveSource))
			{
				body = null;
				fault = KingdomCityFault.DuplicateBinding;
				return false;
			}

			// A retry after Add/Bind/root-release is already an exact attended projection.
			// Legacy unrooted saves use the same narrow binding/body/cell proof; an off-zone
			// or unbound body is never guessed or pulled here.
			if (!hasRoot)
			{
				if (!ReferenceEquals(body, visible) || binding.ZoneId != liveSource.ZoneID
					|| body.CurrentZone != liveSource
					|| body.CurrentCell != liveSource.GetCell(row.DeliverySourceX,
						row.DeliverySourceY))
				{
					body = null;
					fault = KingdomCityFault.UnknownBinding;
					return false;
				}
			}

			Cell source = liveSource.GetCell(row.DeliverySourceX, row.DeliverySourceY);
			if (source == null) { body = null; fault = KingdomCityFault.OutsideItinerary; return false; }
			if (body.CurrentCell == null)
			{
				GameObject accepted = null;
				try { accepted = source.AddObject(body, Silent: true, NoStack: true); }
				catch { }
				finally { KingdomSurvey.ObserveAddResultInActive(liveSource, body, accepted); }
				if (!ReferenceEquals(accepted, body) || !ReferenceEquals(body.CurrentCell, source))
				{ body = null; fault = KingdomCityFault.OutsideItinerary; return false; }
				body.MakeActive();
			}
			else if (!ReferenceEquals(body.CurrentCell, source) || body.CurrentZone != liveSource)
			{ body = null; fault = KingdomCityFault.OutsideItinerary; return false; }
			long now = The.Game == null ? 0L : The.Game.TimeTicks;
			if (!KingdomResidents.Bind(system, tripId, KingdomBindingKind.Transient,
				liveSource.ZoneID, body, now) || !ReleaseTransitRoot(ownerOperationId, tripId, body))
			{ body = null; fault = KingdomCityFault.DuplicateBinding; return false; }
			if (!TryInspectConstructionInputCancellationCarrier(system, ownerOperationId,
				receiptSchema, manifestDigest, manifestRevision, jobId, tripId, ownerJob,
				receipt, childOrdinal, liveSource, out GameObject exact, out fault)
				|| !ReferenceEquals(exact, body)) { body = null; return false; }
			fault = KingdomCityFault.None;
			return true;
		}

		private static bool ExactCancellationBody(GameObject body, string objectId, int tripId)
		{
			return GameObject.Validate(body) && body.IsAlive && body.Inventory != null
				&& !body.IsPlayer() && !body.IsPlayerLed() && body.IDIfAssigned == objectId
				&& body.GetIntProperty(KingdomResidents.JobIdProperty) == tripId
				&& body.GetPart<r_KingdomPorter>()?.JobId == tripId;
		}

		private static int CountActiveTripBodies(Zone zone, int tripId, string objectId,
			out GameObject exact)
		{
			exact = null;
			if (KingdomSurvey.ActiveFor(zone) == null
				|| !KingdomSurvey.ActiveFor(zone).TryLoaded(out System.Collections.Generic.IList<GameObject> all))
				return -1;
			int count = 0;
			for (int i = 0; i < all.Count; i++)
			{
				GameObject candidate = all[i];
				if (!GameObject.Validate(candidate)
					|| candidate.GetIntProperty(KingdomResidents.JobIdProperty) != tripId
					|| objectId != null && candidate.IDIfAssigned != objectId) continue;
				count++;
				if (count == 1) exact = candidate;
			}
			if (count != 1) exact = null;
			return count;
		}
	}
}
