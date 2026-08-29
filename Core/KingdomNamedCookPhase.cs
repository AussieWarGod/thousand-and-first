namespace ThousandAndFirst
{
	/// <summary>Durable boundary for one city-authorized resident cook projection.</summary>
	public enum KingdomNamedCookPhase : byte
	{
		None = 0,
		Prepared = 1,
		Applied = 2,
		ReleasePrepared = 3,
		Released = 4,
		Quarantined = 5,
		DeathVacancyPrepared = 6,
		DeathVacant = 7,
		DepartureVacancyPrepared = 8,
		DepartureVacant = 9,
		RetirementVacancyPrepared = 10,
		RetirementVacant = 11,
		HandoffVacancyPrepared = 12,
		HandoffVacant = 13
	}

	/// <summary>Exact witnessed or explicit cause of a named-cook vacancy. It is encoded by the
	/// append-only phase vocabulary, so the released city-book receipt keeps its original shape.</summary>
	public enum KingdomNamedCookVacancyCause : byte
	{
		None = 0,
		Released = 1,
		Death = 2,
		Departure = 3,
		VoluntaryRetirement = 4,
		Handoff = 5
	}

	public enum KingdomNamedCookServiceState : byte
	{
		Vacant = 0,
		Available = 1,
		RecoveryPending = 2,
		Quarantined = 3
	}

	public enum KingdomNamedCookVerdict : byte
	{
		Allowed = 0,
		Unfounded = 1,
		NotOwnedCity = 2,
		NotStandingResident = 3,
		BodyNotExact = 4,
		PlayerOrFollower = 5,
		NativeRecipeAlreadyPresent = 6,
		ForeignCookMarker = 7,
		OpenReceipt = 8,
		MalformedIdentity = 9
	}
}
