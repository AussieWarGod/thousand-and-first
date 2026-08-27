using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomRaidIncidentRules
	{
		public static bool TryApply(KingdomRaidLedger Before,
			KingdomLifecycleOperation Operation, out KingdomRaidLedger After)
		{
			After = null;
			if (!CanPublish(Before, Operation) || Before.StateRevision == long.MaxValue) return false;
			KingdomRaidLedger next = Copy(Before);
			KingdomRaidIncident active = Active(next);
			KingdomRaidIncident subject = Incident(next, Operation.ObjectId);
			KingdomRaidGrievance grievance = active == null ? null : Grievance(next, active.GrievanceId);
			switch (Operation.Action)
			{
			case KingdomLifecycleAction.RaidWarning:
				KingdomRaidGrievance g = new KingdomRaidGrievance
				{
					Id = Operation.ObjectId, IssuerFactionId = Operation.Faction,
					TargetSettlementId = Operation.SettlementId, TargetZoneId = Operation.ZoneId,
					CauseCode = Operation.Creed, SourceEventId = Operation.Origin,
					SourceTick = Operation.CreatedTick, SourceZoneId = Operation.ArrivalText,
					Severity = Operation.Target, EvidenceText = Operation.Detail,
					Status = KingdomRaidGrievanceStatus.Reserved
				};
				bool queue = active != null;
				KingdomRaidIncident q = new KingdomRaidIncident
				{
					Id = Operation.ObjectMarker, GrievanceId = g.Id,
					CauseSnapshot = g.EvidenceText, SettlementId = Operation.SettlementId,
					TargetZoneId = Operation.ZoneId, AttackerFactionId = Operation.Faction,
					SourceKind = Operation.ObjectName ?? "authored act",
					SourceLocator = Operation.ArrivalText,
					ReachRule = Operation.DisplayFaction,
					State = queue ? KingdomRaidIncidentState.Queued : KingdomRaidIncidentState.Rumored,
					Seed = SeedFor(Operation.ObjectMarker), RumorTick = Operation.CreatedTick,
					DeliveredTick = 0L, DueTick = 0L,
					DemandLeadTicks = Operation.DepartTick - Operation.CreatedTick,
					RemainingLeadTicks = Operation.DepartTick - Operation.CreatedTick,
					DemandChannelId = DemandChannelId(Operation.ObjectMarker),
					ChannelState = queue ? KingdomRaidChannelState.None
						: KingdomRaidChannelState.AwaitingDelivery,
					DisclosedStake = Operation.PlunderRequested,
					MaximumPlunder = Operation.Kind,
					ForceProfileId = Operation.Blueprint, DefenceEstimate = Operation.Defence,
					ObjectiveCode = "stores", PlannedPartySize = Operation.Count,
					LastNotice = Operation.Outbox == null ? null : Operation.Outbox.Message
				};
				next.Grievances.Add(g); next.Incidents.Add(q);
				if (!queue) next.ActiveIncidentId = q.Id;
				for (int i = 0; i < next.Incidents.Count - 1; i++)
				{
					KingdomRaidIncident owed = next.Incidents[i];
					if (owed.TalkObligation && owed.TalkObligationDischargedBy == null
						&& string.Equals(owed.AttackerFactionId, q.AttackerFactionId,
							StringComparison.Ordinal)) owed.TalkObligationDischargedBy = q.Id;
				}
				break;
			case KingdomLifecycleAction.RaidDeliverDemand:
				active.ChannelRevision = Operation.Target;
				active.DemandObjectId = Operation.ObjectMarker;
				active.ChannelState = KingdomRaidChannelState.Issued;
				active.LastNotice = Operation.Outbox == null ? null : Operation.Outbox.Message;
				break;
			case KingdomLifecycleAction.RaidAcknowledgeDemand:
				active.ChannelState = KingdomRaidChannelState.Acknowledged;
				if (active.State == KingdomRaidIncidentState.Rumored)
					active.State = KingdomRaidIncidentState.Warned;
				if (active.State == KingdomRaidIncidentState.Warned)
				{
					active.DeliveredTick = Operation.CreatedTick;
					active.DueTick = Operation.DepartTick;
					active.RemainingLeadTicks = 0L;
				}
				else active.DueTick = 0L;
				active.LastNotice = Operation.Outbox == null ? null : Operation.Outbox.Message;
				break;
			case KingdomLifecycleAction.RaidLoseChannel:
				if (active.State == KingdomRaidIncidentState.Warned)
				{
					long remaining = active.DueTick > Operation.CreatedTick
						? active.DueTick - Operation.CreatedTick : active.DemandLeadTicks;
					active.RemainingLeadTicks = Math.Max(1L,
						Math.Min(active.DemandLeadTicks, remaining));
					active.DueTick = 0L;
				}
				active.ChannelState = KingdomRaidChannelState.RedeliveryQueued;
				active.LastNotice = Operation.Outbox == null ? null : Operation.Outbox.Message;
				break;
			case KingdomLifecycleAction.RaidRewarning:
			case KingdomLifecycleAction.RaidDeadline:
				if (active.State != KingdomRaidIncidentState.Warned) return false;
				active.State = KingdomRaidIncidentState.ConfrontationReady;
				active.DueTick = 0L;
				active.RemainingLeadTicks = 0L;
				active.LastNotice = Operation.Outbox == null ? null : Operation.Outbox.Message;
				break;
			case KingdomLifecycleAction.RaidFight:
				if (!CanAnswer(active)) return false;
				active.Response = KingdomRaidResponse.Fight;
				active.State = KingdomRaidIncidentState.FightCommitted;
				active.LastNotice = Operation.Outbox == null ? null : Operation.Outbox.Message;
				break;
			case KingdomLifecycleAction.RaidFortify:
				if ((!CanAnswer(active) && active.State != KingdomRaidIncidentState.FortifyOrdered)
					|| string.IsNullOrEmpty(Operation.Detail)
					|| Operation.Defence <= 0) return false;
				List<KingdomRaidDefenceReservation> reservations;
				int reservedDefence;
				if (!TryDecodeDefenceReservations(Operation.Detail, out reservations,
					out reservedDefence) || reservedDefence != Operation.Defence) return false;
				active.Response = KingdomRaidResponse.Fortify;
				active.State = KingdomRaidIncidentState.Fortified;
				active.DefenceEstimate = Operation.Defence;
				active.DefenceCommitment = Operation.Detail;
				active.DefenceReservationVersion = CurrentDefenceReservationVersion;
				active.DefenceReservations = reservations;
				active.FortifyOrderedTick = 0L;
				active.LastNotice = Operation.Outbox == null ? null : Operation.Outbox.Message;
				break;
			case KingdomLifecycleAction.RaidFortifyOrder:
				active.Response = KingdomRaidResponse.Fortify;
				active.State = KingdomRaidIncidentState.FortifyOrdered;
				ClearDefenceReservations(active);
				active.FortifyOrderedTick = Operation.CreatedTick;
				active.LastNotice = Operation.Outbox == null ? null : Operation.Outbox.Message;
				break;
			case KingdomLifecycleAction.RaidFortifyFailure:
				active.Response = KingdomRaidResponse.None;
				active.State = KingdomRaidIncidentState.ConfrontationReady;
				active.DueTick = 0L;
				active.RemainingLeadTicks = 0L;
				active.DefenceEstimate = 0;
				ClearDefenceReservations(active);
				active.FortifyOrderedTick = 0L;
				active.LastNotice = Operation.Outbox == null ? null : Operation.Outbox.Message;
				break;
			case KingdomLifecycleAction.RaidAttack:
				if (active.State != KingdomRaidIncidentState.FightCommitted
					&& active.State != KingdomRaidIncidentState.Fortified) return false;
				active.State = KingdomRaidIncidentState.Active;
				active.ObjectiveCode = Operation.ArrivalText;
				active.ObjectiveObjectId = Operation.Origin;
				active.ObjectiveX = Operation.Target; active.ObjectiveY = Operation.Count;
				active.DefenceEstimate = Operation.Defence;
				active.PlannedPartySize = Operation.PartySize;
				active.SpawnedPartySize = Operation.Spawned;
				active.AttackOperationId = Operation.Id;
				break;
			case KingdomLifecycleAction.RaidRecoveryAccept:
				subject.RecoveryState = KingdomRaidRecoveryState.Active;
				subject.RecoveryOpenedTick = Operation.CreatedTick;
				subject.RecoveryNotice = Operation.Outbox == null ? null : Operation.Outbox.Message;
				break;
			case KingdomLifecycleAction.RaidRecoveryReady:
				subject.RecoveryState = KingdomRaidRecoveryState.Ready;
				subject.RecoveryNotice = Operation.Outbox == null ? null : Operation.Outbox.Message;
				break;
			case KingdomLifecycleAction.RaidRecoveryResolve:
				subject.RecoveryState = KingdomRaidRecoveryState.Resolved;
				subject.RecoveryResolvedTick = Operation.CreatedTick;
				subject.RecoveryNotice = Operation.Outbox == null ? null : Operation.Outbox.Message;
				break;
			case KingdomLifecycleAction.RaidRecoveryDecline:
				subject.RecoveryState = KingdomRaidRecoveryState.Declined;
				subject.RecoveryResolvedTick = Operation.CreatedTick;
				subject.RecoveryNotice = Operation.Outbox == null ? null : Operation.Outbox.Message;
				break;
			case KingdomLifecycleAction.RaidTribute:
				if (!CanAnswer(active) || Operation.WaterRequested != active.DisclosedStake
					|| Operation.WaterProved != Operation.WaterRequested) return false;
				if (!Resolve(next, active, grievance, Operation, KingdomRaidResponse.Tribute,
					KingdomRaidResolution.TributePaid, false)) return false;
				break;
			case KingdomLifecycleAction.RaidTalkDown:
				if (!CanAnswer(active)) return false;
				if (!Resolve(next, active, grievance, Operation, KingdomRaidResponse.Talk,
					KingdomRaidResolution.TalkedDownWithObligation, true)) return false;
				break;
			case KingdomLifecycleAction.RaidResolve:
				if (active.State != KingdomRaidIncidentState.Active) return false;
				KingdomRaidResolution resolution = (KingdomRaidResolution)Operation.Kind;
				if (resolution != KingdomRaidResolution.StoresPlundered
					&& resolution != KingdomRaidResolution.RaidersDefeated
					&& resolution != KingdomRaidResolution.ObjectiveLost) return false;
				if (!ResolveResultShape(Operation.Kind, Operation.Target,
					active.MaximumPlunder)) return false;
				active.PlunderProved = Operation.Target;
				if (!Resolve(next, active, grievance, Operation, active.Response,
					resolution, false)) return false;
				break;
			case KingdomLifecycleAction.RaidCancel:
				if (active == null)
				{
					long legacyLast;
					if (!long.TryParse(Operation.Origin, System.Globalization.NumberStyles.None,
						System.Globalization.CultureInfo.InvariantCulture, out legacyLast)
						|| legacyLast < 0L) return false;
					next.LegacyEvidenceArchived = true;
					next.LegacyRaidState = Operation.Target;
					next.LegacyFaction = Operation.Faction;
					next.LegacyDueTick = Operation.DepartTick;
					next.LegacyLastTick = legacyLast;
					next.LegacyTimesDeferred = Operation.Count;
				}
				else
				{
					KingdomRaidResolution cancelled = (KingdomRaidResolution)Operation.Kind;
					if (cancelled != KingdomRaidResolution.SourceInvalid
						&& cancelled != KingdomRaidResolution.OptionDisabled
						&& cancelled != KingdomRaidResolution.Repelled
						&& cancelled != KingdomRaidResolution.NoValidObjective) return false;
					if (!Resolve(next, active, grievance, Operation, active.Response,
						cancelled, false)) return false;
					active.State = KingdomRaidIncidentState.Cancelled;
				}
				break;
			default:
				return false;
			}
			next.StateRevision++;
			if (!ValidLedger(next)) return false;
			After = next;
			return true;
		}
	}
}
