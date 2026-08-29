using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomPolityExperienceRuntime
	{
		/// <summary>Authenticates only full lease identities owned by cohorts in current ledger.</summary>
		internal static bool TryBuildRetirementAllowances(KingdomSystem System, long Tick,
			out List<KingdomExperienceRetirementLeaseAllowance> Allowances,
			out string Blocker, out string Failure)
		{
			Allowances = new List<KingdomExperienceRetirementLeaseAllowance>();
			Blocker = null; Failure = null;
			KingdomPolityLedger ledger = System?.PolityLedger;
			KingdomExperienceLedger experience = System?.Experience;
			if (ledger == null || experience == null || Tick < 0L ||
				!KingdomPolityRules.TryValidate(ledger, out Failure)) return false;
			for (int i = 0; i < ledger.Cohorts.Count; i++)
			{
				KingdomPolityCohortPlan cohort = ledger.Cohorts[i];
				KingdomExperienceAudienceReceipt audience = FindAudience(experience,
					cohort.CohortId);
				KingdomExperienceBodyReservation bodies = FindBodies(experience, cohort.CohortId);
				if (audience == null && bodies == null) continue;
				if (!TryCause(ledger, cohort, out long cause, out string causeFailure))
				{
					Blocker = "Polity cohort " + cohort.CohortId +
						" owns W0 capacity but lost its exact cause: " + causeFailure; return true;
				}
				bool ambient = cohort.PresentationOptionKind ==
					KingdomExperienceOptionKind.AmbientUse;
				if (bodies == null || ambient != (audience != null) ||
					!LeaseShape(bodies, System.RealmId, cohort, cause, Tick) ||
					(audience != null && !LeaseShape(audience, System.RealmId, cohort, cause, Tick)))
				{
					Blocker = "Polity cohort " + cohort.CohortId +
						" has a partial, foreign, or mismatched W0 lease at " + cohort.SurfaceRef + ".";
					return true;
				}
				if (audience != null) Allowances.Add(
					new KingdomExperienceRetirementLeaseAllowance { Audience = audience });
				Allowances.Add(new KingdomExperienceRetirementLeaseAllowance { Bodies = bodies });
			}
			return true;
		}
	}
}
