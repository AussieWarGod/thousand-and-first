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
		private static bool GrowthDomainSetMatchesAction(KingdomGrowthOperation operation)
		{
			KingdomGrowthDomainStepKind[] expected;
			switch (operation.Action)
			{
			case KingdomGrowthAction.Heartbeat:
				expected = operation.PopulationDelta < 0
					? new[] { KingdomGrowthDomainStepKind.Scarcity,
						KingdomGrowthDomainStepKind.Roster, KingdomGrowthDomainStepKind.Creed,
						KingdomGrowthDomainStepKind.Population,
						KingdomGrowthDomainStepKind.Accounting }
					: new[] { KingdomGrowthDomainStepKind.Scarcity,
						KingdomGrowthDomainStepKind.Accounting };
				break;
			case KingdomGrowthAction.Fetch:
			case KingdomGrowthAction.Mill:
				expected = new[] { KingdomGrowthDomainStepKind.Accounting }; break;
			case KingdomGrowthAction.Arrival:
				expected = operation.ArrivalDisposition == KingdomGrowthArrivalDisposition.Joined
					? new[] { KingdomGrowthDomainStepKind.Enrollment,
						KingdomGrowthDomainStepKind.Roster, KingdomGrowthDomainStepKind.Creed,
						KingdomGrowthDomainStepKind.Population,
						KingdomGrowthDomainStepKind.Accounting }
					: new KingdomGrowthDomainStepKind[0];
				break;
			case KingdomGrowthAction.Departure:
				expected = operation.DepartureCauseKind == KingdomGrowthDepartureCauseKind.Subsidence
					? new[] { KingdomGrowthDomainStepKind.Roster, KingdomGrowthDomainStepKind.Creed,
						KingdomGrowthDomainStepKind.Population,
						KingdomGrowthDomainStepKind.SubsidenceSchedule,
						KingdomGrowthDomainStepKind.Accounting }
					: new[] { KingdomGrowthDomainStepKind.Roster,
						KingdomGrowthDomainStepKind.Creed, KingdomGrowthDomainStepKind.Population,
						KingdomGrowthDomainStepKind.Accounting };
				break;
			case KingdomGrowthAction.Delivery:
				expected = new[] { KingdomGrowthDomainStepKind.PendingCrop,
					KingdomGrowthDomainStepKind.Accounting };
				break;
			case KingdomGrowthAction.Sow:
			case KingdomGrowthAction.Withdraw:
			case KingdomGrowthAction.Ripen:
				expected = new[] { KingdomGrowthDomainStepKind.CropRegistry,
					KingdomGrowthDomainStepKind.Field }; break;
			case KingdomGrowthAction.Harvest:
				expected = operation.PendingCropDelta == 0
					? new[] { KingdomGrowthDomainStepKind.CropRegistry,
						KingdomGrowthDomainStepKind.Field,
						KingdomGrowthDomainStepKind.Accounting }
					: new[] { KingdomGrowthDomainStepKind.CropRegistry,
						KingdomGrowthDomainStepKind.Field,
						KingdomGrowthDomainStepKind.PendingCrop,
						KingdomGrowthDomainStepKind.Accounting };
				break;
			case KingdomGrowthAction.Irrigate:
				expected = new[] { KingdomGrowthDomainStepKind.Field }; break;
			default: return false;
			}
			if (operation.DomainSteps.Count != expected.Length) return false;
			for (int i = 0; i < expected.Length; i++)
			{
				KingdomGrowthDomainStep step = operation.DomainSteps[i];
				if (step == null || step.Kind != expected[i]
					|| !string.Equals(step.Lease.ScopeId, operation.SettlementId,
						StringComparison.Ordinal)
					|| !GrowthDomainScalarBinding(operation, step)) return false;
				if (step.Kind == KingdomGrowthDomainStepKind.Population
					|| step.Kind == KingdomGrowthDomainStepKind.PendingCrop
					|| step.Kind == KingdomGrowthDomainStepKind.Scarcity
					|| step.Kind == KingdomGrowthDomainStepKind.Accounting
					|| step.Kind == KingdomGrowthDomainStepKind.SubsidenceSchedule
					|| step.Kind == KingdomGrowthDomainStepKind.PorterJob)
				{
					if (!string.Equals(step.SubjectId, operation.SettlementId,
						StringComparison.Ordinal)) return false;
				}
				else if (step.Kind == KingdomGrowthDomainStepKind.Field
					|| step.Kind == KingdomGrowthDomainStepKind.CropRegistry)
				{
					if (!string.Equals(step.SubjectId, operation.FieldId,
						StringComparison.Ordinal) || !string.Equals(step.ActorId,
						operation.TargetId, StringComparison.Ordinal)) return false;
				}
				else
				{
					string actor = operation.TargetId;
					if (!string.Equals(step.ActorId, actor, StringComparison.Ordinal)
						|| !string.Equals(step.SubjectId, actor, StringComparison.Ordinal)) return false;
				}
			}
			return true;
		}

		private static bool GrowthDomainScalarBinding(KingdomGrowthOperation operation,
			KingdomGrowthDomainStep step)
		{
			switch (step.Kind)
			{
			case KingdomGrowthDomainStepKind.Population:
				return step.BeforeValue == operation.PopulationBefore
					&& step.AfterValue == operation.PopulationAfter;
			case KingdomGrowthDomainStepKind.PendingCrop:
				return step.BeforeValue == operation.PendingCropBefore
					&& step.AfterValue == operation.PendingCropAfter;
			case KingdomGrowthDomainStepKind.SubsidenceSchedule:
				return step.BeforeValue == operation.SubsidenceBefore
					&& step.AfterValue == operation.SubsidenceAfter;
			case KingdomGrowthDomainStepKind.Enrollment:
			case KingdomGrowthDomainStepKind.Roster:
			case KingdomGrowthDomainStepKind.Creed:
				if (operation.Action == KingdomGrowthAction.Arrival)
					return step.AfterValue == step.BeforeValue + 1L;
				return step.BeforeValue > 0L && step.AfterValue == step.BeforeValue - 1L;
			case KingdomGrowthDomainStepKind.Field:
			case KingdomGrowthDomainStepKind.CropRegistry:
			case KingdomGrowthDomainStepKind.Scarcity:
			case KingdomGrowthDomainStepKind.Accounting:
				return step.BeforeValue < long.MaxValue
					&& step.AfterValue == step.BeforeValue + 1L;
			default: return false;
			}
		}

		private static bool GrowthLeaseShape(KingdomLifecycleResourceLease lease,
			string operationId, bool publication)
		{
			long after;
			return lease != null && ValidGeneratedId(operationId)
				&& string.Equals(lease.OperationId, operationId, StringComparison.Ordinal)
				&& GrowthResourceKindAllowed(lease.Kind) && ValidRootId(lease.ScopeId)
				&& ValidRootId(lease.SubjectId)
				&& string.Equals(lease.Key, ResourceKey(lease.Kind, lease.ScopeId,
					lease.SubjectId), StringComparison.Ordinal)
				&& lease.Delta != 0L && CheckedAdd(lease.Before, lease.Delta, out after)
				&& after == lease.After && lease.BeforeRevision >= 0L
				&& lease.BeforeRevision < long.MaxValue
				&& lease.AfterRevision == lease.BeforeRevision + 1L
				&& Enum.IsDefined(typeof(KingdomLifecycleLeaseState), lease.State)
				&& (!publication || lease.State == KingdomLifecycleLeaseState.Prepared);
		}

		private static bool GrowthResourceKindAllowed(KingdomLifecycleResourceKind kind)
		{
			return kind == KingdomLifecycleResourceKind.Population
				|| kind == KingdomLifecycleResourceKind.Roster
				|| kind == KingdomLifecycleResourceKind.OriginRoster
				|| kind == KingdomLifecycleResourceKind.CreedRoster
				|| kind == KingdomLifecycleResourceKind.WaterVessel
				|| kind == KingdomLifecycleResourceKind.Object
				|| kind == KingdomLifecycleResourceKind.Projection
				|| kind == KingdomLifecycleResourceKind.GrowthClock
				|| kind == KingdomLifecycleResourceKind.GrowthPendingCrop
				|| kind == KingdomLifecycleResourceKind.GrowthField
				|| kind == KingdomLifecycleResourceKind.GrowthHealth
				|| kind == KingdomLifecycleResourceKind.GrowthScarcity
				|| kind == KingdomLifecycleResourceKind.GrowthAccounting
				|| kind == KingdomLifecycleResourceKind.GrowthCropRegistry
				|| kind == KingdomLifecycleResourceKind.GrowthSubsidenceSchedule
				|| kind == KingdomLifecycleResourceKind.GrowthPorterJob
				|| kind == KingdomLifecycleResourceKind.GrowthEscrowRelease
				|| kind == KingdomLifecycleResourceKind.GrowthArrivalCandidate;
		}

		private static bool GrowthResourceShape(KingdomLifecycleResourceRevision row)
		{
			return row != null && GrowthResourceKindAllowed(row.Kind) && ValidRootId(row.ScopeId)
				&& ValidRootId(row.SubjectId) && row.Revision >= 0L
				&& string.Equals(row.Key, ResourceKey(row.Kind, row.ScopeId, row.SubjectId),
					StringComparison.Ordinal)
				&& (row.ActiveOperationId == null
					|| ValidGeneratedId(row.ActiveOperationId))
				&& (row.LastOperationId == null
					|| ValidGeneratedId(row.LastOperationId));
		}

		private static bool GrowthPrefixShape(KingdomGrowthOperation operation, bool publication)
		{
			if (!publication && operation.Phase == KingdomGrowthPhase.Quarantined)
				return GrowthWaterPrefix(operation.WaterLegs, operation.WaterCursor, false)
					&& GrowthObjectPrefix(operation.Sources, operation.SourceCursor, false)
					&& GrowthObjectPrefix(operation.Outputs, operation.OutputCursor, false)
					&& GrowthDomainPrefix(operation.DomainSteps, operation.DomainCursor, false)
					&& (operation.ClockState == KingdomLifecyclePhysicalState.Prepared
						&& operation.ClockLease.State == KingdomLifecycleLeaseState.Prepared
						|| operation.ClockState == KingdomLifecyclePhysicalState.Intent
							&& operation.ClockLease.State == KingdomLifecycleLeaseState.Intent
						|| operation.ClockState == KingdomLifecyclePhysicalState.Proved
							&& operation.ClockLease.State == KingdomLifecycleLeaseState.Proved);
			int current = GrowthPhaseIndex(operation, operation.Phase);
			if (current < 0 || !GrowthWaterPhaseShape(operation, current, publication)
				|| !GrowthObjectPhaseShape(operation, operation.Sources, operation.SourceCursor,
					KingdomGrowthPhase.SourceIntent, KingdomGrowthPhase.SourcesSettled,
					current, publication)
				|| !GrowthObjectPhaseShape(operation, operation.Outputs, operation.OutputCursor,
					KingdomGrowthPhase.OutputIntent, KingdomGrowthPhase.OutputsSettled,
					current, publication)
				|| !GrowthDomainPhaseShape(operation, current, publication))
				return false;
			if (publication) return operation.ClockState == KingdomLifecyclePhysicalState.Prepared
				&& operation.ClockLease.State == KingdomLifecycleLeaseState.Prepared;
			if (operation.Phase == KingdomGrowthPhase.Sinks
				|| operation.Phase == KingdomGrowthPhase.Terminal)
				return operation.ClockState == KingdomLifecyclePhysicalState.Proved
					&& operation.ClockLease.State == KingdomLifecycleLeaseState.Proved
					&& (operation.Phase != KingdomGrowthPhase.Terminal
						|| GrowthOutboxTerminal(operation));
			if (operation.Phase == KingdomGrowthPhase.ClockIntent)
				return operation.ClockState == KingdomLifecyclePhysicalState.Prepared
					&& operation.ClockLease.State == KingdomLifecycleLeaseState.Prepared
					|| operation.ClockState == KingdomLifecyclePhysicalState.Intent
						&& operation.ClockLease.State == KingdomLifecycleLeaseState.Intent
					|| operation.ClockState == KingdomLifecyclePhysicalState.Proved
						&& operation.ClockLease.State == KingdomLifecycleLeaseState.Proved;
			return operation.ClockState == KingdomLifecyclePhysicalState.Prepared
				&& operation.ClockLease.State == KingdomLifecycleLeaseState.Prepared;
		}

		private static bool GrowthWaterPhaseShape(KingdomGrowthOperation operation,
			int current, bool publication)
		{
			int intent = GrowthPhaseIndex(operation, KingdomGrowthPhase.WaterIntent);
			int settled = GrowthPhaseIndex(operation, KingdomGrowthPhase.WaterSettled);
			if (intent < 0) return operation.WaterLegs.Count == 0 && operation.WaterCursor == 0;
			if (current < intent || publication) return operation.WaterCursor == 0
				&& GrowthWaterPrefix(operation.WaterLegs, 0, true);
			if (current == intent) return GrowthWaterPrefix(operation.WaterLegs,
				operation.WaterCursor, false);
			return current >= settled && operation.WaterCursor == operation.WaterLegs.Count
				&& GrowthWaterPrefix(operation.WaterLegs, operation.WaterCursor, false);
		}

		private static bool GrowthObjectPhaseShape(KingdomGrowthOperation operation,
			List<KingdomGrowthObjectLeg> rows, int cursor, KingdomGrowthPhase intentPhase,
			KingdomGrowthPhase settledPhase, int current, bool publication)
		{
			int intent = GrowthPhaseIndex(operation, intentPhase);
			int settled = GrowthPhaseIndex(operation, settledPhase);
			if (intent < 0) return rows.Count == 0 && cursor == 0;
			if (current < intent || publication) return cursor == 0
				&& GrowthObjectPrefix(rows, 0, true);
			if (current == intent) return GrowthObjectPrefix(rows, cursor, false);
			return current >= settled && cursor == rows.Count && GrowthObjectPrefix(rows, cursor, false);
		}

		private static bool GrowthDomainPhaseShape(KingdomGrowthOperation operation,
			int current, bool publication)
		{
			int intent = GrowthPhaseIndex(operation, KingdomGrowthPhase.DomainIntent);
			int settled = GrowthPhaseIndex(operation, KingdomGrowthPhase.DomainSettled);
			if (current < intent || publication) return operation.DomainCursor == 0
				&& GrowthDomainPrefix(operation.DomainSteps, 0, true);
			if (current == intent) return GrowthDomainPrefix(operation.DomainSteps,
				operation.DomainCursor, false);
			return current >= settled && operation.DomainCursor == operation.DomainSteps.Count
				&& GrowthDomainPrefix(operation.DomainSteps, operation.DomainCursor, false);
		}

		private static bool GrowthWaterPrefix(List<KingdomGrowthWaterLeg> rows, int cursor,
			bool publication)
		{
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomLifecyclePhysicalState expected = i < cursor
					? KingdomLifecyclePhysicalState.Proved : KingdomLifecyclePhysicalState.Prepared;
				if (i == cursor && !publication && rows[i].State == KingdomLifecyclePhysicalState.Intent)
					expected = KingdomLifecyclePhysicalState.Intent;
				if (rows[i].State != expected) return false;
			}
			return !publication || cursor == 0;
		}

	}
}
