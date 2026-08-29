namespace ThousandAndFirst
{
	public static partial class KingdomPurposePortfolioRules
	{
		private static bool SameStateExceptTerminal(KingdomPurposePairReceipt A,
			KingdomPurposePairReceipt B)
		{
			return SameOperationalState(A, B);
		}

		private static bool SameOperationalState(KingdomPurposePairReceipt A,
			KingdomPurposePairReceipt B)
		{
			return A.SecondWorkId == B.SecondWorkId
				&& A.NextOperationOrdinal == B.NextOperationOrdinal
				&& A.BootstrapUsed == B.BootstrapUsed
				&& A.ReturnUsed == B.ReturnUsed && A.NextKind == B.NextKind
				&& A.CreditCargoId == B.CreditCargoId
				&& A.CreditCargoReceipt == B.CreditCargoReceipt
				&& SameOperationValue(A.Operation, B.Operation);
		}

		private static bool SameOperationValue(KingdomPurposeOperationReceipt A,
			KingdomPurposeOperationReceipt B)
		{
			if (A == null || B == null) return A == null && B == null;
			return EncodeOperation(A) == EncodeOperation(B);
		}

		private static bool AdoptOnce(string Before, string After)
		{
			return string.IsNullOrEmpty(Before) ? true : Before == After;
		}

		private static bool EvidenceAdvanced(KingdomPurposeOperationReceipt A,
			KingdomPurposeOperationReceipt B)
		{
			return A.WaterSpent != B.WaterSpent || A.WaterLost != B.WaterLost
				|| A.FoodSpent != B.FoodSpent || A.FoodLost != B.FoodLost
				|| A.MaterialSpent != B.MaterialSpent || A.MaterialLost != B.MaterialLost
				|| A.EffectStep != B.EffectStep
				|| A.LocalDebitReceipt != B.LocalDebitReceipt
				|| A.OutputCargoId != B.OutputCargoId
				|| A.OutputCargoReceipt != B.OutputCargoReceipt
				|| A.TransportJobId != B.TransportJobId
				|| A.EffectBeforeDigest != B.EffectBeforeDigest
				|| A.EffectAfterDigest != B.EffectAfterDigest
				|| A.InputBeforeDigest != B.InputBeforeDigest
				|| A.InputAfterDigest != B.InputAfterDigest
				|| A.OutputBeforeDigest != B.OutputBeforeDigest
				|| A.OutputAfterDigest != B.OutputAfterDigest;
		}

		private static bool ClaimMonotone(string Before, string After)
		{
			if (!KingdomMaterialDebitCost.TryParseClaim(Before, out var before)
				|| !KingdomMaterialDebitCost.TryParseClaim(After, out var after)) return false;
			for (int i = 0; i < KingdomMaterialRules.MaterialCount; i++)
				if (after.Materials.Get((KingdomMaterial)i)
					< before.Materials.Get((KingdomMaterial)i)) return false;
			for (int i = 0; i < KingdomMaterialRules.BitTierCount; i++)
				if (after.Bits.Get(i) < before.Bits.Get(i)) return false;
			for (int i = 0; i < KingdomMaterialRules.ExoticCount; i++)
				if (after.Exotics.Get((KingdomExotic)i)
					< before.Exotics.Get((KingdomExotic)i)) return false;
			return true;
		}
	}
}
