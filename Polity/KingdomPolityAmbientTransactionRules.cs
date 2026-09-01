using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Builds and terminates bounded weekly visit claims. It never moves actors,
	/// stock, residents, or pooled resources.</summary>
	public static partial class KingdomPolityAmbientTransactionRules
	{
		public const int MaximumFacts = 4;
		public const int MaximumManifestRows = 4;

		public static bool TryFreeze(string RealmId, string SourcePolityId,
			KingdomPolityDueWork Work, IList<KingdomPolityEndpointFacts> Endpoints,
			out KingdomPolityAmbientTransaction Transaction, out string Failure)
		{
			Transaction = null; Failure = null;
			if (!KingdomPolityRules.TypedId(RealmId, "taf:realm:") || SourcePolityId != RealmId ||
				Work == null || !KingdomPolityRules.TypedId(Work.CohortId, "taf:cohort:") ||
				!KingdomPolityRules.TypedId(Work.EventStreamId, "taf:stream:") ||
				!KingdomPolityRules.SemanticId(Work.SourceRef) || Work.CauseTick < 0L ||
				Endpoints == null || Endpoints.Count < 1 ||
				Endpoints.Count > KingdomPolityDispatchRules.MaximumEndpoints)
				return Fail("ambient transaction has no proof-bound current-realm source", out Failure);
			KingdomPolityEndpointFacts destination = Find(Endpoints, Work.SettlementId);
			if (destination == null || !SafeText(destination.SettlementName, true) ||
				!SafeText(destination.ZoneId, true) || Cause(destination, Work.Purpose) !=
				Work.CauseRef || !KingdomPolityRules.SemanticId(Work.CauseRef))
				return Fail("ambient transaction destination is unresolved", out Failure);

			KingdomPolityEndpointFacts source = destination;
			string locus = null, detail = null, news = null;
			List<string> facts = new List<string>();
			switch (Work.Purpose)
			{
			case KingdomPolityCohortPurpose.Guard:
				if (string.IsNullOrEmpty(destination.GuardCauseRef) ||
					!destination.GuardCauseRef.StartsWith("taf:fact:witnessed:",
						StringComparison.Ordinal)) return Fail(
					"guard visit lacks a witnessed protected-locus or joined-defense fact", out Failure);
				locus = destination.GuardProtectedLocusRef;
				detail = destination.GuardWitnessDetail; facts.Add(destination.GuardCauseRef); break;
			case KingdomPolityCohortPurpose.Patrol:
				if (string.IsNullOrEmpty(destination.PatrolCauseRef) ||
					!destination.PatrolCauseRef.StartsWith("taf:fact:route-condition:",
						StringComparison.Ordinal)) return Fail(
					"patrol visit lacks a caused route or site-condition report", out Failure);
				locus = destination.PatrolConditionLocusRef;
				detail = destination.PatrolConditionDetail; facts.Add(destination.PatrolCauseRef); break;
			case KingdomPolityCohortPurpose.Courier:
				source = Find(Endpoints, destination.CourierSourceSettlementId);
				if (!ExactSource(source, destination.CourierSourceZoneId) || source == destination ||
					string.IsNullOrEmpty(source.DeedFactRef)) return Fail(
					"courier visit lacks two exact endpoints and a frozen source deed", out Failure);
				detail = source.DeedSummary; news = source.DeedFactRef;
				facts.Add(destination.CourierCauseRef); facts.Add(source.DeedFactRef); break;
			case KingdomPolityCohortPurpose.Trader:
				source = Find(Endpoints, destination.TraderSourceSettlementId);
				if (!ExactSource(source, destination.TraderSourceZoneId) || source == destination ||
					string.IsNullOrEmpty(source.MarketFactRef)) return Fail(
					"trader visit lacks an exact origin and counterpart", out Failure);
				detail = "No exact physical stock accompanies this visit; no trade is offered.";
				news = source.DeedFactRef ?? source.MarketFactRef;
				facts.Add(destination.TraderCauseRef); facts.Add(source.MarketFactRef);
				if (news != source.MarketFactRef) facts.Add(news); break;
			case KingdomPolityCohortPurpose.Migrant:
				source = Find(Endpoints, destination.MigrantSourceSettlementId);
				if (!ExactSource(source, destination.MigrantSourceZoneId) || source == destination ||
					string.IsNullOrEmpty(source.PopulationFactRef) ||
					string.IsNullOrEmpty(destination.CapacityFactRef)) return Fail(
					"petition lacks an exact origin, destination, or capacity cause", out Failure);
				detail = "A petitioner asks to enter this settlement; no resident is admitted by the visit.";
				facts.Add(destination.MigrantCauseRef); facts.Add(source.PopulationFactRef);
				facts.Add(destination.CapacityFactRef); break;
			default: return Fail("ambient transaction purpose is unsupported", out Failure);
			}
			facts.Sort(StringComparer.Ordinal);
			if (!SafeText(source?.SettlementName, true) || !SafeText(source?.ZoneId, true) ||
				!SafeText(locus, false) || !SafeText(detail, true) || !CanonicalFacts(facts))
				return Fail("ambient transaction contains unsafe or noncanonical facts", out Failure);
			KingdomPolityAmbientTransaction row = new KingdomPolityAmbientTransaction
			{
				Purpose = Work.Purpose, SourcePolityId = SourcePolityId,
				SourceSettlementId = source.SettlementId, SourceZoneId = source.ZoneId,
				SourceSettlementName = source.SettlementName,
				DestinationSettlementId = destination.SettlementId,
				DestinationSettlementName = destination.SettlementName,
				DestinationZoneId = destination.ZoneId, LocalLocusRef = locus,
				FactRefs = facts, SafeDetail = detail, NewsRef = news,
				PreparedTick = Work.CauseTick
			};
			row.FrozenDigest = FrozenDigest(row);
			row.TransactionId = KingdomPolityRules.ActivationId(
				"taf:ambient-transaction:v1:", "polity-ambient-transaction-v1",
				Work.CohortId, row.FrozenDigest);
			if (!Valid(row, Work.CohortId, out Failure)) return false;
			Transaction = row; return true;
		}

		public static bool TryPrepareAdmissionHandoff(string RealmId,
			KingdomPolityCohortPlan Cohort, string MemberId, string SourceObjectId,
			string SourceZoneId, string ProposedResidentName, long Tick,
			out KingdomPolityAdmissionHandoff Handoff, out string Failure)
		{
			Handoff = null; Failure = null;
			KingdomPolityAmbientTransaction t = Cohort?.AmbientTransaction;
			if (Cohort == null || t == null || t.Purpose != KingdomPolityCohortPurpose.Migrant ||
				!Valid(t, Cohort.CohortId, out Failure) || t.TerminalChoice !=
				KingdomPolityAmbientTerminalChoice.None ||
				!KingdomPolityRules.TypedId(MemberId, "taf:cohort-member:") ||
				!KingdomPolityRules.SemanticId(SourceObjectId) || !SafeText(SourceZoneId, true) ||
				!SafeText(ProposedResidentName, true) || Tick < t.PreparedTick)
				return Fail(Failure ?? "admission handoff source is invalid", out Failure);
			KingdomPolityAdmissionHandoff h = new KingdomPolityAdmissionHandoff
			{
				RealmId = RealmId, PolityId = Cohort.PolityId, CohortId = Cohort.CohortId,
				MemberId = MemberId, TargetSettlementId = t.DestinationSettlementId,
				SourceObjectId = SourceObjectId, SourceZoneId = SourceZoneId,
				ProposedResidentName = ProposedResidentName,
				Decision = KingdomPolityAdmissionDecision.Pending,
				PreparedTick = Tick, CauseDigest = t.FrozenDigest
			};
			h.HandoffId = KingdomPolityRules.ActivationId("taf:admission-handoff:v1:",
				"polity-admission-handoff-v1", h.RealmId, h.PolityId, h.CohortId,
				h.MemberId, h.TargetSettlementId, h.SourceObjectId, h.SourceZoneId,
				h.ProposedResidentName, h.PreparedTick.ToString(CultureInfo.InvariantCulture),
				h.CauseDigest);
			if (!ValidHandoff(h, false)) return Fail("admission handoff is noncanonical", out Failure);
			Handoff = h; return true;
		}

		private static KingdomPolityEndpointFacts Find(IList<KingdomPolityEndpointFacts> Values,
			string SettlementId)
		{
			for (int i = 0; i < Values.Count; i++) if (Values[i]?.SettlementId == SettlementId)
				return Values[i];
			return null;
		}

		private static bool ExactSource(KingdomPolityEndpointFacts E, string ZoneId)
		{
			return E != null && E.ZoneId == ZoneId && SafeText(E.ZoneId, true);
		}

		private static string Cause(KingdomPolityEndpointFacts E,
			KingdomPolityCohortPurpose Purpose)
		{
			switch (Purpose)
			{
			case KingdomPolityCohortPurpose.Guard: return E.GuardCauseRef;
			case KingdomPolityCohortPurpose.Patrol: return E.PatrolCauseRef;
			case KingdomPolityCohortPurpose.Courier: return E.CourierCauseRef;
			case KingdomPolityCohortPurpose.Trader: return E.TraderCauseRef;
			case KingdomPolityCohortPurpose.Migrant: return E.MigrantCauseRef;
			default: return null;
			}
		}

		private static bool Fail(string Message, out string Failure)
		{
			Failure = Message; return false;
		}
	}
}
