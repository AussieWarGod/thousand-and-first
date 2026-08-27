using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomRaidIncidentRules
	{
		private static bool CurrentLedger(KingdomRaidLedger ledger)
		{
			return ledger != null && ledger.Version == KingdomRaidLedger.CurrentVersion
				&& ledger.OpaqueFuturePayload == null;
		}

		private static bool DeadlineShape(KingdomRaidIncident q)
		{
			if (q.DeliveredTick < 0L || q.DueTick < 0L) return false;
			switch (q.State)
			{
			case KingdomRaidIncidentState.Queued:
			case KingdomRaidIncidentState.Rumored:
				return q.DeliveredTick == 0L && q.DueTick == 0L;
			case KingdomRaidIncidentState.Warned:
				if (q.DeliveredTick < q.RumorTick) return false;
				return q.ChannelState == KingdomRaidChannelState.Acknowledged
					? q.DueTick > q.DeliveredTick : q.DueTick == 0L;
			case KingdomRaidIncidentState.ConfrontationReady:
				return q.DeliveredTick >= q.RumorTick && q.DueTick == 0L;
			case KingdomRaidIncidentState.FightCommitted:
			case KingdomRaidIncidentState.FortifyOrdered:
			case KingdomRaidIncidentState.Fortified:
			case KingdomRaidIncidentState.Active:
				return q.DeliveredTick >= q.RumorTick
					&& (q.DueTick == 0L || q.DueTick > q.DeliveredTick);
			case KingdomRaidIncidentState.Resolved:
			case KingdomRaidIncidentState.Cancelled:
			case KingdomRaidIncidentState.Quarantined:
				return q.DueTick == 0L
					&& (q.DeliveredTick == 0L || q.DeliveredTick >= q.RumorTick);
			default:
				return false;
			}
		}

		private static bool ChannelShape(KingdomRaidIncident q)
		{
			if (q.ChannelRevision == 0)
			{
				if (q.DemandObjectId != null) return false;
			}
			else if (!string.Equals(q.DemandObjectId,
				DemandObjectId(q.DemandChannelId, q.ChannelRevision), StringComparison.Ordinal)) return false;
			if (q.State == KingdomRaidIncidentState.Queued)
				return q.ChannelState == KingdomRaidChannelState.None && q.ChannelRevision == 0;
			if (q.State == KingdomRaidIncidentState.Rumored)
				return q.ChannelState == KingdomRaidChannelState.AwaitingDelivery
					|| q.ChannelState == KingdomRaidChannelState.Issued
					|| q.ChannelState == KingdomRaidChannelState.RedeliveryQueued;
			bool terminal = q.State == KingdomRaidIncidentState.Resolved
				|| q.State == KingdomRaidIncidentState.Cancelled
				|| q.State == KingdomRaidIncidentState.Quarantined;
			if (terminal) return q.ChannelState == KingdomRaidChannelState.Closed;
			return q.ChannelState == KingdomRaidChannelState.Issued
				|| q.ChannelState == KingdomRaidChannelState.Acknowledged
				|| q.ChannelState == KingdomRaidChannelState.RedeliveryQueued
				|| q.ChannelState == KingdomRaidChannelState.Closed;
		}

		private static bool RecoveryShape(KingdomRaidIncident q)
		{
			if (q.Resolution != KingdomRaidResolution.StoresPlundered)
				return q.RecoveryState == KingdomRaidRecoveryState.None
					&& q.RecoveryQuestId == null && q.RecoveryStepId == null
					&& q.RecoveryOpenedTick == 0L && q.RecoveryResolvedTick == 0L
					&& q.RecoveryNotice == null;
			if (q.RecoveryState == KingdomRaidRecoveryState.LegacyUnavailable)
				return q.RecoveryQuestId == null && q.RecoveryStepId == null
					&& q.RecoveryOpenedTick == 0L && q.RecoveryResolvedTick == q.ResolvedTick
					&& !string.IsNullOrEmpty(q.RecoveryNotice);
			if (q.RecoveryState == KingdomRaidRecoveryState.CoveredByExisting)
				return q.RecoveryQuestId == null && q.RecoveryStepId == null
					&& q.RecoveryOpenedTick == 0L && q.RecoveryResolvedTick == q.ResolvedTick
					&& !string.IsNullOrEmpty(q.RecoveryNotice);
			if (!string.Equals(q.RecoveryQuestId, RecoveryQuestId(q.Id), StringComparison.Ordinal)
				|| !string.Equals(q.RecoveryStepId, RecoveryStepId(q.Id), StringComparison.Ordinal)
				|| !ValidId(q.AttackOperationId)) return false;
			switch (q.RecoveryState)
			{
			case KingdomRaidRecoveryState.Offered:
				return q.RecoveryOpenedTick == 0L && q.RecoveryResolvedTick == 0L;
			case KingdomRaidRecoveryState.Active:
			case KingdomRaidRecoveryState.Ready:
				return q.RecoveryOpenedTick >= q.ResolvedTick && q.RecoveryResolvedTick == 0L;
			case KingdomRaidRecoveryState.Resolved:
			case KingdomRaidRecoveryState.Declined:
				return q.RecoveryResolvedTick >= q.ResolvedTick;
			default:
				return false;
			}
		}

		private static bool RecoveryMatches(KingdomRaidIncident incident,
			KingdomLifecycleOperation operation)
		{
			return incident != null && operation != null
				&& incident.State == KingdomRaidIncidentState.Resolved
				&& incident.Resolution == KingdomRaidResolution.StoresPlundered
				&& string.Equals(operation.ObjectId, incident.Id, StringComparison.Ordinal)
				&& string.Equals(operation.SettlementId, incident.SettlementId, StringComparison.Ordinal)
				&& string.Equals(operation.ZoneId, incident.TargetZoneId, StringComparison.Ordinal)
				&& string.Equals(operation.Faction, incident.AttackerFactionId, StringComparison.Ordinal);
		}

		private static bool Matches(KingdomRaidIncident Incident,
			KingdomLifecycleOperation Operation)
		{
			return Incident != null && Operation != null
				&& string.Equals(Operation.ObjectId, Incident.Id, StringComparison.Ordinal)
				&& string.Equals(Operation.SettlementId, Incident.SettlementId, StringComparison.Ordinal)
				&& string.Equals(Operation.ZoneId, Incident.TargetZoneId, StringComparison.Ordinal)
				&& string.Equals(Operation.Faction, Incident.AttackerFactionId, StringComparison.Ordinal);
		}

		private static bool ValidId(string value) { return !string.IsNullOrEmpty(value) && Bounded(value, 256); }
		private static bool ValidName(string value) { return !string.IsNullOrEmpty(value) && Bounded(value, 512); }
		private static bool Bounded(string value, int maximum)
		{
			if (value == null) return true;
			if (value.Length > maximum) return false;
			for (int i = 0; i < value.Length; i++) if (char.IsControl(value[i])) return false;
			return true;
		}
	}
}
