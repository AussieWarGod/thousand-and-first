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
		/// <summary>Plan/recovery seam for plain and notable guest production shells. Physical
		/// mutations still require <see cref="TrustedAdapter"/> observations.</summary>
		internal static partial class GuestRuntimeAdapter
		{
			internal static bool PrepareSchedule(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, string zoneId, long before, long after)
			{
				long delta;
				if (!CanOwnAuthority(book) || operation == null
					|| operation.Phase != KingdomLifecyclePhase.Prepared
					|| !ActionAllowedInLane(operation.Action, operation.Lane)
					|| (operation.Lane != KingdomLifecycleLane.PlainGuest
						&& operation.Lane != KingdomLifecycleLane.NotableGuest)
					|| string.IsNullOrEmpty(zoneId) || before < 0L || after < 0L
					|| !CheckedAdd(after, -before, out delta) || delta == 0L) return false;
				string subject = ScheduleSubjectId(book.SettlementId, operation.Lane);
				KingdomLifecycleResourceLease lease = PrepareLeaseCore(book, operation,
					KingdomLifecycleResourceKind.Schedule, book.SettlementId, subject,
					before, delta);
				if (lease == null) return false;
				operation.ZoneId = zoneId;
				operation.DueBefore = before;
				operation.DueAfter = after;
				operation.ResourceLeases.Add(lease);
				return true;
			}

			internal static KingdomLifecycleProjection PrepareProjection(
				KingdomLifecycleBook book, KingdomLifecycleOperation operation,
				string objectId, string blueprint, string zoneId, int x, int y)
			{
				if (!CanOwnAuthority(book) || operation == null
					|| operation.Action != KingdomLifecycleAction.Spawn
					|| operation.Phase != KingdomLifecyclePhase.Prepared
					|| operation.Projections.Count != 0 || !ValidRootId(objectId)
					|| !ValidName(blueprint)
					|| !TopologyValid(KingdomLifecycleTopology.Cell, null, zoneId, x, y))
					return null;
				KingdomLifecycleProjection projection = new KingdomLifecycleProjection
				{
					OperationId = operation.Id,
					EventId = ChildId(operation.Id, "projection", 0),
					ObjectId = objectId,
					Marker = ChildId(operation.Id, "marker", 0),
					Blueprint = blueprint,
					ZoneId = zoneId,
					Topology = KingdomLifecycleTopology.Cell,
					X = x,
					Y = y,
					Material = -1,
					Count = 1,
					NoStack = true,
					State = KingdomLifecyclePhysicalState.Prepared
				};
				string topology = TopologyId(projection.Topology, projection.OwnerId,
					projection.ZoneId, projection.X, projection.Y);
				KingdomLifecycleResourceLease lease = PrepareLeaseCore(book, operation,
					KingdomLifecycleResourceKind.Projection, topology, objectId, 0L, 1L);
				if (lease == null) return null;
				operation.PartySize = 1;
				operation.Projections.Add(projection);
				operation.ResourceLeases.Add(lease);
				return projection;
			}

			internal static bool PrepareRemoval(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, string objectId, string blueprint,
				string zoneId, int x, int y)
			{
				if (!CanOwnAuthority(book) || operation == null
					|| (operation.Action != KingdomLifecycleAction.Depart
						&& operation.Action != KingdomLifecycleAction.OfferWater)
					|| operation.Phase != KingdomLifecyclePhase.Prepared
					|| !ValidRootId(objectId) || !ValidName(blueprint)
					|| !TopologyValid(KingdomLifecycleTopology.Cell, null, zoneId, x, y))
					return false;
				string topology = TopologyId(KingdomLifecycleTopology.Cell, null, zoneId, x, y);
				KingdomLifecycleResourceLease lease = PrepareLeaseCore(book, operation,
					KingdomLifecycleResourceKind.Object, topology, objectId, 1L, -1L);
				if (lease == null) return false;
				operation.ObjectId = objectId;
				operation.Blueprint = blueprint;
				operation.ZoneId = zoneId;
				operation.ObjectTopology = KingdomLifecycleTopology.Cell;
				operation.ObjectX = x;
				operation.ObjectY = y;
				operation.Count = 1;
				operation.RemovalState = KingdomLifecyclePhysicalState.Prepared;
				operation.ResourceLeases.Add(lease);
				return true;
			}

			internal static KingdomLifecycleWaterLeg PrepareWater(
				KingdomLifecycleBook book, KingdomLifecycleOperation operation, int ordinal,
				string ownerId, string blueprint, string zoneId, int capacity, int before,
				int amount, string composition)
			{
				if (!CanOwnAuthority(book) || operation == null
					|| (operation.Action != KingdomLifecycleAction.OfferWater
						&& operation.Action != KingdomLifecycleAction.Lodge)
					|| ordinal != operation.WaterLegs.Count || ordinal < 0
					|| ordinal >= MaxWaterLegs || !ValidRootId(ownerId)
					|| !ValidName(blueprint) || string.IsNullOrEmpty(zoneId)
					|| capacity < 0 || before <= 0 || amount <= 0 || amount > before
					|| TooLong(composition, MaxTextChars)) return null;
				KingdomLifecycleResourceLease lease = PrepareLeaseCore(book, operation,
					KingdomLifecycleResourceKind.WaterVessel, zoneId, ownerId, before, -amount);
				if (lease == null) return null;
				KingdomLifecycleWaterLeg leg = new KingdomLifecycleWaterLeg
				{
					OperationId = operation.Id, LeaseKey = lease.Key, OwnerId = ownerId,
					Blueprint = blueprint, ZoneId = zoneId, Capacity = capacity,
					Before = before, Delta = amount, After = before - amount,
					Composition = composition,
					ReceiptId = ChildId(operation.Id, "water-receipt", ordinal),
					ReceiptState = KingdomLifecyclePhysicalState.Prepared,
					State = KingdomLifecyclePhysicalState.Prepared
				};
				operation.ResourceLeases.Add(lease);
				operation.WaterLegs.Add(leg);
				operation.WaterRequested += amount;
				operation.WaterOutstanding += amount;
				operation.WaterState = KingdomLifecyclePhysicalState.Prepared;
				return leg;
			}

			internal static bool PrepareDomain(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, long before)
			{
				KingdomLifecycleResourceKind kind;
				long delta;
				if (!CanOwnAuthority(book) || operation == null || before < 0L
					|| !TryRequiredDomain(operation, out kind, out delta)) return false;
				KingdomLifecycleResourceLease lease = PrepareLeaseCore(book, operation, kind,
					operation.SettlementId, operation.SettlementId, before, delta);
				if (lease == null || lease.After < 0L) return false;
				operation.ResourceLeases.Add(lease);
				return true;
			}

			internal static bool ProveDomain(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, long before, long after)
			{
				if (!ExactOperationAuthority(book, operation)
					|| operation.Phase != KingdomLifecyclePhase.DomainIntent) return false;
				KingdomLifecycleResourceLease lease = RequiredDomainLease(operation);
				if (lease == null || before != lease.Before || after != lease.After
					|| !BeginLeaseCore(book, lease, before)) return false;
				return CommitLeaseWitnessCore(book, operation, lease,
					FindResource(book, lease.Key), after);
			}

			/// <summary>Closes a guest conceptual lease only from the exact physical receipts
			/// already proved by the preceding phase. Callers cannot mint the domain witness from
			/// an unobserved population/standing scalar.</summary>
			internal static bool ProvePhysicalDomain(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation)
			{
				if (!ExactOperationAuthority(book, operation)
					|| operation.Phase != KingdomLifecyclePhase.DomainIntent
					|| (operation.Lane != KingdomLifecycleLane.PlainGuest
						&& operation.Lane != KingdomLifecycleLane.NotableGuest)) return false;
				bool physical;
				switch (operation.Action)
				{
				case KingdomLifecycleAction.Spawn:
					physical = operation.Projections.Count == 1
						&& operation.Projections[0].State == KingdomLifecyclePhysicalState.Proved
						&& operation.Spawned == operation.PartySize && operation.PartySize == 1;
					break;
				case KingdomLifecycleAction.Depart:
					physical = operation.RemovalState == KingdomLifecyclePhysicalState.Proved;
					break;
				case KingdomLifecycleAction.OfferWater:
					physical = operation.RemovalState == KingdomLifecyclePhysicalState.Proved
						&& operation.WaterState == KingdomLifecyclePhysicalState.Proved
						&& operation.WaterOutstanding == 0
						&& operation.WaterProved == operation.WaterRequested;
					break;
				default:
					return false;
				}
				if (!physical) return false;
				KingdomLifecycleResourceLease lease = RequiredDomainLease(operation);
				if (lease == null) return false;
				if (lease.State == KingdomLifecycleLeaseState.Intent)
					return RecoverDomainIntent(book, operation, lease.After);
				return BeginLeaseCore(book, lease, lease.Before)
					&& CommitLeaseWitnessCore(book, operation, lease,
						FindResource(book, lease.Key), lease.After);
			}

			internal static bool BeginDomain(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, long before)
			{
				KingdomLifecycleResourceLease lease = RequiredDomainLease(operation);
				return ExactOperationAuthority(book, operation)
					&& operation.Phase == KingdomLifecyclePhase.DomainIntent && lease != null
					&& before == lease.Before && BeginLeaseCore(book, lease, before);
			}

			internal static bool CommitDomain(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, long after)
			{
				KingdomLifecycleResourceLease lease = RequiredDomainLease(operation);
				return ExactOperationAuthority(book, operation)
					&& operation.Phase == KingdomLifecyclePhase.DomainIntent && lease != null
					&& after == lease.After && CommitLeaseWitnessCore(book, operation, lease,
						FindResource(book, lease.Key), after);
			}

			internal static bool RecoverDomainIntent(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, long after)
			{
				KingdomLifecycleResourceLease lease = RequiredDomainLease(operation);
				return ExactOperationAuthority(book, operation)
					&& operation.Phase == KingdomLifecyclePhase.DomainIntent && lease != null
					&& lease.State == KingdomLifecycleLeaseState.Intent && after == lease.After
					&& CommitLeaseWitnessCore(book, operation, lease,
						FindResource(book, lease.Key), after);
			}

			internal static bool ResetDomainIntent(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, long before)
			{
				KingdomLifecycleResourceLease lease = RequiredDomainLease(operation);
				KingdomLifecycleResourceRevision row = lease == null ? null : FindResource(book, lease.Key);
				if (!ExactOperationAuthority(book, operation)
					|| operation.Phase != KingdomLifecyclePhase.DomainIntent || lease == null
					|| lease.State != KingdomLifecycleLeaseState.Intent || before != lease.Before
					|| row == null || row.Revision != lease.BeforeRevision
					|| string.Equals(row.LastOperationId, operation.Id, StringComparison.Ordinal)) return false;
				lease.State = KingdomLifecycleLeaseState.Prepared;
				return ExactOperationAuthority(book, operation);
			}

			internal static bool RecoverScheduleIntent(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, long after)
			{
				string subject = ScheduleSubjectId(operation.SettlementId, operation.Lane);
				KingdomLifecycleResourceLease lease = FindLease(operation, ResourceKey(
					KingdomLifecycleResourceKind.Schedule, operation.SettlementId, subject));
				return ExactOperationAuthority(book, operation)
					&& operation.Phase == KingdomLifecyclePhase.ScheduleIntent && lease != null
					&& lease.State == KingdomLifecycleLeaseState.Intent && after == lease.After
					&& CommitLeaseWitnessCore(book, operation, lease,
						FindResource(book, lease.Key), after);
			}

			internal static bool RecoverRemovalIntent(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, bool exactAbsent)
			{
				if (!ExactOperationAuthority(book, operation) || !exactAbsent
					|| operation.Phase != KingdomLifecyclePhase.RemovalIntent
					|| operation.RemovalState != KingdomLifecyclePhysicalState.Intent) return false;
				string topology = TopologyId(operation.ObjectTopology, operation.ObjectOwnerId,
					operation.ZoneId, operation.ObjectX, operation.ObjectY);
				KingdomLifecycleResourceLease lease = FindLease(operation, ResourceKey(
					KingdomLifecycleResourceKind.Object, topology, operation.ObjectId));
				if (lease == null || lease.State != KingdomLifecycleLeaseState.Intent
					|| !CommitLeaseWitnessCore(book, operation, lease,
						FindResource(book, lease.Key), lease.After)) return false;
				operation.RemovalState = KingdomLifecyclePhysicalState.Proved;
				return ExactOperationAuthority(book, operation);
			}

		}
	}
}
