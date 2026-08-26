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
		private static bool OutboxShape(KingdomLifecycleOperation op, bool Publication)
		{
			KingdomLifecycleOutbox box = op.Outbox;
			if (box == null || !string.Equals(box.OperationId, op.Id, StringComparison.Ordinal)
				|| !string.Equals(box.EventId, ChildId(op.Id, "outbox", 0), StringComparison.Ordinal)
				|| !string.Equals(box.ChronicleReceiptId, ChildId(op.Id, "chronicle", 0),
					StringComparison.Ordinal)
				|| TooLong(box.Chronicle, MaxTextChars) || TooLong(box.Ledger, MaxTextChars)
				|| TooLong(box.Message, MaxTextChars) || TooLong(box.Deed, MaxTextChars)
				|| TooLong(box.GuestbookLine, MaxTextChars)
				|| !SinkTextShape(box.Chronicle, box.ChronicleDisposition,
					box.ChronicleState, Publication)
				|| !SinkTextShape(box.Ledger, box.LedgerDisposition, box.LedgerState, Publication)
				|| !SinkTextShape(box.Message, box.MessageDisposition, box.MessageState, Publication)
				|| !SinkTextShape(box.Deed, box.DeedDisposition, box.DeedState, Publication)
				|| !SinkTextShape(box.GuestbookLine, box.GuestbookDisposition,
					box.GuestbookState, Publication)) return false;
			KingdomLifecycleSinkMask required = RequiredSinks(op.Action, op.Lane);
			return RequiredText(required, KingdomLifecycleSinkMask.Chronicle, box.Chronicle,
				box.ChronicleDisposition)
				&& RequiredText(required, KingdomLifecycleSinkMask.Ledger, box.Ledger,
					box.LedgerDisposition)
				&& RequiredText(required, KingdomLifecycleSinkMask.Message, box.Message,
					box.MessageDisposition)
				&& RequiredText(required, KingdomLifecycleSinkMask.Deed, box.Deed, box.DeedDisposition)
				&& RequiredText(required, KingdomLifecycleSinkMask.Guestbook, box.GuestbookLine,
					box.GuestbookDisposition);
		}

		private static bool OutboxTerminal(KingdomLifecycleOperation op)
		{
			if (!OutboxShape(op, false)) return false;
			KingdomLifecycleOutbox b = op.Outbox;
			if (!SinkSettled(b.ChronicleState) || !SinkSettled(b.LedgerState)
				|| !SinkSettled(b.MessageState) || !SinkSettled(b.DeedState)
				|| !SinkSettled(b.GuestbookState)) return false;
			// Chronicle.RecordOnce has exact receipt ownership and must be reconciled, never lost.
			return string.IsNullOrEmpty(b.Chronicle)
				? b.ChronicleState == KingdomLifecycleSinkState.Skipped
				: b.ChronicleState == KingdomLifecycleSinkState.Delivered;
		}

		private static bool SinkTextShape(string Text, KingdomLifecycleSinkDisposition Disposition,
			KingdomLifecycleSinkState State, bool Publication)
		{
			if (!KnownSink(State) || !KnownDisposition(Disposition)) return false;
			if (Disposition == KingdomLifecycleSinkDisposition.Skip)
				return State == KingdomLifecycleSinkState.Skipped;
			if (string.IsNullOrEmpty(Text)) return false;
			return Publication ? State == KingdomLifecycleSinkState.Pending
				: State == KingdomLifecycleSinkState.Pending || State == KingdomLifecycleSinkState.Intent
					|| State == KingdomLifecycleSinkState.Delivered
					|| State == KingdomLifecycleSinkState.Lost;
		}

		private static KingdomLifecycleSinkState InitialSink(string Text)
		{
			return string.IsNullOrEmpty(Text) ? KingdomLifecycleSinkState.Skipped
				: KingdomLifecycleSinkState.Pending;
		}

		private static KingdomLifecycleSinkDisposition InitialDisposition(string Text)
		{
			return string.IsNullOrEmpty(Text) ? KingdomLifecycleSinkDisposition.Skip
				: KingdomLifecycleSinkDisposition.Deliver;
		}

		private static bool RequiredText(KingdomLifecycleSinkMask Required,
			KingdomLifecycleSinkMask Bit, string Text, KingdomLifecycleSinkDisposition Disposition)
		{
			return (Required & Bit) == 0 || (!string.IsNullOrEmpty(Text)
				&& Disposition == KingdomLifecycleSinkDisposition.Deliver);
		}

		private static bool TryNextPhase(KingdomLifecycleAction Action,
			KingdomLifecyclePhase From, out KingdomLifecyclePhase To)
		{
			To = KingdomLifecyclePhase.Invalid;
			switch (From)
			{
			case KingdomLifecyclePhase.Prepared:
				if (Action == KingdomLifecycleAction.Passages) To = KingdomLifecyclePhase.Sinks;
				else if (Action == KingdomLifecycleAction.Spawn
					|| Action == KingdomLifecycleAction.RaidAttack
					|| Action == KingdomLifecycleAction.RaidDeliverDemand)
					To = KingdomLifecyclePhase.ProjectionIntent;
				else if (Action == KingdomLifecycleAction.OfferWater
					|| Action == KingdomLifecycleAction.Lodge
					|| Action == KingdomLifecycleAction.RaidTribute) To = KingdomLifecyclePhase.WaterIntent;
				else if (Action == KingdomLifecycleAction.Depart) To = KingdomLifecyclePhase.RemovalIntent;
				else if (KnownAction(Action)) To = KingdomLifecyclePhase.DomainIntent;
				return To != KingdomLifecyclePhase.Invalid;
			case KingdomLifecyclePhase.ProjectionIntent:
				To = KingdomLifecyclePhase.Projected; return true;
			case KingdomLifecyclePhase.Projected:
				To = Action == KingdomLifecycleAction.RaidAttack
					? KingdomLifecyclePhase.WaterIntent : KingdomLifecyclePhase.DomainIntent;
				return true;
			case KingdomLifecyclePhase.WaterIntent:
				To = KingdomLifecyclePhase.WaterSettled; return true;
			case KingdomLifecyclePhase.WaterSettled:
				To = Action == KingdomLifecycleAction.OfferWater
					? KingdomLifecyclePhase.RemovalIntent : KingdomLifecyclePhase.DomainIntent;
				return true;
			case KingdomLifecyclePhase.RemovalIntent:
				To = KingdomLifecyclePhase.Removed; return true;
			case KingdomLifecyclePhase.Removed:
				To = KingdomLifecyclePhase.DomainIntent; return true;
			case KingdomLifecyclePhase.DomainIntent:
				To = KingdomLifecyclePhase.DomainSettled; return true;
			case KingdomLifecyclePhase.DomainSettled:
				To = Action == KingdomLifecycleAction.RaidAttack
					? KingdomLifecyclePhase.EffectIntent : KingdomLifecyclePhase.Sinks;
				return true;
			case KingdomLifecyclePhase.EffectIntent:
				To = KingdomLifecyclePhase.EffectsSettled; return true;
			case KingdomLifecyclePhase.EffectsSettled:
				To = KingdomLifecyclePhase.Sinks; return true;
			case KingdomLifecyclePhase.Sinks:
				To = KingdomLifecyclePhase.ScheduleIntent; return true;
			case KingdomLifecyclePhase.ScheduleIntent:
				To = KingdomLifecyclePhase.Terminal; return true;
			default:
				return false;
			}
		}

		private static bool TransitionReady(KingdomLifecycleBook book,
			KingdomLifecycleOperation op, KingdomLifecyclePhase to)
		{
			if (to == KingdomLifecyclePhase.Quarantined) return true;
			if (to == KingdomLifecyclePhase.Projected)
			{
				for (int i = 0; i < op.Projections.Count; i++)
					if (op.Projections[i].State != KingdomLifecyclePhysicalState.Proved) return false;
				return ProjectionConserved(op, true)
					&& LeaseKindsProved(book, op, KingdomLifecycleResourceKind.Projection);
			}
			if (to == KingdomLifecyclePhase.WaterSettled)
			{
				if (op.WaterRequested == 0) return op.WaterState == KingdomLifecyclePhysicalState.Skipped;
				if (op.WaterState != KingdomLifecyclePhysicalState.Proved
					|| !WaterConserved(op, true)) return false;
				if (ExternalRaidTributeReceipt(op)) return true;
				for (int i = 0; i < op.WaterLegs.Count; i++)
					if (op.WaterLegs[i].State != KingdomLifecyclePhysicalState.Proved) return false;
				return LeaseKindsProved(book, op, KingdomLifecycleResourceKind.WaterVessel);
			}
			if (to == KingdomLifecyclePhase.Removed)
				return op.RemovalState == KingdomLifecyclePhysicalState.Proved
					&& LeaseKindsProved(book, op, KingdomLifecycleResourceKind.Object);
			if (to == KingdomLifecyclePhase.DomainSettled)
			{
				for (int i = 0; i < op.ResourceLeases.Count; i++)
				{
					KingdomLifecycleResourceLease lease = op.ResourceLeases[i];
					if (lease.Kind != KingdomLifecycleResourceKind.Schedule
						&& lease.Kind != KingdomLifecycleResourceKind.WaterVessel
						&& lease.Kind != KingdomLifecycleResourceKind.Projection
						&& lease.Kind != KingdomLifecycleResourceKind.Object
						&& !LeaseProvedByRow(book, lease)) return false;
				}
				return true;
			}
			if (to == KingdomLifecyclePhase.EffectsSettled)
				return (op.EffectState == KingdomLifecyclePhysicalState.Proved
					|| op.EffectState == KingdomLifecyclePhysicalState.Skipped)
					&& op.PlunderProved <= op.PlunderRequested;
			if (to == KingdomLifecyclePhase.ScheduleIntent) return OutboxTerminal(op);
			if (to == KingdomLifecyclePhase.Terminal)
				return LeaseKindsProved(book, op, KingdomLifecycleResourceKind.Schedule)
					&& TerminalComponentsSettled(book, op);
			return true;
		}

		private static bool LeaseKindsProved(KingdomLifecycleBook book,
			KingdomLifecycleOperation op,
			KingdomLifecycleResourceKind kind)
		{
			bool found = false;
			for (int i = 0; i < op.ResourceLeases.Count; i++)
			{
				KingdomLifecycleResourceLease lease = op.ResourceLeases[i];
				if (lease.Kind != kind) continue;
				found = true;
				if (!LeaseProvedByRow(book, lease)) return false;
			}
			return found;
		}

		private static bool LeaseProvedByRow(KingdomLifecycleBook book,
			KingdomLifecycleResourceLease lease)
		{
			return lease != null && lease.State == KingdomLifecycleLeaseState.Proved
				&& ResourceWitnessMatches(FindResource(book, lease.Key), lease);
		}

		private static bool WaterLegShape(KingdomLifecycleWaterLeg leg,
			KingdomLifecycleOperation op, int ordinal, bool Publication)
		{
			if (leg == null || !string.Equals(leg.OperationId, op.Id, StringComparison.Ordinal)
				|| !ValidRootId(leg.OwnerId) || !ValidName(leg.Blueprint) || !ValidName(leg.ZoneId)
				|| leg.Capacity < 0 || leg.Before <= 0 || leg.Before > leg.Capacity
				|| leg.Delta <= 0 || leg.Delta > leg.Before || leg.After != leg.Before - leg.Delta
				|| string.IsNullOrEmpty(leg.Composition) || TooLong(leg.Composition, MaxTextChars)
				|| !KnownPhysical(leg.State) || !KnownPhysical(leg.ReceiptState)
				|| !string.Equals(leg.ReceiptId, ChildId(op.Id, "water-receipt", ordinal),
					StringComparison.Ordinal)) return false;
			string key = ResourceKey(KingdomLifecycleResourceKind.WaterVessel,
				leg.ZoneId, leg.OwnerId);
			if (!string.Equals(leg.LeaseKey, key, StringComparison.Ordinal)) return false;
			KingdomLifecycleResourceLease lease = FindLease(op, key);
			if (lease == null || lease.Before != leg.Before || lease.Delta != -leg.Delta
				|| lease.After != leg.After) return false;
			bool prepared = leg.State == KingdomLifecyclePhysicalState.Prepared
				&& leg.ReceiptState == KingdomLifecyclePhysicalState.Prepared
				&& leg.ReceiptBeforeMatches == -1 && leg.ReceiptAfterMatches == -1
				&& !leg.ReceiptSameReference && string.IsNullOrEmpty(leg.ReceiptProofId);
			if (Publication || prepared) return prepared;
			if (leg.State == KingdomLifecyclePhysicalState.Intent
				&& leg.ReceiptState == KingdomLifecyclePhysicalState.Intent)
				return leg.ReceiptBeforeMatches == 1 && leg.ReceiptAfterMatches == -1
					&& !leg.ReceiptSameReference && string.IsNullOrEmpty(leg.ReceiptProofId);
			return leg.State == KingdomLifecyclePhysicalState.Proved
				&& leg.ReceiptState == KingdomLifecyclePhysicalState.Proved
				&& leg.ReceiptBeforeMatches == 1 && leg.ReceiptAfterMatches == 1
				&& leg.ReceiptSameReference && ExactWaterReceipt(op, lease, leg);
		}

		private static bool ExactWaterReceipt(KingdomLifecycleOperation operation,
			KingdomLifecycleResourceLease lease, KingdomLifecycleWaterLeg leg)
		{
			if (operation == null || lease == null || leg == null
				|| lease.Kind != KingdomLifecycleResourceKind.WaterVessel
				|| !ReferenceEquals(FindWaterLeg(operation, lease.Key), leg)
				|| !string.Equals(lease.Key, leg.LeaseKey, StringComparison.Ordinal)
				|| leg.ReceiptBeforeMatches != 1 || leg.ReceiptAfterMatches != 1
				|| !leg.ReceiptSameReference) return false;
			return string.Equals(leg.ReceiptProofId,
				WaterReceiptProof(operation, lease, leg), StringComparison.Ordinal);
		}

		private static KingdomLifecycleWaterLeg FindWaterLeg(KingdomLifecycleOperation operation,
			string leaseKey)
		{
			if (operation == null || operation.WaterLegs == null || leaseKey == null) return null;
			KingdomLifecycleWaterLeg found = null;
			for (int i = 0; i < operation.WaterLegs.Count; i++)
			{
				KingdomLifecycleWaterLeg leg = operation.WaterLegs[i];
				if (leg == null || !string.Equals(leg.LeaseKey, leaseKey,
					StringComparison.Ordinal)) continue;
				if (found != null) return null;
				found = leg;
			}
			return found;
		}

		private static bool AllWaterLegsProved(KingdomLifecycleOperation operation)
		{
			if (operation == null || operation.WaterLegs == null
				|| operation.WaterLegs.Count == 0) return false;
			for (int i = 0; i < operation.WaterLegs.Count; i++)
				if (operation.WaterLegs[i] == null
					|| operation.WaterLegs[i].State != KingdomLifecyclePhysicalState.Proved)
					return false;
			return true;
		}

	}
}
