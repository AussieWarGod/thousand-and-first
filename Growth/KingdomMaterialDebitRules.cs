using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Pure planning, accounting and phase laws for the live material receipt.</summary>
	public static partial class KingdomMaterialDebitRules
	{
		public static bool TryPlan(KingdomMaterialDebitCost Cost,
			IList<KingdomMaterialDebitSource> Sources,
			out KingdomMaterialDebitPlan Plan,
			out KingdomMaterialDebitFault Fault)
		{
			Plan = null;
			Fault = KingdomMaterialDebitFault.None;
			if (Cost == null)
			{
				Fault = KingdomMaterialDebitFault.InvalidCost;
				return false;
			}
			if (Sources == null)
			{
				Fault = KingdomMaterialDebitFault.InvalidSources;
				return false;
			}

			List<KingdomMaterialDebitSource> unique = UniqueValidSources(Sources);
			List<KingdomMaterialDebitStep> steps = new List<KingdomMaterialDebitStep>();
			if (!PlanMaterials(Cost.Materials, unique, steps))
			{
				Fault = KingdomMaterialDebitFault.InsufficientMaterials;
				return false;
			}
			if (!PlanExotics(Cost.Exotics, unique, steps))
			{
				Fault = KingdomMaterialDebitFault.InsufficientExotics;
				return false;
			}
			if (!PlanBits(Cost.Bits, unique, steps))
			{
				Fault = KingdomMaterialDebitFault.InsufficientBits;
				return false;
			}
			Plan = new KingdomMaterialDebitPlan(Cost, steps);
			return true;
		}
	}
}
