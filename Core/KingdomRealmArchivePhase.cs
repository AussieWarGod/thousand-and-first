namespace ThousandAndFirst
{
	public enum KingdomRealmArchivePhase : byte
	{
		None = 0,
		Prepared = 1,
		TradeClosed = 2,
		ChronicleFrozen = 3,
		ChronicleCleared = 4,
		Closed = 5,
		Restoring = 6,
		Restored = 7,
		Quarantined = 8,
		/// <summary>Durable intent published before clearing the live old-realm graph.</summary>
		Resetting = 9,
		/// <summary>Durable intent published before retiring the exile mirrors after return.</summary>
		ReturnCleaning = 10,
		/// <summary>All exile mirrors exactly published; later callbacks may no longer repair
		/// a canonical-looking missing mirror.</summary>
		MirrorsPublished = 11
	}
}
