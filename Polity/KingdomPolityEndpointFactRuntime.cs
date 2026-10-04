using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Builds exact 1-3 endpoint facts from persisted city carriers and their
	/// canonical owned-zone locators. It does not inspect or create unloaded zones. The facts
	/// themselves are built by the engine-free KingdomPolityEndpointFactRules.</summary>
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
			if (!KingdomPolityEndpointFactRules.TryBuild(System.RealmId, System.City.SettlementId,
				System.SeatName, KingdomPolityEndpointFactRules.CanonicalZone(System.ClaimedZones),
				true, System.Population, (int)System.Stage, System.ShopTier,
				System.LastKnownStorageSpace, System.Gate, System.LastDeed, System.LastDeedTick,
				System.City, out KingdomPolityEndpointFacts seat, out Failure)) return false;
			endpoints.Add(seat);
			List<KingdomSettlement> rows = System.NonSeatSettlements();
			for (int i = 0; i < rows.Count; i++)
			{
				KingdomSettlement row = rows[i];
				if (row?.City == null) { Failure = "non-seat polity endpoint is incomplete"; return false; }
				row.City.Normalize();
				if (!KingdomPolityEndpointFactRules.TryBuild(System.RealmId, row.City.SettlementId,
					row.SettlementName, KingdomPolityEndpointFactRules.CanonicalZone(row.ClaimedZones),
					false, row.Population, (int)row.Stage, row.ShopTier, row.LastKnownStorageSpace,
					row.Gate, row.LastDeed, row.LastDeedTick, row.City,
					out KingdomPolityEndpointFacts endpoint, out Failure)) return false;
				endpoints.Add(endpoint);
			}
			KingdomPolityEndpointFactRules.LinkSources(System.RealmId, endpoints);
			Offer = new KingdomPolityDispatchOffer
			{
				RealmId = System.RealmId, Tick = Tick, Endpoints = endpoints
			};
			return true;
		}
	}
}
