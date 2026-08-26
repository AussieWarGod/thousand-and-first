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
		private static bool ClaimGrowthArrivalCandidateIdentities(
			Dictionary<string, string> claims, KingdomGrowthArrivalCandidate candidate,
			KingdomGrowthOperation arrival)
		{
			if (candidate == null) return true;
			string physicalOwner = candidate.Id;
			if (arrival != null && arrival.Action == KingdomGrowthAction.Arrival
				&& arrival.ArrivalDisposition == KingdomGrowthArrivalDisposition.Joined
				&& string.Equals(arrival.ArrivalCandidateId, candidate.Id,
					StringComparison.Ordinal)
				&& string.Equals(arrival.TargetId, candidate.ObjectId, StringComparison.Ordinal)
				&& string.Equals(arrival.TargetMarker, candidate.Marker,
					StringComparison.Ordinal)) physicalOwner = arrival.Id;
			return ValidGeneratedId(candidate.Id)
				&& ClaimGrowthIdentity(claims, "marker", candidate.Marker, physicalOwner)
				&& ClaimGrowthIdentity(claims, "object", candidate.ObjectId, physicalOwner)
				&& ClaimGrowthIdentity(claims, "escrow", candidate.EscrowKey, candidate.Id);
		}

		private static bool GrowthOperationAlreadyPresent(KingdomGrowthBook book,
			KingdomGrowthOperation operation)
		{
			if (ReferenceEquals(book.HeartbeatOp, operation) || ReferenceEquals(book.ArrivalOp, operation)
				|| ReferenceEquals(book.DepartureOp, operation)
				|| ReferenceEquals(book.DeliveryOp, operation)
				|| ReferenceEquals(book.FetchOp, operation)
				|| ReferenceEquals(book.MillOp, operation)) return true;
			for (int i = 0; i < book.FieldOps.Count; i++)
				if (ReferenceEquals(book.FieldOps[i].Operation, operation)) return true;
			return false;
		}

		private static bool ClaimGrowthOperationIdentities(Dictionary<string, string> claims,
			KingdomGrowthOperation operation)
		{
			if (operation == null) return true;
			if (!ValidGeneratedId(operation.Id) || operation.WaterLegs == null
				|| operation.Sources == null || operation.Outputs == null) return false;
			string owner = operation.Id;
			if (!ClaimGrowthIdentity(claims, "field", operation.FieldId, owner)
				|| !ClaimGrowthIdentity(claims, "object", operation.TargetId, owner)
				|| !ClaimGrowthIdentity(claims, "marker", operation.TargetMarker, owner)) return false;
			if (operation.TargetId != null)
			{
				string topology = TopologyId(operation.TargetTopology, operation.TargetOwnerId,
					operation.ZoneId, operation.TargetX, operation.TargetY);
				if (topology == null || !ClaimGrowthIdentity(claims, "target-topology",
					operation.TargetId + "\n" + topology, owner)) return false;
			}
			for (int i = 0; i < operation.WaterLegs.Count; i++)
			{
				KingdomGrowthWaterLeg leg = operation.WaterLegs[i];
				if (leg == null || !ClaimGrowthIdentity(claims, "water-container",
					leg.ContainerId, owner)) return false;
				string topology = TopologyId(leg.OwnerTopology, leg.OwnerId, leg.ZoneId, leg.X, leg.Y);
				if (topology == null || !ClaimGrowthIdentity(claims, "water-topology",
					leg.ContainerId + "\n" + topology, owner)) return false;
			}
			if (!ClaimGrowthObjectIdentities(claims, operation.Sources, owner)
				|| !ClaimGrowthObjectIdentities(claims, operation.Outputs, owner)) return false;
			return true;
		}

		private static bool ClaimGrowthObjectIdentities(Dictionary<string, string> claims,
			List<KingdomGrowthObjectLeg> legs, string owner)
		{
			for (int i = 0; i < legs.Count; i++)
			{
				KingdomGrowthObjectLeg leg = legs[i];
				if (leg == null || !ClaimGrowthIdentity(claims, "object", leg.ObjectId, owner)
					|| !ClaimGrowthIdentity(claims, "marker", leg.Marker, owner)
					|| !ClaimGrowthIdentity(claims, "marker", leg.CreatedMarker, owner)
					|| !ClaimGrowthIdentity(claims, "marker", leg.DetachedMarker, owner)) return false;
			}
			return true;
		}

		private static bool ClaimGrowthIdentity(Dictionary<string, string> claims,
			string kind, string value, string owner)
		{
			if (value == null) return true;
			if (value.Length == 0) return false;
			string key = kind + "\n" + value;
			string prior;
			if (claims.TryGetValue(key, out prior))
				return string.Equals(prior, owner, StringComparison.Ordinal);
			claims.Add(key, owner);
			return true;
		}

		private static bool GrowthProofRowsValid(KingdomGrowthBook book)
		{
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			long priorTick = -1L;
			for (int i = 0; i < book.RecentProofs.Count; i++)
			{
				KingdomGrowthProof proof = book.RecentProofs[i];
				if (proof == null || !KnownGrowthSlot(proof.Slot) || proof.Sequence <= 0L
					|| !KnownGrowthAction(proof.Action) || proof.Tick < priorTick || !ids.Add(proof.Id)
					|| SlotForGrowthAction(proof.Action) != proof.Slot
					|| (proof.Slot == KingdomGrowthSlotKind.Field ? !ValidRootId(proof.FieldId)
						: proof.FieldId != null)
					|| !string.Equals(proof.Id, GrowthOperationId(book.SettlementId, proof.Slot,
						proof.FieldId, proof.Sequence), StringComparison.Ordinal)
					|| !ValidHashNamespace(proof.PlanHash, "growth-plan")
					|| proof.Sequence > GrowthProofRetiredThrough(book, proof)) return false;
				priorTick = proof.Tick;
			}
			return true;
		}

		private static long GrowthProofRetiredThrough(KingdomGrowthBook book,
			KingdomGrowthProof proof)
		{
			if (proof.Slot == KingdomGrowthSlotKind.Field)
			{
				KingdomGrowthFieldSlot field = FindGrowthField(book, proof.FieldId);
				return field == null ? -1L : field.RetiredThrough;
			}
			return GetGrowthRetired(book, proof.Slot, null);
		}

		private static bool GrowthOperationsValid(KingdomGrowthBook book)
		{
			if (!GrowthOperationSlotValid(book, KingdomGrowthSlotKind.Heartbeat, null,
				book.HeartbeatOp, book.HeartbeatNextSequence, book.HeartbeatRetiredThrough)
				|| !GrowthOperationSlotValid(book, KingdomGrowthSlotKind.Arrival, null,
					book.ArrivalOp, book.ArrivalNextSequence, book.ArrivalRetiredThrough)
				|| !GrowthOperationSlotValid(book, KingdomGrowthSlotKind.Departure, null,
					book.DepartureOp, book.DepartureNextSequence, book.DepartureRetiredThrough)
				|| !GrowthOperationSlotValid(book, KingdomGrowthSlotKind.Delivery, null,
					book.DeliveryOp, book.DeliveryNextSequence, book.DeliveryRetiredThrough)
				|| !GrowthOperationSlotValid(book, KingdomGrowthSlotKind.Fetch, null,
					book.FetchOp, book.FetchNextSequence, book.FetchRetiredThrough)
				|| !GrowthOperationSlotValid(book, KingdomGrowthSlotKind.Mill, null,
					book.MillOp, book.MillNextSequence, book.MillRetiredThrough)) return false;
			for (int i = 0; i < book.FieldOps.Count; i++)
			{
				KingdomGrowthFieldSlot field = book.FieldOps[i];
				if (field.Quarantined) continue;
				if (!GrowthOperationSlotValid(book, KingdomGrowthSlotKind.Field, field.FieldId,
					field.Operation, field.NextSequence, field.RetiredThrough)) return false;
			}
			return true;
		}

		private static bool GrowthOperationSlotValid(KingdomGrowthBook book,
			KingdomGrowthSlotKind slot, string fieldId, KingdomGrowthOperation operation,
			long next, long retired)
		{
			if (operation == null) return IsExactSuccessor(next, retired);
			KingdomGrowthFieldSlot field = slot == KingdomGrowthSlotKind.Field
				? FindGrowthField(book, fieldId) : null;
			return IsExactSuccessor(operation.Sequence, retired)
				&& IsExactSuccessor(next, operation.Sequence)
				&& GrowthOperationShape(book, operation, slot, fieldId, false)
				&& GrowthPersistedClockMatches(book, operation, field)
				&& GrowthPersistedDomainScalarsMatch(book, operation);
		}

		private static bool GrowthPersistedDomainScalarsMatch(KingdomGrowthBook book,
			KingdomGrowthOperation operation)
		{
			KingdomGrowthFieldSlot field = SlotForGrowthAction(operation.Action)
				== KingdomGrowthSlotKind.Field ? FindGrowthField(book, operation.FieldId) : null;
			for (int i = 0; i < operation.DomainSteps.Count; i++)
			{
				KingdomGrowthDomainStep step = operation.DomainSteps[i];
				bool proved = step.State == KingdomLifecyclePhysicalState.Proved;
				if (step.Kind == KingdomGrowthDomainStepKind.Field
					&& !GrowthFieldMatchesState(field, proved ? step.FieldAfter : step.FieldBefore))
					return false;
				if (step.Kind == KingdomGrowthDomainStepKind.CropRegistry
					&& !GrowthCropRowsEqual(book.CropRows,
						proved ? step.CropRowsAfter : step.CropRowsBefore)) return false;
			}
			KingdomGrowthDomainStep pending = FindGrowthDomain(operation,
				KingdomGrowthDomainStepKind.PendingCrop);
			int pendingValue = pending != null
				&& pending.State == KingdomLifecyclePhysicalState.Proved
					? operation.PendingCropAfter : operation.PendingCropBefore;
			bool pendingProved = pending != null
				&& pending.State == KingdomLifecyclePhysicalState.Proved;
			string pendingBlueprint = pendingProved ? operation.PendingCropBlueprintAfter
				: operation.PendingCropBlueprintBefore;
			string pendingZone = pendingProved ? operation.PendingCropZoneIdAfter
				: operation.PendingCropZoneIdBefore;
			if (book.PendingCrop != pendingValue
				|| !string.Equals(book.PendingCropBlueprint, pendingBlueprint,
					StringComparison.Ordinal)
				|| !string.Equals(book.PendingCropZoneId, pendingZone,
					StringComparison.Ordinal)) return false;
			KingdomGrowthDomainStep subsidence = FindGrowthDomain(operation,
				KingdomGrowthDomainStepKind.SubsidenceSchedule);
			long subsidenceValue = subsidence != null
				&& subsidence.State == KingdomLifecyclePhysicalState.Proved
					? operation.SubsidenceAfter : operation.SubsidenceBefore;
			return book.LastSubsidenceTick == subsidenceValue;
		}

		private static bool GrowthPersistedClockMatches(KingdomGrowthBook book,
			KingdomGrowthOperation operation, KingdomGrowthFieldSlot field)
		{
			if (book == null || operation == null || operation.ClockLease == null) return false;
			bool proved = operation.ClockState == KingdomLifecyclePhysicalState.Proved
				&& operation.ClockLease.State == KingdomLifecycleLeaseState.Proved;
			bool before = operation.ClockState == KingdomLifecyclePhysicalState.Prepared
				&& operation.ClockLease.State == KingdomLifecycleLeaseState.Prepared
				|| operation.ClockState == KingdomLifecyclePhysicalState.Intent
					&& operation.ClockLease.State == KingdomLifecycleLeaseState.Intent;
			if (!proved && !before) return false;
			long expected = proved ? operation.ClockLease.After : operation.ClockLease.Before;
			if (GrowthClockValue(book, operation.Action, field) != expected) return false;
			return field == null || field.ClockTick == (proved
				? operation.FieldClockAfter : operation.FieldClockBefore)
				&& (proved ? string.Equals(field.LastOperationId, operation.Id,
					StringComparison.Ordinal) : !string.Equals(field.LastOperationId,
					operation.Id, StringComparison.Ordinal));
		}

	}
}
