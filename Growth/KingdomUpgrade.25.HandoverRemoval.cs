using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using XRL;
using XRL.Messages;
using XRL.UI;
using XRL.World;

using ThousandAndFirst;

namespace ThousandAndFirst
{
	using XRL.World.Parts;

	public static partial class KingdomUpgrade
	{
		private const string ImprovementRemovalReceipt = "improvement-handover:v2";
		private const string LegacyImprovementRemovalReceipt = "improvement-handover:v1";

		private static bool TryRemoveHandoverPredecessor(GameObject Predecessor,
			GameObject Successor, Cell cell, string predecessorId, string SuccessorKey,
			r_KingdomImprovement intent, KingdomSystem ownerSystem,
			ref KingdomConstructionJob job, int carriedLiquid, int carriedItems,
			out string predecessorName)
		{
			predecessorName = null;
			// The successor was indexed when the scaffold first landed it. Plot growth and
			// CarryMarks happen later and can add plot, larder, stores, yielding, visual, and other
			// semantic memberships. Refresh the same bound index before any exact removal proof or
			// the immediate improvement follow-on consumes it.
			KingdomSurvey activeSurvey = KingdomSurvey.ActiveFor(Successor.CurrentZone);
			if (activeSurvey != null && !activeSurvey.ObserveChanged(Successor))
			{
				r_KingdomImprovement.FailHandover(intent,
					"The improved successor could not refresh the active settlement survey.");
				if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return false;
			}
			predecessorName = KingdomDesign.ReferenceFor(Predecessor, Predecessor.ShortDisplayName);
			LiquidVolume remaining = Predecessor.GetPart<LiquidVolume>();
			if (remaining != null && remaining.Volume > 0 && cell != null)
			{
				r_KingdomImprovement.FailHandover(intent,
					"Liquid reappeared after the exact handover receipt settled.");
				if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return false;
			}
			GameObject exactPredecessor;
			GameObject exactSuccessor;
			if (!GameObject.Validate(Predecessor) || Predecessor.CurrentCell != cell
				|| Successor.CurrentCell != cell || Successor.GetIntProperty(BuiltProperty) != 1
				|| Successor.GetStringProperty(BuildKeyProperty) != SuccessorKey
				|| (job != null && (!KingdomConstruction.HasReceipt(Predecessor, job)
					|| !r_KingdomScaffold.IsExactSuccessor(Successor,
						Predecessor.CurrentZone, cell, job, intent.SuccessorBlueprint)
						|| !KingdomConstruction.Owns(ownerSystem, Predecessor.CurrentZone, job)
						|| KingdomConstruction.FindGlobalPredecessorAuthority(
							job, Successor, out exactPredecessor) != KingdomPhysicalLookupState.Exact
						|| !ReferenceEquals(exactPredecessor, Predecessor)
						|| KingdomConstruction.FindExactId(Predecessor.CurrentZone,
							Successor.IDIfAssigned, out exactSuccessor) != KingdomPhysicalLookupState.Exact
						|| !ReferenceEquals(exactSuccessor, Successor)
						|| !ExactPendingRemovalProof(Successor, intent.Scaffold.IDIfAssigned,
							predecessorId, job)
					|| !KingdomConstruction.IsCurrent(job))))
			{
				r_KingdomImprovement.FailHandover(intent,
					"The improved successor could not be verified before handover.");
				if (job != null) KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return false;
			}
			string contentFailure = null;
			if (job != null && (!r_KingdomScaffold.IsExactPendingImprovementSuccessor(Successor)
				|| !r_KingdomImprovement.VerifyHandoverContentCustody(Predecessor,
					Successor, cell, intent, true, out contentFailure)
				|| !TryPublishRemovalIntent(ref job, predecessorId, Successor.IDIfAssigned,
					carriedItems, carriedLiquid)))
			{
				r_KingdomImprovement.FailHandover(intent,
					contentFailure
					?? "The final predecessor-removal intent could not be published exactly.");
				if (job != null && KingdomConstruction.IsCurrent(job))
					KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
				return false;
			}
			if (job != null)
			{
				string proof = Successor.GetStringProperty(r_KingdomScaffold.RemovalProofProperty);
				if (Successor.HasIntProperty(r_KingdomScaffold.RemovalProofProperty)
					|| proof != predecessorId && proof != intent.Scaffold.IDIfAssigned)
				{
					r_KingdomImprovement.FailHandover(intent,
						"The successor carries foreign predecessor-removal proof.");
					KingdomConstruction.Quarantine(ref job, intent.HandoverFailure);
					return false;
				}
				Successor.SetStringProperty(r_KingdomScaffold.RemovalProofProperty, predecessorId);
				if (!r_KingdomScaffold.HasRemovalProof(Successor, predecessorId))
					return QuarantineRemoval(intent, ref job,
						"The successor did not retain pending predecessor-removal proof.");
			}
			bool removed;
			string removalException = null;
			try
			{
				removed = Predecessor.Destroy(null, Silent: true);
			}
			catch (System.Exception ex)
			{
				removed = false;
				removalException = ex.Message;
			}
			KingdomSurvey.ObserveCurrentTopologyInActive(Successor.CurrentZone, Predecessor);
			GameObject afterPredecessor;
			KingdomPhysicalLookupState predecessorState = job == null
				? KingdomConstruction.FindGlobalLiveId(predecessorId, out afterPredecessor)
				: KingdomConstruction.FindGlobalPredecessorAuthority(job, Successor,
					out afterPredecessor);
			bool directReferenceLive = GameObject.Validate(Predecessor);
			bool exactReference = ReferenceEquals(afterPredecessor, Predecessor);
			bool identityMatches = directReferenceLive
				&& Predecessor.IDIfAssigned == predecessorId
				&& (job == null || KingdomConstruction.HasReceipt(Predecessor, job)
					&& intent.HandoverSourceId == predecessorId
					&& intent.HandoverConstructionReceipt == job.Id);
			bool groundMatches = directReferenceLive && Predecessor.CurrentCell == cell;
			KingdomExactRemovalAction aftermath =
				KingdomConstructionRules.ImprovementRemovalAftermath(predecessorState,
					directReferenceLive, exactReference, identityMatches, groundMatches);
			if (aftermath == KingdomExactRemovalAction.InvokeOnce
				&& Successor.CurrentCell == cell)
			{
				KingdomSurvey.ObserveCurrentTopologyInActive(Successor.CurrentZone, Predecessor);
				intent.HandoverFailure = "Improvement removal "
					+ (removed ? "reported success without an exact effect"
						: "was vetoed without an exact effect")
					+ (removalException == null ? "." : ": " + removalException);
				if (job != null) KingdomConstruction.FinishProjection(ref job, false, false,
					intent.HandoverFailure);
				return false;
			}
			if (aftermath != KingdomExactRemovalAction.ProvedAbsent
				|| Successor.CurrentCell != cell)
			{
				return QuarantineRemoval(intent, ref job,
					"Improvement removal moved or ambiguously changed an endpoint.");
			}
			if (job != null && !TryRecoverAbsentHandover(ownerSystem, Successor.CurrentZone,
				Successor, ref job, out string recoveryFailure))
				return QuarantineRemoval(intent, ref job, recoveryFailure);
			return true;
		}

