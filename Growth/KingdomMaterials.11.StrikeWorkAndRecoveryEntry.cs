using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;
using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomMaterials
	{
		private static void WorkStrike(KingdomSystem System, Zone Z, GameObject Building,
			int Hands, long TimeTicks)
		{
			if (!TryStrikeJob(System, Z, Building, out KingdomConstructionJob job))
			{
				int legacyLeft = Building.GetIntProperty(StrikeEffortProperty);
				int legacyTotal = Building.GetIntProperty(StrikeTotalProperty);
				if (legacyLeft <= 0 || !OrderStrikeDurable(System, Z, Building, null, false,
					false, null, out job, out _)) return;
				Building.SetIntProperty(StrikeEffortProperty, legacyLeft);
				Building.SetIntProperty(StrikeTotalProperty,
					legacyTotal > 0 ? legacyTotal : legacyLeft);
			}
			if (job.PhysicalPhase != KingdomPhysicalPhase.StrikeWorking
				|| Building.GetIntProperty(StrikeEffortProperty) <= 0)
			{
				if (job.PhysicalPhase == KingdomPhysicalPhase.StrikeWorking
					&& Building.GetIntProperty(StrikeEffortProperty) <= 0)
					KingdomConstruction.UpdatePhysical(ref job,
						KingdomPhysicalPhase.StrikeWorkComplete, 0, 0,
						job.PhysicalSpilled, null, null, job.PhysicalReceipt);
				ContinueStrike(System, Z, Building, job);
				return;
			}
			// The order keeps its own checkpoint so zone activation cannot manufacture work-days.
			long worked = ReadTick(Building, StrikeWorkedProperty);
			if (worked <= 0)
			{
				WriteTick(Building, StrikeWorkedProperty, TimeTicks);
				return;
			}
			int days = KingdomRules.ElapsedDays(TimeTicks - worked);
			if (days <= 0) return;
			if (Hands <= 0)
			{
				if (Building.GetIntProperty(StrikeAnnouncedProperty) != 1)
				{
					Building.SetIntProperty(StrikeAnnouncedProperty, 1);
					System.Ledger.Note("{{r|The " + Building.ShortDisplayName + " is condemned, and there is nobody free to take it down. Stand a settler down off the water or a work.}}");
				}
				WriteTick(Building, StrikeWorkedProperty,
					KingdomRules.AdvanceCheckpoint(worked, TimeTicks));
				return;
			}
			Building.SetIntProperty(StrikeAnnouncedProperty, 0);
			WriteTick(Building, StrikeWorkedProperty,
				KingdomRules.AdvanceCheckpoint(worked, TimeTicks));
			int left = Building.GetIntProperty(StrikeEffortProperty)
				- KingdomMaterialRules.EffortWorked(Hands, days);
			Building.SetIntProperty(StrikeEffortProperty, Math.Max(0, left));
			if (left <= 0 && KingdomConstruction.UpdatePhysical(ref job,
				KingdomPhysicalPhase.StrikeWorkComplete, 0, 0, job.PhysicalSpilled,
				null, null, job.PhysicalReceipt)) ContinueStrike(System, Z, Building, job);
		}

		internal static void RetryConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| (Job.Route != KingdomConstructionRoute.Strike
					&& Job.Route != KingdomConstructionRoute.SocketConvert)
				|| Job.PhysicalPhase == KingdomPhysicalPhase.None) return;
			GameObject source = ExactObject(Job.SourceId);
			if (Job.PhysicalPhase == KingdomPhysicalPhase.StrikeCancellationPending)
			{
				KingdomConstructionJob cancelling = Job;
				FinishStrikeCancellation(Z, source, ref cancelling);
				return;
			}
			if (Job.PhysicalPhase == KingdomPhysicalPhase.StrikeOrdered
				|| Job.PhysicalPhase == KingdomPhysicalPhase.StrikeStampPending)
			{
				if (!KingdomConstructionRules.TryDecodeStrikeIntent(Job.PhysicalReceipt,
					out KingdomStrikeIntent stampIntent) || stampIntent.Targets == null)
				{
					QuarantineStrike(Job, "Legacy or malformed strike stamp lacks exact targets.");
					return;
				}
				KingdomConstructionJob stamping = Job;
				ResumeStrikeStamp(Z, source, stampIntent, ref stamping);
				return;
			}
			if (GameObject.Validate(source) && source.CurrentZone == Z
				&& source.GetIntProperty(StrikeEffortProperty) > 0
				&& Job.PhysicalPhase == KingdomPhysicalPhase.StrikeWorking) return;
			if (GameObject.Validate(source) && source.CurrentZone == Z
				&& source.GetIntProperty(StrikeEffortProperty) <= 0
				&& Job.PhysicalPhase == KingdomPhysicalPhase.StrikeWorking)
			{
				KingdomConstructionJob completed = Job;
				if (!KingdomConstruction.UpdatePhysical(ref completed,
					KingdomPhysicalPhase.StrikeWorkComplete, 0, 0,
					completed.PhysicalSpilled, null, null, completed.PhysicalReceipt)) return;
				Job = completed;
			}
			ContinueStrike(System, Z, source, Job);
		}

		internal static void InspectConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			RetryConstruction(System, Z, Job);
		}

		private static bool HasActiveStrikeReceipt(KingdomSystem System, Zone Z,
			GameObject Building)
		{
			return TryStrikeJob(System, Z, Building, out KingdomConstructionJob job)
				&& job.PhysicalPhase != KingdomPhysicalPhase.None;
		}

		private static bool TryStrikeJob(KingdomSystem System, Zone Z, GameObject Building,
			out KingdomConstructionJob Job)
		{
			Job = null;
			if (!GameObject.Validate(Building)) return false;
			string receipt = Building.GetStringProperty(KingdomConstruction.ReceiptProperty);
			return !string.IsNullOrEmpty(receipt) && KingdomConstruction.TryFind(receipt, out Job)
				&& KingdomConstruction.Owns(System, Z, Job)
				&& !KingdomConstructionRules.IsTerminal(Job.Phase)
				&& (Job.Route == KingdomConstructionRoute.Strike
					|| Job.Route == KingdomConstructionRoute.SocketConvert)
				&& Job.SourceId == Building.ID;
		}

		private static GameObject ExactObject(string Id)
		{
			if (string.IsNullOrEmpty(Id)) return null;
			GameObject item = GameObject.FindByID(Id);
			return GameObject.Validate(item) && item.ID == Id ? item : null;
		}

	}
}
