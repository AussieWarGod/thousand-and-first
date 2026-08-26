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
		private static void AttemptRemoval(GameObject Actor, KingdomSystem System,
			r_KingdomLabRemovalJob Job, LabProcedure Procedure)
		{
			if (!CurrentRemovalAuthority(Actor, System, Job)) return;
			if (KingdomLabRules.RemovalFundingPhase(Job.WaterOwed, Job.WaterPaid,
				Job.WaterQuarantined) != KingdomLabRemovalPhase.Paid)
			{
				Job.State = Job.WaterQuarantined ? KingdomLabRemovalPhase.Quarantined
					: KingdomLabRemovalPhase.FundingRecovery;
				return;
			}
			KingdomLabOwnershipSnapshot snapshot = RemovalSnapshot(Job);
			KingdomLabOwnedTarget ignored;
			KingdomLabOwnedTargetState before = KingdomProcedures.ClassifyOwned(Actor,
				snapshot, out ignored);
			if (before == KingdomLabOwnedTargetState.Absent)
			{
				CompleteRemoval(Actor, System, Job, Procedure, snapshot);
				return;
			}
			if (before != KingdomLabOwnedTargetState.Present)
			{
				Job.State = KingdomLabRemovalPhase.Quarantined;
				Job.Fault = "The exact tracked effect cannot be distinguished from a foreign same-class replacement. Nothing was touched.";
				return;
			}
			Job.State = KingdomLabRemovalPhase.Removing;
			KingdomLabOwnedTargetState after;
			try
			{
				after = KingdomProcedures.RemoveExact(Actor, Procedure, snapshot);
			}
			catch (Exception ex)
			{
				KingdomLog.Log("lab: exact removal threw (" + ex.Message + ")");
					after = KingdomProcedures.ClassifyOwned(Actor, snapshot, out ignored);
				Job.Fault = "The exact engine removal threw: " + ex.Message;
			}
			Job.State = KingdomLabRules.RemovalObservation(after, RemovingStarted: true);
			if (after == KingdomLabOwnedTargetState.Absent)
			{
				CompleteRemoval(Actor, System, Job, Procedure, snapshot);
			}
			else if (after == KingdomLabOwnedTargetState.Present)
			{
				Job.Fault = "The exact owned effect remains present. Retry charges no more water.";
			}
			else
			{
				Job.Fault = "Removal returned an uncertain identity. The receipt is quarantined; no class scan will be attempted.";
			}
		}

		private static void CompleteRemoval(GameObject Actor, KingdomSystem System,
			r_KingdomLabRemovalJob Job, LabProcedure Procedure,
			KingdomLabOwnershipSnapshot Snapshot)
		{
			Job.EffectRemoved = true;
			Job.State = KingdomLabRemovalPhase.Removed;
			EnsureRemovalGovernance(Job);
			FinishRemoval(Actor, System, Job, Procedure, Snapshot);
		}

		private static void FinishRemoval(GameObject Actor, KingdomSystem System,
			r_KingdomLabRemovalJob Job, LabProcedure Procedure)
		{
			KingdomLabOwnershipSnapshot snapshot = RemovalSnapshot(Job);
			FinishRemoval(Actor, System, Job, Procedure, snapshot);
		}

		private static void FinishRemoval(GameObject Actor, KingdomSystem System,
			r_KingdomLabRemovalJob Job, LabProcedure Procedure,
			KingdomLabOwnershipSnapshot Snapshot)
		{
			KingdomLabOwnedTarget ignored;
			KingdomLabOwnedTargetState observed = KingdomProcedures.ClassifyOwned(Actor,
				Snapshot, out ignored);
			if (observed == KingdomLabOwnedTargetState.Present)
			{
				Job.State = KingdomLabRemovalPhase.RemovalRecovery;
				Job.Fault = "The exact effect is present again. Terminal cleanup and tellings were stopped; retry removes only that identity.";
				return;
			}
			if (observed != KingdomLabOwnedTargetState.Absent)
			{
				Job.State = KingdomLabRemovalPhase.Quarantined;
				Job.Fault = "Terminal observation is uncertain. No marker, record, chronicle, or message was changed.";
				return;
			}
			try { Actor.WantToReequip(); }
			catch (Exception ex)
			{
				KingdomLog.Log("lab: post-removal reequip callback threw (" + ex.Message + ")");
			}
			observed = KingdomProcedures.ClassifyOwned(Actor, Snapshot, out ignored);
			if (observed != KingdomLabOwnedTargetState.Absent)
			{
				Job.State = observed == KingdomLabOwnedTargetState.Present
					? KingdomLabRemovalPhase.RemovalRecovery : KingdomLabRemovalPhase.Quarantined;
				Job.Fault = "The exact target changed during terminal callbacks. Tellings were stopped.";
				return;
			}
			if (!Job.Chronicled)
			{
				try
				{
					Job.Chronicled = KingdomChronicle.RecordOnce(System,
						Job.ChronicleEventId, KingdomLabRules.RemovedTelling(
							Job.FrozenName, KingdomPresentation.Rich(Job.City)));
				}
				catch (Exception ex)
				{
					Job.Fault = "The keyed chronicle outbox stopped: " + ex.Message;
					return;
				}
				if (!Job.Chronicled) return;
			}
			observed = KingdomProcedures.ClassifyOwned(Actor, Snapshot, out ignored);
			if (observed != KingdomLabOwnedTargetState.Absent)
			{
				Job.State = observed == KingdomLabOwnedTargetState.Present
					? KingdomLabRemovalPhase.RemovalRecovery : KingdomLabRemovalPhase.Quarantined;
				Job.Fault = "The exact target changed during chronicle publication. The completion message was stopped.";
				return;
			}
			if (!KingdomLabRules.MessageSettled(
				(KingdomLabMessagePhase)Job.TerminalMessagePhase))
			{
				KingdomLabMessagePhase phase = PublishMessage(ref Job.TerminalMessagePhase,
					ref Job.TerminalMessageText, Job.AnnounceEventId,
					"{{K|It is off. Nothing was given back for it.}}");
				Job.Announced = phase == KingdomLabMessagePhase.Delivered;
			}
			observed = KingdomProcedures.ClassifyOwned(Actor, Snapshot, out ignored);
			if (observed != KingdomLabOwnedTargetState.Absent)
			{
				Job.State = observed == KingdomLabOwnedTargetState.Present
					? KingdomLabRemovalPhase.RemovalRecovery : KingdomLabRemovalPhase.Quarantined;
				Job.Fault = "The exact target changed during completion presentation. Completion was revoked.";
				return;
			}
			// Ownership proof stays live through every external callback above. This is
			// essential for modifier-only mutation removal: the exact unlisted runtime part
			// proves our contribution absent until all tellings have settled.
			if (!Job.OwnershipCleaned)
			{
				if (!KingdomProcedures.CleanupOwned(Actor, Procedure, Snapshot))
				{
					Job.State = KingdomLabRemovalPhase.RemovalRecovery;
					Job.Fault = "Exact ownership-marker cleanup did not complete. It will retry without touching a replacement.";
					return;
				}
				Job.OwnershipCleaned = true;
				Job.RecordCleaned = true;
			}
			if (!Job.Chronicled || !KingdomLabRules.MessageSettled(
				(KingdomLabMessagePhase)Job.TerminalMessagePhase)) return;
			if (!RecordReplayProof("remove:" + Job.RemovalId))
			{
				Job.State = KingdomLabRemovalPhase.RemovalRecovery;
				Job.Fault = "The bounded replay proof could not be persisted; the exact tombstone and removal receipt remain.";
				return;
			}
			if (!KingdomProcedures.PurgeOwnedTombstone(Actor, Snapshot))
			{
				Job.State = KingdomLabRemovalPhase.RemovalRecovery;
				Job.Fault = "The exact effect tombstone could not be purged after all tellings settled.";
				return;
			}
			Job.Fault = "";
			Job.State = KingdomLabRemovalPhase.Complete;
			try { Actor.RemovePart(Job); }
			catch (Exception ex)
			{
				KingdomLog.Log("lab: terminal removal receipt cleanup threw (" + ex.Message + ")");
			}
		}

		private static bool ArchiveCleanAbsentRemoval(GameObject Actor,
			r_KingdomLabRemovalJob Job, LabProcedure Procedure,
			KingdomLabOwnershipSnapshot Snapshot)
		{
			if (!KingdomProcedures.CleanupOwned(Actor, Procedure, Snapshot))
			{
				Job.State = KingdomLabRemovalPhase.Quarantined;
				Job.Fault = "The unspent receipt could not prove exact marker cleanup. It remains quarantined and offers no charge or body callback.";
				return false;
			}
			Job.OwnershipCleaned = true;
			Job.RecordCleaned = true;
			Job.State = KingdomLabRemovalPhase.Cancelled;
			Job.Fault = "The exact effect was already absent. This clean receipt was archived without charge, governance, or success tellings.";
			return true;
		}

		private static void DiscardCleanRemovalReceipt(GameObject Actor,
			r_KingdomLabRemovalJob Job)
		{
			if (Actor == null || Job == null || Job.WaterPaid != 0 || Job.WaterLost != 0
				|| Job.WaterQuarantined || !Job.WaterMeasurementExact
				|| !string.Equals(Job.PatientId, Actor.ID, StringComparison.Ordinal)) return;
			Job.State = KingdomLabRemovalPhase.Cancelled;
			Job.Fault = "Clean unspent receipt discarded; no procedure debt was inherited.";
			try { Actor.RemovePart(Job); }
			catch (Exception ex)
			{
				KingdomLog.Log("lab: clean removal-receipt discard threw after durable cancellation ("
					+ ex.Message + ")");
			}
		}

		private static void EnsureRemovalGovernance(r_KingdomLabRemovalJob Job)
		{
			if (Job == null || Job.GovernanceCommitted)
			{
				return;
			}
			bool durable = Job.WaterPaid > 0 || Job.WaterLost > 0 || Job.WaterQuarantined
				|| Job.EffectRemoved;
			if (durable && KingdomGovernanceScope.Commit("remove lab procedure"))
			{
				Job.GovernanceCommitted = true;
			}
		}

		private static string RemovalReceipt(r_KingdomLabRemovalJob Job,
			LabProcedure Procedure)
		{
			return KingdomLabRules.Named(Procedure.Named) + " removal: water {{C|"
				+ Job.WaterPaid + "/" + Job.WaterOwed + "}} measured"
				+ ((Job.WaterLost > Job.WaterPaid) ? (" (" + Job.WaterLost
					+ " physically lost)") : "")
				+ (Job.WaterQuarantined ? " {{r|[quarantined]}}" : "")
				+ "; phase {{W|" + Job.State + "}}."
				+ (string.IsNullOrEmpty(Job.Fault) ? "" : ("\n\n{{r|" + Job.Fault + "}}"));
		}
	}
}
