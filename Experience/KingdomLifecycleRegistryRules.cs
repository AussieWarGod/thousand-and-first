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
		private static bool CarryResourceRegistryValid(KingdomCarryBook Book)
		{
			if (Book == null || Book.Resources == null || Book.Resources.Count > MaxResourceRows)
				return false;
			HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Book.Resources.Count; i++)
				if (!ResourceShape(Book.Resources[i]) || !keys.Add(Book.Resources[i].Key))
					return false;
			return true;
		}

		private static bool CarryActiveResourcesValid(KingdomCarryBook Book)
		{
			if (!CarryResourceRegistryValid(Book)) return false;
			for (int i = 0; i < Book.Resources.Count; i++)
			{
				KingdomLifecycleResourceRevision row = Book.Resources[i];
				if (row.ActiveOperationId == null) continue;
				if (Book.Open == null || !string.Equals(row.ActiveOperationId,
					Book.Open.Id, StringComparison.Ordinal)
					|| !string.Equals(row.Key, Book.Open.ScheduleLease == null
						? null : Book.Open.ScheduleLease.Key, StringComparison.Ordinal)
					|| !ResourceWitnessMatches(row, Book.Open.ScheduleLease)) return false;
			}
			if (Book.Open == null) return true;
			return Book.Open.ScheduleLease != null
				&& ResourceWitnessMatches(FindResource(Book, Book.Open.ScheduleLease.Key),
					Book.Open.ScheduleLease);
		}

		private static bool ActiveResourcesValid(KingdomLifecycleBook Book)
		{
			if (!ResourceRegistryValid(Book)) return false;
			for (int i = 0; i < Book.Resources.Count; i++)
			{
				KingdomLifecycleResourceRevision row = Book.Resources[i];
				if (row.ActiveOperationId == null) continue;
				KingdomLifecycleOperation op = FindOpenOperation(Book, row.ActiveOperationId);
				KingdomLifecycleResourceLease lease = op == null ? null : FindLease(op, row.Key);
				if (lease == null || !ResourceWitnessMatches(row, lease)) return false;
			}
			return OperationResourcesValid(Book, Book.PlainGuest)
				&& OperationResourcesValid(Book, Book.NotableGuest)
				&& OperationResourcesValid(Book, Book.Raid)
				&& OperationResourcesValid(Book, Book.Petition);
		}

		private static bool OperationResourcesValid(KingdomLifecycleBook book,
			KingdomLifecycleOperation operation)
		{
			if (operation == null) return true;
			if (operation.ResourceLeases == null) return false;
			for (int i = 0; i < operation.ResourceLeases.Count; i++)
			{
				KingdomLifecycleResourceLease lease = operation.ResourceLeases[i];
				KingdomLifecycleResourceRevision row = lease == null
					? null : FindResource(book, lease.Key);
				if (!LeaseStateAllowedAtPhase(operation, lease)) return false;
				if (LodgeAuthorityReleased(operation))
				{
					if (!ReleasedResourceWitnessMatches(row, lease)) return false;
				}
				else if (!ResourceWitnessMatches(row, lease)) return false;
			}
			return true;
		}

		private static bool ResourceWitnessMatches(KingdomLifecycleResourceRevision row,
			KingdomLifecycleResourceLease lease)
		{
			if (!ResourceMatches(row, lease)
				|| !string.Equals(row.ActiveOperationId, lease.OperationId,
					StringComparison.Ordinal)) return false;
			if (lease.State == KingdomLifecycleLeaseState.Prepared
				|| lease.State == KingdomLifecycleLeaseState.Intent
				|| lease.State == KingdomLifecycleLeaseState.Skipped)
				return row.Revision == lease.BeforeRevision
					&& !string.Equals(row.LastOperationId, lease.OperationId,
						StringComparison.Ordinal);
			if (lease.State == KingdomLifecycleLeaseState.Proved)
				return row.Revision == lease.AfterRevision
					&& string.Equals(row.LastOperationId, lease.OperationId,
						StringComparison.Ordinal);
			return false;
		}

		private static KingdomLifecycleOperation FindOpenOperation(KingdomLifecycleBook Book,
			string Id)
		{
			if (Book == null || string.IsNullOrEmpty(Id)) return null;
			if (Book.PlainGuest != null && Book.PlainGuest.Id == Id) return Book.PlainGuest;
			if (Book.NotableGuest != null && Book.NotableGuest.Id == Id) return Book.NotableGuest;
			if (Book.Raid != null && Book.Raid.Id == Id) return Book.Raid;
			if (Book.Petition != null && Book.Petition.Id == Id) return Book.Petition;
			return null;
		}

		private static KingdomLifecycleResourceRevision FindResource(KingdomLifecycleBook Book,
			string Key)
		{
			if (Book == null || Book.Resources == null || Key == null) return null;
			for (int i = 0; i < Book.Resources.Count; i++)
				if (Book.Resources[i] != null && Book.Resources[i].Key == Key) return Book.Resources[i];
			return null;
		}

		private static KingdomLifecycleResourceRevision FindResource(KingdomCarryBook Book,
			string Key)
		{
			if (Book == null || Book.Resources == null || Key == null) return null;
			for (int i = 0; i < Book.Resources.Count; i++)
				if (Book.Resources[i] != null && string.Equals(Book.Resources[i].Key, Key,
					StringComparison.Ordinal)) return Book.Resources[i];
			return null;
		}

		private static KingdomLifecycleResourceLease FindLease(KingdomLifecycleOperation op,
			string Key)
		{
			if (op == null || op.ResourceLeases == null || Key == null) return null;
			for (int i = 0; i < op.ResourceLeases.Count; i++)
				if (op.ResourceLeases[i] != null && op.ResourceLeases[i].Key == Key)
					return op.ResourceLeases[i];
			return null;
		}

		private static bool HasLease(KingdomLifecycleOperation op,
			KingdomLifecycleResourceKind Kind)
		{
			if (op == null || op.ResourceLeases == null) return false;
			for (int i = 0; i < op.ResourceLeases.Count; i++)
				if (op.ResourceLeases[i] != null && op.ResourceLeases[i].Kind == Kind) return true;
			return false;
		}

		private static bool HasDomainLease(KingdomLifecycleOperation op)
		{
			if (op == null || op.ResourceLeases == null) return false;
			for (int i = 0; i < op.ResourceLeases.Count; i++)
				if (op.ResourceLeases[i] != null
					&& op.ResourceLeases[i].Kind != KingdomLifecycleResourceKind.Schedule
					&& op.ResourceLeases[i].Kind != KingdomLifecycleResourceKind.WaterVessel
					&& op.ResourceLeases[i].Kind != KingdomLifecycleResourceKind.Projection
					&& op.ResourceLeases[i].Kind != KingdomLifecycleResourceKind.Object) return true;
			return false;
		}

		private static bool IsDomainLease(KingdomLifecycleResourceLease lease)
		{
			return lease != null
				&& lease.Kind != KingdomLifecycleResourceKind.Schedule
				&& lease.Kind != KingdomLifecycleResourceKind.WaterVessel
				&& lease.Kind != KingdomLifecycleResourceKind.Projection
				&& lease.Kind != KingdomLifecycleResourceKind.Object;
		}

		private static KingdomLifecycleResourceLease RequiredDomainLease(
			KingdomLifecycleOperation op)
		{
			if (op == null || op.ResourceLeases == null) return null;
			KingdomLifecycleResourceLease found = null;
			for (int i = 0; i < op.ResourceLeases.Count; i++)
				if (IsDomainLease(op.ResourceLeases[i]))
				{
					if (found != null) return null;
					found = op.ResourceLeases[i];
				}
			return found;
		}

		private static bool IsRequiredDomainLease(KingdomLifecycleOperation op,
			KingdomLifecycleResourceLease lease)
		{
			KingdomLifecycleResourceKind kind;
			long delta;
			return lease != null && TryRequiredDomain(op, out kind, out delta)
				&& lease.Kind == kind && lease.Delta == delta
				&& string.Equals(lease.ScopeId, op.SettlementId, StringComparison.Ordinal)
				&& string.Equals(lease.SubjectId, op.SettlementId, StringComparison.Ordinal)
				&& ReferenceEquals(RequiredDomainLease(op), lease);
		}

		private static bool TryRequiredDomain(KingdomLifecycleOperation op,
			out KingdomLifecycleResourceKind kind, out long delta)
		{
			kind = KingdomLifecycleResourceKind.None;
			delta = 0L;
			if (op == null) return false;
			switch (op.Action)
			{
			case KingdomLifecycleAction.Passages:
				return false;
			case KingdomLifecycleAction.Spawn:
				kind = KingdomLifecycleResourceKind.Population; delta = op.PartySize; break;
			case KingdomLifecycleAction.Depart:
				kind = KingdomLifecycleResourceKind.Population; delta = -op.Count; break;
			case KingdomLifecycleAction.OfferWater:
				kind = KingdomLifecycleResourceKind.Standing; delta = op.WaterRequested; break;
			case KingdomLifecycleAction.Lodge:
				kind = KingdomLifecycleResourceKind.Roster; delta = 1L; break;
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
				kind = KingdomLifecycleResourceKind.Raid; delta = 1L; break;
			case KingdomLifecycleAction.PetitionOffer:
			case KingdomLifecycleAction.PetitionAccept:
			case KingdomLifecycleAction.PetitionDecline:
			case KingdomLifecycleAction.PetitionResolve:
			case KingdomLifecycleAction.PetitionExpire:
				kind = KingdomLifecycleResourceKind.Petition; delta = 1L; break;
			default:
				return false;
			}
			return delta != 0L;
		}

		private static bool RequiresDomainLease(KingdomLifecycleAction action)
		{
			return action != KingdomLifecycleAction.Passages;
		}

		private static void AppendProof(List<KingdomLifecycleProof> Proofs,
			KingdomLifecycleProof Proof)
		{
			Proofs.Add(Proof);
			while (Proofs.Count > MaxRecentProofs) Proofs.RemoveAt(0);
		}

		private static int IndexOfSource(KingdomCarryOperation op, KingdomCarrySource source)
		{
			if (op == null || op.Sources == null) return -1;
			for (int i = 0; i < op.Sources.Count; i++) if (ReferenceEquals(op.Sources[i], source)) return i;
			return -1;
		}

		private static int IndexOfOutput(KingdomCarryOperation op,
			KingdomLifecycleProjection output)
		{
			if (op == null || op.Outputs == null) return -1;
			for (int i = 0; i < op.Outputs.Count; i++)
				if (ReferenceEquals(op.Outputs[i], output)) return i;
			return -1;
		}

		private static int FirstIncompleteSource(KingdomCarryOperation op)
		{
			if (op == null || op.Sources == null) return 0;
			for (int i = 0; i < op.Sources.Count; i++)
				if (op.Sources[i] == null
					|| op.Sources[i].State != KingdomLifecyclePhysicalState.Proved) return i;
			return op.Sources.Count;
		}

		private static KingdomLifecycleOperation GetSlot(KingdomLifecycleBook Book,
			KingdomLifecycleLane Lane)
		{
			if (Book == null) return null;
			switch (Lane)
			{
			case KingdomLifecycleLane.PlainGuest: return Book.PlainGuest;
			case KingdomLifecycleLane.NotableGuest: return Book.NotableGuest;
			case KingdomLifecycleLane.Raid: return Book.Raid;
			case KingdomLifecycleLane.Petition: return Book.Petition;
			default: return null;
			}
		}

	}
}
