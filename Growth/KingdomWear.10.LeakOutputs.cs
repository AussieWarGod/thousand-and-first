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
		private static void ContinueLeakOutputs(KingdomSystem System, GameObject Work,
			r_KingdomWear Wear)
		{
			if (Wear == null || !GameObject.Validate(Work) || Wear.ParentObject != Work
				|| !ReferenceEquals(Work.GetPart<r_KingdomWear>(), Wear)
				|| !string.Equals(Wear.LeakOwnerId, Work.IDIfAssigned, StringComparison.Ordinal)
				|| Work.CurrentZone == null || Work.CurrentCell == null
				|| Work.CurrentCell.ParentZone != Work.CurrentZone
				|| !string.Equals(Wear.LeakZoneId, Work.CurrentZone.ZoneID,
					StringComparison.Ordinal)
				|| Work.CurrentCell.X != Wear.LeakCellX || Work.CurrentCell.Y != Wear.LeakCellY)
			{
				if (Wear != null)
				{
					Wear.LifecycleQuarantined = true;
					Wear.QuarantineReason =
						"A completed storage loss is no longer bound to its exact work, wear part, cell, and zone.";
					Wear.LeakPhase = (int)KingdomWearLeakPhase.Quarantined;
				}
				return;
			}
			KingdomWearLeakPhase phase = (KingdomWearLeakPhase)Wear.LeakPhase;
			if (Wear.LeakAnnounced && phase == KingdomWearLeakPhase.Mutated)
			{
				Wear.LeakLedgerState = (int)KingdomWearSinkDisposition.Skipped;
				Wear.LeakMessageState = (int)KingdomWearSinkDisposition.Skipped;
				Wear.LeakPhase = (int)KingdomWearLeakPhase.Complete;
				phase = KingdomWearLeakPhase.Complete;
			}
			if (phase == KingdomWearLeakPhase.Mutated)
			{
				if (!KingdomChronicle.RecordOnce(System, Wear.LeakIncidentId + ":chronicle",
					Wear.LeakLine)) return;
				Wear.LeakPhase = (int)KingdomWearLeakPhase.ChronicleDone;
				phase = KingdomWearLeakPhase.ChronicleDone;
			}
			if (phase == KingdomWearLeakPhase.ChronicleDone)
			{
				if (Wear.LeakLedgerState == (int)KingdomWearSinkDisposition.None)
					Wear.LeakLedgerState = (int)KingdomWearSinkDisposition.Pending;
				Wear.LeakPhase = (int)KingdomWearLeakPhase.LedgerIntent;
				DeliverWearLedger(System, ref Wear.LeakLedgerState,
					"{{r|" + XRL.Language.Grammar.InitCap(Wear.LeakLine) + "}}");
				Wear.LeakPhase = (int)KingdomWearLeakPhase.LedgerDone;
				phase = KingdomWearLeakPhase.LedgerDone;
			}
			else if (phase == KingdomWearLeakPhase.LedgerIntent)
			{
				if (Wear.LeakLedgerState == (int)KingdomWearSinkDisposition.None)
					Wear.LeakLedgerState = (int)KingdomWearSinkDisposition.Attempting;
				Wear.LeakLedgerState = (int)KingdomWearRules.RecoverUninspectable(
					(KingdomWearSinkDisposition)Wear.LeakLedgerState);
				Wear.LeakPhase = (int)KingdomWearLeakPhase.LedgerDone;
				phase = KingdomWearLeakPhase.LedgerDone;
			}
			if (phase == KingdomWearLeakPhase.LedgerDone)
			{
				if (Wear.LeakMessageState == (int)KingdomWearSinkDisposition.None)
					Wear.LeakMessageState = (int)KingdomWearSinkDisposition.Pending;
				Wear.LeakPhase = (int)KingdomWearLeakPhase.MessageIntent;
				DeliverWearMessage(ref Wear.LeakMessageState,
					"{{r|" + XRL.Language.Grammar.InitCap(Wear.LeakLine) + "}}");
				Wear.LeakPhase = (int)KingdomWearLeakPhase.MessageDone;
				phase = KingdomWearLeakPhase.MessageDone;
			}
			else if (phase == KingdomWearLeakPhase.MessageIntent)
			{
				if (Wear.LeakMessageState == (int)KingdomWearSinkDisposition.None)
					Wear.LeakMessageState = (int)KingdomWearSinkDisposition.Attempting;
				Wear.LeakMessageState = (int)KingdomWearRules.RecoverUninspectable(
					(KingdomWearSinkDisposition)Wear.LeakMessageState);
				Wear.LeakPhase = (int)KingdomWearLeakPhase.MessageDone;
				phase = KingdomWearLeakPhase.MessageDone;
			}
			if (phase == KingdomWearLeakPhase.MessageDone)
			{
				Wear.LeakAnnounced = true;
				Wear.LeakPhase = (int)KingdomWearLeakPhase.Complete;
				phase = KingdomWearLeakPhase.Complete;
				KingdomLog.Log("wear: leak " + Work.Blueprint + " kind=" + Wear.LeakKind
					+ " lost=" + Wear.LeakActualLost + " incident=" + Wear.LeakIncidentId);
			}
			if (phase == KingdomWearLeakPhase.Complete) ClearLeakReceipt(Wear);
		}

	}
}
