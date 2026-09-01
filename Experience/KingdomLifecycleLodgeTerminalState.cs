namespace ThousandAndFirst
{

	/// <summary>Monotone recovery state for a Lodge target that dies before lodging can finish.</summary>
	public enum KingdomLifecycleLodgeTerminalState : byte
	{
		None = 0,
		BodyDeathProved = 1,
		ResidentSourceProved = 2,
		AbandonIntent = 3,
		Abandoned = 4,
		AuthorityReleased = 5
	}
}
