using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	using XRL;
	using XRL.Messages;
	using XRL.UI;
	using XRL.World;
	using XRL.World.Parts;

	internal static partial class KingdomLab
	{
		private static void ManageRemoval(GameObject Actor, KingdomSystem System,
			r_KingdomLabRemovalJob Job)
		{
			Job.Normalize();
			LabProcedure procedure = FrozenRemovalProcedure(Job);
			if (!CurrentRemovalAuthority(Actor, System, Job) || procedure == null)
			{
				Job.State = KingdomLabRemovalPhase.Quarantined;
				Job.Fault = string.IsNullOrEmpty(Job.Fault)
					? "The removal receipt cannot prove its exact patient, realm lineage, or frozen contract. It offers no action."
					: Job.Fault;
				EnsureRemovalGovernance(Job);
				Popup.Show(Job.Fault);
				return;
			}
			EnsureRemovalGovernance(Job);
			string receipt = RemovalReceipt(Job, procedure);
			KingdomLabOwnershipSnapshot snapshot = RemovalSnapshot(Job);
			KingdomLabOwnedTarget ignored;
			KingdomLabOwnedTargetState physical = KingdomProcedures.ClassifyOwned(Actor,
				snapshot, out ignored);
			if (physical == KingdomLabOwnedTargetState.Uncertain)
			{
				Job.State = KingdomLabRemovalPhase.Quarantined;
				Job.Fault = "The frozen effect identity or patient-slot bearer is uncertain. No payment, cleanup, or body callback will run.";
				Popup.Show(RemovalReceipt(Job, procedure));
				return;
			}
			if (physical == KingdomLabOwnedTargetState.Absent)
			{
				bool durable = Job.WaterPaid > 0 || Job.WaterLost > 0 || Job.WaterQuarantined
					|| Job.EffectRemoved;
				if (!durable)
				{
					ArchiveCleanAbsentRemoval(Actor, Job, procedure, snapshot);
					Popup.Show("The exact graft is absent. The unspent receipt was cleaned without governance or success tellings.");
					return;
				}
				CompleteRemoval(Actor, System, Job, procedure, snapshot);
				Popup.Show(RemovalReceipt(Job, procedure));
				Job.ReceiptPresented = true;
				return;
			}
			if (Job.State == KingdomLabRemovalPhase.Funding
				|| Job.State == KingdomLabRemovalPhase.FundingRecovery)
			{
				bool clean = Job.WaterPaid == 0 && Job.WaterLost == 0
					&& Job.WaterMeasurementExact && !Job.WaterQuarantined;
				string[] options = clean
					? new string[] { "Retry only the outstanding exact water.",
						"Discard this clean, unspent receipt; keep the action free.",
						"Leave the receipt preserved." }
					: new string[] { "Retry only the outstanding exact water.",
						"Leave the receipt preserved." };
				int choice = Popup.PickOption(Title: "recover removal payment", Intro: receipt,
					Options: options, AllowEscape: true);
				if (choice == 0)
				{
					RecoverRemovalFunding(Actor, System, Job, procedure);
				}
				else if (clean && choice == 1)
				{
					DiscardCleanRemovalReceipt(Actor, Job);
				}
				return;
			}
			if (Job.State == KingdomLabRemovalPhase.Paid
				|| Job.State == KingdomLabRemovalPhase.Removing
				|| Job.State == KingdomLabRemovalPhase.RemovalRecovery
				|| Job.State == KingdomLabRemovalPhase.Removed
				|| Job.State == KingdomLabRemovalPhase.Complete)
			{
				int choice = Popup.PickOption(Title: "recover exact removal", Intro: receipt,
					Options: new string[] { "Retry the exact tracked effect; charge no more water.",
						"Leave the receipt preserved." }, AllowEscape: true);
				if (choice == 0)
				{
					AttemptRemoval(Actor, System, Job, procedure);
				}
				return;
			}
			Popup.Show(RemovalReceipt(Job, procedure));
		}

		private static void RecoverRemovalFunding(GameObject Actor, KingdomSystem System,
			r_KingdomLabRemovalJob Job, LabProcedure Procedure)
		{
			if (Job.WaterQuarantined || !CurrentRemovalAuthority(Actor, System, Job))
			{
				return;
			}
			KingdomLabOwnershipSnapshot snapshot = RemovalSnapshot(Job);
			KingdomLabOwnedTarget ignored;
			KingdomLabOwnedTargetState before = KingdomProcedures.ClassifyOwned(Actor,
				snapshot, out ignored);
			if (before == KingdomLabOwnedTargetState.Absent)
			{
				if (Job.WaterPaid > 0 || Job.WaterLost > 0)
					CompleteRemoval(Actor, System, Job, Procedure, snapshot);
				else if (KingdomProcedures.CleanupOwned(Actor, Procedure, snapshot))
					ArchiveCleanAbsentRemoval(Actor, Job, Procedure, snapshot);
				return;
			}
			if (before != KingdomLabOwnedTargetState.Present)
			{
				Job.State = KingdomLabRemovalPhase.Quarantined;
				Job.Fault = "The exact target is uncertain before outstanding water. Nothing further was charged.";
				return;
			}
			int outstanding = Math.Max(0, Job.WaterOwed - Job.WaterPaid);
			if (outstanding > 0)
			{
				KingdomSurvey survey = (Actor.CurrentZone == null) ? null
					: KingdomSurvey.Take(Actor.CurrentZone, System);
				KingdomWaterDebit debit;
				if (survey == null || !survey.TryReserveExactWater(outstanding, out debit))
				{
					Popup.Show("The stores cannot reserve the exact outstanding {{C|" + outstanding
						+ "}} drams. The receipt was unchanged.");
					return;
				}
				KingdomLabOwnedTargetState preCommit = KingdomProcedures.ClassifyOwned(Actor,
					snapshot, out ignored);
				if (preCommit != KingdomLabOwnedTargetState.Present)
				{
					debit.Rollback();
					if (preCommit == KingdomLabOwnedTargetState.Absent
						&& Job.WaterPaid == 0 && Job.WaterLost == 0)
					{
						ArchiveCleanAbsentRemoval(Actor, Job, Procedure, snapshot);
					}
					else
					{
						Job.State = KingdomLabRemovalPhase.Quarantined;
						Job.Fault = "The exact target changed after water reservation but before commit. Nothing was charged or touched.";
					}
					return;
				}
				debit.Commit();
				KingdomLabOwnedTargetState afterCommit = KingdomProcedures.ClassifyOwned(Actor,
					snapshot, out ignored);
				if (afterCommit != KingdomLabOwnedTargetState.Present)
				{
					bool compensated = debit.Rollback();
					MergeRemovalWater(Job, debit);
					if (afterCommit == KingdomLabOwnedTargetState.Absent && compensated
						&& Job.WaterPaid == 0 && Job.WaterLost == 0 && !Job.WaterQuarantined)
					{
						ArchiveCleanAbsentRemoval(Actor, Job, Procedure, snapshot);
						return;
					}
					Job.State = KingdomLabRemovalPhase.Quarantined;
					Job.Fault = "The exact target changed during retry water callbacks. Compensation was measured; no replacement was touched.";
					EnsureRemovalGovernance(Job);
					return;
				}
				MergeRemovalWater(Job, debit);
			}
			Job.State = KingdomLabRules.RemovalFundingPhase(Job.WaterOwed,
				Job.WaterPaid, Job.WaterQuarantined);
			EnsureRemovalGovernance(Job);
			if (Job.State == KingdomLabRemovalPhase.Paid)
			{
				AttemptRemoval(Actor, System, Job, Procedure);
			}
			else
			{
				Popup.Show(RemovalReceipt(Job, Procedure));
			}
		}

		private static void MergeRemovalWater(r_KingdomLabRemovalJob Job,
			KingdomWaterDebit Debit)
		{
			if (Job == null || Debit == null)
			{
				return;
			}
			KingdomLabWaterClaim claim = KingdomLabRules.MergeWaterClaim(Job.WaterOwed,
				Job.WaterPaid, Job.WaterLost, Job.WaterQuarantined,
				Debit.Spent, Debit.Lost, Debit.MeasurementExact);
			Job.WaterMeasurementExact = Job.WaterMeasurementExact && Debit.MeasurementExact;
			Job.WaterPaid = claim.Paid;
			Job.WaterLost = claim.Lost;
			Job.WaterQuarantined = claim.Quarantined;
			if (claim.Quarantined)
			{
				Job.Fault = "Water vessel identity or composition became uncertain. Automatic retry is quarantined so the apparent balance cannot be charged twice."
					+ (string.IsNullOrEmpty(Debit.Failure) ? "" : (" " + Debit.Failure));
			}
			else if (!claim.Settled && !string.IsNullOrEmpty(Debit.Failure))
			{
				Job.Fault = Debit.Failure;
			}
		}

	}
}
