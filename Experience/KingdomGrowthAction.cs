namespace ThousandAndFirst
{

	// Numeric values are append-only nested Growth wire contracts.
	public enum KingdomGrowthAction : byte
	{
		None = 0,
		Heartbeat = 1,
		Arrival = 2,
		Departure = 3,
		Delivery = 4,
		Sow = 5,
		Withdraw = 6,
		Ripen = 7,
		Harvest = 8,
		Fetch = 9,
		Mill = 10,
		Irrigate = 11
	}
}
