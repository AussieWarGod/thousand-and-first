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
		public static string OperationId(string SettlementId, KingdomLifecycleLane Lane,
			long Sequence)
		{
			return HashId("operation", delegate(BinaryWriter w)
			{
				CanonicalString(w, SettlementId);
				w.Write((byte)Lane);
				w.Write(Sequence);
			});
		}

		public static string CarryId(string RealmId, long Sequence)
		{
			return HashId("carry", delegate(BinaryWriter w)
			{
				CanonicalString(w, RealmId);
				w.Write(Sequence);
			});
		}

		public static string ChildId(string Parent, string Kind, int Ordinal)
		{
			return HashId("child", delegate(BinaryWriter w)
			{
				CanonicalString(w, Parent);
				CanonicalString(w, Kind);
				w.Write(Ordinal);
			});
		}

		public static string ResourceKey(KingdomLifecycleResourceKind Kind,
			string ScopeId, string SubjectId)
		{
			if (!KnownResourceKind(Kind) || !ValidRootId(ScopeId) || !ValidRootId(SubjectId))
				return null;
			return HashId("resource", delegate(BinaryWriter w)
			{
				w.Write((byte)Kind);
				CanonicalString(w, ScopeId);
				CanonicalString(w, SubjectId);
			});
		}

		public static string TopologyId(KingdomLifecycleTopology Topology, string OwnerId,
			string ZoneId, int X, int Y)
		{
			if (!TopologyValid(Topology, OwnerId, ZoneId, X, Y)) return null;
			return HashId("topology", delegate(BinaryWriter w)
			{
				w.Write((byte)Topology);
				CanonicalString(w, OwnerId);
				CanonicalString(w, ZoneId);
				w.Write(X);
				w.Write(Y);
			});
		}

		public static string ScheduleSubjectId(string SettlementId, KingdomLifecycleLane Lane)
		{
			if (!ValidRootId(SettlementId) || Lane == KingdomLifecycleLane.None
				|| !Enum.IsDefined(typeof(KingdomLifecycleLane), Lane)) return null;
			return HashId("schedule-subject", delegate(BinaryWriter w)
			{
				CanonicalString(w, SettlementId);
				w.Write((byte)Lane);
			});
		}

		public static bool ActionAllowedInLane(KingdomLifecycleAction Action,
			KingdomLifecycleLane Lane)
		{
			switch (Action)
			{
			case KingdomLifecycleAction.Passages:
				return Lane == KingdomLifecycleLane.PlainGuest
					|| Lane == KingdomLifecycleLane.NotableGuest;
			case KingdomLifecycleAction.Spawn:
			case KingdomLifecycleAction.Depart:
			case KingdomLifecycleAction.OfferWater:
				return Lane == KingdomLifecycleLane.PlainGuest
					|| Lane == KingdomLifecycleLane.NotableGuest;
			case KingdomLifecycleAction.Lodge:
				return Lane == KingdomLifecycleLane.NotableGuest;
			case KingdomLifecycleAction.RaidWarning:
			case KingdomLifecycleAction.RaidRewarning:
			case KingdomLifecycleAction.RaidTribute:
			case KingdomLifecycleAction.RaidTalkDown:
			case KingdomLifecycleAction.RaidAttack:
			case KingdomLifecycleAction.RaidCancel:
			case KingdomLifecycleAction.RaidFight:
			case KingdomLifecycleAction.RaidFortify:
			case KingdomLifecycleAction.RaidResolve:
			case KingdomLifecycleAction.RaidDeliverDemand:
			case KingdomLifecycleAction.RaidAcknowledgeDemand:
			case KingdomLifecycleAction.RaidLoseChannel:
			case KingdomLifecycleAction.RaidDeadline:
			case KingdomLifecycleAction.RaidFortifyOrder:
			case KingdomLifecycleAction.RaidFortifyFailure:
			case KingdomLifecycleAction.RaidRecoveryAccept:
			case KingdomLifecycleAction.RaidRecoveryReady:
			case KingdomLifecycleAction.RaidRecoveryResolve:
			case KingdomLifecycleAction.RaidRecoveryDecline:
				return Lane == KingdomLifecycleLane.Raid;
			case KingdomLifecycleAction.PetitionOffer:
			case KingdomLifecycleAction.PetitionAccept:
			case KingdomLifecycleAction.PetitionDecline:
			case KingdomLifecycleAction.PetitionResolve:
			case KingdomLifecycleAction.PetitionExpire:
				return Lane == KingdomLifecycleLane.Petition;
			default:
				return false;
			}
		}

		public static KingdomLifecycleOperation PrepareOperation(KingdomLifecycleBook Book,
			KingdomLifecycleLane Lane, KingdomLifecycleAction Action, long Tick)
		{
			if (!CanOwnAuthority(Book) || Tick < 0L || !ActionAllowedInLane(Action, Lane)
				|| GetSlot(Book, Lane) != null) return null;
			long next = GetNextSequence(Book, Lane);
			if (next <= GetRetiredThrough(Book, Lane) || next == long.MaxValue) return null;
			return new KingdomLifecycleOperation
			{
				Sequence = next,
				Id = OperationId(Book.SettlementId, Lane, next),
				Lane = Lane,
				Action = Action,
				Phase = KingdomLifecyclePhase.Prepared,
				CreatedTick = Tick,
				UpdatedTick = Tick,
				SettlementId = Book.SettlementId,
				WaterState = KingdomLifecyclePhysicalState.Skipped,
				RemovalState = KingdomLifecyclePhysicalState.Skipped,
				EffectState = KingdomLifecyclePhysicalState.Skipped
			};
		}

		public static KingdomLifecycleResourceLease PrepareLease(KingdomLifecycleBook Book,
			KingdomLifecycleOperation Operation, KingdomLifecycleResourceKind Kind,
			string ScopeId, string SubjectId, long Before, long Delta)
		{
			return IsDomainResourceKind(Kind)
				? PrepareLeaseCore(Book, Operation, Kind, ScopeId, SubjectId, Before, Delta)
				: null;
		}

		private static KingdomLifecycleResourceLease PrepareLeaseCore(KingdomLifecycleBook Book,
			KingdomLifecycleOperation Operation, KingdomLifecycleResourceKind Kind,
			string ScopeId, string SubjectId, long Before, long Delta)
		{
			if (!CanOwnAuthority(Book) || Operation == null || Delta == 0L) return null;
			long after;
			if (!CheckedAdd(Before, Delta, out after)) return null;
			string key = ResourceKey(Kind, ScopeId, SubjectId);
			if (key == null) return null;
			KingdomLifecycleResourceRevision row = FindResource(Book, key);
			if (row != null && (!string.IsNullOrEmpty(row.ActiveOperationId)
				|| row.Kind != Kind || row.ScopeId != ScopeId || row.SubjectId != SubjectId)) return null;
			long revision = row == null ? 0L : row.Revision;
			if (revision < 0L || revision == long.MaxValue) return null;
			return new KingdomLifecycleResourceLease
			{
				OperationId = Operation.Id,
				Kind = Kind,
				ScopeId = ScopeId,
				SubjectId = SubjectId,
				Key = key,
				Before = Before,
				Delta = Delta,
				After = after,
				BeforeRevision = revision,
				AfterRevision = revision + 1L,
				State = KingdomLifecycleLeaseState.Prepared
			};
		}

		public static KingdomLifecycleOutbox PrepareOutbox(KingdomLifecycleOperation Operation,
			string Chronicle, string Ledger, string Message, string Deed, string Guestbook)
		{
			if (Operation == null || !CanonicalOperationId(Operation)) return null;
			return new KingdomLifecycleOutbox
			{
				OperationId = Operation.Id,
				EventId = ChildId(Operation.Id, "outbox", 0),
				ChronicleReceiptId = ChildId(Operation.Id, "chronicle", 0),
				Chronicle = Chronicle,
				ChronicleDisposition = InitialDisposition(Chronicle),
				ChronicleState = InitialSink(Chronicle),
				Ledger = Ledger,
				LedgerDisposition = InitialDisposition(Ledger),
				LedgerState = InitialSink(Ledger),
				Message = Message,
				MessageDisposition = InitialDisposition(Message),
				MessageState = InitialSink(Message),
				Deed = Deed,
				DeedDisposition = InitialDisposition(Deed),
				DeedState = InitialSink(Deed),
				GuestbookLine = Guestbook,
				GuestbookDisposition = InitialDisposition(Guestbook),
				GuestbookState = InitialSink(Guestbook)
			};
		}

		public static bool TryPublish(KingdomLifecycleBook Book,
			KingdomLifecycleOperation Operation)
		{
			if (!CanOwnAuthority(Book) || Operation == null
				|| GetSlot(Book, Operation.Lane) != null
				|| !string.Equals(Operation.SettlementId, Book.SettlementId,
					StringComparison.Ordinal)
				|| Operation.Sequence != GetNextSequence(Book, Operation.Lane)
				|| !IsExactSuccessor(Operation.Sequence,
					GetRetiredThrough(Book, Operation.Lane))
				|| Operation.Sequence == long.MaxValue
				|| !CanonicalOperationId(Operation)
				|| Operation.Phase != KingdomLifecyclePhase.Prepared
				|| (Operation.Lane == KingdomLifecycleLane.Raid
					&& !KingdomRaidIncidentRules.CanPublish(Book.RaidLedger, Operation))
				|| !PublicationPlanValid(Operation)) return false;

			string expectedHash;
			if (!TryPlanHash(Operation, out expectedHash)) return false;
			if (!string.IsNullOrEmpty(Operation.PlanHash)
				&& !string.Equals(Operation.PlanHash, expectedHash, StringComparison.Ordinal)) return false;

			List<KingdomLifecycleResourceRevision> rows = new List<KingdomLifecycleResourceRevision>();
			int newRows = 0;
			HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Operation.ResourceLeases.Count; i++)
			{
				KingdomLifecycleResourceLease lease = Operation.ResourceLeases[i];
				if (!LeaseShape(lease, Operation.Id, true) || !keys.Add(lease.Key)) return false;
				KingdomLifecycleResourceRevision row = FindResource(Book, lease.Key);
				if (row == null)
				{
					row = new KingdomLifecycleResourceRevision
					{
						Kind = lease.Kind, ScopeId = lease.ScopeId, SubjectId = lease.SubjectId,
						Key = lease.Key, Revision = 0L
					};
					newRows++;
				}
				if (!ResourceMatches(row, lease) || row.Revision != lease.BeforeRevision
					|| !string.IsNullOrEmpty(row.ActiveOperationId)
					|| string.Equals(row.LastOperationId, Operation.Id,
						StringComparison.Ordinal)) return false;
				rows.Add(row);
			}
			if (Book.Resources.Count + newRows > MaxResourceRows) return false;

			Operation.PlanHash = expectedHash;
			for (int i = 0; i < rows.Count; i++)
			{
				if (FindResource(Book, rows[i].Key) == null) Book.Resources.Add(rows[i]);
				rows[i].ActiveOperationId = Operation.Id;
			}
			SetNextSequence(Book, Operation.Lane, Operation.Sequence + 1L);
			SetSlot(Book, Operation.Lane, Operation);
			return true;
		}

		public static KingdomLifecycleCasAction LeaseAction(KingdomLifecycleBook Book,
			KingdomLifecycleResourceLease Lease, long CurrentValue)
		{
			return Lease != null && IsDomainResourceKind(Lease.Kind)
				? LeaseActionCore(Book, Lease, CurrentValue)
				: KingdomLifecycleCasAction.Quarantine;
		}

		private static KingdomLifecycleCasAction LeaseActionCore(KingdomLifecycleBook Book,
			KingdomLifecycleResourceLease Lease, long CurrentValue)
		{
			KingdomLifecycleOperation operation;
			KingdomLifecycleResourceRevision row;
			if (!ExactLeaseAuthority(Book, Lease, out operation, out row)
				|| !LeasePhaseAllows(operation, Lease)) return KingdomLifecycleCasAction.Quarantine;
			return LeaseSnapshotAction(CurrentValue, row.Revision, row.LastOperationId,
				row.ActiveOperationId, Lease);
		}

	}
}
