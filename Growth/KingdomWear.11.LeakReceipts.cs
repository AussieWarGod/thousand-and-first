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
		private static bool TryReadStrictTick(GameObject Work, string Property, out long Tick)
		{
			Tick = 0L;
			if (!GameObject.Validate(Work) || string.IsNullOrEmpty(Property)) return false;
			string text = Work.GetStringProperty(Property);
			if (string.IsNullOrEmpty(text)) return true;
			if (text.Length > 20) return false;
			return long.TryParse(text, global::System.Globalization.NumberStyles.None,
				global::System.Globalization.CultureInfo.InvariantCulture, out Tick)
				&& Tick >= 0L && Tick.ToString(
					global::System.Globalization.CultureInfo.InvariantCulture) == text;
		}

		private static string WearEventId(GameObject Work, string Kind, long Tick)
		{
			return KingdomWearRules.WorkStream(Work?.ID) + ":event:" + Kind + ":"
				+ Tick.ToString(global::System.Globalization.CultureInfo.InvariantCulture);
		}

		private static void QuarantineLeak(KingdomSystem System, GameObject Work,
			r_KingdomWear Wear, int Proved, string Reason)
		{
			if (Wear == null) return;
			Wear.LeakActualLost = (Proved > Wear.LeakWanted) ? Wear.LeakWanted
				: ((Proved > 0) ? Proved : 0);
			if (Wear.LeakActualLost > 0 && Wear.LeakToTick >= Wear.LastLeakTick)
			{
				Wear.LastLeakTick = Wear.LeakToTick;
				Wear.LeakClockInitialized = true;
			}
			Wear.LeakPhase = (int)KingdomWearLeakPhase.Quarantined;
			Wear.LifecycleQuarantined = true;
			Wear.QuarantineReason = string.IsNullOrEmpty(Reason)
				? "Its storage-loss receipt is physically ambiguous." : Reason;
			if (GameObject.Validate(Work) && Wear.ParentObject == Work
				&& ReferenceEquals(Work.GetPart<r_KingdomWear>(), Wear))
			{
				Work.SetIntProperty(SemanticPassPhaseProperty,
					(int)KingdomWearPassPhase.Quarantined);
				TellWearQuarantine(System, Work, Wear);
			}
		}

		private static void ClearLeakReceipt(r_KingdomWear Wear)
		{
			Wear.LeakIncidentId = null;
			Wear.LeakPhase = (int)KingdomWearLeakPhase.None;
			Wear.LeakKind = 0;
			Wear.LeakFromTick = 0L;
			Wear.LeakToTick = 0L;
			Wear.LeakBefore = 0;
			Wear.LeakAfter = 0;
			Wear.LeakWanted = 0;
			Wear.LeakActualLost = 0;
			Wear.LeakOwnerId = null;
			Wear.LeakZoneId = null;
			Wear.LeakCellX = 0;
			Wear.LeakCellY = 0;
			Wear.LeakCapacity = 0;
			Wear.LeakLine = null;
			Wear.LeakItemIds = null;
			Wear.LeakItemOriginalCounts = null;
			Wear.LeakItemAllocations = null;
		}

		/// <summary>
		/// Migrates the retired food-loss receipt in place. Its fields remain serializable for
		/// old-save compatibility, but no phase may resume and no pantry object is inspected or
		/// mutated. Completed legacy announcements on a larder are also silenced.
		/// </summary>
		internal static bool RetireFoodLeakReceipt(GameObject Work, r_KingdomWear Wear)
		{
			if (Wear == null) return false;
			bool retired = Wear.LeakKind == (int)KingdomWearRules.LeakKind.Food;
			if (retired)
			{
				ClearLeakReceipt(Wear);
				Wear.LeakLedgerState = (int)KingdomWearSinkDisposition.None;
				Wear.LeakMessageState = (int)KingdomWearSinkDisposition.None;
				Wear.LeakAnnounced = false;
			}
			else if ((KingdomWearLeakPhase)Wear.LeakPhase == KingdomWearLeakPhase.None
				&& GameObject.Validate(Work) && Work.GetIntProperty(LarderProperty) == 1)
			{
				Wear.LeakAnnounced = false;
			}
			return retired;
		}

		private static void QuarantineWear(KingdomSystem System, GameObject Work, string Reason)
		{
			if (!GameObject.Validate(Work)) return;
			r_KingdomWear wear = Work.GetPart<r_KingdomWear>();
			if (wear == null || wear.ParentObject != Work) return;
			wear.LifecycleQuarantined = true;
			wear.QuarantineReason = string.IsNullOrEmpty(Reason)
				? "Its wear receipt is physically ambiguous." : Reason;
			if ((KingdomWearIncidentPhase)wear.IncidentPhase != KingdomWearIncidentPhase.None)
			{
				wear.IncidentPhase = (int)KingdomWearIncidentPhase.Quarantined;
			}
			if ((KingdomWearLeakPhase)wear.LeakPhase != KingdomWearLeakPhase.None)
			{
				wear.LeakPhase = (int)KingdomWearLeakPhase.Quarantined;
			}
			Work.SetIntProperty(SemanticPassPhaseProperty, (int)KingdomWearPassPhase.Quarantined);
			TellWearQuarantine(System, Work, wear);
		}

		private static void TellWearQuarantine(KingdomSystem System, GameObject Work,
			r_KingdomWear Wear)
		{
			if (Wear == null) return;
			if (Wear.QuarantineTold)
			{
				if (Wear.QuarantineLedgerState == (int)KingdomWearSinkDisposition.None)
					Wear.QuarantineLedgerState = (int)KingdomWearSinkDisposition.Skipped;
				if (Wear.QuarantineMessageState == (int)KingdomWearSinkDisposition.None)
					Wear.QuarantineMessageState = (int)KingdomWearSinkDisposition.Skipped;
				return;
			}
			string name = GameObject.Validate(Work) ? DisplayName(Work) : "A damaged work";
			string line = name + " has an uncertain wear receipt and is quarantined; "
				+ (Wear.QuarantineReason ?? "no physical mutation will be guessed through it.");
			string eventId = WearEventId(Work, "quarantine", 0L);
			if (!KingdomChronicle.RecordOnce(System, eventId, line)) return;
			DeliverWearLedger(System, ref Wear.QuarantineLedgerState,
				"{{r|" + XRL.Language.Grammar.InitCap(line) + "}}");
			DeliverWearMessage(ref Wear.QuarantineMessageState,
				"{{r|" + XRL.Language.Grammar.InitCap(line) + "}}");
			Wear.QuarantineTold = KingdomWearRules.SinkSettled(
				(KingdomWearSinkDisposition)Wear.QuarantineLedgerState)
				&& KingdomWearRules.SinkSettled(
					(KingdomWearSinkDisposition)Wear.QuarantineMessageState);
		}

		private static bool DeliverWearMessage(ref int RawState, string Line)
		{
			KingdomWearSinkDisposition state = KingdomWearRules.RecoverUninspectable(
				(KingdomWearSinkDisposition)RawState);
			RawState = (int)state;
			if (KingdomWearRules.SinkSettled(state)) return true;
			if (string.IsNullOrEmpty(Line))
			{
				RawState = (int)KingdomWearSinkDisposition.Skipped;
				return true;
			}
			RawState = (int)KingdomWearSinkDisposition.Attempting;
			MessageQueue.AddPlayerMessage(Line);
			RawState = (int)KingdomWearSinkDisposition.Delivered;
			return true;
		}

		private static bool DeliverWearLedger(KingdomSystem System, ref int RawState,
			string Line)
		{
			KingdomWearSinkDisposition state = KingdomWearRules.RecoverUninspectable(
				(KingdomWearSinkDisposition)RawState);
			RawState = (int)state;
			if (KingdomWearRules.SinkSettled(state)) return true;
			if (System == null || string.IsNullOrEmpty(Line))
			{
				RawState = (int)KingdomWearSinkDisposition.Skipped;
				return true;
			}
			RawState = (int)KingdomWearSinkDisposition.Attempting;
			System.Ledger.Note(Line);
			RawState = (int)KingdomWearSinkDisposition.Delivered;
			return true;
		}

	}
}
