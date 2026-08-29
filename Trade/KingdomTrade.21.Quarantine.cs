using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomTrade
	{
		private static void FinalizeQuarantine(KingdomSystem System, KingdomTradeBook Book,
			KingdomTradeOperation Operation, long Now, TradeLiveFrame Frame)
		{
			if (System == null || Book == null || Operation == null
				|| !ReferenceEquals(System.TradeBook, Book)
				|| !ReferenceEquals(Book.OpenOperation, Operation))
			{
				FailDetachedAuthority(Frame, "Detached trade quarantine could not finalize authority.");
				return;
			}
			if (Operation.Kind == KingdomTradeOperationKind.PolityConsignmentDelivery)
			{
				// Intent can mean a callback committed before throwing. It may be retired only
				// after the loaded immutable vessel classifies it as exact before/after.
				if (KingdomTradeRules.HasPolityWaterIntent(Operation)) return;
				KingdomTradeRules.SealUnstartedPolityConsignmentLegs(Operation);
			}
			if (Operation.Kind == KingdomTradeOperationKind.ManifestLoad
				&& Operation.ProvedWater > 0 && Book.Manifest == null)
			{
					Book.Manifest = new KingdomTradeManifestState
					{
						OperationSequence = Operation.Sequence,
						OperationId = Operation.Id,
						Id = Operation.ManifestId,
					OriginId = Operation.OriginId,
					OriginName = Operation.OriginName,
					DestinationId = Operation.DestinationId,
					DestinationName = Operation.DestinationName,
					OriginalDrams = Operation.RequestedWater,
					EscrowDrams = Operation.ProvedWater,
					LoadedTick = Operation.ManifestLoadedTick,
					DeadlineTick = Operation.ManifestDeadlineTick,
					Status = KingdomTradeManifestStatus.Quarantined,
					Fault = Operation.Fault
				};
			}
			if (Operation.Kind == KingdomTradeOperationKind.ManifestDelivery
				&& Book.Manifest != null && string.Equals(Book.Manifest.Id,
					Operation.ManifestId, StringComparison.Ordinal))
			{
				SettleManifestCreditAccounting(Book, Operation);
				Book.Manifest.Status = KingdomTradeManifestStatus.Quarantined;
				Book.Manifest.Fault = Operation.Fault;
			}
			if (Operation.Kind == KingdomTradeOperationKind.ManifestLapse
				&& Book.Manifest != null && string.Equals(Book.Manifest.Id,
					Operation.ManifestId, StringComparison.Ordinal))
			{
				SettleRetainedAccounting(Book, Operation);
				Book.Manifest.Status = KingdomTradeManifestStatus.Quarantined;
				Book.Manifest.Fault = Operation.Fault;
			}
			if (Operation.Kind == KingdomTradeOperationKind.PolityConsignmentDelivery &&
				Operation.ProvedWater > 0 &&
				!SettlePolityConsignmentRetention(Book, Operation)) return;
			RefreshBookDomain(Frame);
			System.SynchronizeLegacyManifestProjection();
			if (Operation.Outbox == null && Operation.Kind ==
				KingdomTradeOperationKind.PolityConsignmentDelivery)
			{
				// The directed conversation consumes the typed terminal-failure receipt.
				// No second unsolicited callback may strand exact retained custody.
				Operation.Outbox = new KingdomTradeOutbox
				{
					EventId = Operation.Id,
					ChronicleState = KingdomTradeSinkState.Skipped,
					LedgerState = KingdomTradeSinkState.Skipped,
					MessageState = KingdomTradeSinkState.Skipped,
					DeedState = KingdomTradeSinkState.Skipped
				};
			}
			else if (Operation.Outbox == null)
			{
				Operation.Outbox = new KingdomTradeOutbox
				{
					EventId = Operation.Id,
					Chronicle = "trade receipt " + Operation.Id + " was quarantined after proving "
						+ Operation.ProvedWater + " drams; " + (Operation.Fault ?? "physical state is uncertain"),
					ChronicleState = KingdomTradeSinkState.Pending,
					LedgerNote = "{{r|Trade receipt " + Operation.Id + " is quarantined: "
						+ (Operation.Fault ?? "physical state is uncertain") + ". It will not replay.}}",
					LedgerState = KingdomTradeSinkState.Pending,
					Message = "{{r|A trade receipt was quarantined and will not be repeated. Inspect the ledger.}}",
					MessageState = KingdomTradeSinkState.Pending,
					DeedState = KingdomTradeSinkState.Skipped
				};
			}
			if (Operation.Kind == KingdomTradeOperationKind.CharterDelivery
				&& !KingdomTradeRules.CharterOutboxSafeForQuarantineDispatch(Operation))
			{
				QuarantineScheduleAuthority(Book, Operation,
					"The malformed Charter outbox was retained and no external sink was called.");
				return;
			}
			if (!SettlePatternForQuarantine(System, Operation, Frame)) return;
			DispatchOutbox(System, Operation, Frame);
			if (!OutboxSettled(Operation.Outbox)) SettleOutboxAsLost(Operation);
			if (Operation.Kind == KingdomTradeOperationKind.CharterDelivery)
			{
				QuarantineScheduleAuthority(Book, Operation,
					"The charter receipt was quarantined; its schedule authority was frozen.");
			}
			KingdomTradeRules.Retire(Book, Operation, KingdomTradePhase.Quarantined,
				Now, Operation.Fault);
			System.SynchronizeLegacyManifestProjection();
		}

		private static void SettleOutboxAsLost(KingdomTradeOperation Operation)
		{
			if (Operation.Outbox == null) return;
			if (!KingdomTradeRules.SinkSettled(Operation.Outbox.ChronicleState))
				Operation.Outbox.ChronicleState = KingdomTradeSinkState.Lost;
			if (!KingdomTradeRules.SinkSettled(Operation.Outbox.LedgerState))
				Operation.Outbox.LedgerState = KingdomTradeSinkState.Lost;
			if (!KingdomTradeRules.SinkSettled(Operation.Outbox.MessageState))
				Operation.Outbox.MessageState = KingdomTradeSinkState.Lost;
			if (!KingdomTradeRules.SinkSettled(Operation.Outbox.DeedState))
				Operation.Outbox.DeedState = KingdomTradeSinkState.Lost;
		}

		private static void Quarantine(KingdomTradeOperation Operation, string Fault)
		{
			if (Operation?.Kind == KingdomTradeOperationKind.PolityConsignmentDelivery)
				KingdomTradeRules.SealUnstartedPolityConsignmentLegs(Operation);
			Operation.Fault = AppendFault(Operation.Fault, Fault);
			Operation.Phase = KingdomTradePhase.Quarantined;
		}

		private static bool QuarantineFalse(KingdomTradeOperation Operation, string Fault)
		{
			Quarantine(Operation, Fault);
			return false;
		}

		private static string AppendFault(string Existing, string Added)
		{
			if (!string.IsNullOrEmpty(Existing))
			{
				if (Existing.Length > KingdomTradeRules.MaxTextChars || string.IsNullOrEmpty(Added)
					|| Added.Length > KingdomTradeRules.MaxTextChars - Existing.Length - 2)
					return Existing;
				return Existing + "; " + Added;
			}
			if (Added == null || Added.Length <= KingdomTradeRules.MaxTextChars) return Added;
			return Added.Substring(0, KingdomTradeRules.MaxTextChars);
		}

		private static string FactionDisplay(string FactionName)
		{
			Faction faction = Factions.GetIfExists(FactionName);
			return faction == null ? (FactionName ?? "an unknown faction")
				: Faction.GetFormattedName(FactionName);
		}

	}
}
