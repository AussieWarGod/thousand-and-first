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
		private static bool PlanShape(KingdomLifecycleOperation op, bool Publication)
		{
			if (op == null || !ActionAllowedInLane(op.Action, op.Lane)
				|| !CanonicalOperationId(op) || !ValidRootId(op.SettlementId)
				|| op.CreatedTick < 0L || op.UpdatedTick < op.CreatedTick
				|| op.DueBefore < 0L || op.DueAfter < 0L || op.DepartTick < 0L
				|| TooLong(op.ZoneId, MaxNameChars) || TooLong(op.ObjectId, MaxIdChars)
				|| TooLong(op.ObjectMarker, MaxIdChars) || TooLong(op.Blueprint, MaxNameChars)
				|| TooLong(op.ObjectOwnerId, MaxIdChars)
				|| TooLong(op.ObjectName, MaxNameChars) || TooLong(op.Origin, MaxNameChars)
				|| TooLong(op.Faction, MaxNameChars) || TooLong(op.DisplayFaction, MaxNameChars)
				|| TooLong(op.Detail, MaxTextChars) || TooLong(op.Creed, MaxNameChars)
				|| TooLong(op.ArrivalText, MaxTextChars) || TooLong(op.Fault, MaxTextChars)
				|| !ValidCount(op.Count) || !ValidCount(op.DepartedCount)
				|| !ValidCount(op.Spawned) || !ValidCount(op.PlunderRequested)
				|| !ValidCount(op.PlunderProved)
				|| op.WaterLegs == null || op.WaterLegs.Count > MaxWaterLegs
				|| op.Projections == null || op.Projections.Count > MaxProjections
				|| op.ResourceLeases == null || op.ResourceLeases.Count > MaxResourceLeases
				|| !KnownPhysical(op.WaterState) || !KnownPhysical(op.RemovalState)
				|| !KnownPhysical(op.EffectState) || !OutboxShape(op, Publication)) return false;

			HashSet<string> waterOwners = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < op.WaterLegs.Count; i++)
					if (!WaterLegShape(op.WaterLegs[i], op, i, Publication)
					|| !waterOwners.Add(op.WaterLegs[i].OwnerId)) return false;
			HashSet<string> objects = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> events = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> markers = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < op.Projections.Count; i++)
			{
				KingdomLifecycleProjection p = op.Projections[i];
				if (!ProjectionShape(p, op.Id, i, Publication)
					|| !LifecycleProjectionReceiptPristine(p)
					|| !objects.Add(p.ObjectId) || !events.Add(p.EventId) || !markers.Add(p.Marker))
					return false;
				string topology = TopologyId(p.Topology, p.OwnerId, p.ZoneId, p.X, p.Y);
				KingdomLifecycleResourceLease projectionLease = FindLease(op,
					ResourceKey(KingdomLifecycleResourceKind.Projection, topology, p.ObjectId));
				if (projectionLease == null || projectionLease.Before != 0L
					|| projectionLease.Delta != p.Count || projectionLease.After != p.Count) return false;
			}
			HashSet<string> leases = new HashSet<string>(StringComparer.Ordinal);
			int scheduleLeases = 0;
			int waterLeases = 0;
			int projectionLeases = 0;
			int objectLeases = 0;
			int domainLeases = 0;
			for (int i = 0; i < op.ResourceLeases.Count; i++)
			{
				KingdomLifecycleResourceLease lease = op.ResourceLeases[i];
				if (!LeaseShape(lease, op.Id, Publication) || !leases.Add(lease.Key)) return false;
				switch (lease.Kind)
				{
				case KingdomLifecycleResourceKind.Schedule: scheduleLeases++; break;
				case KingdomLifecycleResourceKind.WaterVessel: waterLeases++; break;
				case KingdomLifecycleResourceKind.Projection: projectionLeases++; break;
				case KingdomLifecycleResourceKind.Object: objectLeases++; break;
				default: domainLeases++; break;
				}
			}
			string scheduleSubject = ScheduleSubjectId(op.SettlementId, op.Lane);
			KingdomLifecycleResourceLease schedule = FindLease(op,
				ResourceKey(KingdomLifecycleResourceKind.Schedule, op.SettlementId, scheduleSubject));
			if (scheduleLeases != 1 || schedule == null || schedule.Before != op.DueBefore
				|| schedule.After != op.DueAfter || schedule.Delta != op.DueAfter - op.DueBefore)
				return false;

			bool needsWater = op.Action == KingdomLifecycleAction.OfferWater
				|| op.Action == KingdomLifecycleAction.Lodge
				|| op.Action == KingdomLifecycleAction.RaidTribute;
			bool waterSkipped = op.WaterRequested == 0 && op.WaterProved == 0
				&& op.WaterOutstanding == 0 && op.WaterLost == 0 && op.WaterAmbiguous == 0
				&& op.WaterLegs.Count == 0 && op.WaterState == KingdomLifecyclePhysicalState.Skipped;
			if (needsWater)
			{
				if (op.WaterRequested <= 0 || !WaterConserved(op, false)) return false;
				if (Publication && !ExternalRaidTributeReceipt(op)
					&& (op.WaterProved != 0 || op.WaterOutstanding != op.WaterRequested
					|| op.WaterLost != 0 || op.WaterAmbiguous != 0
					|| op.WaterState != KingdomLifecyclePhysicalState.Prepared)) return false;
			}
			else if (!waterSkipped && op.Action != KingdomLifecycleAction.RaidAttack) return false;
			else if (op.Action == KingdomLifecycleAction.RaidAttack
				&& !waterSkipped && !WaterConserved(op, false)) return false;
			if (waterLeases != op.WaterLegs.Count) return false;

			bool needsProjection = op.Action == KingdomLifecycleAction.Spawn
				|| op.Action == KingdomLifecycleAction.RaidAttack
				|| op.Action == KingdomLifecycleAction.RaidDeliverDemand;
			if (needsProjection && op.Projections.Count == 0) return false;
			if (!needsProjection && op.Projections.Count != 0) return false;
			if (projectionLeases != op.Projections.Count) return false;
			bool needsRemoval = op.Action == KingdomLifecycleAction.Depart
				|| op.Action == KingdomLifecycleAction.OfferWater;
			if (needsRemoval)
			{
				if (!ValidRootId(op.ObjectId) || !ValidName(op.Blueprint) || op.Count <= 0
					|| !TopologyValid(op.ObjectTopology, op.ObjectOwnerId, op.ZoneId,
						op.ObjectX, op.ObjectY)) return false;
				string topology = TopologyId(op.ObjectTopology, op.ObjectOwnerId, op.ZoneId,
					op.ObjectX, op.ObjectY);
				KingdomLifecycleResourceLease objectLease = FindLease(op,
					ResourceKey(KingdomLifecycleResourceKind.Object, topology, op.ObjectId));
				if (objectLease == null || objectLease.Before != op.Count
					|| objectLease.Delta != -op.Count || objectLease.After != 0L) return false;
				if (Publication && op.RemovalState != KingdomLifecyclePhysicalState.Prepared) return false;
			}
			else if (op.RemovalState != KingdomLifecyclePhysicalState.Skipped
				|| op.ObjectTopology != KingdomLifecycleTopology.None
				|| !string.IsNullOrEmpty(op.ObjectOwnerId) || op.ObjectX != -1 || op.ObjectY != -1)
				return false;
			if (objectLeases != (needsRemoval ? 1 : 0)) return false;
			bool needsEffect = op.Action == KingdomLifecycleAction.RaidAttack;
			if (Publication && needsEffect && op.EffectState != KingdomLifecyclePhysicalState.Prepared)
				return false;
			if (!needsEffect && op.EffectState != KingdomLifecyclePhysicalState.Skipped) return false;
			if (Publication)
			{
				if (op.Spawned != 0 || op.PlunderProved != 0 || op.DepartedCount != 0)
					return false;
				for (int i = 0; i < op.Projections.Count; i++)
					if (op.Projections[i].State != KingdomLifecyclePhysicalState.Prepared) return false;
			}
			KingdomLifecycleResourceKind requiredKind;
			long requiredDelta;
			bool requiresDomain = TryRequiredDomain(op, out requiredKind, out requiredDelta);
			if (requiresDomain)
			{
				KingdomLifecycleResourceLease domain = null;
				for (int i = 0; i < op.ResourceLeases.Count; i++)
					if (IsDomainLease(op.ResourceLeases[i])) domain = op.ResourceLeases[i];
				if (domainLeases != 1 || domain == null || domain.Kind != requiredKind
					|| !string.Equals(domain.ScopeId, op.SettlementId, StringComparison.Ordinal)
					|| !string.Equals(domain.SubjectId, op.SettlementId, StringComparison.Ordinal)
					|| domain.Delta != requiredDelta || domain.Before < 0L || domain.After < 0L)
					return false;
			}
			else if (domainLeases != 0) return false;
			if (op.Action == KingdomLifecycleAction.Depart)
			{
				KingdomLifecycleResourceLease domain = RequiredDomainLease(op);
				bool proved = domain != null && domain.State == KingdomLifecycleLeaseState.Proved;
				if (op.DepartedCount != (proved ? op.Count : 0)) return false;
			}
			else if (op.DepartedCount != 0) return false;
			if (Publication && op.DepartedCount != 0) return false;
			return ConservationEquations(op, false);
		}

		private static bool TerminalComponentsSettled(KingdomLifecycleBook Book,
			KingdomLifecycleOperation op)
		{
			if (Book == null || op == null || !PlanShape(op, false) || !OutboxTerminal(op)
				|| !ConservationEquations(op, true)) return false;
			for (int i = 0; i < op.ResourceLeases.Count; i++)
			{
				KingdomLifecycleResourceLease lease = op.ResourceLeases[i];
				KingdomLifecycleResourceRevision row = FindResource(Book, lease.Key);
				if (lease.State != KingdomLifecycleLeaseState.Proved || row == null
					|| row.Revision != lease.AfterRevision
					|| !string.Equals(row.ActiveOperationId, op.Id, StringComparison.Ordinal)
					|| !string.Equals(row.LastOperationId, op.Id, StringComparison.Ordinal)) return false;
			}
			for (int i = 0; i < op.Projections.Count; i++)
				if (op.Projections[i].State != KingdomLifecyclePhysicalState.Proved
					&& op.Projections[i].State != KingdomLifecyclePhysicalState.Skipped) return false;
			if (op.RemovalState != KingdomLifecyclePhysicalState.Proved
				&& op.RemovalState != KingdomLifecyclePhysicalState.Skipped) return false;
			if (op.EffectState != KingdomLifecyclePhysicalState.Proved
				&& op.EffectState != KingdomLifecyclePhysicalState.Skipped) return false;
			return true;
		}

		private static bool ConservationEquations(KingdomLifecycleOperation op, bool Terminal)
		{
			if (!WaterConserved(op, Terminal)) return false;
			if (op.PlunderProved > op.PlunderRequested || !ProjectionConserved(op, Terminal)) return false;
			for (int i = 0; i < op.ResourceLeases.Count; i++)
			{
				KingdomLifecycleResourceLease lease = op.ResourceLeases[i];
				long after;
				if (!CheckedAdd(lease.Before, lease.Delta, out after) || after != lease.After
					|| lease.BeforeRevision < 0L || lease.BeforeRevision == long.MaxValue
					|| lease.AfterRevision != lease.BeforeRevision + 1L) return false;
			}
			KingdomLifecycleResourceLease domain = RequiredDomainLease(op);
			if (RequiresDomainLease(op.Action) && domain == null) return false;
			if (op.Action == KingdomLifecycleAction.Depart)
			{
				bool proved = domain != null && domain.State == KingdomLifecycleLeaseState.Proved;
				if (op.DepartedCount != (proved ? op.Count : 0)
					|| (Terminal && op.DepartedCount != op.Count)) return false;
			}
			else if (op.DepartedCount != 0) return false;
			return true;
		}

		private static bool ProjectionConserved(KingdomLifecycleOperation op, bool terminal)
		{
			bool projects = op.Action == KingdomLifecycleAction.Spawn
				|| op.Action == KingdomLifecycleAction.RaidAttack
				|| op.Action == KingdomLifecycleAction.RaidDeliverDemand;
			if (!projects) return op.Spawned == 0 && op.PartySize == 0;
			long planned = 0L;
			long proved = 0L;
			for (int i = 0; i < op.Projections.Count; i++)
			{
				planned += op.Projections[i].Count;
				if (op.Projections[i].State == KingdomLifecyclePhysicalState.Proved)
					proved += op.Projections[i].Count;
			}
			if (planned != op.PartySize || op.Spawned < 0 || op.Spawned > proved) return false;
			return !terminal || (proved == planned && op.Spawned == proved);
		}

		public static KingdomLifecycleSinkMask RequiredSinks(KingdomLifecycleAction Action,
			KingdomLifecycleLane Lane)
		{
			KingdomLifecycleSinkMask common = KingdomLifecycleSinkMask.Chronicle
				| KingdomLifecycleSinkMask.Ledger;
			switch (Action)
			{
			case KingdomLifecycleAction.Passages:
				// An absence with no traffic is explicitly not news. Plans may still carry the
				// dated aggregate when Count > 0; no empty sink is mandatory.
				return KingdomLifecycleSinkMask.None;
			case KingdomLifecycleAction.Depart:
				return common | (Lane == KingdomLifecycleLane.NotableGuest
					? KingdomLifecycleSinkMask.Guestbook : KingdomLifecycleSinkMask.None);
			case KingdomLifecycleAction.Lodge:
				return common | KingdomLifecycleSinkMask.Message
					| KingdomLifecycleSinkMask.Guestbook;
			case KingdomLifecycleAction.Spawn:
				return Lane == KingdomLifecycleLane.NotableGuest
					? common | KingdomLifecycleSinkMask.Message
						| KingdomLifecycleSinkMask.Guestbook
					: KingdomLifecycleSinkMask.None;
			case KingdomLifecycleAction.OfferWater:
			case KingdomLifecycleAction.RaidWarning:
			case KingdomLifecycleAction.RaidRewarning:
			case KingdomLifecycleAction.RaidTribute:
			case KingdomLifecycleAction.RaidTalkDown:
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
			case KingdomLifecycleAction.PetitionOffer:
			case KingdomLifecycleAction.PetitionAccept:
			case KingdomLifecycleAction.PetitionDecline:
			case KingdomLifecycleAction.PetitionResolve:
			case KingdomLifecycleAction.PetitionExpire:
				return common | KingdomLifecycleSinkMask.Message;
			case KingdomLifecycleAction.RaidAttack:
				return common | KingdomLifecycleSinkMask.Message | KingdomLifecycleSinkMask.Deed;
			default:
				return KingdomLifecycleSinkMask.None;
			}
		}

	}
}
