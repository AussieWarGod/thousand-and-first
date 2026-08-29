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
		private static void ContinuePost(KingdomSystem System, Zone Z, GameObject Notice,
			r_KingdomNotice Data)
		{
			BountyPostPhase phase = (BountyPostPhase)Data.PostPhase;
			if (phase == BountyPostPhase.None || phase == BountyPostPhase.Complete) return;
			Cell expectedCell = BoundCell(Z, Data.PostZoneId, Data.PostCellX, Data.PostCellY);
			if (!NoticeBindingExact(Notice, Data, Z, expectedCell))
			{
				Quarantine(Data, "The posted notice no longer has its exact object, part, cell, and zone binding.");
				return;
			}
			if (!string.IsNullOrEmpty(Data.PileId))
			{
				Cell pileCell = BoundCell(Z, Data.PostZoneId,
					Data.PostPileCellX, Data.PostPileCellY);
				GameObject pile = (Z == null) ? null : Z.FindObjectByID(Data.PileId);
				if (!PileBindingExact(pile, Z, pileCell)
					|| pile.GetStringProperty(FetchMarkProperty) != Notice.IDIfAssigned)
				{
					Quarantine(Data, "The posted fetch notice no longer owns its exact pile mark.");
					return;
				}
			}
			if (phase == BountyPostPhase.Bound)
			{
				if (!KingdomChronicle.RecordOnce(System, EventId(Data, "posted"),
					Data.PostChronicleLine)) return;
				Data.PostPhase = (int)BountyPostPhase.ChronicleDone;
				phase = BountyPostPhase.ChronicleDone;
			}
			if (phase == BountyPostPhase.ChronicleDone)
			{
				if (!DeliverMessage(ref Data.PostMessageState, Data.PostMessageLine)) return;
				Data.PostPhase = (int)BountyPostPhase.MessageSettled;
				phase = BountyPostPhase.MessageSettled;
			}
			if (phase == BountyPostPhase.MessageSettled)
			{
				Data.PostPhase = (int)BountyPostPhase.Complete;
			}
		}

		private static Cell BoundCell(Zone Z, string ZoneId, int X, int Y)
		{
			if (Z == null || string.IsNullOrEmpty(ZoneId) || Z.ZoneID != ZoneId
				|| X < 0 || Y < 0) return null;
			return Z.GetCell(X, Y);
		}

		private static bool NoticeBindingExact(GameObject Notice, r_KingdomNotice Data,
			Zone Z, Cell Cell)
		{
			return GameObject.Validate(Notice) && Data != null && Z != null && Cell != null
				&& Notice.CurrentZone == Z && Notice.CurrentCell == Cell && Cell.ParentZone == Z
				&& ReferenceEquals(Data.ParentObject, Notice)
				&& ReferenceEquals(Notice.GetPart<r_KingdomNotice>(), Data);
		}

		private static bool PileBindingExact(GameObject Pile, Zone Z, Cell Cell)
		{
			return GameObject.Validate(Pile) && Z != null && Cell != null
				&& Pile.CurrentZone == Z && Pile.CurrentCell == Cell && Cell.ParentZone == Z;
		}

		private static bool ClearBoundFetchMark(Zone Z, GameObject Notice,
			r_KingdomNotice Data, string PileId)
		{
			Cell noticeCell = BoundCell(Z, Data.WithdrawZoneId,
				Data.WithdrawCellX, Data.WithdrawCellY);
			if (!NoticeBindingExact(Notice, Data, Z, noticeCell)) return false;
			if (string.IsNullOrEmpty(PileId))
			{
				Data.PileId = null;
				return true;
			}
			Cell pileCell = BoundCell(Z, Data.WithdrawZoneId,
				Data.WithdrawPileCellX, Data.WithdrawPileCellY);
			GameObject pile = (Z == null) ? null : Z.FindObjectByID(PileId);
			if (!PileBindingExact(pile, Z, pileCell)
				|| pile.GetStringProperty(FetchMarkProperty) != Notice.IDIfAssigned) return false;
			pile.RemoveStringProperty(FetchMarkProperty);
			if (!PileBindingExact(pile, Z, pileCell)
				|| !string.IsNullOrEmpty(pile.GetStringProperty(FetchMarkProperty))
				|| !NoticeBindingExact(Notice, Data, Z, noticeCell)) return false;
			Data.PileId = null;
			return true;
		}

		private static bool DeliverMessage(ref int RawState, string Line)
		{
			BountySinkDisposition state = KingdomBountyRules.RecoverUninspectable(
				(BountySinkDisposition)RawState);
			RawState = (int)state;
			if (KingdomBountyRules.SinkSettled(state)) return true;
			if (string.IsNullOrEmpty(Line))
			{
				RawState = (int)BountySinkDisposition.Skipped;
				return true;
			}
			RawState = (int)BountySinkDisposition.Attempting;
			MessageQueue.AddPlayerMessage(Line);
			RawState = (int)BountySinkDisposition.Delivered;
			return true;
		}

		private static bool DeliverLedger(KingdomSystem System, ref int RawState, string Line)
		{
			BountySinkDisposition state = KingdomBountyRules.RecoverUninspectable(
				(BountySinkDisposition)RawState);
			RawState = (int)state;
			if (KingdomBountyRules.SinkSettled(state)) return true;
			if (System == null || string.IsNullOrEmpty(Line))
			{
				RawState = (int)BountySinkDisposition.Skipped;
				return true;
			}
			RawState = (int)BountySinkDisposition.Attempting;
			System.Ledger.Note(Line);
			RawState = (int)BountySinkDisposition.Delivered;
			return true;
		}

		private sealed class CleanupFrame
		{
			internal GameObject Notice;
			internal string NoticeId;
			internal r_KingdomNotice Data;
			internal Zone Zone;
			internal Cell Cell;
		}

		private static bool TryCaptureCleanup(GameObject Notice, r_KingdomNotice Data,
			out CleanupFrame Frame)
		{
			Frame = null;
			if (!GameObject.Validate(Notice) || string.IsNullOrEmpty(Notice.IDIfAssigned) || Data == null
				|| Data.ParentObject != Notice
				|| !ReferenceEquals(Notice.GetPart<r_KingdomNotice>(), Data)) return false;
			Zone zone = Notice.CurrentZone;
			Cell cell = Notice.CurrentCell;
			if ((zone == null) != (cell == null)
				|| (cell != null && cell.ParentZone != zone)) return false;
			Frame = new CleanupFrame
			{
				Notice = Notice,
				NoticeId = Notice.IDIfAssigned,
				Data = Data,
				Zone = zone,
				Cell = cell
			};
			return true;
		}

		private static bool CleanupFinalized(CleanupFrame Frame)
		{
			if (Frame == null || GameObject.Validate(Frame.Notice)) return false;
			GameObject sameId = GameObject.FindByID(Frame.NoticeId);
			return !GameObject.Validate(sameId);
		}

		/// <summary>The only destructive bounty call site. Attempting recovery never enters it.</summary>
		private static bool InvokeCleanupOnce(GameObject Target, bool Silent)
		{
			if (Target == null || !GameObject.Validate(Target)) return true;
			Zone zone = Target.CurrentZone;
			try { return Target.Obliterate(null, Silent); }
			finally { KingdomSurvey.ObserveCurrentTopologyInActive(zone, Target); }
		}

		private static string EventId(r_KingdomNotice Data, string Suffix)
		{
			return (Data?.LifecycleId ?? "taf:bounty:event:v1:unknown") + ":" + Suffix;
		}

		private static void Quarantine(r_KingdomNotice Data, string Reason)
		{
			if (Data == null) return;
			Data.LifecycleQuarantined = true;
			if (string.IsNullOrEmpty(Data.QuarantineReason)) Data.QuarantineReason = Reason;
		}

		private static void TellQuarantine(KingdomSystem System, r_KingdomNotice Data)
		{
			if (System == null || Data == null) return;
			if (Data.QuarantineTold
				&& Data.QuarantineLedgerState == (int)BountySinkDisposition.None
				&& Data.QuarantineMessageState == (int)BountySinkDisposition.None)
			{
				Data.QuarantineLedgerState = (int)BountySinkDisposition.Skipped;
				Data.QuarantineMessageState = (int)BountySinkDisposition.Skipped;
			}
			if (Data.QuarantineTold) return;
			string reason = string.IsNullOrEmpty(Data.QuarantineReason)
				? "A notice receipt is uncertain. It is quarantined; no task, transfer, or payment will be repeated."
				: Data.QuarantineReason;
			DeliverLedger(System, ref Data.QuarantineLedgerState, "{{r|" + reason + "}}");
			DeliverMessage(ref Data.QuarantineMessageState, "{{r|" + reason + "}}");
			Data.QuarantineTold = KingdomBountyRules.SinkSettled(
				(BountySinkDisposition)Data.QuarantineLedgerState)
				&& KingdomBountyRules.SinkSettled(
					(BountySinkDisposition)Data.QuarantineMessageState);
			KingdomLog.Log("bounty: quarantined " + (Data.LifecycleId ?? "unknown")
				+ " reason=" + reason);
		}

		/// <summary>Consumes opportunities nobody could have taken while unattended, without drawing
		/// an outcome from a later roster. False means the notice exhausted its bounded lane.</summary>
		private static bool ConsumeMissedAttempts(r_KingdomNotice Data, long LatestTick, long Skipped)
		{
			if (Skipped <= 0L)
			{
				Data.NextAttemptTick = LatestTick;
				return true;
			}
			long room = (long)KingdomBountyRules.MaxPasses - Data.Passes;
			if (Skipped >= room)
			{
				Data.Passes = KingdomBountyRules.MaxPasses;
				Data.AttemptScheduleExhausted = true;
				Data.NextAttemptTick = 0L;
				return false;
			}
			Data.Passes += (int)Skipped;
			Data.NextAttemptTick = LatestTick;
			KingdomLog.Log("bounty: skipped " + Skipped
				+ " unattended notice opportunities; latest=" + LatestTick);
			return true;
		}

		private static void ConsumeAttempt(r_KingdomNotice Data, long ScheduledTick)
		{
			if (Data.Passes < KingdomBountyRules.MaxPasses)
			{
				Data.Passes++;
			}
			long next;
			if (Data.Passes >= KingdomBountyRules.MaxPasses
				|| !KingdomBountyRules.TryAdvanceAttemptTick(ScheduledTick, out next))
			{
				Data.AttemptScheduleExhausted = true;
				Data.NextAttemptTick = 0L;
				return;
			}
			Data.NextAttemptTick = next;
		}

	}
}
