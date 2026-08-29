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
		public static bool TryRegisterGrowthField(KingdomGrowthBook Book, string FieldId)
		{
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| !ValidRootId(FieldId)) return false;
			KingdomGrowthFieldSlot existing = FindGrowthField(Book, FieldId);
			if (existing != null) return !existing.Quarantined;
			if (Book.FieldOps.Count >= MaxGrowthFields) return false;
			KingdomGrowthFieldSlot added = new KingdomGrowthFieldSlot { FieldId = FieldId };
			Book.FieldOps.Add(added);
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			Book.FieldOps.RemoveAt(Book.FieldOps.Count - 1);
			return false;
		}

		internal static bool InstallGrowthFieldBootstrap(KingdomGrowthBook Book,
			KingdomGrowthFieldState State, List<KingdomGrowthCropRow> Rows)
		{
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| State == null || Rows == null || !GrowthFieldStateShape(State, State.FieldId)
				|| !GrowthCropRowsShape(Rows, State.FieldId, false, null)
				|| State.DeclaredRows != Rows.Count) return false;
			KingdomGrowthFieldSlot field = FindGrowthField(Book, State.FieldId);
			if (field == null || field.Quarantined || field.Operation != null
				|| !GrowthFieldMatchesState(field, new KingdomGrowthFieldState
				{
					FieldId = field.FieldId, X = -1, Y = -1
				})) return false;
			for (int i = 0; i < Rows.Count; i++)
				if (!string.Equals(Rows[i].FieldId, State.FieldId, StringComparison.Ordinal)) return false;
			KingdomGrowthFieldState before = GrowthFieldState(field);
			List<KingdomGrowthCropRow> rowsBefore = new List<KingdomGrowthCropRow>(Book.CropRows);
			ApplyGrowthFieldState(field, State);
			for (int i = 0; i < Rows.Count; i++) Book.CropRows.Add(CloneGrowthCropRow(Rows[i]));
			if (CanOwnGrowthAuthority(Book, Book.SettlementId)) return true;
			ApplyGrowthFieldState(field, before); Book.CropRows.Clear();
			Book.CropRows.AddRange(rowsBefore); return false;
		}

		public static KingdomGrowthOperation PrepareGrowthOperation(KingdomGrowthBook Book,
			KingdomGrowthAction Action, string FieldId, long Tick)
		{
			bool productiveStarter = Action != KingdomGrowthAction.Withdraw;
			if (!CanOwnGrowthAuthority(Book, Book == null ? null : Book.SettlementId)
				|| Tick < 0L || !KnownGrowthAction(Action)
				|| (productiveStarter && (Book.OptionState != KingdomLifecycleOptionState.Enabled
					|| Book.HealthState != KingdomGrowthHealthState.Healthy || Book.WorkPaused))
				|| Tick < Book.OptionTick || Tick < Book.HealthTick
				|| Tick < Book.ScarcityOptionTick || Tick < Book.EffectiveWorkTick)
				return null;
			KingdomGrowthSlotKind slot = SlotForGrowthAction(Action);
			if (slot == KingdomGrowthSlotKind.None) return null;
			if (slot != KingdomGrowthSlotKind.Field && FieldId != null && FieldId.Length == 0)
				FieldId = null;
			KingdomGrowthFieldSlot field = slot == KingdomGrowthSlotKind.Field
				? FindGrowthField(Book, FieldId) : null;
			if (slot == KingdomGrowthSlotKind.Field && (field == null || field.Quarantined)) return null;
			if (slot != KingdomGrowthSlotKind.Field && FieldId != null) return null;
			if (GetGrowthOperation(Book, slot, FieldId) != null) return null;
			long next = GetGrowthNext(Book, slot, field);
			long retired = GetGrowthRetired(Book, slot, field);
			if (!IsExactSuccessor(next, retired) || next == long.MaxValue) return null;
			if (Action == KingdomGrowthAction.Arrival &&
				(Book.ArrivalIntervalTicks <= 0L || Book.NextArrivalTick <= 0L
					|| Tick < Book.NextArrivalTick
					|| !Book.ArrivalCadenceMigrationPending
						&& (Book.ArrivalOpportunity == null
							|| Tick < Book.ArrivalOpportunity.DueTick))) return null;
			long actionClockBefore = GrowthClockValue(Book, Action, field);
			long clockBefore = slot == KingdomGrowthSlotKind.Field
				? field.CommitRevision : actionClockBefore;
			long clockAfter;
			long effectiveNow;
			if (!TryGrowthEffectiveNow(Book, Tick, out effectiveNow)) return null;
			long fieldClockAfter = slot == KingdomGrowthSlotKind.Field
				? Math.Max(field.ClockTick, effectiveNow) : 0L;
			if (slot == KingdomGrowthSlotKind.Field)
			{
				if (!CheckedAdd(clockBefore, 1L, out clockAfter)) return null;
			}
			else if (Action == KingdomGrowthAction.Arrival)
			{
				if (Book.ArrivalCadenceMigrationPending)
				{
					if (!CheckedAdd(Tick, Book.ArrivalIntervalTicks, out clockAfter)) return null;
				}
				else
				{
					clockAfter = ArrivalClockAfterOpportunity(Book);
					if (clockAfter <= clockBefore) return null;
				}
			}
			else if (Action == KingdomGrowthAction.Heartbeat
				|| Action == KingdomGrowthAction.Fetch || Action == KingdomGrowthAction.Mill)
			{
				if (Tick <= clockBefore) return null;
				clockAfter = Tick;
			}
			else
			{
				if (!CheckedAdd(clockBefore, 1L, out clockAfter)) return null;
				if (Tick > clockAfter) clockAfter = Tick;
			}
			long delta;
			if (!CheckedAdd(clockAfter, -clockBefore, out delta) || delta == 0L) return null;
			string id = GrowthOperationId(Book.SettlementId, slot, FieldId, next);
			string subject = GrowthClockSubject(Book.SettlementId, slot, FieldId);
			string key = ResourceKey(KingdomLifecycleResourceKind.GrowthClock,
				Book.SettlementId, subject);
			KingdomLifecycleResourceRevision row = FindGrowthResource(Book, key);
			if (id == null || key == null || (row != null && (!string.IsNullOrEmpty(row.ActiveOperationId)
				|| row.Revision == long.MaxValue))) return null;
			long revision = row == null ? 0L : row.Revision;
			KingdomGrowthOperation operation = new KingdomGrowthOperation
			{
				Sequence = next, Id = id, Action = Action, Phase = KingdomGrowthPhase.Prepared,
				CreatedTick = Tick, UpdatedTick = Tick, SettlementId = Book.SettlementId,
				FieldId = FieldId, TargetX = -1, TargetY = -1,
				OptionState = Book.OptionState, OptionTick = Book.OptionTick,
				HealthState = Book.HealthState, HealthTick = Book.HealthTick,
					EffectiveWorkBefore = Book.EffectiveWorkTick,
				EffectiveWorkAfter = IsGrowthFieldAction(Action)
						? effectiveNow : Book.EffectiveWorkTick,
					FieldClockBefore = slot == KingdomGrowthSlotKind.Field ? field.ClockTick : 0L,
					FieldClockAfter = fieldClockAfter,
				HeartbeatBefore = Book.LastHeartbeatTick,
					HeartbeatAfter = Action == KingdomGrowthAction.Heartbeat
						? clockAfter : Book.LastHeartbeatTick,
				ArrivalBefore = Book.NextArrivalTick,
				ArrivalAfter = Action == KingdomGrowthAction.Arrival ? clockAfter : Book.NextArrivalTick,
					FetchBefore = Book.LastFetchTick,
					FetchAfter = Action == KingdomGrowthAction.Fetch ? clockAfter : Book.LastFetchTick,
					MillBefore = Book.LastMillTick,
					MillAfter = Action == KingdomGrowthAction.Mill ? clockAfter : Book.LastMillTick,
					SubsidenceBefore = Book.LastSubsidenceTick,
					SubsidenceAfter = Book.LastSubsidenceTick,
					DeliveryBefore = Book.LastDeliveryTick,
					DeliveryAfter = Action == KingdomGrowthAction.Delivery
						? clockAfter : Book.LastDeliveryTick,
					DepartureBefore = Book.LastDepartureTick,
					DepartureAfter = Action == KingdomGrowthAction.Departure
						? clockAfter : Book.LastDepartureTick,
					ScarcityOptionState = Book.ScarcityOptionState,
					ScarcityOptionTick = Book.ScarcityOptionTick,
				PendingCropBefore = Book.PendingCrop, PendingCropAfter = Book.PendingCrop,
				PendingCropBlueprintBefore = Book.PendingCropBlueprint,
				PendingCropZoneIdBefore = Book.PendingCropZoneId,
				PendingCropBlueprintAfter = Book.PendingCropBlueprint,
				PendingCropZoneIdAfter = Book.PendingCropZoneId,
				ClockState = KingdomLifecyclePhysicalState.Prepared,
					ClockLease = new KingdomLifecycleResourceLease
				{
					OperationId = id, Kind = KingdomLifecycleResourceKind.GrowthClock,
					ScopeId = Book.SettlementId, SubjectId = subject, Key = key,
					Before = clockBefore, Delta = delta, After = clockAfter,
					BeforeRevision = revision, AfterRevision = revision + 1L,
					State = KingdomLifecycleLeaseState.Prepared
				}
			};
			if (Action == KingdomGrowthAction.Arrival && !Book.ArrivalCadenceMigrationPending)
			{
				KingdomGrowthArrivalOpportunity opportunity = Book.ArrivalOpportunity;
				operation.ArrivalOpportunityOrdinal = opportunity.Ordinal;
				operation.ArrivalOpportunityDueTick = opportunity.DueTick;
				operation.ArrivalOpportunityRateEpoch = opportunity.RateEpoch;
				operation.ArrivalOpportunityPayloadHash = opportunity.PayloadHash;
			}
			operation.OutboxEvents = new List<KingdomGrowthOutboxEvent>();
			return operation;
		}

		public static KingdomLifecycleOutbox PrepareGrowthOutbox(KingdomGrowthOperation Operation,
			string Chronicle, string Ledger, string Message, string Deed, string Guestbook)
		{
			if (Operation == null || !ValidGeneratedId(Operation.Id)) return null;
			if (Chronicle != null && Chronicle.Length == 0) Chronicle = null;
			if (Ledger != null && Ledger.Length == 0) Ledger = null;
			if (Message != null && Message.Length == 0) Message = null;
			if (Deed != null && Deed.Length == 0) Deed = null;
			if (Guestbook != null && Guestbook.Length == 0) Guestbook = null;
			return new KingdomLifecycleOutbox
			{
				OperationId = Operation.Id, EventId = ChildId(Operation.Id, "outbox", 0),
				ChronicleReceiptId = ChildId(Operation.Id, "chronicle", 0),
				Chronicle = Chronicle, ChronicleDisposition = InitialDisposition(Chronicle),
				ChronicleState = InitialSink(Chronicle), Ledger = Ledger,
				LedgerDisposition = InitialDisposition(Ledger), LedgerState = InitialSink(Ledger),
				Message = Message, MessageDisposition = InitialDisposition(Message),
				MessageState = InitialSink(Message), Deed = Deed,
				DeedDisposition = InitialDisposition(Deed), DeedState = InitialSink(Deed),
				GuestbookLine = Guestbook, GuestbookDisposition = InitialDisposition(Guestbook),
				GuestbookState = InitialSink(Guestbook)
			};
		}

		public static KingdomGrowthOutboxEvent PrepareGrowthOutboxEvent(
			KingdomGrowthOperation Operation, int Ordinal, string Kind, string Chronicle,
			string Ledger, string Message, string Deed, string Guestbook)
		{
			return PrepareGrowthOutboxEvent(Operation, Ordinal, Kind, Chronicle, Ledger,
				Message, Deed, Guestbook, 0, null, 0, null, 0, null, 0, null,
				0, null, 0, null);
		}

		internal static string GrowthChronicleOutboxReceiptId(
			KingdomGrowthOperation Operation, int Ordinal)
		{
			if (Operation == null || !ValidGeneratedId(Operation.Id) || Ordinal < 0
				|| Ordinal >= MaxGrowthOutboxEvents) return null;
			string eventId = ChildId(Operation.Id, "outbox-event", Ordinal);
			return ChildId(eventId, "chronicle", 0);
		}

		public static KingdomGrowthOutboxEvent PrepareGrowthOutboxEvent(
			KingdomGrowthOperation Operation, int Ordinal, string Kind, string Chronicle,
			string Ledger, string Message, string Deed, string Guestbook,
			int ChronicleBeforeCount, string ChronicleBeforeHash,
			int ChronicleDeclaredAfterCount, string ChronicleDeclaredAfterHash,
			int LedgerBeforeCount, string LedgerBeforeHash,
			int LedgerDeclaredAfterCount, string LedgerDeclaredAfterHash)
		{
			// The v1 signature remains callable for source compatibility, but v2 cannot mint a
			// one-register Chronicle promise. Null Chronicle declarations remain canonical.
			if (Chronicle != null) return null;
			return PrepareGrowthOutboxEvent(Operation, Ordinal, Kind, Chronicle, Ledger,
				Message, Deed, Guestbook, ChronicleBeforeCount, ChronicleBeforeHash,
				ChronicleDeclaredAfterCount, ChronicleDeclaredAfterHash,
				0, null, 0, null, LedgerBeforeCount, LedgerBeforeHash,
				LedgerDeclaredAfterCount, LedgerDeclaredAfterHash);
		}

		public static KingdomGrowthOutboxEvent PrepareGrowthOutboxEvent(
			KingdomGrowthOperation Operation, int Ordinal, string Kind, string Chronicle,
			string Ledger, string Message, string Deed, string Guestbook,
			int ChronicleBeforeCount, string ChronicleBeforeHash,
			int ChronicleDeclaredAfterCount, string ChronicleDeclaredAfterHash,
			int OutsiderBeforeCount, string OutsiderBeforeHash,
			int OutsiderDeclaredAfterCount, string OutsiderDeclaredAfterHash,
			int LedgerBeforeCount, string LedgerBeforeHash,
			int LedgerDeclaredAfterCount, string LedgerDeclaredAfterHash)
		{
			// A v2 Chronicle callback must carry exact rendered entries. Compatibility callers
			// without them may still prepare every non-Chronicle sink.
			if (Chronicle != null && Chronicle.Length > 0) return null;
			return PrepareDeclaredGrowthOutboxEvent(Operation, Ordinal, Kind, Chronicle,
				null, null, Ledger, Message, Deed, Guestbook, ChronicleBeforeCount,
				ChronicleBeforeHash, ChronicleDeclaredAfterCount, ChronicleDeclaredAfterHash,
				OutsiderBeforeCount, OutsiderBeforeHash, OutsiderDeclaredAfterCount,
				OutsiderDeclaredAfterHash, LedgerBeforeCount, LedgerBeforeHash,
				LedgerDeclaredAfterCount, LedgerDeclaredAfterHash);
		}

	}
}
