using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomUpgrade
	{
		private static bool _resolving;

		private static void Resolve(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			int freeHands = System.Population - System.AssignedCrew;
			if (freeHands < 0)
			{
				freeHands = 0;
			}
			// Finished improvements are handed over here as well as on the work's own turn tick.
			// The tick is the responsive path; this is the one that cannot be missed, because the
			// settlement pass runs whenever the founder is standing here and a handover that never
			// happens leaves the old work standing beside its replacement forever.
			List<GameObject> pending = new List<GameObject>(Survey.Improvements);
			for (int i = pending.Count - 1; i >= 0; i--)
			{
				GameObject item = pending[i];
				r_KingdomImprovement working = item.GetPart<r_KingdomImprovement>();
				if (working == null || !working.Working) pending.RemoveAt(i);
			}
			for (int i = 0; i < pending.Count; i++)
			{
				GameObject item = pending[i];
				if (!GameObject.Validate(ref item))
				{
					continue;
				}
				r_KingdomImprovement working = item.GetPart<r_KingdomImprovement>();
				if (working != null && working.Working)
				{
					working.PollHandover(The.Game.TimeTicks);
				}
			}
			List<GameObject> works = new List<GameObject>(Survey.Built);
			bool otherWorkUnderway = false;
			for (int i = 0; i < Survey.Improvements.Count; i++)
			{
				GameObject item = Survey.Improvements[i];
				r_KingdomImprovement improvement = item.GetPart<r_KingdomImprovement>();
				if (improvement != null && improvement.Working)
				{
					otherWorkUnderway = true;
				}
			}
			for (int i = 0; !otherWorkUnderway && i < works.Count; i++)
			{
				if (HasActiveConstruction(works[i])) otherWorkUnderway = true;
			}
			GameObject readyWork = null;
			Assessment readyAssessment = default;
			GameObject speaksFirst = null;
			Assessment speaksFirstAssessment = default;
			for (int i = 0; i < works.Count; i++)
			{
				Assessment assessment = Assess(System, Z, works[i], Survey, freeHands, otherWorkUnderway);
				if (!assessment.Valid || assessment.Verdict == KingdomUpgradeRules.UpgradeVerdict.NoSuccessor)
				{
					continue;
				}
				if (KingdomUpgradeRules.IsReady(assessment.Verdict) && readyWork == null)
				{
					readyWork = works[i];
					readyAssessment = assessment;
				}
				else if (KingdomUpgradeRules.IsBlocked(assessment.Verdict) && speaksFirst == null
					&& works[i].RequirePart<r_KingdomImprovement>().AnnouncedReason != (int)assessment.Verdict)
				{
					speaksFirst = works[i];
					speaksFirstAssessment = assessment;
				}
			}
			if (readyWork != null && GiveFirstNotice(System)) return;
			if (readyWork != null)
			{
				Begin(System, Z, readyWork, readyAssessment, Survey);
				return;
			}
			if (speaksFirst != null && speaksFirstAssessment.Reason != null)
			{
				speaksFirst.RequirePart<r_KingdomImprovement>().AnnouncedReason = (int)speaksFirstAssessment.Verdict;
				MessageQueue.AddPlayerMessage("{{K|" + speaksFirstAssessment.Reason + "}}");
				System.Ledger.Note("{{K|" + speaksFirstAssessment.Reason + "}}");
			}
		}

		/// <summary>
		/// Tells the founder once per game that the settlement betters its own works, and where
		/// to stop it. Modal on purpose and exactly once: this is the only moment in the mod
		/// where the settlement will change something the founder placed the order for, and
		/// nobody should ever discover that by finding it already done.
		/// </summary>
		/// <param name="System">The kingdom, for its name.</param>
		public static bool GiveFirstNotice(KingdomSystem System)
		{
			if (The.Game == null || The.Game.GetIntGameState(NoticedState) == 1)
			{
				return false;
			}
			The.Game.SetIntGameState(NoticedState, 1);
			Popup.Show(KingdomUpgradeRules.FirstNoticeLine(KingdomPresentation.Rich(System.SeatName)));
			return true;
		}

		/// <summary>
		/// Raises the scaffolding for one improvement, in the predecessor's own cell. The
		/// predecessor keeps standing and keeps working for the whole build; nothing it holds is
		/// touched until its replacement is actually on the ground.
		/// </summary>
		/// <param name="System">The kingdom.</param>
		/// <param name="Z">Zone the work stands in.</param>
		/// <param name="Work">The work being improved.</param>
		/// <param name="A">Its assessment, which must be <c>Ready</c>.</param>
		/// <param name="Survey">This pass's survey, which the cost is drawn through so its
		/// counters stay true for everything that runs after.</param>
		/// <returns>True once scaffolding is actually standing and the water is actually
		/// spent.</returns>
	}
}
