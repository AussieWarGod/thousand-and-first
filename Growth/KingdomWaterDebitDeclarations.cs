namespace ThousandAndFirst
{
	/// <summary>The durable in-memory phase of one exact draw from dedicated water stores.</summary>
	public enum KingdomWaterDebitState
	{
		Failed = 0,
		Reserved = 1,
		Committed = 2,
		RolledBack = 3
	}

	/// <summary>Why an exact water receipt could not reach the requested phase.</summary>
	public enum KingdomWaterDebitFault
	{
		None = 0,
		InvalidSurvey = 1,
		InvalidVessels = 2,
		InsufficientWater = 3,
		VesselChanged = 4,
		SurveyChanged = 5,
		DrainMismatch = 6,
		Exception = 7,
		RestoreFailed = 8,
		Busy = 9
	}

	/// <summary>The engine action a receipt phase permits. Kept pure so every state is tested.</summary>
	public enum KingdomWaterDebitAction
	{
		Reject = 0,
		SucceedWithoutMutation = 1,
		Drain = 2,
		Restore = 3,
		CancelReservation = 4
	}
}
