using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using ThousandAndFirst.Simulation.City;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleRules
	{
		/// <summary>Production petition shell adapter. Petition semantics live in the retained
		/// operation; these two revision rows fence domain publication and its clock callback.</summary>
		internal static partial class PetitionRuntimeAdapter
		{
			internal static bool PrepareLeases(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation)
			{
				if (!CanOwnAuthority(book) || operation == null
					|| operation.Lane != KingdomLifecycleLane.Petition
					|| operation.Phase != KingdomLifecyclePhase.Prepared
					|| !KingdomPetitionRules.FrozenSnapshotValid(operation)
					|| operation.Sequence <= 0L || operation.ResourceLeases == null
					|| operation.ResourceLeases.Count != 0) return false;
				long before = operation.Sequence - 1L;
				operation.DueBefore = before;
				operation.DueAfter = operation.Sequence;
				KingdomLifecycleResourceLease domain = PrepareLeaseCore(book, operation,
					KingdomLifecycleResourceKind.Petition, operation.SettlementId,
					operation.SettlementId, before, 1L);
				string subject = ScheduleSubjectId(operation.SettlementId,
					KingdomLifecycleLane.Petition);
				KingdomLifecycleResourceLease schedule = PrepareLeaseCore(book, operation,
					KingdomLifecycleResourceKind.Schedule, operation.SettlementId,
					subject, before, 1L);
				if (domain == null || schedule == null) return false;
				operation.ResourceLeases.Add(domain);
				operation.ResourceLeases.Add(schedule);
				return true;
			}

			internal static KingdomLifecycleLeaseState DomainState(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation)
			{
				KingdomLifecycleResourceLease lease = RequiredDomainLease(operation);
				return ExactOperationAuthority(book, operation) && lease != null
					? lease.State : KingdomLifecycleLeaseState.None;
			}

			internal static bool BeginDomain(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation)
			{
				KingdomLifecycleResourceLease lease = RequiredDomainLease(operation);
				return ExactPetitionPhase(book, operation, KingdomLifecyclePhase.DomainIntent)
					&& lease != null && lease.Kind == KingdomLifecycleResourceKind.Petition
					&& lease.Before == operation.Sequence - 1L
					&& lease.After == operation.Sequence
					&& BeginLeaseCore(book, lease, lease.Before);
			}

			internal static bool CommitDomain(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation)
			{
				KingdomLifecycleResourceLease lease = RequiredDomainLease(operation);
				KingdomLifecycleResourceRevision row = lease == null ? null
					: FindResource(book, lease.Key);
				return ExactPetitionPhase(book, operation, KingdomLifecyclePhase.DomainIntent)
					&& lease != null && lease.State == KingdomLifecycleLeaseState.Intent
					&& CommitLeaseWitnessCore(book, operation, lease, row, lease.After);
			}

			internal static bool ProveDomain(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation)
			{
				KingdomLifecycleLeaseState state = DomainState(book, operation);
				if (state == KingdomLifecycleLeaseState.Proved) return true;
				if (state == KingdomLifecycleLeaseState.Prepared && !BeginDomain(book, operation))
					return false;
				return CommitDomain(book, operation);
			}

			internal static bool ProveSchedule(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation)
			{
				if (!ExactPetitionPhase(book, operation, KingdomLifecyclePhase.ScheduleIntent))
					return false;
				string subject = ScheduleSubjectId(operation.SettlementId, operation.Lane);
				KingdomLifecycleResourceLease lease = FindLease(operation, ResourceKey(
					KingdomLifecycleResourceKind.Schedule, operation.SettlementId, subject));
				KingdomLifecycleResourceRevision row = lease == null ? null
					: FindResource(book, lease.Key);
				if (lease == null || row == null || lease.Before != operation.Sequence - 1L
					|| lease.After != operation.Sequence) return false;
				if (lease.State == KingdomLifecycleLeaseState.Proved)
					return ResourceWitnessMatches(row, lease);
				if (lease.State == KingdomLifecycleLeaseState.Prepared
					&& !BeginLeaseCore(book, lease, lease.Before)) return false;
				return lease.State == KingdomLifecycleLeaseState.Intent
					&& CommitLeaseWitnessCore(book, operation, lease, row, lease.After);
			}

			internal static bool BeginSink(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, KingdomLifecycleSinkMask sink)
			{
				KingdomLifecycleSinkState state;
				if (!ExactPetitionPhase(book, operation, KingdomLifecyclePhase.Sinks)
					|| !SingleSink(sink) || !GetSink(operation.Outbox, sink, out state)
					|| state != KingdomLifecycleSinkState.Pending) return false;
				SetSink(operation.Outbox, sink, KingdomLifecycleSinkState.Intent);
				return ExactOperationAuthority(book, operation);
			}

			internal static bool CommitSink(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, KingdomLifecycleSinkMask sink)
			{
				KingdomLifecycleSinkState state;
				if (!ExactPetitionPhase(book, operation, KingdomLifecyclePhase.Sinks)
					|| !SingleSink(sink) || !GetSink(operation.Outbox, sink, out state)
					|| state != KingdomLifecycleSinkState.Intent) return false;
				SetSink(operation.Outbox, sink, KingdomLifecycleSinkState.Delivered);
				return ExactOperationAuthority(book, operation);
			}

			private static bool ExactPetitionPhase(KingdomLifecycleBook book,
				KingdomLifecycleOperation operation, KingdomLifecyclePhase phase)
			{
				return ExactOperationAuthority(book, operation)
					&& operation.Lane == KingdomLifecycleLane.Petition
					&& operation.Phase == phase
					&& KingdomPetitionRules.FrozenSnapshotValid(operation);
			}

			private static bool SingleSink(KingdomLifecycleSinkMask sink)
			{
				byte value = (byte)sink;
				return value != 0 && (value & (value - 1)) == 0;
			}

			private static bool GetSink(KingdomLifecycleOutbox box,
				KingdomLifecycleSinkMask sink, out KingdomLifecycleSinkState state)
			{
				state = KingdomLifecycleSinkState.None;
				if (box == null) return false;
				switch (sink)
				{
				case KingdomLifecycleSinkMask.Chronicle: state = box.ChronicleState; return true;
				case KingdomLifecycleSinkMask.Ledger: state = box.LedgerState; return true;
				case KingdomLifecycleSinkMask.Message: state = box.MessageState; return true;
				case KingdomLifecycleSinkMask.Deed: state = box.DeedState; return true;
				case KingdomLifecycleSinkMask.Guestbook: state = box.GuestbookState; return true;
				default: return false;
				}
			}

			private static void SetSink(KingdomLifecycleOutbox box,
				KingdomLifecycleSinkMask sink, KingdomLifecycleSinkState state)
			{
				switch (sink)
				{
				case KingdomLifecycleSinkMask.Chronicle: box.ChronicleState = state; break;
				case KingdomLifecycleSinkMask.Ledger: box.LedgerState = state; break;
				case KingdomLifecycleSinkMask.Message: box.MessageState = state; break;
				case KingdomLifecycleSinkMask.Deed: box.DeedState = state; break;
				case KingdomLifecycleSinkMask.Guestbook: box.GuestbookState = state; break;
				}
			}
		}
	}
}
