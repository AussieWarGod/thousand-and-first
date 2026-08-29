namespace ThousandAndFirst
{
	public static partial class KingdomPurposePortfolioRules
	{
		/// <summary>The one lawful identity change: when the commissioned second root first
		/// answers its bootstrap commitment, its exact authored stores replace the provisional
		/// legacy destination stores and the whole route is authenticated again.</summary>
		private static bool AdoptsSecondEndpoint(KingdomPurposePairReceipt Before,
			KingdomPurposePairReceipt After)
		{
			return Before != null && After != null
				&& Before.Phase == KingdomPurposePairPhase.SecondPending
				&& After.Phase == KingdomPurposePairPhase.ReturnOutstanding
				&& string.IsNullOrEmpty(Before.SecondWorkId) && Id(After.SecondWorkId)
				&& Before.Operation?.Phase == KingdomPurposeOperationPhase.Delivered
				&& Before.Operation.BootstrapExemption
				&& After.Operation?.Phase == KingdomPurposeOperationPhase.Prepared
				&& After.Operation.ReturnExemption
				&& SameIdentityOutsideSecondEndpoint(Before, After)
				&& (Before.SecondInputStoreId != After.SecondInputStoreId
					|| Before.SecondOutputStoreId != After.SecondOutputStoreId
					|| Before.RouteDigest != After.RouteDigest)
				&& RouteDigestMatches(After);
		}

		private static bool SameIdentityOutsideSecondEndpoint(
			KingdomPurposePairReceipt A, KingdomPurposePairReceipt B)
		{
			return A.PairId == B.PairId && A.RealmId == B.RealmId && A.Epoch == B.Epoch
				&& A.FirstKind == B.FirstKind && A.SecondKind == B.SecondKind
				&& A.FirstSettlementId == B.FirstSettlementId
				&& A.SecondSettlementId == B.SecondSettlementId
				&& A.FirstWorkId == B.FirstWorkId && A.FirstZoneId == B.FirstZoneId
				&& A.SecondZoneId == B.SecondZoneId
				&& A.FirstInputStoreId == B.FirstInputStoreId
				&& A.FirstOutputStoreId == B.FirstOutputStoreId
				&& A.FirstGateKey == B.FirstGateKey && A.SecondGateKey == B.SecondGateKey;
		}
	}
}
