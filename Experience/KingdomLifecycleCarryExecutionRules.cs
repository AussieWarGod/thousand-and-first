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
		private static KingdomLifecycleResourceLease PrepareCarryScheduleLeaseCore(
			KingdomCarryBook Book, KingdomCarryOperation Operation, long Before)
		{
			if (!CanOwnAuthority(Book) || Operation == null
				|| Operation.Phase != KingdomLifecyclePhase.Prepared
				|| !string.Equals(Operation.Id, CarryId(Book.RealmId, Operation.Sequence),
					StringComparison.Ordinal)
				|| !ExactStringList(Operation.SettlementIds, Book.SettlementIds)
				|| !string.Equals(Operation.RealmTopologyHash,
					RealmTopologyDigest(Book.RealmId, Book.SettlementIds), StringComparison.Ordinal)
				|| !SettlementMember(Book, Operation.OriginSettlementId)
				|| !SettlementMember(Book, Operation.DestinationSettlementId)
				|| Operation.DueTick < 0L || Before < 0L) return null;
			long delta;
			if (!CheckedAdd(Operation.DueTick, -Before, out delta) || delta == 0L) return null;
			string key = ResourceKey(KingdomLifecycleResourceKind.Schedule,
				Book.RealmId, Operation.DestinationSettlementId);
			KingdomLifecycleResourceRevision row = FindResource(Book, key);
			if (key == null || (row != null && !string.IsNullOrEmpty(row.ActiveOperationId))) return null;
			long revision = row == null ? 0L : row.Revision;
			if (revision < 0L || revision == long.MaxValue) return null;
			return new KingdomLifecycleResourceLease
			{
				OperationId = Operation.Id,
				Kind = KingdomLifecycleResourceKind.Schedule,
				ScopeId = Book.RealmId,
				SubjectId = Operation.DestinationSettlementId,
				Key = key,
				Before = Before,
				Delta = delta,
				After = Operation.DueTick,
				BeforeRevision = revision,
				AfterRevision = revision + 1L,
				State = KingdomLifecycleLeaseState.Prepared
			};
		}

		public static KingdomCarrySource PrepareCarrySource(KingdomCarryOperation Operation,
			int SourceOrdinal, string ObjectId, string Blueprint,
			KingdomLifecycleTopology Topology, string OwnerId, string ZoneId,
			int X, int Y, int Material, int OriginalCount, int PlannedCount)
		{
			if (Operation == null || SourceOrdinal < 0 || SourceOrdinal >= MaxCarrySources
				|| !ValidRootId(ObjectId) || !ValidName(Blueprint)
				|| !TopologyValid(Topology, OwnerId, ZoneId, X, Y)
				|| Material < 0 || Material >= 6 || OriginalCount <= 0
				|| OriginalCount > MaxPhysicalCount || PlannedCount <= 0
				|| PlannedCount > OriginalCount) return null;
			return new KingdomCarrySource
			{
				OperationId = Operation.Id,
				SourceEventId = ChildId(Operation.Id, "source", SourceOrdinal),
				ObjectId = ObjectId,
				Blueprint = Blueprint,
				Topology = Topology,
				OwnerId = OwnerId,
				ZoneId = ZoneId,
				X = X,
				Y = Y,
				Material = Material,
				OriginalCount = OriginalCount,
				PlannedCount = PlannedCount,
				Removed = 0,
				UnitCursor = 0,
				UnitBefore = OriginalCount,
					UnitAfter = OriginalCount - 1,
					UnitEventId = ChildId(Operation.Id, "source-unit-" + SourceOrdinal, 0),
					UnitState = KingdomLifecyclePhysicalState.Prepared,
					ReceiptId = ChildId(Operation.Id, "source-receipt-" + SourceOrdinal, 0),
					ReceiptTopologyId = TopologyId(Topology, OwnerId, ZoneId, X, Y),
					ReceiptState = KingdomLifecyclePhysicalState.Prepared,
					State = KingdomLifecyclePhysicalState.Prepared
			};
		}

		public static KingdomLifecycleProjection PrepareCarryOutput(
			KingdomCarryOperation Operation, int OutputOrdinal, string ObjectId,
			string Blueprint, KingdomLifecycleTopology Topology, string OwnerId,
			string ZoneId, int X, int Y, int Material, int Count)
		{
			if (Operation == null || OutputOrdinal < 0 || OutputOrdinal >= MaxCarryOutputs
				|| !ValidRootId(ObjectId) || !ValidName(Blueprint)
				|| !TopologyValid(Topology, OwnerId, ZoneId, X, Y)
				|| Material < 0 || Material >= 6 || Count <= 0 || Count > MaxPhysicalCount)
				return null;
			return new KingdomLifecycleProjection
			{
				OperationId = Operation.Id,
				EventId = ChildId(Operation.Id, "projection", OutputOrdinal),
				ObjectId = ObjectId,
				Marker = ChildId(Operation.Id, "marker", OutputOrdinal),
				Blueprint = Blueprint,
				Topology = Topology,
				OwnerId = OwnerId,
				ZoneId = ZoneId,
				X = X,
				Y = Y,
				Material = Material,
				Count = Count,
				NoStack = true,
				State = KingdomLifecyclePhysicalState.Prepared,
				ReceiptId = ChildId(Operation.Id, "output-receipt", OutputOrdinal),
				ReceiptTopologyId = TopologyId(Topology, OwnerId, ZoneId, X, Y),
				ReceiptState = KingdomLifecyclePhysicalState.Prepared
			};
		}

		public static bool TryPublishCarry(KingdomCarryBook Book,
			KingdomCarryOperation Operation)
		{
			if (!CanOwnAuthority(Book) || Operation == null || Book.Open != null
				|| Operation.Sequence != Book.NextSequence
				|| !IsExactSuccessor(Operation.Sequence, Book.RetiredThrough)
				|| Operation.Sequence == long.MaxValue
				|| !string.Equals(Operation.Id, CarryId(Book.RealmId, Operation.Sequence),
					StringComparison.Ordinal)
				|| !ExactStringList(Operation.SettlementIds, Book.SettlementIds)
				|| !string.Equals(Operation.RealmTopologyHash,
					RealmTopologyDigest(Book.RealmId, Book.SettlementIds), StringComparison.Ordinal)
				|| !SettlementMember(Book, Operation.OriginSettlementId)
				|| !SettlementMember(Book, Operation.DestinationSettlementId)
				|| Operation.Phase != KingdomLifecyclePhase.Prepared
				|| !CarryPublicationPlanValid(Operation)) return false;
			KingdomLifecycleResourceLease lease = Operation.ScheduleLease;
			if (!CarryScheduleLeaseShape(Book, Operation, lease, true)) return false;
			KingdomLifecycleResourceRevision row = FindResource(Book, lease.Key);
			bool addRow = row == null;
			if (addRow)
			{
				if (Book.Resources.Count >= MaxResourceRows) return false;
				row = new KingdomLifecycleResourceRevision
				{
					Kind = lease.Kind, ScopeId = lease.ScopeId, SubjectId = lease.SubjectId,
					Key = lease.Key, Revision = 0L
				};
			}
			if (!ResourceMatches(row, lease) || row.Revision != lease.BeforeRevision
				|| !string.IsNullOrEmpty(row.ActiveOperationId)
				|| string.Equals(row.LastOperationId, Operation.Id, StringComparison.Ordinal)) return false;
			string hash;
			if (!TryCarryPlanHash(Operation, out hash)) return false;
			if (!string.IsNullOrEmpty(Operation.PlanHash)
				&& !string.Equals(Operation.PlanHash, hash, StringComparison.Ordinal)) return false;
			Operation.PlanHash = hash;
			if (addRow) Book.Resources.Add(row);
			row.ActiveOperationId = Operation.Id;
			Book.NextSequence = Operation.Sequence + 1L;
			Book.Open = Operation;
			return true;
		}

		private static bool BeginCarryScheduleCore(KingdomCarryBook Book,
			KingdomCarryOperation Operation, KingdomLifecycleResourceLease Lease,
			long CurrentValue)
		{
			KingdomLifecycleResourceRevision row;
			if (!ExactCarryScheduleAuthority(Book, Operation, Lease, out row)
				|| Operation.Phase != KingdomLifecyclePhase.ScheduleIntent
				|| Lease.State != KingdomLifecycleLeaseState.Prepared
				|| CurrentValue != Lease.Before || row.Revision != Lease.BeforeRevision
				|| string.Equals(row.LastOperationId, Operation.Id, StringComparison.Ordinal)) return false;
			Lease.State = KingdomLifecycleLeaseState.Intent;
			return true;
		}

		private static bool CommitCarryScheduleCore(KingdomCarryBook Book,
			KingdomCarryOperation Operation, KingdomLifecycleResourceLease Lease,
			long CurrentValue)
		{
			KingdomLifecycleResourceRevision row;
			if (!ExactCarryScheduleAuthority(Book, Operation, Lease, out row)
				|| Operation.Phase != KingdomLifecyclePhase.ScheduleIntent
				|| Lease.State != KingdomLifecycleLeaseState.Intent
				|| CurrentValue != Lease.After || row.Revision != Lease.BeforeRevision
				|| Lease.AfterRevision != Lease.BeforeRevision + 1L
				|| string.Equals(row.LastOperationId, Operation.Id, StringComparison.Ordinal)) return false;
			row.Revision = Lease.AfterRevision;
			row.LastOperationId = Operation.Id;
			Lease.State = KingdomLifecycleLeaseState.Proved;
			return true;
		}

		private static KingdomLifecycleMutationAction CarryUnitAction(KingdomCarrySource Source,
			int ObservedCount, bool SameIdentity, bool SameTopology)
		{
			if (Source == null || !SameIdentity || !SameTopology) return KingdomLifecycleMutationAction.Quarantine;
			bool before = ObservedCount == Source.UnitBefore;
			bool after = ObservedCount == Source.UnitAfter;
			return MutationAction(Source.UnitState, before, after);
		}

		private static bool BeginCarryUnitCore(KingdomCarryBook Book,
			KingdomCarryOperation Operation, KingdomCarrySource Source)
		{
			int sourceIndex = IndexOfSource(Operation, Source);
			if (!ExactCarryAuthority(Book, Operation)
				|| Operation.Phase != KingdomLifecyclePhase.RemovalIntent
				|| Source == null || Source.OperationId != Operation.Id
				|| sourceIndex < 0 || sourceIndex != Operation.SourceIndex
				|| Source.State != KingdomLifecyclePhysicalState.Prepared
				|| Source.UnitState != KingdomLifecyclePhysicalState.Prepared
				|| Source.ReceiptState != KingdomLifecyclePhysicalState.Prepared
				|| !CarrySourceReceiptPrepared(Source, Operation, sourceIndex)) return false;
			Source.UnitState = KingdomLifecyclePhysicalState.Intent;
			Source.ReceiptBeforeIdMatches = 1;
			Source.ReceiptBeforeCount = Source.UnitBefore;
			Source.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			return true;
		}

		private static bool ConfirmCarryUnitCore(KingdomCarryBook Book,
			KingdomCarryOperation Operation, KingdomCarrySource Source)
		{
			if (!ExactCarryAuthority(Book, Operation)
				|| Operation.Phase != KingdomLifecyclePhase.RemovalIntent
				|| Source == null || Source.OperationId != Operation.Id
				|| Source.UnitState != KingdomLifecyclePhysicalState.Intent
				|| Source.ReceiptState != KingdomLifecyclePhysicalState.Proved
				|| !CarryConserved(Operation)) return false;
			int sourceIndex = IndexOfSource(Operation, Source);
			if (sourceIndex < 0 || sourceIndex != Operation.SourceIndex
				|| !ExactCarrySourceReceipt(Operation, Source, sourceIndex)) return false;
			int nextRemoved;
			int nextEscrow;
			if (!CheckedAdd(Source.Removed, 1, out nextRemoved)
				|| nextRemoved > Source.PlannedCount
				|| !CheckedAdd(MaterialValue(Operation, Source.Material, 1), 1, out nextEscrow)
				|| !ValidCount(nextEscrow)) return false;
			string nextEvent = nextRemoved == Source.PlannedCount ? Source.UnitEventId
				: ChildId(Operation.Id, "source-unit-" + sourceIndex.ToString(
					CultureInfo.InvariantCulture), nextRemoved);
			if (!ValidGeneratedId(nextEvent)) return false;
			string chain = CarrySourceReceiptChain(Source.ReceiptChainId,
				Source.ReceiptProofId, nextRemoved);
			if (!ValidHashNamespace(chain, "carry-source-chain")) return false;
			SetMaterial(Operation, Source.Material, 1, nextEscrow);
			Source.Removed = nextRemoved;
			Source.UnitCursor = nextRemoved;
			Source.ReceiptChainId = chain;
			Source.ReceiptChainCount = nextRemoved;
			Source.UnitState = KingdomLifecyclePhysicalState.Proved;
			if (nextRemoved == Source.PlannedCount)
			{
				Source.State = KingdomLifecyclePhysicalState.Proved;
			}
			else
			{
				Source.UnitBefore = Source.OriginalCount - nextRemoved;
				Source.UnitAfter = Source.UnitBefore - 1;
				Source.UnitEventId = nextEvent;
				Source.UnitState = KingdomLifecyclePhysicalState.Prepared;
				ResetCarrySourceReceipt(Operation, Source, sourceIndex, nextRemoved);
			}
			Operation.SourceIndex = FirstIncompleteSource(Operation);
			return CarryConserved(Operation);
		}

		private static bool BeginCarryOutputCore(KingdomCarryBook Book,
			KingdomCarryOperation Operation, KingdomLifecycleProjection Output)
		{
			int index = IndexOfOutput(Operation, Output);
			if (!ExactCarryAuthority(Book, Operation)
				|| Operation.Phase != KingdomLifecyclePhase.ProjectionIntent
				|| index < 0 || index != Operation.OutputIndex || Operation.LostOnRoad
				|| !CarryOutputShape(Output, Operation.Id, index, false)
				|| Output.State != KingdomLifecyclePhysicalState.Prepared
				|| Output.ReceiptState != KingdomLifecyclePhysicalState.Prepared
				|| Output.ReceiptBeforeIdMatches != -1
				|| Output.ReceiptBeforeMarkerMatches != -1
				|| Output.ReceiptBeforeCount != -1) return false;
			Output.ReceiptBeforeIdMatches = 0;
			Output.ReceiptBeforeMarkerMatches = 0;
			Output.ReceiptBeforeCount = 0;
			Output.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			Output.State = KingdomLifecyclePhysicalState.Intent;
			return true;
		}

	}
}
