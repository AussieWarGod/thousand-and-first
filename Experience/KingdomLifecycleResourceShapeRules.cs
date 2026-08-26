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
		private static bool LeaseShape(KingdomLifecycleResourceLease lease,
			string OperationId, bool Publication)
		{
			long after;
			return lease != null && ValidRootId(OperationId)
				&& string.Equals(lease.OperationId, OperationId, StringComparison.Ordinal)
				&& KnownOuterResourceKind(lease.Kind) && ValidRootId(lease.ScopeId)
				&& ValidRootId(lease.SubjectId)
				&& string.Equals(lease.Key, ResourceKey(lease.Kind, lease.ScopeId, lease.SubjectId),
					StringComparison.Ordinal)
				&& lease.Delta != 0L && CheckedAdd(lease.Before, lease.Delta, out after)
				&& after == lease.After && lease.BeforeRevision >= 0L
				&& lease.BeforeRevision < long.MaxValue
				&& lease.AfterRevision == lease.BeforeRevision + 1L
				&& Enum.IsDefined(typeof(KingdomLifecycleLeaseState), lease.State)
				&& (!Publication || lease.State == KingdomLifecycleLeaseState.Prepared);
		}

		private static bool ResourceShape(KingdomLifecycleResourceRevision row)
		{
			return row != null && KnownOuterResourceKind(row.Kind) && ValidRootId(row.ScopeId)
				&& ValidRootId(row.SubjectId) && row.Revision >= 0L
				&& string.Equals(row.Key, ResourceKey(row.Kind, row.ScopeId, row.SubjectId),
					StringComparison.Ordinal)
				&& (string.IsNullOrEmpty(row.ActiveOperationId) || ValidGeneratedId(row.ActiveOperationId))
				&& (string.IsNullOrEmpty(row.LastOperationId) || ValidGeneratedId(row.LastOperationId));
		}

		private static bool ResourceMatches(KingdomLifecycleResourceRevision row,
			KingdomLifecycleResourceLease lease)
		{
			return row != null && lease != null && row.Kind == lease.Kind
				&& string.Equals(row.ScopeId, lease.ScopeId, StringComparison.Ordinal)
				&& string.Equals(row.SubjectId, lease.SubjectId, StringComparison.Ordinal)
				&& string.Equals(row.Key, lease.Key, StringComparison.Ordinal);
		}

		private static bool TopologyValid(KingdomLifecycleTopology topology,
			string OwnerId, string ZoneId, int X, int Y)
		{
			if (!ValidName(ZoneId)) return false;
			if (topology == KingdomLifecycleTopology.Cell)
				return OwnerId == null && X >= 0 && X <= MaxCoordinate
					&& Y >= 0 && Y <= MaxCoordinate;
			if (topology == KingdomLifecycleTopology.Inventory)
				return ValidRootId(OwnerId) && X == -1 && Y == -1;
			return false;
		}

		private static bool CarryPublicationPlanValid(KingdomCarryOperation op)
		{
			return CarryPlanShape(op, true) && op.CreatedTick == op.UpdatedTick
				&& op.Phase == KingdomLifecyclePhase.Prepared && CarryConserved(op);
		}

		private static bool CarryPlanShape(KingdomCarryOperation op, bool Publication)
		{
			if (op != null && op.AuthorityKind == KingdomCarryAuthorityKind.ExactManifest)
				return ExactCarryPlanShape(op, Publication);
			if (op == null || op.AuthorityKind != KingdomCarryAuthorityKind.LegacyMaterialProjection
				|| !LegacyCarryExtensionNeutral(op)) return false;
			if (op.Sequence <= 0L || !ValidGeneratedId(op.Id)
				|| op.CreatedTick < 0L || op.UpdatedTick < op.CreatedTick
				|| !FrozenSettlementSetValid(op.SettlementIds)
				|| !ValidHashNamespace(op.RealmTopologyHash, "carry-realm-topology")
				|| !ValidRootId(op.OriginSettlementId)
				|| op.SettlementIds.BinarySearch(op.OriginSettlementId, StringComparer.Ordinal) < 0
				|| !ValidName(op.OriginZoneId) || op.OriginX < 0 || op.OriginX > MaxCoordinate
				|| op.OriginY < 0 || op.OriginY > MaxCoordinate
				|| !ValidRootId(op.DestinationSettlementId)
				|| op.SettlementIds.BinarySearch(op.DestinationSettlementId, StringComparer.Ordinal) < 0
				|| !ValidName(op.DestinationSettlementName)
				|| !TopologyValid(op.DestinationTopology, op.DestinationOwnerId,
					op.DestinationZoneId, op.DestinationX, op.DestinationY)
				|| op.DueTick < 0L || !op.RiskFrozen
				|| op.SourceIndex < 0 || op.OutputIndex < 0
				|| op.Sources == null || op.Sources.Count == 0
				|| op.Sources.Count > MaxCarrySources
				|| op.Outputs == null || op.Outputs.Count == 0
				|| op.Outputs.Count > MaxCarryOutputs
				|| op.SourceIndex > op.Sources.Count || op.OutputIndex > op.Outputs.Count
				|| TooLong(op.Fault, MaxTextChars) || !CarryCountsValid(op)
				|| !LeaseShape(op.ScheduleLease, op.Id, Publication)
				|| op.ScheduleLease.Kind != KingdomLifecycleResourceKind.Schedule
				|| !string.Equals(op.ScheduleLease.SubjectId, op.DestinationSettlementId,
					StringComparison.Ordinal)
				|| op.ScheduleLease.After != op.DueTick
				|| !CarryScheduleReceiptShape(op, Publication)
				|| !CarryOutboxShape(op, Publication)) return false;

			HashSet<string> objects = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> events = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < op.Sources.Count; i++)
			{
				KingdomCarrySource source = op.Sources[i];
				if (!CarrySourceShape(source, op, i, Publication)
					|| !objects.Add(source.ObjectId) || !events.Add(source.SourceEventId)) return false;
			}
			if (op.SourceIndex != FirstIncompleteSource(op)) return false;
			HashSet<string> outputObjects = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> outputEvents = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> markers = new HashSet<string>(StringComparer.Ordinal);
			long[] output = new long[6];
			for (int i = 0; i < op.Outputs.Count; i++)
			{
				KingdomLifecycleProjection p = op.Outputs[i];
				KingdomLifecyclePhysicalState settled = op.LostOnRoad
					? KingdomLifecyclePhysicalState.Skipped : KingdomLifecyclePhysicalState.Proved;
				if (!CarryOutputShape(p, op.Id, i, Publication) || p.Material < 0
					|| !string.Equals(p.ZoneId, op.DestinationZoneId, StringComparison.Ordinal)
					|| objects.Contains(p.ObjectId)
					|| !outputObjects.Add(p.ObjectId) || !outputEvents.Add(p.EventId)
					|| !markers.Add(p.Marker) || !CheckedAccumulate(output, p.Material, p.Count))
					return false;
				if (!Publication && ((i < op.OutputIndex && p.State != settled)
					|| (i == op.OutputIndex && p.State != KingdomLifecyclePhysicalState.Prepared
						&& p.State != KingdomLifecyclePhysicalState.Intent && p.State != settled)
					|| (i > op.OutputIndex && p.State != KingdomLifecyclePhysicalState.Prepared)))
					return false;
			}
			for (int material = 0; material < 6; material++)
				if (output[material] != MaterialValue(op, material, 0)) return false;
			if (Publication)
			{
				if (op.SourceIndex != 0 || op.OutputIndex != 0) return false;
				for (int material = 0; material < 6; material++)
					if (MaterialValue(op, material, 1) != 0
						|| MaterialValue(op, material, 2) != 0
						|| MaterialValue(op, material, 3) != 0) return false;
			}
			return true;
		}

		private static bool LegacyCarryExtensionNeutral(KingdomCarryOperation op)
		{
			if (op == null || op.ManifestVersion != 0 || !string.IsNullOrEmpty(op.ManifestDigest)
				|| op.ManifestRevision != 0L || op.JobIds == null || op.JobIds.Count != 0
				|| op.TripIds == null || op.TripIds.Count != 0
				|| !string.IsNullOrEmpty(op.SignObjectId) || !string.IsNullOrEmpty(op.SignBlueprint)
				|| op.SignTopology != KingdomLifecycleTopology.None
				|| !string.IsNullOrEmpty(op.SignOwnerId) || !string.IsNullOrEmpty(op.SignZoneId)
				|| op.SignX != -1 || op.SignY != -1 || op.SignCount != 0
				|| !string.IsNullOrEmpty(op.SignReceiptId) || op.SignReceiptBeforeMatches != -1
				|| op.SignReceiptAfterMatches != -1 || op.SignReceiptBeforeCount != -1
				|| op.SignReceiptAfterCount != -1
				|| op.SignReceiptSameReference || !string.IsNullOrEmpty(op.SignReceiptProofId)
				|| op.SignReceiptState != KingdomLifecyclePhysicalState.None
				|| op.DestinationSafetyWaiting || op.DestinationSafetyWaitTick != 0L
				|| !string.IsNullOrEmpty(op.SpillZoneId) || op.SpillX != -1 || op.SpillY != -1)
				return false;
			for (int i = 0; op.Sources != null && i < op.Sources.Count; i++)
			{
				KingdomCarrySource source = op.Sources[i];
				if (source == null || source.LoadedCount != 0 || source.DeliveredCount != 0
					|| source.LostCount != 0 || source.CurrentTripId != 0
					|| source.CurrentTopology != KingdomLifecycleTopology.None
					|| !string.IsNullOrEmpty(source.CurrentOwnerId)
					|| !string.IsNullOrEmpty(source.CurrentZoneId)
					|| source.CurrentX != -1 || source.CurrentY != -1
					|| source.PendingTransfer != KingdomCarryTransferKind.None
					|| source.PendingTopology != KingdomLifecycleTopology.None
					|| !string.IsNullOrEmpty(source.PendingOwnerId)
					|| !string.IsNullOrEmpty(source.PendingZoneId)
					|| source.PendingX != -1 || source.PendingY != -1) return false;
			}
			return true;
		}

		private static bool ExactCarryPlanShape(KingdomCarryOperation op, bool publication)
		{
			if (op == null || op.Sequence <= 0L || !ValidGeneratedId(op.Id)
				|| op.CreatedTick < 0L || op.UpdatedTick < op.CreatedTick
				|| !FrozenSettlementSetValid(op.SettlementIds)
				|| !ValidHashNamespace(op.RealmTopologyHash, "carry-realm-topology")
				|| !ValidRootId(op.OriginSettlementId)
				|| op.SettlementIds.BinarySearch(op.OriginSettlementId,
					StringComparer.Ordinal) < 0
				|| !ValidName(op.OriginZoneId) || op.OriginX < 0 || op.OriginX > MaxCoordinate
				|| op.OriginY < 0 || op.OriginY > MaxCoordinate
				|| !ValidRootId(op.DestinationSettlementId)
				|| op.SettlementIds.BinarySearch(op.DestinationSettlementId,
					StringComparer.Ordinal) < 0
				|| !ValidName(op.DestinationSettlementName)
				|| !TopologyValid(op.DestinationTopology, op.DestinationOwnerId,
					op.DestinationZoneId, op.DestinationX, op.DestinationY)
				|| !ValidName(op.SpillZoneId) || op.SpillX < 0 || op.SpillX > MaxCoordinate
				|| op.SpillY < 0 || op.SpillY > MaxCoordinate
				|| !string.Equals(op.SpillZoneId, op.DestinationZoneId, StringComparison.Ordinal)
				|| op.DueTick < 0L || !op.RiskFrozen || op.SourceIndex < 0 || op.OutputIndex < 0
				|| op.Sources == null || op.Sources.Count == 0
				|| op.Sources.Count > MaxCarrySources || op.Outputs == null
				|| op.Outputs.Count != op.Sources.Count || op.Outputs.Count > MaxCarryOutputs
				|| op.SourceIndex > op.Sources.Count || op.OutputIndex > op.Outputs.Count
				|| TooLong(op.Fault, MaxTextChars) || !CarryCountsValid(op)
				|| op.ManifestVersion != CurrentCarryManifestVersion
				|| !ValidHashNamespace(op.ManifestDigest, "carry-manifest")
				|| op.ManifestRevision < 0L || !FrozenPositiveIdsValid(op.JobIds, MaxCarryJobIds)
				|| !FrozenPositiveIdsValid(op.TripIds, MaxCarryTripIds)
				|| !ExactCarrySignShape(op, publication)
				|| (op.DestinationSafetyWaiting
					? op.DestinationSafetyWaitTick < op.CreatedTick : op.DestinationSafetyWaitTick != 0L)
				|| !LeaseShape(op.ScheduleLease, op.Id, publication)
				|| op.ScheduleLease.Kind != KingdomLifecycleResourceKind.Schedule
				|| !string.Equals(op.ScheduleLease.SubjectId, op.DestinationSettlementId,
					StringComparison.Ordinal)
				|| op.ScheduleLease.After != op.DueTick
				|| !CarryScheduleReceiptShape(op, publication)
				|| !CarryOutboxShape(op, publication)) return false;
			string digest;
			if (!TryCarryManifestDigest(op, out digest)
				|| !string.Equals(op.ManifestDigest, digest, StringComparison.Ordinal)) return false;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < op.Sources.Count; i++)
			{
				KingdomCarrySource source = op.Sources[i];
				KingdomLifecycleProjection output = op.Outputs[i];
				if (!ExactManifestSourceShape(source, op, i, publication)
					|| !ids.Add(source.ObjectId)
					|| !ExactManifestOutputShape(output, source, op, i, publication)
					|| !ExactCarryTransferCoupling(source, output, op, i)) return false;
			}
			for (int material = 0; material < 6; material++)
				if (MaterialValue(op, material, 0) != 0
					|| MaterialValue(op, material, 1) != 0
					|| MaterialValue(op, material, 2) != 0
					|| MaterialValue(op, material, 3) != 0) return false;
			if (op.ManifestRevision != ExactCarryProvedRevision(op)) return false;
			if (publication && (op.SourceIndex != 0 || op.OutputIndex != 0
				|| op.ManifestRevision != 0L || op.DestinationSafetyWaiting)) return false;
			return ExactCarryConserved(op);
		}

	}
}
