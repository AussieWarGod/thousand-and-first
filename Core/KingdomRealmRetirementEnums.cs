namespace ThousandAndFirst
{
	/// <summary>Terminal current-save lifecycle. This is not profile-generation retirement.</summary>
	public enum KingdomRealmRetirementPhase : byte
	{
		None = 0,
		Planning = 1,
		Paused = 2,
		CleaningGround = 3,
		ReadyForFence = 4,
		FenceCommitted = 5,
		PreparedForRemoval = 6,
		Quarantined = 7
	}

	public enum KingdomRemovalLocatorState : byte
	{
		OutstandingVisit = 0,
		Cleaning = 1,
		Cleaned = 2,
		Contested = 3,
		Diverged = 4
	}

	public enum KingdomRemovalProjectionKind : byte
	{
		Authority = 1,
		ZoneProperty = 2,
		Object = 3,
		ObjectPart = 4,
		ObjectProperty = 5,
		Citizen = 6,
		Faction = 7,
		FactionEdge = 8,
		Ability = 9,
		Quest = 10,
		Job = 11,
		Cargo = 12,
		JournalHistory = 13,
		GlobalState = 14,
		ExternalOwnership = 15,
		LegacyArtifact = 16
	}

	public enum KingdomRemovalDisposition : byte
	{
		Pending = 0,
		Preserved = 1,
		Restored = 2,
		Converted = 3,
		Stripped = 4,
		Retired = 5,
		Closed = 6,
		PriorUnknown = 7,
		Untracked = 8,
		Diverged = 9,
		Blocked = 10,
		/// <summary>Exact terminal cut is authorized but has not been falsely reported complete.</summary>
		TerminalIntent = 11
	}

	public enum KingdomIdentityFenceDisposition : byte
	{
		Unfounded = 0,
		Operational = 1,
		RetiredOrAbandoned = 2,
		PreparedForRemoval = 3
	}

	public enum KingdomIdentityFenceObservation : byte
	{
		Absent = 0,
		Unfounded = 1,
		Operational = 2,
		Prepared = 3,
		LostAuthority = 4,
		Malformed = 5,
		WrongGame = 6
	}
}
