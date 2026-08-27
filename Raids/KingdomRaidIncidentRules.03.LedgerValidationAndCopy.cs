using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomRaidIncidentRules
	{
		public static bool ValidLedger(KingdomRaidLedger Ledger)
		{
			if (Ledger == null) return false;
			if (Ledger.Version > KingdomRaidLedger.CurrentVersion)
				return Ledger.OpaqueFuturePayload != null
					&& Ledger.OpaqueFuturePayload.Length <= KingdomLifecycleRules.MaxRaidLedgerBytes
					&& Ledger.StateRevision == 0L && Ledger.ScheduleRevision == 0L
					&& Ledger.ActiveIncidentId == null && Ledger.Grievances != null
					&& Ledger.Grievances.Count == 0 && Ledger.Incidents != null
					&& Ledger.Incidents.Count == 0 && !Ledger.LegacyEvidenceArchived
					&& Ledger.LegacyRaidState == 0 && Ledger.LegacyFaction == null
					&& Ledger.LegacyDueTick == 0L && Ledger.LegacyLastTick == 0L
					&& Ledger.LegacyTimesDeferred == 0;
			if (Ledger.Version != KingdomRaidLedger.CurrentVersion
				|| Ledger.OpaqueFuturePayload != null
				|| Ledger.StateRevision < 0L || Ledger.ScheduleRevision < 0L
				|| Ledger.Grievances == null || Ledger.Incidents == null
				|| Ledger.Grievances.Count != Ledger.Incidents.Count
				|| Ledger.Grievances.Count > KingdomLifecycleRules.MaxRaidGrievances
				|| Ledger.Incidents.Count > KingdomLifecycleRules.MaxRaidIncidents
				|| !Bounded(Ledger.ActiveIncidentId, 256)
				|| !Bounded(Ledger.LegacyFaction, 512)
				|| Ledger.LegacyDueTick < 0L || Ledger.LegacyLastTick < 0L
				|| Ledger.LegacyTimesDeferred < 0) return false;
			if (!Ledger.LegacyEvidenceArchived && (Ledger.LegacyRaidState != 0
				|| Ledger.LegacyFaction != null || Ledger.LegacyDueTick != 0L
				|| Ledger.LegacyLastTick != 0L || Ledger.LegacyTimesDeferred != 0)) return false;
			HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
			HashSet<string> sources = new HashSet<string>(StringComparer.Ordinal);
			for (int i = 0; i < Ledger.Grievances.Count; i++)
			{
				KingdomRaidGrievance g = Ledger.Grievances[i];
				if (g == null || !ValidId(g.Id) || !ids.Add(g.Id) || !ValidName(g.IssuerFactionId)
					|| !ValidId(g.TargetSettlementId) || !ValidName(g.TargetZoneId)
					|| !ValidName(g.CauseCode) || !ValidId(g.SourceEventId)
					|| !sources.Add(g.SourceEventId) || g.SourceTick < 0L
					|| !Bounded(g.SourceZoneId, 512) || g.Severity < 1 || g.Severity > MaxSeverity
					|| string.IsNullOrEmpty(g.EvidenceText) || !Bounded(g.EvidenceText, 4096)
					|| !Enum.IsDefined(typeof(KingdomRaidGrievanceStatus), g.Status)
					|| g.Status == KingdomRaidGrievanceStatus.None
					|| !Bounded(g.ResolutionId, 256)) return false;
			}
			int live = 0;
			string liveId = null;
			int queued = 0;
			for (int i = 0; i < Ledger.Incidents.Count; i++)
			{
				KingdomRaidIncident q = Ledger.Incidents[i];
				KingdomRaidGrievance g = q == null ? null : Grievance(Ledger, q.GrievanceId);
				if (q == null || !ValidId(q.Id) || !ids.Add(q.Id) || g == null
					|| !string.Equals(q.Id, IncidentId(g.Id), StringComparison.Ordinal)
					|| !string.Equals(q.SettlementId, g.TargetSettlementId, StringComparison.Ordinal)
					|| !string.Equals(q.TargetZoneId, g.TargetZoneId, StringComparison.Ordinal)
					|| !string.Equals(q.AttackerFactionId, g.IssuerFactionId, StringComparison.Ordinal)
					|| !string.Equals(q.CauseSnapshot, g.EvidenceText, StringComparison.Ordinal)
					|| !string.Equals(q.SourceLocator, g.SourceZoneId, StringComparison.Ordinal)
					|| g.SourceTick != q.RumorTick
					|| string.IsNullOrEmpty(q.CauseSnapshot) || !Bounded(q.CauseSnapshot, 4096)
					|| !ValidName(q.SourceKind) || !Bounded(q.SourceLocator, 512)
					|| !ValidName(q.ReachRule) || !Enum.IsDefined(typeof(KingdomRaidIncidentState), q.State)
					|| q.State == KingdomRaidIncidentState.None || q.Seed != SeedFor(q.Id)
					|| q.RumorTick < 0L || !DeadlineShape(q)
					|| q.DemandLeadTicks <= 0L || q.RemainingLeadTicks < 0L
					|| q.RemainingLeadTicks > q.DemandLeadTicks
					|| !string.Equals(q.DemandChannelId, DemandChannelId(q.Id), StringComparison.Ordinal)
					|| !Bounded(q.DemandObjectId, 256)
					|| !Enum.IsDefined(typeof(KingdomRaidChannelState), q.ChannelState)
					|| q.ChannelRevision < 0 || !ChannelShape(q)
					|| !Enum.IsDefined(typeof(KingdomRaidResponse), q.Response)
					|| q.DisclosedStake <= 0 || q.DisclosedStake > MaxStake
					|| q.MaximumPlunder < q.DisclosedStake || q.MaximumPlunder > MaxStake
					|| !ValidName(q.ForceProfileId) || q.DefenceEstimate < 0
					|| q.DefenceEstimate > KingdomLifecycleRules.MaxPhysicalCount
					|| !string.Equals(q.ObjectiveCode, "stores", StringComparison.Ordinal)
					|| !Bounded(q.ObjectiveObjectId, 256)
					|| q.ObjectiveX < -1 || q.ObjectiveY < -1
					|| q.ObjectiveX > KingdomLifecycleRules.MaxPhysicalCount
					|| q.ObjectiveY > KingdomLifecycleRules.MaxPhysicalCount
					|| q.PlannedPartySize <= 0 || q.PlannedPartySize > MaxParty
					|| q.SpawnedPartySize < 0 || q.SpawnedPartySize > q.PlannedPartySize
					|| q.PlunderProved < 0 || q.PlunderProved > q.MaximumPlunder
					|| !Bounded(q.DefenceCommitment, 4096)
					|| q.FortifyOrderedTick < 0L
					|| !Bounded(q.TalkObligationDischargedBy, 256)
					|| !Bounded(q.LastNotice, 4096)
					|| !Enum.IsDefined(typeof(KingdomRaidResolution), q.Resolution)
					|| !Bounded(q.AttackOperationId, 256)
					|| !Bounded(q.ResolutionOperationId, 256) || q.ResolvedTick < 0L
					|| !Enum.IsDefined(typeof(KingdomRaidRecoveryState), q.RecoveryState)
					|| !Bounded(q.RecoveryQuestId, 256) || !Bounded(q.RecoveryStepId, 256)
					|| q.RecoveryOpenedTick < 0L || q.RecoveryResolvedTick < 0L
					|| !Bounded(q.RecoveryNotice, 4096) || !RecoveryShape(q)
					|| !IncidentFieldShape(q)) return false;
				bool terminal = q.State == KingdomRaidIncidentState.Resolved
					|| q.State == KingdomRaidIncidentState.Cancelled
					|| q.State == KingdomRaidIncidentState.Quarantined;
				if (terminal)
				{
					if (q.Resolution == KingdomRaidResolution.None || !ValidId(q.ResolutionOperationId)
						|| q.ResolvedTick < q.RumorTick
						|| !string.Equals(g.ResolutionId, q.ResolutionOperationId,
							StringComparison.Ordinal)
						|| (q.State == KingdomRaidIncidentState.Quarantined
							? g.Status != KingdomRaidGrievanceStatus.Quarantined
							: g.Status != KingdomRaidGrievanceStatus.Consumed)) return false;
					if (q.TalkObligationDischargedBy != null
						&& (!q.TalkObligation || !ValidId(q.TalkObligationDischargedBy))) return false;
				}
				else
				{
					if (q.Resolution != KingdomRaidResolution.None || q.ResolutionOperationId != null
						|| q.ResolvedTick != 0L || g.Status != KingdomRaidGrievanceStatus.Reserved) return false;
					if (q.State == KingdomRaidIncidentState.Queued)
					{
						queued++;
						if (q.Response != KingdomRaidResponse.None
							|| q.SpawnedPartySize != 0 || q.PlunderProved != 0
							|| q.DefenceCommitment != null || q.TalkObligation) return false;
					}
					else { live++; liveId = q.Id; }
				}
			}
			for (int i = 0; i < Ledger.Incidents.Count; i++)
			{
				KingdomRaidIncident q = Ledger.Incidents[i];
				if (q.TalkObligationDischargedBy == null) continue;
				KingdomRaidIncident discharge = Incident(Ledger, q.TalkObligationDischargedBy);
				if (!q.TalkObligation || discharge == null || ReferenceEquals(q, discharge)
					|| !string.Equals(q.AttackerFactionId, discharge.AttackerFactionId,
						StringComparison.Ordinal)
					|| discharge.RumorTick < q.ResolvedTick) return false;
			}
			if (live == 0) return queued == 0 && Ledger.ActiveIncidentId == null;
			KingdomRaidIncident active = Active(Ledger);
			return live == 1 && active != null
				&& string.Equals(active.Id, liveId, StringComparison.Ordinal)
				&& active.State != KingdomRaidIncidentState.Queued;
		}

		public static KingdomRaidLedger Copy(KingdomRaidLedger Source)
		{
			if (Source == null) return null;
			KingdomRaidLedger x = new KingdomRaidLedger
			{
				Version = Source.Version, StateRevision = Source.StateRevision,
				ScheduleRevision = Source.ScheduleRevision,
				ActiveIncidentId = Source.ActiveIncidentId,
				LegacyEvidenceArchived = Source.LegacyEvidenceArchived,
				LegacyRaidState = Source.LegacyRaidState, LegacyFaction = Source.LegacyFaction,
				LegacyDueTick = Source.LegacyDueTick, LegacyLastTick = Source.LegacyLastTick,
				LegacyTimesDeferred = Source.LegacyTimesDeferred,
				OpaqueFuturePayload = Source.OpaqueFuturePayload == null ? null
					: (byte[])Source.OpaqueFuturePayload.Clone()
			};
			if (Source.Grievances != null)
				for (int i = 0; i < Source.Grievances.Count; i++) x.Grievances.Add(Copy(Source.Grievances[i]));
			if (Source.Incidents != null)
				for (int i = 0; i < Source.Incidents.Count; i++) x.Incidents.Add(Copy(Source.Incidents[i]));
			return x;
		}

		private static KingdomRaidGrievance Copy(KingdomRaidGrievance g)
		{
			return new KingdomRaidGrievance
			{
				Id = g.Id, IssuerFactionId = g.IssuerFactionId, TargetSettlementId = g.TargetSettlementId,
				TargetZoneId = g.TargetZoneId, CauseCode = g.CauseCode, SourceEventId = g.SourceEventId,
				SourceTick = g.SourceTick, SourceZoneId = g.SourceZoneId, Severity = g.Severity,
				EvidenceText = g.EvidenceText, Status = g.Status, ResolutionId = g.ResolutionId
			};
		}

		private static KingdomRaidIncident Copy(KingdomRaidIncident q)
		{
			return new KingdomRaidIncident
			{
				Id = q.Id, GrievanceId = q.GrievanceId, CauseSnapshot = q.CauseSnapshot,
				SettlementId = q.SettlementId, TargetZoneId = q.TargetZoneId,
				AttackerFactionId = q.AttackerFactionId, SourceKind = q.SourceKind,
				SourceLocator = q.SourceLocator, ReachRule = q.ReachRule, State = q.State, Seed = q.Seed,
				RumorTick = q.RumorTick, DeliveredTick = q.DeliveredTick, DueTick = q.DueTick,
				DemandLeadTicks = q.DemandLeadTicks, RemainingLeadTicks = q.RemainingLeadTicks,
				DemandChannelId = q.DemandChannelId, DemandObjectId = q.DemandObjectId,
				ChannelState = q.ChannelState, ChannelRevision = q.ChannelRevision,
				Response = q.Response, DisclosedStake = q.DisclosedStake,
				MaximumPlunder = q.MaximumPlunder,
				ForceProfileId = q.ForceProfileId, DefenceEstimate = q.DefenceEstimate,
				ObjectiveCode = q.ObjectiveCode, ObjectiveObjectId = q.ObjectiveObjectId,
				ObjectiveX = q.ObjectiveX, ObjectiveY = q.ObjectiveY,
				PlannedPartySize = q.PlannedPartySize, SpawnedPartySize = q.SpawnedPartySize,
				PlunderProved = q.PlunderProved, DefenceCommitment = q.DefenceCommitment,
				DefenceReservationVersion = q.DefenceReservationVersion,
				DefenceReservations = CopyDefenceReservations(q.DefenceReservations),
				FortifyOrderedTick = q.FortifyOrderedTick,
				TalkObligation = q.TalkObligation,
				TalkObligationDischargedBy = q.TalkObligationDischargedBy,
				LastNotice = q.LastNotice,
				Resolution = q.Resolution, AttackOperationId = q.AttackOperationId,
				ResolutionOperationId = q.ResolutionOperationId, ResolvedTick = q.ResolvedTick,
				RecoveryState = q.RecoveryState, RecoveryQuestId = q.RecoveryQuestId,
				RecoveryStepId = q.RecoveryStepId, RecoveryOpenedTick = q.RecoveryOpenedTick,
				RecoveryResolvedTick = q.RecoveryResolvedTick, RecoveryNotice = q.RecoveryNotice
			};
		}
	}
}
