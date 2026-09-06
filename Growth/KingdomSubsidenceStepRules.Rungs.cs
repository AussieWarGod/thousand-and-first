namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRules
	{
		/// <summary>Freeze all selected works and roof recipients before the first physical effect.
		/// The runtime must prove selection completeness and exact carrier ownership separately.</summary>
		internal static bool TryFreezeRungPlan(KingdomSubsidenceStepBook prior,
			KingdomSubsidenceRungPlan plan, out KingdomSubsidenceStepBook next)
		{
			next = null;
			if (!Open(prior) || prior.Active.Phase != KingdomSubsidenceStepPhase.Settling
				|| !KingdomSubsidenceRungRules.Matches(plan, prior)) return false;
			foreach (KingdomSubsidenceRungWork work in plan.Works)
			{
				if (work.WearPhase != KingdomSubsidenceEffectPhase.Prepared) return false;
				foreach (KingdomSubsidenceRungRoof roof in work.Roofs)
					if (roof.Phase != KingdomSubsidenceEffectPhase.Prepared) return false;
			}
			if (!KingdomSubsidenceRungCodec.TryEncode(plan, out string wire)) return false;
			if (prior.Active.RungModel == wire) { next = prior; return true; }
			if (prior.Active.RungModel != UnplannedRungs) return false;
			return With(prior, prior.Active.Copy(rungModel: wire), out next);
		}

		internal static bool TryReadRungPlan(KingdomSubsidenceStepBook prior,
			out KingdomSubsidenceRungPlan plan)
		{
			plan = null;
			return Open(prior) && prior.Active.Phase == KingdomSubsidenceStepPhase.Settling
				&& KingdomSubsidenceRungCodec.TryDecode(prior.Active.RungModel, out plan)
				&& KingdomSubsidenceRungRules.Matches(plan, prior);
		}

		internal static bool TryArmRungWear(KingdomSubsidenceStepBook prior, int index,
			out KingdomSubsidenceStepBook next)
		{
			next = null;
			return TryReadRungPlan(prior, out KingdomSubsidenceRungPlan plan)
				&& KingdomSubsidenceRungRules.TryArmWear(plan, index, out KingdomSubsidenceRungPlan changed)
				&& StoreRung(prior, changed, out next);
		}

		internal static bool TryProveRungWear(KingdomSubsidenceStepBook prior, int index,
			bool exactAuthority, bool hasPart, int wear, out KingdomSubsidenceStepBook next)
		{
			next = null;
			return TryReadRungPlan(prior, out KingdomSubsidenceRungPlan plan)
				&& KingdomSubsidenceRungRules.TryProveWear(plan, index, exactAuthority, hasPart,
					wear, out KingdomSubsidenceRungPlan changed) && StoreRung(prior, changed, out next);
		}

		internal static bool TryArmRungRoof(KingdomSubsidenceStepBook prior, int work, int roof,
			out KingdomSubsidenceStepBook next)
		{
			next = null;
			return TryReadRungPlan(prior, out KingdomSubsidenceRungPlan plan)
				&& KingdomSubsidenceRungRules.TryArmRoof(plan, work, roof, out KingdomSubsidenceRungPlan changed)
				&& StoreRung(prior, changed, out next);
		}

		internal static bool TryProveRungRoof(KingdomSubsidenceStepBook prior, int work, int roof,
			bool exactAuthority, bool stands, long reached, long warned, out KingdomSubsidenceStepBook next)
		{
			next = null;
			return TryReadRungPlan(prior, out KingdomSubsidenceRungPlan plan)
				&& KingdomSubsidenceRungRules.TryProveRoof(plan, work, roof, exactAuthority,
					stands, reached, warned, out KingdomSubsidenceRungPlan changed)
				&& StoreRung(prior, changed, out next);
		}

		internal static bool TryArmRungRelease(KingdomSubsidenceStepBook prior, int index,
			bool exactAuthority, KingdomSubsidenceWearReceipt observed, out KingdomSubsidenceStepBook next)
		{
			next = null;
			return TryReadRungPlan(prior, out KingdomSubsidenceRungPlan plan)
				&& KingdomSubsidenceRungRules.TryArmRelease(plan, index, exactAuthority, observed,
					out KingdomSubsidenceRungPlan changed) && StoreRung(prior, changed, out next);
		}

		internal static bool TryProveRungRelease(KingdomSubsidenceStepBook prior, int index,
			bool exactAuthority, KingdomSubsidenceWearReceipt observed, out KingdomSubsidenceStepBook next)
		{
			next = null;
			return TryReadRungPlan(prior, out KingdomSubsidenceRungPlan plan)
				&& KingdomSubsidenceRungRules.TryProveRelease(plan, index, exactAuthority, observed,
					out KingdomSubsidenceRungPlan changed) && StoreRung(prior, changed, out next);
		}

		private static bool StoreRung(KingdomSubsidenceStepBook prior, KingdomSubsidenceRungPlan plan,
			out KingdomSubsidenceStepBook next)
		{
			next = null;
			return KingdomSubsidenceRungRules.Matches(plan, prior)
				&& KingdomSubsidenceRungCodec.TryEncode(plan, out string wire)
				&& With(prior, prior.Active.Copy(rungModel: wire), out next);
		}
	}
}
