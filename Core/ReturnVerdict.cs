namespace ThousandAndFirst
{
	/// <summary>Why a return may not proceed, or that it is allowed.</summary>
	public enum ReturnVerdict
	{
		Allowed = 0,
		NeverCastOut = 1,
		FoundedAgain = 2,
		NothingRemembered = 3,
		NotOnTheirGround = 4,
		RegardTooLow = 5
	}
}
