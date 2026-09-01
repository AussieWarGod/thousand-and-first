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
		private static KingdomLifecycleCasAction LeaseSnapshotAction(long CurrentValue,
			long CurrentRevision, string LastOperationId, string ActiveOperationId,
			KingdomLifecycleResourceLease Lease)
		{
			if (!LeaseShape(Lease, Lease == null ? null : Lease.OperationId, false)
				|| !string.Equals(ActiveOperationId, Lease.OperationId, StringComparison.Ordinal))
				return KingdomLifecycleCasAction.Quarantine;
			if (Lease.State == KingdomLifecycleLeaseState.Prepared)
			{
				return CurrentValue == Lease.Before && CurrentRevision == Lease.BeforeRevision
					&& !string.Equals(LastOperationId, Lease.OperationId, StringComparison.Ordinal)
					? KingdomLifecycleCasAction.Apply : KingdomLifecycleCasAction.Quarantine;
			}
			if (Lease.State == KingdomLifecycleLeaseState.Intent)
			{
				return CurrentValue == Lease.After && CurrentRevision == Lease.AfterRevision
					&& string.Equals(LastOperationId, Lease.OperationId, StringComparison.Ordinal)
					? KingdomLifecycleCasAction.Confirm : KingdomLifecycleCasAction.Quarantine;
			}
			if (Lease.State == KingdomLifecycleLeaseState.Proved)
			{
				return CurrentValue == Lease.After && CurrentRevision == Lease.AfterRevision
					&& string.Equals(LastOperationId, Lease.OperationId, StringComparison.Ordinal)
					? KingdomLifecycleCasAction.Confirm : KingdomLifecycleCasAction.Quarantine;
			}
			return KingdomLifecycleCasAction.Quarantine;
		}

		public static bool BeginLease(KingdomLifecycleBook Book,
			KingdomLifecycleResourceLease Lease, long CurrentValue)
		{
			return Lease != null && IsDomainResourceKind(Lease.Kind)
				&& BeginLeaseCore(Book, Lease, CurrentValue);
		}

		private static bool BeginLeaseCore(KingdomLifecycleBook Book,
			KingdomLifecycleResourceLease Lease, long CurrentValue)
		{
			KingdomLifecycleOperation operation;
			KingdomLifecycleResourceRevision row;
			if (Lease == null || !ExactLeaseAuthority(Book, Lease, out operation, out row)
				|| !LeasePhaseAllows(operation, Lease)
				|| LeaseActionCore(Book, Lease, CurrentValue)
					!= KingdomLifecycleCasAction.Apply) return false;
			Lease.State = KingdomLifecycleLeaseState.Intent;
			return true;
		}

		/// <summary>Called only in the same live stack after the scalar mutation returned.</summary>
		public static bool CommitLeaseWitness(KingdomLifecycleBook Book,
			KingdomLifecycleResourceLease Lease, long CurrentValue)
		{
			KingdomLifecycleOperation operation;
			KingdomLifecycleResourceRevision row;
			if (Lease == null || !IsDomainResourceKind(Lease.Kind)
				|| !ExactLeaseAuthority(Book, Lease, out operation, out row)) return false;
			return CommitLeaseWitnessCore(Book, operation, Lease, row, CurrentValue);
		}

		private static bool CommitLeaseWitnessCore(KingdomLifecycleBook Book,
			KingdomLifecycleOperation operation, KingdomLifecycleResourceLease Lease,
			KingdomLifecycleResourceRevision row, long CurrentValue)
		{
			if (!ExactOperationAuthority(Book, operation)
				|| !LeasePhaseAllows(operation, Lease)
				|| Lease.State != KingdomLifecycleLeaseState.Intent
				|| CurrentValue != Lease.After || row.Revision != Lease.BeforeRevision
				|| Lease.AfterRevision != Lease.BeforeRevision + 1L
				|| !string.Equals(row.ActiveOperationId, Lease.OperationId, StringComparison.Ordinal)
				|| string.Equals(row.LastOperationId, Lease.OperationId, StringComparison.Ordinal))
				return false;
			row.Revision = Lease.AfterRevision;
			row.LastOperationId = Lease.OperationId;
			Lease.State = KingdomLifecycleLeaseState.Proved;
			if (operation.Action == KingdomLifecycleAction.Depart
				&& IsRequiredDomainLease(operation, Lease)) operation.DepartedCount = operation.Count;
			return true;
		}

		private static bool BeginWaterLeaseCore(KingdomLifecycleBook Book,
			KingdomLifecycleResourceLease Lease, KingdomLifecycleWaterLeg Leg,
			long CurrentValue)
		{
			KingdomLifecycleOperation operation;
			KingdomLifecycleResourceRevision row;
			if (!ExactLeaseAuthority(Book, Lease, out operation, out row)
				|| Leg == null || Leg.State != KingdomLifecyclePhysicalState.Prepared
				|| Leg.ReceiptState != KingdomLifecyclePhysicalState.Prepared
				|| !ReferenceEquals(FindWaterLeg(operation, Lease.Key), Leg)
				|| LeaseActionCore(Book, Lease, CurrentValue) != KingdomLifecycleCasAction.Apply)
				return false;
			Lease.State = KingdomLifecycleLeaseState.Intent;
			Leg.State = KingdomLifecyclePhysicalState.Intent;
			Leg.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			return true;
		}

		private static bool ConfirmWaterLeaseCore(KingdomLifecycleBook Book,
			KingdomLifecycleResourceLease Lease, KingdomLifecycleWaterLeg Leg,
			long CurrentValue)
		{
			KingdomLifecycleOperation operation;
			KingdomLifecycleResourceRevision row;
			int proved;
			int outstanding;
			if (!ExactLeaseAuthority(Book, Lease, out operation, out row)
				|| Leg == null || Leg.State != KingdomLifecyclePhysicalState.Intent
				|| Leg.ReceiptState != KingdomLifecyclePhysicalState.Intent
				|| !ReferenceEquals(FindWaterLeg(operation, Lease.Key), Leg)
				|| !CheckedAdd(operation.WaterProved, Leg.Delta, out proved)
				|| !CheckedAdd(operation.WaterOutstanding, -Leg.Delta, out outstanding)
				|| !ValidCount(proved) || !ValidCount(outstanding)
				|| !CommitLeaseWitnessCore(Book, operation, Lease, row, CurrentValue)) return false;
			Leg.State = KingdomLifecyclePhysicalState.Proved;
			operation.WaterProved = proved;
			operation.WaterOutstanding = outstanding;
			operation.WaterState = AllWaterLegsProved(operation)
				? KingdomLifecyclePhysicalState.Proved : KingdomLifecyclePhysicalState.Intent;
			return true;
		}

		public static KingdomLifecycleMutationAction MutationAction(
			KingdomLifecyclePhysicalState State, bool ExactBefore, bool ExactAfter)
		{
			switch (State)
			{
			case KingdomLifecyclePhysicalState.Prepared:
				return ExactBefore && !ExactAfter
					? KingdomLifecycleMutationAction.InvokeOnce
					: KingdomLifecycleMutationAction.Quarantine;
			case KingdomLifecyclePhysicalState.Intent:
				return ExactAfter && !ExactBefore
					? KingdomLifecycleMutationAction.ConfirmAfter
					: KingdomLifecycleMutationAction.Quarantine;
			case KingdomLifecyclePhysicalState.Proved:
			case KingdomLifecyclePhysicalState.Skipped:
				return KingdomLifecycleMutationAction.Settled;
			default:
				return KingdomLifecycleMutationAction.Quarantine;
			}
		}

		public static bool CanTransition(KingdomLifecycleAction Action,
			KingdomLifecyclePhase From, KingdomLifecyclePhase To)
		{
			if (To == KingdomLifecyclePhase.Quarantined)
				return PhaseAllowed(Action, From) && From != KingdomLifecyclePhase.Terminal
					&& From != KingdomLifecyclePhase.Quarantined;
			KingdomLifecyclePhase next;
			return TryNextPhase(Action, From, out next) && next == To;
		}

		public static bool PhaseAllowed(KingdomLifecycleAction Action,
			KingdomLifecyclePhase Phase)
		{
			if (Phase == KingdomLifecyclePhase.Quarantined)
				return KnownAction(Action);
			KingdomLifecyclePhase current = KingdomLifecyclePhase.Prepared;
			if (!KnownAction(Action)) return false;
			for (int i = 0; i < 16; i++)
			{
				if (current == Phase) return true;
				KingdomLifecyclePhase next;
				if (!TryNextPhase(Action, current, out next)) return false;
				current = next;
			}
			return false;
		}

		public static bool AdvancePhase(KingdomLifecycleBook Book,
			KingdomLifecycleOperation Operation, KingdomLifecyclePhase To, long Tick)
		{
			if (!ExactOperationAuthority(Book, Operation) || Tick < Operation.UpdatedTick
				|| !CanTransition(Operation.Action, Operation.Phase, To)
				|| !TransitionReady(Book, Operation, To)) return false;
			if (To == KingdomLifecyclePhase.Terminal
				&& !TerminalComponentsSettled(Book, Operation)) return false;
			Operation.Phase = To;
			Operation.UpdatedTick = Tick;
			return true;
		}

		public static bool Quarantine(KingdomLifecycleOperation Operation, string Fault)
		{
			if (Operation == null || Operation.Phase == KingdomLifecyclePhase.Quarantined) return false;
			Operation.Phase = KingdomLifecyclePhase.Quarantined;
			Operation.Fault = SafeFault(Fault);
			return true;
		}

		public static bool Retire(KingdomLifecycleBook Book,
			KingdomLifecycleOperation Operation, long Tick)
		{
			if (LodgeAuthorityReleased(Operation))
				return TryRemoveReleasedLodge(Book, Operation, Tick);
			if (LodgeAbandoned(Operation))
				return TryReleaseAbandonedLodge(Book, Operation, Tick)
					&& TryRemoveReleasedLodge(Book, Operation, Tick);
			if (!ExactOperationAuthority(Book, Operation) || Tick < Operation.UpdatedTick
				|| Operation.Phase != KingdomLifecyclePhase.Terminal
				|| !IsExactSuccessor(Operation.Sequence,
					GetRetiredThrough(Book, Operation.Lane))
				|| !TerminalComponentsSettled(Book, Operation)
				|| !ProofListValid(Book)) return false;
			for (int i = 0; i < Operation.ResourceLeases.Count; i++)
			{
				KingdomLifecycleResourceLease lease = Operation.ResourceLeases[i];
				KingdomLifecycleResourceRevision row = FindResource(Book, lease.Key);
				if (row == null || lease.State != KingdomLifecycleLeaseState.Proved
					|| row.Revision != lease.AfterRevision
					|| !string.Equals(row.LastOperationId, Operation.Id, StringComparison.Ordinal)
					|| !string.Equals(row.ActiveOperationId, Operation.Id, StringComparison.Ordinal))
					return false;
			}
			for (int i = 0; i < Operation.ResourceLeases.Count; i++)
				FindResource(Book, Operation.ResourceLeases[i].Key).ActiveOperationId = null;
			Operation.UpdatedTick = Tick;
			SetRetiredThrough(Book, Operation.Lane, Operation.Sequence);
			AppendLifecycleProof(Book, new KingdomLifecycleProof
			{
				Sequence = Operation.Sequence,
				Id = Operation.Id,
				PlanHash = Operation.PlanHash,
				Lane = Operation.Lane,
				Action = Operation.Action,
				Tick = Tick
			});
			SetSlot(Book, Operation.Lane, null);
			return true;
		}

		public static bool SinkSettled(KingdomLifecycleSinkState State)
		{
			return State == KingdomLifecycleSinkState.Delivered
				|| State == KingdomLifecycleSinkState.Skipped
				|| State == KingdomLifecycleSinkState.Lost;
		}

		public static KingdomLifecycleSinkState ResumeSink(
			KingdomLifecycleSinkState State, bool ChronicleRecordOnce)
		{
			if (State != KingdomLifecycleSinkState.Intent) return State;
			return ChronicleRecordOnce ? KingdomLifecycleSinkState.Pending
				: KingdomLifecycleSinkState.Lost;
		}

		public static bool RecoverOutbox(KingdomLifecycleBook Book,
			KingdomLifecycleOperation Operation)
		{
			if (!ExactOperationAuthority(Book, Operation)
				|| Operation.Phase != KingdomLifecyclePhase.Sinks
				|| Operation.Outbox == null) return false;
			KingdomLifecycleOutbox Outbox = Operation.Outbox;
			Outbox.ChronicleState = ResumeSink(Outbox.ChronicleState, true);
			Outbox.LedgerState = ResumeSink(Outbox.LedgerState, false);
			Outbox.MessageState = ResumeSink(Outbox.MessageState, false);
			Outbox.DeedState = ResumeSink(Outbox.DeedState, false);
			Outbox.GuestbookState = ResumeSink(Outbox.GuestbookState, false);
			return ExactOperationAuthority(Book, Operation);
		}

		public static bool RecoverCarryOutbox(KingdomCarryBook Book,
			KingdomCarryOperation Operation)
		{
			if (!ExactCarryAuthority(Book, Operation)
				|| Operation.Phase != KingdomLifecyclePhase.Sinks
				|| Operation.Outbox == null) return false;
			KingdomLifecycleOutbox Outbox = Operation.Outbox;
			Outbox.ChronicleState = ResumeSink(Outbox.ChronicleState, true);
			Outbox.LedgerState = ResumeSink(Outbox.LedgerState, false);
			Outbox.MessageState = ResumeSink(Outbox.MessageState, false);
			Outbox.DeedState = ResumeSink(Outbox.DeedState, false);
			Outbox.GuestbookState = ResumeSink(Outbox.GuestbookState, false);
			return ExactCarryAuthority(Book, Operation);
		}

	}
}
