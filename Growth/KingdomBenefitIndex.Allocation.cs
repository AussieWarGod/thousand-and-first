using System.Collections.Generic;

namespace ThousandAndFirst
{
	public sealed partial class KingdomBenefitIndex
	{
		private bool AllocatePending(out string Failure)
		{
			Failure = null;
			for (int r = 0; r < Rows.Count; r++)
			{
				KingdomBenefitDesignation designation = Rows[r].Designation;
				if (!ByIdentity.TryGetValue(designation.Identity, out Aggregate aggregate))
					return Fail("benefit allocation lost its exact designation", out Failure);
				List<KingdomBenefitAllocationClaim> claims =
					new List<KingdomBenefitAllocationClaim>();
				for (int i = 0; i < aggregate.Pending.Count; i++)
					claims.Add(aggregate.Pending[i].Claim);
				if (!KingdomBenefitAllocationRules.TryAllocate(designation.Caps,
					designation.AcceptedTags, claims, out _, out Failure)) return false;
				for (int i = 0; i < aggregate.Pending.Count; i++)
					ApplyAllocation(aggregate, aggregate.Pending[i]);
				aggregate.Pending.Clear();
			}
			return true;
		}

		private static void ApplyAllocation(Aggregate Aggregate, ProviderEvaluation Evaluation)
		{
			KingdomBenefitInspection inspection = Evaluation.Inspection;
			KingdomBenefitAllocationClaim claim = Evaluation.Claim;
			inspection.LimitedByDesignation |= claim.Limited;
			inspection.OutsideDesignationContract |= claim.OutsideContract;
			inspection.SaturatedByDesignation |= claim.Saturated;
			bool credited = false;
			for (int i = 0; i < claim.Credited.Count; i++)
			{
				KindAmount row = claim.Credited[i];
				inspection.Credited.Add(row); credited = true;
				Aggregate.Amounts.TryGetValue(row.Kind, out int prior);
				Aggregate.Amounts[row.Kind] = SaturatingAdd(prior, row.Amount);
			}
			for (int i = 0; i < claim.CreditedTags.Count; i++)
			{
				inspection.CreditedTags.Add(claim.CreditedTags[i]);
				CreditTag(Aggregate, claim.CreditedTags[i]); credited = true;
			}
			if (!credited && claim.OutsideContract)
				Fault(inspection, KingdomBenefitFault.UnacceptedBenefit,
					claim.Saturated ? "offer does not fit this building role; accepted supply is also full"
						: "offer does not fit this building role's accepted benefits");
			else if (!credited && claim.Saturated)
				Fault(inspection, KingdomBenefitFault.ProviderCap,
					"accepted supply is already full for this building");
			else if (!credited)
				Fault(inspection, KingdomBenefitFault.Inoperable,
					"current operation produces no whole unit or quality");
			else if (claim.OutsideContract && claim.Saturated)
				inspection.Detail = "some supply does not fit this building role; accepted supply is also full";
			else if (claim.OutsideContract)
				inspection.Detail = "some supply does not fit this building role";
			else if (claim.Saturated)
				inspection.Detail = "some accepted supply is already full for this building";
		}
	}
}
