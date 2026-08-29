namespace ThousandAndFirst
{
	public enum KingdomGrowthFirstGuestFactsState : byte
	{
		Exact = 1,
		LegacyPartial = 2
	}

	public enum KingdomGrowthFirstGuestChoiceState : byte
	{
		AwaitingChoice = 1,
		Deferred = 2,
		Admitted = 3,
		Declined = 4
	}

	public enum KingdomGrowthFirstGuestBodyLeaseState : byte
	{
		None = 0,
		Reserved = 1,
		Released = 2
	}

	/// <summary>Durable physical state of a rules-v2 first guest.</summary>
	public enum KingdomGrowthFirstGuestGuestPhase : byte
	{
		None = 0,
		Preparing = 1,
		Hosted = 2,
		CitizenshipIntent = 3,
		CitizenshipPrepared = 4,
		DepartureIntent = 5,
		Terminal = 6
	}

	public enum KingdomGrowthFirstGuestTerminalState : byte
	{
		None = 0,
		Citizen = 1,
		Departed = 2,
		Died = 3,
		CouldNotJoin = 4
	}
}
