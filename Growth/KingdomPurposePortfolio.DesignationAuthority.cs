namespace ThousandAndFirst
{
	public static partial class KingdomPurpose
	{
		/// <summary>Purpose endpoints remain exact until their pair is explicitly dissolved.
		/// Releasing a named store would turn a current receipt into foreign authority.</summary>
		internal static bool TryCanReleasePurposeStore(string objectId, out string failure)
		{
			failure = null;
			if (string.IsNullOrEmpty(objectId))
				return Fail("The store has no exact identity to release.", out failure);
			KingdomPurposePairReceipt pair;
			if (!TryReadPortfolioPair(out pair, out failure)) return false;
			if (pair == null || pair.Phase == KingdomPurposePairPhase.Dormant) return true;
			if (!NamesStore(pair, objectId)) return true;
			return Fail("A reciprocal purpose receipt still names this store. Dissolve or finish "
				+ "that purpose link before releasing its designation.", out failure);
		}

		private static bool NamesStore(KingdomPurposePairReceipt pair, string objectId)
		{
			if (pair.FirstInputStoreId == objectId || pair.FirstOutputStoreId == objectId
				|| pair.SecondInputStoreId == objectId || pair.SecondOutputStoreId == objectId)
				return true;
			KingdomPurposeOperationReceipt operation = pair.Operation;
			return operation != null && (operation.SourceInputStoreId == objectId
				|| operation.SourceOutputStoreId == objectId
				|| operation.DestinationInputStoreId == objectId);
		}
	}
}
