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
		private static bool CarrySourceShape(KingdomCarrySource source,
			KingdomCarryOperation op, int ordinal, bool Publication)
		{
			if (source == null || !string.Equals(source.OperationId, op.Id, StringComparison.Ordinal)
				|| !string.Equals(source.SourceEventId, ChildId(op.Id, "source", ordinal),
					StringComparison.Ordinal)
				|| !ValidRootId(source.ObjectId) || !ValidName(source.Blueprint)
				|| !TopologyValid(source.Topology, source.OwnerId, source.ZoneId, source.X, source.Y)
				|| source.Material < 0 || source.Material >= 6 || source.OriginalCount <= 0
				|| source.OriginalCount > MaxPhysicalCount || source.PlannedCount <= 0
				|| source.PlannedCount > source.OriginalCount || source.Removed < 0
				|| source.Removed > source.PlannedCount || source.UnitCursor != source.Removed
				|| !KnownPhysical(source.UnitState) || !KnownPhysical(source.State)) return false;
			int expectedOrdinal = source.Removed == source.PlannedCount
				? source.Removed - 1 : source.Removed;
			if (expectedOrdinal < 0) expectedOrdinal = 0;
			if (!string.Equals(source.UnitEventId, ChildId(op.Id,
				"source-unit-" + ordinal.ToString(CultureInfo.InvariantCulture), expectedOrdinal),
				StringComparison.Ordinal)) return false;
			if (source.Removed == source.PlannedCount)
			{
				if (source.State != KingdomLifecyclePhysicalState.Proved
					|| source.UnitState != KingdomLifecyclePhysicalState.Proved
					|| source.UnitBefore != source.OriginalCount - source.Removed + 1
					|| source.UnitAfter != source.OriginalCount - source.Removed
					|| !ExactCarrySourceReceipt(op, source, ordinal)) return false;
			}
			else
			{
				if (source.State != KingdomLifecyclePhysicalState.Prepared
					|| (source.UnitState != KingdomLifecyclePhysicalState.Prepared
						&& source.UnitState != KingdomLifecyclePhysicalState.Intent)
					|| source.UnitBefore != source.OriginalCount - source.Removed
					|| source.UnitAfter != source.UnitBefore - 1) return false;
				if (source.UnitState == KingdomLifecyclePhysicalState.Prepared)
				{
					if (!CarrySourceReceiptPrepared(source, op, ordinal)) return false;
				}
				else if (source.ReceiptState == KingdomLifecyclePhysicalState.Intent)
				{
					if (!CarrySourceReceiptIntent(source, op, ordinal)) return false;
				}
				else if (!ExactCarrySourceReceipt(op, source, ordinal)) return false;
			}
			return !Publication || (source.Removed == 0 && source.UnitCursor == 0
				&& source.UnitState == KingdomLifecyclePhysicalState.Prepared
				&& source.State == KingdomLifecyclePhysicalState.Prepared
				&& CarrySourceReceiptPrepared(source, op, ordinal));
		}

		private static bool ExactManifestSourcePrepared(KingdomCarrySource source,
			KingdomCarryOperation op, int ordinal)
		{
			return source != null && op != null
				&& string.Equals(source.OperationId, op.Id, StringComparison.Ordinal)
				&& string.Equals(source.SourceEventId, ChildId(op.Id, "source", ordinal),
					StringComparison.Ordinal)
				&& source.PlannedCount == source.OriginalCount && source.Removed == 0
				&& source.UnitCursor == 0 && source.UnitBefore == source.OriginalCount
				&& source.UnitAfter == source.OriginalCount
				&& source.LoadedCount == 0 && source.DeliveredCount == 0 && source.LostCount == 0
				&& source.CurrentTripId == 0
				&& source.CurrentTopology == source.Topology
				&& string.Equals(source.CurrentOwnerId, source.OwnerId, StringComparison.Ordinal)
				&& string.Equals(source.CurrentZoneId, source.ZoneId, StringComparison.Ordinal)
				&& source.CurrentX == source.X && source.CurrentY == source.Y
				&& ExactCarryPendingNeutral(source)
				&& source.UnitState == KingdomLifecyclePhysicalState.Prepared
				&& source.State == KingdomLifecyclePhysicalState.Prepared
				&& CarrySourceReceiptPrepared(source, op, ordinal);
		}

		private static bool ExactManifestSourceShape(KingdomCarrySource source,
			KingdomCarryOperation op, int ordinal, bool publication)
		{
			if (source == null || op == null
				|| !string.Equals(source.OperationId, op.Id, StringComparison.Ordinal)
				|| !string.Equals(source.SourceEventId, ChildId(op.Id, "source", ordinal),
					StringComparison.Ordinal)
				|| !ValidRootId(source.ObjectId) || !ValidName(source.Blueprint)
				|| !TopologyValid(source.Topology, source.OwnerId, source.ZoneId, source.X, source.Y)
				|| source.Material != -1 || source.OriginalCount <= 0
				|| source.OriginalCount > MaxPhysicalCount
				|| source.PlannedCount != source.OriginalCount || source.Removed != 0
				|| source.UnitCursor != 0 || source.UnitBefore != source.OriginalCount
				|| source.UnitAfter != source.OriginalCount
				|| !string.Equals(source.UnitEventId, ChildId(op.Id,
					"source-unit-" + ordinal.ToString(CultureInfo.InvariantCulture), 0),
					StringComparison.Ordinal)
				|| source.LoadedCount < 0 || source.DeliveredCount < 0 || source.LostCount < 0
				|| (source.LoadedCount != 0 && source.LoadedCount != source.PlannedCount)
				|| (source.DeliveredCount != 0 && source.DeliveredCount != source.PlannedCount)
				|| (source.LostCount != 0 && source.LostCount != source.PlannedCount)
				|| source.DeliveredCount + source.LostCount > source.LoadedCount
				|| !TopologyValid(source.CurrentTopology, source.CurrentOwnerId,
					source.CurrentZoneId, source.CurrentX, source.CurrentY)) return false;

			bool atOrigin = source.CurrentTopology == source.Topology
				&& string.Equals(source.CurrentOwnerId, source.OwnerId, StringComparison.Ordinal)
				&& string.Equals(source.CurrentZoneId, source.ZoneId, StringComparison.Ordinal)
				&& source.CurrentX == source.X && source.CurrentY == source.Y;
			bool prepared = source.LoadedCount == 0 && source.DeliveredCount == 0
				&& source.LostCount == 0 && source.CurrentTripId == 0 && atOrigin
				&& ExactCarryPendingNeutral(source)
				&& source.State == KingdomLifecyclePhysicalState.Prepared
				&& source.UnitState == KingdomLifecyclePhysicalState.Prepared
				&& CarrySourceReceiptPrepared(source, op, ordinal);
			if (publication || prepared) return prepared;
			if (source.LoadedCount == 0)
				return source.DeliveredCount == 0 && source.LostCount == 0
					&& source.CurrentTripId > 0 && TripMember(op, source.CurrentTripId) && atOrigin
					&& source.PendingTransfer == KingdomCarryTransferKind.Pickup
					&& TopologyValid(source.PendingTopology, source.PendingOwnerId,
						source.PendingZoneId, source.PendingX, source.PendingY)
					&& source.State == KingdomLifecyclePhysicalState.Prepared
					&& source.UnitState == KingdomLifecyclePhysicalState.Intent
					&& CarrySourceReceiptIntent(source, op, ordinal);
			if (source.CurrentTripId <= 0 || !TripMember(op, source.CurrentTripId)
				|| !ExactManifestPickupChain(source)
				|| source.State != KingdomLifecyclePhysicalState.Proved
				|| source.UnitState != KingdomLifecyclePhysicalState.Proved
				|| source.ReceiptState != KingdomLifecyclePhysicalState.Proved
				|| source.ReceiptBeforeIdMatches != 1 || source.ReceiptAfterIdMatches != 1
				|| source.ReceiptBeforeCount != source.OriginalCount
				|| source.ReceiptAfterCount != source.OriginalCount
				|| !source.ReceiptSameReference
				|| !string.Equals(source.ReceiptProofId,
					ExactCarryPickupProof(op, source, ordinal), StringComparison.Ordinal)) return false;
			if (source.DeliveredCount == 0 && source.LostCount == 0)
			{
				if (source.CurrentTopology != KingdomLifecycleTopology.Inventory
					|| !ValidRootId(source.CurrentOwnerId)) return false;
				if (ExactCarryPendingNeutral(source)) return true;
				return (source.PendingTransfer == KingdomCarryTransferKind.Delivery
					|| source.PendingTransfer == KingdomCarryTransferKind.RoadLoss)
					&& TopologyValid(source.PendingTopology, source.PendingOwnerId,
						source.PendingZoneId, source.PendingX, source.PendingY);
			}
			if (source.DeliveredCount == source.PlannedCount)
				return ExactCarryPendingNeutral(source)
					&& ExactCarryDeliveredTopology(op, source, ordinal);
			return source.LostCount == source.PlannedCount
				&& source.CurrentTopology == KingdomLifecycleTopology.Cell
				&& ExactCarryPendingNeutral(source);
		}

		private static bool ExactCarryPendingNeutral(KingdomCarrySource source)
		{
			return source != null && source.PendingTransfer == KingdomCarryTransferKind.None
				&& source.PendingTopology == KingdomLifecycleTopology.None
				&& source.PendingOwnerId == null && source.PendingZoneId == null
				&& source.PendingX == -1 && source.PendingY == -1;
		}

		/// <summary>Frozen carrier topology for exact-manifest pickup. While cargo is still on
		/// the carrier the token must match live durable Current* state; after terminal movement
		/// it remains bounded evidence tied to the same central trip.</summary>
		private static bool ExactManifestPickupChain(KingdomCarrySource source)
		{
			if (source == null || source.CurrentTripId <= 0
				|| source.ReceiptChainCount != source.CurrentTripId
				|| !ValidHashNamespace(source.ReceiptChainId, "topology")) return false;
			if (source.LoadedCount == 0)
				return source.PendingTransfer == KingdomCarryTransferKind.Pickup
					&& string.Equals(source.ReceiptChainId, TopologyId(source.PendingTopology,
						source.PendingOwnerId, source.PendingZoneId, source.PendingX,
						source.PendingY), StringComparison.Ordinal);
			if (source.DeliveredCount == 0 && source.LostCount == 0)
				return string.Equals(source.ReceiptChainId, TopologyId(source.CurrentTopology,
					source.CurrentOwnerId, source.CurrentZoneId, source.CurrentX,
					source.CurrentY), StringComparison.Ordinal);
			return source.DeliveredCount == source.PlannedCount
				|| source.LostCount == source.PlannedCount;
		}

		private static bool ExactCarryTransferCoupling(KingdomCarrySource source,
			KingdomLifecycleProjection output, KingdomCarryOperation op, int ordinal)
		{
			if (source == null || output == null || op == null) return false;
			if (source.PendingTransfer == KingdomCarryTransferKind.None)
				return output.State != KingdomLifecyclePhysicalState.Intent
					&& output.ReceiptState != KingdomLifecyclePhysicalState.Intent;
			if (source.PendingTransfer == KingdomCarryTransferKind.Pickup)
				return source.LoadedCount == 0
					&& output.State == KingdomLifecyclePhysicalState.Prepared
					&& output.ReceiptState == KingdomLifecyclePhysicalState.Prepared;
			if (source.LoadedCount != source.PlannedCount
				|| source.DeliveredCount != 0 || source.LostCount != 0
				|| ordinal != op.OutputIndex
				|| output.State != KingdomLifecyclePhysicalState.Intent
				|| output.ReceiptState != KingdomLifecyclePhysicalState.Intent) return false;
			if (source.PendingTransfer == KingdomCarryTransferKind.RoadLoss)
				return source.PendingTopology == KingdomLifecycleTopology.Cell;
			if (source.PendingTransfer != KingdomCarryTransferKind.Delivery) return false;
			bool target = source.PendingTopology == output.Topology
				&& string.Equals(source.PendingOwnerId, output.OwnerId, StringComparison.Ordinal)
				&& string.Equals(source.PendingZoneId, output.ZoneId, StringComparison.Ordinal)
				&& source.PendingX == output.X && source.PendingY == output.Y;
			bool spill = source.PendingTopology == KingdomLifecycleTopology.Cell
				&& source.PendingOwnerId == null
				&& string.Equals(source.PendingZoneId, op.SpillZoneId, StringComparison.Ordinal)
				&& source.PendingX == op.SpillX && source.PendingY == op.SpillY;
			return target || spill;
		}

		private static long ExactCarryProvedRevision(KingdomCarryOperation op)
		{
			if (op == null || op.Sources == null || op.Outputs == null) return -1L;
			long value = op.SignReceiptState == KingdomLifecyclePhysicalState.Proved ? 1L : 0L;
			for (int i = 0; i < op.Sources.Count; i++)
				if (op.Sources[i] != null
					&& op.Sources[i].LoadedCount == op.Sources[i].PlannedCount) value++;
			for (int i = 0; i < op.Outputs.Count; i++)
				if (op.Outputs[i] != null
					&& (op.Outputs[i].State == KingdomLifecyclePhysicalState.Proved
						|| op.Outputs[i].State == KingdomLifecyclePhysicalState.Lost)) value++;
			return value;
		}

		private static bool ExactManifestOutputShape(KingdomLifecycleProjection output,
			KingdomCarrySource source, KingdomCarryOperation op, int ordinal, bool publication)
		{
			if (output == null || source == null || op == null
				|| !string.Equals(output.OperationId, op.Id, StringComparison.Ordinal)
				|| !string.Equals(output.EventId, ChildId(op.Id, "projection", ordinal),
					StringComparison.Ordinal)
				|| !string.Equals(output.ObjectId, source.ObjectId, StringComparison.Ordinal)
				|| !string.Equals(output.Marker, ChildId(op.Id, "marker", ordinal),
					StringComparison.Ordinal)
				|| !string.Equals(output.Blueprint, source.Blueprint, StringComparison.Ordinal)
				|| !TopologyValid(output.Topology, output.OwnerId, output.ZoneId, output.X, output.Y)
				|| !string.Equals(output.ZoneId, op.DestinationZoneId, StringComparison.Ordinal)
				|| output.Material != -1 || output.Count != source.PlannedCount
				|| !output.NoStack
				|| !string.Equals(output.ReceiptId, ChildId(op.Id, "output-receipt", ordinal),
					StringComparison.Ordinal)
				|| !string.Equals(output.ReceiptTopologyId, TopologyId(output.Topology,
					output.OwnerId, output.ZoneId, output.X, output.Y), StringComparison.Ordinal)) return false;
			bool prepared = output.State == KingdomLifecyclePhysicalState.Prepared
				&& output.ReceiptState == KingdomLifecyclePhysicalState.Prepared
				&& output.ReceiptBeforeIdMatches == -1 && output.ReceiptBeforeMarkerMatches == -1
				&& output.ReceiptBeforeCount == -1 && output.ReceiptAfterIdMatches == -1
				&& output.ReceiptAfterMarkerMatches == -1 && output.ReceiptAfterCount == -1
				&& !output.ReceiptSameReference && string.IsNullOrEmpty(output.ReceiptProofId);
			if (publication || prepared) return prepared;
			if (output.State == KingdomLifecyclePhysicalState.Intent
				&& output.ReceiptState == KingdomLifecyclePhysicalState.Intent)
				return output.ReceiptBeforeIdMatches == 1
					&& output.ReceiptBeforeMarkerMatches == 0
					&& output.ReceiptBeforeCount == source.PlannedCount
					&& output.ReceiptAfterIdMatches == -1
					&& output.ReceiptAfterMarkerMatches == -1
					&& output.ReceiptAfterCount == -1 && !output.ReceiptSameReference
					&& string.IsNullOrEmpty(output.ReceiptProofId);
			bool delivered = output.State == KingdomLifecyclePhysicalState.Proved
				&& output.ReceiptState == KingdomLifecyclePhysicalState.Proved
				&& source.DeliveredCount == source.PlannedCount && source.LostCount == 0;
			bool lost = output.State == KingdomLifecyclePhysicalState.Lost
				&& output.ReceiptState == KingdomLifecyclePhysicalState.Lost
				&& source.LostCount == source.PlannedCount && source.DeliveredCount == 0;
			return (delivered || lost) && output.ReceiptBeforeIdMatches == 1
				&& output.ReceiptBeforeMarkerMatches == 0
				&& output.ReceiptBeforeCount == source.PlannedCount
				&& output.ReceiptAfterIdMatches == 1 && output.ReceiptAfterMarkerMatches == 0
				&& output.ReceiptAfterCount == source.PlannedCount
				&& output.ReceiptSameReference
				&& string.Equals(output.ReceiptProofId,
					ExactCarryDestinationProof(op, source, output, ordinal, lost),
					StringComparison.Ordinal);
		}

	}
}
