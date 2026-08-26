namespace ThousandAndFirst
{
	/// <summary>Exclusive use of one physical stockpile object in a material debit.</summary>
	public enum KingdomMaterialDebitSourceKind : byte
	{
		None = 0,
		Material = 1,
		Exotic = 2,
		BitStock = 3
	}

	/// <summary>The externally observable terminal result of one material receipt.</summary>
	public enum KingdomMaterialDebitOutcome : byte
	{
		InvalidReservation = 0,
		Reserved = 1,
		ExactCommit = 2,
		CleanRefusal = 3,
		RecoverablePartial = 4,
		IrreversiblePartial = 5,
		CompensatedExact = 6,
		Cancelled = 7
	}

	/// <summary>Why a material receipt did not reach the requested phase.</summary>
	public enum KingdomMaterialDebitFault : byte
	{
		None = 0,
		InvalidStock = 1,
		InvalidCost = 2,
		InvalidSources = 3,
		InsufficientMaterials = 4,
		InsufficientBits = 5,
		InsufficientExotics = 6,
		SourceChanged = 7,
		OperationRefused = 8,
		OperationMismatch = 9,
		Exception = 10,
		CompensationUnsafe = 11,
		CompensationFailed = 12,
		Busy = 13,
		WrongPhase = 14
	}
}
