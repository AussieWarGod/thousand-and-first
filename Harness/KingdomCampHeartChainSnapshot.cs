namespace ThousandAndFirst.Harness
{
	/// <summary>Immutable physical anchor for a higher-heart save witness.</summary>
	internal sealed class KingdomCampHeartChainAnchor
	{
		internal readonly string Id;
		internal readonly int X, Y;
		internal KingdomCampHeartChainAnchor(string id, int x, int y)
		{ Id = id; X = x; Y = y; }
	}

	/// <summary>
	/// Higher-heart cold-load evidence, separate from the existing rung-two camp witness.
	/// Digests bind the construction ledger, resident census, physical support works and store
	/// custody observed at the recorded clock. They are comparisons, not authority to repair
	/// or mint loaded state. The native observer must still prove the named physical objects.
	/// </summary>
	internal sealed class KingdomCampHeartChainSnapshot
	{
		internal readonly string GameId, RealmId, CityId, ZoneId, ResidentId, JobId;
		internal readonly string JobsDigest, ResidentsDigest, SupportDigest, CustodyDigest;
		internal readonly KingdomCampHeartChainAnchor Heart, Basin, Store, Track;
		internal readonly int Rung, Population, Water, Food;
		internal readonly long Turns, TimeTicks;

		internal KingdomCampHeartChainSnapshot(string gameId, string realmId, string cityId,
			string zoneId, int rung, KingdomCampHeartChainAnchor heart,
			KingdomCampHeartChainAnchor basin, KingdomCampHeartChainAnchor store,
			KingdomCampHeartChainAnchor track, string residentId, string jobId,
			string jobsDigest, string residentsDigest, string supportDigest, string custodyDigest,
			int population, int water, int food, long turns, long timeTicks)
		{
			GameId = gameId; RealmId = realmId; CityId = cityId; ZoneId = zoneId; Rung = rung;
			Heart = heart; Basin = basin; Store = store; Track = track;
			ResidentId = residentId; JobId = jobId; JobsDigest = jobsDigest;
			ResidentsDigest = residentsDigest; SupportDigest = supportDigest; CustodyDigest = custodyDigest;
			Population = population; Water = water; Food = food; Turns = turns; TimeTicks = timeTicks;
		}
	}
}
