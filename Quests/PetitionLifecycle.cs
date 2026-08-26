using System;
using System.Globalization;

namespace ThousandAndFirst
{
	/// <summary>Legacy projection values. Runtime authority is the retained terminal operation in
	/// <see cref="KingdomLifecycleBook.Petition"/>.</summary>
	public enum PetitionLifecycle : byte
	{
		None = 0,
		Offered = 1,
		Accepted = 2,
		Declined = 3,
		Resolved = 4,
		Expired = 5
	}
}
