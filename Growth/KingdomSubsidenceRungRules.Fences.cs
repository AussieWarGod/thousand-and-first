namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceRungRules
	{
		internal static bool BlocksProjection(string stepWire)
		{
			return !TryFencePlan(stepWire, out KingdomSubsidenceRungPlan plan)
				|| plan != null && !ReleasedComplete(plan);
		}

		internal static bool AdmitsRoofWrite(string stepWire, int residentId, int homeWorkId,
			string zoneId, int standing, bool beforeStanding,
			long beforeReached, long beforeWarned, bool stands, long reached, long warned)
		{
			if (!TryFencePlan(stepWire, out KingdomSubsidenceRungPlan plan, true) || plan == null) return false;
			for (int work = 0; work < plan.Works.Count; work++)
				for (int index = 0; index < plan.Works[work].Roofs.Count; index++)
				{
					KingdomSubsidenceRungRoof roof = plan.Works[work].Roofs[index];
					if (roof.ResidentId != residentId) continue;
					return homeWorkId == plan.Works[work].WorkId && zoneId == plan.ZoneId
						&& standing == (int)Simulation.City.KingdomResidentStanding.Resident
						&& RoofAction(plan, work, index, true, beforeStanding, beforeReached, beforeWarned)
						!= KingdomSubsidenceEffectAction.Refuse && stands
						&& reached == (roof.BeforeStanding ? roof.BeforeReached : plan.DueTick)
						&& warned == (roof.BeforeStanding ? roof.BeforeWarned : KingdomBrinkRules.Unwarned);
				}
			return false;
		}

		internal static bool BlocksWork(string stepWire, string objectId)
		{
			if (!TryFencePlan(stepWire, out KingdomSubsidenceRungPlan plan)) return true;
			if (plan == null) return false;
			if (string.IsNullOrEmpty(objectId)) return true;
			foreach (KingdomSubsidenceRungWork work in plan.Works)
				if (work.ObjectId == objectId) return work.ReleasePhase != KingdomSubsidenceReleasePhase.Released;
			return false;
		}

		internal static bool BlocksRoof(string stepWire, int residentId)
		{
			if (!TryFencePlan(stepWire, out KingdomSubsidenceRungPlan plan)) return true;
			if (plan == null) return false;
			if (residentId <= 0) return true;
			foreach (KingdomSubsidenceRungWork work in plan.Works)
				foreach (KingdomSubsidenceRungRoof roof in work.Roofs)
					if (roof.ResidentId == residentId)
						return roof.Phase != KingdomSubsidenceEffectPhase.Proved;
			return false;
		}

		private static bool TryFencePlan(string wire, out KingdomSubsidenceRungPlan plan, bool mustSettle = false)
		{
			plan = null;
			if (!KingdomSubsidenceStepCodec.TryDecode(wire, out KingdomSubsidenceStepBook book)) return false;
			KingdomSubsidenceStepOperation active = book.Active;
			if (mustSettle && active?.Phase != KingdomSubsidenceStepPhase.Settling) return false;
			if (active == null || active.RungModel == KingdomSubsidenceStepRules.NoRungs
				|| active.RungModel == KingdomSubsidenceStepRules.UnplannedRungs) return true;
			return KingdomSubsidenceRungCodec.TryDecode(active.RungModel, out plan) && Matches(plan, book);
		}
	}
}
