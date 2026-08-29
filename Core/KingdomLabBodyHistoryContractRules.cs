namespace ThousandAndFirst
{
	/// <summary>Engine-free save migration and terminal-delivery law for D5 lab jobs.</summary>
	public static class KingdomLabBodyHistoryContractRules
	{
		public static bool TryResolveLoaded(int ContractVersion, int StoredPhase,
			bool ExactRulerLife, out int ResolvedVersion,
			out KingdomLabBodyHistoryPhase ResolvedPhase)
		{
			ResolvedVersion = ContractVersion;
			ResolvedPhase = (KingdomLabBodyHistoryPhase)StoredPhase;
			if (ContractVersion == 0
				&& ResolvedPhase == KingdomLabBodyHistoryPhase.LegacyPhysicalOnly)
			{
				if (!ExactRulerLife) return true;
				ResolvedVersion = KingdomBodyHistoryRules.LabContractVersion;
				ResolvedPhase = KingdomLabBodyHistoryPhase.Pending;
				return true;
			}
			return ContractVersion == KingdomBodyHistoryRules.LabContractVersion
				&& ResolvedPhase != KingdomLabBodyHistoryPhase.LegacyPhysicalOnly
				&& System.Enum.IsDefined(typeof(KingdomLabBodyHistoryPhase), ResolvedPhase)
				&& ExactRulerLife;
		}

		internal static KingdomLabBodyHistoryPhase AfterFailure(
			KingdomBodyHistoryPreparationBlock Block)
		{
			return Block == KingdomBodyHistoryPreparationBlock.Capacity
				|| Block == KingdomBodyHistoryPreparationBlock.OpaqueFuture
				|| Block == KingdomBodyHistoryPreparationBlock.Quarantined
				? KingdomLabBodyHistoryPhase.OmittedPreservingMemory
				: KingdomLabBodyHistoryPhase.Pending;
		}

		public static bool AllowsPhysicalCleanup(KingdomLabBodyHistoryPhase Phase)
		{
			return Phase == KingdomLabBodyHistoryPhase.LegacyPhysicalOnly
				|| Phase == KingdomLabBodyHistoryPhase.Applied
				|| Phase == KingdomLabBodyHistoryPhase.OmittedPreservingMemory;
		}
	}
}
