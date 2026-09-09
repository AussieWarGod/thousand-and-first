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
		Complete = 6,
		/// <summary>
		/// Four founding citizens stand on the approach, enrolled and on the roll. Reachable only
		/// from <see cref="Complete"/>, and only for a receipt whose founders disposition is
		/// <see cref="KingdomQuickstartFoundersDisposition.Seeding"/>.
		/// </summary>
		FoundersSeeded = 7
	}

	/// <summary>
	/// Durable state of the founding cohort WITHIN the <see cref="KingdomQuickstartPhase.Complete"/>
	/// phase. Phase alone cannot carry it: a phase advances exactly one step and only forward, while
	/// the cohort needs a state that survives a crash between its reversible and irreversible halves.
	/// <para>
	/// <see cref="Omitted"/> is what a world written before founders existed decodes as, and what a
	/// world founded with the option off is stamped with before its receipt is ever published. Both
	/// are terminal, both encode on the old wire, and neither can ever gain founders.
	/// </para>
	/// </summary>
	public enum KingdomQuickstartFoundersDisposition : byte
	{
		Omitted = 0,
		Pending = 1,
		Seeding = 2,
		Seeded = 3,
		Faulted = 4
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
		/// <summary>
		/// True only for a receipt minted by a version that bares the shelter lots when it builds
		/// the world, and therefore the only receipt whose founding pass owes those lots a stake.
		/// A receipt written before the lots existed decodes false and resumes exactly as it did.
		/// </summary>
		public bool ShelterObligation;

		/// <summary>Durable state of the founding cohort. See the enum for what each value means.</summary>
		public KingdomQuickstartFoundersDisposition FoundersDisposition;

		/// <summary>
		/// The exact four founder bodies, by object id, in index order. Empty for every disposition
		/// that has no cohort; four non-empty, distinct identities for every disposition that does.
		/// The array itself is fixed so no caller can shrink, grow or alias the cohort.
		/// </summary>
		public readonly string[] FounderObjectIds = { "", "", "", "" };

		public KingdomQuickstartReceipt Copy()
		{
			KingdomQuickstartReceipt copy = new KingdomQuickstartReceipt
			{
				ProfileKey = ProfileKey,
				ZoneId = ZoneId,
				Phase = Phase,
				FoodBlueprint = FoodBlueprint,
				WaterObjectId = WaterObjectId,
				LarderObjectId = LarderObjectId,
				StockpileObjectId = StockpileObjectId,
				AdvisorDisposition = AdvisorDisposition,
				AdvisorObjectId = AdvisorObjectId,
				ShelterObligation = ShelterObligation,
				FoundersDisposition = FoundersDisposition
			};
			for (int i = 0; i < FounderObjectIds.Length; i++)
				copy.FounderObjectIds[i] = FounderObjectIds[i] ?? "";
			return copy;
		}
	}
}
