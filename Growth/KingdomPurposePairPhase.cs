namespace ThousandAndFirst
{
	/// <summary>Frozen lifecycle for one reciprocal two-city purpose pair.</summary>
	public enum KingdomPurposePairPhase : byte
	{
		Invalid = 0,
		Frozen = 1,
		BootstrapOutstanding = 2,
		SecondPending = 3,
		ReturnOutstanding = 4,
		CargoAwaitingActivation = 5,
		Active = 6,
		OperationOutstanding = 7,
		CargoAwaitingConsumption = 8,
		Orphaned = 9,
		Dormant = 10,
		Quarantined = 11
	}
}
