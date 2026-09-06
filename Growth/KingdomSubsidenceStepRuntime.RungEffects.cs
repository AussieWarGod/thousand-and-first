using System;
using System.Collections.Generic;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	internal static partial class KingdomSubsidenceStepRuntime
	{
		/// <summary>Resumes a frozen plan before ordinary city projection can alter its carriers.
		/// Completion proves physical obligations and durable release, not telling or step retirement.</summary>
		internal static bool TryResumeRung(KingdomSystem system, Zone zone, KingdomSurvey survey,
			out string refusal)
		{
			refusal = "The settlement's fall waits for its exact works and residents.";
			try
			{
				if (!TryReadOwned(system, out List<Snapshot> books)) return false;
				Snapshot owner = books.Find(item => ReferenceEquals(item.City, system.City));
				if (owner == null) return false;
				KingdomSubsidenceStepOperation active = owner.Step.Active;
				if (active == null || active.RungModel == KingdomSubsidenceStepRules.NoRungs
					|| active.RungModel == KingdomSubsidenceStepRules.UnplannedRungs)
				{ refusal = null; return true; }
				if (!TryRungFrame(system, zone, survey, out RungFrame frame)) return false;
				for (int i = 0; i < frame.Plan.Works.Count; i++)
				{
					KingdomSubsidenceRungWork work = frame.Plan.Works[i];
					bool complete = work.WearPhase == KingdomSubsidenceEffectPhase.Proved;
					foreach (KingdomSubsidenceRungRoof roof in work.Roofs)
						complete &= roof.Phase == KingdomSubsidenceEffectPhase.Proved;
					if (complete)
					{
						if (!ResumeRungRelease(frame, i, ref refusal)) return false;
						continue;
					}
					if (!frame.Subjects.TryGetValue(work.ObjectId, out GameObject body)
						|| !RungWorkExact(frame, work, body)) return false;
					if (work.WearPhase != KingdomSubsidenceEffectPhase.Proved)
					{
						if (!KingdomSubsidenceStepRules.TryArmRungWear(frame.Owner.Step, i,
							out KingdomSubsidenceStepBook intent) || !SaveRung(frame, intent)) return false;
						work = frame.Plan.Works[i];
						if (!KingdomSubsidenceWearRuntime.TryApply(frame.Plan, i, body,
							() => RungWorkExact(frame, work, body), out r_KingdomWear measured, out string wearRefusal))
						{ refusal = wearRefusal ?? refusal; return false; }
						if (!RungWorkExact(frame, work, body) || measured == null
							|| !ReferenceEquals(body.GetPart<r_KingdomWear>(), measured)
							|| !KingdomSubsidenceStepRules.TryProveRungWear(frame.Owner.Step, i, true, true,
								measured.Wear, out KingdomSubsidenceStepBook proved) || !SaveRung(frame, proved)) return false;
					}
					for (int roof = 0; roof < frame.Plan.Works[i].Roofs.Count; roof++)
					{
						if (frame.Plan.Works[i].Roofs[roof].Phase == KingdomSubsidenceEffectPhase.Proved) continue;
						if (!RungWorkExact(frame, frame.Plan.Works[i], body)
							|| !KingdomSubsidenceStepRules.TryArmRungRoof(frame.Owner.Step, i, roof,
								out KingdomSubsidenceStepBook intent) || !SaveRung(frame, intent)
							|| !ApplyRungRoof(frame, i, roof)) return false;
					}
					if (!ResumeRungRelease(frame, i, ref refusal)) return false;
				}
				if (!RungOwnerExact(frame) || !KingdomSubsidenceRungRules.ReleasedComplete(frame.Plan)) return false;
				refusal = null;
				return true;
			}
			catch (Exception) { return false; }
		}

	}
}
