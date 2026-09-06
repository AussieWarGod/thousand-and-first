using System;

namespace ThousandAndFirst.Harness
{
	internal static class KingdomSubsidenceRungSaveShape
	{
		internal static bool TryMatch(KingdomSubsidenceRungPlan plan, string primaryId,
			string heartId, out KingdomSubsidenceRungWork companion)
		{
			companion = null;
			if (string.IsNullOrEmpty(primaryId) || string.IsNullOrEmpty(heartId)
				|| string.CompareOrdinal(primaryId, heartId) >= 0
				|| !KingdomSubsidenceRungRules.Valid(plan)
				|| plan.From != GrowthStage.City || plan.To != GrowthStage.Town) return false;
			bool selected = KingdomSubsidenceRules.RollRuin(plan.SettlementId, heartId,
				(ulong)plan.DueTick, plan.From);
			if (plan.Works.Count != (selected ? 2 : 1)) return false;
			KingdomSubsidenceRungWork primary = plan.Works[0];
			if (primary.ObjectId != primaryId || !primary.HadWearPart
				|| primary.WearPhase != KingdomSubsidenceEffectPhase.Proved
				|| primary.ReleasePhase != KingdomSubsidenceReleasePhase.Intent
				|| primary.Roofs.Count != 1 || primary.Roofs[0].Phase != KingdomSubsidenceEffectPhase.Proved)
				return false;
			if (!selected) return true;
			KingdomSubsidenceRungWork heart = plan.Works[1];
			if (heart.ObjectId != heartId || heart.HadWearPart || heart.BeforeWear != 0
				|| heart.WearPhase != KingdomSubsidenceEffectPhase.Prepared
				|| heart.ReleasePhase != KingdomSubsidenceReleasePhase.Pending
				|| heart.ReleaseBefore != null || heart.ReleaseAfter != null || heart.Roofs.Count != 0)
				return false;
			companion = heart;
			return true;
		}
	}
}
