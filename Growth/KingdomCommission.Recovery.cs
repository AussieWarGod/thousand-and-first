using System.Collections.Generic;
using ThousandAndFirst.Simulation.Kernel;
using XRL;
using XRL.Messages;
using XRL.Rules;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst
{
	public static partial class KingdomCommission
	{
		internal static void RetryConstruction(KingdomSystem System, Zone Z, KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null || Job.Route != KingdomConstructionRoute.CommissionScaffold
				|| !KingdomData.TryGetBuilding(Job.TargetKey, out var entry))
			{
				return;
			}
			GameObject scaffold = FindExpectedScaffold(Z, Job, entry);
			if (scaffold != null && scaffold.ID == Job.SubjectId)
			{
				r_KingdomScaffold part = scaffold.GetPart<r_KingdomScaffold>();
				if (part.RemainingTicks <= 0 && part.LastWorkedTick > 0)
				{
					part.RetryDurable(System, Z, Job);
					return;
				}
			}
			ProjectScaffold(System, Z, entry, Job.Payload, Job, out _, out _);
		}

		internal static void InspectConstruction(KingdomSystem System, Zone Z,
			KingdomConstructionJob Job)
		{
			if (System == null || Z == null || Job == null
				|| Job.Route != KingdomConstructionRoute.CommissionScaffold
				|| !KingdomData.TryGetBuilding(Job.TargetKey, out var entry)) return;
			if (CountExpectedScaffolds(Z, Job, entry) > 1)
			{
				KingdomConstructionJob duplicate = Job;
				KingdomConstruction.Quarantine(ref duplicate,
					"More than one commissioned scaffold carries the exact receipt.");
				return;
			}
			GameObject existing = FindExpectedScaffold(Z, Job, entry);
			KingdomConstructionJob inspected = Job;
			GameObject successor;
			int successors = r_KingdomScaffold.FindExactSuccessors(Z, Job,
				entry.Blueprint, existing, out successor);
			if (successors > 1)
			{
				KingdomConstruction.Quarantine(ref inspected,
					"More than one exact commissioned successor carries this receipt.");
				return;
			}
			if (Job.Phase == KingdomConstructionPhase.Complete)
			{
				if (existing != null)
				{
					if (!KingdomConstructionRules.FullyFundedExact(Job))
					{
						KingdomConstruction.Quarantine(ref inspected,
							"A premature terminal commission does not carry exact paid claims.");
						return;
					}
					if (inspected.SubjectId != existing.ID
						&& !KingdomConstruction.UpdateSubject(ref inspected, existing.ID)) return;
					if (successors == 1)
					{
						KingdomConstruction.FinishProjection(ref inspected, false, false,
							"The terminal receipt still has an exact scaffold to remove.");
					}
					else
					{
						KingdomConstruction.FinishProjection(ref inspected, true, true);
					}
				}
				else if (successors == 1)
				{
					if (!r_KingdomScaffold.HasRemovalProof(successor, Job.SubjectId))
					{
						KingdomConstruction.Quarantine(ref inspected,
							"The terminal commissioned successor lacks scaffold-removal proof.");
						return;
					}
					r_KingdomScaffold.TellCompletion(System, successor, Job);
				}
				return;
			}
			if (existing != null)
			{
				if (inspected.SubjectId != existing.ID)
				{
					if (!KingdomConstruction.UpdateSubject(ref inspected, existing.ID)) return;
					KingdomConstruction.FinishProjection(ref inspected, true, true);
					return;
				}
				r_KingdomScaffold part = existing.GetPart<r_KingdomScaffold>();
				int finalPending = existing.GetIntProperty(r_KingdomScaffold.FinalPendingProperty);
				if (finalPending != 0 && finalPending != 1)
				{
					KingdomConstruction.Quarantine(ref inspected,
						"The commissioned scaffold final flag is not an exact boolean.");
					return;
				}
				if (Job.Phase == KingdomConstructionPhase.ProjectionPending
					&& finalPending == 0)
				{
					KingdomConstruction.FinishProjection(ref inspected, true, true);
				}
				else if (Job.Phase == KingdomConstructionPhase.Working
					|| Job.Phase == KingdomConstructionPhase.ProjectionPending)
					part.AdvanceDurable(System, Z, Job, The.Game.TimeTicks);
				else if (Job.Phase == KingdomConstructionPhase.Outstanding)
				{
					if (part.RemainingTicks <= 0 && part.LastWorkedTick > 0)
						part.RetryDurable(System, Z, Job);
					else
						KingdomConstruction.FinishProjection(ref inspected, true, true);
				}
				return;
			}
			if (successors == 1)
			{
				if (!r_KingdomScaffold.HasRemovalProof(successor, Job.SubjectId))
				{
					KingdomConstruction.Quarantine(ref inspected,
						"The commissioned successor lacks exact scaffold-removal proof.");
					return;
				}
				if (KingdomConstruction.Complete(ref inspected))
					r_KingdomScaffold.TellCompletion(System, successor, inspected);
				return;
			}
			KingdomConstruction.Quarantine(ref inspected,
				"The commissioned receipt has no exact predecessor or successor at its recorded cell.");
		}

	}
}
