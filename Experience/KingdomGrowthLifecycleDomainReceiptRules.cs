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
		private static bool GrowthScarcityInputsEqual(KingdomGrowthScarcitySnapshot a,
			KingdomGrowthScarcitySnapshot b)
		{
			return a.ElapsedTicks == b.ElapsedTicks && a.Days == b.Days
				&& a.Population == b.Population && a.Stage == b.Stage
				&& a.UpkeepRequested == b.UpkeepRequested
				&& a.WaterAvailable == b.WaterAvailable
				&& a.RationsAvailable == b.RationsAvailable && a.Foraged == b.Foraged
				&& a.Eaten == b.Eaten && a.FromDish == b.FromDish && a.Kitchens == b.Kitchens
				&& string.Equals(a.DishName, b.DishName, StringComparison.Ordinal)
				&& string.Equals(a.DishText, b.DishText, StringComparison.Ordinal)
				&& string.Equals(a.DishStaple, b.DishStaple, StringComparison.Ordinal)
				&& string.Equals(a.DishSource, b.DishSource, StringComparison.Ordinal)
				&& a.RequestedWater == b.RequestedWater && a.ProvedWater == b.ProvedWater
				&& a.RequestedRations == b.RequestedRations
				&& a.ProvedRations == b.ProvedRations && a.StoresPolicy == b.StoresPolicy
				&& a.DistrictPercent == b.DistrictPercent;
		}

		private static int GrowthThirstBite(KingdomGrowthThirstOutcome value)
		{
			return value == KingdomGrowthThirstOutcome.Withering ? 3
				: value == KingdomGrowthThirstOutcome.Emigration ? 2
					: value == KingdomGrowthThirstOutcome.Warned ? 1 : 0;
		}

		private static int GrowthHungerBite(KingdomGrowthHungerOutcome value)
		{
			return value == KingdomGrowthHungerOutcome.Famine ? 3
				: value == KingdomGrowthHungerOutcome.Emigration ? 2
					: value == KingdomGrowthHungerOutcome.Warned ? 1 : 0;
		}

		private static bool GrowthAccountingSnapshotShape(KingdomGrowthAccountingSnapshot x)
		{
			return x != null && x.Fetched >= 0L && x.UpkeepDrawn >= 0L
				&& x.ArrivalCost >= 0L && x.Delivered >= 0L && x.Harvested >= 0L
				&& x.Foraged >= 0L && x.RationsDrawn >= 0L && x.Milled >= 0L
				&& x.HarvestLost >= 0L && x.Plundered >= 0L && x.Arrivals >= 0L
				&& x.Departures >= 0L;
		}

		private static bool GrowthAccountingTransitionShape(KingdomGrowthOperation operation,
			KingdomGrowthAccountingSnapshot before, KingdomGrowthAccountingSnapshot after)
		{
			if (operation == null || before == null || after == null) return false;
			int fetched = 0, upkeep = 0, arrivalCost = 0, delivered = 0, harvested = 0;
			int foraged = 0, rations = 0, milled = 0, harvestLost = 0;
			int plundered = 0, arrivals = 0, departures = 0;
			int quantity;
			switch (operation.Action)
			{
			case KingdomGrowthAction.Heartbeat:
				KingdomGrowthDomainStep scarcity = FindGrowthDomain(operation,
					KingdomGrowthDomainStepKind.Scarcity);
				if (scarcity == null || scarcity.ScarcityAfter == null) return false;
				upkeep = scarcity.ScarcityAfter.ProvedWater;
				foraged = scarcity.ScarcityAfter.Foraged;
				rations = scarcity.ScarcityAfter.Eaten;
				departures = operation.PopulationDelta < 0 ? -operation.PopulationDelta : 0;
				break;
			case KingdomGrowthAction.Fetch:
				if (!GrowthWaterQuantity(operation, KingdomGrowthWaterMutationKind.Fill,
					out fetched)) return false;
				break;
			case KingdomGrowthAction.Mill:
				int ground;
				int stored;
				if (!GrowthRemovedObjectQuantity(operation, false, out ground)
					|| !GrowthAddedObjectQuantity(operation, null, out stored)) return false;
				long made = (long)ground * KingdomRules.PreserveMultiple;
				if (made > int.MaxValue || stored > made) return false;
				milled = Math.Max(0, stored - ground);
				harvestLost = (int)made - stored;
				break;
			case KingdomGrowthAction.Arrival:
				if (operation.ArrivalDisposition != KingdomGrowthArrivalDisposition.Joined)
					return false;
				if (!GrowthWaterQuantity(operation, KingdomGrowthWaterMutationKind.Drain,
					out arrivalCost)) return false;
				arrivals = operation.PopulationDelta;
				break;
			case KingdomGrowthAction.Departure:
				departures = -operation.PopulationDelta;
				break;
			case KingdomGrowthAction.Delivery:
				if (!GrowthAddedObjectQuantity(operation, null, out delivered)) return false;
				break;
			case KingdomGrowthAction.Harvest:
				if (!GrowthAddedObjectQuantity(operation, operation.HarvestCropBlueprint,
					out quantity) || operation.PendingCropDelta < 0
					|| !CheckedAdd(quantity, operation.PendingCropDelta, out harvested)) return false;
				int yield = GrowthHarvestExpectedYield(operation);
				if (yield < harvested) return false;
				harvestLost = yield - harvested;
				break;
			default: return false;
			}
			return GrowthAccountingDelta(before.Fetched, after.Fetched, fetched)
				&& GrowthAccountingDelta(before.UpkeepDrawn, after.UpkeepDrawn, upkeep)
				&& GrowthAccountingDelta(before.ArrivalCost, after.ArrivalCost, arrivalCost)
				&& GrowthAccountingDelta(before.Delivered, after.Delivered, delivered)
				&& GrowthAccountingDelta(before.Harvested, after.Harvested, harvested)
				&& GrowthAccountingDelta(before.Foraged, after.Foraged, foraged)
				&& GrowthAccountingDelta(before.RationsDrawn, after.RationsDrawn, rations)
				&& GrowthAccountingDelta(before.Milled, after.Milled, milled)
				&& GrowthAccountingDelta(before.HarvestLost, after.HarvestLost, harvestLost)
				&& GrowthAccountingDelta(before.Plundered, after.Plundered, plundered)
				&& GrowthAccountingDelta(before.Arrivals, after.Arrivals, arrivals)
				&& GrowthAccountingDelta(before.Departures, after.Departures, departures);
		}

		private static bool GrowthAccountingDelta(int before, int after, int delta)
		{
			int expected;
			return delta >= 0 && CheckedAdd(before, delta, out expected) && after == expected;
		}

		private static bool TryGrowthDomainKind(KingdomGrowthDomainStepKind stepKind,
			KingdomGrowthDomainCallbackKind callbackKind,
			out KingdomLifecycleResourceKind resourceKind)
		{
			resourceKind = KingdomLifecycleResourceKind.None;
			switch (stepKind)
			{
			case KingdomGrowthDomainStepKind.Enrollment:
				if (callbackKind != KingdomGrowthDomainCallbackKind.Enroll) return false;
				resourceKind = KingdomLifecycleResourceKind.OriginRoster; return true;
			case KingdomGrowthDomainStepKind.Roster:
				if (callbackKind != KingdomGrowthDomainCallbackKind.RosterAdd
					&& callbackKind != KingdomGrowthDomainCallbackKind.RosterRemove) return false;
				resourceKind = KingdomLifecycleResourceKind.Roster; return true;
			case KingdomGrowthDomainStepKind.Creed:
				if (callbackKind != KingdomGrowthDomainCallbackKind.CreedSet) return false;
				resourceKind = KingdomLifecycleResourceKind.CreedRoster; return true;
			case KingdomGrowthDomainStepKind.Population:
				if (callbackKind != KingdomGrowthDomainCallbackKind.PopulationAdjust) return false;
				resourceKind = KingdomLifecycleResourceKind.Population; return true;
			case KingdomGrowthDomainStepKind.PendingCrop:
				if (callbackKind != KingdomGrowthDomainCallbackKind.PendingCropSet) return false;
				resourceKind = KingdomLifecycleResourceKind.GrowthPendingCrop; return true;
			case KingdomGrowthDomainStepKind.Field:
				if (callbackKind != KingdomGrowthDomainCallbackKind.FieldSet) return false;
				resourceKind = KingdomLifecycleResourceKind.GrowthField; return true;
			case KingdomGrowthDomainStepKind.Scarcity:
				if (callbackKind != KingdomGrowthDomainCallbackKind.ScarcitySet) return false;
				resourceKind = KingdomLifecycleResourceKind.GrowthScarcity; return true;
			case KingdomGrowthDomainStepKind.Accounting:
				if (callbackKind != KingdomGrowthDomainCallbackKind.AccountingSet) return false;
				resourceKind = KingdomLifecycleResourceKind.GrowthAccounting; return true;
			case KingdomGrowthDomainStepKind.CropRegistry:
				if (callbackKind != KingdomGrowthDomainCallbackKind.CropRegistrySet) return false;
				resourceKind = KingdomLifecycleResourceKind.GrowthCropRegistry; return true;
			case KingdomGrowthDomainStepKind.SubsidenceSchedule:
				if (callbackKind != KingdomGrowthDomainCallbackKind.SubsidenceScheduleSet) return false;
				resourceKind = KingdomLifecycleResourceKind.GrowthSubsidenceSchedule; return true;
			case KingdomGrowthDomainStepKind.PorterJob:
				if (callbackKind != KingdomGrowthDomainCallbackKind.PorterJobSet) return false;
				resourceKind = KingdomLifecycleResourceKind.GrowthPorterJob; return true;
			case KingdomGrowthDomainStepKind.EscrowRelease:
				if (callbackKind != KingdomGrowthDomainCallbackKind.EscrowRelease) return false;
				resourceKind = KingdomLifecycleResourceKind.GrowthEscrowRelease; return true;
			default: return false;
			}
		}

		private static bool GrowthDomainReceiptHashesEmpty(KingdomGrowthDomainStep step)
		{
			return step.ReceiptBeforeGraphHash == null
				&& step.ReceiptAfterGraphHash == null
				&& step.ReceiptBeforeMapHash == null
				&& step.ReceiptAfterMapHash == null;
		}

		private static bool GrowthDomainReceiptBeforeExact(KingdomGrowthDomainStep step)
		{
			return string.Equals(step.ReceiptBeforeGraphHash, step.BeforeGraphHash,
				StringComparison.Ordinal) && string.Equals(step.ReceiptBeforeMapHash,
				step.BeforeMapHash, StringComparison.Ordinal);
		}

		private static bool GrowthDomainReceiptAfterEmpty(KingdomGrowthDomainStep step)
		{
			return step.ReceiptAfterGraphHash == null
				&& step.ReceiptAfterMapHash == null;
		}

		private static bool GrowthDomainReceiptAfterExact(KingdomGrowthDomainStep step)
		{
			return string.Equals(step.ReceiptAfterGraphHash, step.AfterGraphHash,
				StringComparison.Ordinal) && string.Equals(step.ReceiptAfterMapHash,
				step.AfterMapHash, StringComparison.Ordinal);
		}

	}
}
