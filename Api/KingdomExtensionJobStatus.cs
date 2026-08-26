using System;

namespace ThousandAndFirst.Api
{
	/// <summary>Durable state of an extension job.</summary>
	public enum KingdomExtensionJobStatus : byte
	{
		/// <summary>Carrier is in flight.</summary>
		Open = 0,
		/// <summary>Completion changes committed once.</summary>
		Completed = 1,
		/// <summary>Completion could not commit; reserved cargo was restored where possible.</summary>
		Failed = 2
	}
}
