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
		public static bool WaterConserved(KingdomLifecycleOperation Operation, bool Terminal)
		{
			if (Operation == null || !ValidCount(Operation.WaterRequested)
				|| !ValidCount(Operation.WaterProved) || !ValidCount(Operation.WaterOutstanding)
				|| !ValidCount(Operation.WaterLost) || !ValidCount(Operation.WaterAmbiguous)
				|| Operation.WaterLegs == null || Operation.WaterLegs.Count > MaxWaterLegs) return false;
			if (ExternalRaidTributeReceipt(Operation)) return true;
			long planned = 0L;
			long proved = 0L;
			for (int i = 0; i < Operation.WaterLegs.Count; i++)
			{
				KingdomLifecycleWaterLeg leg = Operation.WaterLegs[i];
				if (!WaterLegShape(leg, Operation, i, false)) return false;
				planned += leg.Delta;
				if (leg.State == KingdomLifecyclePhysicalState.Proved) proved += leg.Delta;
			}
				if (planned != Operation.WaterRequested || proved != Operation.WaterProved
					|| (long)Operation.WaterRequested != Operation.WaterProved
						+ Operation.WaterOutstanding + Operation.WaterLost
						+ Operation.WaterAmbiguous) return false;
			if (Terminal && (Operation.WaterOutstanding != 0 || Operation.WaterAmbiguous != 0
				|| Operation.WaterLost != 0 || Operation.WaterProved != Operation.WaterRequested))
				return false;
			return true;
		}

		private static bool ExternalRaidTributeReceipt(KingdomLifecycleOperation operation)
		{
			return operation != null
				&& operation.Action == KingdomLifecycleAction.RaidTribute
				&& operation.WaterRequested > 0
				&& operation.WaterProved == operation.WaterRequested
				&& operation.WaterOutstanding == 0 && operation.WaterLost == 0
				&& operation.WaterAmbiguous == 0
				&& operation.WaterState == KingdomLifecyclePhysicalState.Proved
				&& operation.WaterLegs != null && operation.WaterLegs.Count == 0
				&& string.Equals(operation.ObjectMarker,
					ChildId(operation.Id, "raid-tribute-receipt", 0), StringComparison.Ordinal);
		}

		private static bool LifecycleBookShape(KingdomLifecycleBook book)
		{
			return book != null
				&& (book.LegacyIdentity ? ValidRootId(book.LegacyMigrationKey)
					: string.IsNullOrEmpty(book.LegacyMigrationKey))
				&& !TooLong(book.Fault, MaxTextChars)
				&& KnownOption(book.LocusOption) && KnownOption(book.NotableOption)
				&& KnownOption(book.RaidOption) && KnownOption(book.PetitionOption)
				&& book.LocusOptionTick >= 0L && book.NotableOptionTick >= 0L
				&& book.RaidOptionTick >= 0L && book.PetitionOptionTick >= 0L
				&& ResourceRegistryValid(book) && ProofListValid(book)
				&& KingdomRaidIncidentRules.ValidLedger(book.RaidLedger)
				&& LaneAuthorityValid(book, KingdomLifecycleLane.PlainGuest, book.PlainGuest)
				&& LaneAuthorityValid(book, KingdomLifecycleLane.NotableGuest, book.NotableGuest)
				&& LaneAuthorityValid(book, KingdomLifecycleLane.Raid, book.Raid)
				&& LaneAuthorityValid(book, KingdomLifecycleLane.Petition, book.Petition)
				&& ActiveResourcesValid(book) && GrowthAttachmentValid(book);
		}

		private static bool CarryBookShape(KingdomCarryBook book)
		{
			if (book == null || (book.LegacyIdentity ? !ValidRootId(book.LegacyMigrationKey)
				: !string.IsNullOrEmpty(book.LegacyMigrationKey))
				|| TooLong(book.Fault, MaxTextChars) || !CarryProofListValid(book)
				|| !CarrySettlementSetShape(book) || !CarryResourceRegistryValid(book)
				|| !CarrySequenceValid(book)) return false;
			if (book.Open == null) return CarryActiveResourcesValid(book);
			KingdomCarryOperation op = book.Open;
			string hash;
			return string.Equals(op.Id, CarryId(book.RealmId, op.Sequence), StringComparison.Ordinal)
				&& ExactStringList(op.SettlementIds, book.SettlementIds)
				&& string.Equals(op.RealmTopologyHash,
					RealmTopologyDigest(book.RealmId, book.SettlementIds), StringComparison.Ordinal)
				&& CarryPhaseAllowed(op.Phase) && op.CreatedTick >= 0L
				&& op.UpdatedTick >= op.CreatedTick && !TooLong(op.Fault, MaxTextChars)
				&& CarryPlanShape(op, false) && TryCarryPlanHash(op, out hash)
				&& string.Equals(op.PlanHash, hash, StringComparison.Ordinal)
				&& SettlementMember(book, op.OriginSettlementId)
				&& SettlementMember(book, op.DestinationSettlementId)
				&& CarryConserved(op) && CarryPhaseProgressValid(op)
				&& CarryActiveResourcesValid(book);
		}

		private static bool LaneAuthorityValid(KingdomLifecycleBook book,
			KingdomLifecycleLane lane, KingdomLifecycleOperation op)
		{
			if (!LaneSequenceValid(book, lane, op)) return false;
			if (op == null) return true;
			string hash;
			return op.Lane == lane && ActionAllowedInLane(op.Action, lane)
				&& CanonicalOperationId(op)
				&& string.Equals(op.SettlementId, book.SettlementId, StringComparison.Ordinal)
				&& KnownPhase(op.Phase) && PhaseAllowed(op.Action, op.Phase)
				&& op.CreatedTick >= 0L && op.UpdatedTick >= op.CreatedTick
				&& !TooLong(op.Fault, MaxTextChars) && PlanShape(op, false)
				&& LifecyclePhaseProgressValid(op)
				&& TryPlanHash(op, out hash)
				&& string.Equals(op.PlanHash, hash, StringComparison.Ordinal);
		}

		private static bool LaneSequenceValid(KingdomLifecycleBook book,
			KingdomLifecycleLane lane, KingdomLifecycleOperation op)
		{
			if (book == null) return false;
			long next = GetNextSequence(book, lane);
			long retired = GetRetiredThrough(book, lane);
			if (!CounterShape(next, retired)) return false;
			if (op == null) return IsExactSuccessor(next, retired);
			long after;
			return IsExactSuccessor(op.Sequence, retired)
				&& CheckedAdd(op.Sequence, 1L, out after) && next == after;
		}

		private static bool CarrySequenceValid(KingdomCarryBook book)
		{
			if (book == null || !CounterShape(book.NextSequence, book.RetiredThrough)) return false;
			if (book.Open == null) return IsExactSuccessor(book.NextSequence, book.RetiredThrough);
			long after;
			return IsExactSuccessor(book.Open.Sequence, book.RetiredThrough)
				&& CheckedAdd(book.Open.Sequence, 1L, out after) && book.NextSequence == after;
		}

		private static bool CarryPhaseProgressValid(KingdomCarryOperation operation)
		{
			if (operation == null) return false;
			if (operation.Phase == KingdomLifecyclePhase.Quarantined) return true;
			if (operation.AuthorityKind == KingdomCarryAuthorityKind.ExactManifest)
				return ExactCarryPhaseProgressValid(operation);
			bool sourcesDone = AllSourcesProved(operation);
			bool outputsDone = OutputsSettledForRoad(operation);
			bool outputsPrepared = operation.OutputIndex == 0;
			if (operation.Outputs == null) return false;
			for (int i = 0; i < operation.Outputs.Count; i++)
				if (operation.Outputs[i] == null
					|| operation.Outputs[i].State != KingdomLifecyclePhysicalState.Prepared)
					outputsPrepared = false;
			KingdomLifecycleLeaseState schedule = operation.ScheduleLease == null
				? KingdomLifecycleLeaseState.None : operation.ScheduleLease.State;
			switch (operation.Phase)
			{
			case KingdomLifecyclePhase.Prepared:
				return operation.SourceIndex == 0 && outputsPrepared
					&& schedule == KingdomLifecycleLeaseState.Prepared
					&& CarryEscrow(operation) == 0 && OutboxInitial(operation.Outbox);
			case KingdomLifecyclePhase.RemovalIntent:
				return outputsPrepared && schedule == KingdomLifecycleLeaseState.Prepared
					&& MaterialDisposition(operation) == 0
					&& OutboxInitial(operation.Outbox);
			case KingdomLifecyclePhase.Removed:
				return sourcesDone && outputsPrepared
					&& schedule == KingdomLifecycleLeaseState.Prepared
					&& MaterialDisposition(operation) == 0 && OutboxInitial(operation.Outbox);
			case KingdomLifecyclePhase.ScheduleIntent:
				return sourcesDone && outputsPrepared
					&& (schedule == KingdomLifecycleLeaseState.Prepared
						|| schedule == KingdomLifecycleLeaseState.Intent
						|| schedule == KingdomLifecycleLeaseState.Proved)
					&& MaterialDisposition(operation) == 0 && OutboxInitial(operation.Outbox);
			case KingdomLifecyclePhase.ProjectionIntent:
				return sourcesDone && schedule == KingdomLifecycleLeaseState.Proved
					&& OutboxInitial(operation.Outbox);
			case KingdomLifecyclePhase.Projected:
				return sourcesDone && schedule == KingdomLifecycleLeaseState.Proved
					&& outputsDone && CarryEscrow(operation) == 0
					&& OutboxInitial(operation.Outbox);
			case KingdomLifecyclePhase.Sinks:
				return sourcesDone && schedule == KingdomLifecycleLeaseState.Proved
					&& outputsDone && CarryEscrow(operation) == 0;
			case KingdomLifecyclePhase.Terminal:
				return sourcesDone && schedule == KingdomLifecycleLeaseState.Proved
					&& outputsDone && CarryEscrow(operation) == 0
					&& CarryOutboxTerminal(operation);
			default:
				return false;
			}
		}

		private static bool ExactCarryPhaseProgressValid(KingdomCarryOperation operation)
		{
			if (operation == null || operation.Outputs == null || operation.Sources == null)
				return false;
			bool sourcesPrepared = operation.SourceIndex == 0;
			for (int i = 0; i < operation.Sources.Count; i++)
				if (operation.Sources[i] == null || operation.Sources[i].LoadedCount != 0
					|| operation.Sources[i].State != KingdomLifecyclePhysicalState.Prepared)
					sourcesPrepared = false;
			bool outputsPrepared = operation.OutputIndex == 0;
			for (int i = 0; i < operation.Outputs.Count; i++)
				if (operation.Outputs[i] == null
					|| operation.Outputs[i].State != KingdomLifecyclePhysicalState.Prepared)
					outputsPrepared = false;
			bool signProved = operation.SignReceiptState == KingdomLifecyclePhysicalState.Proved;
			bool sourcesDone = AllExactSourcesLoaded(operation);
			bool outputsDone = ExactOutputsSettled(operation);
			KingdomLifecycleLeaseState schedule = operation.ScheduleLease == null
				? KingdomLifecycleLeaseState.None : operation.ScheduleLease.State;
			switch (operation.Phase)
			{
			case KingdomLifecyclePhase.Prepared:
				return sourcesPrepared && outputsPrepared
					&& schedule == KingdomLifecycleLeaseState.Prepared
					&& CarryEscrow(operation) == 0 && OutboxInitial(operation.Outbox);
			case KingdomLifecyclePhase.RemovalIntent:
				return signProved && outputsPrepared
					&& schedule == KingdomLifecycleLeaseState.Prepared
					&& MaterialDisposition(operation) == 0 && OutboxInitial(operation.Outbox);
			case KingdomLifecyclePhase.Removed:
				return signProved && sourcesDone && outputsPrepared
					&& schedule == KingdomLifecycleLeaseState.Prepared
					&& MaterialDisposition(operation) == 0 && OutboxInitial(operation.Outbox);
			case KingdomLifecyclePhase.ScheduleIntent:
				return signProved && sourcesDone && outputsPrepared
					&& (schedule == KingdomLifecycleLeaseState.Prepared
						|| schedule == KingdomLifecycleLeaseState.Intent
						|| schedule == KingdomLifecycleLeaseState.Proved)
					&& MaterialDisposition(operation) == 0 && OutboxInitial(operation.Outbox);
			case KingdomLifecyclePhase.ProjectionIntent:
				return signProved && sourcesDone && schedule == KingdomLifecycleLeaseState.Proved
					&& OutboxInitial(operation.Outbox)
					&& (!operation.DestinationSafetyWaiting
						|| operation.OutputIndex == 0 && outputsPrepared);
			case KingdomLifecyclePhase.Projected:
				return signProved && sourcesDone && schedule == KingdomLifecycleLeaseState.Proved
					&& outputsDone && CarryEscrow(operation) == 0
					&& !operation.DestinationSafetyWaiting && OutboxInitial(operation.Outbox);
			case KingdomLifecyclePhase.Sinks:
				return signProved && sourcesDone && schedule == KingdomLifecycleLeaseState.Proved
					&& outputsDone && CarryEscrow(operation) == 0
					&& !operation.DestinationSafetyWaiting;
			case KingdomLifecyclePhase.Terminal:
				return signProved && sourcesDone && schedule == KingdomLifecycleLeaseState.Proved
					&& outputsDone && CarryEscrow(operation) == 0
					&& !operation.DestinationSafetyWaiting && CarryOutboxTerminal(operation);
			default:
				return false;
			}
		}

		private static int MaterialDisposition(KingdomCarryOperation operation)
		{
			long value = operation == null ? -1L : SumSix(operation.DeliveredMud + operation.LostMud,
				operation.DeliveredBrush + operation.LostBrush,
				operation.DeliveredTimber + operation.LostTimber,
				operation.DeliveredStone + operation.LostStone,
				operation.DeliveredMarble + operation.LostMarble,
				operation.DeliveredScrap + operation.LostScrap);
			return value < 0L || value > int.MaxValue ? -1 : (int)value;
		}

		private static bool ExactOperationAuthority(KingdomLifecycleBook book,
			KingdomLifecycleOperation operation)
		{
			return operation != null && CanOwnAuthority(book)
				&& ReferenceEquals(GetSlot(book, operation.Lane), operation)
				&& string.Equals(operation.SettlementId, book.SettlementId,
					StringComparison.Ordinal);
		}

		private static bool ExactCarryAuthority(KingdomCarryBook book,
			KingdomCarryOperation operation)
		{
			return operation != null && CanOwnAuthority(book)
				&& ReferenceEquals(book.Open, operation)
				&& ExactStringList(operation.SettlementIds, book.SettlementIds)
				&& string.Equals(operation.RealmTopologyHash,
					RealmTopologyDigest(book.RealmId, book.SettlementIds), StringComparison.Ordinal);
		}

	}
}
