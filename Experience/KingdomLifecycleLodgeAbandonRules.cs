using System;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		internal static bool LodgeAbandoned(KingdomLifecycleOperation op)
		{
			return op != null && op.Action == KingdomLifecycleAction.Lodge
				&& op.Phase == KingdomLifecyclePhase.Terminal && op.LodgeTerminal != null
				&& (op.LodgeTerminal.State == KingdomLifecycleLodgeTerminalState.Abandoned
					|| op.LodgeTerminal.State ==
						KingdomLifecycleLodgeTerminalState.AuthorityReleased)
				&& LodgeTerminalShape(op, false);
		}

		internal static bool LodgeAuthorityReleased(KingdomLifecycleOperation op)
		{
			return LodgeAbandoned(op) && op.LodgeTerminal.State ==
				KingdomLifecycleLodgeTerminalState.AuthorityReleased;
		}

		internal static KingdomLifecycleMutationAction LodgeAbandonScheduleAction(
			KingdomLifecycleBook book, KingdomLifecycleOperation op, long current)
		{
			if (!ExactOperationAuthority(book, op) || op.Phase != KingdomLifecyclePhase.DomainIntent
				|| op.LodgeTerminal == null || op.LodgeTerminal.State !=
					KingdomLifecycleLodgeTerminalState.AbandonIntent) return KingdomLifecycleMutationAction.Quarantine;
			KingdomLifecycleResourceLease lease = FindLease(op, ResourceKey(
				KingdomLifecycleResourceKind.Schedule, op.SettlementId,
				ScheduleSubjectId(op.SettlementId, op.Lane)));
			KingdomLifecycleResourceRevision row = lease == null ? null : FindResource(book, lease.Key);
			if (lease == null || row == null || row.ActiveOperationId != op.Id) return KingdomLifecycleMutationAction.Quarantine;
			if (lease.State == KingdomLifecycleLeaseState.Prepared)
				return row.Revision == lease.BeforeRevision && row.LastOperationId != op.Id
					&& current == lease.Before ? KingdomLifecycleMutationAction.InvokeOnce
					: KingdomLifecycleMutationAction.Quarantine;
			if (lease.State == KingdomLifecycleLeaseState.Intent)
			{
				if (row.Revision != lease.BeforeRevision || row.LastOperationId == op.Id)
					return KingdomLifecycleMutationAction.Quarantine;
				if (current == lease.Before) return KingdomLifecycleMutationAction.InvokeOnce;
				if (current == lease.After) return KingdomLifecycleMutationAction.ConfirmAfter;
				return KingdomLifecycleMutationAction.Quarantine;
			}
			return lease.State == KingdomLifecycleLeaseState.Proved
				&& ResourceWitnessMatches(row, lease) && current == lease.After
				? KingdomLifecycleMutationAction.Settled : KingdomLifecycleMutationAction.Quarantine;
		}

		internal static bool BeginLodgeAbandonSchedule(KingdomLifecycleBook book,
			KingdomLifecycleOperation op, long current)
		{
			if (LodgeAbandonScheduleAction(book, op, current)
				!= KingdomLifecycleMutationAction.InvokeOnce) return false;
			KingdomLifecycleResourceLease lease = FindLease(op, ResourceKey(
				KingdomLifecycleResourceKind.Schedule, op.SettlementId,
				ScheduleSubjectId(op.SettlementId, op.Lane)));
			if (lease.State == KingdomLifecycleLeaseState.Intent) return true;
			return BeginLeaseCore(book, lease, current);
		}

		internal static bool CommitLodgeAbandonSchedule(KingdomLifecycleBook book,
			KingdomLifecycleOperation op, long current)
		{
			KingdomLifecycleMutationAction action = LodgeAbandonScheduleAction(book, op, current);
			if (action == KingdomLifecycleMutationAction.Settled) return true;
			if (action != KingdomLifecycleMutationAction.ConfirmAfter) return false;
			KingdomLifecycleResourceLease lease = FindLease(op, ResourceKey(
				KingdomLifecycleResourceKind.Schedule, op.SettlementId,
				ScheduleSubjectId(op.SettlementId, op.Lane)));
			return CommitLeaseWitnessCore(book, op, lease, FindResource(book, lease.Key), current);
		}

		internal static bool TryCommitLodgeAbandon(KingdomLifecycleBook book,
			KingdomLifecycleOperation op, long tick)
		{
			if (!ExactOperationAuthority(book, op) || tick < op.UpdatedTick
				|| op.Phase != KingdomLifecyclePhase.DomainIntent || op.LodgeTerminal == null
				|| op.LodgeTerminal.State != KingdomLifecycleLodgeTerminalState.AbandonIntent
				|| !LodgeTerminalShape(op, false) || !OutboxInitial(op.Outbox)
				|| !WaterConserved(op, true)) return false;
			KingdomLifecycleResourceLease domain = RequiredDomainLease(op);
			KingdomLifecycleResourceLease schedule = FindLease(op, ResourceKey(
				KingdomLifecycleResourceKind.Schedule, op.SettlementId,
				ScheduleSubjectId(op.SettlementId, op.Lane)));
			if (domain == null || domain.Kind != KingdomLifecycleResourceKind.Roster
				|| schedule == null || schedule.State != KingdomLifecycleLeaseState.Proved
				|| !ResourceWitnessMatches(FindResource(book, schedule.Key), schedule)) return false;
			for (int i = 0; i < op.ResourceLeases.Count; i++)
			{
				KingdomLifecycleResourceLease lease = op.ResourceLeases[i];
				if (lease == domain || lease == schedule) continue;
				if (lease.Kind != KingdomLifecycleResourceKind.WaterVessel
					|| lease.State != KingdomLifecycleLeaseState.Proved
					|| !ResourceWitnessMatches(FindResource(book, lease.Key), lease)) return false;
			}
			KingdomLifecycleResourceRevision row = FindResource(book, domain.Key);
			if (domain.State == KingdomLifecycleLeaseState.Proved)
			{
				if (!ResourceWitnessMatches(row, domain)) return false;
			}
			else
			{
				if ((domain.State != KingdomLifecycleLeaseState.Prepared
					&& domain.State != KingdomLifecycleLeaseState.Intent
					&& domain.State != KingdomLifecycleLeaseState.Skipped)
					|| row == null || row.ActiveOperationId != op.Id
					|| row.Revision != domain.BeforeRevision || row.LastOperationId == op.Id) return false;
				domain.State = KingdomLifecycleLeaseState.Skipped;
			}
			op.LodgeTerminal.State = KingdomLifecycleLodgeTerminalState.Abandoned;
			op.Phase = KingdomLifecyclePhase.Terminal; op.UpdatedTick = tick;
			return ExactOperationAuthority(book, op) && LodgeAbandonedComponentsSettled(book, op);
		}

		private static bool LodgeAbandonedComponentsSettled(KingdomLifecycleBook book,
			KingdomLifecycleOperation op)
		{
			if (!LodgeAbandoned(op) || !OutboxInitial(op.Outbox) || !WaterConserved(op, true)) return false;
			KingdomLifecycleResourceLease domain = RequiredDomainLease(op);
			for (int i = 0; i < op.ResourceLeases.Count; i++)
			{
				KingdomLifecycleResourceLease lease = op.ResourceLeases[i];
				if (!ResourceWitnessMatches(FindResource(book, lease.Key), lease)) return false;
				if (lease == domain)
				{
					if (lease.State != KingdomLifecycleLeaseState.Proved
						&& lease.State != KingdomLifecycleLeaseState.Skipped) return false;
				}
				else if (lease.State != KingdomLifecycleLeaseState.Proved) return false;
			}
			return domain != null && domain.Kind == KingdomLifecycleResourceKind.Roster;
		}

		internal static bool TryReleaseAbandonedLodge(KingdomLifecycleBook book,
			KingdomLifecycleOperation op, long tick)
		{
			if (LodgeAuthorityReleased(op))
				return tick >= op.UpdatedTick && ExactOperationAuthority(book, op)
					&& LodgeReleasedComponentsSettled(book, op);
			if (!ExactOperationAuthority(book, op) || tick < op.UpdatedTick
				|| !IsExactSuccessor(op.Sequence, GetRetiredThrough(book, op.Lane))
				|| !LodgeAbandonedComponentsSettled(book, op) || !ProofListValid(book)) return false;
			for (int i = 0; i < op.ResourceLeases.Count; i++)
				FindResource(book, op.ResourceLeases[i].Key).ActiveOperationId = null;
			op.LodgeTerminal.State = KingdomLifecycleLodgeTerminalState.AuthorityReleased;
			op.UpdatedTick = tick; SetRetiredThrough(book, op.Lane, op.Sequence);
			AppendLifecycleProof(book, new KingdomLifecycleProof
			{
				Sequence = op.Sequence, Id = op.Id, PlanHash = op.PlanHash,
				Lane = op.Lane, Action = op.Action, Tick = tick
			});
			return CanOwnAuthority(book) && LodgeReleasedComponentsSettled(book, op);
		}

		private static void AppendLifecycleProof(KingdomLifecycleBook book,
			KingdomLifecycleProof proof)
		{
			book.RecentProofs.Add(proof);
			while (book.RecentProofs.Count > MaxRecentProofs)
			{
				int remove = LodgeAuthorityReleased(book.NotableGuest)
					&& book.RecentProofs[0].Id == book.NotableGuest.Id ? 1 : 0;
				book.RecentProofs.RemoveAt(remove);
			}
		}

		private static bool ReleasedResourceWitnessMatches(KingdomLifecycleResourceRevision row,
			KingdomLifecycleResourceLease lease)
		{
			if (!ResourceMatches(row, lease) || row.ActiveOperationId != null) return false;
			if (lease.State == KingdomLifecycleLeaseState.Skipped)
				return row.Revision == lease.BeforeRevision
					&& row.LastOperationId != lease.OperationId;
			return lease.State == KingdomLifecycleLeaseState.Proved
				&& row.Revision == lease.AfterRevision && row.LastOperationId == lease.OperationId;
		}

		internal static bool TryRemoveReleasedLodge(KingdomLifecycleBook book,
			KingdomLifecycleOperation op, long tick)
		{
			if (!ExactOperationAuthority(book, op) || tick < op.UpdatedTick
				|| !LodgeReleasedComponentsSettled(book, op)) return false;
			SetSlot(book, op.Lane, null);
			return CanOwnAuthority(book);
		}

		internal static bool ExactLodgeRetirementProof(KingdomLifecycleBook book,
			string operationId, string planHash)
		{
			if (book == null || !ProofListValid(book) || string.IsNullOrEmpty(operationId)
				|| string.IsNullOrEmpty(planHash)) return false;
			int matches = 0;
			for (int i = 0; i < book.RecentProofs.Count; i++)
			{
				KingdomLifecycleProof proof = book.RecentProofs[i];
				if (proof.Id != operationId) continue;
				if (proof.PlanHash != planHash || proof.Lane != KingdomLifecycleLane.NotableGuest
					|| proof.Action != KingdomLifecycleAction.Lodge
					|| proof.Sequence > book.NotableGuestRetiredThrough) return false;
				matches++;
			}
			return matches == 1;
		}

		private static bool LodgeReleasedComponentsSettled(KingdomLifecycleBook book,
			KingdomLifecycleOperation op)
		{
			if (!LodgeAuthorityReleased(op) || !OutboxInitial(op.Outbox)
				|| !WaterConserved(op, true) || op.Sequence != GetRetiredThrough(book, op.Lane)
				|| !ExactLodgeRetirementProof(book, op.Id, op.PlanHash)) return false;
			KingdomLifecycleResourceLease domain = RequiredDomainLease(op);
			for (int i = 0; i < op.ResourceLeases.Count; i++)
			{
				KingdomLifecycleResourceLease lease = op.ResourceLeases[i];
				if (!ReleasedResourceWitnessMatches(FindResource(book, lease.Key), lease)) return false;
				if (lease == domain)
				{
					if (lease.State != KingdomLifecycleLeaseState.Proved
						&& lease.State != KingdomLifecycleLeaseState.Skipped) return false;
				}
				else if (lease.State != KingdomLifecycleLeaseState.Proved) return false;
			}
			return domain != null && domain.Kind == KingdomLifecycleResourceKind.Roster;
		}
	}
}
