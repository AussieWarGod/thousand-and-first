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
		private static void BuildOutbox(KingdomSystem System, KingdomTradeOperation Operation)
		{
			if (Operation.Outbox != null) return;
			string realm = KingdomPresentation.Rich(System.KingdomDisplayName);
			string origin = KingdomPresentation.Rich(Operation.OriginName);
			string destination = KingdomPresentation.Rich(Operation.DestinationName);
			string chronicle = null;
			string ledger = null;
			string message = null;
			string deed = null;
			switch (Operation.Kind)
			{
			case KingdomTradeOperationKind.CharterDelivery:
				string faction = FactionDisplay(Operation.Faction);
				chronicle = ((Operation.Cycles > 1) ? Operation.Cycles + " caravans of " : "a caravan of ")
					+ faction + " came to " + realm + " and delivered "
					+ Operation.ProvedWater + " drams under charter";
				ledger = "{{G|" + ((Operation.Cycles > 1) ? Operation.Cycles + " caravans of " : "A caravan of ")
					+ faction + " came under charter: " + Operation.ProvedWater + " drams"
					+ (Operation.ProvedWater < Operation.RequestedWater ? ", with the unplaced water retained by the caravan" : "")
					+ (Operation.MaterialRequested > Operation.MaterialProved ? "; some material remained quarantined" : "") + ".}}";
				message = "{{G|A chartered caravan of " + faction + " arrived.}}";
				deed = "the caravans that come to " + realm;
				break;
			case KingdomTradeOperationKind.ManifestLoad:
				chronicle = "the water-keepers of " + origin + " sent "
					+ Operation.ProvedWater + " drams toward " + destination;
				ledger = "{{G|" + Operation.ProvedWater + " drams left " + origin
					+ " under exact manifest " + Operation.ManifestId + ".}}";
				message = "{{G|" + Operation.ProvedWater + " drams leave the stores of "
					+ origin + ", bound for " + destination
					+ ".}} The road is given " + KingdomManifestRules.ManifestWindowDays
					+ " days; only exact proved placement can reduce its escrow.";
				break;
			case KingdomTradeOperationKind.ManifestDelivery:
				chronicle = Operation.ProvedWater > 0 ? "water sent from " + origin
					+ " reached " + destination + ": " + Operation.ProvedWater
					+ " drams entered its exact stores" : null;
				ledger = Operation.ProvedWater > 0 ? "{{G|A manifest from " + origin
					+ " delivered " + Operation.ProvedWater + " drams; "
					+ (Operation.RequestedWater - Operation.ProvedWater) + " remain in escrow.}}" : null;
				message = Operation.ProvedWater > 0 ? "{{G|The manifest carters have arrived.}}" : null;
				deed = Operation.ProvedWater > 0 ? "the water that reached "
					+ destination + " from " + origin : null;
				break;
			case KingdomTradeOperationKind.ManifestTurnback:
				chronicle = KingdomManifestRules.ManifestTurnedBackDeed(origin,
					destination, Operation.RequestedWater);
				ledger = "{{y|" + chronicle + ".}}";
				message = "{{y|The manifest turns back with all " + Operation.RequestedWater
					+ " escrowed drams still on its carts.}}";
				break;
			case KingdomTradeOperationKind.ManifestLapse:
				chronicle = "the twice-spent manifest road closed, and " + Operation.RequestedWater
					+ " drams remained retained under " + Operation.ManifestId;
				ledger = "{{y|The manifest road closed. Its " + Operation.RequestedWater
					+ " drams remain retained under permanent receipt; none were destroyed or reissued.}}";
				message = "{{y|The manifest road has closed; its escrow is retained for inspection.}}";
				break;
			}
			Operation.Outbox = new KingdomTradeOutbox
			{
				EventId = Operation.Id,
				Chronicle = chronicle,
				ChronicleState = chronicle == null ? KingdomTradeSinkState.Skipped : KingdomTradeSinkState.Pending,
				LedgerNote = ledger,
				LedgerDeliveredDelta = Operation.Kind == KingdomTradeOperationKind.CharterDelivery
					|| Operation.Kind == KingdomTradeOperationKind.ManifestDelivery
					? Operation.ProvedWater : 0,
				LedgerState = ledger == null && Operation.ProvedWater == 0
					? KingdomTradeSinkState.Skipped : KingdomTradeSinkState.Pending,
				Message = message,
				MessageState = message == null ? KingdomTradeSinkState.Skipped : KingdomTradeSinkState.Pending,
				Deed = deed,
				DeedState = deed == null ? KingdomTradeSinkState.Skipped : KingdomTradeSinkState.Pending
			};
		}

		private static bool DispatchOutbox(KingdomSystem System, KingdomTradeOperation Operation,
			TradeLiveFrame Frame)
		{
			KingdomTradeOutbox box = Operation.Outbox;
			if (box == null || Frame == null) return false;
			KingdomTradePhase expectedPhase = Operation.Phase;
			if (!ExactSinkFrame(Frame, Operation, box, expectedPhase)) return false;
			if (box.ChronicleState == KingdomTradeSinkState.Pending)
			{
				string eventId = box.EventId;
				string chronicle = box.Chronicle;
				box.ChronicleState = KingdomTradeSinkState.Intent;
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				if (callback == null) return SinkFrameFailed(Frame, Operation,
					"Chronicle callback frame could not be frozen.");
				bool delivered = KingdomChronicle.RecordOnce(System,
					eventId + ":chronicle", chronicle);
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactSinkFrame(Frame, Operation, box, expectedPhase)
					|| box.ChronicleState != KingdomTradeSinkState.Intent
					|| !string.Equals(box.EventId, eventId, StringComparison.Ordinal)
					|| !string.Equals(box.Chronicle, chronicle, StringComparison.Ordinal))
					return SinkFrameFailed(Frame, Operation,
						"The chronicle callback changed its exact trade sink frame.");
				box.ChronicleState = delivered
					? KingdomTradeSinkState.Delivered : KingdomTradeSinkState.Lost;
			}
			if (box.LedgerState == KingdomTradeSinkState.Pending)
			{
				if (!ExactSinkFrame(Frame, Operation, box, expectedPhase)) return false;
				int deliveredBefore = Frame.LedgerDelivered;
				string note = box.LedgerNote;
				int delta = box.LedgerDeliveredDelta;
				box.LedgerState = KingdomTradeSinkState.Intent;
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				if (callback == null) return SinkFrameFailed(Frame, Operation,
					"Ledger callback frame could not be frozen.");
				Frame.Ledger.Delivered = KingdomTradeRules.SaturatingAdd(
					deliveredBefore, delta);
				if (!string.IsNullOrEmpty(note)) Frame.Ledger.Note(note);
				if (!ExactCallbackWitness(Frame, callback)
					|| box.LedgerState != KingdomTradeSinkState.Intent
					|| !ExactLedgerAfter(Frame, deliveredBefore, delta, note)
					|| !ReferenceEquals(Operation.Outbox, box)
					|| !ExactSettlement(Frame) || !ExactPhysicalFrame(Frame,
						Operation, Frame.Zone))
					return SinkFrameFailed(Frame, Operation,
						"The exact settlement ledger CAS did not match its frozen delta and note.");
				Frame.LedgerDelivered = Frame.Ledger.Delivered;
				Frame.LedgerNoteRows = Frame.LedgerNotes.ToArray();
				box.LedgerState = KingdomTradeSinkState.Delivered;
			}
			if (box.MessageState == KingdomTradeSinkState.Pending)
			{
				if (!ExactSinkFrame(Frame, Operation, box, expectedPhase)) return false;
				string message = box.Message;
				box.MessageState = KingdomTradeSinkState.Intent;
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				if (callback == null) return SinkFrameFailed(Frame, Operation,
					"Message callback frame could not be frozen.");
				MessageQueue.AddPlayerMessage(message);
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactSinkFrame(Frame, Operation, box, expectedPhase)
					|| box.MessageState != KingdomTradeSinkState.Intent
					|| !string.Equals(box.Message, message, StringComparison.Ordinal))
					return SinkFrameFailed(Frame, Operation,
						"The player-message callback changed its exact trade sink frame.");
				box.MessageState = KingdomTradeSinkState.Delivered;
			}
			if (box.DeedState == KingdomTradeSinkState.Pending)
			{
				if (!ExactSinkFrame(Frame, Operation, box, expectedPhase)) return false;
				string deed = box.Deed;
				box.DeedState = KingdomTradeSinkState.Intent;
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				if (callback == null) return SinkFrameFailed(Frame, Operation,
					"Deed callback frame could not be frozen.");
				System.RecordDeed(deed);
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactSinkFrame(Frame, Operation, box, expectedPhase)
					|| box.DeedState != KingdomTradeSinkState.Intent
					|| !string.Equals(box.Deed, deed, StringComparison.Ordinal)
					|| !string.Equals(System.LastDeed, deed, StringComparison.Ordinal))
					return SinkFrameFailed(Frame, Operation,
						"The deed sink changed its exact trade frame.");
				box.DeedState = KingdomTradeSinkState.Delivered;
			}
			return true;
		}

		private static bool ExactSinkFrame(TradeLiveFrame Frame,
			KingdomTradeOperation Operation, KingdomTradeOutbox Outbox,
			KingdomTradePhase ExpectedPhase)
		{
			return ReferenceEquals(Operation?.Outbox, Outbox)
				&& ExactAuthority(Frame, ExpectedPhase)
				&& ExactPhysicalFrame(Frame, Operation, Frame.Zone);
		}

		private static bool SinkFrameFailed(TradeLiveFrame Frame,
			KingdomTradeOperation Operation, string Fault)
		{
			if (Frame == null || !ReferenceEquals(Frame.System?.TradeBook, Frame.Book)
				|| !ReferenceEquals(Frame.Book?.OpenOperation, Operation))
				return FailDetachedAuthority(Frame, Fault);
			Quarantine(Operation, Fault);
			return false;
		}

		private static bool ExactLedgerAfter(TradeLiveFrame Frame, int Before,
			int Delta, string Note)
		{
			if (Frame == null || Frame.Ledger == null
				|| !ReferenceEquals(Frame.System.Ledger, Frame.Ledger)
				|| !ReferenceEquals(Frame.Ledger.Notes, Frame.LedgerNotes)
				|| Frame.LedgerNotes == null || Frame.LedgerNoteRows == null
				|| Frame.Ledger.Delivered != KingdomTradeRules.SaturatingAdd(Before, Delta))
				return false;
			bool append = !string.IsNullOrEmpty(Note) && Frame.LedgerNoteRows.Length < 12;
			int expected = Frame.LedgerNoteRows.Length + (append ? 1 : 0);
			if (Frame.LedgerNotes.Count != expected) return false;
			for (int i = 0; i < Frame.LedgerNoteRows.Length; i++)
				if (!string.Equals(Frame.LedgerNotes[i], Frame.LedgerNoteRows[i],
					StringComparison.Ordinal)) return false;
			return !append || string.Equals(Frame.LedgerNotes[expected - 1], Note,
				StringComparison.Ordinal);
		}

		private static bool OutboxSettled(KingdomTradeOutbox Outbox)
		{
			return Outbox != null && KingdomTradeRules.SinkSettled(Outbox.ChronicleState)
				&& KingdomTradeRules.SinkSettled(Outbox.LedgerState)
				&& KingdomTradeRules.SinkSettled(Outbox.MessageState)
				&& KingdomTradeRules.SinkSettled(Outbox.DeedState);
		}

	}
}
