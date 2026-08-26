using System;

namespace ThousandAndFirst.Api
{
	/// <summary>Role one node plays in an extension network.</summary>
	public enum KingdomExtensionNetworkRole : byte
	{
		/// <summary>Produces the declared resource.</summary>
		Source = 0,
		/// <summary>Consumes capacity, in priority order.</summary>
		Sink = 1,
		/// <summary>Transmits without producing or demanding.</summary>
		Relay = 2
	}
}
