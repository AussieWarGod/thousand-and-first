using System;
using System.Collections.Generic;
using XRL;
using XRL.Messages;
using XRL.World;
using ThousandAndFirst;
namespace ThousandAndFirst
{
	using XRL.World.Parts;
	public static partial class KingdomPlanMarker
	{
		internal static void RetryConstruction(KingdomSystem System, Zone Z, KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null || Job.Route != KingdomConstructionRoute.PlanScaffold
				|| !KingdomData.TryGetBuilding(Job.TargetKey, out var entry))
			{
				return;
			}
			if (CountPlanScaffolds(Z, Job, entry) > 1)
			{
				KingdomConstructionJob duplicate = Job;
				KingdomConstruction.Quarantine(ref duplicate,
					"More than one planned scaffold carries the exact receipt.");
				return;
			}
			GameObject existing = FindPlanScaffold(Z, Job, entry);
			GameObject marker = FindExactPlanMarker(System, Z, Job, entry);
			GameObject namedSubject;
			KingdomPhysicalLookupState subjectState = KingdomConstruction.FindExactId(
				Z, Job.SubjectId, out namedSubject);
			if (subjectState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstructionJob duplicate = Job;
				KingdomConstruction.Quarantine(ref duplicate,
					"The planned subject ID resolves to more than one loaded object.");
				return;
			}
			if (GameObject.Validate(namedSubject) && marker == null
				&& (existing == null || namedSubject != existing))
			{
				KingdomConstructionJob moved = Job;
				KingdomConstruction.Quarantine(ref moved,
					"The paid plan predecessor no longer matches its recorded cell or design.");
				return;
			}
			if (existing != null && existing.CurrentCell == Z.GetCell(Job.X, Job.Y)
				&& existing.GetPart<r_KingdomScaffold>() != null
				&& existing.GetPart<r_KingdomScaffold>().TargetBlueprint == entry.Blueprint)
			{
				r_KingdomScaffold part = existing.GetPart<r_KingdomScaffold>();
				GameObject finalSuccessor;
				int finalSuccessors = r_KingdomScaffold.FindExactSuccessors(Z, Job,
					entry.Blueprint, existing, out finalSuccessor);
				if (finalSuccessors > 1)
				{
					KingdomConstructionJob duplicate = Job;
					KingdomConstruction.Quarantine(ref duplicate,
						"More than one exact planned successor carries this receipt.");
					return;
				}
				if (finalSuccessors == 0 && Job.Phase != KingdomConstructionPhase.Working
					&& !part.TryValidateInitialDurableWork(Job, Job.UpdatedTick,
						out string initialFailure))
				{
					KingdomConstructionJob damaged = Job;
					KingdomConstruction.Quarantine(ref damaged, initialFailure);
					return;
				}
				KingdomConstructionJob complete = Job;
				if (marker != null && marker.GetPart<r_KingdomPlanMarker>() != null)
				{
					if (!KingdomConstruction.BeginProjection(ref complete, out _)) return;
					if (!KingdomConstruction.IsCurrent(complete)
						|| !KingdomConstruction.HasReceipt(marker, complete)
						|| !KingdomConstruction.HasReceipt(existing, complete)) return;
					string markerId = marker.IDIfAssigned;
					bool removed;
					try
					{
						removed = marker.Destroy(null, Silent: true);
					}
					catch (System.Exception ex)
					{
						KingdomSurvey.ObserveCurrentTopologyInActive(Z, marker);
						KingdomConstruction.Quarantine(ref complete,
							"Plan-marker retry threw during removal: " + ex.Message);
						return;
					}
					if (removed && !GameObject.Validate(marker))
						KingdomSurvey.ObserveRemovedFromActive(Z, marker);
					if (KingdomConstructionRules.ExactRemovalAction(true, removed,
						GameObject.Validate(marker), KingdomConstruction.FindExactId(
							Z, markerId, out _) != KingdomPhysicalLookupState.Absent, true)
						!= KingdomExactRemovalAction.ProvedAbsent)
					{
						KingdomConstruction.Quarantine(ref complete,
							"Plan-marker retry was vetoed, moved, replaced, or partially changed.");
						return;
					}
					string callbackFailure = null;
					if (!TryProveMarkerRemoval(System, Z, existing,
						Z.GetCell(Job.X, Job.Y), entry, markerId, ref complete,
						out string removalFailure)
						|| !part.TryValidateInitialDurableWork(complete, Job.UpdatedTick,
							out callbackFailure))
					{
						KingdomConstruction.Quarantine(ref complete,
							removalFailure ?? callbackFailure
								?? "The planned scaffold changed during retried marker removal.");
						return;
					}
				}
				if (!GameObject.Validate(existing) || existing.CurrentCell != Z.GetCell(Job.X, Job.Y)
					|| !KingdomConstruction.HasReceipt(existing, complete)
					|| !KingdomConstruction.IsCurrent(complete)) return;
				if (complete.SubjectId != existing.IDIfAssigned)
				{
					if (!r_KingdomScaffold.HasRemovalProof(existing, complete.SubjectId))
					{
						KingdomConstruction.Quarantine(ref complete,
							"The planned scaffold lacks exact marker-removal proof.");
						return;
					}
					if (!KingdomConstruction.UpdateSubject(ref complete, existing.IDIfAssigned)) return;
				}
				if (part.RemainingTicks <= 0 && part.LastWorkedTick > 0)
					part.RetryDurable(System, Z, complete);
				else
					KingdomConstruction.FinishProjection(ref complete, true, true);
				return;
			}
			if (marker != null && marker.GetPart<r_KingdomPlanMarker>() != null)
			{
				Realize(System, marker, entry, Job, out _);
				return;
			}
			KingdomConstructionJob absent = Job;
			KingdomConstruction.Quarantine(ref absent,
				"The planned receipt has no exact marker or scaffold at its recorded cell.");
		}
		internal static void InspectConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| Job.Route != KingdomConstructionRoute.PlanScaffold
				|| !KingdomData.TryGetBuilding(Job.TargetKey, out var entry)) return;
			GameObject result = FindPlanScaffold(Z, Job, entry);
			Cell cell = Z.GetCell(Job.X, Job.Y);
			KingdomConstructionJob inspected = Job;
			if (CountPlanScaffolds(Z, Job, entry) > 1)
			{
				KingdomConstruction.Quarantine(ref inspected,
					"More than one planned scaffold carries the exact receipt.");
				return;
			}
			GameObject marker = FindExactPlanMarker(System, Z, Job, entry);
			GameObject namedSubject;
			KingdomPhysicalLookupState subjectState = KingdomConstruction.FindExactId(
				Z, Job.SubjectId, out namedSubject);
			if (subjectState == KingdomPhysicalLookupState.Ambiguous)
			{
				KingdomConstruction.Quarantine(ref inspected,
					"The planned subject ID resolves to more than one loaded object.");
				return;
			}
			r_KingdomScaffold scaffold = GameObject.Validate(result)
				? result.GetPart<r_KingdomScaffold>() : null;
			GameObject successor;
			int successors = r_KingdomScaffold.FindExactSuccessors(Z, Job,
				entry.Blueprint, result, out successor);
			if (successors > 1)
			{
				KingdomConstruction.Quarantine(ref inspected,
					"More than one exact planned successor carries this receipt.");
				return;
			}
			if (Job.Phase == KingdomConstructionPhase.Complete)
			{
				if (result != null)
				{
					if (!KingdomConstructionRules.FullyFundedExact(Job))
					{
						KingdomConstruction.Quarantine(ref inspected,
							"A premature terminal plan does not carry exact paid claims.");
						return;
					}
					if (marker != null)
					{
						KingdomConstruction.FinishProjection(ref inspected, false, false,
							"The terminal plan still has its exact marker to remove.");
						return;
					}
					// Migration for the first registry wave, which terminalized after marker
					// removal and scaffold placement. That old path carried no removal-proof stamp.
					if (inspected.SubjectId != result.IDIfAssigned
						&& !KingdomConstruction.UpdateSubject(ref inspected, result.IDIfAssigned)) return;
					if (successors == 1)
						KingdomConstruction.FinishProjection(ref inspected, false, false,
							"The terminal receipt still has an exact scaffold to remove.");
					else
						KingdomConstruction.FinishProjection(ref inspected, true, true);
				}
				else if (successors == 1)
				{
					if (!r_KingdomScaffold.HasRemovalProof(successor, Job.SubjectId))
					{
						KingdomConstruction.Quarantine(ref inspected,
							"The terminal planned successor lacks scaffold-removal proof.");
						return;
					}
					r_KingdomScaffold.TellCompletion(System, successor, Job);
				}
				return;
			}
			if (scaffold != null && result.CurrentCell == cell
				&& scaffold.TargetBlueprint == entry.Blueprint)
			{
				int finalPending = result.GetIntProperty(r_KingdomScaffold.FinalPendingProperty);
				if (finalPending != 0 && finalPending != 1)
				{
					KingdomConstruction.Quarantine(ref inspected,
						"The planned scaffold final flag is not an exact boolean.");
					return;
				}
				if (successors == 0 && finalPending == 0
					&& (Job.Phase == KingdomConstructionPhase.ProjectionPending
						|| Job.Phase == KingdomConstructionPhase.Outstanding)
					&& !scaffold.TryValidateInitialDurableWork(Job, Job.UpdatedTick,
						out string initialFailure))
				{
					KingdomConstruction.Quarantine(ref inspected, initialFailure);
					return;
				}
				if (marker != null && marker.GetPart<r_KingdomPlanMarker>() != null)
				{
					KingdomConstruction.FinishProjection(ref inspected, false, false,
						"The scaffold is verified and its exact plan marker still needs removal.");
				}
				else
				{
					if (inspected.SubjectId != result.IDIfAssigned)
					{
						if (!r_KingdomScaffold.HasRemovalProof(result, inspected.SubjectId))
						{
							KingdomConstruction.Quarantine(ref inspected,
								"The planned scaffold lacks exact marker-removal proof after reload.");
							return;
						}
						if (!KingdomConstruction.UpdateSubject(ref inspected, result.IDIfAssigned)) return;
					}
				if (Job.Phase == KingdomConstructionPhase.ProjectionPending
					&& finalPending == 0)
					{
						KingdomConstruction.FinishProjection(ref inspected, true, true);
					}
					else if (Job.Phase == KingdomConstructionPhase.Working
						|| Job.Phase == KingdomConstructionPhase.ProjectionPending)
						scaffold.AdvanceDurable(System, Z, inspected, The.Game.TimeTicks);
					else if (Job.Phase == KingdomConstructionPhase.Outstanding)
					{
						if (scaffold.RemainingTicks <= 0 && scaffold.LastWorkedTick > 0)
							scaffold.RetryDurable(System, Z, inspected);
						else
							KingdomConstruction.FinishProjection(ref inspected, true, true);
					}
				}
				return;
			}
			if (successors == 1)
			{
				if (!r_KingdomScaffold.HasRemovalProof(successor, Job.SubjectId))
				{
					KingdomConstruction.Quarantine(ref inspected,
						"The planned successor lacks exact scaffold-removal proof.");
					return;
				}
				if (KingdomConstruction.Complete(ref inspected))
					r_KingdomScaffold.TellCompletion(System, successor, inspected);
				return;
			}
			if (GameObject.Validate(namedSubject) && marker == null
				&& (result == null || namedSubject != result))
			{
				KingdomConstruction.Quarantine(ref inspected,
					"The plan predecessor moved or changed outside its exact recorded identity.");
				return;
			}
			KingdomConstruction.Quarantine(ref inspected,
				"The interrupted plan projection has no safely identifiable exact endpoint.");
		}
	}
}
