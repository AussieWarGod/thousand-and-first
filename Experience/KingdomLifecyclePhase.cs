namespace ThousandAndFirst
{

	public enum KingdomLifecyclePhase : byte
	{
		Invalid = 0,
		Prepared = 1,
		ProjectionIntent = 2,
		Projected = 3,
		WaterIntent = 4,
		WaterSettled = 5,
		RemovalIntent = 6,
		Removed = 7,
		DomainIntent = 8,
		DomainSettled = 9,
		EffectIntent = 10,
		EffectsSettled = 11,
		Sinks = 12,
		ScheduleIntent = 13,
		Terminal = 14,
		Quarantined = 15
	}
}
