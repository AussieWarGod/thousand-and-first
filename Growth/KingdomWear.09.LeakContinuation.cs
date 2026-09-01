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
		private static void ContinueBoundLeak(KingdomSystem System, KingdomSurvey Survey,
			GameObject Work, r_KingdomWear Wear)
		{
			if (RetireFoodLeakReceipt(Work, Wear)) return;
			KingdomWearLeakPhase phase = (KingdomWearLeakPhase)Wear.LeakPhase;
			if (phase == KingdomWearLeakPhase.Quarantined)
			{
				TellWearQuarantine(System, Work, Wear);
				return;
			}
			if (phase == KingdomWearLeakPhase.None) return;
			if (phase == KingdomWearLeakPhase.MutationIntent)
			{
				QuarantineLeak(System, Work, Wear, 0,
					"A storage-loss callback was interrupted; its mutation was not inspected, credited, or repeated.");
				return;
			}
			if (phase >= KingdomWearLeakPhase.Mutated)
			{
				ContinueLeakOutputs(System, Work, Wear);
				return;
			}
			if (!GameObject.Validate(Work) || Wear == null || Wear.ParentObject != Work
				|| !ReferenceEquals(Work.GetPart<r_KingdomWear>(), Wear)
				|| !string.Equals(Wear.LeakOwnerId, Work.ID, StringComparison.Ordinal)
				|| !string.Equals(Wear.LeakZoneId, Work.CurrentZone?.ZoneID,
					StringComparison.Ordinal) || Work.CurrentCell == null
				|| Work.CurrentCell.X != Wear.LeakCellX || Work.CurrentCell.Y != Wear.LeakCellY)
			{
				QuarantineLeak(System, Work, Wear, 0,
					"Its bound storage work changed identity or zone.");
				return;
			}
			int current;
			LiquidVolume boundVessel = null;
			Capacitor boundBed = null;
			if ((KingdomWearRules.LeakKind)Wear.LeakKind == KingdomWearRules.LeakKind.Water)
			{
				boundVessel = Work.GetPart<LiquidVolume>();
				if (boundVessel == null || boundVessel.ParentObject != Work
					|| Work.GetIntProperty(StoresProperty) != 1
					|| boundVessel.MaxVolume != Wear.LeakCapacity
					|| !(boundVessel.Volume == 0 || boundVessel.IsFreshWater()))
				{
					QuarantineLeak(System, Work, Wear, 0,
						"Its bound water vessel changed identity or contents.");
					return;
				}
				current = boundVessel.Volume;
			}
			else if ((KingdomWearRules.LeakKind)Wear.LeakKind == KingdomWearRules.LeakKind.Charge)
			{
				boundBed = Work.GetPart<Capacitor>();
				if (boundBed == null || boundBed.ParentObject != Work
					|| boundBed.MaxCharge != Wear.LeakCapacity
					|| Work.GetPart<r_KingdomPowerStore>() == null)
				{
					QuarantineLeak(System, Work, Wear, 0,
						"Its bound charge bed changed identity.");
					return;
				}
				current = boundBed.Charge;
			}
			else
			{
				QuarantineLeak(System, Work, Wear, 0,
					"Its bound storage-loss kind is unknown.");
				return;
			}
			KingdomWearMutationAction action = KingdomWearRules.LeakMutationAction(phase,
				Wear.LeakBefore, current, Wear.LeakAfter);
			if (action == KingdomWearMutationAction.Apply)
			{
				LeakWorkFrame frame;
				if (!TryCaptureLeakWork(Work, Wear, out frame))
				{
					QuarantineLeak(System, Work, Wear, 0,
						"The storage-loss live frame could not capture its exact work, wear part, cell, zone, and storage parts.");
					return;
				}
				Wear.LeakPhase = (int)KingdomWearLeakPhase.MutationIntent;
				if ((KingdomWearRules.LeakKind)Wear.LeakKind == KingdomWearRules.LeakKind.Water)
				{
					if (Survey == null || !Survey.Stores.Contains(boundVessel))
					{
						QuarantineLeak(System, Work, Wear, 0, "Its water survey is absent.");
						return;
					}
					int removed;
					bool exact = Survey.TryLeakFromExact(boundVessel, Wear.LeakWanted, out removed);
					if (!exact || removed != frame.Wanted
						|| !LeakWorkExact(frame, KingdomWearLeakPhase.MutationIntent)
						|| boundVessel.Volume != frame.After)
					{
						QuarantineLeak(System, Work, Wear, 0,
							"The water-loss callback changed an exact work, wear, owner, vessel, dictionary, survey-list, cell, zone, counter, capacity, or delta witness.");
						return;
					}
				}
				else if ((KingdomWearRules.LeakKind)Wear.LeakKind == KingdomWearRules.LeakKind.Charge)
				{
					boundBed.UseCharge(Wear.LeakWanted);
					bool stillExact = LeakWorkExact(frame, KingdomWearLeakPhase.MutationIntent)
						&& ReferenceEquals(Work.GetPart<Capacitor>(), boundBed)
						&& boundBed.ParentObject == Work
						&& boundBed.MaxCharge == Wear.LeakCapacity
						&& boundBed.Charge == Wear.LeakAfter;
					if (!stillExact)
					{
						QuarantineLeak(System, Work, Wear, 0,
							"The charge-loss mutation did not leave its exact bound bed and delta.");
						return;
					}
				}
				Wear.LeakActualLost = Wear.LeakWanted;
				Wear.LastLeakTick = Wear.LeakToTick;
				Wear.LeakClockInitialized = true;
				Wear.LeakPhase = (int)KingdomWearLeakPhase.Mutated;
				ContinueLeakOutputs(System, Work, Wear);
				return;
			}
			QuarantineLeak(System, Work, Wear, 0,
				"A bound storage-loss receipt changed before its one live callback frame began.");
		}

	}
}
