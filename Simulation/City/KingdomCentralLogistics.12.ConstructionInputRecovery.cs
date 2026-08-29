using System;
using System.Collections.Generic;

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomCentralLogistics
	{
		/// <summary>Closes route/body authority only after the owning receipt proves the exact
		/// adopted manifest and that every cargo object has left central custody. A retry after the
		/// row publication only retires the stale physical projection.</summary>
		internal static bool TryCloseConstructionInputTrip(KingdomSystem system,
			string ownerOperationId, int tripId, int provedManifestVersion,
			string provedManifestDigest, long provedManifestRevision,
			bool ownerReceiptProvesCargoReleased, XRL.World.Zone liveTarget,
			out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			if (system == null || system.Jobs == null || system.Bindings == null
				|| string.IsNullOrEmpty(ownerOperationId)
				|| tripId <= 0 || provedManifestVersion <= 0
				|| string.IsNullOrEmpty(provedManifestDigest) || provedManifestRevision <= 0L
				|| !ownerReceiptProvesCargoReleased
				|| !ActiveZone(liveTarget) || !system.Jobs.TryRead(out table, out fault)) return false;
			List<KingdomJobRow> rows = TripRows(table, tripId);
			if (rows.Count == 0)
			{
				KingdomBindingTable absentBindings;
				if (ConstructionInputTransitRootExists(ownerOperationId, tripId)
					|| !system.Bindings.TryRead(out absentBindings, out fault)
					|| absentBindings.Holds(tripId, KingdomBindingKind.Transient)
					|| CountActiveTripBodies(liveTarget, tripId, null, out _) != 0
					|| !ExactRetirementMarker(ownerOperationId, tripId, out string _))
				{ fault = KingdomCityFault.DuplicateBinding; return false; }
				fault = KingdomCityFault.None;
				return true;
			}
			if (rows.Count != 1) { fault = KingdomCityFault.DuplicateBinding; return false; }
			KingdomJobRow row = rows[0];
			if (row.DeliveryCargoAuthority
					!= KingdomDeliveryCargoAuthority.ConstructionInput
				|| row.DeliveryPhase != KingdomDeliveryPhase.LandedAwaitingOwner
				|| !string.Equals(row.DeliveryOwnerOperationId, ownerOperationId,
					StringComparison.Ordinal)
				|| row.DeliveryOwnerManifestVersion != provedManifestVersion
				|| !string.Equals(row.DeliveryOwnerManifestDigest, provedManifestDigest,
					StringComparison.Ordinal)
				|| provedManifestRevision < row.DeliveryOwnerManifestRevision)
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			if (!TryRetireActiveConstructionInputCarrier(system, ownerOperationId, tripId,
				liveTarget, row.DeliveryTargetX, row.DeliveryTargetY, out fault)
				|| !RetirementSettled(system, ownerOperationId, tripId, liveTarget)) return false;
			KingdomJobTable next;
			KingdomJobRow[] closed;
			if (!table.TryCloseTrip(tripId, out next, out closed, out fault)
				|| !system.Jobs.TryPublish(next, out fault)) return false;
			return true;
		}

		/// <summary>Atomically releases an adopted owner that never transferred custody. The
		/// parent first proves every split/cask was restored or released; only then may all exact
		/// SourceDebitPrepared rows retire their empty projections and close.</summary>
		internal static bool TryReleaseUndebitedConstructionInputOwner(KingdomSystem system,
			string ownerOperationId, int provedManifestVersion, string provedManifestDigest,
			long provedManifestRevision,
			bool ownerReceiptProvesAllCargoReleasedAndSourcesRestored,
			out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			if (system == null || system.Jobs == null || string.IsNullOrEmpty(ownerOperationId)
				|| provedManifestVersion <= 0 || string.IsNullOrEmpty(provedManifestDigest)
				|| provedManifestRevision < 0L
				|| !ownerReceiptProvesAllCargoReleasedAndSourcesRestored
				|| !system.Jobs.TryRead(out table, out fault)) return false;
			List<KingdomJobRow> rows = ConstructionInputRows(table, ownerOperationId);
			// This non-attended legacy entry point has no lawful retirement partition and
			// cannot prove an absent expected-child set. It is deliberately fail-closed.
			fault = rows.Count >= 0 ? KingdomCityFault.OutsideItinerary
				: KingdomCityFault.DuplicateBinding;
			return false;
		}

		/// <summary>Closes every route projection only after a mixed cancellation receipt proves
		/// each exact source restored/compensated and each cargo authority released. Neutral rows
		/// retain zero manifest authority; adopted rows must match the frozen parent.</summary>
		internal static bool TryCloseCancelledConstructionInputOwner(KingdomSystem system,
			string ownerOperationId, int provedManifestVersion,
			string provedManifestDigest, long provedManifestRevision,
			bool ownerReceiptProvesEveryLineReturned,
			KingdomConstructionInputReceipt receipt, IList<int> expectedTripIds,
			out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			if (system == null || system.Jobs == null || system.Bindings == null
				|| string.IsNullOrEmpty(ownerOperationId) || provedManifestVersion <= 0
				|| string.IsNullOrEmpty(provedManifestDigest) || provedManifestRevision < 0L
				|| !ownerReceiptProvesEveryLineReturned
				|| receipt == null || receipt.ConstructionJobId != ownerOperationId
				|| expectedTripIds == null || expectedTripIds.Count < 1
				|| !system.Jobs.TryRead(out table, out fault)) return false;
			List<KingdomJobRow> rows = ConstructionInputRows(table, ownerOperationId);
			if (rows.Count == 0)
			{
				KingdomBindingTable closedBindings;
				if (!system.Bindings.TryRead(out closedBindings, out fault)) return false;
				for (int i = 0; i < expectedTripIds.Count; i++)
				{
					int trip = expectedTripIds[i];
					bool neutral = ExpectedNeutralTrip(receipt, trip);
					if (closedBindings.Holds(trip, KingdomBindingKind.Transient)
						|| ConstructionInputTransitRootExists(ownerOperationId, trip)
						|| neutral && !NeutralRetirementSettled(ownerOperationId, trip)
						|| !neutral && !ExactRetirementMarker(ownerOperationId, trip,
							out string _))
					{ fault = KingdomCityFault.DuplicateBinding; return false; }
				}
				fault = KingdomCityFault.None; return true;
			}
			if (rows.Count != expectedTripIds.Count)
			{ fault = KingdomCityFault.DuplicateBinding; return false; }
			int[] trips = new int[rows.Count];
			bool[] rowNeutral = new bool[rows.Count];
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomJobRow row = rows[i];
				bool expected = false;
				for (int j = 0; j < expectedTripIds.Count; j++)
					if (expectedTripIds[j] == row.DeliveryTripId) { expected = true; break; }
				bool neutral = row.DeliveryPhase == KingdomDeliveryPhase.ReservationPrepared;
				if (!expected || row.DeliveryCargoAuthority
						!= KingdomDeliveryCargoAuthority.ConstructionInput
					|| row.Cargo != KingdomStockKind.OpaqueManifest
					|| row.JobId != row.DeliveryTripId
					|| TripRows(table, row.DeliveryTripId).Count != 1
					|| (!neutral && row.DeliveryPhase != KingdomDeliveryPhase.SourceDebitPrepared
						&& row.DeliveryPhase != KingdomDeliveryPhase.InFlight
						&& row.DeliveryPhase != KingdomDeliveryPhase.LandedAwaitingOwner
						&& row.DeliveryPhase != KingdomDeliveryPhase.Quarantined)
					|| (neutral && (row.DeliveryOwnerManifestVersion != 0
						|| !string.IsNullOrEmpty(row.DeliveryOwnerManifestDigest)
						|| row.DeliveryOwnerManifestRevision != 0L))
					|| (!neutral && (row.DeliveryOwnerManifestVersion != provedManifestVersion
						|| !string.Equals(row.DeliveryOwnerManifestDigest,
							provedManifestDigest, StringComparison.Ordinal)
						|| row.DeliveryOwnerManifestRevision > provedManifestRevision)))
				{ fault = KingdomCityFault.DuplicateBinding; return false; }
				trips[i] = row.DeliveryTripId;
				rowNeutral[i] = neutral;
			}
			KingdomBindingTable bindings;
			if (!system.Bindings.TryRead(out bindings, out fault)) return false;
			for (int i = 0; i < trips.Length; i++)
			{
				bool expectedNeutral = ExpectedNeutralTrip(receipt, trips[i]);
				bool cleanNeutral = expectedNeutral && rowNeutral[i];
				KingdomConstructionInputChild expectedChild = null;
				for (int j = 0; expectedNeutral && j < receipt.ChildCount; j++)
					if (receipt.ChildAt(j).TripId == trips[i]) expectedChild = receipt.ChildAt(j);
				bool adoptedNeutral = expectedNeutral && !rowNeutral[i]
					&& ExactNeverProjectedCancellation(receipt, expectedChild, rows[i])
					&& ExactUnprojectedMarker(ownerOperationId, trips[i]);
				if (bindings.Holds(trips[i], KingdomBindingKind.Transient)
					|| ConstructionInputTransitRootExists(ownerOperationId, trips[i])
					|| cleanNeutral && !NeutralRetirementSettled(ownerOperationId, trips[i])
					|| !cleanNeutral && !adoptedNeutral
						&& !ExactRetirementMarker(ownerOperationId, trips[i],
						out string _))
				{ fault = KingdomCityFault.DuplicateBinding; return false; }
			}
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomJobTable next;
				KingdomJobRow closed;
				if (!table.TryClose(rows[i].JobId, out next, out closed, out fault)) return false;
				table = next;
			}
			return system.Jobs.TryPublish(table, out fault);
		}

		private static bool ExpectedNeutralTrip(KingdomConstructionInputReceipt receipt,
			int tripId)
		{
			int matches = 0;
			for (int i = 0; receipt != null && i < receipt.ChildCount; i++)
				if (receipt.ChildAt(i).TripId == tripId
					&& receipt.ChildAt(i).CentralPhase
						== (int)KingdomDeliveryPhase.ReservationPrepared) matches++;
			return matches == 1;
		}

		private static bool NeutralRetirementSettled(string owner, int tripId)
		{
			KingdomPhysicalLookupState state = LookupRetirement(owner, tripId, out string _);
			return state == KingdomPhysicalLookupState.Absent
				|| state == KingdomPhysicalLookupState.Exact
				&& (ExactUnprojectedMarker(owner, tripId)
					|| ExactRetirementMarker(owner, tripId, out string _));
		}

		/// <summary>Stops every child owned by one parent without unbinding a body or touching
		/// inventory. The exact cargo remains recoverable for explicit parent reconciliation.</summary>
		internal static bool TryQuarantineConstructionInputOwner(KingdomSystem system,
			string ownerOperationId, out KingdomCityFault fault)
		{
			fault = KingdomCityFault.NullArgument;
			KingdomJobTable table;
			if (system == null || system.Jobs == null || string.IsNullOrEmpty(ownerOperationId)
				|| !system.Jobs.TryRead(out table, out fault)) return false;
			return QuarantineConstructionInputOwner(system, table, ownerOperationId, out fault);
		}

		private static bool QuarantineConstructionInputOwner(KingdomSystem system,
			KingdomJobTable table, string ownerOperationId, out KingdomCityFault fault)
		{
			List<KingdomJobRow> rows = ConstructionInputRows(table, ownerOperationId);
			if (rows.Count == 0) { fault = KingdomCityFault.None; return true; }
			KingdomJobRow[] held = new KingdomJobRow[rows.Count];
			for (int i = 0; i < rows.Count; i++)
				held[i] = rows[i].WithDeliveryPhase(KingdomDeliveryPhase.Quarantined);
			KingdomJobTable next;
			return table.TryRewrite(held, held.Length, out next, out fault)
				&& system.Jobs.TryPublish(next, out fault);
		}
	}
}
