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
		private static bool TryNextGrowthPhase(KingdomGrowthAction action,
			KingdomGrowthPhase from, out KingdomGrowthPhase to)
		{
			to = KingdomGrowthPhase.Invalid;
			bool water = action == KingdomGrowthAction.Heartbeat
				|| action == KingdomGrowthAction.Fetch || action == KingdomGrowthAction.Arrival
				|| action == KingdomGrowthAction.Sow;
			bool source = action == KingdomGrowthAction.Heartbeat
				|| action == KingdomGrowthAction.Departure || action == KingdomGrowthAction.Mill
				|| action == KingdomGrowthAction.Sow || action == KingdomGrowthAction.Withdraw
				|| action == KingdomGrowthAction.Ripen || action == KingdomGrowthAction.Harvest;
			bool output = action == KingdomGrowthAction.Arrival
				|| action == KingdomGrowthAction.Delivery || action == KingdomGrowthAction.Mill
				|| action == KingdomGrowthAction.Sow || action == KingdomGrowthAction.Withdraw
				|| action == KingdomGrowthAction.Harvest;
			switch (from)
			{
			case KingdomGrowthPhase.Prepared:
				to = water ? KingdomGrowthPhase.WaterIntent : source
					? KingdomGrowthPhase.SourceIntent : output
						? KingdomGrowthPhase.OutputIntent : KingdomGrowthPhase.DomainIntent; return true;
			case KingdomGrowthPhase.WaterIntent: if (!water) return false;
				to = KingdomGrowthPhase.WaterSettled; return true;
			case KingdomGrowthPhase.WaterSettled: if (!water) return false;
				to = source ? KingdomGrowthPhase.SourceIntent : output
					? KingdomGrowthPhase.OutputIntent : KingdomGrowthPhase.DomainIntent; return true;
			case KingdomGrowthPhase.SourceIntent: if (!source) return false;
				to = KingdomGrowthPhase.SourcesSettled; return true;
			case KingdomGrowthPhase.SourcesSettled: if (!source) return false;
				to = output ? KingdomGrowthPhase.OutputIntent : KingdomGrowthPhase.DomainIntent; return true;
			case KingdomGrowthPhase.OutputIntent: if (!output) return false;
				to = KingdomGrowthPhase.OutputsSettled; return true;
			case KingdomGrowthPhase.OutputsSettled: if (!output) return false;
				to = KingdomGrowthPhase.DomainIntent; return true;
			case KingdomGrowthPhase.DomainIntent: to = KingdomGrowthPhase.DomainSettled; return true;
			case KingdomGrowthPhase.DomainSettled: to = KingdomGrowthPhase.ClockIntent; return true;
			case KingdomGrowthPhase.ClockIntent: to = KingdomGrowthPhase.Sinks; return true;
			case KingdomGrowthPhase.Sinks: to = KingdomGrowthPhase.Terminal; return true;
			default: return false;
			}
		}

		private static int GrowthPhaseIndex(KingdomGrowthAction action, KingdomGrowthPhase phase)
		{
			KingdomGrowthPhase current = KingdomGrowthPhase.Prepared;
			for (int i = 0; i < 16; i++)
			{
				if (current == phase) return i;
				if (!TryNextGrowthPhase(action, current, out current)) return -1;
			}
			return -1;
		}

		private static bool TryNextGrowthPhase(KingdomGrowthOperation operation,
			KingdomGrowthPhase from, out KingdomGrowthPhase to)
		{
			to = KingdomGrowthPhase.Invalid;
			if (operation == null || operation.WaterLegs == null || operation.Sources == null
				|| operation.Outputs == null || operation.DomainSteps == null) return false;
			bool water = operation.WaterLegs.Count > 0;
			bool source = operation.Sources.Count > 0;
			bool output = operation.Outputs.Count > 0;
			bool domain = operation.DomainSteps.Count > 0;
			switch (from)
			{
			case KingdomGrowthPhase.Prepared:
				to = water ? KingdomGrowthPhase.WaterIntent : source
					? KingdomGrowthPhase.SourceIntent : output ? KingdomGrowthPhase.OutputIntent
						: domain ? KingdomGrowthPhase.DomainIntent : KingdomGrowthPhase.ClockIntent;
				return true;
			case KingdomGrowthPhase.WaterIntent:
				if (!water) return false; to = KingdomGrowthPhase.WaterSettled; return true;
			case KingdomGrowthPhase.WaterSettled:
				if (!water) return false; to = source ? KingdomGrowthPhase.SourceIntent : output
					? KingdomGrowthPhase.OutputIntent : domain ? KingdomGrowthPhase.DomainIntent
						: KingdomGrowthPhase.ClockIntent; return true;
			case KingdomGrowthPhase.SourceIntent:
				if (!source) return false; to = KingdomGrowthPhase.SourcesSettled; return true;
			case KingdomGrowthPhase.SourcesSettled:
				if (!source) return false; to = output ? KingdomGrowthPhase.OutputIntent : domain
					? KingdomGrowthPhase.DomainIntent : KingdomGrowthPhase.ClockIntent; return true;
			case KingdomGrowthPhase.OutputIntent:
				if (!output) return false; to = KingdomGrowthPhase.OutputsSettled; return true;
			case KingdomGrowthPhase.OutputsSettled:
				if (!output) return false; to = domain ? KingdomGrowthPhase.DomainIntent
					: KingdomGrowthPhase.ClockIntent; return true;
			case KingdomGrowthPhase.DomainIntent:
				if (!domain) return false; to = KingdomGrowthPhase.DomainSettled; return true;
			case KingdomGrowthPhase.DomainSettled:
				if (!domain) return false; to = KingdomGrowthPhase.ClockIntent; return true;
			case KingdomGrowthPhase.ClockIntent: to = KingdomGrowthPhase.Sinks; return true;
			case KingdomGrowthPhase.Sinks: to = KingdomGrowthPhase.Terminal; return true;
			default: return false;
			}
		}

		private static int GrowthPhaseIndex(KingdomGrowthOperation operation,
			KingdomGrowthPhase phase)
		{
			KingdomGrowthPhase current = KingdomGrowthPhase.Prepared;
			for (int i = 0; i < 16; i++)
			{
				if (current == phase) return i;
				if (!TryNextGrowthPhase(operation, current, out current)) return -1;
			}
			return -1;
		}

		private static bool ExactGrowthOperationAuthority(KingdomGrowthBook book,
			KingdomGrowthOperation operation)
		{
			if (!CanOwnGrowthAuthority(book, book == null ? null : book.SettlementId)
				|| operation == null) return false;
			KingdomGrowthSlotKind slot = SlotForGrowthAction(operation.Action);
			KingdomGrowthFieldSlot field = slot == KingdomGrowthSlotKind.Field
				? FindGrowthField(book, operation.FieldId) : null;
			if (slot == KingdomGrowthSlotKind.Field
				&& (field == null || field.Quarantined)) return false;
			return slot != KingdomGrowthSlotKind.None && ReferenceEquals(
				GetGrowthOperation(book, slot, operation.FieldId), operation);
		}

		private static bool GrowthTransitionReady(KingdomGrowthBook book,
			KingdomGrowthOperation operation, KingdomGrowthPhase to)
		{
			if (operation.Action == KingdomGrowthAction.Arrival
				&& operation.ArrivalCandidateId != null
				&& (to == KingdomGrowthPhase.DomainIntent || to == KingdomGrowthPhase.ClockIntent
					|| to == KingdomGrowthPhase.Sinks || to == KingdomGrowthPhase.Terminal))
			{
				KingdomGrowthArrivalCandidate candidate = book.ArrivalCandidate;
				if (candidate == null
					|| candidate.Phase != KingdomGrowthArrivalCandidatePhase.Settled
					|| !string.Equals(candidate.Id, operation.ArrivalCandidateId,
						StringComparison.Ordinal)
					|| !string.Equals(candidate.ConsumingOperationId, operation.Id,
						StringComparison.Ordinal)) return false;
			}
			if (to == KingdomGrowthPhase.WaterSettled)
				return operation.WaterCursor == operation.WaterLegs.Count;
			if (to == KingdomGrowthPhase.SourcesSettled)
				return operation.SourceCursor == operation.Sources.Count;
			if (to == KingdomGrowthPhase.OutputsSettled)
				return operation.OutputCursor == operation.Outputs.Count;
			if (to == KingdomGrowthPhase.DomainSettled)
				return operation.DomainCursor == operation.DomainSteps.Count;
			if (to == KingdomGrowthPhase.Sinks)
				return GrowthAllPrefixesSettled(operation)
					&& operation.ClockState == KingdomLifecyclePhysicalState.Proved
					&& GrowthLeaseProvedByRow(book, operation.ClockLease);
			if (to == KingdomGrowthPhase.Terminal)
				return GrowthAllPrefixesSettled(operation) && GrowthAllResourcesProved(book, operation)
					&& GrowthOutboxTerminal(operation);
			return true;
		}

		private static bool GrowthAllPrefixesSettled(KingdomGrowthOperation operation)
		{
			return operation.WaterCursor == operation.WaterLegs.Count
				&& operation.SourceCursor == operation.Sources.Count
				&& operation.OutputCursor == operation.Outputs.Count
				&& operation.DomainCursor == operation.DomainSteps.Count;
		}

		private static bool GrowthLeaseProvedByRow(KingdomGrowthBook book,
			KingdomLifecycleResourceLease lease)
		{
			KingdomLifecycleResourceRevision row = FindGrowthResource(book,
				lease == null ? null : lease.Key);
			return lease != null && lease.State == KingdomLifecycleLeaseState.Proved
				&& GrowthResourceMatches(row, lease) && row.Revision == lease.AfterRevision
				&& string.Equals(row.LastOperationId, lease.OperationId, StringComparison.Ordinal)
				&& string.Equals(row.ActiveOperationId, lease.OperationId, StringComparison.Ordinal);
		}

		private static bool GrowthAllResourcesProved(KingdomGrowthBook book,
			KingdomGrowthOperation operation)
		{
			List<KingdomLifecycleResourceLease> leases = GrowthLeases(operation);
			if (leases == null) return false;
			for (int i = 0; i < leases.Count; i++)
				if (!GrowthLeaseProvedByRow(book, leases[i])) return false;
			return true;
		}

		private static void ApplyGrowthClockValue(KingdomGrowthBook book,
			KingdomGrowthOperation operation)
		{
			switch (operation.Action)
			{
			case KingdomGrowthAction.Heartbeat: book.LastHeartbeatTick = operation.ClockLease.After; break;
			case KingdomGrowthAction.Arrival: book.NextArrivalTick = operation.ClockLease.After; break;
			case KingdomGrowthAction.Departure: book.LastDepartureTick = operation.ClockLease.After; break;
			case KingdomGrowthAction.Delivery: book.LastDeliveryTick = operation.ClockLease.After; break;
			case KingdomGrowthAction.Fetch: book.LastFetchTick = operation.ClockLease.After; break;
			case KingdomGrowthAction.Mill: book.LastMillTick = operation.ClockLease.After; break;
			default:
				KingdomGrowthFieldSlot field = FindGrowthField(book, operation.FieldId);
				if (field != null)
				{
					field.CommitRevision = operation.ClockLease.After;
					field.ClockTick = operation.FieldClockAfter;
					field.LastOperationId = operation.Id;
				}
				if (operation.EffectiveWorkAfter > book.EffectiveWorkTick)
					book.EffectiveWorkTick = operation.EffectiveWorkAfter;
				break;
			}
		}

		private static void AppendGrowthProof(KingdomGrowthBook book, KingdomGrowthProof proof)
		{
			if (book.RecentProofs.Count == MaxRecentProofs) book.RecentProofs.RemoveAt(0);
			book.RecentProofs.Add(proof);
		}

		private static bool GrowthProofAppendWouldBeValid(KingdomGrowthBook book,
			KingdomGrowthProof candidate, KingdomGrowthSlotKind slot, KingdomGrowthFieldSlot field)
		{
			if (book == null || candidate == null || book.RecentProofs == null
				|| book.RecentProofs.Count > MaxRecentProofs
				|| !IsExactSuccessor(candidate.Sequence, GetGrowthRetired(book, slot, field))) return false;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			long priorTick = -1L;
			int start = book.RecentProofs.Count == MaxRecentProofs ? 1 : 0;
			for (int i = start; i <= book.RecentProofs.Count; i++)
			{
				KingdomGrowthProof proof = i == book.RecentProofs.Count
					? candidate : book.RecentProofs[i];
				if (proof == null) return false;
				long retired = proof.Slot == slot
					&& (slot != KingdomGrowthSlotKind.Field || string.Equals(proof.FieldId,
						field == null ? null : field.FieldId, StringComparison.Ordinal))
					? candidate.Sequence : GrowthProofRetiredThrough(book, proof);
				if (!GrowthProofShape(book, proof, retired) || proof.Tick < priorTick
					|| !ids.Add(proof.Id)) return false;
				priorTick = proof.Tick;
			}
			return true;
		}

		private static bool GrowthProofShape(KingdomGrowthBook book, KingdomGrowthProof proof,
			long retiredThrough)
		{
			return proof != null && KnownGrowthSlot(proof.Slot) && proof.Sequence > 0L
				&& KnownGrowthAction(proof.Action) && SlotForGrowthAction(proof.Action) == proof.Slot
				&& (proof.Slot == KingdomGrowthSlotKind.Field ? ValidRootId(proof.FieldId)
					: proof.FieldId == null)
				&& string.Equals(proof.Id, GrowthOperationId(book.SettlementId, proof.Slot,
					proof.FieldId, proof.Sequence), StringComparison.Ordinal)
				&& ValidHashNamespace(proof.PlanHash, "growth-plan")
				&& proof.Tick >= 0L && proof.Sequence <= retiredThrough;
		}

		private static bool GrowthCropRowShape(KingdomGrowthBook book, KingdomGrowthCropRow row,
			bool requireLiveField)
		{
			KingdomGrowthFieldSlot field = FindGrowthField(book, row == null ? null : row.FieldId);
			return row != null && field != null && (!requireLiveField || !field.Quarantined)
				&& ValidRootId(row.RowId)
				&& ValidRootId(row.ObjectId) && ValidRootId(row.Marker) && ValidName(row.Blueprint)
				&& ValidName(row.ZoneId) && ValidRootId(row.OwnerId) && row.X >= 0
				&& row.X <= MaxCoordinate && row.Y >= 0 && row.Y <= MaxCoordinate
				&& row.Count > 0 && row.Count <= MaxPhysicalCount
				&& row.HasHarvestable && row.RegenTimer >= 0
				&& string.Equals(row.RegenTime, string.Empty, StringComparison.Ordinal)
				&& row.TileIndex >= -1 && GrowthBoundedPresentString(row.RenderTile)
				&& GrowthBoundedPresentString(row.RenderColor)
				&& GrowthBoundedPresentString(row.RenderDetail)
				&& GrowthBoundedPresentString(row.RenderString)
				&& GrowthBoundedPresentString(row.TileColor)
				&& GrowthWitnessHash(row.PartGraphHash) && GrowthWitnessHash(row.ObjectGraphHash)
				&& GrowthWitnessHash(row.TopologyHash) && row.Revision >= 0L
				&& (row.LastOperationId == null || ValidGeneratedId(row.LastOperationId));
		}

	}
}
