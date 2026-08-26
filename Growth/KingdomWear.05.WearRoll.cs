using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomWear
	{
		/// <summary>Every OTHER damaged work says once, if it has not already, that this pass's
		/// hands went to the one mending already under way &mdash; the same "one gang, one job"
		/// news a second condemned building gets from <c>KingdomMaterials.OnSettlementPass</c>.</summary>
		private static void AnnounceQueued(KingdomSystem System, List<GameObject> Damaged, GameObject Working)
		{
			for (int i = 0; i < Damaged.Count; i++)
			{
				GameObject work = Damaged[i];
				if (work == Working)
				{
					continue;
				}
				r_KingdomWear wear = work.RequirePart<r_KingdomWear>();
				if (wear.Held || wear.AnnouncedBlock == (int)KingdomWearRules.RepairVerdict.OtherWorkUnderway)
				{
					continue;
				}
				wear.AnnouncedBlock = (int)KingdomWearRules.RepairVerdict.OtherWorkUnderway;
				string line = KingdomWearRules.ReasonLine(KingdomWearRules.RepairVerdict.OtherWorkUnderway, DisplayName(work));
				if (line != null)
				{
					System.Ledger.Note("{{K|" + line + "}}");
				}
			}
		}

		// ==================================================================================
		// The three causes.
		// ==================================================================================

		private static void RollWear(KingdomSystem System, string SettlementId, GameObject Work, int CrewStretch, long TimeTicks)
		{
			long last;
			long active;
			int completed = Work.GetIntProperty(SemanticPassCompletedProperty);
			if (!TryReadStrictTick(Work, SemanticPassCompletedTickProperty, out last)
				|| !TryReadStrictTick(Work, SemanticPassTickProperty, out active)
				|| (completed != 0 && completed != 1))
			{
				QuarantineWear(System, Work, "Its attended wear-pass clock is malformed.");
				return;
			}
			KingdomWearPassPhase phase = (KingdomWearPassPhase)Work.GetIntProperty(
				SemanticPassPhaseProperty);
			if (completed == 1 && active == last
				&& (phase == KingdomWearPassPhase.TemperDone
					|| phase == KingdomWearPassPhase.None))
			{
				Work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.None);
				KingdomMaterials.WriteTick(Work, SemanticPassTickProperty, 0L);
				active = 0L;
				phase = KingdomWearPassPhase.None;
			}
			if (completed == 1 && last == TimeTicks)
			{
				return;
			}
			KingdomWearPassAction action = KingdomWearRules.PassAction(last, active, phase, TimeTicks);
			if (action == KingdomWearPassAction.AlreadyApplied) return;
			if (action == KingdomWearPassAction.Quarantine)
			{
				QuarantineWear(System, Work, "Its attended wear-pass receipt regressed or changed.");
				return;
			}
			if (action == KingdomWearPassAction.Start)
			{
				int original = Work.GetIntProperty(HardRunStreakProperty);
				if (original < 0)
				{
					QuarantineWear(System, Work, "Its hard-running streak is malformed.");
					return;
				}
				int target = (CrewStretch >= 100)
					? ((original == int.MaxValue) ? int.MaxValue : original + 1) : 0;
				KingdomMaterials.WriteTick(Work, SemanticPassTickProperty, TimeTicks);
				Work.SetIntProperty(SemanticPassOriginalStreakProperty, original);
				Work.SetIntProperty(SemanticPassTargetStreakProperty, target);
				Work.SetIntProperty(SemanticPassHardRollProperty,
					CrewStretch >= 100 && KingdomWearRules.RollHardRun(SettlementId, Work.ID, target) ? 1 : 0);
				Work.SetIntProperty(SemanticPassTemperRollProperty,
					CrewStretch > 0 && Work.GetIntProperty(KingdomSalvage.CertifiedProperty) == 1
					&& KingdomWearRules.RollTemperamental(SettlementId, Work.ID, TimeTicks) ? 1 : 0);
				Work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.Bound);
				phase = KingdomWearPassPhase.Bound;
			}
			int beforeStreak = Work.GetIntProperty(SemanticPassOriginalStreakProperty);
			int targetStreak = Work.GetIntProperty(SemanticPassTargetStreakProperty);
			int hardRoll = Work.GetIntProperty(SemanticPassHardRollProperty);
			int temperRoll = Work.GetIntProperty(SemanticPassTemperRollProperty);
			if (beforeStreak < 0 || targetStreak < 0
				|| (hardRoll != 0 && hardRoll != 1) || (temperRoll != 0 && temperRoll != 1))
			{
				QuarantineWear(System, Work, "Its bound hard-running streak is malformed.");
				return;
			}
			if (phase == KingdomWearPassPhase.Bound)
			{
				Work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.StreakIntent);
				phase = KingdomWearPassPhase.StreakIntent;
			}
			if (phase == KingdomWearPassPhase.StreakIntent)
			{
				int current = Work.GetIntProperty(HardRunStreakProperty);
				if (current == beforeStreak) Work.SetIntProperty(HardRunStreakProperty, targetStreak);
				else if (current != targetStreak)
				{
					QuarantineWear(System, Work, "Its hard-running streak changed inside a bound pass.");
					return;
				}
				Work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.StreakDone);
				phase = KingdomWearPassPhase.StreakDone;
			}
			if (phase == KingdomWearPassPhase.StreakDone)
			{
				Work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.HardIncident);
				phase = KingdomWearPassPhase.HardIncident;
			}
			if (phase == KingdomWearPassPhase.HardIncident)
			{
				if (Work.GetIntProperty(SemanticPassHardRollProperty) == 1
					&& !ApplyDamageIncident(System, Work, KingdomWearRules.WearCause.HardRunning,
						WearEventId(Work, "hard", TimeTicks))) return;
				Work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.HardDone);
				phase = KingdomWearPassPhase.HardDone;
			}
			if (phase == KingdomWearPassPhase.HardDone)
			{
				Work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.TemperIncident);
				phase = KingdomWearPassPhase.TemperIncident;
			}
			if (phase == KingdomWearPassPhase.TemperIncident)
			{
				if (Work.GetIntProperty(SemanticPassTemperRollProperty) == 1
					&& !ApplyDamageIncident(System, Work, KingdomWearRules.WearCause.TemperamentalTech,
						WearEventId(Work, "temper", TimeTicks))) return;
				Work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.TemperDone);
			}
			KingdomMaterials.WriteTick(Work, SemanticPassCompletedTickProperty, TimeTicks);
			Work.SetIntProperty(SemanticPassCompletedProperty, 1);
			Work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.None);
			KingdomMaterials.WriteTick(Work, SemanticPassTickProperty, 0L);
		}

	}
}
