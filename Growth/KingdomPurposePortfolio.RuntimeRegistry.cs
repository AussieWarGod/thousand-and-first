using System;
using XRL;

namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		internal const string PortfolioStateKey = "r_TAF_PurposePortfolioPair";
		internal const string PortfolioPairProperty = "r_TAF_PurposePairId";

		internal static bool TryReadPortfolioPair(out KingdomPurposePairReceipt Pair,
			out string Failure)
		{
			Pair = null;
			Failure = null;
			if (The.Game == null)
				return Fail("No live game can answer the purpose-pair register.", out Failure);
			string encoded = The.Game.GetStringGameState(PortfolioStateKey, "");
			if (string.IsNullOrEmpty(encoded)) return true;
			if (!KingdomPurposePortfolioRules.TryDecodePairAny(encoded, out Pair,
				out _))
				return Fail("The purpose-pair register is malformed. It is quarantined from mutation until inspected.", out Failure);
			return true;
		}

		private static bool TryPublishPortfolioPair(KingdomPurposePairReceipt Before,
			KingdomPurposePairReceipt After, out string Failure)
		{
			Failure = null;
			if (The.Game == null || After == null)
				return Fail("No live game can publish the purpose-pair receipt.", out Failure);
			string current = The.Game.GetStringGameState(PortfolioStateKey, "");
			string expected = Before == null ? "" : Before.LegacyWire
				? KingdomPurposePortfolioRules.EncodeLegacyPair(Before)
				: KingdomPurposePortfolioRules.EncodePair(Before);
			string next = KingdomPurposePortfolioRules.EncodePair(After);
			if (expected == null || next == null || current != expected)
				return Fail("The purpose-pair register changed after preview. Review it again; nothing was mutated.", out Failure);
			if (Before == null)
			{
				if (After.Phase != KingdomPurposePairPhase.Frozen || After.Revision != 0)
					return Fail("A new purpose pair must begin at its frozen zero-revision receipt.", out Failure);
			}
			else if (!KingdomPurposePortfolioRules.ValidTransition(Before, After, out _))
				return Fail("The proposed purpose-pair transition is not lawful.", out Failure);
			The.Game.SetStringGameState(PortfolioStateKey, next);
			if (The.Game.GetStringGameState(PortfolioStateKey, "") != next)
				return Fail("The purpose-pair receipt did not persist exactly.", out Failure);
			After.LegacyWire = false;
			return true;
		}

		private static bool TryReplaceDormantPair(KingdomPurposePairReceipt Dormant,
			KingdomPurposePairReceipt Fresh, out string Failure)
		{
			Failure = null;
			if (Dormant == null || Dormant.Phase != KingdomPurposePairPhase.Dormant
				|| Dormant.Epoch == long.MaxValue
				|| Fresh == null || Fresh.Phase != KingdomPurposePairPhase.Frozen
				|| Fresh.Epoch != Dormant.Epoch + 1L || Fresh.Revision != 0)
				return Fail("Only a quiescent dissolved pair may mint the next epoch.", out Failure);
			string current = The.Game?.GetStringGameState(PortfolioStateKey, "");
			string expected = Dormant.LegacyWire
				? KingdomPurposePortfolioRules.EncodeLegacyPair(Dormant)
				: KingdomPurposePortfolioRules.EncodePair(Dormant);
			string next = KingdomPurposePortfolioRules.EncodePair(Fresh);
			if (The.Game == null || current != expected || next == null)
				return Fail("The dissolved pair changed before re-pairing.", out Failure);
			The.Game.SetStringGameState(PortfolioStateKey, next);
			if (The.Game.GetStringGameState(PortfolioStateKey, "") != next)
				return Fail("The new purpose-pair epoch did not persist exactly.", out Failure);
			Fresh.LegacyWire = false;
			return true;
		}
	}
}
