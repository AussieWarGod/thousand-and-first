using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomRaidIncidentRules
	{
		private static bool Resolve(KingdomRaidLedger ledger, KingdomRaidIncident incident,
			KingdomRaidGrievance grievance, KingdomLifecycleOperation operation,
			KingdomRaidResponse response, KingdomRaidResolution resolution, bool obligation)
		{
			incident.Response = response; incident.State = KingdomRaidIncidentState.Resolved;
			incident.Resolution = resolution; incident.ResolutionOperationId = operation.Id;
			incident.ResolvedTick = operation.CreatedTick; incident.TalkObligation = obligation;
			incident.ChannelState = KingdomRaidChannelState.Closed;
			incident.DueTick = 0L; incident.RemainingLeadTicks = 0L;
			if (resolution == KingdomRaidResolution.StoresPlundered)
			{
				bool existing = false;
				for (int i = 0; i < ledger.Incidents.Count; i++)
				{
					KingdomRaidIncident prior = ledger.Incidents[i];
					if (ReferenceEquals(prior, incident) || prior == null
						|| !string.Equals(prior.SettlementId, incident.SettlementId,
							StringComparison.Ordinal)) continue;
					if (prior.RecoveryState == KingdomRaidRecoveryState.Offered
						|| prior.RecoveryState == KingdomRaidRecoveryState.Active
						|| prior.RecoveryState == KingdomRaidRecoveryState.Ready
						|| prior.RecoveryState == KingdomRaidRecoveryState.Declined)
					{
						existing = true;
						break;
					}
				}
				if (existing)
				{
					incident.RecoveryState = KingdomRaidRecoveryState.CoveredByExisting;
					incident.RecoveryResolvedTick = incident.ResolvedTick;
					incident.RecoveryNotice = "An existing bounded watch wound already owns this settlement's one recovery offer.";
				}
				else
				{
					incident.RecoveryState = KingdomRaidRecoveryState.Offered;
					incident.RecoveryQuestId = RecoveryQuestId(incident.Id);
					incident.RecoveryStepId = RecoveryStepId(incident.Id);
					incident.RecoveryNotice = "The raid disordered the watch; recovery is available at the seat.";
				}
			}
			grievance.Status = KingdomRaidGrievanceStatus.Consumed;
			grievance.ResolutionId = operation.Id; ledger.ActiveIncidentId = null;
			return PromoteQueued(ledger, operation.CreatedTick);
		}

		private static bool PromoteQueued(KingdomRaidLedger ledger, long tick)
		{
			KingdomRaidIncident next = null;
			for (int i = 0; i < ledger.Incidents.Count; i++)
			{
				KingdomRaidIncident q = ledger.Incidents[i];
				if (q == null || q.State != KingdomRaidIncidentState.Queued) continue;
				if (next == null || q.RumorTick < next.RumorTick
					|| q.RumorTick == next.RumorTick
					&& string.CompareOrdinal(q.Id, next.Id) < 0) next = q;
			}
			if (next == null) return true;
			if (next.DemandLeadTicks <= 0L) return false;
			next.State = KingdomRaidIncidentState.Rumored;
			next.ChannelState = KingdomRaidChannelState.AwaitingDelivery;
			next.DeliveredTick = 0L; next.DueTick = 0L;
			next.RemainingLeadTicks = next.DemandLeadTicks;
			ledger.ActiveIncidentId = next.Id;
			return true;
		}

		private static bool CanAnswer(KingdomRaidIncident q)
		{
			return q != null && (q.State == KingdomRaidIncidentState.Warned
				|| q.State == KingdomRaidIncidentState.ConfrontationReady);
		}

		private static bool CancelResolution(int kind)
		{
			KingdomRaidResolution resolution = (KingdomRaidResolution)kind;
			return resolution == KingdomRaidResolution.SourceInvalid
				|| resolution == KingdomRaidResolution.OptionDisabled
				|| resolution == KingdomRaidResolution.Repelled
				|| resolution == KingdomRaidResolution.NoValidObjective;
		}

		private static bool ResolveResultShape(int kind, int plunder, int stake)
		{
			KingdomRaidResolution resolution = (KingdomRaidResolution)kind;
			if (resolution == KingdomRaidResolution.StoresPlundered)
				return plunder > 0 && plunder <= stake;
			return (resolution == KingdomRaidResolution.RaidersDefeated
				|| resolution == KingdomRaidResolution.ObjectiveLost) && plunder == 0;
		}

		private static bool IncidentFieldShape(KingdomRaidIncident q)
		{
			bool terminal = q.State == KingdomRaidIncidentState.Resolved
				|| q.State == KingdomRaidIncidentState.Cancelled
				|| q.State == KingdomRaidIncidentState.Quarantined;
			bool ownsFortifyProof = q.Response == KingdomRaidResponse.Fortify
				&& (q.State == KingdomRaidIncidentState.Fortified
					|| q.State == KingdomRaidIncidentState.Active || terminal);
			if (ownsFortifyProof
				? !DefenceReservationShape(q, q.State == KingdomRaidIncidentState.Fortified)
				: !NeutralDefenceReservations(q)) return false;
			bool hasObjective = q.ObjectiveObjectId != null;
			if (hasObjective != (q.ObjectiveX >= 0 && q.ObjectiveY >= 0)) return false;
			bool beforeAttack = q.State == KingdomRaidIncidentState.Queued
				|| q.State == KingdomRaidIncidentState.Rumored
				|| q.State == KingdomRaidIncidentState.Warned
				|| q.State == KingdomRaidIncidentState.ConfrontationReady
				|| q.State == KingdomRaidIncidentState.FightCommitted
				|| q.State == KingdomRaidIncidentState.FortifyOrdered
				|| q.State == KingdomRaidIncidentState.Fortified;
			if (beforeAttack && (hasObjective || q.SpawnedPartySize != 0
				|| q.PlunderProved != 0 || q.AttackOperationId != null)) return false;
			switch (q.State)
			{
			case KingdomRaidIncidentState.Queued:
			case KingdomRaidIncidentState.Rumored:
			case KingdomRaidIncidentState.Warned:
			case KingdomRaidIncidentState.ConfrontationReady:
				return q.Response == KingdomRaidResponse.None
					&& q.DefenceEstimate == 0
					&& q.FortifyOrderedTick == 0L && !q.TalkObligation;
			case KingdomRaidIncidentState.FightCommitted:
				return q.Response == KingdomRaidResponse.Fight
					&& q.FortifyOrderedTick == 0L
					&& !q.TalkObligation;
			case KingdomRaidIncidentState.FortifyOrdered:
				return q.Response == KingdomRaidResponse.Fortify
					&& q.DefenceEstimate == 0
					&& q.FortifyOrderedTick >= q.DeliveredTick && !q.TalkObligation;
			case KingdomRaidIncidentState.Fortified:
				return q.Response == KingdomRaidResponse.Fortify
					&& q.DefenceEstimate > 0 && q.FortifyOrderedTick == 0L
					&& !q.TalkObligation;
			case KingdomRaidIncidentState.Active:
				return (q.Response == KingdomRaidResponse.Fight
						|| q.Response == KingdomRaidResponse.Fortify)
					&& hasObjective && q.SpawnedPartySize > 0
					&& q.SpawnedPartySize == q.PlannedPartySize
					&& q.PlunderProved == 0 && ValidId(q.AttackOperationId)
					&& q.FortifyOrderedTick == 0L && !q.TalkObligation;
			case KingdomRaidIncidentState.Resolved:
			case KingdomRaidIncidentState.Cancelled:
			case KingdomRaidIncidentState.Quarantined:
				return TerminalFieldShape(q, hasObjective);
			default:
				return false;
			}
		}

		private static bool DefenceReservationShape(KingdomRaidIncident q, bool Required)
		{
			if (q == null || q.DefenceReservations == null) return false;
			if (q.DefenceReservationVersion == 0)
				return !Required && q.DefenceReservations.Count == 0
					&& q.DefenceCommitment == null && q.DefenceEstimate == 0;
			if (q.DefenceReservationVersion != CurrentDefenceReservationVersion) return false;
			string commitment;
			int total;
			return TryEncodeDefenceReservations(q.DefenceReservations, out commitment, out total)
				&& total == q.DefenceEstimate
				&& string.Equals(commitment, q.DefenceCommitment, StringComparison.Ordinal);
		}

		private static bool NeutralDefenceReservations(KingdomRaidIncident q)
		{
			return q != null && q.DefenceReservationVersion == 0
				&& q.DefenceReservations != null && q.DefenceReservations.Count == 0
				&& q.DefenceCommitment == null;
		}

		private static void ClearDefenceReservations(KingdomRaidIncident q)
		{
			q.DefenceCommitment = null;
			q.DefenceReservationVersion = 0;
			q.DefenceReservations = new List<KingdomRaidDefenceReservation>();
		}

		private static bool TerminalFieldShape(KingdomRaidIncident q, bool hasObjective)
		{
			switch (q.Resolution)
			{
			case KingdomRaidResolution.TributePaid:
				return q.State == KingdomRaidIncidentState.Resolved
					&& q.Response == KingdomRaidResponse.Tribute && !hasObjective
					&& q.SpawnedPartySize == 0 && q.PlunderProved == 0
					&& q.AttackOperationId == null && !q.TalkObligation;
			case KingdomRaidResolution.TalkedDownWithObligation:
				return q.State == KingdomRaidIncidentState.Resolved
					&& q.Response == KingdomRaidResponse.Talk && !hasObjective
					&& q.SpawnedPartySize == 0 && q.PlunderProved == 0
					&& q.AttackOperationId == null && q.TalkObligation;
			case KingdomRaidResolution.StoresPlundered:
				return q.State == KingdomRaidIncidentState.Resolved && hasObjective
					&& (q.Response == KingdomRaidResponse.Fight
					|| q.Response == KingdomRaidResponse.Fortify)
					&& q.SpawnedPartySize > 0 && q.PlunderProved > 0
					&& ValidId(q.AttackOperationId) && !q.TalkObligation;
			case KingdomRaidResolution.RaidersDefeated:
			case KingdomRaidResolution.ObjectiveLost:
				return q.State == KingdomRaidIncidentState.Resolved && hasObjective
					&& (q.Response == KingdomRaidResponse.Fight
					|| q.Response == KingdomRaidResponse.Fortify)
					&& q.SpawnedPartySize > 0 && q.PlunderProved == 0
					&& ValidId(q.AttackOperationId) && !q.TalkObligation;
			case KingdomRaidResolution.Repelled:
				return q.State == KingdomRaidIncidentState.Cancelled
					&& q.Response == KingdomRaidResponse.Fortify && !hasObjective
					&& q.SpawnedPartySize == 0 && q.PlunderProved == 0
					&& q.AttackOperationId == null;
			case KingdomRaidResolution.NoValidObjective:
				return q.State == KingdomRaidIncidentState.Cancelled && !hasObjective
					&& q.SpawnedPartySize == 0 && q.PlunderProved == 0
					&& q.AttackOperationId == null
					&& (q.Response == KingdomRaidResponse.None
						|| q.Response == KingdomRaidResponse.Fight
						|| q.Response == KingdomRaidResponse.Fortify)
					&& !q.TalkObligation;
			case KingdomRaidResolution.SourceInvalid:
			case KingdomRaidResolution.OptionDisabled:
				return q.State == KingdomRaidIncidentState.Cancelled
					&& q.PlunderProved == 0 && !q.TalkObligation
					&& (hasObjective
						? q.SpawnedPartySize > 0
							&& ValidId(q.AttackOperationId)
							&& (q.Response == KingdomRaidResponse.Fight
								|| q.Response == KingdomRaidResponse.Fortify)
						: q.SpawnedPartySize == 0 && q.AttackOperationId == null
							&& (q.Response == KingdomRaidResponse.None
								|| q.Response == KingdomRaidResponse.Fight
								|| q.Response == KingdomRaidResponse.Fortify));
			default:
				return false;
			}
		}
	}
}
