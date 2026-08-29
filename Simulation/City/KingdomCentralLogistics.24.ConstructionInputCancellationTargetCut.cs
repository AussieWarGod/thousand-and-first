using System;

using XRL;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		internal static bool TryRetractConstructionInputTargetCarrier(KingdomSystem system,
			string owner, int jobId, int tripId, int schema, string digest, long revision,
			KingdomConstructionJob ownerJob, KingdomConstructionInputReceipt receipt,
			int childOrdinal, Zone liveTarget, out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomConstructionInputChild expected = receipt != null && childOrdinal >= 0
				&& childOrdinal < receipt.ChildCount ? receipt.ChildAt(childOrdinal) : null;
			if (!ActiveZone(liveTarget) || system?.Jobs == null || system.Bindings == null
				|| receipt == null || receipt.Schema != schema || receipt.PlanDigest != digest
				|| ownerJob == null || ownerJob.Id != owner
				|| receipt.ConstructionJobId != owner || expected == null
				|| expected.JobId != jobId || expected.TripId != tripId
				|| childOrdinal < 0 || childOrdinal >= receipt.ChildCount
				|| !system.Jobs.TryRead(out KingdomJobTable jobs, out fault)
				|| !jobs.TryGet(jobId, out KingdomJobRow row)
				|| TripRows(jobs, tripId).Count != 1
				|| !ConstructionTransitRow(row, owner, jobId, tripId)
				|| (row.DeliveryPhase != KingdomDeliveryPhase.InFlight
					&& row.DeliveryPhase != KingdomDeliveryPhase.LandedAwaitingOwner)
				|| row.DeliveryOwnerManifestVersion != schema
				|| row.DeliveryOwnerManifestDigest != digest
				|| row.DeliveryOwnerManifestRevision > revision)
				return false;
			KingdomPhysicalLookupState root = LookupTransitRoot(owner, tripId,
				out GameObject body);
			if (root == KingdomPhysicalLookupState.Ambiguous)
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			if (root == KingdomPhysicalLookupState.Exact)
			{
				if (!system.Bindings.TryRead(out KingdomBindingTable bindings, out fault)
					|| !bindings.TryGet(tripId, KingdomBindingKind.Transient,
						out KingdomBinding binding) || binding.ObjectId != body.IDIfAssigned
					|| binding.ZoneId != row.SourceZoneId && binding.ZoneId != row.DestZoneId
					|| !ExactConstructionInputTransitManifest(body, ownerJob, receipt,
						childOrdinal)) return false;
				if (body.CurrentCell == null && body.CurrentZone == null)
				{ fault = KingdomCityFault.None; return true; }
				if (body.CurrentZone != liveTarget
					|| body.CurrentCell != liveTarget.GetCell(row.DeliveryTargetX,
						row.DeliveryTargetY) || !body.TryRemoveFromContext())
				{ fault = KingdomCityFault.OutsideItinerary; return false; }
				body.RemoveFromContext();
				KingdomSurvey.ObserveCurrentTopologyInActive(liveTarget, body);
				if (!TryExactConstructionInputTransitCarrier(owner, tripId,
					out GameObject exactRoot, out fault) || !ReferenceEquals(exactRoot, body)
					|| !ExactConstructionInputTransitManifest(exactRoot, ownerJob, receipt,
						childOrdinal))
					return false;
				fault = KingdomCityFault.None; return true;
			}
			if (!TryResolveConstructionInputTargetCarrier(system, owner, jobId, tripId,
				schema, digest, revision, liveTarget, out body, out fault)
				|| !ExactConstructionInputTransitManifest(body, ownerJob, receipt,
					childOrdinal) || !RootTransit(owner, tripId, body)
				|| !body.TryRemoveFromContext()) return false;
			body.RemoveFromContext();
			KingdomSurvey.ObserveCurrentTopologyInActive(liveTarget, body);
			if (!TryExactConstructionInputTransitCarrier(owner, tripId,
				out GameObject exact, out fault) || !ReferenceEquals(exact, body)
				|| !ExactConstructionInputTransitManifest(exact, ownerJob, receipt,
					childOrdinal)) return false;
			fault = KingdomCityFault.None; return true;
		}

		/// <summary>Classifies arrival save cuts for cancellation partition selection. Exact
		/// semantic transit is not a target partition; target binding or a rooted body already
		/// at the frozen target is. Corrupt/ambiguous authority also blocks source work.</summary>
		internal static KingdomPhysicalLookupState LookupConstructionInputCancellationTargetCut(
			KingdomSystem system, string owner, int jobId, int tripId, int schema,
			string digest, long revision, KingdomConstructionJob ownerJob,
			KingdomConstructionInputReceipt receipt, int childOrdinal)
		{
			KingdomConstructionInputChild expected = receipt != null && childOrdinal >= 0
				&& childOrdinal < receipt.ChildCount ? receipt.ChildAt(childOrdinal) : null;
			if (system?.Jobs == null || system.Bindings == null || receipt == null
				|| ownerJob == null || ownerJob.Id != owner
				|| receipt.ConstructionJobId != owner || expected == null
				|| expected.JobId != jobId || expected.TripId != tripId
				|| childOrdinal < 0 || childOrdinal >= receipt.ChildCount
				|| !system.Jobs.TryRead(out KingdomJobTable jobs, out KingdomCityFault _)
				|| !jobs.TryGet(jobId, out KingdomJobRow row)
				|| TripRows(jobs, tripId).Count != 1
				|| !ConstructionTransitRow(row, owner, jobId, tripId)
				|| (row.DeliveryPhase != KingdomDeliveryPhase.SourceDebitPrepared
					&& row.DeliveryPhase != KingdomDeliveryPhase.InFlight
					&& row.DeliveryPhase != KingdomDeliveryPhase.LandedAwaitingOwner)
				|| row.DeliveryOwnerManifestVersion != schema
				|| row.DeliveryOwnerManifestDigest != digest
				|| row.DeliveryOwnerManifestRevision > revision
				|| !system.Bindings.TryRead(out KingdomBindingTable bindings, out _)
				|| !bindings.TryGet(tripId, KingdomBindingKind.Transient,
					out KingdomBinding binding) || string.IsNullOrEmpty(binding.ObjectId)
				|| binding.ZoneId != row.SourceZoneId && binding.ZoneId != row.DestZoneId)
				return KingdomPhysicalLookupState.Ambiguous;
			KingdomPhysicalLookupState root = LookupTransitRoot(owner, tripId,
				out GameObject body);
			if (root == KingdomPhysicalLookupState.Ambiguous)
				return KingdomPhysicalLookupState.Ambiguous;
			if (root == KingdomPhysicalLookupState.Exact)
			{
				if (body.IDIfAssigned != binding.ObjectId
					|| !ExactConstructionInputTransitManifest(body, ownerJob, receipt,
						childOrdinal)) return KingdomPhysicalLookupState.Ambiguous;
				if (body.CurrentCell == null && body.CurrentZone == null)
					return KingdomPhysicalLookupState.Absent;
				if (body.CurrentZone != null && body.CurrentZone.ZoneID == row.SourceZoneId
					&& body.CurrentCell != null && body.CurrentCell.X == row.DeliverySourceX
					&& body.CurrentCell.Y == row.DeliverySourceY)
					return KingdomPhysicalLookupState.Absent;
				return body.CurrentZone != null && body.CurrentZone.ZoneID == row.DestZoneId
					&& body.CurrentCell != null && body.CurrentCell.X == row.DeliveryTargetX
					&& body.CurrentCell.Y == row.DeliveryTargetY
					? KingdomPhysicalLookupState.Exact
					: KingdomPhysicalLookupState.Ambiguous;
			}
			if (binding.ZoneId == row.DestZoneId)
				return KingdomPhysicalLookupState.Exact;
			return binding.ZoneId == row.SourceZoneId
				? KingdomPhysicalLookupState.Absent
				: KingdomPhysicalLookupState.Ambiguous;
		}
	}
}
