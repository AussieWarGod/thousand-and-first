using System.Collections.Generic;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceRungRules
	{
		/// <summary>Persist the returned intent before any physical part/field mutation.</summary>
		internal static bool TryArmWear(KingdomSubsidenceRungPlan prior, int index,
			out KingdomSubsidenceRungPlan next)
		{
			next = null;
			if (!AtFrontier(prior, index)) return false;
			KingdomSubsidenceRungWork work = prior.Works[index];
			if (work.WearPhase != KingdomSubsidenceEffectPhase.Prepared) { next = prior; return true; }
			next = prior.Replace(index, work.With(KingdomSubsidenceEffectPhase.Intent, work.Roofs));
			return true;
		}

		/// <summary>ExactAuthority includes the owned work and wear-part custody/fence proof.
		/// Matching wear alone is never that proof; a runtime adapter must establish it separately.</summary>
		internal static KingdomSubsidenceEffectAction WearAction(KingdomSubsidenceRungPlan plan,
			int index, bool exactAuthority, bool hasPart, int observedWear)
		{
			if (!exactAuthority || !AtFrontier(plan, index)) return KingdomSubsidenceEffectAction.Refuse;
			KingdomSubsidenceRungWork work = plan.Works[index];
			if (work.WearPhase == KingdomSubsidenceEffectPhase.Prepared) return KingdomSubsidenceEffectAction.Refuse;
			if (hasPart && observedWear == work.AfterWear) return KingdomSubsidenceEffectAction.Confirm;
			if (work.WearPhase != KingdomSubsidenceEffectPhase.Intent || observedWear != work.BeforeWear
				|| !hasPart && (work.HadWearPart || observedWear != 0)) return KingdomSubsidenceEffectAction.Refuse;
			return KingdomSubsidenceEffectAction.Apply;
		}

		internal static bool TryProveWear(KingdomSubsidenceRungPlan prior, int index,
			bool exactAuthority, bool hasPart, int observedWear, out KingdomSubsidenceRungPlan next)
		{
			next = null;
			if (WearAction(prior, index, exactAuthority, hasPart, observedWear)
				!= KingdomSubsidenceEffectAction.Confirm) return false;
			KingdomSubsidenceRungWork work = prior.Works[index];
			next = prior.Replace(index, work.With(KingdomSubsidenceEffectPhase.Proved, work.Roofs));
			return true;
		}

		internal static bool TryArmRoof(KingdomSubsidenceRungPlan prior, int workIndex,
			int roofIndex, out KingdomSubsidenceRungPlan next)
		{
			next = null;
			if (!RoofFrontier(prior, workIndex, roofIndex)) return false;
			KingdomSubsidenceRungRoof roof = prior.Works[workIndex].Roofs[roofIndex];
			if (roof.Phase != KingdomSubsidenceEffectPhase.Prepared) { next = prior; return true; }
			next = ReplaceRoof(prior, workIndex, roofIndex, KingdomSubsidenceEffectPhase.Intent);
			return true;
		}

		/// <summary>The exact roof before/after tuple includes standing, crossing and warning ticks.
		/// An already-standing roof retains its earlier tuple; it is never redated or unwarned.</summary>
		internal static KingdomSubsidenceEffectAction RoofAction(KingdomSubsidenceRungPlan plan,
			int workIndex, int roofIndex, bool exactAuthority, bool stands, long reached, long warned)
		{
			if (!exactAuthority || !RoofFrontier(plan, workIndex, roofIndex)) return KingdomSubsidenceEffectAction.Refuse;
			KingdomSubsidenceRungRoof roof = plan.Works[workIndex].Roofs[roofIndex];
			if (roof.Phase == KingdomSubsidenceEffectPhase.Prepared) return KingdomSubsidenceEffectAction.Refuse;
			long targetReached = roof.BeforeStanding ? roof.BeforeReached : plan.DueTick;
			long targetWarned = roof.BeforeStanding ? roof.BeforeWarned : KingdomBrinkRules.Unwarned;
			if (stands && reached == targetReached && warned == targetWarned) return KingdomSubsidenceEffectAction.Confirm;
			if (roof.Phase == KingdomSubsidenceEffectPhase.Intent && stands == roof.BeforeStanding
				&& reached == roof.BeforeReached && warned == roof.BeforeWarned) return KingdomSubsidenceEffectAction.Apply;
			return KingdomSubsidenceEffectAction.Refuse;
		}

		internal static bool TryProveRoof(KingdomSubsidenceRungPlan prior, int workIndex, int roofIndex,
			bool exactAuthority, bool stands, long reached, long warned, out KingdomSubsidenceRungPlan next)
		{
			next = null;
			if (RoofAction(prior, workIndex, roofIndex, exactAuthority, stands, reached, warned)
				!= KingdomSubsidenceEffectAction.Confirm) return false;
			next = ReplaceRoof(prior, workIndex, roofIndex, KingdomSubsidenceEffectPhase.Proved);
			return true;
		}

		private static KingdomSubsidenceRungPlan ReplaceRoof(KingdomSubsidenceRungPlan prior,
			int workIndex, int roofIndex, KingdomSubsidenceEffectPhase phase)
		{
			KingdomSubsidenceRungWork work = prior.Works[workIndex];
			List<KingdomSubsidenceRungRoof> roofs = new List<KingdomSubsidenceRungRoof>(work.Roofs);
			roofs[roofIndex] = roofs[roofIndex].With(phase);
			return prior.Replace(workIndex, work.With(work.WearPhase, roofs));
		}

		private static bool AtFrontier(KingdomSubsidenceRungPlan plan, int index)
		{
			if (!Valid(plan) || index < 0 || index >= plan.Works.Count) return false;
			for (int i = 0; i < index; i++) if (!WorkComplete(plan.Works[i])) return false;
			return true;
		}

		private static bool RoofFrontier(KingdomSubsidenceRungPlan plan, int workIndex, int roofIndex)
		{
			if (!AtFrontier(plan, workIndex)) return false;
			KingdomSubsidenceRungWork work = plan.Works[workIndex];
			if (work.WearPhase != KingdomSubsidenceEffectPhase.Proved
				|| roofIndex < 0 || roofIndex >= work.Roofs.Count) return false;
			for (int i = 0; i < roofIndex; i++)
				if (work.Roofs[i].Phase != KingdomSubsidenceEffectPhase.Proved) return false;
			return true;
		}
	}
}
