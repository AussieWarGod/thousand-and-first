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
		internal static bool CommitGrowthWaterCallback(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, int Ordinal, string CallbackContainerId,
			string CallbackReferenceHash, bool SameReference,
			string ObservedAfterOwnerGraphHash, string ObservedAfterPartGraphHash,
			string ObservedAfterTopologyHash)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation)
				|| Operation.Phase != KingdomGrowthPhase.WaterIntent
				|| Ordinal != Operation.WaterCursor || Ordinal < 0
				|| Ordinal >= Operation.WaterLegs.Count
				|| !GrowthWitnessHash(CallbackReferenceHash) || !SameReference)
				return false;
			KingdomGrowthWaterLeg leg = Operation.WaterLegs[Ordinal];
			if (leg.State != KingdomLifecyclePhysicalState.Intent
				|| leg.ReceiptState != KingdomLifecyclePhysicalState.Intent
				|| leg.Lease.State != KingdomLifecycleLeaseState.Intent
				|| !string.Equals(CallbackContainerId, leg.ContainerId, StringComparison.Ordinal)
				|| !string.Equals(ObservedAfterOwnerGraphHash, leg.AfterOwnerGraphHash,
					StringComparison.Ordinal)
				|| !string.Equals(ObservedAfterPartGraphHash, leg.AfterPartGraphHash,
					StringComparison.Ordinal)
				|| !string.Equals(ObservedAfterTopologyHash, leg.AfterTopologyHash,
					StringComparison.Ordinal)) return false;
			KingdomLifecycleResourceRevision row = FindGrowthResource(Book, leg.Lease.Key);
			if (!GrowthResourceMatches(row, leg.Lease)
				|| row.Revision != leg.Lease.BeforeRevision
				|| !string.Equals(row.ActiveOperationId, Operation.Id,
					StringComparison.Ordinal)) return false;
			long oldRevision = row.Revision; string oldLast = row.LastOperationId;
			leg.State = KingdomLifecyclePhysicalState.Proved;
			leg.ReceiptState = KingdomLifecyclePhysicalState.Proved;
			leg.Lease.State = KingdomLifecycleLeaseState.Proved;
			leg.ReceiptAfterMatches = 1;
			leg.ReceiptAfterOwnerGraphHash = leg.AfterOwnerGraphHash;
			leg.ReceiptAfterPartGraphHash = leg.AfterPartGraphHash;
			leg.ReceiptAfterTopologyHash = leg.AfterTopologyHash;
			leg.ReceiptCallbackContainerId = CallbackContainerId;
			leg.ReceiptCallbackReferenceHash = CallbackReferenceHash;
			leg.ReceiptSameReference = true;
			leg.ReceiptProofId = GrowthWaterReceiptProof(Operation, leg, Ordinal);
			row.Revision = leg.Lease.AfterRevision; row.LastOperationId = Operation.Id;
			Operation.WaterCursor++;
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			Operation.WaterCursor--; row.Revision = oldRevision; row.LastOperationId = oldLast;
			leg.State = KingdomLifecyclePhysicalState.Intent;
			leg.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			leg.Lease.State = KingdomLifecycleLeaseState.Intent;
			leg.ReceiptAfterMatches = -1;
			leg.ReceiptAfterOwnerGraphHash = null;
			leg.ReceiptAfterPartGraphHash = null;
			leg.ReceiptAfterTopologyHash = null;
			leg.ReceiptCallbackContainerId = null;
			leg.ReceiptCallbackReferenceHash = null;
			leg.ReceiptSameReference = false; leg.ReceiptProofId = null;
			return false;
		}

		public static bool TryAppendGrowthObjectPlacement(KingdomGrowthOperation Operation,
			KingdomGrowthObjectLeg Leg,
			KingdomGrowthObjectMutationKind Kind, KingdomLifecycleTopology Topology, string OwnerId,
			string ZoneId, int X, int Y, string BeforeOwnerGraphHash,
			string AfterOwnerGraphHash, string BeforeObjectGraphHash, string AfterObjectGraphHash,
			string BeforeTopologyHash, string AfterTopologyHash)
		{
			if (Operation == null || Operation.Phase != KingdomGrowthPhase.Prepared
				|| Operation.PlanHash != null || Leg == null
				|| !string.Equals(Leg.OperationId, Operation.Id, StringComparison.Ordinal)
				|| Leg.State != KingdomLifecyclePhysicalState.Prepared
				|| Leg.Lease == null || Leg.Lease.State != KingdomLifecycleLeaseState.Prepared
				|| Leg.Callbacks == null || Leg.Callbacks.Count == 0
				|| Leg.Callbacks.Count >= MaxGrowthObjectCallbacks
				|| Leg.AfterLocation != KingdomGrowthLocationKind.Escrow
				|| (Kind != KingdomGrowthObjectMutationKind.CellAdd
					&& Kind != KingdomGrowthObjectMutationKind.InventoryAdd
					&& Kind != KingdomGrowthObjectMutationKind.Receive)) return false;
			KingdomGrowthLocationKind afterLocation = GrowthLocationFromTopology(Topology);
			int ordinal = Leg.Callbacks.Count;
			KingdomGrowthObjectCallbackStep step = new KingdomGrowthObjectCallbackStep
			{
				EventId = ChildId(Leg.EventId, "object-callback", ordinal), Kind = Kind,
				FromLocation = KingdomGrowthLocationKind.Escrow, ToLocation = afterLocation,
				EscrowKey = Leg.EscrowKey, AfterOwnerId = OwnerId, AfterZoneId = ZoneId,
				AfterX = X, AfterY = Y, BeforeCount = Leg.AfterCount,
				AfterCount = Leg.AfterCount, NoStack = Leg.NoStack,
				BeforeOwnerGraphHash = BeforeOwnerGraphHash,
				AfterOwnerGraphHash = AfterOwnerGraphHash,
				BeforeObjectGraphHash = BeforeObjectGraphHash,
				AfterObjectGraphHash = AfterObjectGraphHash,
				BeforeTopologyHash = BeforeTopologyHash, AfterTopologyHash = AfterTopologyHash,
				State = KingdomLifecyclePhysicalState.Prepared,
				ReceiptId = ChildId(Leg.EventId, "object-callback-receipt", ordinal),
				ReceiptState = KingdomLifecyclePhysicalState.Prepared
			};
			if (!GrowthObjectCallbackStepShape(step, Leg.EventId, Leg.ObjectId, Leg.Marker, ordinal))
				return false;
			Leg.Callbacks.Add(step); Leg.AfterLocation = afterLocation; Leg.Topology = Topology;
			Leg.OwnerId = OwnerId; Leg.ZoneId = ZoneId; Leg.X = X; Leg.Y = Y;
			Leg.AfterOwnerGraphHash = AfterOwnerGraphHash;
			Leg.AfterObjectGraphHash = AfterObjectGraphHash;
			Leg.AfterTopologyHash = AfterTopologyHash;
			Leg.ReceiptTopologyId = TopologyId(Topology, OwnerId, ZoneId, X, Y);
			return true;
		}

		public static KingdomGrowthObjectLeg PrepareGrowthHarvestableMutationLeg(
			KingdomGrowthBook Book, KingdomGrowthOperation Operation, string ObjectId,
			string Marker, string Blueprint,
			string ZoneId, int X, int Y, int Count, bool BeforeRipe, bool AfterRipe,
			int BeforeRegenTimer, int AfterRegenTimer, string BeforeRegenTime,
			string AfterRegenTime, int BeforeTileIndex, int AfterTileIndex,
			string BeforeRenderTile, string AfterRenderTile, string BeforeRenderColor,
			string AfterRenderColor, string BeforeRenderDetail, string AfterRenderDetail,
			string BeforeRenderString, string AfterRenderString, string BeforeTileColor,
			string AfterTileColor, string BeforeOwnerGraphHash, string AfterOwnerGraphHash,
			string BeforeObjectGraphHash, string AfterObjectGraphHash,
			string BeforeTopologyHash, string AfterTopologyHash)
		{
			KingdomGrowthObjectLeg leg = PrepareGrowthObjectLeg(Book, Operation, false,
				KingdomGrowthObjectMutationKind.HarvestableRipeSet, ObjectId, Marker, Blueprint,
				KingdomLifecycleTopology.Cell, null, ZoneId, X, Y, Count, 0, false,
				BeforeOwnerGraphHash, AfterOwnerGraphHash, BeforeObjectGraphHash,
				AfterObjectGraphHash, BeforeTopologyHash, AfterTopologyHash);
			if (leg == null || leg.Callbacks == null || leg.Callbacks.Count != 1) return null;
			leg.DetachedMarker = null;
			KingdomGrowthObjectCallbackStep step = leg.Callbacks[0];
			step.BeforeHasHarvestable = true; step.AfterHasHarvestable = true;
			step.BeforeRipe = BeforeRipe; step.AfterRipe = AfterRipe;
			step.BeforeRegenTimer = BeforeRegenTimer; step.AfterRegenTimer = AfterRegenTimer;
			step.BeforeRegenTime = BeforeRegenTime; step.AfterRegenTime = AfterRegenTime;
			step.BeforeTileIndex = BeforeTileIndex; step.AfterTileIndex = AfterTileIndex;
			step.BeforeRenderTile = BeforeRenderTile; step.AfterRenderTile = AfterRenderTile;
			step.BeforeRenderColor = BeforeRenderColor; step.AfterRenderColor = AfterRenderColor;
			step.BeforeRenderDetail = BeforeRenderDetail;
			step.AfterRenderDetail = AfterRenderDetail;
			step.BeforeRenderString = BeforeRenderString;
			step.AfterRenderString = AfterRenderString;
			step.BeforeTileColor = BeforeTileColor; step.AfterTileColor = AfterTileColor;
			return GrowthObjectCallbackStepShape(step, leg.EventId, leg.ObjectId, leg.Marker, 0)
				? leg : null;
		}

		internal static bool BeginGrowthObjectCallback(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, bool Output, int LegOrdinal,
			string BeforeOwnerGraphHash, string AfterOwnerGraphHash,
			string BeforeObjectGraphHash, string AfterObjectGraphHash,
			string BeforeTopologyHash, string AfterTopologyHash)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation)) return false;
			List<KingdomGrowthObjectLeg> list = Output ? Operation.Outputs : Operation.Sources;
			int cursor = Output ? Operation.OutputCursor : Operation.SourceCursor;
			KingdomGrowthPhase required = Output ? KingdomGrowthPhase.OutputIntent
				: KingdomGrowthPhase.SourceIntent;
			if (Operation.Phase != required || LegOrdinal != cursor || LegOrdinal < 0
				|| LegOrdinal >= list.Count) return false;
			KingdomGrowthObjectLeg leg = list[LegOrdinal];
			if (leg.State != KingdomLifecyclePhysicalState.Prepared
				&& leg.State != KingdomLifecyclePhysicalState.Intent
				|| leg.CallbackCursor < 0 || leg.CallbackCursor >= leg.Callbacks.Count) return false;
			KingdomGrowthObjectCallbackStep step = leg.Callbacks[leg.CallbackCursor];
			if (step.State != KingdomLifecyclePhysicalState.Prepared) return false;
			bool create = step.Kind == KingdomGrowthObjectMutationKind.Create;
			string oldStepBeforeOwner = step.BeforeOwnerGraphHash;
			string oldStepAfterOwner = step.AfterOwnerGraphHash;
			string oldStepBeforeObject = step.BeforeObjectGraphHash;
			string oldStepAfterObject = step.AfterObjectGraphHash;
			string oldStepBeforeTopology = step.BeforeTopologyHash;
			string oldStepAfterTopology = step.AfterTopologyHash;
			string oldLegAfterOwner = leg.AfterOwnerGraphHash;
			string oldLegAfterObject = leg.AfterObjectGraphHash;
			string oldLegAfterTopology = leg.AfterTopologyHash;
			KingdomLifecyclePhysicalState oldStepState = step.State;
			KingdomLifecyclePhysicalState oldStepReceiptState = step.ReceiptState;
			int oldStepBeforeMatches = step.ReceiptBeforeMatches;
			int oldStepBeforeCount = step.ReceiptBeforeCount;
			string oldStepReceiptBeforeOwner = step.ReceiptBeforeOwnerGraphHash;
			string oldStepReceiptBeforeObject = step.ReceiptBeforeObjectGraphHash;
			string oldStepReceiptBeforeTopology = step.ReceiptBeforeTopologyHash;
			KingdomLifecyclePhysicalState oldLegState = leg.State;
			KingdomLifecycleLeaseState oldLeaseState = leg.Lease.State;
			KingdomLifecyclePhysicalState oldLegReceiptState = leg.ReceiptState;
			int oldLegBeforeIdMatches = leg.ReceiptBeforeIdMatches;
			int oldLegBeforeMarkerMatches = leg.ReceiptBeforeMarkerMatches;
			int oldLegBeforeCount = leg.ReceiptBeforeCount;
			string oldLegReceiptBeforeOwner = leg.ReceiptBeforeOwnerGraphHash;
			string oldLegReceiptBeforeObject = leg.ReceiptBeforeObjectGraphHash;
			string oldLegReceiptBeforeTopology = leg.ReceiptBeforeTopologyHash;
			if (create)
			{
				if (BeforeOwnerGraphHash != null || AfterOwnerGraphHash != null
					|| BeforeObjectGraphHash != null || AfterObjectGraphHash != null
					|| BeforeTopologyHash != null || AfterTopologyHash != null) return false;
			}
			else
			{
				if (!GrowthWitnessHash(BeforeOwnerGraphHash)
					|| !GrowthWitnessHash(AfterOwnerGraphHash)
					|| !GrowthWitnessHash(BeforeObjectGraphHash)
					|| !GrowthWitnessHash(AfterObjectGraphHash)
					|| !GrowthWitnessHash(BeforeTopologyHash)
					|| !GrowthWitnessHash(AfterTopologyHash)) return false;
				if (step.BeforeOwnerGraphHash != null && (!string.Equals(
					step.BeforeOwnerGraphHash, BeforeOwnerGraphHash, StringComparison.Ordinal)
					|| !string.Equals(step.AfterOwnerGraphHash, AfterOwnerGraphHash,
						StringComparison.Ordinal)
					|| !string.Equals(step.BeforeObjectGraphHash, BeforeObjectGraphHash,
						StringComparison.Ordinal)
					|| !string.Equals(step.AfterObjectGraphHash, AfterObjectGraphHash,
						StringComparison.Ordinal)
					|| !string.Equals(step.BeforeTopologyHash, BeforeTopologyHash,
						StringComparison.Ordinal)
					|| !string.Equals(step.AfterTopologyHash, AfterTopologyHash,
						StringComparison.Ordinal))) return false;
				step.BeforeOwnerGraphHash = BeforeOwnerGraphHash;
				step.AfterOwnerGraphHash = AfterOwnerGraphHash;
				step.BeforeObjectGraphHash = BeforeObjectGraphHash;
				step.AfterObjectGraphHash = AfterObjectGraphHash;
				step.BeforeTopologyHash = BeforeTopologyHash;
				step.AfterTopologyHash = AfterTopologyHash;
				if (leg.CallbackCursor == leg.Callbacks.Count - 1)
				{
					leg.AfterOwnerGraphHash = AfterOwnerGraphHash;
					leg.AfterObjectGraphHash = AfterObjectGraphHash;
					leg.AfterTopologyHash = AfterTopologyHash;
				}
			}
			step.State = KingdomLifecyclePhysicalState.Intent;
			step.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			step.ReceiptBeforeMatches = step.BeforeCount == 0 ? 0 : 1;
			step.ReceiptBeforeCount = step.BeforeCount;
			step.ReceiptBeforeOwnerGraphHash = step.BeforeOwnerGraphHash;
			step.ReceiptBeforeObjectGraphHash = step.BeforeObjectGraphHash;
			step.ReceiptBeforeTopologyHash = step.BeforeTopologyHash;
			leg.State = KingdomLifecyclePhysicalState.Intent;
			leg.Lease.State = KingdomLifecycleLeaseState.Intent;
			leg.ReceiptState = KingdomLifecyclePhysicalState.Intent;
			leg.ReceiptBeforeIdMatches = Output
				&& leg.MutationKind == KingdomGrowthObjectMutationKind.Create ? 0 : 1;
			leg.ReceiptBeforeMarkerMatches = leg.ReceiptBeforeIdMatches;
			leg.ReceiptBeforeCount = leg.BeforeCount;
			leg.ReceiptBeforeOwnerGraphHash = leg.BeforeOwnerGraphHash;
			leg.ReceiptBeforeObjectGraphHash = leg.BeforeObjectGraphHash;
			leg.ReceiptBeforeTopologyHash = leg.BeforeTopologyHash;
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			step.BeforeOwnerGraphHash = oldStepBeforeOwner;
			step.AfterOwnerGraphHash = oldStepAfterOwner;
			step.BeforeObjectGraphHash = oldStepBeforeObject;
			step.AfterObjectGraphHash = oldStepAfterObject;
			step.BeforeTopologyHash = oldStepBeforeTopology;
			step.AfterTopologyHash = oldStepAfterTopology;
			leg.AfterOwnerGraphHash = oldLegAfterOwner;
			leg.AfterObjectGraphHash = oldLegAfterObject;
			leg.AfterTopologyHash = oldLegAfterTopology;
			step.State = oldStepState; step.ReceiptState = oldStepReceiptState;
			step.ReceiptBeforeMatches = oldStepBeforeMatches;
			step.ReceiptBeforeCount = oldStepBeforeCount;
			step.ReceiptBeforeOwnerGraphHash = oldStepReceiptBeforeOwner;
			step.ReceiptBeforeObjectGraphHash = oldStepReceiptBeforeObject;
			step.ReceiptBeforeTopologyHash = oldStepReceiptBeforeTopology;
			leg.State = oldLegState; leg.Lease.State = oldLeaseState;
			leg.ReceiptState = oldLegReceiptState;
			leg.ReceiptBeforeIdMatches = oldLegBeforeIdMatches;
			leg.ReceiptBeforeMarkerMatches = oldLegBeforeMarkerMatches;
			leg.ReceiptBeforeCount = oldLegBeforeCount;
			leg.ReceiptBeforeOwnerGraphHash = oldLegReceiptBeforeOwner;
			leg.ReceiptBeforeObjectGraphHash = oldLegReceiptBeforeObject;
			leg.ReceiptBeforeTopologyHash = oldLegReceiptBeforeTopology;
			return false;
		}

	}
}
