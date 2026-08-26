using System;

namespace ThousandAndFirst.Api
{
	/// <summary>One bounded network node.</summary>
	public readonly struct KingdomExtensionNetworkNode
	{
		/// <summary>Owner-local stable node key.</summary>
		public readonly string Key;
		/// <summary>Held zone this node stands in.</summary>
		public readonly string ZoneId;
		/// <summary>Node role.</summary>
		public readonly KingdomExtensionNetworkRole Role;
		/// <summary>Units supplied or demanded per world day.</summary>
		public readonly int RatePerDay;
		/// <summary>Brownout priority; lower is served first.</summary>
		public readonly int Priority;

		/// <summary>Builds one proposed node.</summary>
		public KingdomExtensionNetworkNode(string Key, string ZoneId,
			KingdomExtensionNetworkRole Role, int RatePerDay, int Priority)
		{
			this.Key = Key;
			this.ZoneId = ZoneId;
			this.Role = Role;
			this.RatePerDay = RatePerDay;
			this.Priority = Priority;
		}
	}
}
