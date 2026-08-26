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
		internal static KingdomLifecycleCasAction GrowthInspectableOutboxAction(
			KingdomGrowthBook Book, KingdomGrowthOperation Operation, int EventOrdinal,
			KingdomGrowthOutboxSinkKind Sink, int ObservedCount, string ObservedHash)
		{
			KingdomGrowthOutboxEvent e = GrowthOutboxEventAt(Book, Operation, EventOrdinal);
			if (e == null || (Sink != KingdomGrowthOutboxSinkKind.Chronicle
				&& Sink != KingdomGrowthOutboxSinkKind.Ledger))
				return KingdomLifecycleCasAction.Quarantine;
			if (Sink == KingdomGrowthOutboxSinkKind.Chronicle
				&& !e.LegacySingleRegisterChronicle)
				return KingdomLifecycleCasAction.Quarantine;
			KingdomLifecycleSinkState state = Sink == KingdomGrowthOutboxSinkKind.Chronicle
				? e.Outbox.ChronicleState : e.Outbox.LedgerState;
			string text = Sink == KingdomGrowthOutboxSinkKind.Chronicle
				? e.Outbox.Chronicle : e.Outbox.Ledger;
			if (text == null) return state == KingdomLifecycleSinkState.Skipped
				? KingdomLifecycleCasAction.Confirm : KingdomLifecycleCasAction.Quarantine;
			int beforeCount = Sink == KingdomGrowthOutboxSinkKind.Chronicle
				? e.ChronicleBeforeCount : e.LedgerBeforeCount;
			string beforeHash = Sink == KingdomGrowthOutboxSinkKind.Chronicle
				? e.ChronicleBeforeHash : e.LedgerBeforeHash;
			int afterCount = Sink == KingdomGrowthOutboxSinkKind.Chronicle
				? e.ChronicleDeclaredAfterCount : e.LedgerDeclaredAfterCount;
			string afterHash = Sink == KingdomGrowthOutboxSinkKind.Chronicle
				? e.ChronicleDeclaredAfterHash : e.LedgerDeclaredAfterHash;
			bool before = ObservedCount == beforeCount
				&& string.Equals(ObservedHash, beforeHash, StringComparison.Ordinal);
			bool after = ObservedCount == afterCount
				&& string.Equals(ObservedHash, afterHash, StringComparison.Ordinal);
			if (state == KingdomLifecycleSinkState.Pending
				|| state == KingdomLifecycleSinkState.Intent)
				return before ? KingdomLifecycleCasAction.Apply
					: state == KingdomLifecycleSinkState.Intent && after
						? KingdomLifecycleCasAction.Confirm
						: KingdomLifecycleCasAction.Quarantine;
			if (state == KingdomLifecycleSinkState.Delivered)
				return after ? KingdomLifecycleCasAction.Confirm
					: KingdomLifecycleCasAction.Quarantine;
			return KingdomLifecycleCasAction.Quarantine;
		}

		internal static KingdomLifecycleCasAction GrowthChronicleOutboxAction(
			KingdomGrowthBook Book, KingdomGrowthOperation Operation, int EventOrdinal,
			int ChronicleCount, string ChronicleHash, int OutsiderCount, string OutsiderHash)
		{
			KingdomGrowthOutboxEvent e = GrowthOutboxEventAt(Book, Operation, EventOrdinal);
			if (e == null || e.LegacySingleRegisterChronicle || e.Outbox.Chronicle == null)
				return KingdomLifecycleCasAction.Quarantine;
			KingdomLifecycleSinkState state = e.Outbox.ChronicleState;
			bool before = ChronicleCount == e.ChronicleBeforeCount
				&& string.Equals(ChronicleHash, e.ChronicleBeforeHash, StringComparison.Ordinal)
				&& OutsiderCount == e.OutsiderBeforeCount
				&& string.Equals(OutsiderHash, e.OutsiderBeforeHash, StringComparison.Ordinal);
			bool after = ChronicleCount == e.ChronicleDeclaredAfterCount
				&& string.Equals(ChronicleHash, e.ChronicleDeclaredAfterHash,
					StringComparison.Ordinal)
				&& OutsiderCount == e.OutsiderDeclaredAfterCount
				&& string.Equals(OutsiderHash, e.OutsiderDeclaredAfterHash,
					StringComparison.Ordinal);
			bool orderedCut = ChronicleCount == e.ChronicleDeclaredAfterCount
				&& string.Equals(ChronicleHash, e.ChronicleDeclaredAfterHash,
					StringComparison.Ordinal)
				&& OutsiderCount == e.OutsiderBeforeCount
				&& string.Equals(OutsiderHash, e.OutsiderBeforeHash,
					StringComparison.Ordinal);
			if (state == KingdomLifecycleSinkState.Pending
				|| state == KingdomLifecycleSinkState.Intent)
				return before ? KingdomLifecycleCasAction.Apply
					: state == KingdomLifecycleSinkState.Intent && orderedCut
						? KingdomLifecycleCasAction.Apply
					: state == KingdomLifecycleSinkState.Intent && after
						? KingdomLifecycleCasAction.Confirm
						: KingdomLifecycleCasAction.Quarantine;
			return state == KingdomLifecycleSinkState.Delivered && after
				? KingdomLifecycleCasAction.Confirm : KingdomLifecycleCasAction.Quarantine;
		}

		internal static bool BeginGrowthChronicleOutbox(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, int EventOrdinal, int ChronicleBeforeCount,
			string ChronicleBeforeHash, int OutsiderBeforeCount, string OutsiderBeforeHash)
		{
			if (GrowthChronicleOutboxAction(Book, Operation, EventOrdinal,
				ChronicleBeforeCount, ChronicleBeforeHash, OutsiderBeforeCount,
				OutsiderBeforeHash) != KingdomLifecycleCasAction.Apply) return false;
			KingdomGrowthOutboxEvent e = Operation.OutboxEvents[EventOrdinal];
			KingdomLifecycleSinkState old = e.Outbox.ChronicleState;
			e.Outbox.ChronicleState = KingdomLifecycleSinkState.Intent;
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			e.Outbox.ChronicleState = old; return false;
		}

		internal static bool CommitGrowthChronicleOutbox(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, int EventOrdinal, int ChronicleObservedCount,
			string ChronicleObservedHash, int OutsiderObservedCount, string OutsiderObservedHash)
		{
			if (GrowthChronicleOutboxAction(Book, Operation, EventOrdinal,
				ChronicleObservedCount, ChronicleObservedHash, OutsiderObservedCount,
				OutsiderObservedHash) != KingdomLifecycleCasAction.Confirm) return false;
			KingdomGrowthOutboxEvent e = Operation.OutboxEvents[EventOrdinal];
			KingdomLifecycleSinkState oldState = e.Outbox.ChronicleState;
			int oldChronicleCount = e.ChronicleObservedCount;
			string oldChronicleHash = e.ChronicleObservedHash;
			int oldOutsiderCount = e.OutsiderObservedCount;
			string oldOutsiderHash = e.OutsiderObservedHash;
			e.Outbox.ChronicleState = KingdomLifecycleSinkState.Delivered;
			e.ChronicleObservedCount = ChronicleObservedCount;
			e.ChronicleObservedHash = ChronicleObservedHash;
			e.OutsiderObservedCount = OutsiderObservedCount;
			e.OutsiderObservedHash = OutsiderObservedHash;
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			e.Outbox.ChronicleState = oldState;
			e.ChronicleObservedCount = oldChronicleCount;
			e.ChronicleObservedHash = oldChronicleHash;
			e.OutsiderObservedCount = oldOutsiderCount;
			e.OutsiderObservedHash = oldOutsiderHash;
			return false;
		}

		internal static bool BeginGrowthInspectableOutbox(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, int EventOrdinal,
			KingdomGrowthOutboxSinkKind Sink, int BeforeCount, string BeforeHash)
		{
			if (GrowthInspectableOutboxAction(Book, Operation, EventOrdinal, Sink,
				BeforeCount, BeforeHash) != KingdomLifecycleCasAction.Apply) return false;
			KingdomGrowthOutboxEvent e = Operation.OutboxEvents[EventOrdinal];
			KingdomLifecycleSinkState old = Sink == KingdomGrowthOutboxSinkKind.Chronicle
				? e.Outbox.ChronicleState : e.Outbox.LedgerState;
			if (Sink == KingdomGrowthOutboxSinkKind.Chronicle)
				e.Outbox.ChronicleState = KingdomLifecycleSinkState.Intent;
			else e.Outbox.LedgerState = KingdomLifecycleSinkState.Intent;
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			if (Sink == KingdomGrowthOutboxSinkKind.Chronicle) e.Outbox.ChronicleState = old;
			else e.Outbox.LedgerState = old;
			return false;
		}

		internal static bool CommitGrowthInspectableOutbox(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, int EventOrdinal,
			KingdomGrowthOutboxSinkKind Sink, int ObservedCount, string ObservedHash)
		{
			if (GrowthInspectableOutboxAction(Book, Operation, EventOrdinal, Sink,
				ObservedCount, ObservedHash) != KingdomLifecycleCasAction.Confirm) return false;
			KingdomGrowthOutboxEvent e = Operation.OutboxEvents[EventOrdinal];
			KingdomLifecycleSinkState oldState = Sink == KingdomGrowthOutboxSinkKind.Chronicle
				? e.Outbox.ChronicleState : e.Outbox.LedgerState;
			int oldCount = Sink == KingdomGrowthOutboxSinkKind.Chronicle
				? e.ChronicleObservedCount : e.LedgerObservedCount;
			string oldHash = Sink == KingdomGrowthOutboxSinkKind.Chronicle
				? e.ChronicleObservedHash : e.LedgerObservedHash;
			if (Sink == KingdomGrowthOutboxSinkKind.Chronicle)
			{
				e.Outbox.ChronicleState = KingdomLifecycleSinkState.Delivered;
				e.ChronicleObservedCount = ObservedCount;
				e.ChronicleObservedHash = ObservedHash;
			}
			else
			{
				e.Outbox.LedgerState = KingdomLifecycleSinkState.Delivered;
				e.LedgerObservedCount = ObservedCount;
				e.LedgerObservedHash = ObservedHash;
			}
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			if (Sink == KingdomGrowthOutboxSinkKind.Chronicle)
			{
				e.Outbox.ChronicleState = oldState; e.ChronicleObservedCount = oldCount;
				e.ChronicleObservedHash = oldHash;
			}
			else
			{
				e.Outbox.LedgerState = oldState; e.LedgerObservedCount = oldCount;
				e.LedgerObservedHash = oldHash;
			}
			return false;
		}

		internal static bool BeginGrowthAtMostOnceOutbox(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, int EventOrdinal,
			KingdomGrowthOutboxSinkKind Sink)
		{
			KingdomGrowthOutboxEvent e = GrowthOutboxEventAt(Book, Operation, EventOrdinal);
			if (e == null || (Sink != KingdomGrowthOutboxSinkKind.Message
				&& Sink != KingdomGrowthOutboxSinkKind.Deed
				&& Sink != KingdomGrowthOutboxSinkKind.Guestbook)) return false;
			KingdomLifecycleSinkState old = GrowthOutboxSinkState(e.Outbox, Sink);
			if (old != KingdomLifecycleSinkState.Pending) return false;
			SetGrowthOutboxSinkState(e.Outbox, Sink, KingdomLifecycleSinkState.Intent);
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			SetGrowthOutboxSinkState(e.Outbox, Sink, old); return false;
		}

		internal static bool CommitGrowthAtMostOnceOutbox(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation, int EventOrdinal,
			KingdomGrowthOutboxSinkKind Sink)
		{
			KingdomGrowthOutboxEvent e = GrowthOutboxEventAt(Book, Operation, EventOrdinal);
			if (e == null || (Sink != KingdomGrowthOutboxSinkKind.Message
				&& Sink != KingdomGrowthOutboxSinkKind.Deed
				&& Sink != KingdomGrowthOutboxSinkKind.Guestbook)
				|| GrowthOutboxSinkState(e.Outbox, Sink) != KingdomLifecycleSinkState.Intent)
				return false;
			SetGrowthOutboxSinkState(e.Outbox, Sink, KingdomLifecycleSinkState.Delivered);
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			SetGrowthOutboxSinkState(e.Outbox, Sink, KingdomLifecycleSinkState.Intent);
			return false;
		}

		internal static bool RecoverGrowthOutbox(KingdomGrowthBook Book,
			KingdomGrowthOperation Operation)
		{
			if (!ExactGrowthOperationAuthority(Book, Operation)
				|| Operation.Phase != KingdomGrowthPhase.Sinks
				|| Operation.OutboxEvents == null) return false;
			List<KingdomLifecycleSinkState> old = new List<KingdomLifecycleSinkState>();
			for (int i = 0; i < Operation.OutboxEvents.Count; i++)
			{
				KingdomLifecycleOutbox box = Operation.OutboxEvents[i].Outbox;
				old.Add(box.MessageState); old.Add(box.DeedState); old.Add(box.GuestbookState);
				if (box.MessageState == KingdomLifecycleSinkState.Intent)
					box.MessageState = KingdomLifecycleSinkState.Lost;
				if (box.DeedState == KingdomLifecycleSinkState.Intent)
					box.DeedState = KingdomLifecycleSinkState.Lost;
				if (box.GuestbookState == KingdomLifecycleSinkState.Intent)
					box.GuestbookState = KingdomLifecycleSinkState.Lost;
			}
			if (ExactGrowthOperationAuthority(Book, Operation)) return true;
			int p = 0;
			for (int i = 0; i < Operation.OutboxEvents.Count; i++)
			{
				KingdomLifecycleOutbox box = Operation.OutboxEvents[i].Outbox;
				box.MessageState = old[p++]; box.DeedState = old[p++];
				box.GuestbookState = old[p++];
			}
			return false;
		}

		private static KingdomGrowthOutboxEvent GrowthOutboxEventAt(KingdomGrowthBook book,
			KingdomGrowthOperation operation, int ordinal)
		{
			return ExactGrowthOperationAuthority(book, operation)
				&& operation.Phase == KingdomGrowthPhase.Sinks
				&& operation.OutboxEvents != null && ordinal >= 0
				&& ordinal < operation.OutboxEvents.Count ? operation.OutboxEvents[ordinal] : null;
		}

		private static KingdomLifecycleSinkState GrowthOutboxSinkState(
			KingdomLifecycleOutbox box, KingdomGrowthOutboxSinkKind sink)
		{
			switch (sink)
			{
			case KingdomGrowthOutboxSinkKind.Message: return box.MessageState;
			case KingdomGrowthOutboxSinkKind.Deed: return box.DeedState;
			case KingdomGrowthOutboxSinkKind.Guestbook: return box.GuestbookState;
			default: return KingdomLifecycleSinkState.None;
			}
		}

		private static void SetGrowthOutboxSinkState(KingdomLifecycleOutbox box,
			KingdomGrowthOutboxSinkKind sink, KingdomLifecycleSinkState state)
		{
			switch (sink)
			{
			case KingdomGrowthOutboxSinkKind.Message: box.MessageState = state; break;
			case KingdomGrowthOutboxSinkKind.Deed: box.DeedState = state; break;
			case KingdomGrowthOutboxSinkKind.Guestbook: box.GuestbookState = state; break;
			}
		}

	}
}
