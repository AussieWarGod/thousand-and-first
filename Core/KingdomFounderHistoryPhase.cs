namespace ThousandAndFirst
{
	/// <summary>
	/// Durable boundary for the one TAF-owned founder-memory projection in a world.
	/// Values 3-5 are schema-1 migration evidence only; schema 2 never writes them.
	/// </summary>
	public enum KingdomFounderHistoryPhase : byte
	{
		None = 0,
		Suppressed = 1,
		Prepared = 2,
		/// <summary>Schema-1 only: a vanilla HistoryKit entity had been inserted.</summary>
		EntityPublished = 3,
		/// <summary>Schema-1 only: a vanilla HistoryKit event had been inserted.</summary>
		EventPublished = 4,
		/// <summary>Schema-1 only: a vanilla Sultan-journal note had been inserted.</summary>
		NotePublished = 5,
		Committed = 6,
		Quarantined = 7
	}
}
