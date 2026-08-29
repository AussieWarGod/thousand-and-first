using XRL;
using XRL.World;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		/// <summary>Reads an exact post-root pickup cut without requiring a source-visible body.</summary>
		internal static bool TryResolveConstructionInputRootedPickup(KingdomSystem system,
			string owner, int jobId, int tripId, int schema, string digest, long revision,
			KingdomConstructionJob ownerJob, KingdomConstructionInputReceipt receipt,
			int childOrdinal, Zone attendedSource, out GameObject body, out KingdomCityFault fault)
		{
			body = null; fault = KingdomCityFault.UnknownBinding;
			KingdomPhysicalLookupState root = LookupTransitRoot(owner, tripId, out body);
			if (root == KingdomPhysicalLookupState.Absent) return false;
			if (root == KingdomPhysicalLookupState.Ambiguous)
			{ body = null; fault = KingdomCityFault.DuplicateBinding; return false; }
			KingdomJobTable jobs; KingdomBindingTable bindings;
			if (system?.Jobs == null || system.Bindings == null || receipt == null
				|| !system.Jobs.TryRead(out jobs, out fault)
				|| !jobs.TryGet(jobId, out KingdomJobRow row)
				|| TripRows(jobs, tripId).Count != 1
				|| !ConstructionTransitRow(row, owner, jobId, tripId)
				|| row.SourceZoneId != attendedSource?.ZoneID
				|| row.DeliveryOwnerManifestVersion != schema
				|| row.DeliveryOwnerManifestDigest != digest
				|| row.DeliveryOwnerManifestRevision > revision
				|| (row.DeliveryPhase != KingdomDeliveryPhase.SourceDebitPrepared
					&& row.DeliveryPhase != KingdomDeliveryPhase.InFlight)
				|| !system.Bindings.TryRead(out bindings, out fault)
				|| !bindings.TryGet(tripId, KingdomBindingKind.Transient,
					out KingdomBinding binding) || binding.ObjectId != body.IDIfAssigned
				|| binding.ZoneId != row.SourceZoneId
				|| !ExactConstructionInputTransitManifest(body, ownerJob, receipt, childOrdinal))
			{ body = null; fault = KingdomCityFault.DuplicateBinding; return false; }
			if (body.CurrentCell != null || body.CurrentZone != null)
			{ fault = KingdomCityFault.OutsideItinerary; return false; }
			fault = KingdomCityFault.None; return true;
		}

		internal static bool ConstructionInputCancellationSourceProjected(KingdomSystem system,
			string owner, int jobId, int tripId, string sourceZoneId)
		{
			KingdomJobTable jobs; KingdomBindingTable bindings;
			if (system?.Jobs == null || system.Bindings == null
				|| string.IsNullOrEmpty(sourceZoneId) || !system.Jobs.TryRead(out jobs, out _)
				|| !jobs.TryGet(jobId, out KingdomJobRow row)
				|| TripRows(jobs, tripId).Count != 1
				|| !ConstructionTransitRow(row, owner, jobId, tripId)
				|| row.SourceZoneId != sourceZoneId
				|| !system.Bindings.TryRead(out bindings, out _)
				|| !bindings.TryGet(tripId, KingdomBindingKind.Transient,
					out KingdomBinding binding)) return false;
			KingdomPhysicalLookupState root = LookupTransitRoot(owner, tripId,
				out GameObject rooted);
			Zone active = The.ZoneManager?.ActiveZone;
			bool rootedSource = root == KingdomPhysicalLookupState.Exact
				&& rooted.IDIfAssigned == binding.ObjectId && rooted.CurrentZone != null
				&& rooted.CurrentZone.ZoneID == sourceZoneId && rooted.CurrentCell != null
				&& rooted.CurrentCell.X == row.DeliverySourceX
				&& rooted.CurrentCell.Y == row.DeliverySourceY;
			bool rootedTransit = root == KingdomPhysicalLookupState.Exact
				&& rooted.IDIfAssigned == binding.ObjectId
				&& rooted.CurrentZone == null && rooted.CurrentCell == null;
			if (binding.ZoneId != sourceZoneId && !rootedSource) return false;
			if (row.DestZoneId == sourceZoneId)
			{
				GameObject body = active?.ZoneID == sourceZoneId
					? KingdomSurvey.ActiveFor(active)?.FindBoundBody(binding.ObjectId,
						KingdomBindingKind.Transient) : null;
				if (!rootedSource && (!GameObject.Validate(body) || body.CurrentCell
					!= active.GetCell(row.DeliverySourceX, row.DeliverySourceY))) return false;
			}
			return root != KingdomPhysicalLookupState.Ambiguous
				&& (root == KingdomPhysicalLookupState.Absent || rootedSource || rootedTransit);
		}
	}
}
