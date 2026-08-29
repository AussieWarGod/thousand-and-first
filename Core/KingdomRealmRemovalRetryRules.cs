using System.Collections.Generic;

namespace ThousandAndFirst
{
	public enum KingdomRemovalOwnerVerdict : byte
	{
		NotApplicable = 0,
		CurrentRealm = 1,
		Ambiguous = 2,
		ForeignOrDivergent = 3,
		ValueBearing = 4
	}

	public enum KingdomRemovalCutProgress : byte
	{
		Quarantine = 0,
		InvokeOrResume = 1,
		Settled = 2
	}

	/// <summary>Engine-free retry rules for a frozen destructive projection preview.</summary>
	public static class KingdomRealmRemovalRetryRules
	{
		public static bool ExactOrRemoved(long FrozenAmount, string FrozenDigest,
			int CurrentCount, string CurrentDigest)
		{
			return CurrentCount == 0 || (FrozenAmount == CurrentCount
				&& FrozenDigest == CurrentDigest);
		}

		public static bool FenceCapacityReserved(int RecordCount)
		{
			return RecordCount >= 0 && RecordCount < KingdomRealmRetirementState.MaxRecords;
		}

		public static bool ExactRemainingSubset(IList<string> Frozen, IList<string> Current)
		{
			if (Frozen == null || Current == null || Current.Count > Frozen.Count) return false;
			HashSet<string> authority = new HashSet<string>(Frozen,
				System.StringComparer.Ordinal);
			if (authority.Count != Frozen.Count) return false;
			HashSet<string> remaining = new HashSet<string>(Current,
				System.StringComparer.Ordinal);
			return remaining.Count == Current.Count && remaining.IsSubsetOf(authority);
		}

		public static KingdomRemovalCutProgress CutProgress(IList<string> Frozen,
			IList<string> Current, bool AttemptPersisted)
		{
			if (!AttemptPersisted || !ExactRemainingSubset(Frozen, Current))
				return KingdomRemovalCutProgress.Quarantine;
			return Current.Count == 0 ? KingdomRemovalCutProgress.Settled
				: KingdomRemovalCutProgress.InvokeOrResume;
		}

		public static bool WorstCaseCapacityReserved(int ExistingRecords,
			int PreviewRows, int CompletionRows, int AuthorityRows)
		{
			if (ExistingRecords < 0 || PreviewRows < 0 || CompletionRows < 0
				|| AuthorityRows < 0) return false;
			long total = (long)ExistingRecords + PreviewRows + CompletionRows + AuthorityRows + 1L;
			return total <= KingdomRealmRetirementState.MaxRecords;
		}

		public static KingdomRemovalOwnerVerdict ClassifyOwnerEvidence(string CurrentRealm,
			IList<string> Evidence, bool Candidate, bool ValueBearing)
		{
			if (!Candidate) return KingdomRemovalOwnerVerdict.NotApplicable;
			if (ValueBearing) return KingdomRemovalOwnerVerdict.ValueBearing;
			if (string.IsNullOrEmpty(CurrentRealm) || Evidence == null || Evidence.Count == 0)
				return KingdomRemovalOwnerVerdict.Ambiguous;
			for (int i = 0; i < Evidence.Count; i++)
				if (Evidence[i] != CurrentRealm)
					return KingdomRemovalOwnerVerdict.ForeignOrDivergent;
			return KingdomRemovalOwnerVerdict.CurrentRealm;
		}

		public static bool GroundMutationAllowed(bool InPlayerCustody,
			KingdomRemovalOwnerVerdict Verdict)
		{
			return !InPlayerCustody && Verdict == KingdomRemovalOwnerVerdict.CurrentRealm;
		}

		/// <summary>Only the frozen full Charter pair, its ordered part-only suffix, or
		/// native absence can be resumed after the fence. The runtime reconstructs the
		/// full-pair digest from the part's retained ability identity.</summary>
		public static bool AuthenticatedPlayerTerminalProgress(long FrozenAmount,
			bool PartPresent, bool AbilityPresent, bool FullPairDigestMatches)
		{
			return FrozenAmount == 2L
				&& ((!PartPresent && !AbilityPresent)
					|| (PartPresent && FullPairDigestMatches));
		}

		public static bool TerminalSystemRemovalSettled(bool RegistryContainsCarrier,
			bool CallbackThrew)
		{
			return !RegistryContainsCarrier;
		}
	}
}
