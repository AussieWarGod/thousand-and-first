using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>One fully evaluated physical offer. Runtime custody and operation are already
	/// proved; this engine-free row decides only deterministic cap attribution.</summary>
	internal sealed class KingdomBenefitAllocationClaim
	{
		internal string StableKey;
		internal string DesignationIdentity;
		internal List<KindAmount> ActiveAmounts = new List<KindAmount>();
		internal List<string> ActiveTags = new List<string>();
		internal List<KindAmount> Credited = new List<KindAmount>();
		internal List<string> CreditedTags = new List<string>();
		internal bool Limited;
		internal bool OutsideContract;
		internal bool Saturated;
		internal string OrderKey;
	}

	internal sealed class KingdomBenefitInspectionOrderRow
	{
		internal KingdomBenefitInspection Inspection;
		internal string IdentityBase;
		internal string StableAnchor;
		internal string OrderKey;
	}
}
