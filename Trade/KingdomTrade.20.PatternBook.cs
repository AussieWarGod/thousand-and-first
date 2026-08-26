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
		private static bool ContinuePatternBook(KingdomSystem System,
			KingdomTradeOperation Operation, TradeLiveFrame Frame)
		{
			KingdomTradePatternReceipt receipt = Operation?.Pattern;
			if (Operation == null || Operation.Kind != KingdomTradeOperationKind.CharterDelivery
				|| receipt == null || !KingdomTradePatternRules.Valid(receipt))
				return QuarantineFalse(Operation,
					"The CharterDelivery pattern receipt was missing or malformed before retirement.");
			if (KingdomTradePatternRules.Terminal(receipt)) return true;

			if (receipt.State == KingdomTradePatternState.Offered
				|| receipt.State == KingdomTradePatternState.ChoiceIntent)
			{
				if (!KingdomTradePatternRules.BeginChoice(receipt)
					|| !ExactAuthority(Frame, KingdomTradePhase.ScheduleIntent)
					|| !ExactPhysicalFrame(Frame, Operation, Frame.Zone))
					return QuarantineFalse(Operation,
						"The pattern-book choice lost its exact settlement frame.");
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				if (callback == null) return QuarantineFalse(Operation,
					"The pattern-book choice callback could not be frozen.");
				int pick = KingdomCeremony.PickPatternBook(receipt);
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactAuthority(Frame, KingdomTradePhase.ScheduleIntent)
					|| !ExactPhysicalFrame(Frame, Operation, Frame.Zone))
					return FailDetachedAuthority(Frame,
						"The pattern-book UI callback changed its exact trade authority or city.");
				if (pick < 0 || pick >= receipt.Offers.Count)
				{
					if (!KingdomTradePatternRules.Decline(receipt))
						return QuarantineFalse(Operation,
							"The pattern-book decline did not match its choice intent.");
					return true;
				}
				string failure;
				if (!KingdomTradePatternRules.TrySelect(receipt, pick,
					Frame.KeepersRoster, KingdomPresentation.Rich(Operation.SettlementName), out failure))
				{
					KingdomTradePatternRules.MarkConflict(receipt, failure);
					KingdomLog.Log("trade: pattern-book selection refused: " + failure);
					return true;
				}
			}

			if (receipt.State == KingdomTradePatternState.Selected
				|| receipt.State == KingdomTradePatternState.RosterIntent)
			{
				KingdomTradePatternCasVerdict verdict =
					KingdomTradePatternRules.InspectRoster(receipt, System.KeepersRoster);
				if (verdict == KingdomTradePatternCasVerdict.ThirdValue)
				{
					KingdomTradePatternRules.MarkConflict(receipt,
						"The seated city's stored roster was neither the frozen before nor after value; it was not overwritten.");
					return true;
				}
				if (verdict == KingdomTradePatternCasVerdict.Invalid)
					return QuarantineFalse(Operation,
						"The pattern-book roster CAS evidence was malformed.");
				if (verdict == KingdomTradePatternCasVerdict.Apply)
				{
					if (!KingdomTradePatternRules.MarkRosterIntent(receipt)
						|| !ExactAuthority(Frame, KingdomTradePhase.ScheduleIntent)
						|| !string.Equals(System.KeepersRoster ?? "", receipt.RosterBefore,
							StringComparison.Ordinal))
						return QuarantineFalse(Operation,
							"The pattern-book roster changed before its exact CAS.");
					System.KeepersRoster = receipt.RosterAfter;
					Frame.KeepersRoster = receipt.RosterAfter;
					if (!ExactAuthority(Frame, KingdomTradePhase.ScheduleIntent)
						|| !string.Equals(System.City?.SettlementId, Operation.SettlementId,
							StringComparison.Ordinal)
						|| KingdomTradePatternRules.InspectRoster(receipt,
							System.KeepersRoster) != KingdomTradePatternCasVerdict.AlreadyApplied)
						return FailDetachedAuthority(Frame,
							"The exact city-roster CAS did not publish its frozen after value.");
				}
				if (!KingdomTradePatternRules.MarkLearned(receipt))
					return QuarantineFalse(Operation,
						"The pattern-book roster proof could not settle as learned.");
			}

			if (receipt.State == KingdomTradePatternState.Learned
				&& !DispatchPatternSinks(System, Operation, Frame,
					KingdomTradePhase.ScheduleIntent)) return false;
			return KingdomTradePatternRules.Terminal(receipt);
		}

		private static bool DispatchPatternSinks(KingdomSystem System,
			KingdomTradeOperation Operation, TradeLiveFrame Frame,
			KingdomTradePhase ExpectedPhase)
		{
			KingdomTradePatternReceipt receipt = Operation?.Pattern;
			if (receipt == null || receipt.State != KingdomTradePatternState.Learned
				|| !KingdomTradePatternRules.Valid(receipt)) return false;
			if (receipt.ChronicleState == KingdomTradeSinkState.Pending
				|| receipt.ChronicleState == KingdomTradeSinkState.Intent)
			{
				receipt.ChronicleState = KingdomTradeSinkState.Intent;
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				if (callback == null) return false;
				bool settled = KingdomChronicle.RecordOnce(System,
					Operation.Id + ":pattern:chronicle", receipt.Chronicle);
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactAuthority(Frame, ExpectedPhase)
					|| receipt.ChronicleState != KingdomTradeSinkState.Intent)
					return FailDetachedAuthority(Frame,
						"The pattern-book chronicle callback changed its exact receipt.");
				if (!settled) return false;
				receipt.ChronicleState = KingdomTradeSinkState.Delivered;
			}
			// MessageQueue has no receipt lookup. Intent on re-entry is conservatively lost,
			// while Pending gets exactly one callback attempt in this process.
			if (receipt.MessageState == KingdomTradeSinkState.Intent)
				receipt.MessageState = KingdomTradeSinkState.Lost;
			if (receipt.MessageState == KingdomTradeSinkState.Pending)
			{
				receipt.MessageState = KingdomTradeSinkState.Intent;
				CallbackWitness callback = CaptureCallbackWitness(Frame);
				if (callback == null) return false;
				MessageQueue.AddPlayerMessage(receipt.Message);
				if (!ExactCallbackWitness(Frame, callback)
					|| !ExactAuthority(Frame, ExpectedPhase)
					|| receipt.MessageState != KingdomTradeSinkState.Intent)
					return FailDetachedAuthority(Frame,
						"The pattern-book message callback changed its exact receipt.");
				receipt.MessageState = KingdomTradeSinkState.Delivered;
			}
			return KingdomTradePatternRules.Terminal(receipt);
		}

		private static bool SettlePatternForQuarantine(KingdomSystem System,
			KingdomTradeOperation Operation, TradeLiveFrame Frame)
		{
			if (Operation.Kind != KingdomTradeOperationKind.CharterDelivery) return true;
			KingdomTradePatternReceipt receipt = Operation.Pattern;
			if (receipt == null || !KingdomTradePatternRules.Valid(receipt))
			{
				Operation.Pattern = KingdomTradePatternRules.Conflict(
					"The quarantined charter had no valid frozen pattern-book receipt.");
				return true;
			}
			if (KingdomTradePatternRules.Terminal(receipt)) return true;
			if (receipt.State == KingdomTradePatternState.Offered
				|| receipt.State == KingdomTradePatternState.ChoiceIntent)
			{
				KingdomTradePatternRules.MarkConflict(receipt,
					"The charter was quarantined before its frozen pattern-book choice settled.");
				return true;
			}
			if (receipt.State == KingdomTradePatternState.Selected
				|| receipt.State == KingdomTradePatternState.RosterIntent)
			{
				KingdomTradePatternCasVerdict verdict =
					KingdomTradePatternRules.InspectRoster(receipt, System.KeepersRoster);
				bool exactCity = string.Equals(System.City?.SettlementId,
					Operation.SettlementId, StringComparison.Ordinal);
				if (exactCity && verdict == KingdomTradePatternCasVerdict.AlreadyApplied)
				{
					if (!KingdomTradePatternRules.MarkLearned(receipt)) return false;
				}
				else
				{
					KingdomTradePatternRules.MarkConflict(receipt,
						"The charter was quarantined before its exact city-roster CAS could be proved applied.");
					return true;
				}
			}
			if (receipt.State == KingdomTradePatternState.Learned)
				return DispatchPatternSinks(System, Operation, Frame,
					KingdomTradePhase.Quarantined);
			Operation.Pattern = KingdomTradePatternRules.Conflict(
				"The quarantined charter had an unrecognized pattern-book continuation state.");
			return true;
		}

		private static bool SettleSchedule(KingdomTradeBook Book,
			KingdomTradeOperation Operation, TradeLiveFrame Frame)
		{
			if (Operation.Kind != KingdomTradeOperationKind.CharterDelivery) return true;
			KingdomTradeCharter charter = null;
			int matches = 0;
			for (int i = 0; i < Book.Charters.Count; i++)
			{
				KingdomTradeCharter row = Book.Charters[i];
				if (row == null || !(string.Equals(row.Id, Operation.CharterId,
						StringComparison.Ordinal)
					|| (string.Equals(row.DealKey, Operation.DealKey, StringComparison.Ordinal)
						&& string.Equals(row.Faction, Operation.Faction,
							StringComparison.Ordinal)))) continue;
				matches++;
				charter = row;
			}
			if (matches != 1 || charter == null || charter.Quarantined
				|| !string.Equals(charter.Id, Operation.CharterId, StringComparison.Ordinal)
				|| !string.Equals(charter.DealKey, Operation.DealKey, StringComparison.Ordinal)
				|| !string.Equals(charter.Faction, Operation.Faction, StringComparison.Ordinal))
			{
				QuarantineScheduleAuthority(Book, Operation,
					"The exact charter schedule row disappeared or collided.");
				return false;
			}
			if (!ExactAuthority(Frame, KingdomTradePhase.ScheduleIntent)
				|| !ExactPhysicalFrame(Frame, Operation, Frame.Zone))
				return QuarantineFalse(Operation,
					"The charter schedule frame changed before its exact CAS.");
			if (charter.NextTick == Operation.DueAfter) return true;
			if (charter.NextTick != Operation.DueBefore)
				return QuarantineFalse(Operation,
					"Charter schedule changed outside its frozen before/after CAS; it was not overwritten.");
			charter.NextTick = Operation.DueAfter;
			return ExactAuthority(Frame, KingdomTradePhase.ScheduleIntent)
				&& ExactPhysicalFrame(Frame, Operation, Frame.Zone)
				&& charter.NextTick == Operation.DueAfter;
		}

		private static void QuarantineScheduleAuthority(KingdomTradeBook Book,
			KingdomTradeOperation Operation, string Fault)
		{
			if (Book?.Charters != null)
			{
				for (int i = 0; i < Book.Charters.Count; i++)
				{
					KingdomTradeCharter row = Book.Charters[i];
					if (row == null || !(string.Equals(row.Id, Operation.CharterId,
							StringComparison.Ordinal)
						|| (string.Equals(row.DealKey, Operation.DealKey,
								StringComparison.Ordinal)
							&& string.Equals(row.Faction, Operation.Faction,
								StringComparison.Ordinal)))) continue;
					row.Quarantined = true;
					row.Fault = AppendFault(row.Fault, Fault);
				}
			}
			Quarantine(Operation, Fault);
		}

	}
}
