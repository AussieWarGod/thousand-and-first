using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomTradeRules
	{
		public static bool CharterOutboxReadyForDispatch(KingdomTradeOperation Operation)
		{
			if (!CharterOutboxPayloadExact(Operation)) return false;
			KingdomTradeOutbox box = Operation.Outbox;
			return DispatchableSink(box.ChronicleState) && DispatchableSink(box.LedgerState)
				&& DispatchableSink(box.MessageState) && DispatchableSink(box.DeedState);
		}

		/// <summary>Quarantine may dispatch either exact normal payload or its distinct alert payload.</summary>
		public static bool CharterOutboxSafeForQuarantineDispatch(KingdomTradeOperation Operation)
		{
			return CharterOutboxLaneShape(Operation) || QuarantineCharterOutboxLaneShape(Operation);
		}

		private static bool TerminalCharterOutboxExact(KingdomTradeOperation Operation)
		{
			if (!CharterOutboxPayloadExact(Operation)) return false;
			KingdomTradeOutbox box = Operation.Outbox;
			return box.ChronicleState == KingdomTradeSinkState.Delivered
				&& box.LedgerState == KingdomTradeSinkState.Delivered
				&& box.MessageState == KingdomTradeSinkState.Delivered
				&& box.DeedState == KingdomTradeSinkState.Delivered;
		}

		private static bool QuarantineCharterOutboxExact(KingdomTradeOperation Operation)
		{
			if (!QuarantineCharterOutboxPayloadExact(Operation)) return false;
			KingdomTradeOutbox box = Operation.Outbox;
			return box.ChronicleState == KingdomTradeSinkState.Delivered
				&& box.LedgerState == KingdomTradeSinkState.Delivered
				&& box.MessageState == KingdomTradeSinkState.Delivered
				&& box.DeedState == KingdomTradeSinkState.Skipped;
		}

		private static bool CharterOutboxLaneShape(KingdomTradeOperation Operation)
		{
			if (!CharterOutboxPayloadExact(Operation)) return false;
			KingdomTradeOutbox box = Operation.Outbox;
			return MandatorySink(box.ChronicleState) && MandatorySink(box.LedgerState)
				&& MandatorySink(box.MessageState) && MandatorySink(box.DeedState);
		}

		private static bool QuarantineCharterOutboxLaneShape(KingdomTradeOperation Operation)
		{
			if (!QuarantineCharterOutboxPayloadExact(Operation)) return false;
			KingdomTradeOutbox box = Operation.Outbox;
			return MandatorySink(box.ChronicleState) && MandatorySink(box.LedgerState)
				&& MandatorySink(box.MessageState) && box.DeedState == KingdomTradeSinkState.Skipped;
		}

		private static bool CharterOutboxPayloadExact(KingdomTradeOperation Operation)
		{
			KingdomTradeOutbox box = Operation?.Outbox;
			return Operation != null && Operation.Kind == KingdomTradeOperationKind.CharterDelivery
				&& box != null && string.Equals(box.EventId, Operation.Id, StringComparison.Ordinal)
				&& ValidOutboxPayload(box.Chronicle) && ValidOutboxPayload(box.LedgerNote)
				&& ValidOutboxPayload(box.Message) && ValidOutboxPayload(box.Deed)
				&& box.LedgerDeliveredDelta == Operation.ProvedWater;
		}

		private static bool QuarantineCharterOutboxPayloadExact(KingdomTradeOperation Operation)
		{
			KingdomTradeOutbox box = Operation?.Outbox;
			return Operation != null && Operation.Kind == KingdomTradeOperationKind.CharterDelivery
				&& box != null && string.Equals(box.EventId, Operation.Id, StringComparison.Ordinal)
				&& ValidOutboxPayload(box.Chronicle) && ValidOutboxPayload(box.LedgerNote)
				&& ValidOutboxPayload(box.Message) && string.IsNullOrEmpty(box.Deed)
				&& box.LedgerDeliveredDelta == 0;
		}

		private static bool ValidOutboxPayload(string Value)
		{
			return !string.IsNullOrWhiteSpace(Value) && Value.Length <= MaxTextChars;
		}

		private static bool MandatorySink(KingdomTradeSinkState State)
		{
			return State == KingdomTradeSinkState.Pending || State == KingdomTradeSinkState.Intent
				|| State == KingdomTradeSinkState.Delivered || State == KingdomTradeSinkState.Lost;
		}

		private static bool DispatchableSink(KingdomTradeSinkState State)
		{
			return State == KingdomTradeSinkState.Pending || State == KingdomTradeSinkState.Delivered;
		}

		private static bool TerminalPhysical(KingdomTradePhysicalState State)
		{
			return State == KingdomTradePhysicalState.None
				|| State == KingdomTradePhysicalState.Proved
				|| State == KingdomTradePhysicalState.Skipped;
		}

		private static bool DurableDomainSettled(KingdomTradeBook Book,
			KingdomTradeOperation Operation)
		{
			if (Book == null || Operation == null) return false;
			if (Operation.ProjectionState == KingdomTradePhysicalState.Proved)
			{
				int matches = 0;
				for (int i = 0; i < (Book.Projections?.Count ?? 0); i++)
				{
					KingdomTradeProjectionRow row = Book.Projections[i];
					if (row != null && !row.Quarantined
						&& row.OperationSequence == Operation.Sequence
						&& string.Equals(row.OperationId, Operation.Id, StringComparison.Ordinal)
						&& string.Equals(row.SettlementId, Operation.SettlementId, StringComparison.Ordinal)
						&& string.Equals(row.ZoneId, Operation.ZoneId, StringComparison.Ordinal)
						&& string.Equals(row.ProjectionId, Operation.ProjectionId, StringComparison.Ordinal)
						&& string.Equals(row.ObjectId, Operation.ProjectionObjectId, StringComparison.Ordinal))
						matches++;
				}
				if (matches != 1) return false;
			}
			KingdomTradeManifestState manifest = Book.Manifest;
			switch (Operation.Kind)
			{
			case KingdomTradeOperationKind.CharterDelivery:
				int schedules = 0;
				KingdomTradeCharter charter = null;
				for (int i = 0; i < (Book.Charters?.Count ?? 0); i++)
				{
					KingdomTradeCharter row = Book.Charters[i];
					if (row == null || !(string.Equals(row.Id, Operation.CharterId,
							StringComparison.Ordinal)
						|| (string.Equals(row.DealKey, Operation.DealKey,
								StringComparison.Ordinal)
							&& string.Equals(row.Faction, Operation.Faction,
								StringComparison.Ordinal)))) continue;
					schedules++;
					charter = row;
				}
				if (schedules != 1 || charter == null
					|| !string.Equals(charter.Id, Operation.CharterId, StringComparison.Ordinal)
					|| !string.Equals(charter.DealKey, Operation.DealKey, StringComparison.Ordinal)
					|| !string.Equals(charter.Faction, Operation.Faction, StringComparison.Ordinal)
					|| charter.Sequence <= 0L || charter.Sequence >= Book.NextCharterSequence
					|| !string.Equals(charter.Id, CharterId(Book.RealmId, charter.Sequence),
						StringComparison.Ordinal)
					|| charter.CreatedTick < 0L || charter.CreatedTick > Operation.CreatedTick)
					return false;
				if (Operation.Phase == KingdomTradePhase.RetirementReady
					|| Operation.Phase == KingdomTradePhase.Terminal)
					return !charter.Quarantined && charter.NextTick == Operation.DueAfter;
				if (Operation.Phase != KingdomTradePhase.Quarantined) return false;
				return charter.NextTick == Operation.DueAfter
					|| (charter.Quarantined && charter.NextTick == Operation.DueBefore);
			case KingdomTradeOperationKind.ManifestLoad:
				return manifest != null
					&& manifest.OperationSequence == Operation.Sequence
					&& string.Equals(manifest.OperationId, Operation.Id, StringComparison.Ordinal)
					&& string.Equals(manifest.Id, Operation.ManifestId, StringComparison.Ordinal)
					&& manifest.EscrowDrams == Operation.ProvedWater;
			case KingdomTradeOperationKind.ManifestDelivery:
				return manifest != null
					&& string.Equals(manifest.Id, Operation.ManifestId, StringComparison.Ordinal)
					&& manifest.EscrowDrams == Operation.ManifestEscrowAfter;
			case KingdomTradeOperationKind.ManifestTurnback:
				return manifest != null && manifest.TurnedBack
					&& string.Equals(manifest.Id, Operation.ManifestId, StringComparison.Ordinal)
					&& string.Equals(manifest.OriginId, Operation.DestinationId, StringComparison.Ordinal)
					&& string.Equals(manifest.DestinationId, Operation.OriginId, StringComparison.Ordinal);
			case KingdomTradeOperationKind.ManifestLapse:
				return manifest != null
					&& string.Equals(manifest.Id, Operation.ManifestId, StringComparison.Ordinal)
					&& Book.RetainedEscrowDrams == Operation.RetainedAfter;
			case KingdomTradeOperationKind.PolityConsignmentDelivery:
				return Operation.Phase == KingdomTradePhase.Quarantined
					? Operation.ProvedWater == 0 ? Operation.RetainedBefore == 0L &&
						Operation.RetainedDelta == 0L && Operation.RetainedAfter == 0L &&
						Operation.RetainedState == KingdomTradePhysicalState.None :
						Operation.RetainedState == KingdomTradePhysicalState.Proved &&
						Operation.RetainedDelta == Operation.ProvedWater &&
						Book.RetainedEscrowDrams == Operation.RetainedAfter
					: Operation.ProvedWater > 0 && Operation.ProvedWater <=
						Operation.RequestedWater && Operation.RetainedDelta == 0L;
			default:
				return true;
			}
		}

	}
}
