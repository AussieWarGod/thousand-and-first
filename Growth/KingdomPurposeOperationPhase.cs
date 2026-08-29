namespace ThousandAndFirst
{
	/// <summary>Monotone checkpoints around every physical callback in one purpose operation.</summary>
	public enum KingdomPurposeOperationPhase : byte
	{
		Invalid = 0,
		Prepared = 1,
		InputDebitPending = 2,
		InputDebited = 3,
		LocalDebitPending = 4,
		LocalDebited = 5,
		EffectPending = 6,
		EffectApplied = 7,
		OutputPending = 8,
		Dispatching = 9,
		Delivered = 10,
		Acknowledged = 11,
		Quarantined = 12,
		/// <summary>The exact cargo has left source custody and is rooted between gates.</summary>
		PickupComplete = 13,
		/// <summary>The rooted cargo has an acknowledged in-flight checkpoint and may land.</summary>
		LandingPending = 14
	}
}
