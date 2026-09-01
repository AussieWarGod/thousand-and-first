namespace ThousandAndFirst
{
	/// <summary>One reviewed wilderness start offered only by Kingdom Quickstart.</summary>
	public sealed class KingdomQuickstartProfile
	{
		public readonly string Key;
		public readonly string LocationId;
		public readonly string ZoneId;
		public readonly string CityName;
		public readonly string TerrainFamily;
		public readonly int WorldX;
		public readonly int WorldY;

		internal KingdomQuickstartProfile(string key, string locationId, string zoneId,
			string cityName, string terrainFamily, int worldX, int worldY)
		{
			Key = key;
			LocationId = locationId;
			ZoneId = zoneId;
			CityName = cityName;
			TerrainFamily = terrainFamily;
			WorldX = worldX;
			WorldY = worldY;
		}
	}

	public enum KingdomQuickstartPhase : byte
	{
		Reserved = 0,
		Founded = 1,
		WaterStocked = 2,
		FoodStocked = 3,
		MaterialsStocked = 4,
		AdvisorResolved = 5,
		Complete = 6
	}

	public enum KingdomQuickstartAdvisorDisposition : byte
	{
		Unresolved = 0,
		Included = 1,
		Omitted = 2
	}

	/// <summary>Measured durable state at one grant boundary.</summary>
	public enum KingdomQuickstartGrantObservation : byte
	{
		Absent = 0,
		ExactPlaced = 1,
		ForeignOrMalformed = 2
	}

	/// <summary>Only lawful response to one measured grant boundary.</summary>
	public enum KingdomQuickstartRecoveryAction : byte
	{
		Refuse = 0,
		PreparePlaceAndPublish = 1,
		PublishExisting = 2,
		VerifyPublished = 3
	}

	/// <summary>Durable identities of the exact physical quickstart grant.</summary>
	public sealed class KingdomQuickstartReceipt
	{
		public string ProfileKey = "";
		public string ZoneId = "";
		public KingdomQuickstartPhase Phase;
		public string FoodBlueprint = "";
		public string WaterObjectId = "";
		public string LarderObjectId = "";
		public string StockpileObjectId = "";
		public KingdomQuickstartAdvisorDisposition AdvisorDisposition;
		public string AdvisorObjectId = "";

		public KingdomQuickstartReceipt Copy()
		{
			return new KingdomQuickstartReceipt
			{
				ProfileKey = ProfileKey,
				ZoneId = ZoneId,
				Phase = Phase,
				FoodBlueprint = FoodBlueprint,
				WaterObjectId = WaterObjectId,
				LarderObjectId = LarderObjectId,
				StockpileObjectId = StockpileObjectId,
				AdvisorDisposition = AdvisorDisposition,
				AdvisorObjectId = AdvisorObjectId
			};
		}
	}
}
