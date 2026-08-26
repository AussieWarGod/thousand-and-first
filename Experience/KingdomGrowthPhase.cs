namespace ThousandAndFirst
{

	public enum KingdomGrowthPhase : byte
	{
		Invalid = 0,
		Prepared = 1,
		WaterIntent = 2,
		WaterSettled = 3,
		SourceIntent = 4,
		SourcesSettled = 5,
		OutputIntent = 6,
		OutputsSettled = 7,
		DomainIntent = 8,
		DomainSettled = 9,
		ClockIntent = 10,
		Sinks = 11,
		Terminal = 12,
		Quarantined = 13
	}
}
