namespace ThousandAndFirst
{
	/// <summary>Append-only lifecycle of one city's assenting-moot authority.</summary>
	public enum KingdomAssentingMootPhase
	{
		None = 0,
		Prepared = 1,
		Applied = 2,
		Suspended = 3,
		Quarantined = 4
	}

	/// <summary>Two independent named memberships carried by one moot receipt.</summary>
	public enum KingdomAssentingMootRole
	{
		Assent = 1,
		Exemption = 2
	}
}
