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
		private sealed class LeakWorkFrame
		{
			internal GameObject Work;
			internal string WorkId;
			internal Zone Zone;
			internal Cell Cell;
			internal r_KingdomWear WearPart;
			internal int Wear;
			internal int LastCause;
			internal bool Held;
			internal int RepairEffort;
			internal long LastLeakTick;
			internal bool LeakClockInitialized;
			internal bool LeakAnnounced;
			internal string IncidentId;
			internal int LeakKind;
			internal long FromTick;
			internal long ToTick;
			internal int Before;
			internal int After;
			internal int Wanted;
			internal int Capacity;
			internal string OwnerId;
			internal string ZoneId;
			internal string ItemIds;
			internal string ItemOriginals;
			internal string ItemAllocations;
			internal LiquidVolume Vessel;
			internal Capacitor Bed;
			internal Inventory Inventory;
			internal r_KingdomPowerStore PowerStore;
			internal int StoresMark;
			internal int LarderMark;
		}

		private static bool TryCaptureLeakWork(GameObject Work, r_KingdomWear Wear,
			out LeakWorkFrame Frame)
		{
			Frame = null;
			if (!GameObject.Validate(Work) || Wear == null || Work.CurrentZone == null
				|| Work.CurrentCell == null || Work.CurrentCell.ParentZone != Work.CurrentZone
				|| Wear.ParentObject != Work || !ReferenceEquals(Work.GetPart<r_KingdomWear>(), Wear)
				|| Work.ID != Wear.LeakOwnerId || Work.CurrentZone.ZoneID != Wear.LeakZoneId
				|| Work.CurrentCell.X != Wear.LeakCellX || Work.CurrentCell.Y != Wear.LeakCellY) return false;
			Frame = new LeakWorkFrame
			{
				Work = Work,
				WorkId = Work.ID,
				Zone = Work.CurrentZone,
				Cell = Work.CurrentCell,
				WearPart = Wear,
				Wear = Wear.Wear,
				LastCause = Wear.LastCause,
				Held = Wear.Held,
				RepairEffort = Wear.RepairEffortLeft,
				LastLeakTick = Wear.LastLeakTick,
				LeakClockInitialized = Wear.LeakClockInitialized,
				LeakAnnounced = Wear.LeakAnnounced,
				IncidentId = Wear.LeakIncidentId,
				LeakKind = Wear.LeakKind,
				FromTick = Wear.LeakFromTick,
				ToTick = Wear.LeakToTick,
				Before = Wear.LeakBefore,
				After = Wear.LeakAfter,
				Wanted = Wear.LeakWanted,
				Capacity = Wear.LeakCapacity,
				OwnerId = Wear.LeakOwnerId,
				ZoneId = Wear.LeakZoneId,
				ItemIds = Wear.LeakItemIds,
				ItemOriginals = Wear.LeakItemOriginalCounts,
				ItemAllocations = Wear.LeakItemAllocations,
				Vessel = Work.GetPart<LiquidVolume>(),
				Bed = Work.GetPart<Capacitor>(),
				Inventory = Work.Inventory,
				PowerStore = Work.GetPart<r_KingdomPowerStore>(),
				StoresMark = Work.GetIntProperty(StoresProperty),
				LarderMark = Work.GetIntProperty(LarderProperty)
			};
			return true;
		}

		private static bool LeakWorkExact(LeakWorkFrame Frame,
			KingdomWearLeakPhase ExpectedPhase)
		{
			if (Frame == null || !GameObject.Validate(Frame.Work) || Frame.Work.ID != Frame.WorkId
				|| Frame.Work.CurrentZone != Frame.Zone || Frame.Work.CurrentCell != Frame.Cell
				|| Frame.Cell == null || Frame.Cell.ParentZone != Frame.Zone
				|| Frame.WearPart == null || Frame.WearPart.ParentObject != Frame.Work
				|| !ReferenceEquals(Frame.Work.GetPart<r_KingdomWear>(), Frame.WearPart)
				|| Frame.WearPart.Wear != Frame.Wear || Frame.WearPart.LastCause != Frame.LastCause
				|| Frame.WearPart.Held != Frame.Held
				|| Frame.WearPart.RepairEffortLeft != Frame.RepairEffort
				|| Frame.WearPart.LastLeakTick != Frame.LastLeakTick
				|| Frame.WearPart.LeakClockInitialized != Frame.LeakClockInitialized
				|| Frame.WearPart.LeakAnnounced != Frame.LeakAnnounced
				|| Frame.WearPart.LeakIncidentId != Frame.IncidentId
				|| Frame.WearPart.LeakKind != Frame.LeakKind
				|| Frame.WearPart.LeakFromTick != Frame.FromTick
				|| Frame.WearPart.LeakToTick != Frame.ToTick
				|| Frame.WearPart.LeakBefore != Frame.Before
				|| Frame.WearPart.LeakAfter != Frame.After
				|| Frame.WearPart.LeakWanted != Frame.Wanted
				|| Frame.WearPart.LeakCapacity != Frame.Capacity
				|| Frame.WearPart.LeakOwnerId != Frame.OwnerId
				|| Frame.WearPart.LeakZoneId != Frame.ZoneId
				|| Frame.WearPart.LeakItemIds != Frame.ItemIds
				|| Frame.WearPart.LeakItemOriginalCounts != Frame.ItemOriginals
				|| Frame.WearPart.LeakItemAllocations != Frame.ItemAllocations
				|| (KingdomWearLeakPhase)Frame.WearPart.LeakPhase != ExpectedPhase
				|| !ReferenceEquals(Frame.Work.GetPart<LiquidVolume>(), Frame.Vessel)
				|| !ReferenceEquals(Frame.Work.GetPart<Capacitor>(), Frame.Bed)
				|| !ReferenceEquals(Frame.Work.Inventory, Frame.Inventory)
				|| !ReferenceEquals(Frame.Work.GetPart<r_KingdomPowerStore>(), Frame.PowerStore)
				|| Frame.Work.GetIntProperty(StoresProperty) != Frame.StoresMark
				|| Frame.Work.GetIntProperty(LarderProperty) != Frame.LarderMark) return false;
			return true;
		}

	}
}
