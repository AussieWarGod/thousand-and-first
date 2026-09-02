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
	}
}
