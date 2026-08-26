namespace ThousandAndFirst
{

	public enum KingdomRaidChannelState : byte
	{
		None = 0,
		AwaitingDelivery = 1,
		Issued = 2,
		Acknowledged = 3,
		RedeliveryQueued = 4,
		Closed = 5
	}
}
