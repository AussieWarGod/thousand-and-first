using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>
	/// Pure, bounded pattern-book protocol. A CharterDelivery owns its draw, exact offer, choice,
	/// city-roster CAS, and sink dispositions from prepare through retirement.
	/// </summary>
	public static partial class KingdomTradePatternRules
	{
		public const int MaxOffers = 3;

		public static KingdomTradePatternReceipt Freeze(string SettlementId, long OperationSequence,
			IEnumerable<KingdomTradePatternDesign> Candidates)
		{
			List<KingdomTradePatternDesign> exact = CanonicalCandidates(Candidates);
			if (exact.Count == 0) return Empty(KingdomTradePatternState.NoCandidates);
			if (!KingdomTradeRules.ValidId(SettlementId) || OperationSequence <= 0L
				|| !KingdomCeremonyRules.ShouldOfferPattern(SettlementId,
					unchecked((ulong)OperationSequence)))
				return Empty(KingdomTradePatternState.ChanceMiss);

			KingdomTradePatternReceipt receipt = Empty(KingdomTradePatternState.Offered);
			for (int step = 0; step < MaxOffers && exact.Count > 0; step++)
			{
				int index = KingdomCeremonyRules.PickPatternIndex(SettlementId,
					unchecked((ulong)OperationSequence), step, exact.Count);
				if (index < 0 || index >= exact.Count) index = 0;
				receipt.Offers.Add(Copy(exact[index]));
				exact.RemoveAt(index);
			}
			return receipt;
		}

		/// <summary>Prior-wire CharterDelivery operations migrate with the additive lane skipped.</summary>
		public static KingdomTradePatternReceipt PriorWireDefault()
		{
			return Empty(KingdomTradePatternState.None);
		}

		/// <summary>A clean terminal receipt for a lane which cannot safely continue.</summary>
		public static KingdomTradePatternReceipt Conflict(string Fault)
		{
			KingdomTradePatternReceipt receipt = Empty(KingdomTradePatternState.Conflict);
			receipt.Fault = Bound(Fault);
			return receipt;
		}

		public static bool BeginChoice(KingdomTradePatternReceipt Receipt)
		{
			if (Receipt == null) return false;
			if (Receipt.State == KingdomTradePatternState.ChoiceIntent) return true;
			if (Receipt.State != KingdomTradePatternState.Offered) return false;
			Receipt.State = KingdomTradePatternState.ChoiceIntent;
			return true;
		}

		public static bool Decline(KingdomTradePatternReceipt Receipt)
		{
			if (Receipt == null || (Receipt.State != KingdomTradePatternState.Offered
				&& Receipt.State != KingdomTradePatternState.ChoiceIntent)) return false;
			Receipt.State = KingdomTradePatternState.Declined;
			Receipt.SelectedIndex = -1;
			Receipt.RosterBefore = null;
			Receipt.RosterAfter = null;
			Receipt.Chronicle = null;
			Receipt.ChronicleState = KingdomTradeSinkState.Skipped;
			Receipt.Message = null;
			Receipt.MessageState = KingdomTradeSinkState.Skipped;
			return true;
		}

		public static bool TrySelect(KingdomTradePatternReceipt Receipt, int Index,
			string CurrentStoredRoster, string SettlementName, out string Failure)
		{
			Failure = null;
			if (Receipt == null || (Receipt.State != KingdomTradePatternState.Offered
				&& Receipt.State != KingdomTradePatternState.ChoiceIntent)
				|| Receipt.Offers == null || Index < 0 || Index >= Receipt.Offers.Count
				|| !ValidDesign(Receipt.Offers[Index])
				|| !KingdomTradeRules.ValidName(SettlementName))
			{
				Failure = "Pattern selection did not match its exact frozen offer and city.";
				return false;
			}
			string before;
			List<string> rows;
			if (!TryCanonicalRoster(CurrentStoredRoster, out before, out rows))
			{
				Failure = "The seated city's stored knowledge roster is malformed.";
				return false;
			}
			KingdomTradePatternDesign selected = Receipt.Offers[Index];
			string key = KingdomZoningRules.ComposeKey(
				KingdomCeremonyRules.PatternKnowledgeKind, selected.LearnName);
			if (string.IsNullOrEmpty(key))
			{
				Failure = "The selected foreign pattern no longer composes a bounded knowledge key.";
				return false;
			}
			Receipt.SelectedIndex = Index;
			Receipt.RosterBefore = before;
			if (rows.Contains(key))
			{
				Receipt.RosterAfter = before;
				Receipt.State = KingdomTradePatternState.AlreadyKnown;
				Receipt.Chronicle = null;
				Receipt.ChronicleState = KingdomTradeSinkState.Skipped;
				Receipt.Message = null;
				Receipt.MessageState = KingdomTradeSinkState.Skipped;
				return true;
			}
			rows.Add(key);
			string after;
			if (!KingdomZoningRules.TryEncodeRoster(rows, out after))
			{
				Receipt.SelectedIndex = -1;
				Receipt.RosterBefore = null;
				Failure = "The seated city's knowledge roster has no bounded room for this pattern.";
				return false;
			}
			Receipt.RosterAfter = after;
			Receipt.Chronicle = "the keepers of " + SettlementName + " learned "
				+ Article(selected.Label) + " from a caravan's pattern-book";
			Receipt.ChronicleState = KingdomTradeSinkState.Pending;
			Receipt.Message = "{{G|The pattern for " + selected.Label + " is learned in "
				+ SettlementName + ".}}";
			Receipt.MessageState = KingdomTradeSinkState.Pending;
			Receipt.State = KingdomTradePatternState.Selected;
			return true;
		}

		public static KingdomTradePatternCasVerdict InspectRoster(
			KingdomTradePatternReceipt Receipt, string CurrentStoredRoster)
		{
			if (Receipt == null || (Receipt.State != KingdomTradePatternState.Selected
				&& Receipt.State != KingdomTradePatternState.RosterIntent)
				|| string.IsNullOrEmpty(Receipt.RosterAfter))
				return KingdomTradePatternCasVerdict.Invalid;
			string canonical;
			List<string> ignored;
			if (!TryCanonicalRoster(CurrentStoredRoster, out canonical, out ignored))
				return KingdomTradePatternCasVerdict.ThirdValue;
			if (string.Equals(canonical, Receipt.RosterAfter, StringComparison.Ordinal))
				return KingdomTradePatternCasVerdict.AlreadyApplied;
			if (string.Equals(canonical, Receipt.RosterBefore, StringComparison.Ordinal))
				return KingdomTradePatternCasVerdict.Apply;
			return KingdomTradePatternCasVerdict.ThirdValue;
		}

		public static bool MarkRosterIntent(KingdomTradePatternReceipt Receipt)
		{
			if (Receipt == null) return false;
			if (Receipt.State == KingdomTradePatternState.RosterIntent) return true;
			if (Receipt.State != KingdomTradePatternState.Selected) return false;
			Receipt.State = KingdomTradePatternState.RosterIntent;
			return true;
		}

		public static bool MarkLearned(KingdomTradePatternReceipt Receipt)
		{
			if (Receipt == null || (Receipt.State != KingdomTradePatternState.Selected
				&& Receipt.State != KingdomTradePatternState.RosterIntent)) return false;
			Receipt.State = KingdomTradePatternState.Learned;
			return true;
		}

		public static void MarkConflict(KingdomTradePatternReceipt Receipt, string Fault)
		{
			if (Receipt == null) return;
			Receipt.State = KingdomTradePatternState.Conflict;
			Receipt.Fault = Bound(Fault);
			Receipt.Chronicle = null;
			Receipt.ChronicleState = KingdomTradeSinkState.Skipped;
			Receipt.Message = null;
			Receipt.MessageState = KingdomTradeSinkState.Skipped;
		}

		/// <summary>Structural reload repair. UI intent is replayable; player messages are not.</summary>
		public static bool Normalize(KingdomTradePatternReceipt Receipt)
		{
			if (Receipt == null) return false;
			if (Receipt.State == KingdomTradePatternState.ChoiceIntent)
				Receipt.State = KingdomTradePatternState.Offered;
			if (Receipt.State == KingdomTradePatternState.Learned
				&& Receipt.MessageState == KingdomTradeSinkState.Intent)
				Receipt.MessageState = KingdomTradeSinkState.Lost;
			return Valid(Receipt);
		}

		public static bool Valid(KingdomTradePatternReceipt Receipt)
		{
			if (Receipt == null || !Enum.IsDefined(typeof(KingdomTradePatternState), Receipt.State)
				|| Receipt.Offers == null || Receipt.Offers.Count > MaxOffers
				|| TooLong(Receipt.RosterBefore, KingdomZoningRules.MaxRosterEncodedChars)
				|| TooLong(Receipt.RosterAfter, KingdomZoningRules.MaxRosterEncodedChars)
				|| TooLong(Receipt.Chronicle, KingdomTradeRules.MaxTextChars)
				|| TooLong(Receipt.Message, KingdomTradeRules.MaxTextChars)
				|| TooLong(Receipt.Fault, KingdomTradeRules.MaxTextChars)
				|| !Enum.IsDefined(typeof(KingdomTradeSinkState), Receipt.ChronicleState)
				|| !Enum.IsDefined(typeof(KingdomTradeSinkState), Receipt.MessageState)) return false;
			for (int i = 0; i < Receipt.Offers.Count; i++)
			{
				if (!ValidDesign(Receipt.Offers[i])) return false;
				for (int j = 0; j < i; j++)
					if (string.Equals(Receipt.Offers[i].BuildingKey,
						Receipt.Offers[j].BuildingKey, StringComparison.Ordinal)
						|| string.Equals(Receipt.Offers[i].LearnName,
							Receipt.Offers[j].LearnName, StringComparison.Ordinal)) return false;
			}
			bool hasOffer = Receipt.Offers.Count > 0;
			switch (Receipt.State)
			{
			case KingdomTradePatternState.None:
			case KingdomTradePatternState.NoCandidates:
			case KingdomTradePatternState.ChanceMiss:
				return !hasOffer && EmptyChoice(Receipt);
			case KingdomTradePatternState.Offered:
			case KingdomTradePatternState.ChoiceIntent:
			case KingdomTradePatternState.Declined:
				return hasOffer && EmptyChoice(Receipt);
			case KingdomTradePatternState.Selected:
			case KingdomTradePatternState.RosterIntent:
				return ValidSelectionCas(Receipt) && PendingSinkShape(Receipt);
			case KingdomTradePatternState.Learned:
				return ValidSelectionCas(Receipt) && ActiveSinkShape(Receipt);
			case KingdomTradePatternState.AlreadyKnown:
				return ValidAlreadyKnown(Receipt) && SkippedSinks(Receipt);
			case KingdomTradePatternState.Conflict:
				return !string.IsNullOrWhiteSpace(Receipt.Fault) && SkippedSinks(Receipt);
			default:
				return false;
			}
		}

		public static bool Terminal(KingdomTradePatternReceipt Receipt)
		{
			if (!Valid(Receipt)) return false;
			switch (Receipt.State)
			{
			case KingdomTradePatternState.None:
			case KingdomTradePatternState.NoCandidates:
			case KingdomTradePatternState.ChanceMiss:
			case KingdomTradePatternState.Declined:
			case KingdomTradePatternState.AlreadyKnown:
			case KingdomTradePatternState.Conflict:
				return true;
			case KingdomTradePatternState.Learned:
				return Receipt.ChronicleState == KingdomTradeSinkState.Delivered
					&& KingdomTradeRules.SinkSettled(Receipt.MessageState);
			default:
				return false;
			}
		}
	}
}
