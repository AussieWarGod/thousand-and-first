namespace ThousandAndFirst.Harness
{
	internal sealed class KingdomCampHeartSaveSnapshot
	{
		internal readonly string GameId, RealmId, CityId, ZoneId, HeartId, UpgradeJobId;
		internal readonly string StoreId, FireId, TimberId, ContentsDigest, BrushDigest, TentJobId;
		internal readonly int HeartX, HeartY, StoreX, StoreY, FireX, FireY, Water;
		internal readonly long Turns, TimeTicks;

		internal KingdomCampHeartSaveSnapshot(string gameId, string realmId, string cityId,
			string zoneId, string heartId, string upgradeJobId, string storeId, string fireId,
			string timberId, string contentsDigest, string brushDigest, string tentJobId, int heartX, int heartY,
			int storeX, int storeY, int fireX, int fireY, int water, long turns, long timeTicks)
		{
			GameId = gameId; RealmId = realmId; CityId = cityId; ZoneId = zoneId;
			HeartId = heartId; UpgradeJobId = upgradeJobId; StoreId = storeId; FireId = fireId;
			TimberId = timberId; ContentsDigest = contentsDigest; BrushDigest = brushDigest; TentJobId = tentJobId;
			HeartX = heartX; HeartY = heartY; StoreX = storeX; StoreY = storeY;
			FireX = fireX; FireY = fireY; Water = water; Turns = turns; TimeTicks = timeTicks;
		}
	}
}
