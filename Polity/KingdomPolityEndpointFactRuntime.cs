using System;
using System.Collections.Generic;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Builds exact 1–3 endpoint facts from persisted city carriers, never zones.</summary>
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
			endpoints.Add(Build(System.RealmId, System.City.SettlementId, true,
				System.Population, (int)System.Stage, System.ShopTier,
				System.LastKnownStorageSpace, System.Gate, System.LastDeed, System.LastDeedTick));
			List<KingdomSettlement> rows = System.NonSeatSettlements();
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomSettlement row = rows[i];
				if (row?.City == null) { Failure = "non-seat polity endpoint is incomplete"; return false; }
				endpoints.Add(Build(System.RealmId, row.City.SettlementId, false,
					row.Population, (int)row.Stage, row.ShopTier, row.LastKnownStorageSpace,
					row.Gate, row.LastDeed, row.LastDeedTick));
			}
			endpoints.Sort((a, b) => string.CompareOrdinal(a.SettlementId, b.SettlementId));
			string topology = KingdomPolityRules.ActivationDigest("polity-owned-topology-v1",
				EndpointIds(endpoints));
			for (int i = 0; i < endpoints.Count; i++)
			{
				KingdomPolityEndpointFacts endpoint = endpoints[i];
				if (endpoints.Count > 1) endpoint.PatrolCauseRef = Fact(
					"taf:fact:patrol:v1:", "patrol", System.RealmId,
					endpoint.SettlementId, topology);
				if (endpoints.Count > 1 && endpoint.CourierCauseRef != null)
					endpoint.CourierCauseRef = Fact("taf:fact:courier:v1:", "courier",
						System.RealmId, endpoint.SettlementId, endpoint.CourierCauseRef, topology);
			}
			Offer = new KingdomPolityDispatchOffer
			{
				RealmId = System.RealmId, Tick = Tick, Endpoints = endpoints
			};
			return true;
		}

		private static KingdomPolityEndpointFacts Build(string RealmId, string SettlementId,
			bool Seat, int Population, int Stage, int ShopTier, int Storage,
			KingdomRules.GatePolicy Gate, string LastDeed, long LastDeedTick)
		{
			string population = Math.Max(0, Population).ToString(CultureInfo.InvariantCulture);
			KingdomPolityEndpointFacts result = new KingdomPolityEndpointFacts
			{
				SettlementId = SettlementId, IsSeat = Seat, Population = Math.Max(0, Population),
				Stage = Stage, ShopTier = Math.Max(0, ShopTier),
				KnownStorageSpace = Math.Max(0, Storage),
				GuardCauseRef = Fact("taf:fact:watch:v1:", "watch", RealmId,
					SettlementId, population, ((int)Gate).ToString(CultureInfo.InvariantCulture))
			};
			if (ShopTier > 0) result.TraderCauseRef = Fact("taf:fact:market:v1:",
				"market", RealmId, SettlementId, ShopTier.ToString(CultureInfo.InvariantCulture));
			if (Storage > 0) result.MigrantCauseRef = Fact("taf:fact:room:v1:",
				"room", RealmId, SettlementId, Storage.ToString(CultureInfo.InvariantCulture));
			if (LastDeedTick > 0L && KingdomPolityRules.Text(LastDeed, true))
				result.CourierCauseRef = Fact("taf:fact:deed:v1:", "deed", RealmId,
					SettlementId, LastDeedTick.ToString(CultureInfo.InvariantCulture), LastDeed);
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
