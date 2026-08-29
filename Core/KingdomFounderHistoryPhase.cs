namespace ThousandAndFirst
{
	/// <summary>Durable publication boundary for the one public founder memory in a world.</summary>
	public enum KingdomFounderHistoryPhase : byte
	{
		None = 0,
		Suppressed = 1,
		Prepared = 2,
		EntityPublished = 3,
		EventPublished = 4,
		NotePublished = 5,
		Committed = 6,
		Quarantined = 7
	}
}
