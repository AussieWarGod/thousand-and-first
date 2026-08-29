using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		private const string ConstructionInputTransitPrefix =
			"$ThousandAndFirst_ConstructionInputTransit_";
		private const int MaxTransitGraphObjects = 256;

		internal static bool TryRootConstructionInputTransitCarrier(KingdomSystem system,
			string ownerOperationId, int jobId, int tripId, Zone liveSource,
			out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable jobs; KingdomJobRow row;
			if (!ActiveZone(liveSource) || system?.Jobs == null
				|| string.IsNullOrEmpty(ownerOperationId) || jobId <= 0 || tripId <= 0
				|| !system.Jobs.TryRead(out jobs, out fault) || !jobs.TryGet(jobId, out row)
				|| !ConstructionTransitRow(row, ownerOperationId, jobId, tripId)
				|| TripRows(jobs, tripId).Count != 1
				|| (row.DeliveryPhase != KingdomDeliveryPhase.SourceDebitPrepared
					&& row.DeliveryPhase != KingdomDeliveryPhase.InFlight)
				|| row.SourceZoneId != liveSource.ZoneID) return false;
			GameObject carrier;
			if (!TryExactTransitRoot(ownerOperationId, tripId, out carrier))
			{
				if (row.DeliveryPhase == KingdomDeliveryPhase.InFlight)
					{ fault = KingdomCityFault.UnknownBinding; return false; }
				if (!TryResolveConstructionInputSourceCarrier(system, ownerOperationId,
					jobId, tripId, row.DeliveryOwnerManifestVersion,
					row.DeliveryOwnerManifestDigest, row.DeliveryOwnerManifestRevision,
					out carrier, out fault)) return false;
				if (!RootTransit(ownerOperationId, tripId, carrier))
				{ fault = KingdomCityFault.DuplicateBinding; return false; }
			}
			if (carrier.CurrentCell != null)
			{
				if (carrier.CurrentZone != liveSource || !carrier.TryRemoveFromContext())
				{ fault = KingdomCityFault.OutsideItinerary; return false; }
				carrier.RemoveFromContext();
				KingdomSurvey.ObserveCurrentTopologyInActive(liveSource, carrier);
			}
			if (!ExactTransitCarrier(system, ownerOperationId, jobId, tripId, carrier)
				|| carrier.CurrentCell != null || carrier.CurrentZone != null)
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			fault = KingdomCityFault.None; return true;
		}

		internal static bool TryProveConstructionInputTransitRootable(KingdomSystem system,
			string ownerOperationId, int jobId, int tripId, GameObject carrier,
			out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable jobs;
			KingdomBindingTable bindings;
			KingdomBinding binding;
			if (system?.Jobs == null || system.Bindings == null || !GameObject.Validate(carrier)
				|| !system.Jobs.TryRead(out jobs, out fault) || !jobs.TryGet(jobId, out KingdomJobRow row)
				|| TripRows(jobs, tripId).Count != 1
				|| !ConstructionTransitRow(row, ownerOperationId, jobId, tripId)
				|| !system.Bindings.TryRead(out bindings, out fault)
				|| !bindings.TryGet(tripId, KingdomBindingKind.Transient, out binding)
				|| binding.ObjectId != carrier.IDIfAssigned
				|| binding.ZoneId != row.SourceZoneId) return false;
			KingdomPhysicalLookupState root = LookupTransitRoot(ownerOperationId, tripId,
				out GameObject rooted);
			if (root == KingdomPhysicalLookupState.Ambiguous
				|| root == KingdomPhysicalLookupState.Exact && !ReferenceEquals(rooted, carrier))
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			fault = KingdomCityFault.None; return true;
		}

		internal static bool TryExactConstructionInputTransitCarrier(string ownerOperationId,
			int tripId, out GameObject carrier, out KingdomCityFault fault)
		{
			if (!TryExactTransitRoot(ownerOperationId, tripId, out carrier)
				|| carrier.CurrentCell != null || carrier.CurrentZone != null)
			{ carrier = null; fault = KingdomCityFault.UnknownBinding; return false; }
			fault = KingdomCityFault.None; return true;
		}

		internal static KingdomPhysicalLookupState FindConstructionInputTransitObject(
			KingdomSystem system, KingdomConstructionInputReceipt receipt, string objectId,
			out GameObject exact)
		{
			exact = null;
			KingdomJobTable jobs;
			KingdomBindingTable bindings;
			if (system?.Jobs == null || system.Bindings == null || receipt == null
				|| string.IsNullOrEmpty(objectId) || !system.Jobs.TryRead(out jobs, out _)
				|| !system.Bindings.TryRead(out bindings, out _))
				return KingdomPhysicalLookupState.Ambiguous;
			HashSet<GameObject> matches = new HashSet<GameObject>();
			HashSet<GameObject> seen = new HashSet<GameObject>();
			for (int i = 0; i < receipt.ChildCount; i++)
			{
				KingdomConstructionInputChild child = receipt.ChildAt(i);
				GameObject body;
				KingdomPhysicalLookupState rootState = LookupTransitRoot(
					receipt.ConstructionJobId, child.TripId, out body);
				if (rootState == KingdomPhysicalLookupState.Ambiguous)
					return KingdomPhysicalLookupState.Ambiguous;
				if (rootState == KingdomPhysicalLookupState.Absent) continue;
				if (!jobs.TryGet(child.JobId, out KingdomJobRow row)
					|| TripRows(jobs, child.TripId).Count != 1
					|| !ConstructionTransitRow(row, receipt.ConstructionJobId,
						child.JobId, child.TripId)
					|| row.DeliveryOwnerManifestVersion != receipt.Schema
					|| row.DeliveryOwnerManifestDigest != receipt.PlanDigest
					|| !bindings.TryGet(child.TripId, KingdomBindingKind.Transient,
						out KingdomBinding binding)
					|| binding.ObjectId != body.IDIfAssigned
					|| binding.ZoneId != row.SourceZoneId && binding.ZoneId != row.DestZoneId)
					return KingdomPhysicalLookupState.Ambiguous;
				if (body.GetIntProperty(KingdomResidents.JobIdProperty) != child.TripId
					|| body.GetPart<r_KingdomPorter>()?.JobId != child.TripId
					|| !KingdomOrdinaryCustody.TryCollect(body,
						out List<GameObject> graph, out string _))
					return KingdomPhysicalLookupState.Ambiguous;
				for (int cursor = 0; cursor < graph.Count; cursor++)
				{
					GameObject item = graph[cursor];
					if (!GameObject.Validate(item) || !seen.Add(item)
						|| seen.Count > MaxTransitGraphObjects)
						return KingdomPhysicalLookupState.Ambiguous;
					if (item.IDIfAssigned == objectId) matches.Add(item);
				}
			}
			if (matches.Count == 0) return KingdomPhysicalLookupState.Absent;
			if (matches.Count != 1) return KingdomPhysicalLookupState.Ambiguous;
			foreach (GameObject item in matches) exact = item;
			return KingdomPhysicalLookupState.Exact;
		}

		internal static bool TryRetireConstructionInputTransitCarrier(KingdomSystem system,
			string ownerOperationId, int tripId, out KingdomCityFault fault)
		{
			// A semantic root is not a physical retirement partition. Cancellation must
			// materialize the exact body on its attended source and use the write-ahead
			// whole-custody retirement lane instead.
			fault = KingdomCityFault.OutsideItinerary;
			return false;
		}

		internal static bool ConstructionInputTransitRootExists(string ownerOperationId,
			int tripId)
		{
			string key = TransitKey(ownerOperationId, tripId);
			return key != null && The.Game?.ObjectGameState != null
				&& The.Game.ObjectGameState.ContainsKey(key);
		}

		internal static bool ConstructionInputTransitRootSettled(string ownerOperationId,
			int tripId)
		{
			return LookupTransitRoot(ownerOperationId, tripId, out GameObject body)
				== KingdomPhysicalLookupState.Exact
				&& body.CurrentCell == null && body.CurrentZone == null;
		}

		private static bool ConstructionTransitRow(KingdomJobRow row, string owner,
			int jobId, int tripId)
		{
			return row.JobId == jobId && row.DeliveryTripId == tripId
				&& row.DeliveryCargoAuthority == KingdomDeliveryCargoAuthority.ConstructionInput
				&& row.Cargo == KingdomStockKind.OpaqueManifest
				&& row.DeliveryOwnerOperationId == owner;
		}

		private static bool ActiveZone(Zone zone)
		{
			return zone != null && The.ZoneManager != null
				&& ReferenceEquals(The.ZoneManager.ActiveZone, zone)
				&& KingdomSurvey.ActiveFor(zone) != null;
		}

		private static string TransitKey(string owner, int tripId)
		{
			return string.IsNullOrEmpty(owner) || tripId <= 0 ? null
				: ConstructionInputTransitPrefix + owner + "_" + tripId;
		}

		private static bool RootTransit(string owner, int tripId, GameObject body)
		{
			string key = TransitKey(owner, tripId);
			if (The.Game?.ObjectGameState == null || key == null || !GameObject.Validate(body)
				|| The.Game.ObjectGameState.Count > 65536) return false;
			if (The.Game.ObjectGameState.TryGetValue(key, out object prior)
				&& !ReferenceEquals(prior, body)) return false;
			The.Game.SetObjectGameState(key, body);
			return The.Game.ObjectGameState.TryGetValue(key, out object rooted)
				&& ReferenceEquals(rooted, body);
		}

		private static bool TryExactTransitRoot(string owner, int tripId, out GameObject body)
		{
			return LookupTransitRoot(owner, tripId, out body)
				== KingdomPhysicalLookupState.Exact;
		}

		private static KingdomPhysicalLookupState LookupTransitRoot(string owner,
			int tripId, out GameObject body)
		{
			body = null; string key = TransitKey(owner, tripId);
			if (The.Game?.ObjectGameState == null || key == null) return KingdomPhysicalLookupState.Ambiguous;
			if (!The.Game.ObjectGameState.TryGetValue(key, out object rooted))
				return KingdomPhysicalLookupState.Absent;
			body = rooted as GameObject;
			if (!ExactTransitCarrier(owner, tripId, body))
			{ body = null; return KingdomPhysicalLookupState.Ambiguous; }
			return KingdomPhysicalLookupState.Exact;
		}

		private static bool ExactTransitCarrier(string owner, int tripId, GameObject body)
		{
			return GameObject.Validate(body) && body.IsAlive && body.Inventory != null
				&& !body.IsPlayer() && !body.IsPlayerLed()
				&& body.GetIntProperty(KingdomResidents.JobIdProperty) == tripId
				&& body.GetPart<r_KingdomPorter>()?.JobId == tripId
				&& The.Game.ObjectGameState.TryGetValue(TransitKey(owner, tripId), out object rooted)
				&& ReferenceEquals(rooted, body);
		}

		private static bool ExactTransitCarrier(KingdomSystem system, string owner,
			int jobId, int tripId, GameObject body)
		{
			KingdomJobTable jobs;
			KingdomJobRow row;
			KingdomBindingTable bindings;
			KingdomBinding binding;
			return ExactTransitCarrier(owner, tripId, body) && system?.Jobs != null
				&& system.Bindings != null && system.Jobs.TryRead(out jobs, out _)
				&& jobs.TryGet(jobId, out row) && TripRows(jobs, tripId).Count == 1
				&& ConstructionTransitRow(row, owner, jobId, tripId)
				&& system.Bindings.TryRead(out bindings, out _)
				&& bindings.TryGet(tripId, KingdomBindingKind.Transient, out binding)
				&& binding.ObjectId == body.IDIfAssigned
				&& binding.ZoneId == row.SourceZoneId;
		}

		private static bool ReleaseTransitRoot(string owner, int tripId, GameObject body)
		{
			string key = TransitKey(owner, tripId);
			if (!The.Game.ObjectGameState.TryGetValue(key, out object rooted)) return true;
			if (!ReferenceEquals(rooted, body)) return false;
			The.Game.ObjectGameState.Remove(key); return !The.Game.ObjectGameState.ContainsKey(key);
		}
	}
}
