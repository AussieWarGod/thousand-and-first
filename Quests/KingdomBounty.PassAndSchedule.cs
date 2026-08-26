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
		// ==================================================================================
		// The pass
		// ==================================================================================

		/// <summary>
		/// Resolves every notice standing on this ground through the current absolute schedule: who read them, who
		/// took them, what got finished, and what got paid.
		/// <para>
		/// Call from the settlement's canonical attended pass <b>after</b> growth and
		/// improvement, and for the same reason those two are ordered against each other: this
		/// pays out of what the stores have left once the settlement's own upkeep and arrivals are
		/// done with them, and it mans works out of the idleness growth has just finished
		/// measuring.
		/// </para>
		/// </summary>
		/// <param name="System">The realm. Does nothing when unfounded.</param>
		/// <param name="Z">The activated ground. Does nothing when it is not the kingdom's.</param>
		/// <param name="Survey">This pass's shared survey, drawn from and written to.</param>
		public static void OnSettlementPass(KingdomSystem System, Zone Z, KingdomSurvey Survey)
		{
			if (!Enabled || System == null || !System.Founded || Z == null || Survey == null || !System.ClaimedZones.Contains(Z.ZoneID))
			{
				return;
			}
			List<GameObject> notices = new List<GameObject>(Survey.Notices);
			for (int i = 0; i < notices.Count; i++)
			{
				GameObject notice = notices[i];
				r_KingdomNotice data = notice.GetPart<r_KingdomNotice>();
				if (data == null || !GameObject.Validate(notice))
				{
					continue;
				}
				Resolve(System, Z, Survey, notice, data);
			}
		}

		private static void Resolve(KingdomSystem System, Zone Z, KingdomSurvey Survey, GameObject Notice, r_KingdomNotice Data)
		{
			EnsureLifecycleIdentity(Notice, Data);
			if ((BountySinkDisposition)Data.StakeCleanupState == BountySinkDisposition.Attempting)
			{
				Data.StakeCleanupState = (int)BountySinkDisposition.Lost;
				Quarantine(Data, "Stake cleanup was interrupted; its destructive callback was not repeated.");
			}
			if ((BountyWithdrawPhase)Data.WithdrawPhase != BountyWithdrawPhase.None)
			{
				ContinueWithdraw(System, Z, Notice, Data);
				if (Data.LifecycleQuarantined) TellQuarantine(System, Data);
				return;
			}
			if (Data.LifecycleQuarantined)
			{
				TellQuarantine(System, Data);
				return;
			}
			if ((BountyPostPhase)Data.PostPhase != BountyPostPhase.None
				&& (BountyPostPhase)Data.PostPhase != BountyPostPhase.Complete)
			{
				ContinuePost(System, Z, Notice, Data);
				if (Data.LifecycleQuarantined) TellQuarantine(System, Data);
				return;
			}
			if ((BountyTakePhase)Data.TakePhase != BountyTakePhase.None
				&& (BountyTakePhase)Data.TakePhase != BountyTakePhase.Complete)
			{
				ContinueTake(System, Z, Notice, Data);
			}
			if ((BountyTakePhase)Data.TakePhase == BountyTakePhase.Complete)
			{
				CompleteTakeCursor(Data);
			}
			if (Data.LifecycleQuarantined
				|| (BountyTakePhase)Data.TakePhase != BountyTakePhase.None) return;
			if (Data.Done)
			{
				if (Data.CompletionPhase > 0 && Data.CompletionPhase < 4)
				{
					ContinueFinish(System, Z, Survey, Notice, Data);
					return;
				}
				Settle(System, Z, Survey, Notice, Data);
				return;
			}
			if (!string.IsNullOrEmpty(Data.WorkerName))
			{
				Work(System, Z, Survey, Notice, Data);
				return;
			}
			EnsureAttemptSchedule(Notice, Data, The.Game.TimeTicks);
			if (Data.AttemptScheduleExhausted || Data.Passes >= KingdomBountyRules.MaxPasses)
			{
				Data.AttemptScheduleExhausted = true;
				return;
			}
			BountyBlock block = Blocking(System, Z, Survey, Data);
			if (block != BountyBlock.None)
			{
				Announce(System, Data, block);
				return;
			}
			Announce(System, Data, BountyBlock.None);
			BountyTask task = (BountyTask)Data.TaskCode;
			long latestTick;
			long skipped;
			if (!KingdomBountyRules.TryLatestDueAttempt(The.Game.TimeTicks, Data.NextAttemptTick,
				Data.AttemptScheduleExhausted, out latestTick, out skipped))
			{
				return;
			}
			if (!ConsumeMissedAttempts(Data, latestTick, skipped))
			{
				return;
			}
			int due = 1;
			int presented = 0;
			int omittedRefusals = 0;
			string settlementId = KingdomChronicle.SettlementId(System);
			Simulation.City.KingdomCityState residentState;
			Simulation.City.KingdomResidentRollProjection roll;
			List<string> residentNames = Simulation.City.KingdomResidents.TryRoll(System,
				out residentState, out roll) ? roll.Names : new List<string>();
			for (int i = 0; i < due && string.IsNullOrEmpty(Data.WorkerName)
				&& !Data.AttemptScheduleExhausted
				&& Data.Passes < KingdomBountyRules.MaxPasses; i++)
			{
				long scheduledTick = Data.NextAttemptTick;
				KingdomBountyRules.BountyAttempt attempt = KingdomBountyRules.ResolveScheduled(
					settlementId, Data.EventStreamId, scheduledTick, residentNames, task, Data.Price);
				if (!attempt.Determined)
				{
					// Kernel failure has no outcome. Keep this exact scheduled event at the cursor;
					// a later pass retries it instead of silently burning it.
					break;
				}
				if (attempt.Outcome == BountyOutcome.Taken)
				{
					bool taken = Take(System, Z, Notice, Data, task, attempt, scheduledTick);
					if ((BountyTakePhase)Data.TakePhase == BountyTakePhase.Complete) CompleteTakeCursor(Data);
					if (taken)
					{
						break;
					}
					continue;
				}
				ConsumeAttempt(Data, scheduledTick);
				if (attempt.Outcome != BountyOutcome.Refused)
				{
					continue;
				}
				if (!Data.RefusalTold)
				{
					if (KingdomChronicle.RecordOnce(System, EventId(Data, "refused"),
						KingdomBountyRules.RefusedChronicle(attempt.Name, task, attempt.FlawIndex)))
					{
						Data.RefusalTold = true;
					}
				}
				if (presented < KingdomBountyRules.MaxAttemptPresentations)
				{
					presented++;
					System.Ledger.Note("{{K|" + attempt.Name + " read the notice offering water to " + KingdomBountyRules.TaskName(task) + ", and left it standing.}}");
					KingdomLog.Log("bounty: refused by " + attempt.Name + " task=" + KingdomBountyRules.TaskKey(task) + " scheduled=" + scheduledTick);
				}
				else
				{
					omittedRefusals++;
				}
			}
			if (omittedRefusals > 0)
			{
				System.Ledger.Note("{{K|" + omittedRefusals + ((omittedRefusals == 1)
					? " other settler read the notice and left it standing.}}"
					: " other settlers read the notice and left it standing.}}"));
			}
		}

		private static void EnsureAttemptSchedule(GameObject Notice, r_KingdomNotice Data, long NowTick)
		{
			if (Data.ScheduleVersion == 2)
			{
				if (string.IsNullOrEmpty(Data.EventStreamId))
				{
					Data.EventStreamId = KingdomBountyRules.NoticeEventStream((Notice != null) ? Notice.ID : null);
				}
				if (!Data.AttemptScheduleExhausted && Data.NextAttemptTick <= 0L)
				{
					Data.AttemptScheduleExhausted = !KingdomBountyRules.TryAttemptAfter(NowTick,
						Data.PostedTick, out Data.NextAttemptTick);
				}
				return;
			}
			// Legacy Passes are already-consumed outcomes. Start their absolute lane strictly after
			// migration time, retaining Passes only as the audit count; loading cannot reroll a reader.
			Data.EventStreamId = KingdomBountyRules.NoticeEventStream((Notice != null) ? Notice.ID : null);
			Data.AttemptScheduleExhausted = !KingdomBountyRules.TryAttemptAfter(NowTick,
				Data.PostedTick, out Data.NextAttemptTick);
			Data.ScheduleVersion = 2;
		}

		private static void EnsureLifecycleIdentity(GameObject Notice, r_KingdomNotice Data)
		{
			if (string.IsNullOrEmpty(Data.LifecycleId))
			{
				Data.LifecycleId = KingdomBountyRules.NoticeEventId(
					GameObject.Validate(Notice) ? Notice.ID : null);
			}
			else if (!KingdomBountyRules.IsNoticeEventId(Data.LifecycleId))
			{
				Quarantine(Data, "The notice's stable event identity is malformed.");
			}
		}

	}
}
