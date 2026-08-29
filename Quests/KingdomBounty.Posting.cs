using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomBounty
	{
		private static void PostNotice(KingdomSystem System, Zone Z, GameObject Founder)
		{
			KingdomSurvey survey = KingdomSurvey.Take(Z);
			BountyTask[] tasks = new BountyTask[KingdomBountyRules.TaskCount]
			{
				BountyTask.Clearance, BountyTask.Fetch, BountyTask.Manning, BountyTask.Scouting
			};
			string[] refusals = new string[KingdomBountyRules.TaskCount];
			string[] options = new string[KingdomBountyRules.TaskCount];
			for (int i = 0; i < tasks.Length; i++)
			{
				refusals[i] = WhyNotPostable(System, Z, Founder, survey, tasks[i]);
				options[i] = (refusals[i] == null)
					? KingdomBountyRules.TaskName(tasks[i]).Capitalize()
					: ("{{K|" + KingdomBountyRules.TaskName(tasks[i]).Capitalize() + " -- " + refusals[i] + "}}");
			}
			int pick = Popup.PickOption(
				Title: "What is the price for?",
				Intro: "Name a task and a price. Settlers read the notice on their own time and decide for themselves; nobody is ordered to take it.",
				Options: options, AllowEscape: true);
			if (pick < 0)
			{
				return;
			}
			if (refusals[pick] != null)
			{
				Popup.Show(refusals[pick].Capitalize() + ".");
				return;
			}
			Stake(System, Z, Founder, survey, tasks[pick]);
		}

		/// <summary>Why a task cannot be posted on this ground right now, or null when it can.
		/// Lower-case clause, no trailing period.</summary>
		private static string WhyNotPostable(KingdomSystem System, Zone Z, GameObject Founder, KingdomSurvey Survey, BountyTask Task)
		{
			switch (Task)
			{
			case BountyTask.Clearance:
			{
				if (!KingdomMaterials.Enabled)
				{
					return "the settlement is not clearing ground";
				}
				Cell cell = (Founder != null) ? Founder.CurrentCell : null;
				if (cell == null)
				{
					return "there is nowhere here to stake a cord";
				}
				return null;
			}
			case BountyTask.Fetch:
			{
				if (MarkablePiles(Z, Founder).Count == 0)
				{
					return "there is no pile of materials within reach to mark";
				}
				if (KingdomMaterials.Stock(Z).None)
				{
					return "there is no stockpile dedicated to carry anything into";
				}
				return null;
			}
			case BountyTask.Manning:
				if (Survey.Works.Count == 0) return "the settlement has no works to stand";
				if (ManningCandidates(Survey).Count == 0)
					return "the settlement has no idle, unpromised work to stand";
				return KingdomCrews.WorkHandCount(System,
					KingdomCrews.AvailableSettlers(System, Survey)) > 0
					? null : "every grounded labour hand is already carrying water";
			case BountyTask.Scouting:
				return (Frontier(System).Count == 0) ? "the claim has no unclaimed edge left" : null;
			default:
				return "the notice board does not know that word";
			}
		}

		private static void Stake(KingdomSystem System, Zone Z, GameObject Founder, KingdomSurvey Survey, BountyTask Task)
		{
			int magnitude = 0;
			int x1 = 0;
			int y1 = 0;
			int x2 = 0;
			int y2 = 0;
			GameObject pile = null;
			GameObject manningWork = null;
			if (Task == BountyTask.Clearance && !PickRect(System, Z, Founder, out x1, out y1, out x2, out y2, out magnitude))
			{
				return;
			}
			if (Task == BountyTask.Fetch && !PickPile(System, Z, Founder, out pile, out magnitude))
			{
				return;
			}
			if (Task == BountyTask.Manning)
			{
				if (!PickManningWork(Survey, out manningWork)) return;
				magnitude = manningWork.GetIntProperty("KingdomStaffNeeded");
			}
			int price = PickPrice(System, Survey, Task, magnitude);
			if (price <= 0)
			{
				return;
			}
			string warning = (Survey.StoredWater < price)
				? ("\n\n{{r|The stores hold " + Survey.StoredWater + " drams, which will not cover it today.}} Nothing is set aside now, so the notice may stand until they do -- but whoever claims it will be owed until then.")
				: "";
			string detail = Task == BountyTask.Manning
				? "The promised work is " + manningWork.ShortDisplayName + "." : null;
			if (Popup.ShowYesNo("Stake the notice?\n\n" + KingdomBountyRules.NoticeText(Task, price, detail)
				+ "\n\nNo water leaves the stores until somebody has done it." + warning) != DialogResult.Yes)
			{
				return;
			}
			Cell cell = HeartCell(Z, Founder);
			if (cell == null)
			{
				Popup.Show("There is nowhere at the heart to drive a stake.");
				return;
			}
			GameObject notice = GameObject.Create(NoticeBlueprint);
			r_KingdomNotice data = (notice != null) ? notice.GetPart<r_KingdomNotice>() : null;
			if (data == null)
			{
				InvokeCleanupOnce(notice, true);
				Popup.Show("The stake could not be driven.");
				return;
			}
			data.TaskCode = (int)Task;
			data.Price = price;
			data.PostedTick = The.Game.TimeTicks;
			data.ScheduleVersion = 2;
			data.EventStreamId = KingdomBountyRules.NoticeEventStream(notice.ID);
			data.LifecycleId = KingdomBountyRules.NoticeEventId(notice.ID);
			data.AttemptScheduleExhausted = !KingdomBountyRules.TryFirstAttemptTick(data.PostedTick, out data.NextAttemptTick);
			data.Magnitude = magnitude;
			if (Task == BountyTask.Manning)
			{
				BindManningTarget(data, manningWork);
				if (!BindManningOption(System, data, data.PostedTick))
				{
					InvokeCleanupOnce(notice, true);
					Popup.Show("The realm's manning clock could not be bound safely.");
					return;
				}
			}
			data.X1 = x1;
			data.Y1 = y1;
			data.X2 = x2;
			data.Y2 = y2;
			data.PostChronicleLine = KingdomBountyRules.PostedChronicle(
				KingdomPresentation.Rich(System.SeatName), Task, price);
			data.PostMessageLine = "{{G|The notice is up.}} "
				+ KingdomBountyRules.NoticeText(Task, price, null);
			data.PostZoneId = Z.ZoneID;
			data.PostCellX = cell.X;
			data.PostCellY = cell.Y;
			data.PostPileCellX = (pile == null || pile.CurrentCell == null) ? 0 : pile.CurrentCell.X;
			data.PostPileCellY = (pile == null || pile.CurrentCell == null) ? 0 : pile.CurrentCell.Y;
			data.PostMessageState = (int)BountySinkDisposition.Pending;
			data.PostPhase = (int)BountyPostPhase.Bound;
			string oldPileMark = (pile == null) ? null : pile.GetStringProperty(FetchMarkProperty);
			Cell pileCell = (pile == null) ? null : pile.CurrentCell;
			bool inserted = false;
			GameObject acceptedNotice = null;
			try
			{
				acceptedNotice = cell.AddObject(notice);
				inserted = ReferenceEquals(acceptedNotice, notice)
					&& NoticeBindingExact(notice, data, Z, cell);
				if (!inserted) throw new InvalidOperationException(
					"The notice insertion callback changed its exact object, part, cell, or zone binding.");
				notice.MakeActive();
				if (!NoticeBindingExact(notice, data, Z, cell)) throw new InvalidOperationException(
					"Activating the notice changed its exact object, part, cell, or zone binding.");
				if (pile != null)
				{
					if (!PileBindingExact(pile, Z, pileCell)
						|| pile.GetStringProperty(FetchMarkProperty) != oldPileMark
						|| !string.IsNullOrEmpty(oldPileMark)) throw new InvalidOperationException(
							"The selected fetch pile changed before its mark compare-and-set.");
					data.PileId = pile.ID;
					pile.SetStringProperty(FetchMarkProperty, notice.ID);
					if (!PileBindingExact(pile, Z, pileCell)
						|| pile.GetStringProperty(FetchMarkProperty) != notice.ID
						|| !NoticeBindingExact(notice, data, Z, cell)) throw new InvalidOperationException(
							"The fetch mark compare-and-set did not leave its exact binding.");
				}
			}
			catch (Exception error)
			{
				if (pile != null && PileBindingExact(pile, Z, pileCell)
					&& pile.GetStringProperty(FetchMarkProperty) == notice.ID)
				{
					pile.SetStringProperty(FetchMarkProperty, oldPileMark, RemoveIfNull: true);
				}
				Quarantine(data, inserted
					? "Posting crossed an uncertain activation or fetch-mark callback seam."
					: "Posting crossed an uncertain insertion callback seam.");
				data.StakeCleanupState = (int)BountySinkDisposition.Pending;
				CleanupFrame cleanup;
				if (TryCaptureCleanup(notice, data, out cleanup))
				{
					data.StakeCleanupState = (int)BountySinkDisposition.Attempting;
					InvokeCleanupOnce(notice, true);
					data.StakeCleanupState = (int)(CleanupFinalized(cleanup)
						? BountySinkDisposition.Delivered : BountySinkDisposition.Lost);
				}
				else
				{
					data.StakeCleanupState = (int)BountySinkDisposition.Skipped;
				}
				MetricsManager.LogError("ThousandAndFirst bounty posting", error);
				Popup.Show((data.StakeCleanupState == (int)BountySinkDisposition.Delivered)
					? "The stake could not be driven cleanly; its one cleanup attempt removed it."
					: "The stake crossed an uncertain callback seam. It is quarantined and cleanup was not repeated.");
				return;
			}
			finally
			{
				KingdomSurvey.ObserveAddResultInActive(Z, notice, acceptedNotice);
			}
			// The notice, its active schedule, and any protected fetch mark are now durable.
			// Telling and description may fail without turning that completed publication free.
			KingdomGovernanceScope.Commit("post bounty");
			Describe(System, Z, notice, data);
			ContinuePost(System, Z, notice, data);
			KingdomLog.Log("bounty: posted task=" + KingdomBountyRules.TaskKey(Task) + " price=" + price + " magnitude=" + magnitude);
		}

	}
}
