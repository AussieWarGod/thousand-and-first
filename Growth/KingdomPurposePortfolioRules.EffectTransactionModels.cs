namespace ThousandAndFirst
{
	internal enum KingdomPurposeEffectAttemptState : byte
	{
		Invalid = 0,
		Clear = 1,
		Before = 2,
		Settled = 3,
		Ambiguous = 4
	}

	internal enum KingdomPurposeEffectCallbackAftermath : byte
	{
		Invalid = 0,
		Unavailable = 1,
		Settled = 2,
		Ambiguous = 3
	}

}
