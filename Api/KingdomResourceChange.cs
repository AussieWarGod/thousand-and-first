using System;

namespace ThousandAndFirst.Api
{
	/// <summary>One signed change to an extension-owned resource. Keys may be owner-local or already
	/// qualified to the same owner; foreign keys are refused.</summary>
	public readonly struct KingdomResourceChange
	{
		/// <summary>Resource key.</summary>
		public readonly string ResourceKey;
		/// <summary>Signed units. Zero is malformed and the containing atomic result is refused.</summary>
		public readonly long Amount;

		/// <summary>Builds one proposed atomic change.</summary>
		public KingdomResourceChange(string ResourceKey, long Amount)
		{
			this.ResourceKey = ResourceKey;
			this.Amount = Amount;
		}
	}
}
