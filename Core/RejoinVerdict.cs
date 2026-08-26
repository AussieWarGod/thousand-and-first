using System;
using System.Collections.Generic;

namespace ThousandAndFirst
{
	/// <summary>Why a seceded city may not be taken back, or that it may.</summary>
	public enum RejoinVerdict
	{
		Allowed = 0,
		NothingSeceded = 1,
		RealmIsFull = 2,
		NotOnTheirGround = 3,
		ClashStillLive = 4,
		StandingTooLow = 5
	}
}
