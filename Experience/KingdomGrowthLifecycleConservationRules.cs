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
		private static bool GrowthDeliveryOutputsShape(KingdomGrowthOperation operation)
		{
			if (operation.PendingCropBefore <= 0 || operation.PendingCropDelta >= 0
				|| !ValidName(operation.PendingCropBlueprintBefore)
				|| !ValidName(operation.PendingCropZoneIdBefore)) return false;
			for (int i = 0; i < operation.Outputs.Count; i++)
			{
				KingdomGrowthObjectLeg output = operation.Outputs[i];
				if (!string.Equals(output.Blueprint, operation.PendingCropBlueprintBefore,
					StringComparison.Ordinal)
					|| !string.Equals(output.ZoneId, operation.PendingCropZoneIdBefore,
						StringComparison.Ordinal)) return false;
			}
			return true;
		}

		private static bool GrowthActionConservationShape(KingdomGrowthOperation operation)
		{
			int water;
			int removed;
			int added;
			switch (operation.Action)
			{
			case KingdomGrowthAction.Heartbeat:
				KingdomGrowthDomainStep scarcity = FindGrowthDomain(operation,
					KingdomGrowthDomainStepKind.Scarcity);
				return scarcity != null && scarcity.ScarcityAfter != null
					&& GrowthWaterQuantity(operation, KingdomGrowthWaterMutationKind.Drain,
						out water) && water == scarcity.ScarcityAfter.ProvedWater
					&& GrowthRemovedObjectQuantity(operation, true, out removed)
					&& removed == scarcity.ScarcityAfter.Eaten;
			case KingdomGrowthAction.Fetch:
				return GrowthFetchWaterShape(operation);
			case KingdomGrowthAction.Mill:
				if (!GrowthRemovedObjectQuantity(operation, false, out removed)
					|| !GrowthAddedObjectQuantity(operation, null, out added)) return false;
				return removed > 0 && added > 0
					&& (long)removed * KingdomRules.PreserveMultiple >= added;
			case KingdomGrowthAction.Arrival:
				if (operation.ArrivalDisposition != KingdomGrowthArrivalDisposition.Joined)
					return operation.PopulationDelta == 0;
				return operation.PopulationDelta == 1
					&& GrowthWaterQuantity(operation, KingdomGrowthWaterMutationKind.Drain,
						out water) && water == KingdomRules.DramsPerArrival;
			case KingdomGrowthAction.Departure:
				return operation.PopulationDelta == -1;
			case KingdomGrowthAction.Delivery:
				return operation.PendingCropDelta < 0
					&& GrowthAddedObjectQuantity(operation, null, out added)
					&& added == -operation.PendingCropDelta;
			case KingdomGrowthAction.Sow:
				return GrowthWaterQuantity(operation, KingdomGrowthWaterMutationKind.Drain,
						out water) && water == KingdomCropRules.PlantWaterCostDrams
					&& GrowthRemovedObjectQuantity(operation, false, out removed) && removed == 1
					&& GrowthAddedObjectQuantity(operation, null, out added) && added > 0;
			case KingdomGrowthAction.Withdraw:
				return GrowthRemovedObjectQuantity(operation, false, out removed) && removed > 0
					&& GrowthAddedObjectQuantity(operation, null, out added) && added <= 1;
			case KingdomGrowthAction.Ripen:
				return operation.PendingCropDelta == 0;
			case KingdomGrowthAction.Harvest:
				return GrowthHarvestConservationShape(operation);
			case KingdomGrowthAction.Irrigate:
				return operation.PendingCropDelta == 0;
			default: return false;
			}
		}

		private static bool GrowthHarvestConservationShape(KingdomGrowthOperation operation)
		{
			int crop = 0;
			int seed = 0;
			if (operation.PendingCropDelta < 0) return false;
			for (int i = 0; i < operation.Outputs.Count; i++)
			{
				KingdomGrowthObjectLeg leg = operation.Outputs[i];
				int quantity = leg.AfterCount - leg.BeforeCount;
				if (quantity < 0) return false;
				if (string.Equals(leg.Blueprint, operation.HarvestCropBlueprint,
					StringComparison.Ordinal))
				{
					if (!CheckedAdd(crop, quantity, out crop)) return false;
				}
				else if (operation.HarvestSeedBlueprint != null
					&& string.Equals(leg.Blueprint, operation.HarvestSeedBlueprint,
						StringComparison.Ordinal))
				{
					if (!CheckedAdd(seed, quantity, out seed)) return false;
				}
				else return false;
			}
			int yield = GrowthHarvestExpectedYield(operation);
			if (yield <= 0 || crop > yield || operation.PendingCropDelta > yield - crop) return false;
			int expectedSeeds = operation.HarvestSeedBlueprint == null ? 0
				: KingdomCropRules.SeedReturned(operation.SettlementId, operation.TargetId,
					operation.HarvestFirstOrdinal, operation.HarvestCycles, yield);
			return seed == expectedSeeds;
		}

		private static int GrowthHarvestExpectedYield(KingdomGrowthOperation operation)
		{
			if (!GrowthHarvestOracleShape(operation)) return -1;
			return KingdomCropRules.GatheredYield(operation.HarvestStandingRows,
				operation.HarvestRipeRows, operation.HarvestCycles,
				operation.HarvestCountsRipeLast, operation.HarvestEffectivenessPercent,
				operation.HarvestMethodPercent);
		}

		private static KingdomGrowthDomainStep FindGrowthDomain(KingdomGrowthOperation operation,
			KingdomGrowthDomainStepKind kind)
		{
			KingdomGrowthDomainStep found = null;
			if (operation == null || operation.DomainSteps == null) return null;
			for (int i = 0; i < operation.DomainSteps.Count; i++)
				if (operation.DomainSteps[i] != null && operation.DomainSteps[i].Kind == kind)
				{
					if (found != null) return null;
					found = operation.DomainSteps[i];
				}
			return found;
		}

		private static bool GrowthWaterQuantity(KingdomGrowthOperation operation,
			KingdomGrowthWaterMutationKind kind, out int quantity)
		{
			long total = 0L;
			quantity = 0;
			if (operation == null || operation.WaterLegs == null) return false;
			for (int i = 0; i < operation.WaterLegs.Count; i++)
				if (operation.WaterLegs[i] != null && operation.WaterLegs[i].MutationKind == kind)
				{
					total += operation.WaterLegs[i].Delta;
					if (total > int.MaxValue) return false;
				}
			quantity = (int)total;
			return true;
		}

		private static bool GrowthRemovedObjectQuantity(KingdomGrowthOperation operation,
			bool excludeTarget, out int quantity)
		{
			long total = 0L;
			quantity = 0;
			if (operation == null || operation.Sources == null) return false;
			for (int i = 0; i < operation.Sources.Count; i++)
			{
				KingdomGrowthObjectLeg leg = operation.Sources[i];
				if (leg == null || excludeTarget && string.Equals(leg.ObjectId,
					operation.TargetId, StringComparison.Ordinal)) continue;
				int removed = leg.BeforeCount - leg.AfterCount;
				if (removed < 0) return false;
				total += removed;
				if (total > int.MaxValue) return false;
			}
			quantity = (int)total;
			return true;
		}

		private static bool GrowthAddedObjectQuantity(KingdomGrowthOperation operation,
			string blueprint, out int quantity)
		{
			long total = 0L;
			quantity = 0;
			if (operation == null || operation.Outputs == null) return false;
			for (int i = 0; i < operation.Outputs.Count; i++)
			{
				KingdomGrowthObjectLeg leg = operation.Outputs[i];
				if (leg == null || blueprint != null && !string.Equals(leg.Blueprint, blueprint,
					StringComparison.Ordinal)) continue;
				int added = leg.AfterCount - leg.BeforeCount;
				if (added < 0) return false;
				total += added;
				if (total > int.MaxValue) return false;
			}
			quantity = (int)total;
			return true;
		}

		private static bool GrowthFetchWaterShape(KingdomGrowthOperation operation)
		{
			long drained = 0L; long filled = 0L; bool fillSeen = false;
			for (int i = 0; i < operation.WaterLegs.Count; i++)
			{
				KingdomGrowthWaterLeg leg = operation.WaterLegs[i];
				if (leg == null || leg.Delta <= 0) return false;
				if (leg.MutationKind == KingdomGrowthWaterMutationKind.Drain)
				{
					if (fillSeen || !CheckedAdd(drained, leg.Delta, out drained)) return false;
				}
				else if (leg.MutationKind == KingdomGrowthWaterMutationKind.Fill)
				{
					fillSeen = true;
					if (!CheckedAdd(filled, leg.Delta, out filled)) return false;
				}
				else return false;
			}
			return fillSeen && drained > 0L && drained == filled;
		}

		private static bool GrowthAllWaterKinds(KingdomGrowthOperation operation,
			KingdomGrowthWaterMutationKind kind)
		{
			for (int i = 0; i < operation.WaterLegs.Count; i++)
				if (operation.WaterLegs[i] == null || operation.WaterLegs[i].MutationKind != kind)
					return false;
			return true;
		}

		private static bool GrowthAllObjectKinds(List<KingdomGrowthObjectLeg> legs,
			KingdomGrowthObjectMutationKind kind)
		{
			for (int i = 0; i < legs.Count; i++)
				if (legs[i] == null || legs[i].MutationKind != kind) return false;
			return true;
		}

		private static bool GrowthAllObjectBlueprints(List<KingdomGrowthObjectLeg> legs,
			string blueprint)
		{
			if (!ValidName(blueprint)) return false;
			for (int i = 0; i < legs.Count; i++)
				if (legs[i] == null || !string.Equals(legs[i].Blueprint, blueprint,
					StringComparison.Ordinal)) return false;
			return true;
		}

	}
}
