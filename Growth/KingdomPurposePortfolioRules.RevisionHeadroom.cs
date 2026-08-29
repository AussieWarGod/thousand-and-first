namespace ThousandAndFirst
{
	public static partial class KingdomPurposePortfolioRules
	{
		public const int MaxOrdinaryOperationAdvances = 79;
		public const int MaxExemptOperationAdvances = 77;
		public const int TerminalPairRevisionReserve = 1;
		public const int NormalOperationAdmissionHeadroom = 82;
		public const int ReturnOperationAdmissionHeadroom = 160;
		public const int BootstrapOperationAdmissionHeadroom = 238;

		public static bool CanStartOperationAtRevision(int pairRevision,
			KingdomPurposePairPhase phase)
		{
			int required = phase == KingdomPurposePairPhase.Frozen
				? BootstrapOperationAdmissionHeadroom
				: phase == KingdomPurposePairPhase.SecondPending
					? ReturnOperationAdmissionHeadroom
					: phase == KingdomPurposePairPhase.Active
						|| phase == KingdomPurposePairPhase.CargoAwaitingActivation
							? NormalOperationAdmissionHeadroom : -1;
			return RevisionHasHeadroom(pairRevision, required);
		}

		internal static bool PairRevisionHeadroomIsValid(KingdomPurposePairReceipt pair)
		{
			return pair != null && TryRequiredPairRevisionHeadroom(pair.Phase,
				pair.ResumePhase, pair.Operation?.Revision ?? 0, out int required)
				&& RevisionHasHeadroom(pair.Revision, required);
		}

		internal static bool LandingRevisionHeadroomIsValid(
			KingdomPurposeLandingTransactionState state)
		{
			return TryRequiredPairRevisionHeadroom(state.PairPhase, state.ResumePhase,
				state.OperationRevision, out int required)
				&& RevisionHasHeadroom(state.PairRevision, required);
		}

		internal static bool TryRequiredPairRevisionHeadroom(KingdomPurposePairPhase phase,
			KingdomPurposePairPhase resumePhase, int operationRevision, out int required)
		{
			required = -1;
			switch (phase)
			{
			case KingdomPurposePairPhase.Frozen:
			case KingdomPurposePairPhase.Active:
				required = TerminalPairRevisionReserve;
				return true;
			case KingdomPurposePairPhase.BootstrapOutstanding:
				return Outstanding(MaxExemptOperationAdvances, operationRevision,
					ReturnOperationAdmissionHeadroom, out required);
			case KingdomPurposePairPhase.SecondPending:
				required = ReturnOperationAdmissionHeadroom;
				return true;
			case KingdomPurposePairPhase.ReturnOutstanding:
				return Outstanding(MaxExemptOperationAdvances, operationRevision,
					NormalOperationAdmissionHeadroom, out required);
			case KingdomPurposePairPhase.CargoAwaitingActivation:
				required = NormalOperationAdmissionHeadroom;
				return true;
			case KingdomPurposePairPhase.OperationOutstanding:
				return Outstanding(MaxOrdinaryOperationAdvances, operationRevision, 2,
					out required);
			case KingdomPurposePairPhase.CargoAwaitingConsumption:
				required = 2;
				return true;
			case KingdomPurposePairPhase.Orphaned:
				if (!TryRequiredPairRevisionHeadroom(resumePhase,
					KingdomPurposePairPhase.Invalid, operationRevision, out required)
					|| required == int.MaxValue) return false;
				required++;
				return true;
			case KingdomPurposePairPhase.Dormant:
			case KingdomPurposePairPhase.Quarantined:
				required = 0;
				return true;
			default:
				return false;
			}
		}

		private static bool Outstanding(int maximum, int operationRevision,
			int downstream, out int required)
		{
			required = -1;
			if (operationRevision < 0 || operationRevision > maximum) return false;
			required = maximum - operationRevision + downstream;
			return true;
		}

		private static bool RevisionHasHeadroom(int revision, int required)
		{
			return revision >= 0 && required >= 0
				&& (long)revision + required <= int.MaxValue;
		}
	}
}
