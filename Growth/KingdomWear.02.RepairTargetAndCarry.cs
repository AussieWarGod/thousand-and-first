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
		private sealed class RepairTargetFrame
		{
			internal GameObject Work;
			internal string Id;
			internal Zone Zone;
			internal Cell Cell;
			internal r_KingdomWear WearPart;
			internal int Wear;
			internal int LastCause;
			internal bool Held;
			internal int Effort;
			internal long LastLeakTick;
			internal bool LeakInitialized;
			internal bool LeakAnnounced;
			internal bool Quarantined;
			internal string Receipt;
		}

		private static bool TryCaptureRepairTarget(GameObject Work, r_KingdomWear Wear,
			out RepairTargetFrame Frame)
		{
			Frame = null;
			if (!GameObject.Validate(Work) || Work.CurrentZone == null || Work.CurrentCell == null
				|| Work.CurrentCell.ParentZone != Work.CurrentZone || Wear == null
				|| Wear.ParentObject != Work
				|| !ReferenceEquals(Work.GetPart<r_KingdomWear>(), Wear)) return false;
			Frame = new RepairTargetFrame
			{
				Work = Work,
				Id = Work.ID,
				Zone = Work.CurrentZone,
				Cell = Work.CurrentCell,
				WearPart = Wear,
				Wear = Wear.Wear,
				LastCause = Wear.LastCause,
				Held = Wear.Held,
				Effort = Wear.RepairEffortLeft,
				LastLeakTick = Wear.LastLeakTick,
				LeakInitialized = Wear.LeakClockInitialized,
				LeakAnnounced = Wear.LeakAnnounced,
				Quarantined = Wear.LifecycleQuarantined,
				Receipt = Work.GetStringProperty(KingdomConstruction.ReceiptProperty)
			};
			return true;
		}

		private static bool RepairTargetExact(RepairTargetFrame Frame, string ExpectedReceipt)
		{
			return Frame != null && GameObject.Validate(Frame.Work) && Frame.Work.ID == Frame.Id
				&& Frame.Work.CurrentZone == Frame.Zone && Frame.Work.CurrentCell == Frame.Cell
				&& Frame.Cell != null && Frame.Cell.ParentZone == Frame.Zone
				&& Frame.WearPart != null && Frame.WearPart.ParentObject == Frame.Work
				&& ReferenceEquals(Frame.Work.GetPart<r_KingdomWear>(), Frame.WearPart)
				&& Frame.WearPart.Wear == Frame.Wear
				&& Frame.WearPart.LastCause == Frame.LastCause
				&& Frame.WearPart.Held == Frame.Held
				&& Frame.WearPart.RepairEffortLeft == Frame.Effort
				&& Frame.WearPart.LastLeakTick == Frame.LastLeakTick
				&& Frame.WearPart.LeakClockInitialized == Frame.LeakInitialized
				&& Frame.WearPart.LeakAnnounced == Frame.LeakAnnounced
				&& Frame.WearPart.LifecycleQuarantined == Frame.Quarantined
				&& string.Equals(Frame.Work.GetStringProperty(
					KingdomConstruction.ReceiptProperty), ExpectedReceipt,
					StringComparison.Ordinal);
		}

		private static string RepairPayload(int Wear, bool Finishing)
		{
			return "v1|" + Wear.ToString(global::System.Globalization.CultureInfo.InvariantCulture)
				+ "|" + (Finishing ? "1" : "0");
		}

		private static bool TryRepairPayload(string Payload, out int Wear, out bool Finishing)
		{
			return KingdomWearRules.TryRepairPayload(Payload, out Wear, out Finishing);
		}

		public static bool Enabled => Options.GetOption("r_TAF_OptionWear") != "No";

		/// <summary>Refuses a handover while a wear mutation has identity bound to this object.</summary>
		public static bool CanCarryStableState(GameObject Source, out string Failure)
		{
			Failure = null;
			r_KingdomWear wear = GameObject.Validate(Source)
				? Source.GetPart<r_KingdomWear>() : null;
			if (wear == null) return true;
			if (wear.LifecycleQuarantined
				|| (KingdomWearIncidentPhase)wear.IncidentPhase != KingdomWearIncidentPhase.None
				|| (KingdomWearLeakPhase)wear.LeakPhase != KingdomWearLeakPhase.None
				|| wear.RepairEffortLeft != 0)
			{
				Failure = "That work has a wear, leak, or repair receipt in hand; settle it before changing the plan.";
				return false;
			}
			return true;
		}

		/// <summary>Carries stable founder-visible wear state across an in-place handover.</summary>
		public static bool TryCarryStableState(GameObject Source, GameObject Target)
		{
			if (!GameObject.Validate(Source) || !GameObject.Validate(Target)
				|| !CanCarryStableState(Source, out _)) return false;
			r_KingdomWear before = Source.GetPart<r_KingdomWear>();
			if (before == null) return Target.GetPart<r_KingdomWear>() == null;
			r_KingdomWear after = Target.RequirePart<r_KingdomWear>();
			after.Wear = before.Wear;
			after.LastCause = before.LastCause;
			after.Held = before.Held;
			after.RepairEffortLeft = before.RepairEffortLeft;
			after.LastLeakTick = before.LastLeakTick;
			after.LeakAnnounced = before.LeakAnnounced;
			after.AnnouncedBlock = before.AnnouncedBlock;
			after.LastCompletedIncidentId = before.LastCompletedIncidentId;
			after.LeakClockInitialized = before.LeakClockInitialized;
			return SameStableState(Source, Target);
		}

		public static bool SameStableState(GameObject Source, GameObject Target)
		{
			r_KingdomWear before = GameObject.Validate(Source)
				? Source.GetPart<r_KingdomWear>() : null;
			r_KingdomWear after = GameObject.Validate(Target)
				? Target.GetPart<r_KingdomWear>() : null;
			if (before == null) return after == null;
			return after != null && before.Wear == after.Wear
				&& before.LastCause == after.LastCause && before.Held == after.Held
				&& before.RepairEffortLeft == after.RepairEffortLeft
				&& before.LastLeakTick == after.LastLeakTick
				&& before.LeakAnnounced == after.LeakAnnounced
				&& before.AnnouncedBlock == after.AnnouncedBlock
				&& before.LastCompletedIncidentId == after.LastCompletedIncidentId
				&& before.LeakClockInitialized == after.LeakClockInitialized;
		}

	}
}
