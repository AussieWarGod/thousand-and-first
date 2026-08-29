using System;
using System.Collections.Generic;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomBounty
	{
		private static List<GameObject> ManningCandidates(KingdomSurvey Survey)
		{
			List<GameObject> found = new List<GameObject>();
			if (Survey == null) return found;
			HashSet<string> noticed = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Survey.Notices.Count; i++)
			{
				r_KingdomNotice data = Survey.Notices[i].GetPart<r_KingdomNotice>();
				if (data != null && data.TaskCode == (int)BountyTask.Manning && !data.Done
					&& !data.LifecycleQuarantined && !string.IsNullOrEmpty(data.ManningWorkId))
					noticed.Add(data.ManningWorkId);
			}
			for (int i = 0; i < Survey.Works.Count; i++)
			{
				GameObject work = Survey.Works[i];
				string workId = work?.IDIfAssigned;
				if (!GameObject.Validate(work) || string.IsNullOrEmpty(workId)
					|| work.GetIntProperty("KingdomStaffNeeded") <= 0
					|| work.GetIntProperty("KingdomEffectiveness") > 0
					|| noticed.Contains(workId)) continue;
				found.Add(work);
			}
			return found;
		}

		private static bool PickManningWork(KingdomSurvey Survey, out GameObject Work)
		{
			Work = null;
			List<GameObject> candidates = ManningCandidates(Survey);
			if (candidates.Count == 0)
			{
				Popup.Show("There is no idle, unpromised work to name on the notice.");
				return false;
			}
			string[] options = new string[candidates.Count];
			for (int i = 0; i < candidates.Count; i++)
			{
				int hands = candidates[i].GetIntProperty("KingdomStaffNeeded");
				options[i] = candidates[i].ShortDisplayName + " {{K|(" + hands
					+ (hands == 1 ? " hand" : " hands") + ")}}";
			}
			int pick = Popup.PickOption(Title: "Which work?",
				Intro: "The notice binds one willing resident to this exact work for thirty serviced days. It does not add a free hand.",
				Options: options, AllowEscape: true);
			if (pick < 0) return false;
			Work = candidates[pick];
			return true;
		}

		private static void BindManningTarget(r_KingdomNotice Data, GameObject Work)
		{
			Data.ManningVersion = 1;
			Data.ManningWorkId = Work.ID;
			Data.ManningWorkName = Work.ShortDisplayName;
			Data.Magnitude = Work.GetIntProperty("KingdomStaffNeeded");
		}

		private static BountyBlock ManningBlock(KingdomSystem System, KingdomSurvey Survey,
			r_KingdomNotice Data)
		{
			int work = FindWorkIndex(Survey?.Works, Data?.ManningWorkId);
			if (Data == null || Data.ManningVersion != 1 || work < 0
				|| Survey.Works[work].GetIntProperty("KingdomStaffNeeded") <= 0)
				return BountyBlock.ManningTargetLost;
			if (Survey.Works[work].GetIntProperty("KingdomEffectiveness") > 0)
				return BountyBlock.NoIdleWork;
			List<int> ids;
			return ReaderRoster(System, Survey, BountyTask.Manning, out ids).Count == 0
				? BountyBlock.NoFreeHands : BountyBlock.None;
		}

		private static bool CanTakeManning(KingdomSystem System, Zone Z,
			r_KingdomNotice Data, int ResidentId)
		{
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z) ?? KingdomSurvey.Take(Z);
			if (survey == null || ManningBlock(System, survey, Data) != BountyBlock.None
				|| ResidentId <= 0 || TargetPromisedElsewhere(survey, Data)) return false;
			List<int> ids;
			ReaderRoster(System, survey, BountyTask.Manning, out ids);
			return ids.Contains(ResidentId);
		}

		private static bool TargetPromisedElsewhere(KingdomSurvey Survey, r_KingdomNotice Data)
		{
			for (int i = 0; i < Survey.Notices.Count; i++)
			{
				r_KingdomNotice other = Survey.Notices[i].GetPart<r_KingdomNotice>();
				if (other != null && !ReferenceEquals(other, Data)
					&& other.TaskCode == (int)BountyTask.Manning && !other.Done
					&& !other.LifecycleQuarantined
					&& other.ManningWorkId == Data.ManningWorkId) return true;
			}
			return false;
		}

		private static string ManningDetail(r_KingdomNotice Data)
		{
			string work = string.IsNullOrWhiteSpace(Data.ManningWorkName)
				? "the named work" : Data.ManningWorkName.Trim();
			return "The promised work is " + work + ". A season is "
				+ KingdomBountyRules.ManningSeasonDays + " serviced days.";
		}

		private static string ManningProgress(r_KingdomNotice Data)
		{
			string worker = KingdomPresentation.Rich(Data.WorkerName);
			string work = string.IsNullOrWhiteSpace(Data.ManningWorkName)
				? "the named work" : Data.ManningWorkName.Trim();
			int left = KingdomBountyManningRules.RemainingDays(Data.ManningServedTicks);
			long servedTicks = KingdomBountyManningRules.ClampServed(Data.ManningServedTicks);
			long served = servedTicks / KingdomRules.TicksPerDay;
			if (!Data.ManningAssigned && left > 0)
			{
				string reason = KingdomBountyRules.BlockReason((BountyBlock)Data.AnnouncedBlock,
					BountyTask.Manning, null);
				return "{{W|" + worker + " has " + work + ". " + served + " of "
					+ KingdomBountyRules.ManningSeasonDays + " serviced days are counted.}}"
					+ (reason == null ? " {{K|The service clock is stopped.}}" : " {{r|" + reason + "}}");
			}
			return "{{W|" + worker + " has " + work + ". " + served + " of "
				+ KingdomBountyRules.ManningSeasonDays + " serviced days are counted; " + left
				+ (left == 1 ? " day remains.}}" : " days remain.}}");
		}

		private static bool ManningComplete(r_KingdomNotice Data)
		{
			return KingdomBountyManningRules.RemainingTicks(Data.ManningServedTicks) == 0L;
		}
	}
}
