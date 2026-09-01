using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Builds exact 1–3 endpoint facts from persisted city carriers and their
	/// canonical owned-zone locators. It does not inspect or create unloaded zones.</summary>
	internal static class KingdomPolityEndpointFactRuntime
	{
		internal static bool TryOffer(KingdomSystem System, long Tick,
			out KingdomPolityDispatchOffer Offer, out string Failure)
		{
			Offer = null; Failure = null;
			if (System == null || !System.Founded || System.City == null || Tick < 0L ||
				!KingdomPolityRules.TypedId(System.RealmId, "taf:realm:"))
			{
				Failure = "realm has no exact polity dispatch topology"; return false;
			}
			List<KingdomPolityEndpointFacts> endpoints = new List<KingdomPolityEndpointFacts>();
			System.City.Normalize();
			if (!TryBuild(System.RealmId, System.City.SettlementId, System.SeatName,
				CanonicalZone(System.ClaimedZones), true,
				System.Population, (int)System.Stage, System.ShopTier,
				System.LastKnownStorageSpace, System.Gate, System.LastDeed, System.LastDeedTick,
				System.City, out KingdomPolityEndpointFacts seat, out Failure)) return false;
			endpoints.Add(seat);
			List<KingdomSettlement> rows = System.NonSeatSettlements();
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomSettlement row = rows[i];
				if (row?.City == null) { Failure = "non-seat polity endpoint is incomplete"; return false; }
				row.City.Normalize();
				if (!TryBuild(System.RealmId, row.City.SettlementId, row.SettlementName,
					CanonicalZone(row.ClaimedZones), false,
					row.Population, (int)row.Stage, row.ShopTier, row.LastKnownStorageSpace,
					row.Gate, row.LastDeed, row.LastDeedTick, row.City,
					out KingdomPolityEndpointFacts endpoint, out Failure)) return false;
				endpoints.Add(endpoint);
			}
			endpoints.Sort((a, b) => string.CompareOrdinal(a.SettlementId, b.SettlementId));
			string topology = KingdomPolityRules.ActivationDigest("polity-owned-topology-v1",
				EndpointIds(endpoints));
			for (int i = 0; i < endpoints.Count; i++)
			{
				KingdomPolityEndpointFacts endpoint = endpoints[i];
				KingdomPolityEndpointFacts deed = Source(endpoints, i, e =>
					!string.IsNullOrEmpty(e.DeedFactRef));
				if (deed != null)
				{
					endpoint.CourierSourceSettlementId = deed.SettlementId;
					endpoint.CourierSourceZoneId = deed.ZoneId;
					endpoint.CourierCauseRef = Fact("taf:fact:courier:v2:", "courier",
						System.RealmId, deed.SettlementId, endpoint.SettlementId,
						deed.DeedFactRef, topology);
				}
				KingdomPolityEndpointFacts market = Source(endpoints, i, e =>
					!string.IsNullOrEmpty(e.MarketFactRef));
				if (market != null)
				{
					endpoint.TraderSourceSettlementId = market.SettlementId;
					endpoint.TraderSourceZoneId = market.ZoneId;
					endpoint.TraderCauseRef = Fact("taf:fact:market-visit:v1:", "market-visit",
						System.RealmId, market.SettlementId, endpoint.SettlementId,
						market.MarketFactRef, topology);
				}
				KingdomPolityEndpointFacts people = endpoint.KnownStorageSpace > 0 ?
					Source(endpoints, i, e => !string.IsNullOrEmpty(e.PopulationFactRef)) : null;
				if (people != null)
				{
					endpoint.MigrantSourceSettlementId = people.SettlementId;
					endpoint.MigrantSourceZoneId = people.ZoneId;
					endpoint.MigrantCauseRef = Fact("taf:fact:petition-intent:v1:", "petition",
						System.RealmId, people.SettlementId, endpoint.SettlementId,
						people.PopulationFactRef, endpoint.CapacityFactRef, topology);
				}
			}
			Offer = new KingdomPolityDispatchOffer
			{
				RealmId = System.RealmId, Tick = Tick, Endpoints = endpoints
			};
			return true;
		}

		private static bool TryBuild(string RealmId, string SettlementId,
			string SettlementName, string ZoneId, bool Seat, int Population, int Stage, int ShopTier, int Storage,
			KingdomRules.GatePolicy Gate, string LastDeed, long LastDeedTick,
			Simulation.City.KingdomCityBook Book, out KingdomPolityEndpointFacts Result,
			out string Failure)
		{
			Result = null; Failure = null;
			string population = Math.Max(0, Population).ToString(CultureInfo.InvariantCulture);
			KingdomPolityEndpointFacts result = new KingdomPolityEndpointFacts
			{
				SettlementId = SettlementId, SettlementName = SettlementName, ZoneId = ZoneId,
				IsSeat = Seat, Population = Math.Max(0, Population),
				Stage = Stage, ShopTier = Math.Max(0, ShopTier),
				KnownStorageSpace = Math.Max(0, Storage)
			};
			if (Population > 0) result.PopulationFactRef = Fact("taf:fact:population:v1:",
				"population", RealmId, SettlementId, population);
			if (ShopTier > 0) result.MarketFactRef = Fact("taf:fact:market:v1:",
				"market", RealmId, SettlementId, ShopTier.ToString(CultureInfo.InvariantCulture));
			if (Storage > 0) result.CapacityFactRef = Fact("taf:fact:room:v1:",
				"room", RealmId, SettlementId, Storage.ToString(CultureInfo.InvariantCulture));
			if (LastDeedTick > 0L && KingdomPolityAmbientTransactionRules.SafeText(LastDeed, true))
			{
				result.DeedFactRef = Fact("taf:fact:deed:v1:", "deed", RealmId,
					SettlementId, LastDeedTick.ToString(CultureInfo.InvariantCulture), LastDeed);
				result.DeedSummary = LastDeed;
			}
			if (Book == null || !KingdomPolityEndpointObservationRules.TryGuard(RealmId,
				SettlementId, Book.ZoneIds, Book.ZoneLastReadTicks, Book.ZoneDefences,
				out KingdomPolityEndpointObservation guard, out Failure) ||
				!KingdomPolityEndpointObservationRules.TryCondition(RealmId, SettlementId,
					Book.ZoneIds, Book.ZoneLastReadTicks, Book.WorkIds, Book.WorkZoneIds,
					Book.WorkDesignKeys, Book.WorkConditions, Book.WorkRanThroughTicks,
					out KingdomPolityEndpointObservation condition, out Failure)) return false;
			if (guard != null)
			{
				result.GuardCauseRef = guard.CauseRef;
				result.GuardProtectedLocusRef = guard.LocusRef;
				result.GuardWitnessDetail = guard.Detail;
			}
			if (condition != null)
			{
				result.PatrolCauseRef = condition.CauseRef;
				result.PatrolConditionLocusRef = condition.LocusRef;
				result.PatrolConditionDetail = condition.Detail;
			}
			Result = result; return true;
		}

		private static KingdomPolityEndpointFacts Source(
			IList<KingdomPolityEndpointFacts> Values, int Destination,
			Func<KingdomPolityEndpointFacts, bool> Eligible)
		{
			for (int offset = 1; offset < Values.Count; offset++)
			{
				KingdomPolityEndpointFacts row = Values[(Destination + offset) % Values.Count];
				if (Eligible(row)) return row;
			}
			return null;
		}

		private static string CanonicalZone(IList<string> Values)
		{
			string result = null;
			for (int i = 0; i < (Values?.Count ?? 0); i++)
				if (!string.IsNullOrEmpty(Values[i]) && (result == null ||
					string.CompareOrdinal(Values[i], result) < 0)) result = Values[i];
			return result;
		}

		private static List<string> EndpointIds(IList<KingdomPolityEndpointFacts> Values)
		{
			List<string> result = new List<string>();
			for (int i = 0; i < Values.Count; i++) result.Add(Values[i].SettlementId);
			return result;
		}

		private static string Fact(string Prefix, string Kind, params string[] Values)
		{
			string[] all = new string[Values.Length + 1]; all[0] = Kind;
			for (int i = 0; i < Values.Length; i++) all[i + 1] = Values[i] ?? "";
			return KingdomPolityRules.ActivationId(Prefix, "polity-endpoint-fact-v1", all);
		}
	}
}