		internal static bool TryRecoverAbsentHandover(KingdomSystem System, Zone Z,
			GameObject Successor, ref KingdomConstructionJob Job, out string Failure)
		{
			Failure = null;
			GameObject exact;
			KingdomRules.BuildEntry entry;
			if (System == null || Z == null || Job == null || !GameObject.Validate(Successor)
				|| Job.Route != KingdomConstructionRoute.Improvement
				|| !KingdomConstruction.Owns(System, Z, Job) || !KingdomConstruction.IsCurrent(Job)
				|| !KingdomData.TryGetBuilding(Job.TargetKey, out entry)
				|| Successor.IDIfAssigned != Job.OutputId || Successor.CurrentCell != Z.GetCell(Job.X, Job.Y)
				|| KingdomConstruction.FindGlobalPredecessorAuthority(Job, Successor, out _)
					!= KingdomPhysicalLookupState.Absent
				|| KingdomConstruction.FindExactId(Z, Job.OutputId, out exact)
					!= KingdomPhysicalLookupState.Exact || !ReferenceEquals(exact, Successor)
				|| !r_KingdomScaffold.IsExactSuccessor(Successor, Z,
					Z.GetCell(Job.X, Job.Y), Job, entry.Blueprint)
				|| Successor.HasIntProperty(r_KingdomScaffold.RemovalProofProperty)
				|| !ExactRecoverableRemovalReceipt(Job)
				|| !r_KingdomScaffold.HasRemovalProof(Successor,
					Job.SubjectId))
			{
				Failure = "Final improvement-removal evidence is absent, duplicated, or changed.";
				return false;
			}
			bool legacyZeroContent = !ExactRemovalReceipt(Job)
				&& ExactRecoverableRemovalReceipt(Job);
			if (!legacyZeroContent
				&& (!r_KingdomImprovement.VerifySettledHandoverContentCustody(Successor,
					Job.Id, out int settledItems, out int settledLiquid, out Failure)
					|| settledItems != Job.PhysicalIndex
					|| settledLiquid != Job.PhysicalAmount))
			{
				Failure = Failure ?? "Settled handover contents disagree with the removal receipt.";
				return false;
			}
			if (Job.PhysicalPhase == KingdomPhysicalPhase.FinalRemovalPending
				&& !KingdomConstruction.UpdatePhysical(ref Job, KingdomPhysicalPhase.FinalRemoved,
					Job.PhysicalIndex, Job.PhysicalAmount, 0, Job.SubjectId, Job.OutputId,
					ImprovementRemovalReceipt))
			{
				Failure = "Exact predecessor absence could not be committed to its receipt.";
				return false;
			}
			if (!KingdomArchitectureStamper.TryRetirePendingUpgradeComponents(Successor, Z,
				out Failure)) return false;
			KingdomSurvey active = KingdomSurvey.ActiveFor(Z);
			if (active != null && !active.ObserveChanged(Successor))
			{
				Failure = "The completed successor could not refresh its active survey identity.";
				return false;
			}
			if (!KingdomConstruction.Complete(ref Job))
			{
				Failure = "The physically closed improvement receipt could not complete.";
				return false;
			}
			if (!r_KingdomScaffold.TellCompletion(System, Successor, Job))
			{
				Failure = "The completed improvement could not settle its exact telling outbox.";
				return false;
			}
			if (!r_KingdomImprovement.TryRetireHandoverContentCustody(Successor, Job,
				out Failure)) return false;
			return true;
		}

