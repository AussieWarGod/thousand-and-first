namespace ThousandAndFirst
{

	public enum KingdomRaidRecoveryState : byte
	{
		None = 0,
		Offered = 1,
		Active = 2,
		Ready = 3,
		Resolved = 4,
		Declined = 5,
		LegacyUnavailable = 6,
		CoveredByExisting = 7
	}
}
