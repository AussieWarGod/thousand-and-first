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
		private static bool Take(KingdomSystem System, Zone Z, GameObject Notice, r_KingdomNotice Data, BountyTask Task, KingdomBountyRules.BountyAttempt Attempt, long ScheduledTick, int ResidentId)
		{
			if ((BountyTakePhase)Data.TakePhase == BountyTakePhase.None)
			{
				Data.PendingAttemptTick = ScheduledTick;
				Data.PendingWorkerName = Attempt.Name;
				Data.PendingWorkerResidentId = ResidentId;
				Data.PendingVirtueIndex = Attempt.VirtueIndex;
				Data.PendingFlawIndex = Attempt.FlawIndex;
				Data.PendingTasteMatched = Attempt.TasteMatched;
				Data.PendingAttemptConsumed = false;
				Data.TakePhase = (int)BountyTakePhase.Bound;
			}
			ContinueTake(System, Z, Notice, Data);
			return !string.IsNullOrEmpty(Data.WorkerName);
		}

		private static void ContinueTake(KingdomSystem System, Zone Z, GameObject Notice,
			r_KingdomNotice Data)
		{
			BountyTakePhase phase = (BountyTakePhase)Data.TakePhase;
			if (phase == BountyTakePhase.Quarantined || phase == BountyTakePhase.None
				|| phase == BountyTakePhase.Complete) return;
			if (string.IsNullOrEmpty(Data.PendingWorkerName) || Data.PendingAttemptTick < 0L)
			{
				Quarantine(Data, "The notice lost its bound reader before takeover completed.");
				return;
			}
			BountyTask task = (BountyTask)Data.TaskCode;
			bool taskMayStart = false;
			if (phase == BountyTakePhase.Bound)
			{
				Data.TakePhase = (int)BountyTakePhase.TaskIntent;
				phase = BountyTakePhase.TaskIntent;
				taskMayStart = true;
			}
			if (phase == BountyTakePhase.TaskIntent)
			{
				if (task == BountyTask.Manning && !CanTakeManning(System, Z, Data,
					Data.PendingWorkerResidentId))
				{
					if (!taskMayStart)
					{
						Quarantine(Data, "A manning takeover lost its exact resident or work before publication.");
						Data.TakePhase = (int)BountyTakePhase.Quarantined;
						return;
					}
					Data.PendingWorkerName = null;
					Data.TakePhase = (int)BountyTakePhase.Complete;
					return;
				}
				if (task == BountyTask.Clearance && !HasMatchingClearance(Z, Data))
				{
					if (!taskMayStart)
					{
						Quarantine(Data,
							"A clearance takeover crossed an uncertain staking callback seam.");
						Data.TakePhase = (int)BountyTakePhase.Quarantined;
						return;
					}
					string failure;
					if (!KingdomMaterials.StakeClearance(System, Z, Data.X1, Data.Y1,
						Data.X2, Data.Y2, out failure))
					{
						if (!Data.StakeFailedAnnounced)
						{
							Data.StakeFailedAnnounced = true;
						System.Ledger.Note("{{r|" + KingdomPresentation.Rich(Data.PendingWorkerName)
								+ " would have taken the clearance notice, and could not: "
								+ failure + "}}");
						}
						// Clean refusal consumes this scheduled answer but never binds a worker.
						Data.PendingWorkerName = null;
						Data.TakePhase = (int)BountyTakePhase.Complete;
						return;
					}
					Data.StakeFailedAnnounced = false;
				}
				Data.TakePhase = (int)BountyTakePhase.TaskDone;
				phase = BountyTakePhase.TaskDone;
			}
			if (phase == BountyTakePhase.TaskDone)
			{
				Data.WorkerName = Data.PendingWorkerName;
				Data.WorkerResidentId = task == BountyTask.Manning
					? Data.PendingWorkerResidentId : 0;
				Data.TakenTick = Data.PendingAttemptTick;
				Data.DueTick = task == BountyTask.Manning ? 0L
					: KingdomBountyRules.WorkDueTick(Data.TakenTick,
						KingdomBountyRules.WorkDays(task, Data.Magnitude));
				if (!KingdomChronicle.RecordOnce(System, EventId(Data, "taken"),
					KingdomBountyRules.TakenChronicle(
						KingdomPresentation.Rich(Data.PendingWorkerName), task,
						Data.PendingVirtueIndex, Data.PendingTasteMatched)))
				{
					return;
				}
				Data.TakePhase = (int)BountyTakePhase.ChronicleDone;
				phase = BountyTakePhase.ChronicleDone;
			}
			if (phase == BountyTakePhase.ChronicleDone)
			{
				if (Data.TakeLedgerState == (int)BountySinkDisposition.None)
					Data.TakeLedgerState = (int)BountySinkDisposition.Pending;
				Data.TakePhase = (int)BountyTakePhase.LedgerIntent;
				DeliverLedger(System, ref Data.TakeLedgerState, "{{G|" + KingdomPresentation.Rich(Data.PendingWorkerName)
					+ " took the notice offering water to " + KingdomBountyRules.TaskName(task) + ".}}");
				Data.TakePhase = (int)BountyTakePhase.LedgerDone;
				phase = BountyTakePhase.LedgerDone;
			}
			else if (phase == BountyTakePhase.LedgerIntent)
			{
				if (Data.TakeLedgerState == (int)BountySinkDisposition.None)
					Data.TakeLedgerState = (int)BountySinkDisposition.Attempting;
				Data.TakeLedgerState = (int)KingdomBountyRules.RecoverUninspectable(
					(BountySinkDisposition)Data.TakeLedgerState);
				Data.TakePhase = (int)BountyTakePhase.LedgerDone;
				phase = BountyTakePhase.LedgerDone;
			}
			if (phase == BountyTakePhase.LedgerDone)
			{
				if (Data.TakeMessageState == (int)BountySinkDisposition.None)
					Data.TakeMessageState = (int)BountySinkDisposition.Pending;
				Data.TakePhase = (int)BountyTakePhase.MessageIntent;
				DeliverMessage(ref Data.TakeMessageState, "{{G|" + KingdomPresentation.Rich(Data.PendingWorkerName)
					+ " takes the posted notice.}}");
				Data.TakePhase = (int)BountyTakePhase.MessageDone;
				phase = BountyTakePhase.MessageDone;
			}
			else if (phase == BountyTakePhase.MessageIntent)
			{
				if (Data.TakeMessageState == (int)BountySinkDisposition.None)
					Data.TakeMessageState = (int)BountySinkDisposition.Attempting;
				Data.TakeMessageState = (int)KingdomBountyRules.RecoverUninspectable(
					(BountySinkDisposition)Data.TakeMessageState);
				Data.TakePhase = (int)BountyTakePhase.MessageDone;
				phase = BountyTakePhase.MessageDone;
			}
			if (phase == BountyTakePhase.MessageDone)
			{
				Describe(System, Z, Notice, Data);
				Data.TakePhase = (int)BountyTakePhase.Complete;
				KingdomLog.Log("bounty: taken by " + Data.WorkerName + " resident="
					+ Data.PendingWorkerResidentId + " task=" + KingdomBountyRules.TaskKey(task)
					+ " due=" + Data.DueTick);
			}
		}

		private static void CompleteTakeCursor(r_KingdomNotice Data)
		{
			if (!Data.PendingAttemptConsumed)
			{
				long next;
				if (Data.NextAttemptTick == Data.PendingAttemptTick)
				{
					ConsumeAttempt(Data, Data.PendingAttemptTick);
				}
				else if (KingdomBountyRules.TryAdvanceAttemptTick(Data.PendingAttemptTick, out next)
					&& Data.NextAttemptTick != next && !Data.AttemptScheduleExhausted)
				{
					Quarantine(Data, "The bound reader's schedule cursor no longer matches its event.");
					return;
				}
				Data.PendingAttemptConsumed = true;
			}
			Data.TakePhase = (int)BountyTakePhase.None;
			Data.PendingAttemptTick = 0L;
			Data.PendingWorkerName = null;
		}

		private static bool HasMatchingClearance(Zone Z, r_KingdomNotice Data)
		{
			if (Z == null) return false;
			KingdomSurvey survey = KingdomSurvey.ActiveFor(Z);
			IEnumerable<GameObject> clearances = survey != null
				? (IEnumerable<GameObject>)survey.Clearances : KingdomSurvey.ObjectsFor(Z);
			foreach (GameObject item in clearances)
			{
				r_KingdomClearance order = item.GetPart<r_KingdomClearance>();
				if (order != null && order.X1 == Data.X1 && order.Y1 == Data.Y1
					&& order.X2 == Data.X2 && order.Y2 == Data.Y2) return true;
			}
			return false;
		}

	}
}
