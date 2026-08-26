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
			internal static bool ProveLifecycleProjection(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, KingdomLifecycleProjection projection,
				IKingdomLifecycleTrustedWorld world)
			{
				if (!ExactOperationAuthority(book, operation) || projection == null
					|| operation.Phase != KingdomLifecyclePhase.ProjectionIntent
					|| projection.State != KingdomLifecyclePhysicalState.Prepared) return false;
				string topology = TopologyId(projection.Topology, projection.OwnerId,
					projection.ZoneId, projection.X, projection.Y);
				KingdomLifecycleResourceLease lease = FindLease(operation, ResourceKey(
					KingdomLifecycleResourceKind.Projection, topology, projection.ObjectId));
				if (lease == null || !ReferenceEquals(ProjectionForLease(operation, lease), projection))
					return false;
				int beforeIds;
				int beforeMarkers;
				ScanOutput(world, projection, out beforeIds, out beforeMarkers);
				if (beforeIds != 0 || beforeMarkers != 0
					|| !BeginLeaseCore(book, lease, lease.Before)) return false;
				projection.State = KingdomLifecyclePhysicalState.Intent;
				object returned;
				try { returned = world.InvokeLifecycleProjection(projection); }
				catch (Exception) { return false; }
				if (returned == null) return false;
				int afterIds;
				int afterMarkers;
				Snapshot after = ScanOutput(world, projection, out afterIds, out afterMarkers);
				if (afterIds != 1 || afterMarkers != 1 || after == null
					|| !ReferenceEquals(after.Reference, returned)
					|| !string.Equals(after.Marker, projection.Marker, StringComparison.Ordinal)
					|| !string.Equals(after.Blueprint, projection.Blueprint, StringComparison.Ordinal)
					|| after.Count != projection.Count || !ExactTopology(after,
						projection.Topology, projection.OwnerId, projection.ZoneId,
						projection.X, projection.Y)) return false;
				int spawned;
				if (!CheckedAdd(operation.Spawned, projection.Count, out spawned)
					|| !ValidCount(spawned)) return false;
				KingdomLifecycleResourceRevision row = FindResource(book, lease.Key);
				if (!CommitLeaseWitnessCore(book, operation, lease, row, lease.After)) return false;
				projection.State = KingdomLifecyclePhysicalState.Proved;
				projection.LiveAuthority = returned;
				operation.Spawned = spawned;
				return ExactOperationAuthority(book, operation);
			}

			internal static bool ProveLifecycleRemoval(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, IKingdomLifecycleTrustedWorld world)
			{
				if (!ExactOperationAuthority(book, operation)
					|| operation.Phase != KingdomLifecyclePhase.RemovalIntent
					|| operation.RemovalState != KingdomLifecyclePhysicalState.Prepared
					|| !ValidName(operation.Blueprint)) return false;
				string topology = TopologyId(operation.ObjectTopology, operation.ObjectOwnerId,
					operation.ZoneId, operation.ObjectX, operation.ObjectY);
				KingdomLifecycleResourceLease lease = FindLease(operation, ResourceKey(
					KingdomLifecycleResourceKind.Object, topology, operation.ObjectId));
				int beforeMatches;
				Snapshot before = ExactObservation(world, delegate(Snapshot value)
				{
					return string.Equals(value.ObjectId, operation.ObjectId, StringComparison.Ordinal);
				}, out beforeMatches);
				if (lease == null || beforeMatches != 1 || before == null
					|| !ExactLifecycleObjectFields(before, operation, operation.Count)
					|| !BeginLeaseCore(book, lease, lease.Before)) return false;
				operation.RemovalState = KingdomLifecyclePhysicalState.Intent;
				operation.LiveAuthority = before.Reference;
				object returned;
				try
				{
					returned = world.InvokeLifecycleRemoval(before.Reference,
						operation.Count, operation.Id);
				}
				catch (Exception) { return false; }
				if (returned == null) return false;
				int afterMatches;
				Snapshot after = ExactObservation(world, delegate(Snapshot value)
				{
					return string.Equals(value.ObjectId, operation.ObjectId, StringComparison.Ordinal);
				}, out afterMatches);
				if (afterMatches != 1 || after == null
					|| !ReferenceEquals(before.Reference, returned)
					|| !ReferenceEquals(after.Reference, returned)
					|| !ExactLifecycleObjectFields(after, operation, 0)) return false;
				KingdomLifecycleResourceRevision row = FindResource(book, lease.Key);
				if (!CommitLeaseWitnessCore(book, operation, lease, row, lease.After)) return false;
				operation.RemovalState = KingdomLifecyclePhysicalState.Proved;
				return ExactOperationAuthority(book, operation);
			}

			internal static bool ProveLifecycleSchedule(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, IKingdomLifecycleTrustedWorld world)
			{
				if (!ExactOperationAuthority(book, operation)
					|| operation.Phase != KingdomLifecyclePhase.ScheduleIntent) return false;
				string subject = ScheduleSubjectId(operation.SettlementId, operation.Lane);
				KingdomLifecycleResourceLease lease = FindLease(operation, ResourceKey(
					KingdomLifecycleResourceKind.Schedule, operation.SettlementId, subject));
				KingdomLifecycleResourceRevision row = lease == null ? null : FindResource(book, lease.Key);
				int beforeMatches;
				Snapshot before = ExactObservation(world, delegate(Snapshot value)
				{
					return string.Equals(value.ObjectId, lease == null ? null : lease.Key,
						StringComparison.Ordinal);
				}, out beforeMatches);
				if (lease == null || row == null || beforeMatches != 1 || before == null
					|| !ExactLifecycleScheduleFields(before, operation, lease.Before,
						lease.BeforeRevision, row.LastOperationId)
					|| !BeginLeaseCore(book, lease, before.Value)) return false;
				object returned;
				try { returned = world.InvokeSchedule(before.Reference, lease.After, operation.Id); }
				catch (Exception) { return false; }
				if (returned == null) return false;
				int afterMatches;
				Snapshot after = ExactObservation(world, delegate(Snapshot value)
				{
					return string.Equals(value.ObjectId, lease.Key, StringComparison.Ordinal);
				}, out afterMatches);
				if (afterMatches != 1 || after == null
					|| !ReferenceEquals(before.Reference, returned)
					|| !ReferenceEquals(after.Reference, returned)
					|| !ExactLifecycleScheduleFields(after, operation, lease.After,
						lease.AfterRevision, operation.Id)
					|| before.Topology != after.Topology || before.X != after.X || before.Y != after.Y
					|| !string.Equals(before.OwnerId, after.OwnerId, StringComparison.Ordinal)) return false;
				return CommitLeaseWitnessCore(book, operation, lease, row, after.Value);
			}

			internal static bool PrepareCarrySchedule(KingdomCarryBook book,
				KingdomCarryOperation operation, IKingdomLifecycleTrustedWorld world)
			{
				if (!CanOwnAuthority(book) || operation == null
					|| operation.Phase != KingdomLifecyclePhase.Prepared
					|| !ExactStringList(operation.SettlementIds, book.SettlementIds)
					|| !string.Equals(operation.RealmTopologyHash,
						RealmTopologyDigest(book.RealmId, book.SettlementIds), StringComparison.Ordinal)
					|| !SettlementMember(book, operation.DestinationSettlementId)) return false;
				string key = ResourceKey(KingdomLifecycleResourceKind.Schedule,
					book.RealmId, operation.DestinationSettlementId);
				int matches;
				Snapshot before = ExactObservation(world,
					delegate(Snapshot x)
					{
						return string.Equals(x.ObjectId, key, StringComparison.Ordinal);
					}, out matches);
				KingdomLifecycleResourceRevision row = FindResource(book, key);
				long revision = row == null ? 0L : row.Revision;
				if (matches != 1 || before == null || before.Reference == null
					|| !string.Equals(before.Blueprint, ScheduleBlueprint, StringComparison.Ordinal)
					|| !string.Equals(before.SettlementId, operation.DestinationSettlementId,
						StringComparison.Ordinal)
					|| before.Value < 0L || before.Revision != revision
					|| !string.Equals(before.LastOperationId,
						row == null ? null : row.LastOperationId, StringComparison.Ordinal)
					|| !TopologyValid(before.Topology, before.OwnerId, before.ZoneId,
						before.X, before.Y)) return false;
				KingdomLifecycleResourceLease lease = PrepareCarryScheduleLeaseCore(book,
					operation, before.Value);
				if (lease == null) return false;
				operation.DestinationZoneId = before.ZoneId;
				operation.DestinationTopology = before.Topology;
				operation.DestinationOwnerId = before.OwnerId;
				operation.DestinationX = before.X;
				operation.DestinationY = before.Y;
				operation.ScheduleLease = lease;
				operation.ScheduleReceiptId = ChildId(operation.Id, "schedule-receipt", 0);
				operation.ScheduleTopologyId = TopologyId(before.Topology, before.OwnerId,
					before.ZoneId, before.X, before.Y);
				operation.ScheduleReceiptState = KingdomLifecyclePhysicalState.Prepared;
				operation.LiveAuthority = before.Reference;
				return CarryScheduleReceiptShape(operation, true);
			}

			internal static bool ProveCarrySchedule(KingdomCarryBook book,
				KingdomCarryOperation operation, IKingdomLifecycleTrustedWorld world)
			{
				if (!ExactCarryAuthority(book, operation) || operation.ScheduleLease == null
					|| operation.Phase != KingdomLifecyclePhase.ScheduleIntent
					|| (operation.ScheduleReceiptState != KingdomLifecyclePhysicalState.Prepared
						&& operation.ScheduleReceiptState != KingdomLifecyclePhysicalState.Intent))
					return false;
				KingdomLifecycleResourceLease lease = operation.ScheduleLease;
				KingdomLifecycleResourceRevision resource = FindResource(book, lease.Key);
				if (operation.ScheduleReceiptState == KingdomLifecyclePhysicalState.Intent)
				{
					int recoveredMatches;
					Snapshot recovered = ExactScheduleObservation(world, operation, lease.After,
						lease.AfterRevision, operation.Id, out recoveredMatches);
					if (recoveredMatches == 1 && recovered != null && recovered.Reference != null
						&& ExactScheduleFields(recovered, operation, lease.After,
							lease.AfterRevision, operation.Id))
					{
						if (!CommitCarryScheduleCore(book, operation, lease, recovered.Value))
							return false;
						operation.ScheduleAfterMatches = 1;
						operation.ScheduleSameReference = true;
						operation.ScheduleProofId = CarryScheduleReceiptProof(operation);
						operation.ScheduleReceiptState = KingdomLifecyclePhysicalState.Proved;
						return CarryScheduleReceiptShape(operation, false);
					}
					int unchangedMatches;
					Snapshot unchanged = ExactScheduleObservation(world, operation, lease.Before,
						lease.BeforeRevision, resource == null ? null : resource.LastOperationId,
						out unchangedMatches);
					if (unchangedMatches != 1 || unchanged == null || unchanged.Reference == null
						|| !ExactScheduleFields(unchanged, operation, lease.Before,
							lease.BeforeRevision, resource == null ? null : resource.LastOperationId))
						return false;
					// The callback had no externally visible effect. Returning to Prepared is safe:
					// no book revision or last-operation witness was committed.
					lease.State = KingdomLifecycleLeaseState.Prepared;
					operation.ScheduleBeforeMatches = -1;
					operation.ScheduleReceiptState = KingdomLifecyclePhysicalState.Prepared;
				}
				int beforeMatches;
				Snapshot before = ExactScheduleObservation(world,
					operation, lease.Before, lease.BeforeRevision,
					resource == null ? null : resource.LastOperationId, out beforeMatches);
				if (beforeMatches != 1 || before == null || before.Reference == null
					|| !ExactScheduleFields(before, operation, lease.Before,
						lease.BeforeRevision, resource == null ? null : resource.LastOperationId)
					|| !BeginCarryScheduleCore(book, operation, lease, before.Value)) return false;
				operation.ScheduleBeforeMatches = 1;
				operation.ScheduleReceiptState = KingdomLifecyclePhysicalState.Intent;
				object returned;
				try { returned = world.InvokeSchedule(before.Reference, lease.After, operation.Id); }
				catch (Exception) { return false; }
				if (returned == null) return false;
				int afterMatches;
				Snapshot after = ExactScheduleObservation(world,
					operation, lease.After, lease.AfterRevision, operation.Id, out afterMatches);
				CallbackReceipt receipt = CallbackReceipt.Create(before, after, returned);
				if (afterMatches != 1 || receipt.After == null
					|| !ExactScheduleFields(receipt.After, operation, lease.After,
						lease.AfterRevision, operation.Id)
					|| !ReferenceEquals(receipt.Before.Reference, receipt.Returned)
					|| !ReferenceEquals(receipt.After.Reference, receipt.Returned)) return false;
				if (!CommitCarryScheduleCore(book, operation, lease, after.Value)) return false;
				operation.ScheduleAfterMatches = 1;
				operation.ScheduleSameReference = true;
				operation.ScheduleProofId = CarryScheduleReceiptProof(operation);
				operation.ScheduleReceiptState = KingdomLifecyclePhysicalState.Proved;
				return CarryScheduleReceiptShape(operation, false);
			}

		}
	}
}
