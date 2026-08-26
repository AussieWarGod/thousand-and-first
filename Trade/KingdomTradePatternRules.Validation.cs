using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomTradePatternRules
	{

		private static KingdomTradePatternReceipt Empty(KingdomTradePatternState State)
		{
			return new KingdomTradePatternReceipt
			{
				State = State,
				Offers = new List<KingdomTradePatternDesign>(),
				SelectedIndex = -1,
				ChronicleState = KingdomTradeSinkState.Skipped,
				MessageState = KingdomTradeSinkState.Skipped
			};
		}

		private static List<KingdomTradePatternDesign> CanonicalCandidates(
			IEnumerable<KingdomTradePatternDesign> Candidates)
		{
			List<KingdomTradePatternDesign> result = new List<KingdomTradePatternDesign>();
			if (Candidates != null)
			{
				foreach (KingdomTradePatternDesign row in Candidates)
				{
					if (!ValidDesign(row)) continue;
					bool duplicate = false;
					for (int i = 0; i < result.Count; i++)
						if (string.Equals(result[i].LearnName, row.LearnName,
							StringComparison.Ordinal)
							|| string.Equals(result[i].BuildingKey, row.BuildingKey,
								StringComparison.Ordinal)) duplicate = true;
					if (!duplicate) result.Add(Copy(row));
				}
			}
			result.Sort(delegate(KingdomTradePatternDesign left,
				KingdomTradePatternDesign right)
			{
				int byName = string.CompareOrdinal(left.LearnName, right.LearnName);
				return byName != 0 ? byName
					: string.CompareOrdinal(left.BuildingKey, right.BuildingKey);
			});
			return result;
		}

		private static KingdomTradePatternDesign Copy(KingdomTradePatternDesign Source)
		{
			return new KingdomTradePatternDesign
			{
				BuildingKey = Source.BuildingKey,
				LearnName = Source.LearnName,
				Label = Source.Label
			};
		}

		private static bool ValidDesign(KingdomTradePatternDesign Design)
		{
			return Design != null && KingdomTradeRules.ValidName(Design.BuildingKey)
				&& KingdomTradeRules.ValidName(Design.LearnName)
				&& KingdomTradeRules.ValidName(Design.Label);
		}

		private static bool EmptyChoice(KingdomTradePatternReceipt Receipt)
		{
			return Receipt.SelectedIndex == -1 && Receipt.RosterBefore == null
				&& Receipt.RosterAfter == null && SkippedSinks(Receipt);
		}

		private static bool ValidSelection(KingdomTradePatternReceipt Receipt)
		{
			return Receipt.SelectedIndex >= 0 && Receipt.SelectedIndex < Receipt.Offers.Count
				&& ValidDesign(Receipt.Offers[Receipt.SelectedIndex])
				&& Receipt.RosterBefore != null && Receipt.RosterAfter != null;
		}

		private static bool ValidSelectionCas(KingdomTradePatternReceipt Receipt)
		{
			if (!ValidSelection(Receipt)
				|| string.Equals(Receipt.RosterBefore, Receipt.RosterAfter,
					StringComparison.Ordinal)) return false;
			string before;
			List<string> rows;
			if (!TryCanonicalRoster(Receipt.RosterBefore, out before, out rows)
				|| !string.Equals(before, Receipt.RosterBefore, StringComparison.Ordinal)) return false;
			string key = KingdomZoningRules.ComposeKey(
				KingdomCeremonyRules.PatternKnowledgeKind,
				Receipt.Offers[Receipt.SelectedIndex].LearnName);
			if (string.IsNullOrEmpty(key) || rows.Contains(key)) return false;
			rows.Add(key);
			string after;
			return KingdomZoningRules.TryEncodeRoster(rows, out after)
				&& string.Equals(after, Receipt.RosterAfter, StringComparison.Ordinal);
		}

		private static bool ValidAlreadyKnown(KingdomTradePatternReceipt Receipt)
		{
			if (!ValidSelection(Receipt)
				|| !string.Equals(Receipt.RosterBefore, Receipt.RosterAfter,
					StringComparison.Ordinal)) return false;
			string canonical;
			List<string> rows;
			if (!TryCanonicalRoster(Receipt.RosterBefore, out canonical, out rows)
				|| !string.Equals(canonical, Receipt.RosterBefore,
					StringComparison.Ordinal)) return false;
			string key = KingdomZoningRules.ComposeKey(
				KingdomCeremonyRules.PatternKnowledgeKind,
				Receipt.Offers[Receipt.SelectedIndex].LearnName);
			return !string.IsNullOrEmpty(key) && rows.Contains(key);
		}

		private static bool PendingSinkShape(KingdomTradePatternReceipt Receipt)
		{
			return !string.IsNullOrWhiteSpace(Receipt.Chronicle)
				&& !string.IsNullOrWhiteSpace(Receipt.Message)
				&& Receipt.ChronicleState == KingdomTradeSinkState.Pending
				&& Receipt.MessageState == KingdomTradeSinkState.Pending;
		}

		private static bool ActiveSinkShape(KingdomTradePatternReceipt Receipt)
		{
			return !string.IsNullOrWhiteSpace(Receipt.Chronicle)
				&& !string.IsNullOrWhiteSpace(Receipt.Message)
				&& (Receipt.ChronicleState == KingdomTradeSinkState.Pending
					|| Receipt.ChronicleState == KingdomTradeSinkState.Intent
					|| Receipt.ChronicleState == KingdomTradeSinkState.Delivered)
				&& (Receipt.MessageState == KingdomTradeSinkState.Pending
					|| Receipt.MessageState == KingdomTradeSinkState.Intent
					|| Receipt.MessageState == KingdomTradeSinkState.Delivered
					|| Receipt.MessageState == KingdomTradeSinkState.Lost);
		}

		private static bool SkippedSinks(KingdomTradePatternReceipt Receipt)
		{
			return Receipt.Chronicle == null && Receipt.Message == null
				&& Receipt.ChronicleState == KingdomTradeSinkState.Skipped
				&& Receipt.MessageState == KingdomTradeSinkState.Skipped;
		}

		private static bool TryCanonicalRoster(string Stored, out string Canonical,
			out List<string> Rows)
		{
			Canonical = null;
			Rows = null;
			if (!KingdomZoningRules.TryDecodeRoster(Stored, out Rows)
				|| !KingdomZoningRules.TryEncodeRoster(Rows, out Canonical)) return false;
			return string.Equals(Stored ?? "", Canonical, StringComparison.Ordinal);
		}

		private static string Article(string Label)
		{
			if (string.IsNullOrEmpty(Label)) return "a pattern";
			char first = char.ToLowerInvariant(Label[0]);
			return ((first == 'a' || first == 'e' || first == 'i' || first == 'o'
				|| first == 'u') ? "an " : "a ") + Label;
		}

		private static string Bound(string Value)
		{
			if (string.IsNullOrWhiteSpace(Value)) return "Pattern-book authority conflicted.";
			return Value.Length <= KingdomTradeRules.MaxTextChars ? Value
				: Value.Substring(0, KingdomTradeRules.MaxTextChars);
		}

		private static bool TooLong(string Value, int Maximum)
		{
			return Value != null && Value.Length > Maximum;
		}
	}
}
