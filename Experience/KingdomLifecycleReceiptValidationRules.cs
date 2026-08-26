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
		private static bool ProjectionShape(KingdomLifecycleProjection p, string OperationId,
			int Ordinal, bool Publication)
		{
			if (p == null || !string.Equals(p.OperationId, OperationId, StringComparison.Ordinal)
				|| !string.Equals(p.EventId, ChildId(OperationId, "projection", Ordinal),
					StringComparison.Ordinal)
				|| !string.Equals(p.Marker, ChildId(OperationId, "marker", Ordinal),
					StringComparison.Ordinal)
				|| !ValidRootId(p.ObjectId) || !ValidName(p.Blueprint)
				|| !TopologyValid(p.Topology, p.OwnerId, p.ZoneId, p.X, p.Y)
				|| p.Material < -1 || p.Material >= 6 || p.Count <= 0
				|| p.Count > MaxPhysicalCount || !p.NoStack || !KnownPhysical(p.State)) return false;
			return !Publication || p.State == KingdomLifecyclePhysicalState.Prepared;
		}

		private static bool LifecycleProjectionReceiptPristine(KingdomLifecycleProjection p)
		{
			return p != null && string.IsNullOrEmpty(p.ReceiptId)
				&& string.IsNullOrEmpty(p.ReceiptTopologyId)
				&& p.ReceiptBeforeIdMatches == -1 && p.ReceiptBeforeMarkerMatches == -1
				&& p.ReceiptBeforeCount == -1 && p.ReceiptAfterIdMatches == -1
				&& p.ReceiptAfterMarkerMatches == -1 && p.ReceiptAfterCount == -1
				&& !p.ReceiptSameReference && string.IsNullOrEmpty(p.ReceiptProofId)
				&& p.ReceiptState == KingdomLifecyclePhysicalState.None;
		}

		private static bool CarrySourceReceiptPrepared(KingdomCarrySource source,
			KingdomCarryOperation operation, int ordinal)
		{
			return source != null && operation != null
				&& string.Equals(source.ReceiptId, ChildId(operation.Id,
					"source-receipt-" + ordinal.ToString(CultureInfo.InvariantCulture),
					source.Removed), StringComparison.Ordinal)
				&& string.Equals(source.ReceiptTopologyId, TopologyId(source.Topology,
					source.OwnerId, source.ZoneId, source.X, source.Y), StringComparison.Ordinal)
				&& source.ReceiptBeforeIdMatches == -1 && source.ReceiptAfterIdMatches == -1
				&& source.ReceiptBeforeCount == -1 && source.ReceiptAfterCount == -1
				&& !source.ReceiptSameReference && string.IsNullOrEmpty(source.ReceiptProofId)
				&& source.ReceiptState == KingdomLifecyclePhysicalState.Prepared
				&& ExactCarrySourceChain(source);
		}

		private static bool CarrySourceReceiptIntent(KingdomCarrySource source,
			KingdomCarryOperation operation, int ordinal)
		{
			return source != null && operation != null
				&& string.Equals(source.ReceiptId, ChildId(operation.Id,
					"source-receipt-" + ordinal.ToString(CultureInfo.InvariantCulture),
					source.Removed), StringComparison.Ordinal)
				&& string.Equals(source.ReceiptTopologyId, TopologyId(source.Topology,
					source.OwnerId, source.ZoneId, source.X, source.Y), StringComparison.Ordinal)
				&& source.ReceiptBeforeIdMatches == 1 && source.ReceiptAfterIdMatches == -1
				&& source.ReceiptBeforeCount == source.UnitBefore
				&& source.ReceiptAfterCount == -1 && !source.ReceiptSameReference
				&& string.IsNullOrEmpty(source.ReceiptProofId)
				&& source.ReceiptState == KingdomLifecyclePhysicalState.Intent
				&& (source.Material == -1
					? ExactManifestPickupChain(source) : ExactCarrySourceChain(source));
		}

		private static bool ExactCarrySourceReceipt(KingdomCarryOperation operation,
			KingdomCarrySource source, int ordinal)
		{
			int receiptOrdinal = source != null && source.Removed == source.PlannedCount
				? source.Removed - 1 : source == null ? -1 : source.Removed;
			return source != null && operation != null
				&& string.Equals(source.ReceiptId, ChildId(operation.Id,
					"source-receipt-" + ordinal.ToString(CultureInfo.InvariantCulture),
					receiptOrdinal), StringComparison.Ordinal)
				&& string.Equals(source.ReceiptTopologyId, TopologyId(source.Topology,
					source.OwnerId, source.ZoneId, source.X, source.Y), StringComparison.Ordinal)
				&& source.ReceiptBeforeIdMatches == 1 && source.ReceiptAfterIdMatches == 1
				&& source.ReceiptBeforeCount == source.UnitBefore
				&& source.ReceiptAfterCount == source.UnitAfter
				&& source.ReceiptSameReference
				&& source.ReceiptState == KingdomLifecyclePhysicalState.Proved
				&& string.Equals(source.ReceiptProofId,
					CarrySourceReceiptProof(operation, source, ordinal), StringComparison.Ordinal)
				&& ExactCarrySourceChain(source);
		}

		private static bool ExactCarrySourceChain(KingdomCarrySource source)
		{
			return source != null && source.ReceiptChainCount == source.Removed
				&& (source.Removed == 0 ? string.IsNullOrEmpty(source.ReceiptChainId)
					: ValidHashNamespace(source.ReceiptChainId, "carry-source-chain"));
		}

		private static string CarrySourceReceiptProof(KingdomCarryOperation operation,
			KingdomCarrySource source, int ordinal)
		{
			return HashId("carry-source-receipt", delegate(BinaryWriter w)
			{
				CanonicalString(w, operation == null ? null : operation.Id);
				w.Write(ordinal); CanonicalString(w, source == null ? null : source.UnitEventId);
				CanonicalString(w, source == null ? null : source.ReceiptId);
				CanonicalString(w, source == null ? null : source.ObjectId);
				CanonicalString(w, source == null ? null : source.Blueprint);
				CanonicalString(w, source == null ? null : source.ReceiptTopologyId);
				w.Write(source == null ? -1 : source.UnitBefore);
				w.Write(source == null ? -1 : source.UnitAfter);
				w.Write(source == null ? -1 : source.ReceiptBeforeIdMatches);
				w.Write(source == null ? -1 : source.ReceiptAfterIdMatches);
				w.Write(source == null ? -1 : source.ReceiptBeforeCount);
				w.Write(source == null ? -1 : source.ReceiptAfterCount);
				w.Write(source != null && source.ReceiptSameReference);
			});
		}

		private static string CarrySourceReceiptChain(string previous,
			string receiptProof, int count)
		{
			return HashId("carry-source-chain", delegate(BinaryWriter w)
			{
				CanonicalString(w, previous); CanonicalString(w, receiptProof); w.Write(count);
			});
		}

		private static void ResetCarrySourceReceipt(KingdomCarryOperation operation,
			KingdomCarrySource source, int ordinal, int removed)
		{
			source.ReceiptId = ChildId(operation.Id,
				"source-receipt-" + ordinal.ToString(CultureInfo.InvariantCulture), removed);
			source.ReceiptTopologyId = TopologyId(source.Topology, source.OwnerId,
				source.ZoneId, source.X, source.Y);
			source.ReceiptBeforeIdMatches = -1;
			source.ReceiptAfterIdMatches = -1;
			source.ReceiptBeforeCount = -1;
			source.ReceiptAfterCount = -1;
			source.ReceiptSameReference = false;
			source.ReceiptProofId = null;
			source.ReceiptState = KingdomLifecyclePhysicalState.Prepared;
			source.LiveAuthority = null;
		}

		private static bool CarryOutputShape(KingdomLifecycleProjection p, string OperationId,
			int Ordinal, bool Publication)
		{
			if (!ProjectionShape(p, OperationId, Ordinal, false)
				|| !string.Equals(p.ReceiptId, ChildId(OperationId, "output-receipt", Ordinal),
					StringComparison.Ordinal)
				|| !string.Equals(p.ReceiptTopologyId, TopologyId(p.Topology, p.OwnerId,
					p.ZoneId, p.X, p.Y), StringComparison.Ordinal)
				|| !KnownPhysical(p.ReceiptState)) return false;
			bool prepared = p.State == KingdomLifecyclePhysicalState.Prepared
				&& p.ReceiptState == KingdomLifecyclePhysicalState.Prepared
				&& p.ReceiptBeforeIdMatches == -1 && p.ReceiptBeforeMarkerMatches == -1
					&& p.ReceiptBeforeCount == -1 && p.ReceiptAfterIdMatches == -1
					&& p.ReceiptAfterMarkerMatches == -1 && p.ReceiptAfterCount == -1
					&& !p.ReceiptSameReference && string.IsNullOrEmpty(p.ReceiptProofId);
			if (Publication) return prepared;
			if (prepared) return true;
			if (p.State == KingdomLifecyclePhysicalState.Intent
				&& p.ReceiptState == KingdomLifecyclePhysicalState.Intent)
					return p.ReceiptBeforeIdMatches == 0 && p.ReceiptBeforeMarkerMatches == 0
						&& p.ReceiptBeforeCount == 0 && p.ReceiptAfterIdMatches == -1
						&& p.ReceiptAfterMarkerMatches == -1 && p.ReceiptAfterCount == -1
						&& !p.ReceiptSameReference && string.IsNullOrEmpty(p.ReceiptProofId);
			if (p.State == KingdomLifecyclePhysicalState.Proved
				&& p.ReceiptState == KingdomLifecyclePhysicalState.Proved)
					return p.ReceiptBeforeIdMatches == 0 && p.ReceiptBeforeMarkerMatches == 0
						&& p.ReceiptBeforeCount == 0 && p.ReceiptAfterIdMatches == 1
						&& p.ReceiptAfterMarkerMatches == 1 && p.ReceiptAfterCount == p.Count
						&& p.ReceiptSameReference && ExactCarryOutputReceiptForShape(p,
							OperationId, false);
			if (p.State == KingdomLifecyclePhysicalState.Skipped
				&& p.ReceiptState == KingdomLifecyclePhysicalState.Skipped)
					return p.ReceiptBeforeIdMatches == 0 && p.ReceiptBeforeMarkerMatches == 0
						&& p.ReceiptBeforeCount == 0 && p.ReceiptAfterIdMatches == 0
						&& p.ReceiptAfterMarkerMatches == 0 && p.ReceiptAfterCount == 0
						&& !p.ReceiptSameReference && ExactCarryOutputReceiptForShape(p,
							OperationId, true);
			return false;
		}

		private static bool ExactCarryOutputReceiptForShape(KingdomLifecycleProjection output,
			string operationId, bool lost)
		{
			return output != null && string.Equals(output.ReceiptProofId,
				CarryOutputReceiptProof(operationId, output, lost), StringComparison.Ordinal);
		}

		private static bool ExactCarryOutputReceipt(KingdomCarryOperation operation,
			KingdomLifecycleProjection output, bool lost)
		{
			return operation != null && output != null
				&& string.Equals(operation.Id, output.OperationId, StringComparison.Ordinal)
				&& ExactCarryOutputReceiptForShape(output, operation.Id, lost);
		}

		private static string CarryOutputReceiptProof(KingdomCarryOperation operation,
			KingdomLifecycleProjection output, bool lost)
		{
			return CarryOutputReceiptProof(operation == null ? null : operation.Id, output, lost);
		}

		private static string CarryOutputReceiptProof(string operationId,
			KingdomLifecycleProjection output, bool lost)
		{
			return HashId("carry-output-receipt", delegate(BinaryWriter w)
			{
				CanonicalString(w, operationId); CanonicalString(w, output.ReceiptId);
				CanonicalString(w, output.ObjectId); CanonicalString(w, output.Marker);
				CanonicalString(w, output.Blueprint);
				CanonicalString(w, output.ReceiptTopologyId); w.Write(output.Material);
				w.Write(output.Count); w.Write(output.ReceiptBeforeIdMatches);
				w.Write(output.ReceiptBeforeMarkerMatches); w.Write(output.ReceiptBeforeCount);
				w.Write(output.ReceiptAfterIdMatches); w.Write(output.ReceiptAfterMarkerMatches);
				w.Write(output.ReceiptAfterCount); w.Write(output.ReceiptSameReference);
				w.Write(lost);
			});
		}

		private static string WaterReceiptProof(KingdomLifecycleOperation operation,
			KingdomLifecycleResourceLease lease, KingdomLifecycleWaterLeg leg)
		{
			return HashId("water-receipt", delegate(BinaryWriter w)
			{
				CanonicalString(w, operation == null ? null : operation.Id);
				CanonicalString(w, operation == null ? null : operation.PlanHash);
				CanonicalString(w, leg.ReceiptId); WriteLeasePlan(w, lease);
				CanonicalString(w, leg.OwnerId); CanonicalString(w, leg.Blueprint);
				CanonicalString(w, leg.ZoneId);
				w.Write(leg.Capacity); w.Write(leg.Before); w.Write(leg.Delta); w.Write(leg.After);
				CanonicalString(w, leg.Composition); w.Write(leg.ReceiptBeforeMatches);
				w.Write(leg.ReceiptAfterMatches); w.Write(leg.ReceiptSameReference);
			});
		}

		private static bool CarryScheduleReceiptShape(KingdomCarryOperation operation,
			bool publication)
		{
			if (operation == null || operation.ScheduleLease == null
				|| !string.Equals(operation.ScheduleReceiptId,
					ChildId(operation.Id, "schedule-receipt", 0), StringComparison.Ordinal)
				|| !string.Equals(operation.ScheduleTopologyId, TopologyId(
					operation.DestinationTopology, operation.DestinationOwnerId,
					operation.DestinationZoneId, operation.DestinationX, operation.DestinationY),
					StringComparison.Ordinal) || !KnownPhysical(operation.ScheduleReceiptState))
				return false;
			bool prepared = operation.ScheduleReceiptState == KingdomLifecyclePhysicalState.Prepared
				&& operation.ScheduleBeforeMatches == -1 && operation.ScheduleAfterMatches == -1
				&& !operation.ScheduleSameReference && string.IsNullOrEmpty(operation.ScheduleProofId);
			if (publication || prepared) return prepared;
			if (operation.ScheduleReceiptState == KingdomLifecyclePhysicalState.Intent)
				return operation.ScheduleBeforeMatches == 1 && operation.ScheduleAfterMatches == -1
					&& !operation.ScheduleSameReference && string.IsNullOrEmpty(operation.ScheduleProofId);
			return operation.ScheduleReceiptState == KingdomLifecyclePhysicalState.Proved
				&& operation.ScheduleBeforeMatches == 1 && operation.ScheduleAfterMatches == 1
				&& operation.ScheduleSameReference
				&& string.Equals(operation.ScheduleProofId,
					CarryScheduleReceiptProof(operation), StringComparison.Ordinal);
		}

		private static string CarryScheduleReceiptProof(KingdomCarryOperation operation)
		{
			return HashId("carry-schedule-receipt", delegate(BinaryWriter w)
			{
				CanonicalString(w, operation.Id); CanonicalString(w, operation.PlanHash);
				CanonicalString(w, operation.RealmTopologyHash);
				CanonicalString(w, operation.DestinationSettlementId);
				CanonicalString(w, operation.ScheduleReceiptId);
				CanonicalString(w, operation.ScheduleTopologyId);
				CanonicalString(w, "Schedule");
				WriteLeasePlan(w, operation.ScheduleLease);
				w.Write(operation.ScheduleBeforeMatches); w.Write(operation.ScheduleAfterMatches);
				w.Write(operation.ScheduleSameReference);
			});
		}

	}
}
