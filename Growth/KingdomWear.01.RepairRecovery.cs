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

	/// <summary>
	/// Wear and repair (BUILDING-CATALOGUE-BRIEF.md Addendum 7: "maintenance/wear translation").
	/// Three causes damage a work &mdash; raiders who got past the wall
	/// (<see cref="OnRaidDamage"/>, called from <c>KingdomRaids.ExecuteRaid</c>), a streak of
	/// consecutive full-stretch attended passes, and certified salvage acting up on use &mdash;
	/// and a fourth, a lost rung, reaches a staffless work too (<c>KingdomSubsidence.Ruin</c>).
	/// Nothing else does. Absence never wears anything: every draw in
	/// <see cref="KingdomWearRules"/> is keyed to an event a real pass produced, never to elapsed
	/// time. What already-damaged works go on LOSING does run on world days, which is a
	/// consequence of the damage rather than a second cause of it.
	/// <para>
	/// A damaged work keeps working, at <c>KingdomMaterialRules.ConditionPercent(Wear)</c> of
	/// what it manages whole, and says so once (STANDARDS 7b) the moment it happens. That
	/// reduction reaches EVERY work, crewed or not (Addendum 10(b),
	/// <see cref="KingdomWearRules.WorkEffectiveness"/>), and on top of it damage has
	/// kind-appropriate consequences: a store loses what it holds (<see cref="Leak"/>), a power
	/// work makes less. Mending is a materials-and-hands job, auto-queued like an improvement but always
	/// visible (<c>r_KingdomWear.HandleEvent</c>) and holdable (<see cref="r_KingdomWear.Held"/>):
	/// one job at a time settlement-wide, the same "one gang, one job" law
	/// <c>KingdomMaterials.OnSettlementPass</c> already keeps for striking and clearing, costed
	/// and timed the same way a strike is &mdash; <c>KingdomMaterialRules.RepairCost</c>/
	/// <c>RepairBits</c> for what it costs, <c>RepairEffort</c> and
	/// <c>KingdomRules.ElapsedDays</c> for how long it takes. Nothing here spends water, and
	/// nothing here ever fails a work past <see cref="KingdomMaterialRules.MaxWearPercent"/>.
	/// </para>
	/// <para>
	/// <b>The clock.</b> <see cref="AdvanceRepair"/> is the reference for checkpoint ordering in
	/// this mod: it reads the gate, names the block once (STANDARDS 7b), and only then advances
	/// the stamp &mdash; so a mending nobody has hands for loses those days rather than banking
	/// them for a crew that was never there. <c>KingdomMaterials.WorkYard</c> keeps the same
	/// order for the same reason. The day count is the full elapsed, uncapped (Addendum 8
	/// clause 1): a crew mends through an absence exactly as it mends through a fortnight of
	/// visits, and what stops a season away from mending everything is that ordering &mdash;
	/// hands first, and one mending settlement-wide at a time. Idle hands put nothing back.
	/// </para>
	/// </summary>
	public static partial class KingdomWear
	{
		internal static void RetryConstruction(KingdomSystem System, Zone Z, KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| Job.Route != KingdomConstructionRoute.WearRepair)
			{
				return;
			}
			GameObject work;
			KingdomPhysicalLookupState workState = KingdomConstruction.FindSubject(Z, Job,
				out work);
			if (workState == KingdomPhysicalLookupState.Ambiguous)
			{
				MarkRepairRemovalLost(ref Job,
					"The repair receipt resolves to more than one exact physical subject.");
				return;
			}
			if (workState != KingdomPhysicalLookupState.Exact
				|| !GameObject.Validate(work) || work.CurrentZone != Z) return;
			if (!RepairSubjectExact(System, Z, work, Job))
			{
				MarkRepairRemovalLost(ref Job,
					"The repair receipt is no longer bound to its exact work, cell, zone, and owner.");
				return;
			}
			r_KingdomWear wear = work.GetPart<r_KingdomWear>();
			if (wear == null)
			{
				RecoverRemovedRepair(System, work, Job);
				return;
			}
			if (!string.IsNullOrEmpty(work.GetStringProperty(RepairRemovalAttemptProperty))
				|| !string.IsNullOrEmpty(work.GetStringProperty(RepairRemovalProofProperty)))
			{
				wear.LifecycleQuarantined = true;
				wear.QuarantineReason =
					"A repair part-removal callback was interrupted and will not be repeated.";
				MarkRepairRemovalLost(ref Job, wear.QuarantineReason);
				TellWearQuarantine(System, work, wear);
				return;
			}
			int paidWear;
			bool finishing;
			if (!TryRepairPayload(Job.Payload, out paidWear, out finishing)) return;
			if (finishing)
			{
				FinishRepairProjection(System, work, wear, Job, out _, out _);
				return;
			}
			if (wear.Wear <= 0)
			{
				FinishRepairProjection(System, work, wear, Job, out _, out _);
				return;
			}
			if (wear.RepairEffortLeft > 0)
			{
				KingdomConstructionJob working = Job;
				KingdomConstruction.FinishProjection(ref working, true, true);
				return;
			}
			ProjectRepair(System, work, wear, Job, out _, out _);
		}

		internal static void InspectConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| Job.Route != KingdomConstructionRoute.WearRepair) return;
			GameObject work;
			KingdomPhysicalLookupState workState = KingdomConstruction.FindSubject(Z, Job,
				out work);
			if (workState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstructionJob duplicate = Job;
				MarkRepairRemovalLost(ref duplicate,
					"The repair receipt resolves to more than one exact physical subject.");
				return;
			}
			if (workState != KingdomPhysicalLookupState.Exact
				|| !GameObject.Validate(work) || work.CurrentZone != Z) return;
			KingdomConstructionJob inspected = Job;
			r_KingdomWear wear = work.GetPart<r_KingdomWear>();
			if (wear == null)
			{
				if (Job.Phase == KingdomConstructionPhase.Complete) return;
				RecoverRemovedRepair(System, work, inspected);
				return;
			}
			if (!RepairSubjectExact(System, Z, work, Job)
				|| !string.IsNullOrEmpty(work.GetStringProperty(RepairRemovalAttemptProperty))
				|| !string.IsNullOrEmpty(work.GetStringProperty(RepairRemovalProofProperty)))
			{
				wear.LifecycleQuarantined = true;
				wear.QuarantineReason =
					"The repair inspector found an uncertain part-removal callback or subject binding.";
				MarkRepairRemovalLost(ref inspected, wear.QuarantineReason);
				TellWearQuarantine(System, work, wear);
				return;
			}
			int paidWear;
			bool finishing;
			if (!TryRepairPayload(Job.Payload, out paidWear, out finishing)) return;
			if (!finishing && wear.RepairEffortLeft > 0)
			{
				if (Job.Phase != KingdomConstructionPhase.Working)
				{
					KingdomConstruction.FinishProjection(ref inspected, true, true);
				}
				return;
			}
			if ((Job.Phase == KingdomConstructionPhase.ProjectionPending
					|| (finishing && Job.Phase == KingdomConstructionPhase.Working))
				&& (wear.Wear == paidWear || (finishing && wear.Wear == 0)))
			{
				KingdomConstruction.FinishProjection(ref inspected, false, false,
					finishing
						? "The receipt proves repair labour finished; its final condition is retryable."
						: "The damaged state survived before repair work was projected.");
			}
		}

		private static bool HasActiveRepair(GameObject Work, out KingdomConstructionJob Job)
		{
			Job = null;
			if (!KingdomConstruction.ReceiptBlocksCurrent(Work)) return false;
			string receipt = Work.GetStringProperty(KingdomConstruction.ReceiptProperty);
			KingdomConstruction.TryFind(receipt, out Job);
			return true;
		}

		private static bool RepairSubjectExact(KingdomSystem System, Zone Z,
			GameObject Work, KingdomConstructionJob Job)
		{
			return System != null && Z != null && Job != null
				&& Job.Route == KingdomConstructionRoute.WearRepair
				&& KingdomConstruction.Owns(System, Z, Job)
				&& GameObject.Validate(Work) && Work.IDIfAssigned == Job.SubjectId
				&& Work.CurrentZone == Z && Work.CurrentCell != null
				&& Work.CurrentCell.ParentZone == Z
				&& KingdomConstruction.HasReceipt(Work, Job);
		}

		private static KingdomConstructionSinkDisposition LoseOpenRepairSink(
			KingdomConstructionSinkDisposition State)
		{
			return State == KingdomConstructionSinkDisposition.Delivered
				|| State == KingdomConstructionSinkDisposition.Skipped
				|| State == KingdomConstructionSinkDisposition.Lost
					? State : KingdomConstructionSinkDisposition.Lost;
		}

		private static void MarkRepairRemovalLost(ref KingdomConstructionJob Job,
			string Failure)
		{
			if (Job == null) return;
			if (Job.Outbox != null)
			{
				KingdomConstructionOutbox lost = Job.Outbox.Copy();
				lost.ChronicleState = LoseOpenRepairSink(lost.ChronicleState);
				lost.LedgerState = LoseOpenRepairSink(lost.LedgerState);
				lost.MessageState = LoseOpenRepairSink(lost.MessageState);
				lost.DeedState = LoseOpenRepairSink(lost.DeedState);
				if (!KingdomConstruction.UpdateOutbox(ref Job, lost)) return;
			}
			KingdomConstruction.Quarantine(ref Job, Failure);
		}

		private static void RecoverRemovedRepair(KingdomSystem System, GameObject Work,
			KingdomConstructionJob Job)
		{
			KingdomConstructionJob recovered = Job;
			Zone zone = Work?.CurrentZone;
			bool proved = RepairSubjectExact(System, zone, Work, recovered)
				&& recovered.Outbox != null
				&& string.Equals(Work.GetStringProperty(RepairRemovalProofProperty),
					recovered.Id, StringComparison.Ordinal)
				&& string.IsNullOrEmpty(
					Work.GetStringProperty(RepairRemovalAttemptProperty));
			if (!proved)
			{
				MarkRepairRemovalLost(ref recovered,
					"The wear part is absent without a persisted exact post-callback removal proof.");
				return;
			}
			if (!KingdomConstruction.Complete(ref recovered)) return;
			Work.RemoveStringProperty(RepairRemovalProofProperty);
			KingdomCeremony.DispatchPending(System, ref recovered);
		}

	}
}
