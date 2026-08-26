using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleWireCodec
	{
		private static KingdomRaidLedger ReadRaidLedger(BinaryReader r)
		{
			int version = r.ReadInt32();
			if (version == 1) return UpgradeRaidLedgerV1(ReadRaidLedgerV1Body(r));
			if (version < 2) throw new InvalidDataException("unsupported raid ledger version");
			int length = ReadCount(r, KingdomLifecycleRules.MaxRaidLedgerBytes);
			byte[] payload = r.ReadBytes(length);
			if (payload.Length != length) throw new EndOfStreamException("raid ledger is truncated");
			if (version > KingdomRaidLedger.CurrentVersion)
				return new KingdomRaidLedger { Version = version, OpaqueFuturePayload = payload };
			using (MemoryStream stream = new MemoryStream(payload, false))
			using (BinaryReader body = new BinaryReader(stream, StrictUtf8, true))
			{
				KingdomRaidLedger value = version == 2
					? UpgradeRaidLedgerV2(ReadRaidLedgerV2Body(body))
					: ReadRaidLedgerV3Body(body);
				if (stream.Position != stream.Length)
					throw new InvalidDataException("raid ledger has trailing bytes");
				return value;
			}
		}

		private static KingdomRaidLedger ReadRaidLedgerV3Body(BinaryReader r)
		{
			KingdomRaidLedger x = ReadRaidLedgerV2Body(r);
			int incidents = ReadCount(r, KingdomLifecycleRules.MaxRaidIncidents);
			if (incidents != x.Incidents.Count)
				throw new InvalidDataException("raid defence appendix does not match incidents");
			for (int i = 0; i < incidents; i++)
			{
				KingdomRaidIncident q = x.Incidents[i];
				q.DefenceReservationVersion = r.ReadInt32();
				int rows = ReadCount(r, KingdomRaidIncidentRules.MaxDefenceWorks);
				q.DefenceReservations = new List<KingdomRaidDefenceReservation>(rows);
				for (int j = 0; j < rows; j++)
				{
					KingdomRaidDefenceReservation row = new KingdomRaidDefenceReservation
					{
						WorkId = r.ReadInt32(),
						FrozenScore = r.ReadInt32()
					};
					int crew = ReadCount(r, KingdomRaidIncidentRules.MaxDefenceCrew);
					row.CrewSemanticIds = new List<int>(crew);
					for (int k = 0; k < crew; k++) row.CrewSemanticIds.Add(r.ReadInt32());
					q.DefenceReservations.Add(row);
				}
			}
			x.Version = KingdomRaidLedger.CurrentVersion;
			return x;
		}

		private static KingdomRaidLedger ReadRaidLedgerV2Body(BinaryReader r)
		{
			KingdomRaidLedger x = new KingdomRaidLedger
			{
				Version = 2, StateRevision = r.ReadInt64(),
				ScheduleRevision = r.ReadInt64(), ActiveIncidentId = S(r, true),
				LegacyEvidenceArchived = ReadExactBoolean(r), LegacyRaidState = r.ReadInt32(),
				LegacyFaction = S(r, false), LegacyDueTick = r.ReadInt64(),
				LegacyLastTick = r.ReadInt64(), LegacyTimesDeferred = r.ReadInt32()
			};
			int grievances = ReadCount(r, KingdomLifecycleRules.MaxRaidGrievances);
			x.Grievances = new List<KingdomRaidGrievance>(grievances);
			for (int i = 0; i < grievances; i++)
				x.Grievances.Add(new KingdomRaidGrievance
				{
					Id = S(r, true), IssuerFactionId = S(r, false),
					TargetSettlementId = S(r, true), TargetZoneId = S(r, false),
					CauseCode = S(r, false), SourceEventId = S(r, true),
					SourceTick = r.ReadInt64(), SourceZoneId = S(r, false),
					Severity = r.ReadInt32(), EvidenceText = S(r, false, true),
					Status = (KingdomRaidGrievanceStatus)r.ReadByte(), ResolutionId = S(r, true)
				});
			int incidents = ReadCount(r, KingdomLifecycleRules.MaxRaidIncidents);
			x.Incidents = new List<KingdomRaidIncident>(incidents);
			for (int i = 0; i < incidents; i++)
			{
				KingdomRaidIncident q = new KingdomRaidIncident
				{
					Id = S(r, true), GrievanceId = S(r, true), CauseSnapshot = S(r, false, true),
					SettlementId = S(r, true), TargetZoneId = S(r, false),
					AttackerFactionId = S(r, false), SourceKind = S(r, false),
					SourceLocator = S(r, false), ReachRule = S(r, false),
					State = (KingdomRaidIncidentState)r.ReadByte(), Seed = r.ReadInt64(),
					RumorTick = r.ReadInt64(), DeliveredTick = r.ReadInt64(), DueTick = r.ReadInt64(),
					Response = (KingdomRaidResponse)r.ReadByte(), DisclosedStake = r.ReadInt32(),
					MaximumPlunder = r.ReadInt32(),
					ForceProfileId = S(r, false), DefenceEstimate = r.ReadInt32(),
					ObjectiveCode = S(r, false), ObjectiveObjectId = S(r, true),
					ObjectiveX = r.ReadInt32(), ObjectiveY = r.ReadInt32(),
					PlannedPartySize = r.ReadInt32(), SpawnedPartySize = r.ReadInt32(),
					PlunderProved = r.ReadInt32(), DefenceCommitment = S(r, false, true),
					TalkObligation = ReadExactBoolean(r),
					TalkObligationDischargedBy = S(r, true),
					LastNotice = S(r, false, true),
					Resolution = (KingdomRaidResolution)r.ReadByte(),
					ResolutionOperationId = S(r, true), ResolvedTick = r.ReadInt64()
				};
				q.DemandLeadTicks = r.ReadInt64(); q.RemainingLeadTicks = r.ReadInt64();
				q.DemandChannelId = S(r, true); q.DemandObjectId = S(r, true);
				q.ChannelState = (KingdomRaidChannelState)r.ReadByte();
				q.ChannelRevision = r.ReadInt32(); q.FortifyOrderedTick = r.ReadInt64();
				q.AttackOperationId = S(r, true);
				q.RecoveryState = (KingdomRaidRecoveryState)r.ReadByte();
				q.RecoveryQuestId = S(r, true); q.RecoveryStepId = S(r, true);
				q.RecoveryOpenedTick = r.ReadInt64(); q.RecoveryResolvedTick = r.ReadInt64();
				q.RecoveryNotice = S(r, false, true);
				x.Incidents.Add(q);
			}
			return x;
		}

		private static KingdomRaidLedger ReadRaidLedgerV1Body(BinaryReader r)
		{
			KingdomRaidLedger x = new KingdomRaidLedger
			{
				Version = 1, StateRevision = r.ReadInt64(), ScheduleRevision = r.ReadInt64(),
				ActiveIncidentId = S(r, true), LegacyEvidenceArchived = ReadExactBoolean(r),
				LegacyRaidState = r.ReadInt32(), LegacyFaction = S(r, false),
				LegacyDueTick = r.ReadInt64(), LegacyLastTick = r.ReadInt64(),
				LegacyTimesDeferred = r.ReadInt32()
			};
			int grievances = ReadCount(r, KingdomLifecycleRules.MaxRaidGrievances);
			for (int i = 0; i < grievances; i++)
				x.Grievances.Add(new KingdomRaidGrievance
				{
					Id = S(r, true), IssuerFactionId = S(r, false), TargetSettlementId = S(r, true),
					TargetZoneId = S(r, false), CauseCode = S(r, false), SourceEventId = S(r, true),
					SourceTick = r.ReadInt64(), SourceZoneId = S(r, false), Severity = r.ReadInt32(),
					EvidenceText = S(r, false, true), Status = (KingdomRaidGrievanceStatus)r.ReadByte(),
					ResolutionId = S(r, true)
				});
			int incidents = ReadCount(r, KingdomLifecycleRules.MaxRaidIncidents);
			for (int i = 0; i < incidents; i++)
				x.Incidents.Add(ReadRaidIncidentV1(r));
			return x;
		}

		private static KingdomRaidIncident ReadRaidIncidentV1(BinaryReader r)
		{
			return new KingdomRaidIncident
			{
				Id = S(r, true), GrievanceId = S(r, true), CauseSnapshot = S(r, false, true),
				SettlementId = S(r, true), TargetZoneId = S(r, false),
				AttackerFactionId = S(r, false), SourceKind = S(r, false),
				SourceLocator = S(r, false), ReachRule = S(r, false),
				State = (KingdomRaidIncidentState)r.ReadByte(), Seed = r.ReadInt64(),
				RumorTick = r.ReadInt64(), DeliveredTick = r.ReadInt64(), DueTick = r.ReadInt64(),
				Response = (KingdomRaidResponse)r.ReadByte(), DisclosedStake = r.ReadInt32(),
				MaximumPlunder = r.ReadInt32(), ForceProfileId = S(r, false),
				DefenceEstimate = r.ReadInt32(), ObjectiveCode = S(r, false),
				ObjectiveObjectId = S(r, true), ObjectiveX = r.ReadInt32(),
				ObjectiveY = r.ReadInt32(), PlannedPartySize = r.ReadInt32(),
				SpawnedPartySize = r.ReadInt32(), PlunderProved = r.ReadInt32(),
				DefenceCommitment = S(r, false, true), TalkObligation = ReadExactBoolean(r),
				TalkObligationDischargedBy = S(r, true), LastNotice = S(r, false, true),
				Resolution = (KingdomRaidResolution)r.ReadByte(),
				ResolutionOperationId = S(r, true), ResolvedTick = r.ReadInt64()
			};
		}

	}
}
