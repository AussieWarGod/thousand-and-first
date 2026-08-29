namespace ThousandAndFirst
{
	public enum KingdomLabCivicKind : byte
	{
		None = 0,
		SavantPrice = 1,
		RefusalDeparture = 2
	}

	public enum KingdomLabCivicPhase : byte
	{
		None = 0,
		Prepared = 1,
		ChoicePrepared = 2,
		Active = 3,
		Closed = 4,
		Quarantined = 5
	}

	public enum KingdomLabCivicRequest : byte
	{
		None = 0,
		ShrineUnconsecrated = 1,
		NeighbourRehoused = 2,
		RoofRefusal = 3
	}

	public enum KingdomLabCivicChoice : byte
	{
		None = 0,
		Granted = 1,
		Refused = 2
	}

	public enum KingdomLabCivicClosure : byte
	{
		None = 0,
		Refused = 1,
		Rehoused = 2,
		Departed = 3,
		CauseGone = 4,
		OwnerGone = 5
	}

	internal enum KingdomLabDepartureProjection : byte
	{
		Diverged = 0,
		RecoverableAtSource = 1,
		Active = 2
	}

	internal enum KingdomLabObjectMatch : byte
	{
		Missing = 0,
		Unique = 1,
		Duplicate = 2
	}
}
