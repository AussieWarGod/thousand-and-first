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
		private static bool GrowthOperationShape(KingdomGrowthBook book,
			KingdomGrowthOperation operation, KingdomGrowthSlotKind slot, string fieldId,
			bool publication)
		{
			if (book == null || operation == null || !KnownGrowthAction(operation.Action)
				|| !KnownGrowthPhase(operation.Phase) || operation.Sequence <= 0L
				|| operation.CreatedTick < 0L || operation.UpdatedTick < operation.CreatedTick
				|| !string.Equals(operation.SettlementId, book.SettlementId, StringComparison.Ordinal)
				|| !string.Equals(operation.FieldId, fieldId, StringComparison.Ordinal)
				|| (slot == KingdomGrowthSlotKind.Field ? !ValidRootId(operation.FieldId)
					: operation.FieldId != null)
				|| !string.Equals(operation.Id, GrowthOperationId(book.SettlementId, slot,
					fieldId, operation.Sequence), StringComparison.Ordinal)
				|| operation.Phase != KingdomGrowthPhase.Quarantined
					&& GrowthPhaseIndex(operation, operation.Phase) < 0
				|| operation.WaterLegs == null || operation.WaterLegs.Count > MaxWaterLegs
				|| operation.Sources == null || operation.Sources.Count > MaxGrowthSources
				|| operation.Outputs == null || operation.Outputs.Count > MaxGrowthOutputs
				|| operation.DomainSteps == null || operation.DomainSteps.Count > MaxResourceLeases
				|| operation.WaterCursor < 0 || operation.WaterCursor > operation.WaterLegs.Count
				|| operation.SourceCursor < 0 || operation.SourceCursor > operation.Sources.Count
				|| operation.OutputCursor < 0 || operation.OutputCursor > operation.Outputs.Count
				|| operation.DomainCursor < 0 || operation.DomainCursor > operation.DomainSteps.Count
				|| !KnownPhysical(operation.ClockState)
				|| operation.ClockLease == null || !GrowthLeaseShape(operation.ClockLease,
					operation.Id, publication) || operation.ClockLease.Kind !=
					KingdomLifecycleResourceKind.GrowthClock || TooLong(operation.Fault, MaxTextChars)
				|| !GrowthOperationScalarsValid(book, operation, slot, fieldId))
				return false;
			if (slot == KingdomGrowthSlotKind.Heartbeat && operation.Action != KingdomGrowthAction.Heartbeat
				|| slot == KingdomGrowthSlotKind.Arrival && operation.Action != KingdomGrowthAction.Arrival
				|| slot == KingdomGrowthSlotKind.Departure && operation.Action != KingdomGrowthAction.Departure
				|| slot == KingdomGrowthSlotKind.Delivery && operation.Action != KingdomGrowthAction.Delivery
				|| slot == KingdomGrowthSlotKind.Fetch && operation.Action != KingdomGrowthAction.Fetch
				|| slot == KingdomGrowthSlotKind.Mill && operation.Action != KingdomGrowthAction.Mill
				|| slot == KingdomGrowthSlotKind.Field && !IsGrowthFieldAction(operation.Action))
				return false;
			if (!GrowthTargetShape(operation, slot) || !GrowthPrefixShape(operation, publication)
				|| !GrowthOutboxShape(operation, publication)) return false;
			HashSet<string> events = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> leaseKeys = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> waterContainers = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> objectIds = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> markers = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < operation.WaterLegs.Count; i++)
				if (!GrowthWaterShape(operation, operation.WaterLegs[i], i, publication)
					|| !events.Add(operation.WaterLegs[i].EventId)
					|| !waterContainers.Add(operation.WaterLegs[i].ContainerId)
					|| !leaseKeys.Add(operation.WaterLegs[i].Lease.Key)) return false;
			for (int i = 0; i < operation.Sources.Count; i++)
				if (!GrowthObjectShape(operation, operation.Sources[i], i, false, publication)
					|| !events.Add(operation.Sources[i].EventId)
					|| !objectIds.Add(operation.Sources[i].ObjectId)
					|| !markers.Add(operation.Sources[i].Marker)
					|| !leaseKeys.Add(operation.Sources[i].Lease.Key)) return false;
			for (int i = 0; i < operation.Outputs.Count; i++)
				if (!GrowthObjectShape(operation, operation.Outputs[i], i, true, publication)
					|| !events.Add(operation.Outputs[i].EventId)
					|| (operation.Outputs[i].ObjectId != null
						&& !objectIds.Add(operation.Outputs[i].ObjectId))
					|| !markers.Add(operation.Outputs[i].Marker)
					|| !leaseKeys.Add(operation.Outputs[i].Lease.Key)) return false;
			for (int i = 0; i < operation.DomainSteps.Count; i++)
				if (!GrowthDomainShape(operation, operation.DomainSteps[i], i, publication)
					|| !events.Add(operation.DomainSteps[i].EventId)
					|| !leaseKeys.Add(operation.DomainSteps[i].Lease.Key)) return false;
			if (!leaseKeys.Add(operation.ClockLease.Key)
				|| !GrowthGroupsMatchAction(operation)
				|| !GrowthArrivalCandidateBindingShape(book, operation, publication)) return false;
			string hash;
			if (!TryGrowthPlanHash(operation, out hash)) return false;
			if (publication)
				return operation.Phase == KingdomGrowthPhase.Prepared
					&& operation.CreatedTick == operation.UpdatedTick
					&& operation.PlanHash == null
					&& operation.Fault == null
					&& operation.ClockState == KingdomLifecyclePhysicalState.Prepared;
			return string.Equals(operation.PlanHash, hash, StringComparison.Ordinal)
				&& (operation.Phase == KingdomGrowthPhase.Quarantined
					? !string.IsNullOrEmpty(operation.Fault) : operation.Fault == null);
		}

		public static bool GrowthPhaseAllowed(KingdomGrowthAction action, KingdomGrowthPhase phase)
		{
			if (!KnownGrowthAction(action) || !KnownGrowthPhase(phase)) return false;
			if (phase == KingdomGrowthPhase.Quarantined) return true;
			if (phase == KingdomGrowthPhase.Prepared || phase == KingdomGrowthPhase.DomainIntent
				|| phase == KingdomGrowthPhase.DomainSettled || phase == KingdomGrowthPhase.ClockIntent
				|| phase == KingdomGrowthPhase.Sinks || phase == KingdomGrowthPhase.Terminal) return true;
			if (phase == KingdomGrowthPhase.WaterIntent || phase == KingdomGrowthPhase.WaterSettled)
				return action == KingdomGrowthAction.Heartbeat || action == KingdomGrowthAction.Fetch
					|| action == KingdomGrowthAction.Arrival || action == KingdomGrowthAction.Sow;
			if (phase == KingdomGrowthPhase.SourceIntent || phase == KingdomGrowthPhase.SourcesSettled)
				return action == KingdomGrowthAction.Heartbeat || action == KingdomGrowthAction.Departure
					|| action == KingdomGrowthAction.Mill || action == KingdomGrowthAction.Sow
					|| action == KingdomGrowthAction.Withdraw || action == KingdomGrowthAction.Ripen
					|| action == KingdomGrowthAction.Harvest;
			if (phase == KingdomGrowthPhase.OutputIntent || phase == KingdomGrowthPhase.OutputsSettled)
				return action == KingdomGrowthAction.Arrival || action == KingdomGrowthAction.Delivery
					|| action == KingdomGrowthAction.Mill || action == KingdomGrowthAction.Sow
					|| action == KingdomGrowthAction.Withdraw || action == KingdomGrowthAction.Harvest;
			return false;
		}

		private static bool GrowthOperationScalarsValid(KingdomGrowthBook book,
			KingdomGrowthOperation operation, KingdomGrowthSlotKind slot, string fieldId)
		{
			int pending; int population;
			if (!KnownOption(operation.OptionState) || !KnownGrowthHealth(operation.HealthState)
				|| operation.OptionTick < 0L || operation.HealthTick < 0L
				|| operation.EffectiveWorkBefore < 0L || operation.EffectiveWorkAfter < 0L
				|| operation.HeartbeatBefore < 0L || operation.HeartbeatAfter < 0L
				|| operation.ArrivalBefore < 0L || operation.ArrivalAfter < 0L
				|| operation.FetchBefore < 0L || operation.FetchAfter < 0L
				|| operation.MillBefore < 0L || operation.MillAfter < 0L
				|| operation.SubsidenceBefore < 0L || operation.SubsidenceAfter < 0L
				|| operation.DeliveryBefore < 0L || operation.DeliveryAfter < 0L
				|| operation.DepartureBefore < 0L || operation.DepartureAfter < 0L
				|| operation.FieldClockBefore < 0L || operation.FieldClockAfter < 0L
				|| !ValidCount(operation.PendingCropBefore)
				|| !CheckedAdd(operation.PendingCropBefore, operation.PendingCropDelta, out pending)
				|| pending != operation.PendingCropAfter || !ValidCount(operation.PendingCropAfter)
				|| !CheckedAdd(operation.PopulationBefore, operation.PopulationDelta, out population)
				|| population != operation.PopulationAfter || !ValidCount(operation.PopulationBefore)
				|| !ValidCount(operation.PopulationAfter)
				|| TooLong(operation.PendingCropBlueprintBefore, MaxNameChars)
				|| TooLong(operation.PendingCropZoneIdBefore, MaxNameChars)
				|| TooLong(operation.PendingCropBlueprintAfter, MaxNameChars)
				|| TooLong(operation.PendingCropZoneIdAfter, MaxNameChars)
				|| (operation.PendingCropBefore == 0
					? operation.PendingCropBlueprintBefore != null
						|| operation.PendingCropZoneIdBefore != null
					: !ValidName(operation.PendingCropBlueprintBefore)
						|| !ValidName(operation.PendingCropZoneIdBefore))
				|| (operation.PendingCropAfter == 0
					? operation.PendingCropBlueprintAfter != null
						|| operation.PendingCropZoneIdAfter != null
					: !ValidName(operation.PendingCropBlueprintAfter)
						|| !ValidName(operation.PendingCropZoneIdAfter))
				|| !GrowthPendingTupleTransitionShape(operation)
				|| !GrowthHarvestOracleShape(operation)
				|| !GrowthHarvestAuthorityShape(book, operation)
				|| !GrowthFieldActionAuthorityShape(operation)
				|| !GrowthVariantScalarsValid(operation)) return false;
			long clockBefore = operation.ClockLease.Before;
			long clockAfter = operation.ClockLease.After;
			KingdomGrowthFieldSlot field = slot == KingdomGrowthSlotKind.Field
				? FindGrowthField(book, fieldId) : null;
			if (!string.Equals(operation.ClockLease.SubjectId,
				GrowthClockSubject(book.SettlementId, slot, fieldId), StringComparison.Ordinal)
				|| !string.Equals(operation.ClockLease.ScopeId, book.SettlementId,
					StringComparison.Ordinal)) return false;
			switch (operation.Action)
			{
			case KingdomGrowthAction.Heartbeat:
				return clockBefore == operation.HeartbeatBefore && clockAfter == operation.HeartbeatAfter
					&& operation.HeartbeatAfter > operation.HeartbeatBefore;
			case KingdomGrowthAction.Arrival:
				return clockBefore == operation.ArrivalBefore && clockAfter == operation.ArrivalAfter
					&& operation.ArrivalAfter > operation.ArrivalBefore;
			case KingdomGrowthAction.Departure:
				return clockBefore == operation.DepartureBefore
					&& clockAfter == operation.DepartureAfter
					&& operation.DepartureAfter > operation.DepartureBefore;
			case KingdomGrowthAction.Delivery:
				return clockBefore == operation.DeliveryBefore
					&& clockAfter == operation.DeliveryAfter
					&& operation.DeliveryAfter > operation.DeliveryBefore;
			case KingdomGrowthAction.Fetch:
				return clockBefore == operation.FetchBefore && clockAfter == operation.FetchAfter
					&& operation.FetchAfter > operation.FetchBefore;
			case KingdomGrowthAction.Mill:
				return clockBefore == operation.MillBefore && clockAfter == operation.MillAfter
					&& operation.MillAfter > operation.MillBefore;
			default:
				return IsGrowthFieldAction(operation.Action) && field != null
					&& clockBefore < long.MaxValue && clockBefore + 1L == clockAfter
					&& operation.FieldClockAfter >= operation.FieldClockBefore
					&& operation.EffectiveWorkAfter >= operation.EffectiveWorkBefore;
			}
		}

		private static bool GrowthPendingTupleTransitionShape(KingdomGrowthOperation operation)
		{
			if (operation.PendingCropDelta == 0)
				return string.Equals(operation.PendingCropBlueprintBefore,
					operation.PendingCropBlueprintAfter, StringComparison.Ordinal)
					&& string.Equals(operation.PendingCropZoneIdBefore,
						operation.PendingCropZoneIdAfter, StringComparison.Ordinal);
			if (operation.PendingCropBefore > 0 && operation.PendingCropAfter > 0)
				return string.Equals(operation.PendingCropBlueprintBefore,
					operation.PendingCropBlueprintAfter, StringComparison.Ordinal)
					&& string.Equals(operation.PendingCropZoneIdBefore,
						operation.PendingCropZoneIdAfter, StringComparison.Ordinal);
			return true;
		}

		private static bool GrowthHarvestOracleShape(KingdomGrowthOperation operation)
		{
			const int baselineMethodPercent = 100;
			if (operation.Action != KingdomGrowthAction.Harvest)
				return operation.HarvestStandingRows == 0 && operation.HarvestRipeRows == 0
					&& operation.HarvestCycles == 0 && !operation.HarvestCountsRipeLast
					&& operation.HarvestEffectivenessPercent == 0
					&& operation.HarvestMethodPercent == 0
					&& operation.HarvestFirstOrdinal == 0UL
					&& operation.HarvestCropBlueprint == null
					&& operation.HarvestSeedBlueprint == null;
			return operation.HarvestStandingRows > 0
				&& operation.HarvestRipeRows >= 0
				&& operation.HarvestRipeRows <= operation.HarvestStandingRows
				&& operation.HarvestCycles > 0
				&& operation.HarvestEffectivenessPercent > 0
				&& operation.HarvestEffectivenessPercent <= 100
				&& operation.HarvestMethodPercent >= baselineMethodPercent
				&& operation.HarvestMethodPercent <= KingdomResearchRules.MaxMethodPercent
				&& ValidName(operation.HarvestCropBlueprint)
				&& (operation.HarvestSeedBlueprint == null
					|| ValidName(operation.HarvestSeedBlueprint));
		}

		private static bool GrowthHarvestAuthorityShape(KingdomGrowthBook book,
			KingdomGrowthOperation operation)
		{
			if (operation.Action != KingdomGrowthAction.Harvest) return true;
			KingdomGrowthDomainStep registry = FindGrowthDomain(operation,
				KingdomGrowthDomainStepKind.CropRegistry);
			KingdomGrowthDomainStep fieldStep = FindGrowthDomain(operation,
				KingdomGrowthDomainStepKind.Field);
			if (registry == null || fieldStep == null || registry.CropRowsBefore == null
				|| registry.CropRowsDeclaredAfter == null || fieldStep.FieldBefore == null
				|| fieldStep.FieldAfter == null) return false;
			KingdomGrowthFieldState before = fieldStep.FieldBefore;
			KingdomGrowthFieldState after = fieldStep.FieldAfter;
			if (!string.Equals(operation.TargetId, before.WorkObjectId, StringComparison.Ordinal)
				|| !string.Equals(operation.TargetMarker, before.Marker, StringComparison.Ordinal)
				|| !string.Equals(operation.Blueprint, before.Blueprint, StringComparison.Ordinal)
				|| !string.Equals(operation.ZoneId, before.ZoneId, StringComparison.Ordinal)
				|| operation.TargetX != before.X || operation.TargetY != before.Y
				|| !string.Equals(operation.HarvestCropBlueprint, before.CropBlueprint,
					StringComparison.Ordinal)
				|| !string.Equals(operation.HarvestSeedBlueprint, before.SeedBlueprint,
					StringComparison.Ordinal)
				|| operation.HarvestEffectivenessPercent != before.EffectivenessPercent
				|| operation.HarvestMethodPercent != before.MethodPercent
				|| operation.HarvestFirstOrdinal != (ulong)(uint)before.Cycles
				|| after.Cycles - before.Cycles != operation.HarvestCycles) return false;
			int standing = 0; int ripe = 0;
			HashSet<string> mutated = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < registry.CropRowsBefore.Count; i++)
			{
				KingdomGrowthCropRow row = registry.CropRowsBefore[i];
				if (!string.Equals(row.FieldId, operation.FieldId, StringComparison.Ordinal)) continue;
				standing++; if (row.Ripe) ripe++;
				KingdomGrowthCropRow changed = FindGrowthCropRow(registry.CropRowsDeclaredAfter,
					row.RowId);
				KingdomGrowthObjectLeg leg = FindGrowthObjectLeg(operation.Sources, row.ObjectId,
					row.Marker);
				if (changed == null || leg == null || !mutated.Add(row.RowId)
					|| !GrowthHarvestableMutationMatches(row, changed, leg)) return false;
			}
			for (int i = 0; i < registry.CropRowsBefore.Count; i++)
			{
				KingdomGrowthCropRow row = registry.CropRowsBefore[i];
				if (string.Equals(row.FieldId, operation.FieldId, StringComparison.Ordinal)) continue;
				KingdomGrowthCropRow afterRow = FindGrowthCropRow(
					registry.CropRowsDeclaredAfter, row.RowId);
				if (!GrowthCropRowEquals(row, afterRow)) return false;
			}
			int expectedRipe = operation.HarvestCountsRipeLast ? ripe : standing;
			return standing > 0
				&& registry.CropRowsBefore.Count == registry.CropRowsDeclaredAfter.Count
				&& operation.Sources.Count == standing
				&& operation.HarvestStandingRows == standing
				&& operation.HarvestRipeRows == expectedRipe;
		}

	}
}
