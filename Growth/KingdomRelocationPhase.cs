namespace ThousandAndFirst
{
	/// <summary>Durable phase of one founder-approved heart ring call.</summary>
	public enum KingdomRelocationPhase : byte
	{
		Active = 1,
		Complete = 2,
		Quarantined = 3
	}

	/// <summary>Durable phase of one sequential whole-lot move.</summary>
	public enum KingdomRelocationMovePhase : byte
	{
		Waiting = 0,
		Working = 1,
		Handover = 2,
		Complete = 3,
		RollingBack = 4,
		RolledBack = 5
	}

	/// <summary>State of one exact physical object during handover.</summary>
	public enum KingdomRelocationRowState : byte
	{
		Source = 0,
		Rooted = 1,
		Destination = 2
	}

	/// <summary>State of one exact destination-ground clearance row.</summary>
	public enum KingdomRelocationClearState : byte
	{
		Standing = 0,
		RemovalPending = 1,
		Removed = 2
	}
}
