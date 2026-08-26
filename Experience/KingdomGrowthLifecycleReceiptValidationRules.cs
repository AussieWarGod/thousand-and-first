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
		private static bool GrowthObjectReceiptShape(KingdomGrowthOperation operation,
			KingdomGrowthObjectLeg leg, int ordinal, bool output, bool publication)
		{
			if (publication || leg.State == KingdomLifecyclePhysicalState.Prepared)
				return leg.State == KingdomLifecyclePhysicalState.Prepared
					&& leg.ReceiptState == KingdomLifecyclePhysicalState.Prepared
					&& leg.ReceiptBeforeIdMatches == -1 && leg.ReceiptBeforeMarkerMatches == -1
					&& leg.ReceiptBeforeCount == -1 && leg.ReceiptAfterIdMatches == -1
					&& leg.ReceiptAfterMarkerMatches == -1 && leg.ReceiptAfterCount == -1
					&& GrowthObjectReceiptHashesEmpty(leg) && leg.ReceiptProofId == null;
			int beforeMatches = output && leg.MutationKind == KingdomGrowthObjectMutationKind.Create
				? 0 : 1;
			if (leg.State == KingdomLifecyclePhysicalState.Intent)
				return leg.ReceiptState == KingdomLifecyclePhysicalState.Intent
					&& leg.ReceiptBeforeIdMatches == beforeMatches
					&& leg.ReceiptBeforeMarkerMatches == beforeMatches
					&& leg.ReceiptBeforeCount == leg.BeforeCount
					&& leg.ReceiptAfterIdMatches == -1 && leg.ReceiptAfterMarkerMatches == -1
					&& leg.ReceiptAfterCount == -1 && GrowthObjectReceiptBeforeExact(leg)
					&& GrowthObjectReceiptAfterEmpty(leg) && leg.ReceiptProofId == null;
			int afterMatches = leg.AfterCount == 0 ? 0 : 1;
			return leg.State == KingdomLifecyclePhysicalState.Proved
				&& leg.ReceiptState == KingdomLifecyclePhysicalState.Proved
				&& leg.ReceiptBeforeIdMatches == beforeMatches
				&& leg.ReceiptBeforeMarkerMatches == beforeMatches
				&& leg.ReceiptBeforeCount == leg.BeforeCount
				&& leg.ReceiptAfterIdMatches == afterMatches
				&& leg.ReceiptAfterMarkerMatches == afterMatches
				&& leg.ReceiptAfterCount == leg.AfterCount
				&& GrowthObjectReceiptBeforeExact(leg) && GrowthObjectReceiptAfterExact(leg)
				&& string.Equals(leg.ReceiptCallbackObjectId, leg.ObjectId, StringComparison.Ordinal)
				&& string.Equals(leg.ReceiptCallbackMarker, leg.Marker, StringComparison.Ordinal)
				&& GrowthWitnessHash(leg.ReceiptCallbackReferenceHash)
				&& leg.ReceiptSameReference
				&& string.Equals(leg.ReceiptProofId,
					GrowthObjectReceiptProof(operation, leg, ordinal, output), StringComparison.Ordinal);
		}

		private static bool GrowthObjectReceiptHashesEmpty(KingdomGrowthObjectLeg leg)
		{
			return leg.ReceiptBeforeOwnerGraphHash == null
				&& leg.ReceiptAfterOwnerGraphHash == null
				&& leg.ReceiptBeforeObjectGraphHash == null
				&& leg.ReceiptAfterObjectGraphHash == null
				&& leg.ReceiptBeforeTopologyHash == null
				&& leg.ReceiptAfterTopologyHash == null
				&& leg.ReceiptCallbackObjectId == null
				&& leg.ReceiptCallbackMarker == null
				&& leg.ReceiptCallbackReferenceHash == null
				&& !leg.ReceiptSameReference;
		}

		private static bool GrowthObjectReceiptBeforeExact(KingdomGrowthObjectLeg leg)
		{
			return string.Equals(leg.ReceiptBeforeOwnerGraphHash, leg.BeforeOwnerGraphHash,
				StringComparison.Ordinal) && string.Equals(leg.ReceiptBeforeObjectGraphHash,
				leg.BeforeObjectGraphHash, StringComparison.Ordinal)
				&& string.Equals(leg.ReceiptBeforeTopologyHash, leg.BeforeTopologyHash,
					StringComparison.Ordinal);
		}

		private static bool GrowthObjectReceiptAfterEmpty(KingdomGrowthObjectLeg leg)
		{
			return leg.ReceiptAfterOwnerGraphHash == null
				&& leg.ReceiptAfterObjectGraphHash == null
				&& leg.ReceiptAfterTopologyHash == null
				&& leg.ReceiptCallbackObjectId == null
				&& leg.ReceiptCallbackMarker == null
				&& leg.ReceiptCallbackReferenceHash == null
				&& !leg.ReceiptSameReference;
		}

		private static bool GrowthObjectReceiptAfterExact(KingdomGrowthObjectLeg leg)
		{
			return string.Equals(leg.ReceiptAfterOwnerGraphHash, leg.AfterOwnerGraphHash,
				StringComparison.Ordinal) && string.Equals(leg.ReceiptAfterObjectGraphHash,
				leg.AfterObjectGraphHash, StringComparison.Ordinal)
				&& string.Equals(leg.ReceiptAfterTopologyHash, leg.AfterTopologyHash,
					StringComparison.Ordinal);
		}

		private static bool GrowthDomainShape(KingdomGrowthOperation operation,
			KingdomGrowthDomainStep step, int ordinal, bool publication)
		{
			KingdomLifecycleResourceKind kind;
			if (step == null || !TryGrowthDomainKind(step.Kind, step.CallbackKind, out kind)
				|| !GrowthWitnessHash(step.CallbackBodyHash)
				|| !string.Equals(step.EventId, ChildId(operation.Id, "domain", ordinal),
					StringComparison.Ordinal)
				|| !ValidRootId(step.ActorId) || !ValidRootId(step.SubjectId)
				|| !GrowthWitnessHash(step.BeforeGraphHash) || !GrowthWitnessHash(step.AfterGraphHash)
				|| !GrowthWitnessHash(step.BeforeMapHash) || !GrowthWitnessHash(step.AfterMapHash)
				|| string.Equals(step.BeforeMapHash, step.AfterMapHash, StringComparison.Ordinal)
				|| !string.Equals(step.ReceiptId, ChildId(operation.Id, "domain-receipt", ordinal),
					StringComparison.Ordinal)
				|| !GrowthLeaseShape(step.Lease, operation.Id, publication)
				|| step.Lease.Kind != kind || !string.Equals(step.Lease.SubjectId, step.SubjectId,
					StringComparison.Ordinal) || step.Lease.Before != step.BeforeValue
				|| step.Lease.After != step.AfterValue || !KnownPhysical(step.State)
				|| !KnownPhysical(step.ReceiptState)
				|| !GrowthDomainSnapshotsShape(operation, step)) return false;
			if (publication || step.State == KingdomLifecyclePhysicalState.Prepared)
				return step.State == KingdomLifecyclePhysicalState.Prepared
					&& step.ReceiptState == KingdomLifecyclePhysicalState.Prepared
					&& step.Lease.State == KingdomLifecycleLeaseState.Prepared
					&& step.ReceiptBeforeValue == 0L && step.ReceiptAfterValue == 0L
					&& GrowthDomainReceiptHashesEmpty(step)
					&& step.ReceiptProofId == null;
			if (step.State == KingdomLifecyclePhysicalState.Intent)
				return step.ReceiptState == KingdomLifecyclePhysicalState.Intent
					&& step.Lease.State == KingdomLifecycleLeaseState.Intent
					&& step.ReceiptBeforeValue == step.BeforeValue
					&& step.ReceiptAfterValue == 0L && GrowthDomainReceiptBeforeExact(step)
					&& GrowthDomainReceiptAfterEmpty(step) && step.ReceiptProofId == null;
			return step.State == KingdomLifecyclePhysicalState.Proved
				&& step.ReceiptState == KingdomLifecyclePhysicalState.Proved
				&& step.Lease.State == KingdomLifecycleLeaseState.Proved
				&& step.ReceiptBeforeValue == step.BeforeValue
				&& step.ReceiptAfterValue == step.AfterValue
				&& GrowthDomainReceiptBeforeExact(step) && GrowthDomainReceiptAfterExact(step)
				&& string.Equals(step.ReceiptProofId,
					GrowthDomainReceiptProof(operation, step, ordinal), StringComparison.Ordinal);
		}

		private static bool GrowthDomainSnapshotsShape(KingdomGrowthOperation operation,
			KingdomGrowthDomainStep step)
		{
			if (step.Kind == KingdomGrowthDomainStepKind.Scarcity)
				return GrowthScarcitySnapshotShape(step.ScarcityBefore)
					&& GrowthScarcitySnapshotShape(step.ScarcityAfter)
					&& GrowthScarcityTransitionShape(operation, step.ScarcityBefore,
						step.ScarcityAfter)
					&& step.AccountingBefore == null && step.AccountingAfter == null
					&& GrowthTypedDomainSnapshotsNull(step);
			if (step.Kind == KingdomGrowthDomainStepKind.Accounting)
				return GrowthAccountingSnapshotShape(step.AccountingBefore)
					&& GrowthAccountingSnapshotShape(step.AccountingAfter)
					&& GrowthAccountingTransitionShape(operation, step.AccountingBefore,
						step.AccountingAfter)
					&& step.ScarcityBefore == null && step.ScarcityAfter == null
					&& GrowthTypedDomainSnapshotsNull(step);
			if (step.Kind == KingdomGrowthDomainStepKind.Field)
				return step.ScarcityBefore == null && step.ScarcityAfter == null
					&& step.AccountingBefore == null && step.AccountingAfter == null
					&& GrowthFieldStateShape(step.FieldBefore, operation.FieldId)
					&& GrowthFieldStateShape(step.FieldAfter, operation.FieldId)
					&& step.CropRowsBefore == null && step.CropRowsDeclaredAfter == null
					&& step.CropRowsAfter == null;
			if (step.Kind == KingdomGrowthDomainStepKind.CropRegistry)
			{
				bool proved = step.State == KingdomLifecyclePhysicalState.Proved;
				return step.ScarcityBefore == null && step.ScarcityAfter == null
					&& step.AccountingBefore == null && step.AccountingAfter == null
					&& step.FieldBefore == null && step.FieldAfter == null
					&& GrowthCropRowsShape(step.CropRowsBefore, operation.FieldId, false,
						operation)
					&& GrowthCropRowsShape(step.CropRowsDeclaredAfter, operation.FieldId, true,
						operation)
					&& (proved ? GrowthCropDeclarationMatchesObserved(operation,
						step.CropRowsDeclaredAfter, step.CropRowsAfter)
						: step.CropRowsAfter == null);
			}
			return step.ScarcityBefore == null && step.ScarcityAfter == null
				&& step.AccountingBefore == null && step.AccountingAfter == null
				&& GrowthTypedDomainSnapshotsNull(step);
		}

		private static bool GrowthTypedDomainSnapshotsNull(KingdomGrowthDomainStep step)
		{
			return step.FieldBefore == null && step.FieldAfter == null
				&& step.CropRowsBefore == null && step.CropRowsDeclaredAfter == null
				&& step.CropRowsAfter == null;
		}

		private static bool GrowthScarcitySnapshotShape(KingdomGrowthScarcitySnapshot x)
		{
			int provedRations;
			if (x == null || x.DryStreak < 0 || x.HungerStreak < 0
				|| !Enum.IsDefined(typeof(KingdomRules.MealVerdict), x.LastMeal)
				|| x.MealShade < 0
				|| x.ElapsedTicks < 0L || x.Days < 0 || x.Population < 0
				|| !Enum.IsDefined(typeof(GrowthStage), x.Stage)
				|| x.UpkeepRequested < 0 || x.WaterAvailable < 0 || x.RationsAvailable < 0
				|| x.Foraged < 0 || x.Eaten < 0 || x.FromDish < 0 || x.FromDish > x.Eaten
				|| x.Kitchens < 0 || TooLong(x.DishName, MaxTextChars)
				|| TooLong(x.DishText, MaxTextChars) || TooLong(x.DishStaple, MaxTextChars)
				|| TooLong(x.DishSource, MaxTextChars)
				|| x.RequestedWater < 0 || x.ProvedWater < 0
				|| x.RequestedRations < 0 || x.ProvedRations < 0
				|| x.ProvedWater > x.RequestedWater || x.ProvedRations > x.RequestedRations
				|| x.Foraged > x.RequestedRations
				|| !CheckedAdd(x.Foraged, x.Eaten, out provedRations)
				|| x.ProvedRations != Math.Min(x.RequestedRations, provedRations)
				|| !Enum.IsDefined(typeof(KingdomRules.StoresPolicy), x.StoresPolicy)
				|| x.DistrictPercent < 0 || x.DistrictPercent > 100
				|| !Enum.IsDefined(typeof(KingdomGrowthComposedBite), x.ComposedBite)
				|| !Enum.IsDefined(typeof(KingdomGrowthThirstOutcome), x.ThirstOutcome)
				|| !Enum.IsDefined(typeof(KingdomGrowthHungerOutcome), x.HungerOutcome))
				return false;
			bool thirsting = x.ThirstOutcome != KingdomGrowthThirstOutcome.Sustained;
			bool starving = x.HungerOutcome != KingdomGrowthHungerOutcome.Fed;
			bool withering = x.ThirstOutcome == KingdomGrowthThirstOutcome.Withering;
			bool famishing = x.HungerOutcome == KingdomGrowthHungerOutcome.Famine;
			KingdomGrowthComposedBite bite = (KingdomGrowthComposedBite)Math.Max(
				GrowthThirstBite(x.ThirstOutcome), GrowthHungerBite(x.HungerOutcome));
			bool healthy = !thirsting && !starving;
			return x.Thirsting == thirsting && x.Starving == starving
				&& x.Withering == withering && x.Famishing == famishing
				&& x.Healthy == healthy && x.ComposedBite == bite;
		}

		private static bool GrowthScarcityTransitionShape(KingdomGrowthOperation operation,
			KingdomGrowthScarcitySnapshot before, KingdomGrowthScarcitySnapshot after)
		{
			if (operation == null || operation.Action != KingdomGrowthAction.Heartbeat
				|| before == null || after == null || !GrowthScarcityInputsEqual(before, after)
				|| before.Population != operation.PopulationBefore
				|| after.Population != operation.PopulationBefore) return false;
			int water;
			int food;
			if (!GrowthWaterQuantity(operation, KingdomGrowthWaterMutationKind.Drain, out water)
				|| !GrowthRemovedObjectQuantity(operation, true, out food)
				|| after.ProvedWater != water || after.Eaten != food) return false;
			bool enabled = operation.ScarcityOptionState == KingdomLifecycleOptionState.Enabled;
			if (!enabled)
			{
				if (operation.ScarcityOptionState != KingdomLifecycleOptionState.Disabled
					|| after.RequestedWater != 0 || after.ProvedWater != 0
					|| after.RequestedRations != 0 || after.ProvedRations != 0
					|| after.Foraged != 0 || after.Eaten != 0 || after.FromDish != 0
					|| after.ThirstOutcome != KingdomGrowthThirstOutcome.Sustained
					|| after.HungerOutcome != KingdomGrowthHungerOutcome.Fed) return false;
			}
			else
			{
				GrowthStage stage = (GrowthStage)after.Stage;
				int upkeep = KingdomRules.PolicyUpkeepForElapsed(after.Population,
					after.ElapsedTicks, (KingdomRules.StoresPolicy)after.StoresPolicy, stage);
				long districtUpkeep = (long)upkeep * after.DistrictPercent / 100L;
				int rations = KingdomRules.RationsForElapsed(after.Population, after.ElapsedTicks);
				if (districtUpkeep < 0L || districtUpkeep > int.MaxValue
					|| after.UpkeepRequested != (int)districtUpkeep
					|| after.RequestedWater != after.UpkeepRequested
					|| after.RequestedRations != rations
					|| after.ProvedWater > after.WaterAvailable
					|| after.Eaten > after.RationsAvailable
					|| after.Eaten > after.RequestedRations - after.Foraged) return false;
			}
			if (after.Days != KingdomRules.ElapsedDays(after.ElapsedTicks)) return false;
			bool waterPaid = after.ProvedWater == after.RequestedWater;
			bool foodPaid = after.ProvedRations == after.RequestedRations;
			int dryAfter = waterPaid ? 0 : before.DryStreak + 1;
			int hungerAfter = foodPaid ? 0 : before.HungerStreak + 1;
			if (dryAfter < 0 || hungerAfter < 0) return false;
			KingdomGrowthThirstOutcome thirst = waterPaid
				? KingdomGrowthThirstOutcome.Sustained
				: (KingdomGrowthThirstOutcome)KingdomRules.ResolveThirst(dryAfter,
					(GrowthStage)after.Stage, after.Population);
			KingdomGrowthHungerOutcome hunger = foodPaid
				? KingdomGrowthHungerOutcome.Fed
				: (KingdomGrowthHungerOutcome)KingdomRules.ResolveHunger(hungerAfter,
					(GrowthStage)after.Stage, after.Population);
			KingdomRules.MealVerdict meal = KingdomRules.JudgeMeal(after.RequestedRations,
				after.FromDish, after.Eaten, after.Kitchens > 0, (GrowthStage)after.Stage);
			return after.DryStreak == dryAfter && after.HungerStreak == hungerAfter
				&& after.Withered == (!waterPaid && (before.Withered
					|| thirst == KingdomGrowthThirstOutcome.Withering))
				&& after.Famished == (!foodPaid && (before.Famished
					|| hunger == KingdomGrowthHungerOutcome.Famine))
				&& after.ThirstOutcome == thirst && after.HungerOutcome == hunger
				&& after.LastMeal == meal && after.MealShade == KingdomRules.MealShadeFor(meal)
				&& after.ScrapsAnnounced == (meal == KingdomRules.MealVerdict.Scraps);
		}

	}
}
