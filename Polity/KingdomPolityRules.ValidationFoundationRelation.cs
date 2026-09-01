namespace ThousandAndFirst
{
	public static partial class KingdomPolityRules
	{
		private static bool ValidRelationProvenance(KingdomPolityRelation R)
		{
			if (R.FoundationState == KingdomPolityFoundationRelationState.Ordinary)
				return R.InitialBand == KingdomPolityRelationBand.Unspecified &&
					string.IsNullOrEmpty(R.FoundationOriginalCauseRef) &&
					string.IsNullOrEmpty(R.FoundationCorrectionReceiptId);
			if (R.FoundationState != KingdomPolityFoundationRelationState.LegacyUnresolved &&
				R.FoundationState != KingdomPolityFoundationRelationState.Causal ||
				R.InitialBand == KingdomPolityRelationBand.Unspecified ||
				!Defined((byte)R.InitialBand, 6) || !SemanticId(R.FoundationOriginalCauseRef))
				return false;
			if (R.FoundationState == KingdomPolityFoundationRelationState.LegacyUnresolved)
				return string.IsNullOrEmpty(R.FoundationCorrectionReceiptId);
			if (!string.IsNullOrEmpty(R.FoundationCorrectionReceiptId) &&
				!TypedId(R.FoundationCorrectionReceiptId,
					"taf:receipt:foundation-relation-correction:v1:")) return false;
			if (R.InitialBand == KingdomPolityRelationBand.Contact)
				return R.FoundationOriginalCauseRef.StartsWith(
					"taf:fact:legacy-contact:v2:", System.StringComparison.Ordinal) &&
					string.IsNullOrEmpty(R.FoundationCorrectionReceiptId) &&
					Contains(R.SourceRefs, R.FoundationOriginalCauseRef);
			return !string.IsNullOrEmpty(R.FoundationCorrectionReceiptId) &&
				Contains(R.SourceRefs, R.FoundationCorrectionReceiptId);
		}
	}
}
