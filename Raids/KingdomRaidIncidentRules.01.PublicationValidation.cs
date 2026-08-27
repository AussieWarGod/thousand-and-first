using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomRaidIncidentRules
	{
		public static bool CanPublish(KingdomRaidLedger Ledger,
			KingdomLifecycleOperation Operation)
		{
			if (!CurrentLedger(Ledger) || !ValidLedger(Ledger) || Operation == null
				|| Operation.Lane != KingdomLifecycleLane.Raid) return false;
			KingdomRaidIncident active = Active(Ledger);
			KingdomRaidIncident subject = Incident(Ledger, Operation.ObjectId);
			switch (Operation.Action)
			{
			case KingdomLifecycleAction.RaidWarning:
				return Ledger.Grievances.Count < KingdomLifecycleRules.MaxRaidGrievances
					&& Ledger.Incidents.Count < KingdomLifecycleRules.MaxRaidIncidents
					&& ValidId(Operation.Origin) && !SourceConsumed(Ledger, Operation.Origin)
					&& string.Equals(Operation.ObjectId, GrievanceId(Operation.Origin),
						StringComparison.Ordinal)
					&& string.Equals(Operation.ObjectMarker, IncidentId(Operation.ObjectId),
						StringComparison.Ordinal)
					&& ValidName(Operation.Faction) && ValidName(Operation.DisplayFaction)
					&& ValidName(Operation.Creed) && ValidName(Operation.ObjectName)
					&& ValidName(Operation.ZoneId) && ValidName(Operation.Blueprint)
					&& !string.IsNullOrEmpty(Operation.Detail)
					&& Bounded(Operation.Detail, 4096) && Bounded(Operation.ArrivalText, 512)
					&& Operation.Target >= 1 && Operation.Target <= MaxSeverity
					&& Operation.PlunderRequested > 0 && Operation.PlunderRequested <= MaxStake
					&& Operation.Kind >= Operation.PlunderRequested && Operation.Kind <= MaxStake
					&& Operation.Count > 0 && Operation.Count <= MaxParty
					&& Operation.Defence == 0 && Operation.PartySize == 0
					&& Operation.Spawned == 0 && Operation.PlunderProved == 0
					&& Operation.DepartTick > Operation.CreatedTick;
			case KingdomLifecycleAction.RaidCancel:
				return active == null
					? !Ledger.LegacyEvidenceArchived
						&& Operation.Kind == (int)KingdomRaidResolution.LegacyWarningDispersed
						&& Operation.Target >= 0 && Operation.Count >= 0
						&& Operation.DepartTick >= 0L && Bounded(Operation.Faction, 512)
					: Matches(active, Operation) && Operation.Target == 0
						&& Operation.Count == 0 && CancelResolution(Operation.Kind);
			case KingdomLifecycleAction.RaidRewarning:
			case KingdomLifecycleAction.RaidDeadline:
				return active != null && Matches(active, Operation)
					&& active.State == KingdomRaidIncidentState.Warned
					&& active.ChannelState == KingdomRaidChannelState.Acknowledged
					&& active.DueTick > active.DeliveredTick
					&& Operation.CreatedTick >= active.DueTick;
			case KingdomLifecycleAction.RaidDeliverDemand:
				return active != null && Matches(active, Operation)
					&& (active.State == KingdomRaidIncidentState.Rumored
						|| active.State == KingdomRaidIncidentState.Warned
						|| active.State == KingdomRaidIncidentState.ConfrontationReady)
					&& (active.ChannelState == KingdomRaidChannelState.AwaitingDelivery
						|| active.ChannelState == KingdomRaidChannelState.RedeliveryQueued)
					&& active.ChannelRevision < int.MaxValue
					&& Operation.Target == active.ChannelRevision + 1
					&& string.Equals(Operation.Origin, active.DemandChannelId, StringComparison.Ordinal)
					&& string.Equals(Operation.ObjectMarker,
						DemandObjectId(active.DemandChannelId, Operation.Target), StringComparison.Ordinal)
					&& ValidName(Operation.Blueprint) && Operation.Count == 1;
			case KingdomLifecycleAction.RaidAcknowledgeDemand:
				return active != null && Matches(active, Operation)
					&& (active.State == KingdomRaidIncidentState.Rumored
						|| active.State == KingdomRaidIncidentState.Warned
						|| active.State == KingdomRaidIncidentState.ConfrontationReady)
					&& active.ChannelState == KingdomRaidChannelState.Issued
					&& string.Equals(Operation.Origin, active.DemandObjectId, StringComparison.Ordinal)
					&& (active.State == KingdomRaidIncidentState.ConfrontationReady
						? Operation.DepartTick == 0L
						: Operation.DepartTick > Operation.CreatedTick);
			case KingdomLifecycleAction.RaidLoseChannel:
				return active != null && Matches(active, Operation)
					&& (active.ChannelState == KingdomRaidChannelState.Issued
						|| active.ChannelState == KingdomRaidChannelState.Acknowledged)
					&& string.Equals(Operation.Origin, active.DemandObjectId, StringComparison.Ordinal)
					&& (active.State == KingdomRaidIncidentState.Rumored
						|| active.State == KingdomRaidIncidentState.Warned
						|| active.State == KingdomRaidIncidentState.ConfrontationReady);
			case KingdomLifecycleAction.RaidTribute:
				return active != null && Matches(active, Operation) && CanAnswer(active)
					&& Operation.WaterRequested == active.DisclosedStake
					&& (Operation.WaterProved == Operation.WaterRequested
						|| Operation.WaterProved == 0
							&& Operation.WaterOutstanding == Operation.WaterRequested);
			case KingdomLifecycleAction.RaidTalkDown:
			case KingdomLifecycleAction.RaidFight:
				return active != null && Matches(active, Operation) && CanAnswer(active);
			case KingdomLifecycleAction.RaidFortify:
				return active != null && Matches(active, Operation)
					&& (CanAnswer(active)
						|| active.State == KingdomRaidIncidentState.FortifyOrdered)
					&& !string.IsNullOrEmpty(Operation.Detail)
					&& Bounded(Operation.Detail, 4096) && Operation.Defence > 0
					&& Operation.Defence <= KingdomLifecycleRules.MaxPhysicalCount;
			case KingdomLifecycleAction.RaidFortifyOrder:
				return active != null && Matches(active, Operation) && CanAnswer(active)
					&& Operation.Defence == 0 && string.IsNullOrEmpty(Operation.Detail);
			case KingdomLifecycleAction.RaidFortifyFailure:
				return active != null && Matches(active, Operation)
					&& (active.State == KingdomRaidIncidentState.FortifyOrdered
						|| active.State == KingdomRaidIncidentState.Fortified)
					&& Operation.Defence == 0;
			case KingdomLifecycleAction.RaidAttack:
				return active != null && Matches(active, Operation)
					&& (active.State == KingdomRaidIncidentState.FightCommitted
						|| active.State == KingdomRaidIncidentState.Fortified)
					&& ValidId(Operation.Origin)
					&& string.Equals(Operation.ArrivalText, "stores", StringComparison.Ordinal)
					&& Operation.Target >= 0
					&& Operation.Target <= KingdomLifecycleRules.MaxPhysicalCount
					&& Operation.Count >= 0
					&& Operation.Count <= KingdomLifecycleRules.MaxPhysicalCount
					&& Operation.Defence >= 0
					&& Operation.Defence <= KingdomLifecycleRules.MaxPhysicalCount
					&& Operation.PartySize > 0 && Operation.PartySize <= MaxParty
					&& (Operation.Spawned == 0 || Operation.Spawned == Operation.PartySize)
					&& Operation.PlunderRequested > 0
					&& Operation.PlunderRequested <= active.MaximumPlunder
					&& Operation.PlunderProved == 0;
			case KingdomLifecycleAction.RaidResolve:
				return active != null && Matches(active, Operation)
					&& active.State == KingdomRaidIncidentState.Active
					&& ResolveResultShape(Operation.Kind, Operation.Target,
						active.MaximumPlunder);
			case KingdomLifecycleAction.RaidRecoveryAccept:
				return RecoveryMatches(subject, Operation)
					&& subject.RecoveryState == KingdomRaidRecoveryState.Offered
					&& string.Equals(Operation.Origin, subject.RecoveryQuestId, StringComparison.Ordinal)
					&& string.Equals(Operation.ObjectMarker, subject.RecoveryStepId, StringComparison.Ordinal);
			case KingdomLifecycleAction.RaidRecoveryReady:
				return RecoveryMatches(subject, Operation)
					&& subject.RecoveryState == KingdomRaidRecoveryState.Active
					&& string.Equals(Operation.Origin, subject.AttackOperationId, StringComparison.Ordinal);
			case KingdomLifecycleAction.RaidRecoveryResolve:
				return RecoveryMatches(subject, Operation)
					&& subject.RecoveryState == KingdomRaidRecoveryState.Ready;
			case KingdomLifecycleAction.RaidRecoveryDecline:
				return RecoveryMatches(subject, Operation)
					&& (subject.RecoveryState == KingdomRaidRecoveryState.Offered
						|| subject.RecoveryState == KingdomRaidRecoveryState.Active
						|| subject.RecoveryState == KingdomRaidRecoveryState.Ready);
			default:
				return false;
			}
		}
	}
}
