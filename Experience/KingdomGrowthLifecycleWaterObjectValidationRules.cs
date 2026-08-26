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
		private static bool GrowthObjectPrefix(List<KingdomGrowthObjectLeg> rows, int cursor,
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

		private static bool GrowthDomainPrefix(List<KingdomGrowthDomainStep> rows, int cursor,
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

		private static bool GrowthWaterShape(KingdomGrowthOperation operation,
			KingdomGrowthWaterLeg leg, int ordinal, bool publication)
		{
			int after;
			if (leg == null || !string.Equals(leg.OperationId, operation.Id, StringComparison.Ordinal)
				|| !string.Equals(leg.EventId, ChildId(operation.Id, "water", ordinal),
					StringComparison.Ordinal)
				|| leg.ContainerKind != KingdomGrowthWaterContainerKind.LiquidVolume
				|| !ValidRootId(leg.ContainerId) || !ValidName(leg.Blueprint)
				|| !GrowthTopologyValid(leg.OwnerTopology, leg.OwnerId, leg.ZoneId, leg.X, leg.Y)
				|| !GrowthLocationShape(leg.BeforeLocation, leg.BeforeOwnerId, leg.BeforeZoneId,
					leg.BeforeX, leg.BeforeY)
				|| !GrowthLocationShape(leg.AfterLocation, leg.AfterOwnerId, leg.AfterZoneId,
					leg.AfterX, leg.AfterY)
				|| GrowthLocationFromTopology(leg.OwnerTopology) != leg.BeforeLocation
				|| !string.Equals(leg.OwnerId, leg.BeforeOwnerId, StringComparison.Ordinal)
				|| !string.Equals(leg.ZoneId, leg.BeforeZoneId, StringComparison.Ordinal)
				|| leg.X != leg.BeforeX || leg.Y != leg.BeforeY
				|| (leg.OwnerRemovedAfter ? (leg.MutationKind != KingdomGrowthWaterMutationKind.Drain
					|| leg.After != 0 || leg.AfterLocation != KingdomGrowthLocationKind.Graveyard)
					: (leg.AfterLocation != leg.BeforeLocation
						|| !string.Equals(leg.AfterOwnerId, leg.BeforeOwnerId, StringComparison.Ordinal)
						|| !string.Equals(leg.AfterZoneId, leg.BeforeZoneId, StringComparison.Ordinal)
						|| leg.AfterX != leg.BeforeX || leg.AfterY != leg.BeforeY))
				|| leg.Capacity <= 0 || leg.Before < 0 || leg.Before > leg.Capacity
				|| leg.Delta <= 0 || !CheckedAdd(leg.Before,
					leg.MutationKind == KingdomGrowthWaterMutationKind.Drain ? -leg.Delta : leg.Delta,
					out after) || after != leg.After || leg.After < 0 || leg.After > leg.Capacity
				|| (leg.MutationKind != KingdomGrowthWaterMutationKind.Drain
					&& leg.MutationKind != KingdomGrowthWaterMutationKind.Fill)
				|| string.IsNullOrEmpty(leg.BeforeComposition)
				|| string.IsNullOrEmpty(leg.AfterComposition)
				|| TooLong(leg.BeforeComposition, MaxTextChars)
				|| TooLong(leg.AfterComposition, MaxTextChars)
				|| !GrowthWitnessHash(leg.BeforeOwnerGraphHash)
				|| !GrowthWitnessHash(leg.AfterOwnerGraphHash)
				|| !GrowthWitnessHash(leg.BeforePartGraphHash)
				|| !GrowthWitnessHash(leg.AfterPartGraphHash)
				|| !GrowthWitnessHash(leg.BeforeTopologyHash)
				|| !GrowthWitnessHash(leg.AfterTopologyHash)
				|| string.Equals(leg.BeforePartGraphHash, leg.AfterPartGraphHash,
					StringComparison.Ordinal)
				|| !string.Equals(leg.ReceiptId, ChildId(operation.Id, "water-receipt", ordinal),
					StringComparison.Ordinal)
				|| !GrowthLeaseShape(leg.Lease, operation.Id, publication)
				|| leg.Lease.Kind != KingdomLifecycleResourceKind.WaterVessel
				|| !string.Equals(leg.Lease.ScopeId, leg.ZoneId, StringComparison.Ordinal)
				|| !string.Equals(leg.Lease.SubjectId, leg.ContainerId, StringComparison.Ordinal)
				|| !string.Equals(leg.LeaseKey, leg.Lease.Key, StringComparison.Ordinal)
				|| leg.Lease.Before != leg.Before || leg.Lease.After != leg.After
				|| !KnownPhysical(leg.State) || !KnownPhysical(leg.ReceiptState)) return false;
			return GrowthWaterReceiptShape(operation, leg, ordinal, publication);
		}

		private static bool GrowthWaterReceiptShape(KingdomGrowthOperation operation,
			KingdomGrowthWaterLeg leg, int ordinal, bool publication)
		{
			if (publication || leg.State == KingdomLifecyclePhysicalState.Prepared)
				return leg.State == KingdomLifecyclePhysicalState.Prepared
					&& leg.ReceiptState == KingdomLifecyclePhysicalState.Prepared
					&& leg.Lease.State == KingdomLifecycleLeaseState.Prepared
					&& leg.ReceiptBeforeMatches == -1 && leg.ReceiptAfterMatches == -1
					&& GrowthWaterReceiptHashesEmpty(leg) && leg.ReceiptProofId == null;
			if (leg.State == KingdomLifecyclePhysicalState.Intent)
				return leg.ReceiptState == KingdomLifecyclePhysicalState.Intent
					&& leg.Lease.State == KingdomLifecycleLeaseState.Intent
					&& leg.ReceiptBeforeMatches == 1 && leg.ReceiptAfterMatches == -1
					&& GrowthWaterReceiptBeforeExact(leg) && GrowthWaterReceiptAfterEmpty(leg)
					&& leg.ReceiptProofId == null;
			return leg.State == KingdomLifecyclePhysicalState.Proved
				&& leg.ReceiptState == KingdomLifecyclePhysicalState.Proved
				&& leg.Lease.State == KingdomLifecycleLeaseState.Proved
				&& leg.ReceiptBeforeMatches == 1 && leg.ReceiptAfterMatches == 1
				&& GrowthWaterReceiptBeforeExact(leg) && GrowthWaterReceiptAfterExact(leg)
				&& string.Equals(leg.ReceiptCallbackContainerId, leg.ContainerId,
					StringComparison.Ordinal)
				&& GrowthWitnessHash(leg.ReceiptCallbackReferenceHash)
				&& leg.ReceiptSameReference
				&& string.Equals(leg.ReceiptProofId,
					GrowthWaterReceiptProof(operation, leg, ordinal), StringComparison.Ordinal);
		}

		private static bool GrowthWaterReceiptHashesEmpty(KingdomGrowthWaterLeg leg)
		{
			return leg.ReceiptBeforeOwnerGraphHash == null
				&& leg.ReceiptAfterOwnerGraphHash == null
				&& leg.ReceiptBeforePartGraphHash == null
				&& leg.ReceiptAfterPartGraphHash == null
				&& leg.ReceiptBeforeTopologyHash == null
				&& leg.ReceiptAfterTopologyHash == null
				&& leg.ReceiptCallbackContainerId == null
				&& leg.ReceiptCallbackReferenceHash == null
				&& !leg.ReceiptSameReference;
		}

		private static bool GrowthWaterReceiptBeforeExact(KingdomGrowthWaterLeg leg)
		{
			return string.Equals(leg.ReceiptBeforeOwnerGraphHash, leg.BeforeOwnerGraphHash,
				StringComparison.Ordinal) && string.Equals(leg.ReceiptBeforePartGraphHash,
				leg.BeforePartGraphHash, StringComparison.Ordinal)
				&& string.Equals(leg.ReceiptBeforeTopologyHash, leg.BeforeTopologyHash,
					StringComparison.Ordinal);
		}

		private static bool GrowthWaterReceiptAfterEmpty(KingdomGrowthWaterLeg leg)
		{
			return leg.ReceiptAfterOwnerGraphHash == null
				&& leg.ReceiptAfterPartGraphHash == null
				&& leg.ReceiptAfterTopologyHash == null
				&& leg.ReceiptCallbackContainerId == null
				&& leg.ReceiptCallbackReferenceHash == null
				&& !leg.ReceiptSameReference;
		}

		private static bool GrowthWaterReceiptAfterExact(KingdomGrowthWaterLeg leg)
		{
			return string.Equals(leg.ReceiptAfterOwnerGraphHash, leg.AfterOwnerGraphHash,
				StringComparison.Ordinal) && string.Equals(leg.ReceiptAfterPartGraphHash,
				leg.AfterPartGraphHash, StringComparison.Ordinal)
				&& string.Equals(leg.ReceiptAfterTopologyHash, leg.AfterTopologyHash,
					StringComparison.Ordinal);
		}

		private static bool GrowthObjectShape(KingdomGrowthOperation operation,
			KingdomGrowthObjectLeg leg, int ordinal, bool output, bool publication)
		{
			int after;
			bool create = leg != null
				&& leg.MutationKind == KingdomGrowthObjectMutationKind.Create;
			bool createObserved = create && leg.Callbacks != null && leg.Callbacks.Count > 0
				&& leg.Callbacks[0] != null
				&& leg.Callbacks[0].State == KingdomLifecyclePhysicalState.Proved;
			bool createSettled = create && leg.State == KingdomLifecyclePhysicalState.Proved;
			if (leg == null || !string.Equals(leg.OperationId, operation.Id, StringComparison.Ordinal)
				|| !string.Equals(leg.EventId, ChildId(operation.Id,
					output ? "output" : "source", ordinal), StringComparison.Ordinal)
				|| (createObserved ? !ValidRootId(leg.ObjectId)
					: create ? leg.ObjectId != null : !ValidRootId(leg.ObjectId))
				|| !ValidRootId(leg.Marker)
				|| !ValidName(leg.Blueprint) || !GrowthTopologyValid(leg.Topology, leg.OwnerId,
					leg.ZoneId, leg.X, leg.Y) || leg.BeforeCount < 0
				|| !CheckedAdd(leg.BeforeCount, leg.Delta, out after) || after != leg.AfterCount
				|| !ValidCount(leg.BeforeCount) || !ValidCount(leg.AfterCount)
				|| !GrowthWitnessHash(leg.BeforeOwnerGraphHash)
				|| (create && !createSettled ? !GrowthOptionalWitnessSet(
					leg.AfterOwnerGraphHash, leg.AfterObjectGraphHash, leg.AfterTopologyHash)
					: !GrowthWitnessHash(leg.AfterOwnerGraphHash))
				|| !GrowthWitnessHash(leg.BeforeObjectGraphHash)
				|| (!create || createSettled) && !GrowthWitnessHash(leg.AfterObjectGraphHash)
				|| !GrowthWitnessHash(leg.BeforeTopologyHash)
				|| (!create || createSettled) && !GrowthWitnessHash(leg.AfterTopologyHash)
				|| (leg.AfterOwnerGraphHash != null && string.Equals(leg.BeforeOwnerGraphHash,
					leg.AfterOwnerGraphHash, StringComparison.Ordinal))
				|| !string.Equals(leg.ReceiptId, ChildId(operation.Id,
					output ? "output-receipt" : "source-receipt", ordinal),
					StringComparison.Ordinal)
				|| !string.Equals(leg.ReceiptTopologyId, TopologyId(leg.Topology, leg.OwnerId,
					leg.ZoneId, leg.X, leg.Y), StringComparison.Ordinal)
				|| !GrowthLeaseShape(leg.Lease, operation.Id, publication)
				|| leg.Lease.Kind != KingdomLifecycleResourceKind.Object
				|| !string.Equals(leg.Lease.ScopeId, operation.SettlementId,
					StringComparison.Ordinal)
				|| !string.Equals(leg.Lease.SubjectId, create ? leg.Marker : leg.ObjectId,
					StringComparison.Ordinal)
				|| !KnownPhysical(leg.State) || !KnownPhysical(leg.ReceiptState)
				|| !GrowthObjectPipelineShape(leg, publication)) return false;
			KingdomLifecycleLeaseState expectedLease = leg.State == KingdomLifecyclePhysicalState.Proved
				? KingdomLifecycleLeaseState.Proved : leg.State == KingdomLifecyclePhysicalState.Intent
					? KingdomLifecycleLeaseState.Intent : KingdomLifecycleLeaseState.Prepared;
			if (leg.Lease.State != expectedLease) return false;
			if (output)
			{
				if (leg.Delta <= 0 || !leg.NoStack
					|| (leg.MutationKind != KingdomGrowthObjectMutationKind.Create
						&& leg.MutationKind != KingdomGrowthObjectMutationKind.CellAdd
						&& leg.MutationKind != KingdomGrowthObjectMutationKind.InventoryAdd
						&& leg.MutationKind != KingdomGrowthObjectMutationKind.Receive)) return false;
				if (leg.MutationKind == KingdomGrowthObjectMutationKind.Create
					? (!string.Equals(leg.CreatedMarker, leg.Marker, StringComparison.Ordinal)
						|| leg.DetachedMarker != null || leg.BeforeCount != 0
						|| leg.Callbacks.Count < 2
						|| leg.AfterLocation != GrowthLocationFromTopology(leg.Topology))
					: (leg.CreatedMarker != null || !string.Equals(
						leg.DetachedMarker, leg.Marker, StringComparison.Ordinal))) return false;
				if (leg.MutationKind == KingdomGrowthObjectMutationKind.CellAdd
					&& leg.Topology != KingdomLifecycleTopology.Cell) return false;
				if ((leg.MutationKind == KingdomGrowthObjectMutationKind.InventoryAdd
					|| leg.MutationKind == KingdomGrowthObjectMutationKind.Receive)
					&& leg.Topology != KingdomLifecycleTopology.Inventory) return false;
			}
			else
			{
				if (leg.CreatedMarker != null) return false;
				if (leg.MutationKind == KingdomGrowthObjectMutationKind.HarvestableRipeSet)
				{
					if (leg.Delta != 0 || leg.BeforeCount != leg.AfterCount
						|| leg.DetachedMarker != null) return false;
				}
				else
				{
					if (leg.Delta >= 0
						|| !string.Equals(leg.DetachedMarker, leg.Marker, StringComparison.Ordinal)
						|| (leg.MutationKind != KingdomGrowthObjectMutationKind.DestroyOne
							&& leg.MutationKind != KingdomGrowthObjectMutationKind.Obliterate)) return false;
					if (leg.MutationKind == KingdomGrowthObjectMutationKind.DestroyOne
						&& leg.Delta != -1) return false;
					if (leg.MutationKind == KingdomGrowthObjectMutationKind.Obliterate
						&& leg.AfterCount != 0) return false;
				}
			}
			return GrowthObjectReceiptShape(operation, leg, ordinal, output, publication);
		}

		private static bool GrowthOptionalWitnessSet(string one, string two, string three)
		{
			return one == null && two == null && three == null
				|| GrowthWitnessHash(one) && GrowthWitnessHash(two) && GrowthWitnessHash(three);
		}

	}
}
