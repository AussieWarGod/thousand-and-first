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
		/// <summary>Production raid shell adapter. Every method rechecks the open operation and
		/// commits against the two scalars owned by RaidLedger; callers cannot substitute standing.</summary>
		internal static partial class RaidRuntimeAdapter
		{
			internal static bool PrepareLeases(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation)
			{
				if (!CanOwnAuthority(book) || operation == null
					|| operation.Lane != KingdomLifecycleLane.Raid
					|| operation.Phase != KingdomLifecyclePhase.Prepared
					|| operation.ResourceLeases == null || HasDomainLease(operation)
					|| HasLease(operation, KingdomLifecycleResourceKind.Schedule)
					|| !KingdomRaidIncidentRules.CanPublish(book.RaidLedger, operation)
					|| book.RaidLedger.ScheduleRevision == long.MaxValue) return false;
				operation.DueBefore = book.RaidLedger.ScheduleRevision;
				operation.DueAfter = book.RaidLedger.ScheduleRevision + 1L;
				KingdomLifecycleResourceLease domain = PrepareLeaseCore(book, operation,
					KingdomLifecycleResourceKind.Raid, operation.SettlementId,
					operation.SettlementId, book.RaidLedger.StateRevision, 1L);
				string scheduleSubject = ScheduleSubjectId(operation.SettlementId,
					KingdomLifecycleLane.Raid);
				KingdomLifecycleResourceLease schedule = PrepareLeaseCore(book, operation,
					KingdomLifecycleResourceKind.Schedule, operation.SettlementId,
					scheduleSubject, operation.DueBefore, 1L);
				if (domain == null || schedule == null) return false;
				operation.ResourceLeases.Add(domain);
				operation.ResourceLeases.Add(schedule);
				return true;
			}

			internal static KingdomLifecycleProjection PrepareProjection(
				KingdomLifecycleBook book, KingdomLifecycleOperation operation, int ordinal,
				string objectId, string blueprint, string zoneId, int x, int y)
			{
				if (!CanOwnAuthority(book) || operation == null
					|| operation.Action != KingdomLifecycleAction.RaidAttack
					|| operation.Phase != KingdomLifecyclePhase.Prepared
					|| ordinal != operation.Projections.Count || ordinal < 0
					|| ordinal >= MaxProjections || !ValidRootId(objectId) || !ValidName(blueprint)
					|| !TopologyValid(KingdomLifecycleTopology.Cell, null, zoneId, x, y)) return null;
				KingdomLifecycleProjection projection = new KingdomLifecycleProjection
				{
					OperationId = operation.Id,
					EventId = ChildId(operation.Id, "projection", ordinal),
					ObjectId = objectId,
					Marker = ChildId(operation.Id, "marker", ordinal),
					Blueprint = blueprint, ZoneId = zoneId,
					Topology = KingdomLifecycleTopology.Cell, X = x, Y = y,
					Material = -1, Count = 1, NoStack = true,
					State = KingdomLifecyclePhysicalState.Prepared
				};
				string topology = TopologyId(projection.Topology, projection.OwnerId,
					projection.ZoneId, projection.X, projection.Y);
				KingdomLifecycleResourceLease lease = PrepareLeaseCore(book, operation,
					KingdomLifecycleResourceKind.Projection, topology, projection.ObjectId, 0L, 1L);
				if (lease == null) return null;
				operation.Projections.Add(projection);
				operation.ResourceLeases.Add(lease);
				return projection;
			}

			internal static KingdomLifecycleProjection PrepareInventoryProjection(
				KingdomLifecycleBook book, KingdomLifecycleOperation operation, int ordinal,
				string objectId, string blueprint, string ownerId, string zoneId)
			{
				if (!CanOwnAuthority(book) || operation == null
					|| operation.Action != KingdomLifecycleAction.RaidDeliverDemand
					|| operation.Phase != KingdomLifecyclePhase.Prepared
					|| ordinal != 0 || ordinal != operation.Projections.Count
					|| operation.PartySize != 0 || operation.Spawned != 0
					|| ordinal >= MaxProjections || !ValidRootId(objectId) || !ValidName(blueprint)
					|| !TopologyValid(KingdomLifecycleTopology.Inventory, ownerId, zoneId, -1, -1))
					return null;
				KingdomLifecycleProjection projection = new KingdomLifecycleProjection
				{
					OperationId = operation.Id, EventId = ChildId(operation.Id, "projection", ordinal),
					ObjectId = objectId, Marker = ChildId(operation.Id, "marker", ordinal),
					Blueprint = blueprint, OwnerId = ownerId, ZoneId = zoneId,
					Topology = KingdomLifecycleTopology.Inventory, X = -1, Y = -1,
					Material = -1, Count = 1, NoStack = true,
					State = KingdomLifecyclePhysicalState.Prepared
				};
				string topology = TopologyId(projection.Topology, projection.OwnerId,
					projection.ZoneId, projection.X, projection.Y);
				KingdomLifecycleResourceLease lease = PrepareLeaseCore(book, operation,
					KingdomLifecycleResourceKind.Projection, topology, projection.ObjectId, 0L, 1L);
				if (lease == null) return null;
				operation.Projections.Add(projection); operation.ResourceLeases.Add(lease);
				operation.PartySize = 1;
				return projection;
			}

			internal static bool PrepareCommittedTribute(KingdomLifecycleOperation operation,
				int requested, int spent, int outstanding, int physicalDeficit,
				bool measurementExact)
			{
				if (operation == null || operation.Action != KingdomLifecycleAction.RaidTribute
					|| operation.Phase != KingdomLifecyclePhase.Prepared || requested <= 0
					|| requested != spent || outstanding != 0 || physicalDeficit != requested
					|| !measurementExact || operation.WaterLegs == null
					|| operation.WaterLegs.Count != 0) return false;
				operation.ObjectMarker = ChildId(operation.Id, "raid-tribute-receipt", 0);
				operation.WaterRequested = requested;
				operation.WaterProved = requested;
				operation.WaterOutstanding = 0;
				operation.WaterLost = 0;
				operation.WaterAmbiguous = 0;
				operation.WaterState = KingdomLifecyclePhysicalState.Proved;
				return ExternalRaidTributeReceipt(operation);
			}

			internal static bool ProveDomain(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation)
			{
				if (!ExactOperationAuthority(book, operation)
					|| operation.Lane != KingdomLifecycleLane.Raid
					|| operation.Phase != KingdomLifecyclePhase.DomainIntent) return false;
				KingdomLifecycleResourceLease lease = RequiredDomainLease(operation);
				KingdomLifecycleResourceRevision row = lease == null ? null : FindResource(book, lease.Key);
				KingdomRaidLedger next;
				if (lease == null || row == null || lease.Kind != KingdomLifecycleResourceKind.Raid
					|| lease.Before != book.RaidLedger.StateRevision
					|| !KingdomRaidIncidentRules.TryApply(book.RaidLedger, operation, out next)
					|| next.StateRevision != lease.After
					|| !BeginLeaseCore(book, lease, book.RaidLedger.StateRevision)) return false;
				KingdomRaidLedger prior = book.RaidLedger;
				book.RaidLedger = next;
				if (CommitLeaseWitnessCore(book, operation, lease, row, next.StateRevision)) return true;
				book.RaidLedger = prior;
				lease.State = KingdomLifecycleLeaseState.Prepared;
				return false;
			}

			internal static bool ProveSchedule(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation)
			{
				if (!ExactOperationAuthority(book, operation)
					|| operation.Lane != KingdomLifecycleLane.Raid
					|| operation.Phase != KingdomLifecyclePhase.ScheduleIntent) return false;
				string subject = ScheduleSubjectId(operation.SettlementId, operation.Lane);
				KingdomLifecycleResourceLease lease = FindLease(operation, ResourceKey(
					KingdomLifecycleResourceKind.Schedule, operation.SettlementId, subject));
				KingdomLifecycleResourceRevision row = lease == null ? null : FindResource(book, lease.Key);
				if (lease == null || row == null || lease.Before != book.RaidLedger.ScheduleRevision
					|| lease.After != lease.Before + 1L
					|| !BeginLeaseCore(book, lease, book.RaidLedger.ScheduleRevision)) return false;
				KingdomRaidLedger prior = book.RaidLedger;
				KingdomRaidLedger next = KingdomRaidIncidentRules.Copy(prior);
				next.ScheduleRevision++;
				book.RaidLedger = next;
				if (CommitLeaseWitnessCore(book, operation, lease, row, next.ScheduleRevision)) return true;
				book.RaidLedger = prior;
				lease.State = KingdomLifecycleLeaseState.Prepared;
				return false;
			}

			internal static bool BeginProjection(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, KingdomLifecycleProjection projection,
				int idMatches, int markerMatches)
			{
				if (!ExactOperationAuthority(book, operation) || projection == null
					|| operation.Phase != KingdomLifecyclePhase.ProjectionIntent
					|| projection.State != KingdomLifecyclePhysicalState.Prepared
					|| idMatches != 0 || markerMatches != 0) return false;
				string topology = TopologyId(projection.Topology, projection.OwnerId,
					projection.ZoneId, projection.X, projection.Y);
				KingdomLifecycleResourceLease lease = FindLease(operation, ResourceKey(
					KingdomLifecycleResourceKind.Projection, topology, projection.ObjectId));
				if (lease == null || !ReferenceEquals(ProjectionForLease(operation, lease), projection)
					|| !BeginLeaseCore(book, lease, 0L)) return false;
				projection.State = KingdomLifecyclePhysicalState.Intent;
				return ExactOperationAuthority(book, operation);
			}

			internal static bool CommitProjection(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, KingdomLifecycleProjection projection,
				int idMatches, int markerMatches, string blueprint, string zoneId, int x, int y)
			{
				return CommitProjection(book, operation, projection, idMatches, markerMatches,
					blueprint, null, zoneId, x, y);
			}

			internal static bool CommitProjection(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, KingdomLifecycleProjection projection,
				int idMatches, int markerMatches, string blueprint, string ownerId,
				string zoneId, int x, int y)
			{
				if (!ExactOperationAuthority(book, operation) || projection == null
					|| operation.Phase != KingdomLifecyclePhase.ProjectionIntent
					|| projection.State != KingdomLifecyclePhysicalState.Intent
					|| idMatches != 1 || markerMatches != 1
					|| !string.Equals(blueprint, projection.Blueprint, StringComparison.Ordinal)
					|| !string.Equals(ownerId, projection.OwnerId, StringComparison.Ordinal)
					|| !string.Equals(zoneId, projection.ZoneId, StringComparison.Ordinal)
					|| x != projection.X || y != projection.Y) return false;
				string topology = TopologyId(projection.Topology, projection.OwnerId,
					projection.ZoneId, projection.X, projection.Y);
				KingdomLifecycleResourceLease lease = FindLease(operation, ResourceKey(
					KingdomLifecycleResourceKind.Projection, topology, projection.ObjectId));
				KingdomLifecycleResourceRevision row = lease == null ? null : FindResource(book, lease.Key);
				int spawned;
				if (lease == null || row == null || !CheckedAdd(operation.Spawned, projection.Count,
					out spawned) || !CommitLeaseWitnessCore(book, operation, lease, row, lease.After))
					return false;
				projection.State = KingdomLifecyclePhysicalState.Proved;
				operation.Spawned = spawned;
				return ExactOperationAuthority(book, operation);
			}

			/// <summary>An interrupted add with exact post-observation of zero bodies may be
			/// retried. No resource revision or world object was committed, so this returns only
			/// the projection lease and receipt to their prepared states.</summary>
			internal static bool ResetAbsentProjectionIntent(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, KingdomLifecycleProjection projection,
				int idMatches, int markerMatches)
			{
				if (!ExactOperationAuthority(book, operation) || projection == null
					|| (operation.Action != KingdomLifecycleAction.RaidAttack
						&& operation.Action != KingdomLifecycleAction.RaidDeliverDemand)
					|| operation.Phase != KingdomLifecyclePhase.ProjectionIntent
					|| projection.State != KingdomLifecyclePhysicalState.Intent
					|| idMatches != 0 || markerMatches != 0) return false;
				string topology = TopologyId(projection.Topology, projection.OwnerId,
					projection.ZoneId, projection.X, projection.Y);
				KingdomLifecycleResourceLease lease = FindLease(operation, ResourceKey(
					KingdomLifecycleResourceKind.Projection, topology, projection.ObjectId));
				KingdomLifecycleResourceRevision row = lease == null ? null : FindResource(book, lease.Key);
				if (lease == null || row == null || lease.State != KingdomLifecycleLeaseState.Intent
					|| row.Revision != lease.BeforeRevision
					|| !string.Equals(row.ActiveOperationId, operation.Id, StringComparison.Ordinal)
					|| string.Equals(row.LastOperationId, operation.Id, StringComparison.Ordinal)) return false;
				lease.State = KingdomLifecycleLeaseState.Prepared;
				projection.State = KingdomLifecyclePhysicalState.Prepared;
				return ExactOperationAuthority(book, operation);
			}

			internal static bool BeginEffect(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, bool exactContact)
			{
				if (!ExactOperationAuthority(book, operation)
					|| operation.Action != KingdomLifecycleAction.RaidAttack
					|| operation.Phase != KingdomLifecyclePhase.EffectIntent
					|| operation.EffectState != KingdomLifecyclePhysicalState.Prepared
					|| !exactContact) return false;
				operation.EffectState = KingdomLifecyclePhysicalState.Intent;
				return ExactOperationAuthority(book, operation);
			}

			internal static bool CommitEffect(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, bool exactContact, int plunderProved)
			{
				if (!ExactOperationAuthority(book, operation) || !exactContact
					|| operation.Action != KingdomLifecycleAction.RaidAttack
					|| operation.Phase != KingdomLifecyclePhase.EffectIntent
					|| operation.EffectState != KingdomLifecyclePhysicalState.Intent
					|| plunderProved < 0 || plunderProved > operation.PlunderRequested) return false;
				operation.PlunderProved = plunderProved;
				operation.EffectState = KingdomLifecyclePhysicalState.Proved;
				return ExactOperationAuthority(book, operation);
			}

			internal static bool SkipEffectWithoutContact(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation)
			{
				if (!ExactOperationAuthority(book, operation)
					|| operation.Action != KingdomLifecycleAction.RaidAttack
					|| operation.Phase != KingdomLifecyclePhase.EffectIntent
					|| operation.EffectState != KingdomLifecyclePhysicalState.Prepared
					|| operation.PlunderProved != 0) return false;
				operation.EffectState = KingdomLifecyclePhysicalState.Skipped;
				return ExactOperationAuthority(book, operation);
			}

		}
	}
}
