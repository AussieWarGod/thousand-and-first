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
		internal static void OnSemanticStep(KingdomSystem System, Zone Zone, KingdomSurvey Survey,
			long BoundaryTick)
		{
			KingdomLabCivicRuntime.Observe(System, Zone, Survey);
			List<GameObject> objects = (Survey != null && ReferenceEquals(Survey.Ground, Zone))
				? Survey.LabJobs : null;
			for (int i = 0; objects != null && i < objects.Count; i++)
			{
				GameObject building = objects[i];
				r_KingdomLabJob job = building?.GetPart<r_KingdomLabJob>();
				if (job == null || job.State != KingdomLabJobPhase.Working)
				{
					continue;
				}
				job.Normalize();
				GameObject patient = GameObject.FindByID(job.PatientId);
				if (job.SchemaQuarantined || !GameObject.Validate(patient)
					|| !CurrentAuthority(building, patient, System, job,
						KingdomLabRegistryStatus.Active)) continue;
				int need = building.GetIntProperty(KingdomAdopt.StaffNeededProperty);
				int crew = (need <= 0) ? 100 : ((building.GetIntProperty("KingdomStaffed") == 1)
					? building.GetIntProperty("KingdomEffectiveness") : 0);
				int wear = KingdomMaterialRules.ConditionPercent(KingdomWear.WearOf(building));
				KingdomLabJobAccrual accrual = KingdomLabRules.AccrueJob(job.LastWorkedTick,
					BoundaryTick, job.RemainingTicks, crew, wear, job.State,
					KingdomCrews.AffinityOf(building));
				job.LastWorkedTick = accrual.NextTick;
				job.RemainingTicks = accrual.RemainingTicks;
				job.State = accrual.Phase;
				if (job.State == KingdomLabJobPhase.Ready
					&& !KingdomLabRules.MessageSettled(
						(KingdomLabMessagePhase)job.ReadyMessagePhase))
				{
					KingdomLabMessagePhase phase = PublishMessage(ref job.ReadyMessagePhase,
						ref job.ReadyMessageText, job.ReadyMessageEventId,
						"{{G|The staffed work on " + job.FrozenName + " is ready at "
							+ KingdomLabRules.Named(KingdomPresentation.Rich(job.City))
							+ ". Return to the hall to complete the procedure.}}",
						ShouldPublish: !string.IsNullOrEmpty(job.FrozenName));
					job.ReadyAnnounced = phase == KingdomLabMessagePhase.Delivered;
				}
			}
		}

		private static void ManageJob(GameObject Building, GameObject Actor, KingdomSystem System,
			r_KingdomLabJob Job)
		{
			Job.Normalize();
			if (Job.SchemaQuarantined || !string.Equals(Job.PatientId, Actor?.ID,
				StringComparison.Ordinal))
			{
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = string.IsNullOrEmpty(Job.Fault)
					? "This job cannot prove its patient or immutable schema. It offers no action."
					: Job.Fault;
				Popup.Show(KingdomLabRules.JobProgressLine(Job.ProcedureKey, Job.State,
					Job.RemainingTicks, 0, false, false) + "\n" + Job.Fault);
				return;
			}
			KingdomLabRegistryStatus expected = Job.RegistryFinalized
				? (Job.State == KingdomLabJobPhase.Cancelled
					? KingdomLabRegistryStatus.Cancelled : KingdomLabRegistryStatus.Complete)
				: KingdomLabRegistryStatus.Active;
			if (!CurrentAuthority(Building, Actor, System, Job, expected))
			{
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = "The canonical patient/building/realm receipt is missing or disagrees. This hall cannot pay, cancel, apply, clean, or inherit the job.";
				Popup.Show(Job.Fault);
				return;
			}
			LabProcedure procedure = FrozenProcedure(Job);
			if (procedure == null)
			{
				Job.SchemaQuarantined = true;
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = "The frozen effect contract is invalid. The job is quarantined.";
				Popup.Show(Job.Fault);
				return;
			}
			bool staffed = Building.GetIntProperty(KingdomAdopt.StaffNeededProperty) <= 0
				|| Building.GetIntProperty("KingdomStaffed") == 1;
			bool wornOut = KingdomMaterialRules.ConditionPercent(KingdomWear.WearOf(Building)) <= 0;
			string receipt = "\n\npaid receipt: water " + Job.WaterPaid + "/" + Job.WaterOwed
				+ ((Job.WaterLost > Job.WaterPaid) ? (" (" + Job.WaterLost + " physically lost)") : "")
				+ (Job.WaterQuarantined ? " {{r|[measurement quarantined; automatic retry forbidden]}}" : "")
				+ ", kept " + Job.KeptPaid + "/" + Job.KeptOwed
				+ ", bits " + (string.IsNullOrEmpty(Job.BitOutstanding) ? "exact" : "outstanding")
				+ ", standing " + Job.StandingAppliedCount + "/" + Job.StandingFactions.Count
				+ " projected, body history " + BodyHistoryStatus(Job)
				+ ". Paid costs are not returned after commissioning."
				+ (string.IsNullOrEmpty(Job.BodyHistoryFault) ? ""
					: ("\nbody-history note: " + Job.BodyHistoryFault));
			string intro = KingdomLabRules.JobProgressLine(procedure.Named, Job.State,
				Job.RemainingTicks, procedure.StaffDays, staffed, wornOut) + receipt
				+ (string.IsNullOrEmpty(Job.Fault) ? "" : ("\n\n{{r|" + Job.Fault + "}}"));
			if (Job.State == KingdomLabJobPhase.Funding || Job.State == KingdomLabJobPhase.FundingRecovery)
			{
				string[] fundingOptions = Job.WaterQuarantined
					? new string[] { "Leave the quarantined receipt preserved.",
						"Cancel it; any measured payment is not returned." }
					: new string[] { "Retry the outstanding exact payment.", "Leave it preserved.",
						"Cancel it; any measured payment is not returned." };
				int picked = Popup.PickOption(Title: "recover commission funding", Intro: intro,
					Options: fundingOptions, AllowEscape: true);
				if (!Job.WaterQuarantined && picked == 0)
				{
					RecoverFunding(Building, Actor, System, Job, procedure);
				}
				else if (picked == (Job.WaterQuarantined ? 1 : 2)
					&& Popup.ShowYesNo("Cancel this persisted commission? Any water, bits, or kept parts already measured on its receipt are not returned.") == DialogResult.Yes)
				{
					Job.State = KingdomLabJobPhase.Cancelled;
					FinalizeApplicationProjection(Actor, Job, KingdomLabRegistryStatus.Cancelled);
				}
				return;
			}
			if (Job.State == KingdomLabJobPhase.Ready || Job.State == KingdomLabJobPhase.Applying
				|| Job.State == KingdomLabJobPhase.ApplicationRecovery)
			{
				int picked = Popup.PickOption(Title: "complete procedure", Intro: intro,
					Options: new string[] { "Finish or recover the terminal procedure.", "Leave it ready." }, AllowEscape: true);
				if (picked == 0)
				{
					ApplyJob(Building, Actor, System, Job, procedure);
				}
				return;
			}
			if (Job.State == KingdomLabJobPhase.Complete || Job.State == KingdomLabJobPhase.Cancelled)
			{
				if (Job.State == KingdomLabJobPhase.Complete)
				{
					FinishJobTellings(Actor, System, Job, procedure);
				}
				if (!ReferenceEquals(Job.ParentObject, Building)) return;
				if (Job.State == KingdomLabJobPhase.Complete
					&& Job.BodyHistoryState == KingdomLabBodyHistoryPhase.Pending)
				{
					int historyChoice = Popup.PickOption(Title: "completed procedure",
						Intro: intro + "\n\nPhysical and standing effects are complete. Civic history remains optional and pending; no effect will replay.",
						Options: new string[] { "Leave exact history pending.",
							"Finish without a civic-history row." }, AllowEscape: true);
					if (historyChoice == 1 && Popup.ShowYesNo(
						"Finish this physical receipt without adding its civic-history row?")
						== DialogResult.Yes)
					{
						OmitPendingBodyHistory(Job);
						PurgeApplicationReceipt(Building, Actor, System, Job,
							KingdomLabRegistryStatus.Complete);
					}
					return;
				}
				Popup.Show(intro);
				if (Job.State == KingdomLabJobPhase.Cancelled && !Job.RegistryFinalized)
					FinalizeApplicationProjection(Actor, Job, KingdomLabRegistryStatus.Cancelled);
				if (Job.State == KingdomLabJobPhase.Cancelled
					&& !KingdomLabRules.MessageSettled(
						(KingdomLabMessagePhase)Job.TerminalMessagePhase))
				{
					KingdomLabMessagePhase phase = PublishMessage(ref Job.TerminalMessagePhase,
						ref Job.TerminalMessageText, Job.AnnounceEventId,
						"{{K|The commission was cancelled. Its paid price was not returned.}}");
					Job.Announced = phase == KingdomLabMessagePhase.Delivered;
				}
				if (Job.RegistryFinalized && Job.MarkerCleaned
					&& KingdomLabRules.MessageSettled(
						(KingdomLabMessagePhase)Job.TerminalMessagePhase)
					&& (Job.State == KingdomLabJobPhase.Cancelled
						|| (Job.Chronicled && Job.Spoken)))
				{
					PurgeApplicationReceipt(Building, Actor, System, Job,
						Job.State == KingdomLabJobPhase.Cancelled
							? KingdomLabRegistryStatus.Cancelled
							: KingdomLabRegistryStatus.Complete);
				}
				return;
			}
			int choice = Popup.PickOption(Title: "commission in progress", Intro: intro,
				Options: new string[] { "Leave the crew to it.", "Cancel it; paid costs are not returned." }, AllowEscape: true);
			if (choice == 1 && Popup.ShowYesNo("Cancel this paid commission? Its water, bits, kept parts, and completed work are not returned.") == DialogResult.Yes)
			{
				Job.State = KingdomLabJobPhase.Cancelled;
				if (FinalizeApplicationProjection(Actor, Job, KingdomLabRegistryStatus.Cancelled)
					&& !KingdomLabRules.MessageSettled(
						(KingdomLabMessagePhase)Job.TerminalMessagePhase))
				{
					KingdomLabMessagePhase phase = PublishMessage(ref Job.TerminalMessagePhase,
						ref Job.TerminalMessageText, Job.AnnounceEventId,
						"{{K|The commission was cancelled. Its paid price was not returned.}}");
					Job.Announced = phase == KingdomLabMessagePhase.Delivered;
				}
				if (Job.RegistryFinalized && Job.MarkerCleaned
					&& KingdomLabRules.MessageSettled(
						(KingdomLabMessagePhase)Job.TerminalMessagePhase))
				{
					PurgeApplicationReceipt(Building, Actor, System, Job,
						KingdomLabRegistryStatus.Cancelled);
				}
			}
		}

	}
}
