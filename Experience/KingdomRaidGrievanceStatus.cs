namespace ThousandAndFirst
{

	// Numeric values are append-only raid-domain wire contracts. Relationship standing is not a
	// grievance and none of these values may be inferred from it.
	public enum KingdomRaidGrievanceStatus : byte
	{
		None = 0,
		Available = 1,
		Reserved = 2,
		Consumed = 3,
		Waived = 4,
		Quarantined = 5
	}
}
