using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Read-only deterministic allocation of one composite price.</summary>
	public sealed class KingdomMaterialDebitPlan
	{
		public readonly KingdomMaterialDebitCost Requested;
		public readonly List<KingdomMaterialDebitStep> Steps;

		public KingdomMaterialDebitPlan(KingdomMaterialDebitCost Requested,
			List<KingdomMaterialDebitStep> Steps)
		{
			this.Requested = (Requested == null) ? new KingdomMaterialDebitCost() : Requested.Copy();
			this.Steps = (Steps == null)
				? new List<KingdomMaterialDebitStep>()
				: new List<KingdomMaterialDebitStep>(Steps);
		}
	}
}
