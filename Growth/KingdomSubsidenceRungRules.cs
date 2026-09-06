using System;
using System.Collections.Generic;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceRungRules
	{
		// Decoder safety bounds refuse the whole plan; they never truncate the selected works.
		internal const int MaxWorks = 4096;
		internal const int MaxRoofs = KingdomRules.MaxPopulation;

		internal static bool Valid(KingdomSubsidenceRungPlan plan)
		{
			if (plan == null || !KingdomSubsidenceStepRules.IsStepId(plan.StepId)
				|| !KingdomIdentityRules.IsRealmId(plan.RealmId)
				|| !KingdomIdentityRules.IsSettlementId(plan.SettlementId)
				|| !Text(plan.ZoneId, 512, false) || plan.From <= GrowthStage.Camp
				|| plan.From > GrowthStage.City || plan.To != plan.From - 1
				|| plan.DueTick < KingdomSubsidenceStepRules.StepTicks || plan.PreparedTick < plan.DueTick
				|| plan.Departed < 1 || plan.Departed > KingdomSubsidenceRules.SettlersPerStep(plan.From)
				|| plan.Works == null || plan.Works.Count > MaxWorks) return false;
			HashSet<int> residents = new HashSet<int>();
			HashSet<string> bodies = new HashSet<string>(StringComparer.Ordinal);
			string previous = null;
			bool unfinished = false;
			bool unreleased = false;
			foreach (KingdomSubsidenceRungWork work in plan.Works)
			{
				if (!ValidWork(plan, work) || previous != null
					&& string.CompareOrdinal(previous, work.ObjectId) >= 0) return false;
				if (unfinished && (work.WearPhase != KingdomSubsidenceEffectPhase.Prepared
					|| !AllRoofsPrepared(work))) return false;
				previous = work.ObjectId;
				int lastResident = 0;
				bool pending = false;
				foreach (KingdomSubsidenceRungRoof roof in work.Roofs)
				{
					if (roof == null || roof.ResidentId <= lastResident
						|| !residents.Add(roof.ResidentId) || !bodies.Add(roof.BodyObjectId)
						|| !Text(roof.BodyObjectId, 512, false) || !Phase(roof.Phase)
						|| roof.BeforeReached < 0 || roof.BeforeWarned < 0
						|| !roof.BeforeStanding && (roof.BeforeReached != 0 || roof.BeforeWarned != 0)
						|| roof.BeforeReached > plan.PreparedTick || roof.BeforeWarned > plan.PreparedTick
						|| work.WearPhase != KingdomSubsidenceEffectPhase.Proved
							&& roof.Phase != KingdomSubsidenceEffectPhase.Prepared
						|| pending && roof.Phase != KingdomSubsidenceEffectPhase.Prepared) return false;
					lastResident = roof.ResidentId;
					pending |= roof.Phase != KingdomSubsidenceEffectPhase.Proved;
				}
				if (residents.Count > MaxRoofs || !ValidRelease(plan, work)
					|| unreleased && work.ReleasePhase != KingdomSubsidenceReleasePhase.Pending) return false;
				unreleased |= work.ReleasePhase != KingdomSubsidenceReleasePhase.Released;
				unfinished |= !WorkComplete(work);
			}
			return true;
		}

		private static bool ValidWork(KingdomSubsidenceRungPlan plan, KingdomSubsidenceRungWork work)
		{
			if (work == null || !Text(work.ObjectId, 512, false)
				|| work.WorkId != KingdomCityRules.StableId(work.ObjectId)
				|| !Text(work.Blueprint, 512, false) || !Text(work.PlotId, 512, true)
				|| work.DesignStamp != null && !Text(work.DesignStamp, 32768, false)
				|| !Text(work.Name, 512, false) || work.X < 0 || work.X >= 80 || work.Y < 0 || work.Y >= 25
				|| work.BeforeWear < 0 || work.BeforeWear > KingdomMaterialRules.MaxWearPercent
				|| !work.HadWearPart && work.BeforeWear != 0 || !Phase(work.WearPhase)
				|| work.Roofs == null || work.Roofs.Count > MaxRoofs
				|| !KingdomSubsidenceRules.RollRuin(plan.SettlementId, work.ObjectId, (ulong)plan.DueTick, plan.From)
				|| work.AfterWear != KingdomMaterialRules.AddWear(work.BeforeWear,
					KingdomSubsidenceRules.RolledRuinIncrement(plan.SettlementId, work.ObjectId, (ulong)plan.DueTick)))
				return false;
			bool crossing = !KingdomLodgingRules.IsCondemned(work.BeforeWear)
				&& KingdomLodgingRules.IsCondemned(work.AfterWear);
			return work.Roofs.Count == 0 || crossing && work.PlotId.Length != 0;
		}

		internal static bool PhysicalComplete(KingdomSubsidenceRungPlan plan)
		{
			if (!Valid(plan)) return false;
			foreach (KingdomSubsidenceRungWork work in plan.Works) if (!WorkComplete(work)) return false;
			return true;
		}

		internal static bool Matches(KingdomSubsidenceRungPlan plan, KingdomSubsidenceStepBook book)
		{
			KingdomSubsidenceStepOperation op = book?.Active;
			return Valid(plan) && op != null && plan.StepId == op.Id && plan.RealmId == book.RealmId
				&& plan.SettlementId == book.SettlementId && plan.From == op.FromStage
				&& plan.To == op.ReachedStage && plan.DueTick == op.DueTick && plan.Departed == op.Completed
				&& plan.PreparedTick >= op.LastActivityTick
				&& (!op.CancelRequested || plan.PreparedTick >= op.CancelTick)
				&& op.PendingDepartureId == "" && (op.Completed == op.Quota || op.CancelRequested);
		}

		private static bool WorkComplete(KingdomSubsidenceRungWork work)
		{
			if (work.WearPhase != KingdomSubsidenceEffectPhase.Proved) return false;
			foreach (KingdomSubsidenceRungRoof roof in work.Roofs)
				if (roof.Phase != KingdomSubsidenceEffectPhase.Proved) return false;
			return true;
		}

		private static bool AllRoofsPrepared(KingdomSubsidenceRungWork work)
		{
			foreach (KingdomSubsidenceRungRoof roof in work.Roofs)
				if (roof == null || roof.Phase != KingdomSubsidenceEffectPhase.Prepared) return false;
			return true;
		}

		private static bool Phase(KingdomSubsidenceEffectPhase phase)
			=> phase >= KingdomSubsidenceEffectPhase.Prepared && phase <= KingdomSubsidenceEffectPhase.Proved;

		internal static bool Text(string text, int max, bool empty)
		{
			if (text == null || text.Length > max || !empty && text.Length == 0) return false;
			for (int i = 0; i < text.Length; i++)
			{
				if (char.IsControl(text[i])) return false;
				if (!char.IsSurrogate(text[i])) continue;
				if (!char.IsHighSurrogate(text[i]) || i + 1 == text.Length
					|| !char.IsLowSurrogate(text[++i])) return false;
			}
			return true;
		}
	}
}
