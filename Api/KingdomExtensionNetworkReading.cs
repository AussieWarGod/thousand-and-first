using System;

namespace ThousandAndFirst.Api
{
	/// <summary>Frozen durable result of the latest solve for one extension network.</summary>
	public readonly struct KingdomExtensionNetworkReading
	{
		/// <summary>Owner-qualified network key.</summary>
		public readonly string Key;
		/// <summary>Owner-qualified resource key.</summary>
		public readonly string ResourceKey;
		/// <summary>Tick through which this network has been integrated.</summary>
		public readonly long ProcessedThroughTick;
		/// <summary>Units per day delivered by the latest bounded solve.</summary>
		public readonly int LastFlowPerDay;
		/// <summary>Demand units per day the latest solve could not serve.</summary>
		public readonly int LastBrownoutPerDay;

		/// <summary>Builds a frozen reading.</summary>
		public KingdomExtensionNetworkReading(string Key, string ResourceKey, long ProcessedThroughTick,
			int LastFlowPerDay, int LastBrownoutPerDay)
		{
			this.Key = Key ?? "";
			this.ResourceKey = ResourceKey ?? "";
			this.ProcessedThroughTick = ProcessedThroughTick;
			this.LastFlowPerDay = LastFlowPerDay;
			this.LastBrownoutPerDay = LastBrownoutPerDay;
		}
	}
}
