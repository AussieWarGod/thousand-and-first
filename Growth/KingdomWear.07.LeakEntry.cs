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
		// ==================================================================================
		// The kind-appropriate consequence (Addendum 10(b)): damaged water and charge stores lose
		// contents. A larder never does; food is opt-in positive play and stays physically stable.
		//
		// The clock is the P1 substrate and nothing else: KingdomRules.ElapsedDays over a stamp
		// that lives on the work's own part, planted on the first pass that looks at it and never
		// counted from zero. Days that produced no loss are BANKED rather than spent, so a small
		// store whose daily share rounds to nothing still empties honestly over a season, and a
		// founder cannot stop a leak by stepping in and out of the zone. Loss, not transfer: this
		// is water going into the ground, not the manifest's pour-on-ground surplus.
		// ==================================================================================

		private static void Leak(KingdomSystem System, KingdomSurvey Survey, GameObject Work, r_KingdomWear Wear, long TimeTicks)
		{
			RetireFoodLeakReceipt(Work, Wear);
			if (Wear.LifecycleQuarantined)
			{
				TellWearQuarantine(System, Work, Wear);
				return;
			}
			if ((KingdomWearLeakPhase)Wear.LeakPhase != KingdomWearLeakPhase.None)
			{
				ContinueBoundLeak(System, Survey, Work, Wear);
				return;
			}
			if (Work.GetIntProperty(StoresProperty) == 1)
			{
				LiquidVolume vessel = Work.GetPart<LiquidVolume>();
				if (vessel != null && vessel.MaxVolume > 0)
				{
					LeakWater(System, Survey, Work, Wear, vessel, TimeTicks);
				}
				return;
			}
			// A damaged larder can provide less effective work until repaired, but its exact items
			// remain the player's. Never plant or advance a passive food-loss clock here.
			if (Work.GetIntProperty(LarderProperty) == 1)
			{
				return;
			}
			if (Work.GetPart<r_KingdomPowerStore>() != null)
			{
				Capacitor bed = Work.GetPart<Capacitor>();
				if (bed != null && bed.MaxCharge > 0)
				{
					LeakCharge(System, Work, Wear, bed, TimeTicks);
				}
			}
		}

		private static void LeakWater(KingdomSystem System, KingdomSurvey Survey, GameObject Work, r_KingdomWear Wear,
			LiquidVolume Vessel, long TimeTicks)
		{
			int days;
			long checkpoint;
			if (!TryLeakWindow(System, Work, Wear, TimeTicks, out days, out checkpoint)) return;
			int wanted = KingdomWearRules.Leaked(KingdomWearRules.LeakKind.Water,
				Vessel.MaxVolume, Vessel.Volume, Wear.Wear, days);
			if (wanted <= 0)
			{
				if (Vessel.Volume <= 0) Wear.LastLeakTick = checkpoint;
				return;
			}
			BindLeak(Work, Wear, KingdomWearRules.LeakKind.Water, Wear.LastLeakTick,
				checkpoint, Vessel.Volume, Vessel.Volume - wanted, wanted, Vessel.MaxVolume,
				null, null, null);
			ContinueBoundLeak(System, Survey, Work, Wear);
		}

		private static void LeakCharge(KingdomSystem System, GameObject Work, r_KingdomWear Wear, Capacitor Bed, long TimeTicks)
		{
			int days;
			long checkpoint;
			if (!TryLeakWindow(System, Work, Wear, TimeTicks, out days, out checkpoint)) return;
			int wanted = KingdomWearRules.Leaked(KingdomWearRules.LeakKind.Charge,
				Bed.MaxCharge, Bed.Charge, Wear.Wear, days);
			if (wanted <= 0)
			{
				if (Bed.Charge <= 0) Wear.LastLeakTick = checkpoint;
				return;
			}
			BindLeak(Work, Wear, KingdomWearRules.LeakKind.Charge, Wear.LastLeakTick,
				checkpoint, Bed.Charge, Bed.Charge - wanted, wanted, Bed.MaxCharge,
				null, null, null);
			ContinueBoundLeak(System, null, Work, Wear);
		}

		private static bool TryLeakWindow(KingdomSystem System, GameObject Work,
			r_KingdomWear Wear, long TimeTicks, out int Days, out long Checkpoint)
		{
			Days = 0;
			Checkpoint = Wear.LastLeakTick;
			int elapsed = (TimeTicks >= Wear.LastLeakTick && Wear.LastLeakTick >= 0L)
				? KingdomRules.ElapsedDays(TimeTicks - Wear.LastLeakTick) : 0;
			KingdomWearClockAction action = KingdomWearRules.LeakClockAction(
				Wear.LeakClockInitialized, Wear.LastLeakTick, TimeTicks, elapsed);
			if (action == KingdomWearClockAction.Quarantine)
			{
				QuarantineWear(System, Work, TimeTicks < Wear.LastLeakTick
					? "Its storage-loss clock regressed." : "Its storage-loss clock is malformed.");
				return false;
			}
			if (action == KingdomWearClockAction.Plant)
			{
				Wear.LastLeakTick = TimeTicks;
				Wear.LeakClockInitialized = true;
				Checkpoint = TimeTicks;
				return false;
			}
			Days = elapsed;
			if (action == KingdomWearClockAction.Wait) return false;
			Checkpoint = KingdomRules.AdvanceCheckpoint(Wear.LastLeakTick, TimeTicks);
			return Checkpoint >= Wear.LastLeakTick;
		}

		private static void BindLeak(GameObject Work, r_KingdomWear Wear,
			KingdomWearRules.LeakKind Kind, long FromTick, long ToTick,
			int Before, int After, int Wanted, int Capacity, string ItemIds,
			string ItemOriginals, string ItemAllocations)
		{
			Wear.LeakIncidentId = WearEventId(Work, "leak-" + (int)Kind, ToTick);
			Wear.LeakKind = (int)Kind;
			Wear.LeakFromTick = FromTick;
			Wear.LeakToTick = ToTick;
			Wear.LeakBefore = Before;
			Wear.LeakAfter = After;
			Wear.LeakWanted = Wanted;
			Wear.LeakActualLost = 0;
			Wear.LeakOwnerId = Work.ID;
			Wear.LeakZoneId = Work.CurrentZone?.ZoneID;
			Wear.LeakCellX = (Work.CurrentCell == null) ? -1 : Work.CurrentCell.X;
			Wear.LeakCellY = (Work.CurrentCell == null) ? -1 : Work.CurrentCell.Y;
			Wear.LeakCapacity = Capacity;
			Wear.LeakLine = KingdomWearRules.LeakBegunLine(DisplayName(Work), Kind);
			Wear.LeakItemIds = ItemIds;
			Wear.LeakItemOriginalCounts = ItemOriginals;
			Wear.LeakItemAllocations = ItemAllocations;
			Wear.LeakLedgerState = (int)KingdomWearSinkDisposition.None;
			Wear.LeakMessageState = (int)KingdomWearSinkDisposition.None;
			Wear.LeakPhase = (int)KingdomWearLeakPhase.Bound;
		}

	}
}
