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
		private static bool ExactCarryScheduleAuthority(KingdomCarryBook book,
			KingdomCarryOperation operation, KingdomLifecycleResourceLease lease,
			out KingdomLifecycleResourceRevision row)
		{
			row = null;
			if (!ExactCarryAuthority(book, operation) || lease == null
				|| !ReferenceEquals(operation.ScheduleLease, lease)
				|| !CarryScheduleLeaseShape(book, operation, lease, false)) return false;
			row = FindResource(book, lease.Key);
			return ResourceMatches(row, lease)
				&& string.Equals(row.ActiveOperationId, operation.Id, StringComparison.Ordinal);
		}

		private static bool CarryScheduleLeaseShape(KingdomCarryBook book,
			KingdomCarryOperation operation, KingdomLifecycleResourceLease lease,
			bool Publication)
		{
			return book != null && operation != null
				&& LeaseShape(lease, operation.Id, Publication)
				&& lease.Kind == KingdomLifecycleResourceKind.Schedule
				&& string.Equals(lease.ScopeId, book.RealmId, StringComparison.Ordinal)
				&& string.Equals(lease.SubjectId, operation.DestinationSettlementId,
					StringComparison.Ordinal)
				&& lease.After == operation.DueTick;
		}

		private static bool CarryScheduleProved(KingdomCarryBook book,
			KingdomCarryOperation operation)
		{
			KingdomLifecycleResourceRevision row;
			return ExactCarryScheduleAuthority(book, operation, operation == null
				? null : operation.ScheduleLease, out row)
				&& operation.ScheduleLease.State == KingdomLifecycleLeaseState.Proved
				&& row.Revision == operation.ScheduleLease.AfterRevision
				&& string.Equals(row.LastOperationId, operation.Id, StringComparison.Ordinal)
				&& CarryScheduleReceiptShape(operation, false)
				&& operation.ScheduleReceiptState == KingdomLifecyclePhysicalState.Proved;
		}

		private static bool ExactLeaseAuthority(KingdomLifecycleBook book,
			KingdomLifecycleResourceLease lease, out KingdomLifecycleOperation operation,
			out KingdomLifecycleResourceRevision row)
		{
			operation = null;
			row = null;
			if (lease == null || !CanOwnAuthority(book)) return false;
			operation = FindOpenOperation(book, lease.OperationId);
			if (operation == null || !ReferenceEquals(GetSlot(book, operation.Lane), operation))
				return false;
			bool member = false;
			for (int i = 0; i < operation.ResourceLeases.Count; i++)
				if (ReferenceEquals(operation.ResourceLeases[i], lease)) { member = true; break; }
			if (!member) return false;
			row = FindResource(book, lease.Key);
			return row != null && ResourceMatches(row, lease)
				&& string.Equals(row.ActiveOperationId, operation.Id, StringComparison.Ordinal);
		}

		private static bool LeasePhaseAllows(KingdomLifecycleOperation operation,
			KingdomLifecycleResourceLease lease)
		{
			if (operation == null || lease == null) return false;
			switch (lease.Kind)
			{
			case KingdomLifecycleResourceKind.Schedule:
				return operation.Phase == KingdomLifecyclePhase.ScheduleIntent
					|| operation.Action == KingdomLifecycleAction.Lodge
						&& operation.Phase == KingdomLifecyclePhase.DomainIntent
						&& operation.LodgeTerminal != null
						&& operation.LodgeTerminal.State ==
							KingdomLifecycleLodgeTerminalState.AbandonIntent;
			case KingdomLifecycleResourceKind.WaterVessel:
				return operation.Phase == KingdomLifecyclePhase.WaterIntent;
			case KingdomLifecycleResourceKind.Projection:
				return operation.Phase == KingdomLifecyclePhase.ProjectionIntent;
			case KingdomLifecycleResourceKind.Object:
				return operation.Phase == KingdomLifecyclePhase.RemovalIntent;
			default:
				return operation.Phase == KingdomLifecyclePhase.DomainIntent;
			}
		}

		private static KingdomLifecyclePhase LeaseIntentPhase(
			KingdomLifecycleResourceLease lease)
		{
			if (lease == null) return KingdomLifecyclePhase.Invalid;
			switch (lease.Kind)
			{
			case KingdomLifecycleResourceKind.Schedule: return KingdomLifecyclePhase.ScheduleIntent;
			case KingdomLifecycleResourceKind.WaterVessel: return KingdomLifecyclePhase.WaterIntent;
			case KingdomLifecycleResourceKind.Projection: return KingdomLifecyclePhase.ProjectionIntent;
			case KingdomLifecycleResourceKind.Object: return KingdomLifecyclePhase.RemovalIntent;
			default: return KingdomLifecyclePhase.DomainIntent;
			}
		}

		private static int PhaseOrdinal(KingdomLifecycleAction action,
			KingdomLifecyclePhase phase)
		{
			if (phase == KingdomLifecyclePhase.Quarantined) return -2;
			KingdomLifecyclePhase current = KingdomLifecyclePhase.Prepared;
			for (int i = 0; i < 16; i++)
			{
				if (current == phase) return i;
				KingdomLifecyclePhase next;
				if (!TryNextPhase(action, current, out next)) break;
				current = next;
			}
			return -1;
		}

		private static bool LifecyclePhaseProgressValid(KingdomLifecycleOperation operation)
		{
			if (operation == null) return false;
			if (operation.Phase == KingdomLifecyclePhase.Quarantined) return true;
			if (LodgeAbandoned(operation)) return OutboxInitial(operation.Outbox)
				&& WaterConserved(operation, true);
			int current = PhaseOrdinal(operation.Action, operation.Phase);
			int sinks = PhaseOrdinal(operation.Action, KingdomLifecyclePhase.Sinks);
			if (current < 0 || sinks < 0) return false;

			if (operation.Projections.Count > 0)
			{
				for (int i = 0; i < operation.Projections.Count; i++)
					if (!PhysicalProgressValid(operation, KingdomLifecyclePhase.ProjectionIntent,
						KingdomLifecyclePhase.Projected, operation.Projections[i].State, false)) return false;
				int projectionIntent = PhaseOrdinal(operation.Action,
					KingdomLifecyclePhase.ProjectionIntent);
				int projected = PhaseOrdinal(operation.Action, KingdomLifecyclePhase.Projected);
				if (current < projectionIntent && operation.Spawned != 0) return false;
				if (current >= projected && !ProjectionConserved(operation, true)) return false;
			}

			if (operation.WaterRequested > 0)
			{
				bool externalRaidTribute = ExternalRaidTributeReceipt(operation);
				if (!externalRaidTribute && !PhysicalProgressValid(operation,
					KingdomLifecyclePhase.WaterIntent,
					KingdomLifecyclePhase.WaterSettled, operation.WaterState, false)) return false;
				for (int i = 0; i < operation.WaterLegs.Count; i++)
					if (!PhysicalProgressValid(operation, KingdomLifecyclePhase.WaterIntent,
						KingdomLifecyclePhase.WaterSettled, operation.WaterLegs[i].State, false))
						return false;
				int waterIntent = PhaseOrdinal(operation.Action, KingdomLifecyclePhase.WaterIntent);
				int waterSettled = PhaseOrdinal(operation.Action, KingdomLifecyclePhase.WaterSettled);
				if (!externalRaidTribute && current < waterIntent && (operation.WaterProved != 0
					|| operation.WaterOutstanding != operation.WaterRequested
					|| operation.WaterLost != 0 || operation.WaterAmbiguous != 0)) return false;
				if (current >= waterSettled && !WaterConserved(operation, true)) return false;
			}

			bool removes = operation.Action == KingdomLifecycleAction.Depart
				|| operation.Action == KingdomLifecycleAction.OfferWater;
			if (removes && !PhysicalProgressValid(operation, KingdomLifecyclePhase.RemovalIntent,
				KingdomLifecyclePhase.Removed, operation.RemovalState, false)) return false;

			if (operation.Action == KingdomLifecycleAction.RaidAttack)
			{
				int effectIntent = PhaseOrdinal(operation.Action, KingdomLifecyclePhase.EffectIntent);
				int effectsSettled = PhaseOrdinal(operation.Action, KingdomLifecyclePhase.EffectsSettled);
				if (current < effectIntent)
				{
					if (operation.EffectState != KingdomLifecyclePhysicalState.Prepared
						|| operation.PlunderProved != 0) return false;
				}
				else if (current == effectIntent)
				{
					if (operation.EffectState != KingdomLifecyclePhysicalState.Prepared
						&& operation.EffectState != KingdomLifecyclePhysicalState.Intent
						&& operation.EffectState != KingdomLifecyclePhysicalState.Proved
						&& operation.EffectState != KingdomLifecyclePhysicalState.Skipped) return false;
				}
				else if (current >= effectsSettled
					&& operation.EffectState != KingdomLifecyclePhysicalState.Proved
					&& operation.EffectState != KingdomLifecyclePhysicalState.Skipped) return false;
			}

			if (current < sinks) return OutboxInitial(operation.Outbox);
			if (current > sinks) return OutboxTerminal(operation);
			return true;
		}

		private static bool PhysicalProgressValid(KingdomLifecycleOperation operation,
			KingdomLifecyclePhase intentPhase, KingdomLifecyclePhase settledPhase,
			KingdomLifecyclePhysicalState state, bool allowSkipped)
		{
			int current = PhaseOrdinal(operation.Action, operation.Phase);
			int intent = PhaseOrdinal(operation.Action, intentPhase);
			int settled = PhaseOrdinal(operation.Action, settledPhase);
			if (current < 0 || intent < 0 || settled < 0) return false;
			if (current < intent) return state == KingdomLifecyclePhysicalState.Prepared;
			if (current < settled) return state == KingdomLifecyclePhysicalState.Prepared
				|| state == KingdomLifecyclePhysicalState.Intent
				|| state == KingdomLifecyclePhysicalState.Proved
				|| (allowSkipped && state == KingdomLifecyclePhysicalState.Skipped);
			return state == KingdomLifecyclePhysicalState.Proved
				|| (allowSkipped && state == KingdomLifecyclePhysicalState.Skipped);
		}

		private static bool OutboxInitial(KingdomLifecycleOutbox box)
		{
			return box != null
				&& InitialSinkState(box.ChronicleDisposition, box.ChronicleState)
				&& InitialSinkState(box.LedgerDisposition, box.LedgerState)
				&& InitialSinkState(box.MessageDisposition, box.MessageState)
				&& InitialSinkState(box.DeedDisposition, box.DeedState)
				&& InitialSinkState(box.GuestbookDisposition, box.GuestbookState);
		}

		private static bool InitialSinkState(KingdomLifecycleSinkDisposition disposition,
			KingdomLifecycleSinkState state)
		{
			return disposition == KingdomLifecycleSinkDisposition.Skip
				? state == KingdomLifecycleSinkState.Skipped
				: disposition == KingdomLifecycleSinkDisposition.Deliver
					&& state == KingdomLifecycleSinkState.Pending;
		}

		private static bool LeaseStateAllowedAtPhase(KingdomLifecycleOperation operation,
			KingdomLifecycleResourceLease lease)
		{
			if (operation == null || lease == null) return false;
			if (LodgeAbandoned(operation))
				return lease.Kind == KingdomLifecycleResourceKind.Roster
					? lease.State == KingdomLifecycleLeaseState.Proved
						|| lease.State == KingdomLifecycleLeaseState.Skipped
					: lease.State == KingdomLifecycleLeaseState.Proved;
			if (operation.Action == KingdomLifecycleAction.Lodge
				&& operation.Phase == KingdomLifecyclePhase.DomainIntent
				&& operation.LodgeTerminal != null
				&& operation.LodgeTerminal.State == KingdomLifecycleLodgeTerminalState.AbandonIntent)
			{
				if (lease.Kind == KingdomLifecycleResourceKind.Schedule)
					return lease.State == KingdomLifecycleLeaseState.Prepared
						|| lease.State == KingdomLifecycleLeaseState.Intent
						|| lease.State == KingdomLifecycleLeaseState.Proved;
				if (lease.Kind == KingdomLifecycleResourceKind.Roster)
					return lease.State == KingdomLifecycleLeaseState.Prepared
						|| lease.State == KingdomLifecycleLeaseState.Intent
						|| lease.State == KingdomLifecycleLeaseState.Proved
						|| lease.State == KingdomLifecycleLeaseState.Skipped;
				return lease.Kind == KingdomLifecycleResourceKind.WaterVessel
					&& lease.State == KingdomLifecycleLeaseState.Proved;
			}
			if (operation.Phase == KingdomLifecyclePhase.Quarantined)
				return lease.State == KingdomLifecycleLeaseState.Prepared
					|| lease.State == KingdomLifecycleLeaseState.Intent
					|| lease.State == KingdomLifecycleLeaseState.Proved;
			int current = PhaseOrdinal(operation.Action, operation.Phase);
			int intent = PhaseOrdinal(operation.Action, LeaseIntentPhase(lease));
			if (current < 0 || intent < 0) return false;
			if (current < intent) return lease.State == KingdomLifecycleLeaseState.Prepared;
			if (current == intent) return lease.State == KingdomLifecycleLeaseState.Prepared
				|| lease.State == KingdomLifecycleLeaseState.Intent
				|| lease.State == KingdomLifecycleLeaseState.Proved;
			return lease.State == KingdomLifecycleLeaseState.Proved;
		}

		private static KingdomLifecycleProjection ProjectionForLease(
			KingdomLifecycleOperation operation, KingdomLifecycleResourceLease lease)
		{
			if (operation == null || lease == null || operation.Projections == null) return null;
			KingdomLifecycleProjection found = null;
			for (int i = 0; i < operation.Projections.Count; i++)
			{
				KingdomLifecycleProjection projection = operation.Projections[i];
				if (projection == null) continue;
				string topology = TopologyId(projection.Topology, projection.OwnerId,
					projection.ZoneId, projection.X, projection.Y);
				string key = ResourceKey(KingdomLifecycleResourceKind.Projection,
					topology, projection.ObjectId);
				if (!string.Equals(key, lease.Key, StringComparison.Ordinal)) continue;
				if (found != null) return null;
				found = projection;
			}
			return found;
		}

		private static bool IsExactSuccessor(long value, long previous)
		{
			long expected;
			return previous >= 0L && previous < long.MaxValue
				&& CheckedAdd(previous, 1L, out expected) && value == expected;
		}

	}
}
