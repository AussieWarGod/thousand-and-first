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
		/// <summary>
		/// The Charter's own entry: shows what is standing at this heart, lets the founder post
		/// another, and lets them take one down. Does all its own messaging.
		/// </summary>
		/// <param name="System">The realm. Unfounded is refused with a reason.</param>
		/// <param name="Founder">The object holding the Charter, read for where it is standing.</param>
		public static void OpenNotices(KingdomSystem System, GameObject Founder)
		{
			if (System == null || !System.Founded)
			{
				Popup.Show("You rule nothing yet.");
				return;
			}
			if (!Enabled)
			{
				Popup.Show("The settlement posts no prices. (Options: the posted price)");
				return;
			}
			Zone zone = (Founder != null) ? Founder.CurrentZone : null;
			if (zone == null || !System.ClaimedZones.Contains(zone.ZoneID))
			{
				Popup.Show("A notice is staked on the kingdom's own ground, where its people will walk past it.");
				return;
			}
			while (true)
			{
				List<GameObject> notices = Notices(zone);
				List<string> options = new List<string>();
				for (int i = 0; i < notices.Count; i++)
				{
					options.Add(StatusLine(System, notices[i]));
				}
				options.Add((notices.Count >= KingdomBountyRules.MaxNotices)
					? ("{{K|" + KingdomBountyRules.MaxNotices + " notices already stand here}}")
					: "{{W|Post a new notice}}");
				int pick = Popup.PickOption(
					Title: "The notice board of " + KingdomPresentation.Rich(System.SeatName),
					Intro: "Nothing here is set aside. A price leaves the stores the day the work is done, and not before.",
					Options: options, AllowEscape: true);
				if (pick < 0)
				{
					return;
				}
				if (pick >= notices.Count)
				{
					if (notices.Count >= KingdomBountyRules.MaxNotices)
					{
						Popup.Show("Three notices is as many as one heart can carry without the settlement stopping to read them all.");
						continue;
					}
					PostNotice(System, zone, Founder);
					if (KingdomGovernanceScope.HasCommitted)
					{
						return;
					}
					continue;
				}
				Inspect(System, zone, notices[pick]);
			}
		}

		private static void Inspect(KingdomSystem System, Zone Z, GameObject Notice)
		{
			r_KingdomNotice notice = Notice.GetPart<r_KingdomNotice>();
			if (notice == null)
			{
				return;
			}
			BountyTask task = (BountyTask)notice.TaskCode;
			string body = KingdomBountyRules.NoticeText(task, notice.Price, DetailOf(System, Z, notice))
				+ "\n\n" + Progress(System, notice);
			int pick = Popup.PickOption(
				Title: "A posted notice",
				Intro: body,
				Options: new string[2] { "Leave it standing", "{{W|Take it down}}" },
				AllowEscape: true);
			if (pick != 1)
			{
				return;
			}
			if (Popup.ShowYesNo("Take the notice down?\n\nNothing is owed either way, and nobody who took it is made to give anything back.") != DialogResult.Yes)
			{
				return;
			}
			Withdraw(System, Z, Notice, notice);
		}

		private static void Withdraw(KingdomSystem System, Zone Z, GameObject Notice, r_KingdomNotice Data)
		{
			EnsureLifecycleIdentity(Notice, Data);
			if ((BountyWithdrawPhase)Data.WithdrawPhase == BountyWithdrawPhase.None)
			{
				Cell initialCell = (Notice != null) ? Notice.CurrentCell : null;
				if (!NoticeBindingExact(Notice, Data, Z, initialCell))
				{
					Quarantine(Data, "Withdrawal could not bind the exact notice object, part, cell, and zone.");
					TellQuarantine(System, Data);
					return;
				}
				BountyTask task = (BountyTask)Data.TaskCode;
				bool claimed = !string.IsNullOrEmpty(Data.WorkerName);
				Data.WithdrawPileId = Data.PileId;
				Data.WithdrawZoneId = Z.ZoneID;
				Data.WithdrawCellX = Notice.CurrentCell.X;
				Data.WithdrawCellY = Notice.CurrentCell.Y;
				GameObject withdrawPile = string.IsNullOrEmpty(Data.WithdrawPileId)
					? null : Z.FindObjectByID(Data.WithdrawPileId);
				Data.WithdrawPileCellX = (withdrawPile != null && withdrawPile.CurrentCell != null)
					? withdrawPile.CurrentCell.X : 0;
				Data.WithdrawPileCellY = (withdrawPile != null && withdrawPile.CurrentCell != null)
					? withdrawPile.CurrentCell.Y : 0;
				Data.WithdrawChronicleLine = KingdomBountyRules.WithdrawnChronicle(
					KingdomPresentation.Rich(System.SeatName), task, claimed,
					KingdomPresentation.Rich(Data.WorkerName));
				Data.WithdrawMessageLine = "{{K|The notice comes off the stake.}} " + (claimed
					? ("Word will reach " + KingdomPresentation.Rich(Data.WorkerName)
						+ " that the settlement has changed its mind. Nothing is asked back.")
					: "Nobody had taken it, and nothing was spent.");
				Data.WithdrawMessageState = (int)BountySinkDisposition.Pending;
				Data.WithdrawPhase = (int)BountyWithdrawPhase.Bound;
			}
			ContinueWithdraw(System, Z, Notice, Data);
		}

		private static void ContinueWithdraw(KingdomSystem System, Zone Z, GameObject Notice,
			r_KingdomNotice Data)
		{
			BountyWithdrawPhase phase = (BountyWithdrawPhase)Data.WithdrawPhase;
			if (phase == BountyWithdrawPhase.None || phase == BountyWithdrawPhase.CleanupLost) return;
			Cell noticeCell = BoundCell(Z, Data.WithdrawZoneId,
				Data.WithdrawCellX, Data.WithdrawCellY);
			if (!NoticeBindingExact(Notice, Data, Z, noticeCell))
			{
				Data.WithdrawPhase = (int)BountyWithdrawPhase.CleanupLost;
				Quarantine(Data, "The withdrawing notice no longer has its exact object, part, cell, and zone binding.");
				return;
			}
			if (phase == BountyWithdrawPhase.CleanupAttempting)
			{
				Data.WithdrawPhase = (int)BountyWithdrawPhase.CleanupLost;
				Quarantine(Data, "Withdrawal cleanup was interrupted; the destructive callback was not repeated.");
				return;
			}
			if (phase == BountyWithdrawPhase.Bound)
			{
				if (!ClearBoundFetchMark(Z, Notice, Data, Data.WithdrawPileId))
				{
					Data.WithdrawPhase = (int)BountyWithdrawPhase.CleanupLost;
					Quarantine(Data, "The withdrawing notice could not prove an exact fetch-mark compare-and-clear.");
					return;
				}
				Data.WithdrawPhase = (int)BountyWithdrawPhase.MarkCleared;
				phase = BountyWithdrawPhase.MarkCleared;
			}
			if (phase == BountyWithdrawPhase.MarkCleared)
			{
				if (!KingdomChronicle.RecordOnce(System, EventId(Data, "withdrawn"),
					Data.WithdrawChronicleLine)) return;
				Data.WithdrawPhase = (int)BountyWithdrawPhase.ChronicleDone;
				phase = BountyWithdrawPhase.ChronicleDone;
			}
			if (phase == BountyWithdrawPhase.ChronicleDone)
			{
				if (!DeliverMessage(ref Data.WithdrawMessageState, Data.WithdrawMessageLine)) return;
				Data.WithdrawPhase = (int)BountyWithdrawPhase.MessageSettled;
				phase = BountyWithdrawPhase.MessageSettled;
			}
			if (phase == BountyWithdrawPhase.MessageSettled)
			{
				CleanupFrame cleanup;
				if (!TryCaptureCleanup(Notice, Data, out cleanup))
				{
					Data.WithdrawPhase = (int)BountyWithdrawPhase.CleanupLost;
					Quarantine(Data,
						"Withdrawal cleanup could not capture its exact notice and data-part identity.");
					return;
				}
				Data.WithdrawPhase = (int)BountyWithdrawPhase.CleanupAttempting;
				KingdomLog.Log("bounty: withdrawing " + EventId(Data, "withdrawn"));
				InvokeCleanupOnce(Notice, false);
				if (!CleanupFinalized(cleanup))
				{
					Data.WithdrawPhase = (int)BountyWithdrawPhase.CleanupLost;
					Quarantine(Data, "Withdrawal cleanup was vetoed or changed by its destructive callback; it was not repeated.");
				}
			}
		}

	}
}
