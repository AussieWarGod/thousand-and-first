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
		private static bool GrowthObjectPipelineShape(KingdomGrowthObjectLeg leg, bool publication)
		{
			if (leg.Callbacks == null || leg.Callbacks.Count == 0
				|| leg.Callbacks.Count > MaxGrowthObjectCallbacks || leg.CallbackCursor < 0
				|| leg.CallbackCursor > leg.Callbacks.Count
				|| leg.BeforeLocation == KingdomGrowthLocationKind.None
				|| leg.AfterLocation == KingdomGrowthLocationKind.None) return false;
			KingdomGrowthObjectCallbackStep first = leg.Callbacks[0];
			KingdomGrowthObjectCallbackStep last = leg.Callbacks[leg.Callbacks.Count - 1];
			if (first == null || last == null || first.Kind != leg.MutationKind
				|| first.FromLocation != leg.BeforeLocation || last.ToLocation != leg.AfterLocation
				|| first.BeforeCount != leg.BeforeCount || last.AfterCount != leg.AfterCount
				|| first.NoStack != leg.NoStack || last.NoStack != leg.NoStack
				|| !string.Equals(first.BeforeOwnerGraphHash, leg.BeforeOwnerGraphHash,
					StringComparison.Ordinal)
				|| !string.Equals(last.AfterOwnerGraphHash, leg.AfterOwnerGraphHash,
					StringComparison.Ordinal)
				|| !string.Equals(first.BeforeObjectGraphHash, leg.BeforeObjectGraphHash,
					StringComparison.Ordinal)
				|| !string.Equals(last.AfterObjectGraphHash, leg.AfterObjectGraphHash,
					StringComparison.Ordinal)
				|| !string.Equals(first.BeforeTopologyHash, leg.BeforeTopologyHash,
					StringComparison.Ordinal)
				|| !string.Equals(last.AfterTopologyHash, leg.AfterTopologyHash,
					StringComparison.Ordinal)) return false;
			for (int i = 0; i < leg.Callbacks.Count; i++)
			{
				KingdomGrowthObjectCallbackStep step = leg.Callbacks[i];
				if (!GrowthObjectCallbackStepShape(step, leg.EventId, leg.ObjectId, leg.Marker, i)
					|| (i > 0 && (leg.Callbacks[i - 1].ToLocation != step.FromLocation
						|| leg.Callbacks[i - 1].AfterCount != step.BeforeCount
						|| !GrowthOptionalWitnessChain(leg.Callbacks[i - 1].AfterOwnerGraphHash,
							step.BeforeOwnerGraphHash)
						|| !GrowthOptionalWitnessChain(leg.Callbacks[i - 1].AfterObjectGraphHash,
							step.BeforeObjectGraphHash)
						|| !GrowthOptionalWitnessChain(leg.Callbacks[i - 1].AfterTopologyHash,
							step.BeforeTopologyHash)))) return false;
				KingdomLifecyclePhysicalState expected = i < leg.CallbackCursor
					? KingdomLifecyclePhysicalState.Proved : KingdomLifecyclePhysicalState.Prepared;
				if (i == leg.CallbackCursor && !publication
					&& step.State == KingdomLifecyclePhysicalState.Intent)
					expected = KingdomLifecyclePhysicalState.Intent;
				if (step.State != expected) return false;
			}
			if (publication) return leg.CallbackCursor == 0
				&& leg.State == KingdomLifecyclePhysicalState.Prepared;
			if (leg.State == KingdomLifecyclePhysicalState.Prepared)
				return leg.CallbackCursor == 0;
			if (leg.State == KingdomLifecyclePhysicalState.Intent)
				return leg.CallbackCursor < leg.Callbacks.Count;
			return leg.State == KingdomLifecyclePhysicalState.Proved
				&& leg.CallbackCursor == leg.Callbacks.Count;
		}

		private static bool GrowthOptionalWitnessChain(string left, string right)
		{
			return left == null && right == null
				|| left != null && string.Equals(left, right, StringComparison.Ordinal);
		}

		private static bool GrowthObjectCallbackStepShape(KingdomGrowthObjectCallbackStep step,
			string parentId, string objectId, string marker, int ordinal,
			bool allowAbsentReference = false)
		{
			if (step == null || !string.Equals(step.EventId,
				ChildId(parentId, "object-callback", ordinal), StringComparison.Ordinal)
				|| !Enum.IsDefined(typeof(KingdomGrowthObjectMutationKind), step.Kind)
				|| step.Kind == KingdomGrowthObjectMutationKind.None
				|| !GrowthLocationShape(step.FromLocation, step.BeforeOwnerId, step.BeforeZoneId,
					step.BeforeX, step.BeforeY)
				|| !GrowthLocationShape(step.ToLocation, step.AfterOwnerId, step.AfterZoneId,
					step.AfterX, step.AfterY)
				|| ((step.FromLocation == KingdomGrowthLocationKind.Escrow
					|| step.ToLocation == KingdomGrowthLocationKind.Escrow)
					? !ValidRootId(step.EscrowKey) : step.EscrowKey != null)
				|| !ValidCount(step.BeforeCount) || !ValidCount(step.AfterCount)
				|| !KnownPhysical(step.State) || !KnownPhysical(step.ReceiptState)
				|| !string.Equals(step.ReceiptId,
					ChildId(parentId, "object-callback-receipt", ordinal), StringComparison.Ordinal))
				return false;
			bool createPending = step.Kind == KingdomGrowthObjectMutationKind.Create
				&& step.State != KingdomLifecyclePhysicalState.Proved;
			bool deferredPrepared = step.State == KingdomLifecyclePhysicalState.Prepared
				&& step.Kind != KingdomGrowthObjectMutationKind.Create
				&& step.BeforeOwnerGraphHash == null
				&& step.BeforeObjectGraphHash == null && step.BeforeTopologyHash == null
				&& GrowthOptionalWitnessSet(step.AfterOwnerGraphHash,
					step.AfterObjectGraphHash, step.AfterTopologyHash);
			if (createPending)
			{
				if (!GrowthWitnessHash(step.BeforeOwnerGraphHash)
					|| !GrowthWitnessHash(step.BeforeObjectGraphHash)
					|| !GrowthWitnessHash(step.BeforeTopologyHash)
					|| step.AfterOwnerGraphHash != null || step.AfterObjectGraphHash != null
					|| step.AfterTopologyHash != null || objectId != null) return false;
			}
			else if (!deferredPrepared && (!GrowthWitnessHash(step.BeforeOwnerGraphHash)
				|| !GrowthWitnessHash(step.AfterOwnerGraphHash)
				|| !GrowthWitnessHash(step.BeforeObjectGraphHash)
				|| !GrowthWitnessHash(step.AfterObjectGraphHash)
				|| !GrowthWitnessHash(step.BeforeTopologyHash)
				|| !GrowthWitnessHash(step.AfterTopologyHash))) return false;
			bool cropMutation = step.Kind == KingdomGrowthObjectMutationKind.HarvestableRipeSet;
			if (cropMutation)
			{
				if (step.FromLocation != step.ToLocation || step.FromLocation != KingdomGrowthLocationKind.Cell
					|| step.BeforeCount != step.AfterCount || step.BeforeCount <= 0
					|| !step.BeforeHasHarvestable || !step.AfterHasHarvestable
					|| step.BeforeRipe == step.AfterRipe
					|| step.BeforeRegenTimer < 0 || step.AfterRegenTimer < 0
					|| !string.Equals(step.BeforeRegenTime, string.Empty, StringComparison.Ordinal)
					|| !string.Equals(step.AfterRegenTime, string.Empty, StringComparison.Ordinal)
					|| step.BeforeTileIndex < -1 || step.AfterTileIndex < -1
					|| !GrowthBoundedPresentString(step.BeforeRenderTile)
					|| !GrowthBoundedPresentString(step.AfterRenderTile)
					|| !GrowthBoundedPresentString(step.BeforeRenderColor)
					|| !GrowthBoundedPresentString(step.AfterRenderColor)
					|| !GrowthBoundedPresentString(step.BeforeRenderDetail)
					|| !GrowthBoundedPresentString(step.AfterRenderDetail)
					|| !GrowthBoundedPresentString(step.BeforeRenderString)
					|| !GrowthBoundedPresentString(step.AfterRenderString)
					|| !GrowthBoundedPresentString(step.BeforeTileColor)
					|| !GrowthBoundedPresentString(step.AfterTileColor)) return false;
			}
			else if (step.BeforeHasHarvestable || step.AfterHasHarvestable
				|| step.BeforeRipe || step.AfterRipe
				|| step.BeforeRegenTimer != 0 || step.AfterRegenTimer != 0
				|| step.BeforeRegenTime != null || step.AfterRegenTime != null
				|| step.BeforeTileIndex != 0 || step.AfterTileIndex != 0
				|| step.BeforeRenderTile != null || step.AfterRenderTile != null
				|| step.BeforeRenderColor != null || step.AfterRenderColor != null
				|| step.BeforeRenderDetail != null || step.AfterRenderDetail != null
				|| step.BeforeRenderString != null || step.AfterRenderString != null
				|| step.BeforeTileColor != null || step.AfterTileColor != null) return false;
			if (!GrowthObjectCallbackTransition(step)) return false;
			if (step.State == KingdomLifecyclePhysicalState.Prepared)
				return step.ReceiptState == KingdomLifecyclePhysicalState.Prepared
					&& step.ReceiptBeforeMatches == -1 && step.ReceiptAfterMatches == -1
					&& step.ReceiptBeforeCount == -1 && step.ReceiptAfterCount == -1
					&& GrowthObjectCallbackReceiptEmpty(step);
			if (step.State == KingdomLifecyclePhysicalState.Intent)
				return step.ReceiptState == KingdomLifecyclePhysicalState.Intent
					&& step.ReceiptBeforeMatches == (step.BeforeCount == 0 ? 0 : 1)
					&& step.ReceiptBeforeCount == step.BeforeCount
					&& step.ReceiptAfterMatches == -1 && step.ReceiptAfterCount == -1
					&& GrowthObjectCallbackReceiptBeforeExact(step)
					&& step.ReceiptAfterOwnerGraphHash == null
					&& step.ReceiptAfterObjectGraphHash == null
					&& step.ReceiptAfterTopologyHash == null
					&& step.ReceiptCallbackObjectId == null
					&& step.ReceiptCallbackMarker == null
					&& step.ReceiptCallbackReferenceHash == null && !step.ReceiptSameReference
					&& step.ReceiptProofId == null;
			return step.State == KingdomLifecyclePhysicalState.Proved
				&& step.ReceiptState == KingdomLifecyclePhysicalState.Proved
				&& step.ReceiptBeforeMatches == (step.BeforeCount == 0 ? 0 : 1)
				&& step.ReceiptAfterMatches == (step.AfterCount == 0 ? 0 : 1)
				&& step.ReceiptBeforeCount == step.BeforeCount
				&& step.ReceiptAfterCount == step.AfterCount
				&& string.Equals(step.ReceiptCallbackObjectId, objectId, StringComparison.Ordinal)
				&& string.Equals(step.ReceiptCallbackMarker, marker, StringComparison.Ordinal)
				&& GrowthWitnessHash(step.ReceiptCallbackReferenceHash)
				&& (step.ReceiptSameReference || allowAbsentReference
					&& step.Kind == KingdomGrowthObjectMutationKind.Obliterate
					&& step.AfterCount == 0)
				&& GrowthObjectCallbackReceiptBeforeExact(step)
				&& string.Equals(step.ReceiptAfterOwnerGraphHash, step.AfterOwnerGraphHash,
					StringComparison.Ordinal)
				&& string.Equals(step.ReceiptAfterObjectGraphHash, step.AfterObjectGraphHash,
					StringComparison.Ordinal)
				&& string.Equals(step.ReceiptAfterTopologyHash, step.AfterTopologyHash,
					StringComparison.Ordinal)
				&& ValidGeneratedId(step.ReceiptProofId);
		}

		private static bool GrowthBoundedPresentString(string value)
		{
			return value != null && !TooLong(value, MaxNameChars);
		}

		private static bool GrowthObjectCallbackTransition(KingdomGrowthObjectCallbackStep step)
		{
			switch (step.Kind)
			{
			case KingdomGrowthObjectMutationKind.Create:
				return step.FromLocation == KingdomGrowthLocationKind.Absent
					&& step.ToLocation == KingdomGrowthLocationKind.Escrow
					&& step.BeforeCount == 0 && step.AfterCount > 0 && step.NoStack;
			case KingdomGrowthObjectMutationKind.CellAdd:
				return step.FromLocation == KingdomGrowthLocationKind.Escrow
					&& step.ToLocation == KingdomGrowthLocationKind.Cell
					&& step.BeforeCount == step.AfterCount && step.BeforeCount > 0 && step.NoStack;
			case KingdomGrowthObjectMutationKind.InventoryAdd:
			case KingdomGrowthObjectMutationKind.Receive:
				return step.FromLocation == KingdomGrowthLocationKind.Escrow
					&& step.ToLocation == KingdomGrowthLocationKind.Inventory
					&& step.BeforeCount == step.AfterCount && step.BeforeCount > 0 && step.NoStack;
			case KingdomGrowthObjectMutationKind.DestroyOne:
				return step.BeforeCount > 0 && step.AfterCount == step.BeforeCount - 1
					&& (step.AfterCount == 0 ? step.ToLocation == KingdomGrowthLocationKind.Graveyard
						: step.ToLocation == step.FromLocation);
			case KingdomGrowthObjectMutationKind.Obliterate:
				return step.BeforeCount > 0 && step.AfterCount == 0
					&& step.ToLocation == KingdomGrowthLocationKind.Graveyard;
			case KingdomGrowthObjectMutationKind.HarvestableRipeSet:
				return true;
			default: return false;
			}
		}

		private static bool GrowthObjectCallbackReceiptEmpty(KingdomGrowthObjectCallbackStep step)
		{
			return step.ReceiptCallbackObjectId == null && step.ReceiptCallbackMarker == null
				&& step.ReceiptCallbackReferenceHash == null && !step.ReceiptSameReference
				&& step.ReceiptBeforeOwnerGraphHash == null && step.ReceiptAfterOwnerGraphHash == null
				&& step.ReceiptBeforeObjectGraphHash == null && step.ReceiptAfterObjectGraphHash == null
				&& step.ReceiptBeforeTopologyHash == null && step.ReceiptAfterTopologyHash == null
				&& step.ReceiptProofId == null;
		}

		private static bool GrowthObjectCallbackReceiptBeforeExact(
			KingdomGrowthObjectCallbackStep step)
		{
			return string.Equals(step.ReceiptBeforeOwnerGraphHash, step.BeforeOwnerGraphHash,
				StringComparison.Ordinal)
				&& string.Equals(step.ReceiptBeforeObjectGraphHash, step.BeforeObjectGraphHash,
					StringComparison.Ordinal)
				&& string.Equals(step.ReceiptBeforeTopologyHash, step.BeforeTopologyHash,
					StringComparison.Ordinal);
		}

		private static bool GrowthLocationShape(KingdomGrowthLocationKind location,
			string ownerId, string zoneId, int x, int y)
		{
			if (!Enum.IsDefined(typeof(KingdomGrowthLocationKind), location)
				|| location == KingdomGrowthLocationKind.None) return false;
			if (location == KingdomGrowthLocationKind.Cell)
				return ownerId == null && ValidName(zoneId) && x >= 0 && x <= MaxCoordinate
					&& y >= 0 && y <= MaxCoordinate;
			if (location == KingdomGrowthLocationKind.Inventory)
				return ValidRootId(ownerId) && ValidName(zoneId) && x == -1 && y == -1;
			return ownerId == null && zoneId == null && x == -1 && y == -1;
		}

		private static KingdomGrowthLocationKind GrowthLocationFromTopology(
			KingdomLifecycleTopology topology)
		{
			if (topology == KingdomLifecycleTopology.Cell) return KingdomGrowthLocationKind.Cell;
			if (topology == KingdomLifecycleTopology.Inventory)
				return KingdomGrowthLocationKind.Inventory;
			return KingdomGrowthLocationKind.None;
		}

	}
}
