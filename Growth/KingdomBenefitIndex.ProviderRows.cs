using System.Collections.Generic;
using XRL.World;

namespace ThousandAndFirst
{
	public sealed partial class KingdomBenefitIndex
	{
		private sealed class RawProviderCandidate
		{
			internal IKingdomBenefitProvider Provider;
			internal string TypeName;
			internal string CanonicalDescription;
		}

		private sealed class ProviderObjectBatch
		{
			internal GameObject Item;
			internal string ObjectAnchor;
			internal string IdentityPrefix;
			internal bool ExactIdentity;
			internal Zone InitialZone;
			internal GameObject InitialHolder;
			internal int InitialX;
			internal int InitialY;
			internal int InitialCount;
			internal bool InitiallyEquipped;
			internal int ExplicitCount;
			internal bool ProviderOverflow;
			internal int NativeCount;
			internal int NativeAdmitted;
			internal bool Admitted;
			internal readonly List<RawProviderCandidate> Raw =
				new List<RawProviderCandidate>();
			internal readonly List<ProviderCandidate> Candidates =
				new List<ProviderCandidate>();
		}

		private sealed class ProviderCandidate
		{
			internal ProviderObjectBatch Batch;
			internal GameObject Item;
			internal IKingdomBenefitProvider Provider;
			internal bool Native;
			internal KingdomBenefitProviderDeclaration Declaration;
			internal string TypeName;
			internal string StableKey;
			internal string IdentityBase;
			internal bool Matched;
			internal KingdomDesignationMatch Match;
			internal Aggregate AssignedAggregate;
			internal bool CustodyValid;
			internal bool AccessObserved;
			internal bool AccessValid;
			internal bool ShellObserved;
			internal bool ShellValid;
			internal bool OperationObserved;
			internal int OperationPercent;
			internal bool ItemBroken;
			internal bool RootBroken;
			internal int ItemConditionPercent;
			internal int RootConditionPercent;
		}

		private sealed class ProviderEvaluation
		{
			internal KingdomBenefitInspection Inspection;
			internal KingdomBenefitAllocationClaim Claim;
		}
	}
}