		private static bool TryPublishRemovalIntent(ref KingdomConstructionJob Job,
			string PredecessorId, string SuccessorId, int Items, int Liquid)
		{
			if (Job.PhysicalPhase == KingdomPhysicalPhase.FinalRemovalPending)
				return Job.Phase == KingdomConstructionPhase.ProjectionPending
					&& ExactRemovalReceipt(Job) && Job.PhysicalIndex == Items
					&& Job.PhysicalAmount == Liquid && Job.PhysicalItemId == PredecessorId
					&& Job.PhysicalDestinationId == SuccessorId;
			if (Job.PhysicalPhase != KingdomPhysicalPhase.None
				|| Job.Phase != KingdomConstructionPhase.ProjectionPending
				|| Job.SubjectId != PredecessorId || Job.OutputId != SuccessorId
				|| Items < 0 || Items > 4096 || Liquid < 0) return false;
			return KingdomConstruction.UpdatePhysical(ref Job,
				KingdomPhysicalPhase.FinalRemovalPending, Items, Liquid, 0, PredecessorId,
				SuccessorId, ImprovementRemovalReceipt);
		}

		private static bool ExactRemovalReceipt(KingdomConstructionJob Job)
		{
			return Job != null && (Job.PhysicalPhase == KingdomPhysicalPhase.FinalRemovalPending
				|| Job.PhysicalPhase == KingdomPhysicalPhase.FinalRemoved)
				&& Job.PhysicalIndex >= 0 && Job.PhysicalIndex <= 4096
				&& Job.PhysicalAmount >= 0 && Job.PhysicalSpilled == 0
				&& Job.PhysicalItemId == Job.SubjectId
				&& Job.PhysicalDestinationId == Job.OutputId
				&& Job.PhysicalReceipt == ImprovementRemovalReceipt;
		}

		/// <summary>
		/// The old v1 transaction had no durable content manifest. Its one recoverable crash cut is
		/// therefore a predecessor already proved absent with an immutable zero-item/zero-liquid
		/// receipt. Anything non-empty remains inspection-only: identities and liquid composition
		/// cannot be reconstructed after the source is gone.
		/// </summary>
		private static bool ExactRecoverableRemovalReceipt(KingdomConstructionJob Job)
		{
			return ExactRemovalReceipt(Job) || Job != null
				&& Job.PhysicalPhase == KingdomPhysicalPhase.FinalRemoved
				&& Job.PhysicalIndex == 0 && Job.PhysicalAmount == 0
				&& Job.PhysicalSpilled == 0 && Job.PhysicalItemId == Job.SubjectId
				&& Job.PhysicalDestinationId == Job.OutputId
				&& Job.PhysicalReceipt == LegacyImprovementRemovalReceipt;
		}

		private static bool ExactPendingRemovalProof(GameObject Successor, string ScaffoldId,
			string PredecessorId, KingdomConstructionJob Job)
		{
			string proof = Successor.GetStringProperty(r_KingdomScaffold.RemovalProofProperty);
			return !Successor.HasIntProperty(r_KingdomScaffold.RemovalProofProperty)
				&& (proof == ScaffoldId || Job != null
					&& Job.PhysicalPhase == KingdomPhysicalPhase.FinalRemovalPending
					&& proof == PredecessorId);
		}

		private static bool QuarantineRemoval(r_KingdomImprovement Intent,
			ref KingdomConstructionJob Job, string Failure)
		{
			r_KingdomImprovement.FailHandover(Intent, Failure);
			if (Job != null) KingdomConstruction.Quarantine(ref Job, Intent.HandoverFailure);
			return false;
		}
	}
}
