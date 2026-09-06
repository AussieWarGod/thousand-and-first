namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceRungRules
	{
		private static bool ValidRelease(KingdomSubsidenceRungPlan plan, KingdomSubsidenceRungWork work)
		{
			if (work.ReleasePhase == KingdomSubsidenceReleasePhase.Pending)
				return work.ReleaseBefore == null && work.ReleaseAfter == null;
			return (work.ReleasePhase == KingdomSubsidenceReleasePhase.Intent
				|| work.ReleasePhase == KingdomSubsidenceReleasePhase.Released) && WorkComplete(work)
				&& KingdomSubsidenceReleaseRules.ValidProof(plan.StepId, work.BeforeWear, work.AfterWear,
					work.ReleaseBefore, work.ReleaseAfter);
		}

		internal static bool ReleasedComplete(KingdomSubsidenceRungPlan plan)
		{
			if (!PhysicalComplete(plan)) return false;
			foreach (KingdomSubsidenceRungWork work in plan.Works)
				if (work.ReleasePhase != KingdomSubsidenceReleasePhase.Released) return false;
			return true;
		}

		internal static bool TryArmRelease(KingdomSubsidenceRungPlan plan, int index,
			bool exactAuthority, KingdomSubsidenceWearReceipt observed, out KingdomSubsidenceRungPlan next)
		{
			next = null;
			if (!exactAuthority || !ReleaseFrontier(plan, index)) return false;
			KingdomSubsidenceRungWork work = plan.Works[index];
			if (work.ReleasePhase != KingdomSubsidenceReleasePhase.Pending
				|| !KingdomSubsidenceReleaseRules.TryPlan(plan.StepId, work.BeforeWear, work.AfterWear,
					observed, out KingdomSubsidenceWearReceipt target)) return false;
			next = plan.Replace(index, work.WithRelease(KingdomSubsidenceReleasePhase.Intent, observed, target));
			return Valid(next);
		}

		internal static bool TryProveRelease(KingdomSubsidenceRungPlan plan, int index,
			bool exactAuthority, KingdomSubsidenceWearReceipt observed, out KingdomSubsidenceRungPlan next)
		{
			next = null;
			if (!exactAuthority || !ReleaseFrontier(plan, index)) return false;
			KingdomSubsidenceRungWork work = plan.Works[index];
			if (work.ReleasePhase != KingdomSubsidenceReleasePhase.Intent
				|| !KingdomSubsidenceReleaseRules.Same(work.ReleaseAfter, observed)) return false;
			next = plan.Replace(index, work.WithRelease(KingdomSubsidenceReleasePhase.Released,
				work.ReleaseBefore, work.ReleaseAfter));
			return Valid(next);
		}

		private static bool ReleaseFrontier(KingdomSubsidenceRungPlan plan, int index)
		{
			if (!Valid(plan) || index < 0 || index >= plan.Works.Count || !WorkComplete(plan.Works[index]))
				return false;
			for (int i = 0; i < index; i++)
				if (plan.Works[i].ReleasePhase != KingdomSubsidenceReleasePhase.Released) return false;
			return true;
		}
	}
}
