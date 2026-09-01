using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityDispatchRules
	{
		public const int MaximumDirectRecords = 12;
		private const int MaximumStoredRecords = MaximumDirectRecords + MaximumEndpoints + 2;

		public static bool TryRecordCapacityFallback(KingdomPolityDispatchState State,
			long ExpectedRevision, KingdomPolityDueWork Work,
			out KingdomPolityDirectRecord Record, out string Failure)
		{
			Record = null; Failure = null;
			if (!ValidState(State, out Failure) || !ExactWorkShape(State, Work))
				return Fail(Failure ?? "direct polity record does not match its frozen intent",
					out Failure);
			KingdomPolityDirectRecord expected = BuildDirectRecord(Work);
			KingdomPolityDirectRecord existing = FindRecord(State.DirectRecords, expected.RecordId);
			if (existing != null)
			{
				if (!SameStoredRecord(existing, expected, false)
					|| (State.CompletedMask & 1 << Work.EndpointOrdinal) == 0)
					return Fail("direct polity record identity carries different facts", out Failure);
				Record = existing.Copy(); return true;
			}
			if (!ExactOpenWork(State, Work)) return Fail(
				"direct polity record does not match its frozen intent", out Failure);
			if (State.Revision == long.MaxValue)
				return Fail("polity dispatch revision is exhausted", out Failure);
			KingdomPolityDispatchState candidate = CloneState(State);
			KingdomPolityDirectRecord intent = FindIntent(candidate, Work.EndpointOrdinal);
			if (intent != null) intent.AmbientTransaction =
				KingdomPolityAmbientTransactionRules.Copy(Work.AmbientTransaction);
			if (intent == null || !AddDetailed(candidate, intent, out Record, out Failure)) return false;
			candidate.DirectRecords.Remove(intent);
			candidate.CompletedMask |= 1 << Work.EndpointOrdinal; candidate.Revision++;
			SortRecords(candidate.DirectRecords);
			if (!TryCommitState(State, candidate, ExpectedRevision, out Failure))
			{
				Record = null; return false;
			}
			Record = FindRecord(State.DirectRecords, Record.RecordId)?.Copy(); return Record != null;
		}

		public static bool TryRecordCapacityFallback(KingdomPolityDispatchState State,
			KingdomPolityDueWork Work, out KingdomPolityDirectRecord Record, out string Failure)
		{
			return TryRecordCapacityFallback(State, State?.Revision ?? -1L, Work,
				out Record, out Failure);
		}

		public static bool TryAcknowledgeDirectRecord(KingdomPolityDispatchState State,
			long ExpectedRevision, string RecordId, string SettlementId, long Tick,
			out string Failure)
		{
			Failure = null;
			if (!ValidState(State, out Failure) || !KingdomPolityRules.TypedId(SettlementId,
				"taf:settlement:v1:") || Tick < 0L)
				return Fail(Failure ?? "direct polity acknowledgement is invalid", out Failure);
			if (RetirementRecord(State) != null)
				return Fail("retired polity direct authority is immutable", out Failure);
			KingdomPolityDirectRecord row = FindRecord(State.DirectRecords, RecordId);
			if (row == null || !(IsKind(row, DirectPrefix) || IsKind(row, AggregatePrefix))
				|| row.SettlementId != null && row.SettlementId != SettlementId)
				return Fail("direct polity record is absent or belongs to another endpoint", out Failure);
			if (Tick < row.CauseTick)
				return Fail("direct polity acknowledgement predates its exact cause", out Failure);
			long acknowledged = Math.Max(1L, Tick);
			if (row.AcknowledgedTick != 0L)
				return row.AcknowledgedTick == acknowledged
					|| Fail("direct polity acknowledgement differs from its terminal receipt", out Failure);
			if (State.Revision == long.MaxValue)
				return Fail("polity dispatch revision is exhausted", out Failure);
			KingdomPolityDispatchState candidate = CloneState(State);
			FindRecord(candidate.DirectRecords, RecordId).AcknowledgedTick = acknowledged;
			candidate.Revision++;
			return TryCommitState(State, candidate, ExpectedRevision, out Failure);
		}

		public static bool TryAcknowledgeDirectRecord(KingdomPolityDispatchState State,
			string RecordId, string SettlementId, long Tick, out string Failure)
		{
			return TryAcknowledgeDirectRecord(State, State?.Revision ?? -1L, RecordId,
				SettlementId, Tick, out Failure);
		}

		/// <summary>Read-only, on-demand terminal facts. No queue or acknowledgement is emitted.</summary>
		public static List<KingdomPolityDirectRecord> ReadableDirectRecords(
			KingdomPolityDispatchState State, string SettlementId, bool IncludeAcknowledged)
		{
			List<KingdomPolityDirectRecord> rows = new List<KingdomPolityDirectRecord>();
			if (!ValidState(State, out string _) || !KingdomPolityRules.TypedId(SettlementId,
				"taf:settlement:v1:")) return rows;
			for (int i = 0; i < State.DirectRecords.Count; i++)
			{
				KingdomPolityDirectRecord row = State.DirectRecords[i];
				if (!(IsKind(row, DirectPrefix) || IsKind(row, AggregatePrefix))
					|| row.SettlementId != null && row.SettlementId != SettlementId
					|| !IncludeAcknowledged && row.AcknowledgedTick != 0L) continue;
				rows.Add(row.Copy());
			}
			rows.Sort((a, b) => a.CauseTick != b.CauseTick
				? a.CauseTick.CompareTo(b.CauseTick) : string.CompareOrdinal(a.RecordId, b.RecordId));
			return rows;
		}

		public static List<KingdomPolityDirectRecord> UnreadDirectRecords(
			KingdomPolityDispatchState State, string SettlementId)
		{
			return ReadableDirectRecords(State, SettlementId, false);
		}

		internal static bool TryReadPresentationSource(KingdomPolityDispatchState State,
			string CohortId, out bool ActiveIntent, out bool TerminalRecord,
			out string SettlementId, out int BodyCount, out long CauseTick, out string Failure)
		{
			ActiveIntent = false; TerminalRecord = false; SettlementId = null;
			BodyCount = 0; CauseTick = 0L; Failure = null;
			if (!ValidState(State, out Failure)
				|| !KingdomPolityRules.TypedId(CohortId, "taf:cohort:")) return false;
			KingdomPolityDirectRecord found = null;
			for (int i = 0; i < State.DirectRecords.Count; i++)
				if (State.DirectRecords[i].SourceRef == CohortId
					&& (IsKind(State.DirectRecords[i], IntentPrefix)
						|| IsKind(State.DirectRecords[i], DirectPrefix)))
				{
					if (found != null) return Fail(
						"polity presentation source is duplicated", out Failure);
					found = State.DirectRecords[i];
				}
			if (found == null) return true;
			ActiveIntent = IsKind(found, IntentPrefix); TerminalRecord = !ActiveIntent;
			SettlementId = found.SettlementId; BodyCount = Members(found.Purpose);
			CauseTick = found.CauseTick; return true;
		}

		internal static bool ValidDirectRecords(KingdomPolityDispatchState State,
			out string Failure)
		{
			Failure = null; IList<KingdomPolityDirectRecord> rows = State?.DirectRecords;
			if (rows == null || rows.Count > MaximumStoredRecords)
				return Fail("direct polity authority is absent or unbounded", out Failure);
			if (rows.Count != 0 && !KingdomPolityRules.TypedId(State.RealmId, "taf:realm:"))
				return Fail("direct polity authority has no exact realm", out Failure);
			int detailed = 0, aggregate = 0, retirement = 0, intents = 0;
			string previous = null; int intentMask = 0;
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomPolityDirectRecord r = rows[i];
				if (r == null || previous != null && string.CompareOrdinal(previous, r.RecordId) >= 0
					|| !KingdomPolityRules.SemanticId(r.SourceRef)
					|| !KingdomPolityRules.Text(r.EndpointVerb, true) || r.CauseTick < 0L)
					return Fail("direct polity record is invalid or noncanonical", out Failure);
				if (IsKind(r, IntentPrefix))
				{
					if (r.AcknowledgedTick > -1L || r.AcknowledgedTick < -MaximumEndpoints)
						return Fail("polity intent ordinal is invalid", out Failure);
					int ordinal = (int)(-r.AcknowledgedTick - 1L); intents++;
					if (!State.HasWindow || ordinal < 0 || ordinal >= State.EndpointCount
						|| (intentMask & 1 << ordinal) != 0 || (State.CompletedMask & 1 << ordinal) != 0
						|| r.WindowOrdinal != State.LastWindowOrdinal
						|| r.CauseTick != State.WindowCauseTick
						|| !ExactEndpointRow(State, r, out DueFactParts intentFacts)
						|| intentFacts.Topology != State.EndpointDigest
						|| r.EndpointVerb.IndexOf("; topology=" + State.EndpointDigest + "; ",
							StringComparison.Ordinal) < 0
						|| r.RecordId != StoredId(IntentPrefix, "polity-intent-v1", r,
							State.EndpointDigest)) return Fail("polity intent is forged", out Failure);
					intentMask |= 1 << ordinal;
				}
				else if (IsKind(r, DirectPrefix))
				{
					detailed++; if (!ExactEndpointRow(State, r, out DueFactParts _)
						|| r.AmbientTransaction != null &&
							!KingdomPolityAmbientTransactionRules.Valid(
								r.AmbientTransaction, r.SourceRef, out _)
						|| r.AcknowledgedTick < 0L
						|| r.AcknowledgedTick != 0L && r.AcknowledgedTick < r.CauseTick
						|| r.RecordId != StoredId(DirectPrefix, "polity-direct-record-v2", r,
							r.AmbientTransaction?.FrozenDigest))
						return Fail("direct polity fact is forged", out Failure);
				}
				else if (IsKind(r, AggregatePrefix))
				{
					aggregate++; if (r.SettlementId != null || r.WindowOrdinal < 1UL
						|| !AmbientPurpose(r.Purpose) || r.AcknowledgedTick < 0L
						|| !ExactAggregate(r)
						|| r.RecordId != StoredId(AggregatePrefix,
							"polity-direct-aggregate-v1", r))
						return Fail("direct polity aggregate is forged", out Failure);
				}
				else if (IsKind(r, RetirementPrefix))
				{
					retirement++; if (r.SettlementId != null || r.AcknowledgedTick != 0L
						|| r.RecordId != StoredId(RetirementPrefix,
							"polity-direct-retirement-v1", r))
						return Fail("direct polity retirement seal is forged", out Failure);
				}
				else return Fail("direct polity authority has an unknown record kind", out Failure);
				previous = r.RecordId;
			}
			int required = State != null && State.HasWindow
				? ((1 << State.EndpointCount) - 1) & ~State.CompletedMask : 0;
			if (retirement == 1 && !ExactRetirementSeal(State, RetirementRecord(State)))
				return Fail("direct polity retirement seal is divergent", out Failure);
			return detailed <= MaximumDirectRecords && aggregate <= 1 && retirement <= 1
				&& intents <= MaximumEndpoints && intentMask == required
				|| Fail("direct polity record bounds or intent coverage diverged", out Failure);
		}

		internal static bool AmbientPurpose(KingdomPolityCohortPurpose P)
		{
			return P == KingdomPolityCohortPurpose.Guard || P == KingdomPolityCohortPurpose.Patrol
				|| P == KingdomPolityCohortPurpose.Courier || P == KingdomPolityCohortPurpose.Trader
				|| P == KingdomPolityCohortPurpose.Migrant;
		}
	}
}
