using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomLifecycleWireCodec
	{
		private static void WriteRaidLedger(BinaryWriter w, KingdomRaidLedger x)
		{
			if (x == null) throw new InvalidDataException("raid ledger is absent");
			if (x.Version > KingdomRaidLedger.CurrentVersion)
			{
				if (x.OpaqueFuturePayload == null
					|| x.OpaqueFuturePayload.Length > KingdomLifecycleRules.MaxRaidLedgerBytes)
					throw new InvalidDataException("future raid ledger envelope is malformed");
				w.Write(x.Version); w.Write(x.OpaqueFuturePayload.Length);
				w.Write(x.OpaqueFuturePayload, 0, x.OpaqueFuturePayload.Length);
				return;
			}
			if (x.Version != KingdomRaidLedger.CurrentVersion || x.OpaqueFuturePayload != null)
				throw new InvalidDataException("raid ledger version is not writable");
			byte[] payload;
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter body = new BinaryWriter(stream, StrictUtf8, true))
			{
				WriteRaidLedgerV3Body(body, x);
				body.Flush();
				if (stream.Length > KingdomLifecycleRules.MaxRaidLedgerBytes)
					throw new InvalidDataException("raid ledger payload exceeds its cap");
				payload = stream.ToArray();
			}
			w.Write(x.Version); w.Write(payload.Length); w.Write(payload, 0, payload.Length);
		}

		private static void WriteRaidLedgerV2(BinaryWriter w, KingdomRaidLedger x)
		{
			if (x == null || x.Version != KingdomRaidLedger.CurrentVersion
				|| x.OpaqueFuturePayload != null)
				throw new InvalidDataException("raid v2 fixture requires current authority");
			byte[] payload;
			using (MemoryStream stream = new MemoryStream())
			using (BinaryWriter body = new BinaryWriter(stream, StrictUtf8, true))
			{
				WriteRaidLedgerV2Body(body, x); body.Flush();
				if (stream.Length > KingdomLifecycleRules.MaxRaidLedgerBytes)
					throw new InvalidDataException("raid ledger payload exceeds its cap");
				payload = stream.ToArray();
			}
			w.Write(2); w.Write(payload.Length); w.Write(payload, 0, payload.Length);
		}

		/// <summary>Raid v3 is v2 byte-for-byte followed by a typed reservation appendix. Keeping
		/// the old body intact makes the previous writer a frozen fixture rather than a conditional
		/// view of today's object graph.</summary>
		private static void WriteRaidLedgerV3Body(BinaryWriter w, KingdomRaidLedger x)
		{
			WriteRaidLedgerV2Body(w, x);
			w.Write(x.Incidents.Count);
			for (int i = 0; i < x.Incidents.Count; i++)
			{
				KingdomRaidIncident q = x.Incidents[i];
				w.Write(q.DefenceReservationVersion);
				EnsureCount(q.DefenceReservations, KingdomRaidIncidentRules.MaxDefenceWorks,
					"raid defence reservations");
				w.Write(q.DefenceReservations.Count);
				for (int j = 0; j < q.DefenceReservations.Count; j++)
				{
					KingdomRaidDefenceReservation row = q.DefenceReservations[j];
					if (row == null) throw new InvalidDataException("null raid defence reservation");
					w.Write(row.WorkId); w.Write(row.FrozenScore);
					EnsureCount(row.CrewSemanticIds, KingdomRaidIncidentRules.MaxDefenceCrew,
						"raid defence crew reservations");
					w.Write(row.CrewSemanticIds.Count);
					for (int k = 0; k < row.CrewSemanticIds.Count; k++)
						w.Write(row.CrewSemanticIds[k]);
				}
			}
		}

		private static void WriteRaidLedgerV2Body(BinaryWriter w, KingdomRaidLedger x)
		{
			EnsureCount(x.Grievances, KingdomLifecycleRules.MaxRaidGrievances,
				"raid grievances");
			EnsureCount(x.Incidents, KingdomLifecycleRules.MaxRaidIncidents,
				"raid incidents");
			w.Write(x.StateRevision); w.Write(x.ScheduleRevision);
			S(w, x.ActiveIncidentId, true); w.Write(x.LegacyEvidenceArchived);
			w.Write(x.LegacyRaidState); S(w, x.LegacyFaction, false);
			w.Write(x.LegacyDueTick); w.Write(x.LegacyLastTick); w.Write(x.LegacyTimesDeferred);
			w.Write(x.Grievances.Count);
			for (int i = 0; i < x.Grievances.Count; i++)
			{
				KingdomRaidGrievance g = x.Grievances[i];
				if (g == null) throw new InvalidDataException("null raid grievance");
				S(w, g.Id, true); S(w, g.IssuerFactionId, false);
				S(w, g.TargetSettlementId, true); S(w, g.TargetZoneId, false);
				S(w, g.CauseCode, false); S(w, g.SourceEventId, true); w.Write(g.SourceTick);
				S(w, g.SourceZoneId, false); w.Write(g.Severity);
				S(w, g.EvidenceText, false, true); w.Write((byte)g.Status);
				S(w, g.ResolutionId, true);
			}
			w.Write(x.Incidents.Count);
			for (int i = 0; i < x.Incidents.Count; i++)
			{
				KingdomRaidIncident q = x.Incidents[i];
				if (q == null) throw new InvalidDataException("null raid incident");
				S(w, q.Id, true); S(w, q.GrievanceId, true); S(w, q.CauseSnapshot, false, true);
				S(w, q.SettlementId, true); S(w, q.TargetZoneId, false);
				S(w, q.AttackerFactionId, false); S(w, q.SourceKind, false);
				S(w, q.SourceLocator, false); S(w, q.ReachRule, false);
				w.Write((byte)q.State); w.Write(q.Seed); w.Write(q.RumorTick);
				w.Write(q.DeliveredTick); w.Write(q.DueTick); w.Write((byte)q.Response);
				w.Write(q.DisclosedStake); w.Write(q.MaximumPlunder);
				S(w, q.ForceProfileId, false);
				w.Write(q.DefenceEstimate); S(w, q.ObjectiveCode, false);
				S(w, q.ObjectiveObjectId, true); w.Write(q.ObjectiveX); w.Write(q.ObjectiveY);
				w.Write(q.PlannedPartySize); w.Write(q.SpawnedPartySize); w.Write(q.PlunderProved);
				S(w, q.DefenceCommitment, false, true); w.Write(q.TalkObligation);
				S(w, q.TalkObligationDischargedBy, true);
				S(w, q.LastNotice, false, true); w.Write((byte)q.Resolution);
				S(w, q.ResolutionOperationId, true); w.Write(q.ResolvedTick);
				w.Write(q.DemandLeadTicks); w.Write(q.RemainingLeadTicks);
				S(w, q.DemandChannelId, true); S(w, q.DemandObjectId, true);
				w.Write((byte)q.ChannelState); w.Write(q.ChannelRevision);
				w.Write(q.FortifyOrderedTick); S(w, q.AttackOperationId, true);
				w.Write((byte)q.RecoveryState); S(w, q.RecoveryQuestId, true);
				S(w, q.RecoveryStepId, true); w.Write(q.RecoveryOpenedTick);
				w.Write(q.RecoveryResolvedTick); S(w, q.RecoveryNotice, false, true);
			}
		}

		private static void WriteRaidLedgerV1Body(BinaryWriter w, KingdomRaidLedger x)
		{
			EnsureCount(x.Grievances, KingdomLifecycleRules.MaxRaidGrievances,
				"raid grievances");
			EnsureCount(x.Incidents, KingdomLifecycleRules.MaxRaidIncidents,
				"raid incidents");
			w.Write(x.StateRevision); w.Write(x.ScheduleRevision);
			S(w, x.ActiveIncidentId, true); w.Write(x.LegacyEvidenceArchived);
			w.Write(x.LegacyRaidState); S(w, x.LegacyFaction, false);
			w.Write(x.LegacyDueTick); w.Write(x.LegacyLastTick); w.Write(x.LegacyTimesDeferred);
			w.Write(x.Grievances.Count);
			for (int i = 0; i < x.Grievances.Count; i++)
			{
				KingdomRaidGrievance g = x.Grievances[i];
				if (g == null) throw new InvalidDataException("null raid grievance");
				S(w, g.Id, true); S(w, g.IssuerFactionId, false);
				S(w, g.TargetSettlementId, true); S(w, g.TargetZoneId, false);
				S(w, g.CauseCode, false); S(w, g.SourceEventId, true); w.Write(g.SourceTick);
				S(w, g.SourceZoneId, false); w.Write(g.Severity);
				S(w, g.EvidenceText, false, true); w.Write((byte)g.Status);
				S(w, g.ResolutionId, true);
			}
			w.Write(x.Incidents.Count);
			for (int i = 0; i < x.Incidents.Count; i++)
			{
				KingdomRaidIncident q = x.Incidents[i];
				if (q == null) throw new InvalidDataException("null raid incident");
				S(w, q.Id, true); S(w, q.GrievanceId, true); S(w, q.CauseSnapshot, false, true);
				S(w, q.SettlementId, true); S(w, q.TargetZoneId, false);
				S(w, q.AttackerFactionId, false); S(w, q.SourceKind, false);
				S(w, q.SourceLocator, false); S(w, q.ReachRule, false);
				w.Write((byte)q.State); w.Write(q.Seed); w.Write(q.RumorTick);
				w.Write(q.DeliveredTick); w.Write(q.DueTick); w.Write((byte)q.Response);
				w.Write(q.DisclosedStake); w.Write(q.MaximumPlunder);
				S(w, q.ForceProfileId, false); w.Write(q.DefenceEstimate);
				S(w, q.ObjectiveCode, false); S(w, q.ObjectiveObjectId, true);
				w.Write(q.ObjectiveX); w.Write(q.ObjectiveY); w.Write(q.PlannedPartySize);
				w.Write(q.SpawnedPartySize); w.Write(q.PlunderProved);
				S(w, q.DefenceCommitment, false, true); w.Write(q.TalkObligation);
				S(w, q.TalkObligationDischargedBy, true); S(w, q.LastNotice, false, true);
				w.Write((byte)q.Resolution); S(w, q.ResolutionOperationId, true);
				w.Write(q.ResolvedTick);
			}
		}

	}
}
