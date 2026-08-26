namespace ThousandAndFirst
{

	public enum KingdomGrowthDomainCallbackKind : byte
	{
		None = 0,
		Enroll = 1,
		RosterAdd = 2,
		RosterRemove = 3,
		CreedSet = 4,
		PopulationAdjust = 5,
		PendingCropSet = 6,
		FieldSet = 7,
		ScarcitySet = 8,
		AccountingSet = 9,
		CropRegistrySet = 10,
		SubsidenceScheduleSet = 11,
		PorterJobSet = 12,
		EscrowRelease = 13
	}
}
