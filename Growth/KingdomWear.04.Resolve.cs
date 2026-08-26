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
		private static void Resolve(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			string settlementId = KingdomChronicle.SettlementId(System);
			long timeTicks = The.Game.TimeTicks;
			int hands = KingdomMaterialRules.FreeHands(System.Population, System.AssignedCrew);
			List<GameObject> damaged = new List<GameObject>();
			GameObject workingRepair = null;
			// Everything the settlement finished, not only the works that ask for crew. Damage
			// reaches a staffless design (KingdomSubsidence.Ruin walks this same list), so mending
			// has to reach it back: a cistern the fall holed was previously damaged forever,
			// because nothing ever put it in front of the repair queue. Addendum 10(b) makes the
			// damage count against the level, and "mending restores function" is only true if the
			// mending can start.
			for (int i = 0; i < Survey.Built.Count; i++)
			{
				GameObject work = Survey.Built[i];
				if (!GameObject.Validate(work))
				{
					continue;
				}
				// The two attended causes are causes of RUNNING, so they are only ever asked of a
				// work with a crew on it. A cistern is not run hard and a palisade does not act up.
				bool hasRepair = HasActiveRepair(work, out _);
				if (!hasRepair && work.GetIntProperty(StaffNeededProperty) > 0)
				{
					RollWear(System, settlementId, work, work.GetIntProperty(EffectivenessProperty), timeTicks);
				}
				r_KingdomWear wear = work.GetPart<r_KingdomWear>();
				if (wear != null && wear.LifecycleQuarantined)
				{
					TellWearQuarantine(System, work, wear);
					continue;
				}
				if (wear == null || wear.Wear <= 0)
				{
					continue;
				}
				// The kind-appropriate consequence, on top of the general effectiveness scale every
				// consumer now applies for itself (KingdomWearRules.WorkEffectiveness): a damaged
				// store loses what it is holding, on world time, until somebody mends it.
				Leak(System, Survey, work, wear, timeTicks);
				damaged.Add(work);
				if ((wear.RepairEffortLeft > 0 || hasRepair) && workingRepair == null)
				{
					workingRepair = work;
				}
			}
			System.DamagedWorks = damaged.Count;
			if (damaged.Count == 0)
			{
				return;
			}
			if (workingRepair != null)
			{
				r_KingdomWear workingWear = workingRepair.RequirePart<r_KingdomWear>();
				if (workingWear.RepairEffortLeft > 0)
				{
					AdvanceRepair(System, workingRepair, workingWear, hands, timeTicks);
				}
				AnnounceQueued(System, damaged, workingRepair);
				return;
			}
			GameObject readyWork = null;
			GameObject speaksFirst = null;
			KingdomWearRules.RepairVerdict speaksFirstVerdict = KingdomWearRules.RepairVerdict.Ready;
			for (int i = 0; i < damaged.Count; i++)
			{
				GameObject work = damaged[i];
				r_KingdomWear wear = work.RequirePart<r_KingdomWear>();
				KingdomWearRules.RepairVerdict verdict = Assess(Z, work, wear, hands);
				if (verdict == KingdomWearRules.RepairVerdict.Ready && readyWork == null)
				{
					readyWork = work;
				}
				else if (KingdomWearRules.IsBlocked(verdict) && speaksFirst == null && wear.AnnouncedBlock != (int)verdict)
				{
					speaksFirst = work;
					speaksFirstVerdict = verdict;
				}
			}
			if (readyWork != null)
			{
				StartRepair(System, readyWork, readyWork.RequirePart<r_KingdomWear>(), timeTicks);
				return;
			}
			if (speaksFirst != null)
			{
				r_KingdomWear wear = speaksFirst.RequirePart<r_KingdomWear>();
				wear.AnnouncedBlock = (int)speaksFirstVerdict;
				string line = KingdomWearRules.ReasonLine(speaksFirstVerdict, DisplayName(speaksFirst));
				if (line != null)
				{
					System.Ledger.Note("{{r|" + line + "}}");
				}
			}
		}

	}
}
