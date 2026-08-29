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
		private static void ApplyJob(GameObject Building, GameObject Actor, KingdomSystem System,
			r_KingdomLabJob Job, LabProcedure Procedure)
		{
			if (!CurrentAuthority(Building, Actor, System, Job, KingdomLabRegistryStatus.Active)
				|| !string.Equals(Actor?.ID, Job.PatientId, StringComparison.Ordinal)
				|| Job.SchemaQuarantined || Procedure == null)
			{
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = "The canonical paid job does not authorize this patient, hall, realm, or frozen contract.";
				Popup.Show(Job.Fault);
				return;
			}
			KingdomLabOwnershipSnapshot snapshot;
			KingdomLabOwnedTargetState observed = SnapshotJobEffect(Actor, Procedure, Job,
				out snapshot);
			if (observed == KingdomLabOwnedTargetState.Uncertain && !Job.EffectCommitted)
			{
				if (!KingdomProcedures.HasProcedureClass(Actor, Procedure))
					observed = KingdomLabOwnedTargetState.Absent;
			}
			if (observed == KingdomLabOwnedTargetState.Absent && Job.EffectCommitted)
			{
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = "The once-committed exact effect is absent. Recovery will not create a second instance or adopt a replacement.";
				return;
			}
			if (observed == KingdomLabOwnedTargetState.Uncertain)
			{
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = "The exact effect receipt is uncertain or a foreign same-class effect exists. The hall will neither duplicate nor adopt it.";
				Popup.Show(Job.Fault + " The paid job remains ready for recovery.");
				return;
			}
			Job.State = KingdomLabJobPhase.Applying;
			if (observed == KingdomLabOwnedTargetState.Absent)
			{
				if (!ValidApplicationTarget(Actor, Job, Procedure))
				{
					Job.State = KingdomLabJobPhase.ApplicationRecovery;
					Job.Fault = "The frozen patient slot/bearer changed or a foreign effect appeared before body mutation.";
					return;
				}
				Job.IntentPublished = true;
				KingdomLabGrantAttempt attempt = KingdomProcedures.GrantAtExact(Actor, Procedure,
					Job.BodyPartId, Job.BearerId, Job.Stamp, Job.JobId, Job.FrozenManager,
					Job.FrozenDetail, Job.FrozenFingerprint);
				if (attempt.State == KingdomLabOwnedTargetState.Present)
				{
					Job.EffectBodyPartId = attempt.BodyPartId;
					Job.EffectPartOrdinal = attempt.PartOrdinal;
				}
				observed = SnapshotJobEffect(Actor, Procedure, Job, out snapshot);
				if (observed != KingdomLabOwnedTargetState.Present)
				{
					Job.State = KingdomLabJobPhase.ApplicationRecovery;
					Job.Fault = string.IsNullOrEmpty(attempt.Failure)
						? "The exact body mutation did not publish a recoverable owned effect."
						: attempt.Failure;
					return;
				}
			}
			Job.EffectCommitted = true;
			Job.EffectBodyPartId = snapshot.BodyPartId;
			Job.EffectPartOrdinal = snapshot.PartOrdinal;
			if (!RepairProcedureOwnership(Actor, Procedure, Job, snapshot))
			{
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = "The exact effect is present, but its owner marker/patient receipt needs repair. It was not announced as complete.";
				Popup.Show(Job.Fault);
				return;
			}
			Job.OwnershipPublished = true;
			while (Job.StandingAppliedCount < Job.StandingFactions.Count
				&& Job.StandingAppliedCount < Job.StandingDeltas.Count)
			{
				int at = Job.StandingAppliedCount;
				KingdomLabStandingPhase standingPhase = (KingdomLabStandingPhase)
					Job.StandingPhases[at];
				if (standingPhase == KingdomLabStandingPhase.Pending)
				{
					Job.StandingBefore[at] =
						System.GetRegardForRealm(Job.StandingFactions[at]);
					Job.StandingTargets[at] = KingdomLabRules.StandingAfter(
						Job.StandingBefore[at], Job.StandingDeltas[at]);
					Job.StandingPhases[at] = (int)KingdomLabStandingPhase.Bound;
					standingPhase = KingdomLabStandingPhase.Bound;
				}
				int currentStanding =
					System.GetRegardForRealm(Job.StandingFactions[at]);
				standingPhase = KingdomLabRules.ObserveStanding(standingPhase,
					currentStanding, Job.StandingBefore[at], Job.StandingTargets[at]);
				Job.StandingPhases[at] = (int)standingPhase;
				if (standingPhase == KingdomLabStandingPhase.Applied)
				{
					Job.StandingAppliedCount++;
					continue;
				}
				if (standingPhase != KingdomLabStandingPhase.Bound)
				{
					Job.State = KingdomLabJobPhase.ApplicationRecovery;
					Job.Fault = "Standing changed outside the exact before/delta/after receipt. The hall will not overwrite the interleaving value.";
					return;
				}
				if (SnapshotJobEffect(Actor, Procedure, Job, out snapshot)
					!= KingdomLabOwnedTargetState.Present)
				{
					Job.State = KingdomLabJobPhase.ApplicationRecovery;
					Job.Fault = "The exact effect changed before a standing callback. No further standing was applied.";
					return;
				}
				try
				{
					Job.StandingPhases[at] = (int)KingdomLabStandingPhase.Intent;
					System.SetRegardForRealm(Job.StandingFactions[at],
						Job.StandingTargets[at]);
				}
				catch (Exception ex)
				{
					Job.State = KingdomLabJobPhase.ApplicationRecovery;
					Job.Fault = "Standing callback threw after intent. Recovery will observe the exact after-value once and never write again: " + ex.Message;
					return;
				}
				standingPhase = KingdomLabRules.ObserveStanding(KingdomLabStandingPhase.Intent,
					System.GetRegardForRealm(Job.StandingFactions[at]),
					Job.StandingBefore[at],
					Job.StandingTargets[at]);
				Job.StandingPhases[at] = (int)standingPhase;
				if (standingPhase != KingdomLabStandingPhase.Applied)
				{
					Job.State = KingdomLabJobPhase.ApplicationRecovery;
					Job.Fault = "Standing callback did not leave the exact after-value. The interleaving value is preserved and the receipt is quarantined.";
					return;
				}
				if (SnapshotJobEffect(Actor, Procedure, Job, out snapshot)
					!= KingdomLabOwnedTargetState.Present)
				{
					Job.State = KingdomLabJobPhase.ApplicationRecovery;
					Job.Fault = "The exact effect changed during a standing callback. Recovery is quarantined from touching replacements.";
					return;
				}
				Job.StandingAppliedCount++;
			}
			Job.StandingApplied = Job.StandingAppliedCount >= Job.StandingFactions.Count;
			if (!Job.StandingApplied)
			{
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = "The effect is present, but its standing receipt is incomplete. Retry to finish bookkeeping.";
				return;
			}
			if (SnapshotJobEffect(Actor, Procedure, Job, out snapshot)
				!= KingdomLabOwnedTargetState.Present)
			{
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				Job.Fault = "The exact effect changed before terminal cleanup.";
				return;
			}
			SettleCompletedBodyHistory(Actor, System, Job);
			if (!FinalizeApplicationProjection(Actor, Job, KingdomLabRegistryStatus.Complete))
			{
				Job.State = KingdomLabJobPhase.ApplicationRecovery;
				return;
			}
			Job.State = KingdomLabJobPhase.Complete;
			Job.Fault = "";
			FinishJobTellings(Actor, System, Job, Procedure);
		}

		private static void FinishJobTellings(GameObject Actor, KingdomSystem System,
			r_KingdomLabJob Job, LabProcedure Procedure)
		{
			if (!ExactJobEffectPresent(Actor, Procedure, Job))
			{
				QuarantineApplicationTelling(Job,
					"The exact effect changed before terminal publication. No further telling was attempted.");
				return;
			}
			if (!Job.Chronicled)
			{
				try
				{
					Job.Chronicled = KingdomChronicle.RecordOnce(System, Job.ChronicleEventId,
						KingdomLabRules.DoneTelling(Job.FrozenName,
							KingdomPresentation.Rich(Job.City)));
				}
				catch (Exception ex)
				{
					KingdomLog.Log("lab: chronicle intent " + Job.ChronicleEventId
						+ " threw after publication (" + ex.Message + ")");
				}
			}
			if (!ExactJobEffectPresent(Actor, Procedure, Job))
			{
				QuarantineApplicationTelling(Job,
					"The exact effect changed during chronicle publication. Petition and message publication stopped.");
				return;
			}
			if (!Job.Spoken)
			{
				try { Job.Spoken = Speak(System, Actor, Procedure, Job); }
				catch (Exception ex)
				{
					Job.Fault = "The keyed petition outbox stopped: " + ex.Message;
					return;
				}
			}
			if (!ExactJobEffectPresent(Actor, Procedure, Job))
			{
				QuarantineApplicationTelling(Job,
					"The exact effect changed during petition publication. Completion messaging stopped.");
				return;
			}
			if (!KingdomLabRules.MessageSettled(
				(KingdomLabMessagePhase)Job.TerminalMessagePhase))
			{
				KingdomLabMessagePhase phase = PublishMessage(ref Job.TerminalMessagePhase,
					ref Job.TerminalMessageText, Job.AnnounceEventId,
					KingdomLabRules.DoneLine(Job.FrozenName,
						KingdomPresentation.Rich(Job.City)));
				Job.Announced = phase == KingdomLabMessagePhase.Delivered;
			}
			if (!ExactJobEffectPresent(Actor, Procedure, Job))
			{
				QuarantineApplicationTelling(Job,
					"The exact effect changed during completion presentation. The hall receipt is quarantined and no replacement will be touched.");
				return;
			}
			if (Job.State == KingdomLabJobPhase.Complete && Job.Chronicled && Job.Spoken
				&& KingdomLabRules.MessageSettled(
					(KingdomLabMessagePhase)Job.TerminalMessagePhase))
			{
				PurgeApplicationReceipt(Job.ParentObject, Actor, System, Job,
					KingdomLabRegistryStatus.Complete);
			}
		}

	}
}
