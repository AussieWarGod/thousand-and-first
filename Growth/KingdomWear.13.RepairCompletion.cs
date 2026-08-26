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
		private static void AdvanceRepair(KingdomSystem System, GameObject Work, r_KingdomWear WearPart, int Hands, long TimeTicks)
		{
			long worked = KingdomMaterials.ReadTick(Work, RepairWorkedProperty);
			if (worked <= 0)
			{
				KingdomMaterials.WriteTick(Work, RepairWorkedProperty, TimeTicks);
				return;
			}
			int days = KingdomRules.ElapsedDays(TimeTicks - worked);
			if (days <= 0)
			{
				return;
			}
			if (Hands <= 0)
			{
				if (WearPart.AnnouncedBlock != (int)KingdomWearRules.RepairVerdict.NoHands)
				{
					WearPart.AnnouncedBlock = (int)KingdomWearRules.RepairVerdict.NoHands;
					string blockLine = KingdomWearRules.ReasonLine(KingdomWearRules.RepairVerdict.NoHands, DisplayName(Work));
					if (blockLine != null)
					{
						System.Ledger.Note("{{r|" + blockLine + "}}");
					}
				}
				KingdomMaterials.WriteTick(Work, RepairWorkedProperty, KingdomRules.AdvanceCheckpoint(worked, TimeTicks));
				return;
			}
			WearPart.AnnouncedBlock = 0;
			KingdomMaterials.WriteTick(Work, RepairWorkedProperty, KingdomRules.AdvanceCheckpoint(worked, TimeTicks));
			int left = WearPart.RepairEffortLeft - KingdomMaterialRules.EffortWorked(Hands, days);
			if (left > 0)
			{
				WearPart.RepairEffortLeft = left;
				return;
			}
			string receipt = Work.GetStringProperty(KingdomConstruction.ReceiptProperty);
			KingdomConstructionJob job;
			if (!string.IsNullOrEmpty(receipt))
			{
				if (!KingdomConstruction.TryFind(receipt, out job)
					|| !KingdomConstruction.Owns(System, Work.CurrentZone, job)
					|| job.Route != KingdomConstructionRoute.WearRepair
					|| KingdomConstructionRules.IsTerminal(job.Phase)) return;
				int paidWear;
				bool finishing;
				if (!TryRepairPayload(job.Payload, out paidWear, out finishing)) return;
				if (!finishing)
				{
					if (!KingdomConstruction.UpdatePayload(ref job,
						RepairPayload(paidWear, true))) return;
				}
				if (!FinishRepairProjection(System, Work, WearPart, job, out job, out _))
				{
					return;
				}
			}
			else
			{
				// A legacy save has no keyed construction row on which to freeze an outbox or
				// publish a one-shot callback intent. Do not guess its destructive continuation.
				QuarantineWear(System, Work,
					"A legacy repair reached part removal without a durable keyed receipt.");
				return;
			}
		}

		private static bool FinishRepairProjection(KingdomSystem System, GameObject Work,
			r_KingdomWear WearPart,
			KingdomConstructionJob Job, out KingdomConstructionJob Updated, out string Failure)
		{
			Updated = Job;
			Failure = null;
			Zone zone = Work?.CurrentZone;
			if (!RepairSubjectExact(System, zone, Work, Job))
			{
				Failure = "The paid repair target could not be verified.";
				return false;
			}
			if (WearPart == null)
			{
				Failure = "The wear part is absent without a live exact removal proof.";
				MarkRepairRemovalLost(ref Updated, Failure);
				return false;
			}
			if (WearPart.ParentObject != Work
				|| !ReferenceEquals(Work.GetPart<r_KingdomWear>(), WearPart)
				|| !string.IsNullOrEmpty(Work.GetStringProperty(RepairRemovalAttemptProperty))
				|| !string.IsNullOrEmpty(Work.GetStringProperty(RepairRemovalProofProperty)))
			{
				Failure = "A repair removal intent already exists or the exact wear part was replaced.";
				MarkRepairRemovalLost(ref Updated, Failure);
				return false;
			}
			int requestedWear;
			bool finishing;
			if (!TryRepairPayload(Job.Payload, out requestedWear, out finishing) || !finishing
				|| (WearPart.Wear != requestedWear && WearPart.Wear != 0))
			{
				Failure = "The damaged state no longer matches its repair receipt.";
				return false;
			}
			if (!KingdomConstruction.BeginProjection(ref Updated, out Failure))
			{
				return false;
			}
			RepairTargetFrame frame;
			if (!TryCaptureRepairTarget(Work, WearPart, out frame)
				|| !RepairTargetExact(frame, Updated.Id)
				|| !KingdomConstruction.IsCurrent(Updated))
			{
				Failure = "The exact repair target changed before its completion outbox was frozen.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			string name = DisplayName(Work);
			string leakStopped = WearPart.LeakAnnounced
				? KingdomWearRules.LeakStoppedLine(name, LeakKindOf(Work)) : null;
			if (!KingdomCeremony.PrepareWearRepaired(System, name, leakStopped, ref Updated)
				|| !RepairTargetExact(frame, Updated.Id)
				|| !KingdomConstruction.IsCurrent(Updated))
			{
				Failure = "The repair completion outbox or exact target could not be frozen.";
				KingdomConstruction.Quarantine(ref Updated, Failure);
				return false;
			}
			Work.SetStringProperty(RepairRemovalAttemptProperty, Updated.Id);
			if (!string.Equals(Work.GetStringProperty(RepairRemovalAttemptProperty),
					Updated.Id, StringComparison.Ordinal)
				|| !RepairTargetExact(frame, Updated.Id)
				|| !KingdomConstruction.IsCurrent(Updated))
			{
				Failure = "The one-shot repair removal intent could not be proved before its callback.";
				MarkRepairRemovalLost(ref Updated, Failure);
				return false;
			}
			WearPart.RepairEffortLeft = 0;
			WearPart.Wear = 0;
			WearPart.LastCause = (int)KingdomWearRules.WearCause.None;
			bool callbackReturned = true;
			try
			{
				// The only wear-owned PartRemoved callback. The durable attempt latch above means
				// no recovery path can enter this call a second time.
				Work.RemovePart(WearPart);
			}
			catch (Exception)
			{
				callbackReturned = false;
			}
			bool exactRemoval = callbackReturned && GameObject.Validate(Work)
				&& Work.ID == frame.Id && Work.CurrentZone == frame.Zone
				&& Work.CurrentCell == frame.Cell && frame.Cell.ParentZone == frame.Zone
				&& string.Equals(Work.GetStringProperty(KingdomConstruction.ReceiptProperty),
					Updated.Id, StringComparison.Ordinal)
				&& string.Equals(Work.GetStringProperty(RepairRemovalAttemptProperty),
					Updated.Id, StringComparison.Ordinal)
				&& WearPart.ParentObject == null && Work.GetPart<r_KingdomWear>() == null
				&& KingdomConstruction.IsCurrent(Updated);
			if (!exactRemoval)
			{
				Failure = "The PartRemoved callback changed or obscured the exact repaired work, part, cell, zone, receipt, or job.";
				MarkRepairRemovalLost(ref Updated, Failure);
				return false;
			}
			Work.SetStringProperty(RepairRemovalProofProperty, Updated.Id);
			Work.RemoveStringProperty(RepairRemovalAttemptProperty);
			if (!string.Equals(Work.GetStringProperty(RepairRemovalProofProperty),
					Updated.Id, StringComparison.Ordinal)
				|| !string.IsNullOrEmpty(
					Work.GetStringProperty(RepairRemovalAttemptProperty))
				|| !KingdomConstruction.IsCurrent(Updated))
			{
				Failure = "The exact post-callback repair proof could not be persisted.";
				MarkRepairRemovalLost(ref Updated, Failure);
				return false;
			}
			if (!KingdomConstruction.Complete(ref Updated)) return false;
			Work.RemoveStringProperty(RepairRemovalProofProperty);
			bool dispatched = KingdomCeremony.DispatchPending(System, ref Updated);
			KingdomLog.Log("wear: repair complete " + Work.Blueprint);
			return dispatched;
		}

		private static string DisplayName(GameObject Work)
		{
			return KingdomDesign.ReferenceFor(Work, Work.ShortDisplayName);
		}

		/// <summary>Which kind of contents this work stores, for the sentence a leak is told in.
		/// Water is the default because the vessel is the ordinary case; a work that stores
		/// nothing never reaches either line.</summary>
		private static KingdomWearRules.LeakKind LeakKindOf(GameObject Work)
		{
			if (Work.GetIntProperty(StoresProperty) == 1)
			{
				return KingdomWearRules.LeakKind.Water;
			}
			if (Work.GetIntProperty(LarderProperty) == 1)
			{
				return KingdomWearRules.LeakKind.Food;
			}
			return (Work.GetPart<r_KingdomPowerStore>() != null)
				? KingdomWearRules.LeakKind.Charge
				: KingdomWearRules.LeakKind.Water;
		}
	}
}
