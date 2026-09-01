using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	public static partial class KingdomPolityDispatchRules
	{
		internal const string IntentPrefix = "taf:polity-intent:v1:";
		internal const string DirectPrefix = "taf:polity-direct-record:v2:";
		internal const string AggregatePrefix = "taf:polity-direct-aggregate:v1:";
		internal const string RetirementPrefix = "taf:polity-direct-retirement:v1:";

		private static bool TryChoose(string RealmId, KingdomPolityEndpointFacts Endpoint,
			int EndpointCount, ulong Window, int Index, long CauseTick, string Topology,
			out KingdomPolityDueWork Work)
		{
			Work = null; int start = (int)((Window + (ulong)Index) % PurposeCount);
			for (int offset = 0; offset < PurposeCount; offset++)
			{
				KingdomPolityCohortPurpose purpose = PurposeAt((start + offset) % PurposeCount);
				string cause = Cause(Endpoint, purpose);
				if (!Eligible(Endpoint, EndpointCount, purpose, cause)) continue;
				Work = Build(RealmId, Endpoint, EndpointCount, Index, Window, CauseTick,
					purpose, cause, Topology); return true;
			}
			return false;
		}

		private static KingdomPolityDueWork Build(string RealmId,
			KingdomPolityEndpointFacts Endpoint, int EndpointCount, int EndpointOrdinal,
			ulong Window, long CauseTick, KingdomPolityCohortPurpose Purpose, string Cause,
			string Topology)
		{
			string ordinal = Window.ToString(CultureInfo.InvariantCulture);
			string token = ((byte)Purpose).ToString(CultureInfo.InvariantCulture);
			string facts = DueSourceDigest(Endpoint, EndpointCount, Purpose, Cause);
			string source = Id("taf:event:polity-due:v1:", "event", RealmId,
				Endpoint.SettlementId, ordinal, token, Cause, facts);
			return new KingdomPolityDueWork
			{
				EndpointOrdinal = EndpointOrdinal, EndpointDigest = Topology, CauseRef = Cause,
					DueFacts = DueFacts(Endpoint, EndpointCount, Topology, facts, Cause, source),
				FairnessTicket = KingdomExperienceFairnessRules.Ticket(
					KingdomExperienceLane.PolityCohort, Endpoint.SettlementId, source,
					CauseTick, Window),
				CohortId = Id("taf:cohort:polity-due:v1:", "cohort", RealmId,
					Endpoint.SettlementId, ordinal, token, Cause, facts),
				EventStreamId = Id("taf:stream:polity-due:v1:", "stream", RealmId,
					Endpoint.SettlementId, ordinal), SourceRef = source,
				SettlementId = Endpoint.SettlementId, Purpose = Purpose,
				WindowOrdinal = Window, CauseTick = CauseTick, StayUntilTick = SafeStay(Window),
				MemberCount = Members(Purpose), EndpointVerb = EndpointVerb(Purpose)
			};
		}

		private static KingdomPolityDirectRecord BuildIntent(KingdomPolityDueWork Work)
		{
			KingdomPolityDirectRecord row = new KingdomPolityDirectRecord
			{
				SourceRef = Work.CohortId, SettlementId = Work.SettlementId,
				Purpose = Work.Purpose, WindowOrdinal = Work.WindowOrdinal,
				CauseTick = Work.CauseTick, EndpointVerb = Work.DueFacts,
				AcknowledgedTick = -(Work.EndpointOrdinal + 1L)
			};
			row.RecordId = StoredId(IntentPrefix, "polity-intent-v1", row,
				Work.EndpointDigest); return row;
		}

		internal static bool ExactOpenWork(KingdomPolityDispatchState State,
			KingdomPolityDueWork Work)
		{
			if (!ExactWorkShape(State, Work)) return false;
			KingdomPolityDirectRecord intent = FindIntent(State, Work.EndpointOrdinal);
			return intent != null && SameStoredRecord(intent, BuildIntent(Work), true);
		}

		internal static bool ExactWorkShape(KingdomPolityDispatchState State,
			KingdomPolityDueWork Work)
		{
			KingdomPolityDirectRecord proof = Work == null ? null : new KingdomPolityDirectRecord
			{
				SourceRef = Work.CohortId, SettlementId = Work.SettlementId,
				Purpose = Work.Purpose, WindowOrdinal = Work.WindowOrdinal,
				CauseTick = Work.CauseTick, EndpointVerb = Work.DueFacts
			};
			if (!ExactEndpointRow(State, proof, out DueFactParts due)) return false;
			string ordinal = Work?.WindowOrdinal.ToString(CultureInfo.InvariantCulture);
			return Work != null && State != null && State.HasWindow
				&& State.LastWindowOrdinal == Work.WindowOrdinal
				&& State.WindowCauseTick == Work.CauseTick
				&& State.EndpointDigest == Work.EndpointDigest
				&& Work.EndpointOrdinal >= 0 && Work.EndpointOrdinal < State.EndpointCount
				&& Work.EndpointVerb == EndpointVerb(Work.Purpose)
				&& due.Topology == Work.EndpointDigest && due.Cause == Work.CauseRef
				&& due.Event == Work.SourceRef
				&& Work.EventStreamId == Id("taf:stream:polity-due:v1:", "stream",
					State.RealmId, Work.SettlementId, ordinal)
				&& Work.StayUntilTick == SafeStay(Work.WindowOrdinal)
				&& Work.MemberCount == Members(Work.Purpose)
				&& Work.FairnessTicket == KingdomExperienceFairnessRules.Ticket(
					KingdomExperienceLane.PolityCohort, Work.SettlementId, Work.SourceRef,
					Work.CauseTick, Work.WindowOrdinal);
		}

		internal static KingdomPolityDirectRecord FindIntent(KingdomPolityDispatchState State,
			int EndpointOrdinal)
		{
			long marker = -(EndpointOrdinal + 1L);
			for (int i = 0; i < (State?.DirectRecords?.Count ?? 0); i++)
				if (IsKind(State.DirectRecords[i], IntentPrefix)
					&& State.DirectRecords[i].AcknowledgedTick == marker)
					return State.DirectRecords[i];
			return null;
		}

		private static bool ValidOffer(KingdomPolityDispatchOffer Offer, out string Failure)
		{
			Failure = null;
			if (Offer == null || !KingdomPolityRules.TypedId(Offer.RealmId, "taf:realm:")
				|| Offer.Tick < 0L || Offer.Endpoints == null || Offer.Endpoints.Count < 1
				|| Offer.Endpoints.Count > MaximumEndpoints)
				return Fail("polity dispatch offer is invalid or unbounded", out Failure);
			string previous = null; int seats = 0;
			for (int i = 0; i < Offer.Endpoints.Count; i++)
			{
				KingdomPolityEndpointFacts endpoint = Offer.Endpoints[i];
				if (!ValidEndpoint(endpoint) || previous != null
					&& string.CompareOrdinal(previous, endpoint.SettlementId) >= 0)
					return Fail("polity dispatch endpoints are not exact canonical settlements",
						out Failure);
				if (endpoint.IsSeat) seats++; previous = endpoint.SettlementId;
			}
			return seats == 1 || Fail("polity dispatch has no unique seat", out Failure);
		}

		private static bool ValidEndpoint(KingdomPolityEndpointFacts E)
		{
			return E != null && KingdomPolityRules.TypedId(E.SettlementId, "taf:settlement:v1:")
				&& KingdomPolityAmbientTransactionRules.SafeText(E.SettlementName, false)
				&& KingdomPolityAmbientTransactionRules.SafeText(E.ZoneId, false)
				&& E.Population >= 0 && E.Population <= 10000 && E.Stage >= 0 && E.Stage <= 4
				&& E.ShopTier >= 0 && E.ShopTier <= 8 && E.KnownStorageSpace >= 0
				&& Optional(E.GuardCauseRef) && Optional(E.PatrolCauseRef)
				&& Optional(E.CourierCauseRef) && Optional(E.TraderCauseRef)
				&& Optional(E.MigrantCauseRef) && Optional(E.PopulationFactRef)
				&& Optional(E.DeedFactRef) && KingdomPolityAmbientTransactionRules.SafeText(
					E.DeedSummary, false) && Optional(E.MarketFactRef)
				&& Optional(E.CapacityFactRef) && OptionalSettlement(E.CourierSourceSettlementId)
				&& KingdomPolityAmbientTransactionRules.SafeText(E.CourierSourceZoneId, false)
				&& OptionalSettlement(E.TraderSourceSettlementId)
				&& KingdomPolityAmbientTransactionRules.SafeText(E.TraderSourceZoneId, false)
				&& OptionalSettlement(E.MigrantSourceSettlementId)
				&& KingdomPolityAmbientTransactionRules.SafeText(E.MigrantSourceZoneId, false)
				&& Optional(E.GuardProtectedLocusRef)
				&& KingdomPolityAmbientTransactionRules.SafeText(E.GuardWitnessDetail, false)
				&& Optional(E.PatrolConditionLocusRef)
				&& KingdomPolityAmbientTransactionRules.SafeText(E.PatrolConditionDetail, false);
		}

		private static bool Eligible(KingdomPolityEndpointFacts E, int Count,
			KingdomPolityCohortPurpose P, string Cause)
		{
			if (!KingdomPolityRules.SemanticId(Cause)) return false;
			switch (P) { case KingdomPolityCohortPurpose.Guard: return E.Population > 0;
			case KingdomPolityCohortPurpose.Patrol: return Count > 1 && E.Population > 1;
			case KingdomPolityCohortPurpose.Courier: return Count > 1;
			case KingdomPolityCohortPurpose.Trader: return E.ShopTier > 0;
			case KingdomPolityCohortPurpose.Migrant: return E.KnownStorageSpace > 0;
			default: return false; }
		}

		private static string Cause(KingdomPolityEndpointFacts E, KingdomPolityCohortPurpose P)
		{
			switch (P) { case KingdomPolityCohortPurpose.Guard: return E.GuardCauseRef;
			case KingdomPolityCohortPurpose.Patrol: return E.PatrolCauseRef;
			case KingdomPolityCohortPurpose.Courier: return E.CourierCauseRef;
			case KingdomPolityCohortPurpose.Trader: return E.TraderCauseRef;
			default: return E.MigrantCauseRef; }
		}

		private static KingdomPolityCohortPurpose PurposeAt(int I)
		{
			switch (I) { case 0: return KingdomPolityCohortPurpose.Guard;
			case 1: return KingdomPolityCohortPurpose.Patrol;
			case 2: return KingdomPolityCohortPurpose.Courier;
			case 3: return KingdomPolityCohortPurpose.Trader;
			default: return KingdomPolityCohortPurpose.Migrant; }
		}

		private static int Members(KingdomPolityCohortPurpose P)
		{
			return P == KingdomPolityCohortPurpose.Guard
				|| P == KingdomPolityCohortPurpose.Courier ? 1 : 2;
		}

		private static string EndpointDigest(IList<KingdomPolityEndpointFacts> Values)
		{
			List<string> rows = new List<string>();
			for (int i = 0; i < Values.Count; i++) rows.Add(EndpointFactRow(Values[i]));
			return KingdomPolityRules.ActivationDigest("polity-dispatch-endpoints-v2", rows);
		}

		private static string EndpointFactRow(KingdomPolityEndpointFacts E)
		{
			return string.Join("|", E.SettlementId, E.IsSeat ? "1" : "0",
				E.Population.ToString(CultureInfo.InvariantCulture),
				E.Stage.ToString(CultureInfo.InvariantCulture),
				E.ShopTier.ToString(CultureInfo.InvariantCulture),
				E.KnownStorageSpace.ToString(CultureInfo.InvariantCulture), E.GuardCauseRef ?? "",
				E.PatrolCauseRef ?? "", E.CourierCauseRef ?? "", E.TraderCauseRef ?? "",
				E.MigrantCauseRef ?? "", E.SettlementName ?? "", E.ZoneId ?? "",
				E.PopulationFactRef ?? "", E.DeedFactRef ?? "", E.DeedSummary ?? "",
				E.MarketFactRef ?? "", E.CapacityFactRef ?? "",
				E.CourierSourceSettlementId ?? "", E.CourierSourceZoneId ?? "",
				E.TraderSourceSettlementId ?? "", E.TraderSourceZoneId ?? "",
				E.MigrantSourceSettlementId ?? "", E.MigrantSourceZoneId ?? "",
				E.GuardProtectedLocusRef ?? "", E.GuardWitnessDetail ?? "",
				E.PatrolConditionLocusRef ?? "", E.PatrolConditionDetail ?? "");
		}

		internal static string StoredId(string Prefix, string Domain,
			KingdomPolityDirectRecord R, string Extra = null)
		{
			return KingdomPolityRules.ActivationId(Prefix, Domain, R.SourceRef ?? "",
				R.SettlementId ?? "", ((byte)R.Purpose).ToString(CultureInfo.InvariantCulture),
				R.WindowOrdinal.ToString(CultureInfo.InvariantCulture),
				R.CauseTick.ToString(CultureInfo.InvariantCulture), R.EndpointVerb ?? "",
				Extra ?? "");
		}

		private static string Id(string Prefix, string Kind, params string[] Values)
		{
			string[] all = new string[Values.Length + 1]; all[0] = Kind;
			for (int i = 0; i < Values.Length; i++) all[i + 1] = Values[i];
			return KingdomPolityRules.ActivationId(Prefix, "polity-due-work-v2", all);
		}

		private static bool Optional(string Value)
		{
			return string.IsNullOrEmpty(Value) || KingdomPolityRules.SemanticId(Value);
		}

		private static bool OptionalSettlement(string Value)
		{
			return string.IsNullOrEmpty(Value) || KingdomPolityRules.TypedId(
				Value, "taf:settlement:v1:");
		}
	}
}
