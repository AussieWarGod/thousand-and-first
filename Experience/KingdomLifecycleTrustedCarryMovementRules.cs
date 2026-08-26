using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		internal static partial class TrustedAdapter
		{
			/// <summary>Moves one whole frozen source object onto its central trip's exact carrier.
			/// No stack unit is removed and no replacement object may satisfy the receipt.</summary>
			internal static bool ProveExactCarryPickup(KingdomCarryBook book,
				KingdomCarryOperation operation, KingdomCarrySource source, int tripId,
				string carrierObjectId, string carrierZoneId,
				IKingdomLifecycleTrustedWorld world)
			{
				int ordinal = IndexOfSource(operation, source);
				if (!ExactCarryAuthority(book, operation)
					|| operation.AuthorityKind != KingdomCarryAuthorityKind.ExactManifest
					|| operation.Phase != KingdomLifecyclePhase.RemovalIntent
					|| ordinal < 0 || ordinal != operation.SourceIndex
					|| !TripMember(operation, tripId) || !ValidRootId(carrierObjectId)
					|| !ValidName(carrierZoneId)) return false;
				int matches;
				Snapshot observed = ExactObservation(world, delegate(Snapshot value)
				{
					return string.Equals(value.ObjectId, source.ObjectId, StringComparison.Ordinal);
				}, out matches);
				if (source.LoadedCount == source.PlannedCount)
					return matches == 1 && ExactCurrentSource(observed, source)
						&& source.CurrentTripId == tripId
						&& string.Equals(source.CurrentOwnerId, carrierObjectId,
							StringComparison.Ordinal);
				if (source.UnitState == KingdomLifecyclePhysicalState.Prepared)
				{
					if (matches != 1 || !ExactCurrentSource(observed, source)
						|| operation.ManifestRevision == long.MaxValue) return false;
					string pickupTopology = TopologyId(KingdomLifecycleTopology.Inventory,
						carrierObjectId, carrierZoneId, -1, -1);
					if (pickupTopology == null) return false;
					source.CurrentTripId = tripId;
					source.PendingTransfer = KingdomCarryTransferKind.Pickup;
					source.PendingTopology = KingdomLifecycleTopology.Inventory;
					source.PendingOwnerId = carrierObjectId;
					source.PendingZoneId = carrierZoneId;
					source.PendingX = -1; source.PendingY = -1;
					source.ReceiptBeforeIdMatches = 1;
					source.ReceiptBeforeCount = source.OriginalCount;
					// Exact manifests do not use the legacy per-unit receipt chain. Reuse its
					// already-versioned columns as immutable pickup-topology evidence so the
					// pickup proof remains verifiable after Current* moves on to delivery.
					source.ReceiptChainId = pickupTopology;
					source.ReceiptChainCount = tripId;
					source.ReceiptState = KingdomLifecyclePhysicalState.Intent;
					source.UnitState = KingdomLifecyclePhysicalState.Intent;
					source.LiveAuthority = observed.Reference;
					if (!ExactCarryAuthority(book, operation)) return false;
				}
				else if (source.UnitState != KingdomLifecyclePhysicalState.Intent
					|| source.ReceiptState != KingdomLifecyclePhysicalState.Intent
					|| source.CurrentTripId != tripId
					|| source.PendingTransfer != KingdomCarryTransferKind.Pickup
					|| source.PendingTopology != KingdomLifecycleTopology.Inventory
					|| !string.Equals(source.PendingOwnerId, carrierObjectId,
						StringComparison.Ordinal)
					|| !string.Equals(source.PendingZoneId, carrierZoneId,
						StringComparison.Ordinal)) return false;

				bool atBefore = matches == 1 && ExactCurrentSource(observed, source);
				bool atAfter = matches == 1 && ExactSourceAt(observed, source,
					source.PendingTopology, source.PendingOwnerId, source.PendingZoneId,
					source.PendingX, source.PendingY);
				if (!atBefore && !atAfter) return false;
				if (atBefore)
				{
					object returned;
					try
					{
						returned = world.InvokeCarryMove(observed.Reference, tripId,
							source.PendingTopology, source.PendingOwnerId, source.PendingZoneId,
							source.PendingX, source.PendingY, source.ReceiptId);
					}
					catch (Exception) { return false; }
					if (returned == null || !ReferenceEquals(observed.Reference, returned))
						return false;
					observed = ExactObservation(world, delegate(Snapshot value)
					{
						return string.Equals(value.ObjectId, source.ObjectId,
							StringComparison.Ordinal);
					}, out matches);
					if (matches != 1 || !ReferenceEquals(observed.Reference, returned)
						|| !ExactSourceAt(observed, source, source.PendingTopology,
							source.PendingOwnerId, source.PendingZoneId,
							source.PendingX, source.PendingY)) return false;
				}
			if (operation.ManifestRevision == long.MaxValue)
				return false;
				source.CurrentTopology = source.PendingTopology;
				source.CurrentOwnerId = source.PendingOwnerId;
				source.CurrentZoneId = source.PendingZoneId;
				source.CurrentX = source.PendingX; source.CurrentY = source.PendingY;
				ClearPendingTransfer(source);
				source.LoadedCount = source.PlannedCount;
				source.ReceiptAfterIdMatches = 1;
				source.ReceiptAfterCount = source.PlannedCount;
				source.ReceiptSameReference = true;
				source.ReceiptProofId = ExactCarryPickupProof(operation, source, ordinal);
				source.ReceiptState = KingdomLifecyclePhysicalState.Proved;
				source.UnitState = KingdomLifecyclePhysicalState.Proved;
				source.State = KingdomLifecyclePhysicalState.Proved;
			operation.SourceIndex = FirstIncompleteSource(operation);
				operation.ManifestRevision++;
				return ExactCarryAuthority(book, operation);
			}

			/// <summary>Moves the same loaded object to its frozen destination/store, frozen spill,
			/// or a central road-loss cell. Delivery never creates, destroys, splits, or stacks it.</summary>
			internal static bool ProveExactCarryDestination(KingdomCarryBook book,
				KingdomCarryOperation operation, KingdomCarrySource source,
				KingdomLifecycleProjection output, bool lost,
				KingdomLifecycleTopology targetTopology, string targetOwnerId,
				string targetZoneId, int targetX, int targetY,
				IKingdomLifecycleTrustedWorld world)
			{
				int ordinal = IndexOfSource(operation, source);
				if (!ExactCarryAuthority(book, operation)
					|| operation.AuthorityKind != KingdomCarryAuthorityKind.ExactManifest
					|| operation.Phase != KingdomLifecyclePhase.ProjectionIntent
					|| operation.DestinationSafetyWaiting || ordinal < 0
					|| ordinal != operation.OutputIndex || operation.Outputs == null
					|| ordinal >= operation.Outputs.Count
					|| !ReferenceEquals(operation.Outputs[ordinal], output)
					|| source.LoadedCount != source.PlannedCount
					|| source.DeliveredCount != 0 || source.LostCount != 0
					|| source.CurrentTripId <= 0 || !TripMember(operation, source.CurrentTripId)
					|| !ValidCarryDestinationTarget(operation, output, lost, targetTopology,
						targetOwnerId, targetZoneId, targetX, targetY)) return false;

				int matches;
				Snapshot observed = ExactObservation(world, delegate(Snapshot value)
				{
					return string.Equals(value.ObjectId, source.ObjectId, StringComparison.Ordinal);
				}, out matches);
				KingdomCarryTransferKind transfer = lost
					? KingdomCarryTransferKind.RoadLoss : KingdomCarryTransferKind.Delivery;
				if (output.State == KingdomLifecyclePhysicalState.Prepared)
				{
					if (matches != 1 || !ExactCurrentSource(observed, source)
						|| operation.ManifestRevision == long.MaxValue) return false;
					source.PendingTransfer = transfer;
					source.PendingTopology = targetTopology;
					source.PendingOwnerId = targetOwnerId;
					source.PendingZoneId = targetZoneId;
					source.PendingX = targetX; source.PendingY = targetY;
					output.ReceiptBeforeIdMatches = 1;
					output.ReceiptBeforeMarkerMatches = 0;
					output.ReceiptBeforeCount = source.PlannedCount;
					output.ReceiptState = KingdomLifecyclePhysicalState.Intent;
					output.State = KingdomLifecyclePhysicalState.Intent;
					if (lost) operation.LostOnRoad = true;
					source.LiveAuthority = observed.Reference;
					if (!ExactCarryAuthority(book, operation)) return false;
				}
				else if (output.State != KingdomLifecyclePhysicalState.Intent
					|| output.ReceiptState != KingdomLifecyclePhysicalState.Intent
					|| source.PendingTransfer != transfer
					|| source.PendingTopology != targetTopology
					|| !string.Equals(source.PendingOwnerId, targetOwnerId,
						StringComparison.Ordinal)
					|| !string.Equals(source.PendingZoneId, targetZoneId,
						StringComparison.Ordinal)
					|| source.PendingX != targetX || source.PendingY != targetY) return false;

				bool atBefore = matches == 1 && ExactCurrentSource(observed, source);
				bool atAfter = matches == 1 && ExactSourceAt(observed, source,
					source.PendingTopology, source.PendingOwnerId, source.PendingZoneId,
					source.PendingX, source.PendingY);
				if (!atBefore && !atAfter) return false;
				if (atBefore)
				{
					object returned;
					try
					{
						returned = world.InvokeCarryMove(observed.Reference, source.CurrentTripId,
							source.PendingTopology, source.PendingOwnerId, source.PendingZoneId,
							source.PendingX, source.PendingY, output.ReceiptId);
					}
					catch (Exception) { return false; }
					if (returned == null || !ReferenceEquals(observed.Reference, returned))
						return false;
					observed = ExactObservation(world, delegate(Snapshot value)
					{
						return string.Equals(value.ObjectId, source.ObjectId,
							StringComparison.Ordinal);
					}, out matches);
					if (matches != 1 || !ReferenceEquals(observed.Reference, returned)
						|| !ExactSourceAt(observed, source, source.PendingTopology,
							source.PendingOwnerId, source.PendingZoneId,
							source.PendingX, source.PendingY)) return false;
				}

				KingdomLifecycleTopology settledTopology = source.PendingTopology;
				string settledOwner = source.PendingOwnerId;
				string settledZone = source.PendingZoneId;
				int settledX = source.PendingX;
				int settledY = source.PendingY;
				source.CurrentTopology = settledTopology; source.CurrentOwnerId = settledOwner;
				source.CurrentZoneId = settledZone; source.CurrentX = settledX;
				source.CurrentY = settledY; ClearPendingTransfer(source);
				if (lost) source.LostCount = source.PlannedCount;
				else source.DeliveredCount = source.PlannedCount;
				output.ReceiptAfterIdMatches = 1;
				output.ReceiptAfterMarkerMatches = 0;
				output.ReceiptAfterCount = source.PlannedCount;
				output.ReceiptSameReference = true;
				output.ReceiptProofId = ExactCarryDestinationProof(operation, source,
					output, ordinal, lost);
				output.ReceiptState = lost ? KingdomLifecyclePhysicalState.Lost
					: KingdomLifecyclePhysicalState.Proved;
				output.State = output.ReceiptState;
				operation.OutputIndex++;
				operation.ManifestRevision++;
				return ExactCarryAuthority(book, operation);
			}

			internal static bool SetExactCarryDestinationSafety(KingdomCarryBook book,
				KingdomCarryOperation operation, bool waiting, long tick)
			{
				if (!ExactCarryAuthority(book, operation)
					|| operation.AuthorityKind != KingdomCarryAuthorityKind.ExactManifest
					|| operation.Phase != KingdomLifecyclePhase.ProjectionIntent
					|| tick < operation.UpdatedTick || operation.OutputIndex != 0) return false;
				for (int i = 0; i < operation.Outputs.Count; i++)
					if (operation.Outputs[i] == null
						|| operation.Outputs[i].State != KingdomLifecyclePhysicalState.Prepared)
						return false;
				operation.DestinationSafetyWaiting = waiting;
				operation.DestinationSafetyWaitTick = waiting ? tick : 0L;
				operation.UpdatedTick = tick;
				return ExactCarryAuthority(book, operation);
			}

		}
	}
}
