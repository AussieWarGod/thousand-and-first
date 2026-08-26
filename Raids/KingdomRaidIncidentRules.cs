using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst
{
	/// <summary>Engine-free raid causality and incident state. Standing never appears here: it may
	/// gate a response, but cannot mint a grievance or select a target.</summary>
	public static class KingdomRaidIncidentRules
	{
		public const int MaxSeverity = 4;
		public const int MaxStake = 24;
		public const int MaxParty = 8;
		public const int MaxDefenceWorks = 64;
		public const int MaxDefenceCrew = 64;
		public const int CurrentDefenceReservationVersion = 1;

		/// <summary>Builds the canonical compact payload carried by the lifecycle operation until
		/// the raid ledger publishes the typed rows. Work and crew order is semantic, not survey
		/// order; duplicate work or resident identities refuse instead of double-counting.</summary>
		public static bool TryEncodeDefenceReservations(
			IList<KingdomRaidDefenceReservation> Reservations, out string Commitment, out int Total)
		{
			Commitment = null;
			Total = 0;
			if (Reservations == null || Reservations.Count == 0
				|| Reservations.Count > MaxDefenceWorks) return false;
			List<KingdomRaidDefenceReservation> rows = CopyDefenceReservations(Reservations);
			rows.Sort(delegate(KingdomRaidDefenceReservation a,
				KingdomRaidDefenceReservation b) { return a.WorkId.CompareTo(b.WorkId); });
			HashSet<int> works = new HashSet<int>();
			HashSet<int> crews = new HashSet<int>();
			long sum = 0L;
			StringBuilder text = new StringBuilder("R1");
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomRaidDefenceReservation row = rows[i];
				if (row == null || row.WorkId <= 0 || !works.Add(row.WorkId)
					|| row.FrozenScore <= 0
					|| row.FrozenScore > KingdomLifecycleRules.MaxPhysicalCount
					|| row.CrewSemanticIds == null
					|| row.CrewSemanticIds.Count > MaxDefenceCrew) return false;
				row.CrewSemanticIds.Sort();
				text.Append(';').Append(row.WorkId.ToString(CultureInfo.InvariantCulture))
					.Append('=').Append(row.FrozenScore.ToString(CultureInfo.InvariantCulture))
					.Append('[');
				for (int j = 0; j < row.CrewSemanticIds.Count; j++)
				{
					int residentId = row.CrewSemanticIds[j];
					if (residentId <= 0 || !crews.Add(residentId)) return false;
					if (j != 0) text.Append(',');
					text.Append(residentId.ToString(CultureInfo.InvariantCulture));
				}
				text.Append(']');
				sum += row.FrozenScore;
				if (sum > KingdomLifecycleRules.MaxPhysicalCount
					|| crews.Count > MaxDefenceCrew
					|| text.Length > KingdomLifecycleRules.MaxTextChars) return false;
			}
			Commitment = text.ToString();
			Total = (int)sum;
			return true;
		}

		/// <summary>Decodes only the canonical payload. A merely parseable alternate spelling is
		/// rejected so reload cannot change row order, plan hash, or exclusivity.</summary>
		public static bool TryDecodeDefenceReservations(string Commitment,
			out List<KingdomRaidDefenceReservation> Reservations, out int Total)
		{
			Reservations = new List<KingdomRaidDefenceReservation>();
			Total = 0;
			if (string.IsNullOrEmpty(Commitment)
				|| Commitment.Length > KingdomLifecycleRules.MaxTextChars
				|| !Commitment.StartsWith("R1;", StringComparison.Ordinal)) return false;
			string[] encoded = Commitment.Substring(3).Split(';');
			if (encoded.Length == 0 || encoded.Length > MaxDefenceWorks) return false;
			for (int i = 0; i < encoded.Length; i++)
			{
				string value = encoded[i];
				int equals = value.IndexOf('=');
				int open = value.IndexOf('[', equals + 1);
				if (equals <= 0 || open <= equals + 1 || value.Length <= open + 1
					|| value[value.Length - 1] != ']') return false;
				int workId;
				int score;
				if (!TryPositive(value.Substring(0, equals), out workId)
					|| !TryPositive(value.Substring(equals + 1, open - equals - 1), out score)
					|| score > KingdomLifecycleRules.MaxPhysicalCount) return false;
				KingdomRaidDefenceReservation row = new KingdomRaidDefenceReservation
				{
					WorkId = workId,
					FrozenScore = score
				};
				string crew = value.Substring(open + 1, value.Length - open - 2);
				if (crew.Length != 0)
				{
					string[] ids = crew.Split(',');
					if (ids.Length > MaxDefenceCrew) return false;
					for (int j = 0; j < ids.Length; j++)
					{
						int residentId;
						if (!TryPositive(ids[j], out residentId)) return false;
						row.CrewSemanticIds.Add(residentId);
					}
				}
				Reservations.Add(row);
			}
			string canonical;
			if (!TryEncodeDefenceReservations(Reservations, out canonical, out Total)
				|| !string.Equals(canonical, Commitment, StringComparison.Ordinal))
			{
				Reservations.Clear();
				Total = 0;
				return false;
			}
			return true;
		}

		private static bool TryPositive(string Text, out int Value)
		{
			Value = 0;
			return !string.IsNullOrEmpty(Text) && Text[0] != '0'
				&& int.TryParse(Text, NumberStyles.None, CultureInfo.InvariantCulture, out Value)
				&& Value > 0;
		}

		private static List<KingdomRaidDefenceReservation> CopyDefenceReservations(
			IList<KingdomRaidDefenceReservation> Source)
		{
			List<KingdomRaidDefenceReservation> rows =
				new List<KingdomRaidDefenceReservation>(Source == null ? 0 : Source.Count);
			for (int i = 0; Source != null && i < Source.Count; i++)
			{
				KingdomRaidDefenceReservation source = Source[i];
				if (source == null) { rows.Add(null); continue; }
				rows.Add(new KingdomRaidDefenceReservation
				{
					WorkId = source.WorkId,
					FrozenScore = source.FrozenScore,
					CrewSemanticIds = source.CrewSemanticIds == null ? null
						: new List<int>(source.CrewSemanticIds)
				});
			}
			return rows;
		}

		public static string GrievanceId(string SourceEventId)
		{
			return ValidId(SourceEventId)
				? KingdomLifecycleRules.ChildId(SourceEventId, "grievance", 0) : null;
		}

		public static string IncidentId(string GrievanceId)
		{
			return ValidId(GrievanceId)
				? KingdomLifecycleRules.ChildId(GrievanceId, "incident", 0) : null;
		}

		public static string DemandChannelId(string IncidentId)
		{
			return ValidId(IncidentId)
				? KingdomLifecycleRules.ChildId(IncidentId, "demand-channel", 0) : null;
		}

		public static string DemandObjectId(string ChannelId, int Revision)
		{
			return ValidId(ChannelId) && Revision > 0
				? KingdomLifecycleRules.ChildId(ChannelId, "witness", Revision) : null;
		}

		public static string RecoveryQuestId(string IncidentId)
		{
			return ValidId(IncidentId) ? "TAF:Recovery:" + IncidentId : null;
		}

		public static string RecoveryStepId(string IncidentId)
		{
			return ValidId(IncidentId)
				? KingdomLifecycleRules.ChildId(IncidentId, "recovery-step", 0) : null;
		}

		public static long SeedFor(string IncidentId)
		{
			if (!ValidId(IncidentId)) return 0L;
			unchecked
			{
				ulong hash = 1469598103934665603UL;
				for (int i = 0; i < IncidentId.Length; i++)
				{
					hash ^= IncidentId[i];
					hash *= 1099511628211UL;
				}
				return (long)(hash & 0x7fffffffffffffffUL);
			}
		}

		public static KingdomRaidIncident Active(KingdomRaidLedger Ledger)
		{
			if (!CurrentLedger(Ledger) || string.IsNullOrEmpty(Ledger.ActiveIncidentId)
				|| Ledger.Incidents == null) return null;
			for (int i = 0; i < Ledger.Incidents.Count; i++)
				if (Ledger.Incidents[i] != null && string.Equals(Ledger.Incidents[i].Id,
					Ledger.ActiveIncidentId, StringComparison.Ordinal)) return Ledger.Incidents[i];
			return null;
		}

		public static KingdomRaidGrievance Grievance(KingdomRaidLedger Ledger, string Id)
		{
			if (!CurrentLedger(Ledger) || Ledger.Grievances == null || string.IsNullOrEmpty(Id)) return null;
			for (int i = 0; i < Ledger.Grievances.Count; i++)
				if (Ledger.Grievances[i] != null && string.Equals(Ledger.Grievances[i].Id,
					Id, StringComparison.Ordinal)) return Ledger.Grievances[i];
			return null;
		}

		public static KingdomRaidIncident Incident(KingdomRaidLedger Ledger, string Id)
		{
			if (!CurrentLedger(Ledger) || Ledger.Incidents == null || string.IsNullOrEmpty(Id)) return null;
			for (int i = 0; i < Ledger.Incidents.Count; i++)
				if (Ledger.Incidents[i] != null && string.Equals(Ledger.Incidents[i].Id,
					Id, StringComparison.Ordinal)) return Ledger.Incidents[i];
			return null;
		}

		public static bool SourceConsumed(KingdomRaidLedger Ledger, string SourceEventId)
		{
			if (!CurrentLedger(Ledger) || Ledger.Grievances == null || string.IsNullOrEmpty(SourceEventId))
				return false;
			for (int i = 0; i < Ledger.Grievances.Count; i++)
				if (Ledger.Grievances[i] != null && string.Equals(
					Ledger.Grievances[i].SourceEventId, SourceEventId,
					StringComparison.Ordinal)) return true;
			return false;
		}

		public static bool HasTalkObligation(KingdomRaidLedger ledger, string faction)
		{
			if (!CurrentLedger(ledger) || ledger.Incidents == null || string.IsNullOrEmpty(faction)) return false;
			for (int i = 0; i < ledger.Incidents.Count; i++)
			{
				KingdomRaidIncident q = ledger.Incidents[i];
				if (q != null && q.TalkObligation && q.TalkObligationDischargedBy == null
					&& string.Equals(q.AttackerFactionId, faction, StringComparison.Ordinal)) return true;
			}
			return false;
		}

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
