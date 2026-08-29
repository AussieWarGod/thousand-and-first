namespace ThousandAndFirst
{
	public static partial class KingdomPurposePortfolioRules
	{
		/// <summary>Executable admission boundary for a competing ordinary operation. Physical
		/// cleanup is not authority: the previous cargo must be fully retired and its consuming pair
		/// receipt published before another operation can reserve the ordinal.</summary>
		internal static KingdomPurposeLandingTransactionVerdict DriveCompetingOperationAdmission(
			KingdomPurposeLandingTransactionState Before, bool SemanticCasAccepted,
			out KingdomPurposeLandingTransactionState After)
		{
			After = Before;
			if (!ValidFigures(Before) || Before.FaultPresent
				|| Before.PairPhase != KingdomPurposePairPhase.Active
				|| Before.ResumePhase != KingdomPurposePairPhase.Invalid
				|| Before.OperationPhase != KingdomPurposeOperationPhase.Invalid
				|| Before.NextOperationOrdinal == int.MaxValue
				|| !CanStartOperationAtRevision(Before.PairRevision, Before.PairPhase)
				|| !RetiredCargoIsReleased(Before))
				return KingdomPurposeLandingTransactionVerdict.Refused;
			if (!SemanticCasAccepted)
				return KingdomPurposeLandingTransactionVerdict.SemanticCasRefused;
			After.PairPhase = KingdomPurposePairPhase.OperationOutstanding;
			After.OperationPhase = KingdomPurposeOperationPhase.Prepared;
			After.OperationRevision = 0;
			After.NextOperationOrdinal++;
			After.PairRevision++;
			if (!LandingRevisionHeadroomIsValid(After))
			{
				After = Before;
				return KingdomPurposeLandingTransactionVerdict.Refused;
			}
			return KingdomPurposeLandingTransactionVerdict.OperationAdmitted;
		}
	}
}
