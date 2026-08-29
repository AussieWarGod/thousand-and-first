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
		public static bool RetireGrowth(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, long Tick)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation) || Tick < Operation.UpdatedTick
				|| Operation.Phase != KingdomGrowthPhase.Terminal || !GrowthOutboxTerminal(Operation)
				|| !GrowthAllResourcesProved(Book, Operation)) return false;
			KingdomGrowthSlotKind slot = SlotForGrowthAction(Operation.Action);
			KingdomGrowthFieldSlot field = slot == KingdomGrowthSlotKind.Field
				? FindGrowthField(Book, Operation.FieldId) : null;
			if (!IsExactSuccessor(Operation.Sequence, GetGrowthRetired(Book, slot, field))) return false;
			List<KingdomLifecycleResourceLease> leases = GrowthLeases(Operation);
			KingdomGrowthProof proof = new KingdomGrowthProof
			{
				Slot = slot, FieldId = Operation.FieldId, Sequence = Operation.Sequence,
				Id = Operation.Id, PlanHash = Operation.PlanHash, Action = Operation.Action, Tick = Tick
			};
			if (leases == null || !GrowthProofAppendWouldBeValid(Book, proof, slot, field)) return false;
			long retiredBefore = GetGrowthRetired(Book, slot, field);
			long arrivalBefore = Book.NextArrivalTick;
			List<KingdomGrowthProof> proofsBefore =
				new List<KingdomGrowthProof>(Book.RecentProofs);
			for (int i = 0; i < leases.Count; i++)
				FindGrowthResource(Book, leases[i].Key).ActiveOperationId = null;
			SetGrowthRetired(Book, slot, field, Operation.Sequence);
			AppendGrowthProof(Book, proof);
			SetGrowthOperation(Book, slot, field, null);
			if (slot == KingdomGrowthSlotKind.Arrival
				&& !Book.ArrivalCadenceMigrationPending && Book.ArrivalOpportunity != null)
				Book.NextArrivalTick = Book.ArrivalOpportunity.DueTick;
			if (slot == KingdomGrowthSlotKind.Arrival && Book.WorkPaused
				&& Book.ArrivalCadenceMigrationPending
				&& Book.ArrivalCandidate == null)
				Book.NextArrivalTick = 0L;
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			for (int i = 0; i < leases.Count; i++)
				FindGrowthResource(Book, leases[i].Key).ActiveOperationId = Operation.Id;
			SetGrowthRetired(Book, slot, field, retiredBefore);
			Book.RecentProofs.Clear(); Book.RecentProofs.AddRange(proofsBefore);
			SetGrowthOperation(Book, slot, field, Operation);
			Book.NextArrivalTick = arrivalBefore;
			return false;
		}

		public static bool QuarantineGrowthField(KingdomGrowthBook Book, string FieldId,
			string Fault)
		{
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| !ValidRootId(FieldId) || string.IsNullOrEmpty(Fault)
				|| TooLong(Fault, MaxTextChars)) return false;
			KingdomGrowthFieldSlot field = FindGrowthField(Book, FieldId);
			if (field == null || field.Quarantined) return false;
			bool oldQuarantined = field.Quarantined;
			string oldFault = field.Fault;
			field.Quarantined = true; field.Fault = SafeFault(Fault);
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			field.Quarantined = oldQuarantined; field.Fault = oldFault;
			return false;
		}

		public static KingdomGrowthWaterLeg PrepareGrowthWaterLeg(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, KingdomGrowthWaterMutationKind MutationKind,
			string ContainerId, KingdomLifecycleTopology OwnerTopology, string OwnerId,
			string Blueprint, string ZoneId, int X, int Y, int Capacity, int Before, int Delta,
			string BeforeComposition, string AfterComposition, string BeforeOwnerGraphHash,
			string AfterOwnerGraphHash, string BeforePartGraphHash, string AfterPartGraphHash,
			string BeforeTopologyHash, string AfterTopologyHash,
			bool OwnerRemovedAfter = false)
		{
			if (OwnerTopology == KingdomLifecycleTopology.Cell && OwnerId != null
				&& OwnerId.Length == 0) OwnerId = null;
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| Operation == null || Operation.Phase != KingdomGrowthPhase.Prepared
				|| Operation.PlanHash != null || Operation.WaterLegs == null
				|| Operation.WaterLegs.Count >= MaxWaterLegs) return null;
			int ordinal = Operation.WaterLegs.Count;
			int after;
			if (Delta <= 0 || !CheckedAdd(Before,
				MutationKind == KingdomGrowthWaterMutationKind.Drain ? -Delta : Delta, out after))
				return null;
			string key = ResourceKey(KingdomLifecycleResourceKind.WaterVessel, ZoneId, ContainerId);
			KingdomLifecycleResourceRevision row = FindGrowthResource(Book, key);
			if (key == null || (row != null && (!string.IsNullOrEmpty(row.ActiveOperationId)
				|| row.Revision == long.MaxValue))) return null;
			long revision = row == null ? 0L : row.Revision;
			KingdomGrowthLocationKind location = GrowthLocationFromTopology(OwnerTopology);
			if (OwnerRemovedAfter && (MutationKind != KingdomGrowthWaterMutationKind.Drain
				|| after != 0)) return null;
			KingdomGrowthWaterLeg leg = new KingdomGrowthWaterLeg
			{
				OperationId = Operation.Id, EventId = ChildId(Operation.Id, "water", ordinal),
				LeaseKey = key, MutationKind = MutationKind,
					ContainerKind = KingdomGrowthWaterContainerKind.LiquidVolume,
					ContainerId = ContainerId, OwnerTopology = OwnerTopology, OwnerId = OwnerId,
					BeforeLocation = location,
					AfterLocation = OwnerRemovedAfter ? KingdomGrowthLocationKind.Graveyard : location,
					BeforeOwnerId = OwnerId, AfterOwnerId = OwnerId,
					BeforeZoneId = ZoneId, AfterZoneId = ZoneId,
					BeforeX = X, BeforeY = Y, AfterX = X, AfterY = Y,
					OwnerRemovedAfter = OwnerRemovedAfter,
				Blueprint = Blueprint, ZoneId = ZoneId, X = X, Y = Y, Capacity = Capacity,
				Before = Before, Delta = Delta, After = after,
				BeforeComposition = BeforeComposition, AfterComposition = AfterComposition,
				BeforeOwnerGraphHash = BeforeOwnerGraphHash,
				AfterOwnerGraphHash = AfterOwnerGraphHash,
				BeforePartGraphHash = BeforePartGraphHash,
				AfterPartGraphHash = AfterPartGraphHash,
				BeforeTopologyHash = BeforeTopologyHash, AfterTopologyHash = AfterTopologyHash,
					State = KingdomLifecyclePhysicalState.Prepared,
				ReceiptId = ChildId(Operation.Id, "water-receipt", ordinal),
				ReceiptState = KingdomLifecyclePhysicalState.Prepared,
				Lease = new KingdomLifecycleResourceLease
				{
					OperationId = Operation.Id, Kind = KingdomLifecycleResourceKind.WaterVessel,
					ScopeId = ZoneId, SubjectId = ContainerId, Key = key, Before = Before,
					Delta = MutationKind == KingdomGrowthWaterMutationKind.Drain ? -Delta : Delta,
					After = after, BeforeRevision = revision, AfterRevision = revision + 1L,
					State = KingdomLifecycleLeaseState.Prepared
				}
			};
			if (OwnerRemovedAfter)
			{
				leg.AfterOwnerId = null; leg.AfterZoneId = null;
				leg.AfterX = -1; leg.AfterY = -1;
			}
			return GrowthWaterShape(Operation, leg, ordinal, true) ? leg : null;
		}

		public static KingdomGrowthObjectLeg PrepareGrowthObjectLeg(
			KingdomGrowthBook Book, KingdomGrowthOperation Operation, bool Output,
			KingdomGrowthObjectMutationKind MutationKind, string ObjectId, string Marker,
			string Blueprint, KingdomLifecycleTopology Topology, string OwnerId, string ZoneId,
			int X, int Y, int BeforeCount, int Delta, bool NoStack,
			string BeforeOwnerGraphHash, string AfterOwnerGraphHash,
			string BeforeObjectGraphHash, string AfterObjectGraphHash,
			string BeforeTopologyHash, string AfterTopologyHash)
		{
			if (Topology == KingdomLifecycleTopology.Cell && OwnerId != null
				&& OwnerId.Length == 0) OwnerId = null;
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| Operation == null || Operation.Phase != KingdomGrowthPhase.Prepared
				|| !string.Equals(Operation.SettlementId, Book.SettlementId,
					StringComparison.Ordinal)
				|| Operation.PlanHash != null || Operation.Sources == null || Operation.Outputs == null)
				return null;
			if (MutationKind == KingdomGrowthObjectMutationKind.Create && ObjectId != null)
				return null;
			if (MutationKind == KingdomGrowthObjectMutationKind.CellAdd
				&& Topology != KingdomLifecycleTopology.Cell) return null;
			if ((MutationKind == KingdomGrowthObjectMutationKind.InventoryAdd
				|| MutationKind == KingdomGrowthObjectMutationKind.Receive)
				&& Topology != KingdomLifecycleTopology.Inventory) return null;
			List<KingdomGrowthObjectLeg> list = Output ? Operation.Outputs : Operation.Sources;
			if (list.Count >= (Output ? MaxGrowthOutputs : MaxGrowthSources)) return null;
			int after;
			if (!CheckedAdd(BeforeCount, Delta, out after)) return null;
			int ordinal = list.Count;
			KingdomGrowthLocationKind physical = GrowthLocationFromTopology(Topology);
			KingdomGrowthLocationKind beforeLocation = MutationKind ==
				KingdomGrowthObjectMutationKind.Create ? KingdomGrowthLocationKind.Absent
				: (MutationKind == KingdomGrowthObjectMutationKind.CellAdd
					|| MutationKind == KingdomGrowthObjectMutationKind.InventoryAdd
					|| MutationKind == KingdomGrowthObjectMutationKind.Receive)
					? KingdomGrowthLocationKind.Escrow : physical;
			KingdomGrowthLocationKind afterLocation = MutationKind ==
				KingdomGrowthObjectMutationKind.Create ? KingdomGrowthLocationKind.Escrow
				: (MutationKind == KingdomGrowthObjectMutationKind.DestroyOne
					|| MutationKind == KingdomGrowthObjectMutationKind.Obliterate) && after == 0
					? KingdomGrowthLocationKind.Graveyard : physical;
			string escrowKey = beforeLocation == KingdomGrowthLocationKind.Escrow
				|| afterLocation == KingdomGrowthLocationKind.Escrow
				? ChildId(Operation.Id, "object-escrow", Output ? ordinal : MaxGrowthOutputs + ordinal)
				: null;
			string leaseSubject = MutationKind == KingdomGrowthObjectMutationKind.Create
				? Marker : ObjectId;
			string leaseKey = ResourceKey(KingdomLifecycleResourceKind.Object,
				Operation.SettlementId, leaseSubject);
			KingdomLifecycleResourceRevision row = FindGrowthResource(Book, leaseKey);
			if (leaseKey == null || row != null && (!string.IsNullOrEmpty(row.ActiveOperationId)
				|| row.Revision == long.MaxValue)) return null;
			long revision = row == null ? 0L : row.Revision;
			KingdomGrowthObjectLeg leg = new KingdomGrowthObjectLeg
			{
				OperationId = Operation.Id, EventId = ChildId(Operation.Id,
					Output ? "output" : "source", ordinal), ObjectId = ObjectId, Marker = Marker,
				Blueprint = Blueprint, Topology = Topology, OwnerId = OwnerId, ZoneId = ZoneId,
				X = X, Y = Y, BeforeCount = BeforeCount, Delta = Delta, AfterCount = after,
				NoStack = NoStack, MutationKind = MutationKind,
				BeforeOwnerGraphHash = BeforeOwnerGraphHash,
				AfterOwnerGraphHash = MutationKind == KingdomGrowthObjectMutationKind.Create
					? null : AfterOwnerGraphHash,
				BeforeObjectGraphHash = BeforeObjectGraphHash,
				AfterObjectGraphHash = MutationKind == KingdomGrowthObjectMutationKind.Create
					? null : AfterObjectGraphHash,
				BeforeTopologyHash = BeforeTopologyHash,
				AfterTopologyHash = MutationKind == KingdomGrowthObjectMutationKind.Create
					? null : AfterTopologyHash,
				CreatedMarker = Output && MutationKind == KingdomGrowthObjectMutationKind.Create
					? Marker : null,
					DetachedMarker = !Output || MutationKind != KingdomGrowthObjectMutationKind.Create
						? Marker : null,
					BeforeLocation = beforeLocation, AfterLocation = afterLocation,
					EscrowKey = escrowKey,
					Lease = new KingdomLifecycleResourceLease
					{
						OperationId = Operation.Id, Kind = KingdomLifecycleResourceKind.Object,
						ScopeId = Operation.SettlementId, SubjectId = leaseSubject, Key = leaseKey,
						Before = revision, Delta = 1L, After = revision + 1L,
						BeforeRevision = revision, AfterRevision = revision + 1L,
						State = KingdomLifecycleLeaseState.Prepared
					},
				State = KingdomLifecyclePhysicalState.Prepared,
				ReceiptId = ChildId(Operation.Id, Output ? "output-receipt" : "source-receipt",
					ordinal), ReceiptTopologyId = TopologyId(Topology, OwnerId, ZoneId, X, Y),
					ReceiptState = KingdomLifecyclePhysicalState.Prepared
				};
			leg.Callbacks.Add(new KingdomGrowthObjectCallbackStep
			{
				EventId = ChildId(leg.EventId, "object-callback", 0), Kind = MutationKind,
				FromLocation = beforeLocation, ToLocation = afterLocation, EscrowKey = escrowKey,
				BeforeOwnerId = beforeLocation == physical ? OwnerId : null,
				AfterOwnerId = afterLocation == physical ? OwnerId : null,
				BeforeZoneId = beforeLocation == physical ? ZoneId : null,
				AfterZoneId = afterLocation == physical ? ZoneId : null,
				BeforeX = beforeLocation == physical ? X : -1,
				BeforeY = beforeLocation == physical ? Y : -1,
				AfterX = afterLocation == physical ? X : -1,
				AfterY = afterLocation == physical ? Y : -1,
				BeforeCount = BeforeCount, AfterCount = after, NoStack = NoStack,
				BeforeOwnerGraphHash = BeforeOwnerGraphHash,
				AfterOwnerGraphHash = MutationKind == KingdomGrowthObjectMutationKind.Create
					? null : AfterOwnerGraphHash,
				BeforeObjectGraphHash = BeforeObjectGraphHash,
				AfterObjectGraphHash = MutationKind == KingdomGrowthObjectMutationKind.Create
					? null : AfterObjectGraphHash,
				BeforeTopologyHash = BeforeTopologyHash,
				AfterTopologyHash = MutationKind == KingdomGrowthObjectMutationKind.Create
					? null : AfterTopologyHash,
				State = KingdomLifecyclePhysicalState.Prepared,
				ReceiptId = ChildId(leg.EventId, "object-callback-receipt", 0),
				ReceiptState = KingdomLifecyclePhysicalState.Prepared
			});
			return leg;
		}

		internal static bool BeginGrowthWaterCallback(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, int Ordinal)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation)
				|| Operation.Phase != KingdomGrowthPhase.WaterIntent
				|| Ordinal != Operation.WaterCursor || Ordinal < 0
				|| Ordinal >= Operation.WaterLegs.Count) return false;
			KingdomGrowthWaterLeg leg = Operation.WaterLegs[Ordinal];
			if (leg.State != KingdomLifecyclePhysicalState.Prepared
				|| leg.ReceiptState != KingdomLifecyclePhysicalState.Prepared
				|| leg.Lease.State != KingdomLifecycleLeaseState.Prepared) return false;
			leg.State = KingdomLifecyclePhysicalState.Intent;
			leg.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			leg.Lease.State = KingdomLifecycleLeaseState.Intent;
			leg.ReceiptBeforeMatches = 1;
			leg.ReceiptBeforeOwnerGraphHash = leg.BeforeOwnerGraphHash;
			leg.ReceiptBeforePartGraphHash = leg.BeforePartGraphHash;
			leg.ReceiptBeforeTopologyHash = leg.BeforeTopologyHash;
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			leg.State = KingdomLifecyclePhysicalState.Prepared;
			leg.ReceiptState = KingdomLifecyclePhysicalState.Prepared;
			leg.Lease.State = KingdomLifecycleLeaseState.Prepared;
			leg.ReceiptBeforeMatches = -1;
			leg.ReceiptBeforeOwnerGraphHash = null;
			leg.ReceiptBeforePartGraphHash = null;
			leg.ReceiptBeforeTopologyHash = null;
			return false;
		}

	}
}
