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
		internal static void InspectConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| Job.Route != KingdomConstructionRoute.Improvement
				|| !KingdomData.TryGetBuilding(Job.TargetKey, out var successor)) return;
			if (Job.Phase == KingdomConstructionPhase.Complete)
			{
				GameObject completed;
				int completedCount = r_KingdomScaffold.FindExactSuccessors(Z, Job,
					successor.Blueprint, null, out completed);
				if (completedCount > 1)
				{
					KingdomConstructionJob duplicate = Job;
					KingdomConstruction.Quarantine(ref duplicate,
						"More than one terminal improvement successor carries this receipt.");
				}
				else if (completedCount == 1)
				{
					if (!r_KingdomScaffold.HasRemovalProof(completed, Job.SubjectId))
					{
						KingdomConstructionJob unproved = Job;
						KingdomConstruction.Quarantine(ref unproved,
							"The terminal improvement successor lacks predecessor-removal proof.");
					}
					else r_KingdomScaffold.TellCompletion(System, completed, Job);
				}
				return;
			}
			GameObject work;
			KingdomPhysicalLookupState workState = KingdomConstruction.FindExactId(
				Z, Job.SubjectId, out work);
			if (workState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstructionJob duplicate = Job;
				KingdomConstruction.Quarantine(ref duplicate,
					"The improvement predecessor ID resolves to more than one loaded object.");
				return;
			}
			if (!EnsureExactImprovementPredecessor(System, Z, work, Job))
			{
				KingdomConstructionJob absent = Job;
				GameObject completed;
				int completedCount = r_KingdomScaffold.FindExactSuccessors(Z, Job,
					successor.Blueprint, null, out completed);
				if (completedCount == 1
					&& r_KingdomScaffold.HasRemovalProof(completed, Job.SubjectId))
				{
					if (KingdomConstruction.Complete(ref absent))
						r_KingdomScaffold.TellCompletion(System, completed, absent);
				}
				else
				{
					KingdomConstruction.Quarantine(ref absent, completedCount > 1
						? "More than one exact improvement successor carries this receipt."
						: "The improvement predecessor moved, changed, or disappeared without exact removal proof.");
				}
				return;
			}
			r_KingdomImprovement carriedIntent = GameObject.Validate(work)
				? work.GetPart<r_KingdomImprovement>() : null;
			Cell expectedCell = Z.GetCell(Job.X, Job.Y);
			GameObject exactScaffold;
			KingdomPhysicalLookupState scaffoldState = FindImprovementScaffold(
				expectedCell, successor, Job, out exactScaffold);
			if (scaffoldState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstructionJob duplicate = Job;
				KingdomConstruction.Quarantine(ref duplicate,
					"The improvement scaffold is duplicated, moved, replaced, or malformed.");
				return;
			}
			if (carriedIntent != null && carriedIntent.Working
				&& GameObject.Validate(carriedIntent.Scaffold)
				&& (!ExpectedImprovementScaffold(carriedIntent.Scaffold, expectedCell, successor, Job)
					|| !KingdomConstruction.HasReceipt(carriedIntent.Scaffold, Job)))
			{
				KingdomConstructionJob moved = Job;
				KingdomConstruction.Quarantine(ref moved,
					"The exact improvement scaffold moved, changed, or lost its receipt.");
				return;
			}
			GameObject scaffold = carriedIntent != null && carriedIntent.Working
					? (scaffoldState == KingdomPhysicalLookupState.Exact
						&& ReferenceEquals(exactScaffold, carriedIntent.Scaffold)
						? exactScaffold : null)
				: (scaffoldState == KingdomPhysicalLookupState.Exact ? exactScaffold : null);
			KingdomConstructionJob inspected = Job;
			if (GameObject.Validate(scaffold))
			{
				GameObject attemptedScaffold = scaffold;
				r_KingdomScaffold scaffoldPart = scaffold.GetPart<r_KingdomScaffold>();
				GameObject exactFinal;
				int exactFinals = r_KingdomScaffold.FindExactSuccessors(Z, Job,
					successor.Blueprint, scaffold, out exactFinal);
				if (exactFinals > 1)
				{
					KingdomConstruction.Quarantine(ref inspected,
						"More than one exact improvement successor carries this receipt.");
					return;
				}
				int finalPending = scaffold.GetIntProperty(r_KingdomScaffold.FinalPendingProperty);
				if (finalPending != 0 && finalPending != 1)
				{
					KingdomConstruction.Quarantine(ref inspected,
						"The improvement scaffold final flag is not an exact boolean.");
					return;
				}
				if (exactFinals == 0 && finalPending == 0
					&& (Job.Phase == KingdomConstructionPhase.ProjectionPending
						|| Job.Phase == KingdomConstructionPhase.Outstanding)
					&& !scaffoldPart.TryValidateInitialDurableWork(Job, Job.UpdatedTick,
						out string initialFailure))
				{
					KingdomConstruction.Quarantine(ref inspected, initialFailure);
					return;
				}
				if (Job.Phase == KingdomConstructionPhase.ProjectionPending
					&& finalPending == 0)
					KingdomConstruction.FinishProjection(ref inspected, true, true);
				else if (Job.Phase == KingdomConstructionPhase.Working
					|| Job.Phase == KingdomConstructionPhase.ProjectionPending)
					scaffoldPart.AdvanceDurable(System, Z, Job, The.Game.TimeTicks);
				else if (Job.Phase == KingdomConstructionPhase.Outstanding
					&& scaffoldPart.RemainingTicks <= 0 && scaffoldPart.LastWorkedTick > 0)
					scaffoldPart.RetryDurable(System, Z, Job);
				// Re-read after callbacks: the scaffold may now be gone and its exact successor present.
				scaffold = carriedIntent != null && carriedIntent.Working
					&& ExpectedImprovementScaffold(carriedIntent.Scaffold, expectedCell, successor, Job)
					&& KingdomConstruction.HasReceipt(carriedIntent.Scaffold, Job)
						? carriedIntent.Scaffold : null;
				if (GameObject.Validate(attemptedScaffold))
				{
					if (!ExpectedImprovementScaffold(attemptedScaffold, expectedCell, successor, Job)
						|| !KingdomConstruction.HasReceipt(attemptedScaffold, Job))
					{
						KingdomConstruction.Quarantine(ref inspected,
							"The improvement scaffold changed during its continuation callback.");
					}
					return;
				}
			}
			if (GameObject.Validate(work) && work.GetIntProperty(BuiltProperty) == 1
				&& work.GetStringProperty(BuildKeyProperty) == Job.TargetKey
				&& work.IDIfAssigned != Job.SubjectId)
			{
				if (!r_KingdomScaffold.HasRemovalProof(work, Job.SubjectId))
				{
					KingdomConstruction.Quarantine(ref inspected,
						"The improvement successor lacks predecessor-removal proof.");
					return;
				}
				if (KingdomConstruction.Complete(ref inspected))
					r_KingdomScaffold.TellCompletion(System, work, inspected);
				return;
			}
			if (GameObject.Validate(work) && !string.IsNullOrEmpty(Job.SubjectId)
				&& work.IDIfAssigned != Job.SubjectId)
			{
				return;
			}
			if (GameObject.Validate(work))
			{
				r_KingdomImprovement improvement = work.GetPart<r_KingdomImprovement>();
				GameObject finished = null;
				KingdomPhysicalLookupState finishedState = improvement == null
					? KingdomPhysicalLookupState.Absent
					: improvement.FindSuccessor(work.CurrentCell, out finished);
				if (finishedState == KingdomPhysicalLookupState.Ambiguous)
				{
					KingdomConstruction.Quarantine(ref inspected,
						"The improvement successor ID is duplicated or malformed.");
					return;
				}
				if (finishedState == KingdomPhysicalLookupState.Exact)
				{
					KingdomConstruction.Bind(finished, inspected);
					HandOver(work, finished, Job.TargetKey);
					return;
				}
			}
			else
			{
				GameObject result;
				KingdomPhysicalLookupState resultState = KingdomConstruction.FindReceipt(
					Z, Job, out result);
				if (resultState == KingdomPhysicalLookupState.Ambiguous)
				{
					KingdomConstruction.Quarantine(ref inspected,
						"More than one physical object carries the improvement receipt.");
					return;
				}
				if (GameObject.Validate(result) && result.GetIntProperty(BuiltProperty) == 1
					&& result.GetStringProperty(BuildKeyProperty) == Job.TargetKey
					&& r_KingdomScaffold.HasRemovalProof(result, Job.SubjectId))
				{
					if (KingdomConstruction.Complete(ref inspected))
						r_KingdomScaffold.TellCompletion(System, result, inspected);
				}
				return;
			}
			if (scaffold != null)
			{
				if (Job.Phase != KingdomConstructionPhase.Working)
				{
					KingdomConstruction.FinishProjection(ref inspected, true, true);
				}
				return;
			}
			KingdomConstruction.Quarantine(ref inspected,
				"The improvement projection has no safely identifiable scaffold or successor.");
		}

		private static bool HasActiveConstruction(GameObject Work)
		{
			return KingdomConstruction.ReceiptBlocksCurrent(Work);
		}

	}
}
