namespace ThousandAndFirst
{

	public enum KingdomGrowthArrivalCandidatePhase : byte
	{
		/// <summary>Sentinel used only by EvidencePhase while a candidate is not quarantined.</summary>
		None = 0,
		Prepared = 1,
		CreateIntent = 2,
		Escrowed = 3,
		LodgingIntent = 4,
		Observed = 5,
		ConsumeIntent = 6,
		RefusalIntent = 7,
		Settled = 8,
		Quarantined = 9,
		AwaitingChoice = 10,
		Declined = 11,
		/// <summary>The exact Growth-created body is a visible, non-citizen guest.</summary>
		GuestHosted = 12,
		/// <summary>Loaded evidence proved the hosted guest departed or died.</summary>
		GuestTerminal = 13
	}
}
