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
		private static void ResetPaymentReceipt(r_KingdomNotice Data)
		{
			Data.PaymentPhase = (int)BountyPaymentPhase.None;
			Data.PaymentAmount = 0;
			Data.PaymentPaidBefore = Data.Paid;
			Data.PaymentProved = 0;
			Data.PaymentZoneId = null;
			Data.PaymentVesselIds = null;
			Data.PaymentOriginalVolumes = null;
			Data.PaymentMaxVolumes = null;
			Data.PaymentAllocations = null;
		}

		private static void ContinueTerminal(KingdomSystem System, GameObject Notice,
			r_KingdomNotice Data)
		{
			BountyTerminalPhase phase = (BountyTerminalPhase)Data.TerminalPhase;
			if (phase == BountyTerminalPhase.None)
			{
				if (!KingdomChronicle.RecordOnce(System, EventId(Data, "paid"),
					KingdomBountyRules.PaidChronicle(
						KingdomPresentation.Rich(Data.WorkerName),
						KingdomPresentation.Rich(System.SeatName),
						(BountyTask)Data.TaskCode, Data.Paid))) return;
				Data.TerminalPhase = (int)BountyTerminalPhase.ChronicleDone;
				phase = BountyTerminalPhase.ChronicleDone;
			}
			if (phase == BountyTerminalPhase.ChronicleDone)
			{
				if (Data.TerminalLedgerState == (int)BountySinkDisposition.None)
					Data.TerminalLedgerState = (int)BountySinkDisposition.Pending;
				Data.TerminalPhase = (int)BountyTerminalPhase.LedgerIntent;
				DeliverLedger(System, ref Data.TerminalLedgerState,
					"{{G|" + KingdomPresentation.Rich(Data.WorkerName) + " was paid " + Data.Paid
					+ ((Data.Paid == 1) ? " dram" : " drams") + " off the notice board.}}");
				Data.TerminalPhase = (int)BountyTerminalPhase.LedgerDone;
				phase = BountyTerminalPhase.LedgerDone;
			}
			else if (phase == BountyTerminalPhase.LedgerIntent)
			{
				if (Data.TerminalLedgerState == (int)BountySinkDisposition.None)
					Data.TerminalLedgerState = (int)BountySinkDisposition.Attempting;
				Data.TerminalLedgerState = (int)KingdomBountyRules.RecoverUninspectable(
					(BountySinkDisposition)Data.TerminalLedgerState);
				Data.TerminalPhase = (int)BountyTerminalPhase.LedgerDone;
				phase = BountyTerminalPhase.LedgerDone;
			}
			if (phase == BountyTerminalPhase.LedgerDone)
			{
				if (Data.TerminalMessageState == (int)BountySinkDisposition.None)
					Data.TerminalMessageState = (int)BountySinkDisposition.Pending;
				Data.TerminalPhase = (int)BountyTerminalPhase.MessageIntent;
				DeliverMessage(ref Data.TerminalMessageState,
					"{{G|The notice is claimed and paid.}} "
					+ Data.Paid + ((Data.Paid == 1) ? " dram goes" : " drams go")
					+ " to " + KingdomPresentation.Rich(Data.WorkerName) + ".");
				Data.TerminalPhase = (int)BountyTerminalPhase.MessageDone;
				phase = BountyTerminalPhase.MessageDone;
			}
			else if (phase == BountyTerminalPhase.MessageIntent)
			{
				if (Data.TerminalMessageState == (int)BountySinkDisposition.None)
					Data.TerminalMessageState = (int)BountySinkDisposition.Attempting;
				Data.TerminalMessageState = (int)KingdomBountyRules.RecoverUninspectable(
					(BountySinkDisposition)Data.TerminalMessageState);
				Data.TerminalPhase = (int)BountyTerminalPhase.MessageDone;
				phase = BountyTerminalPhase.MessageDone;
			}
			if (phase == BountyTerminalPhase.MessageDone)
			{
				CleanupFrame cleanup;
				if (!TryCaptureCleanup(Notice, Data, out cleanup))
				{
					Data.TerminalPhase = (int)BountyTerminalPhase.CleanupLost;
					Quarantine(Data,
						"Paid-notice cleanup could not capture its exact notice and data-part identity.");
					return;
				}
				Data.TerminalPhase = (int)BountyTerminalPhase.CleanupAttempting;
				KingdomLog.Log("bounty: paid " + Data.Paid + " to " + Data.WorkerName
					+ " task=" + KingdomBountyRules.TaskKey((BountyTask)Data.TaskCode));
				InvokeCleanupOnce(Notice, false);
				if (!CleanupFinalized(cleanup))
				{
					Data.TerminalPhase = (int)BountyTerminalPhase.CleanupLost;
					Quarantine(Data, "Paid-notice cleanup was vetoed or changed; its destructive callback was not repeated.");
				}
			}
			else if (phase == BountyTerminalPhase.CleanupAttempting)
			{
				Data.TerminalPhase = (int)BountyTerminalPhase.CleanupLost;
				Quarantine(Data, "Paid-notice cleanup was interrupted; its destructive callback was not repeated.");
			}
		}

		// ==================================================================================
		// Saying why, once
		// ==================================================================================

		private static BountyBlock Blocking(KingdomSystem System, Zone Z, KingdomSurvey Survey, r_KingdomNotice Data)
		{
			if (Simulation.City.KingdomResidents.OnRollCount(System) == 0)
			{
				return BountyBlock.NobodyToTry;
			}
			switch ((BountyTask)Data.TaskCode)
			{
			case BountyTask.Clearance:
			{
				KingdomMaterials.ClearanceAssessment assessment = KingdomMaterials.Assess(System, Z, Data.X1, Data.Y1, Data.X2, Data.Y2);
				return (assessment.Valid && assessment.Standing <= 0) ? BountyBlock.NothingStanding : BountyBlock.None;
			}
			case BountyTask.Fetch:
			{
				GameObject pile = FindPile(Z, null, Data);
				if (pile == null || MaterialUnits(pile) <= 0)
				{
					return BountyBlock.PileEmpty;
				}
				return KingdomMaterials.Stock(Z).None ? BountyBlock.NowhereToCarry : BountyBlock.None;
			}
			case BountyTask.Manning:
				return ManningBlock(System, Survey, Data);
			case BountyTask.Scouting:
				return (Frontier(System).Count == 0) ? BountyBlock.NoFrontier : BountyBlock.None;
			default:
				return BountyBlock.None;
			}
		}

		/// <summary>
		/// Says why once, and only once, per stall. A permanent reason is never repeated even if
		/// the notice is looked at again; an ordinary block is re-armed the moment it lifts, so a
		/// stall that comes back is spoken about again rather than swallowed. STANDARDS 7b.
		/// </summary>
		private static void Announce(KingdomSystem System, r_KingdomNotice Data, BountyBlock Block)
		{
			if (Data.AnnouncedBlock == (int)Block)
			{
				return;
			}
			if (Block == BountyBlock.None)
			{
				if (!KingdomBountyRules.IsPermanent((BountyBlock)Data.AnnouncedBlock))
				{
					Data.AnnouncedBlock = 0;
				}
				return;
			}
			if (KingdomBountyRules.IsPermanent((BountyBlock)Data.AnnouncedBlock))
			{
				return;
			}
			Data.AnnouncedBlock = (int)Block;
			string reason = KingdomBountyRules.BlockReason(Block, (BountyTask)Data.TaskCode,
				KingdomPresentation.Rich(System.SeatName));
			if (reason != null)
			{
				System.Ledger.Note("{{r|" + reason + "}}");
				MessageQueue.AddPlayerMessage("{{r|" + reason + "}}");
			}
			KingdomLog.Log("bounty: blocked " + Block + " task=" + KingdomBountyRules.TaskKey((BountyTask)Data.TaskCode));
		}

		// ==================================================================================
		// Reading the ground
		// ==================================================================================

	}
}
