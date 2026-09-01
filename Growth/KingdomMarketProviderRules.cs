namespace ThousandAndFirst
{
	/// <summary>Pure final gate for one same-operation physical market observation.</summary>
	public static class KingdomMarketProviderRules
	{
		public static bool ExactLiveProjection(bool LiveCapability, int ObservedTier,
			int ProjectedTier)
		{
			return LiveCapability
				&& ObservedTier >= KingdomShopStockRules.FirstPhysicalMarketTier
				&& ObservedTier <= KingdomShopStockRules.MaximumTier
				&& ObservedTier == ProjectedTier;
		}

		public static bool ExactLiveAuthority(bool LiveCapability, int ObservedTier,
			int ProjectedTier, int RecordedTier)
		{
			return ExactLiveProjection(LiveCapability, ObservedTier, ProjectedTier)
				&& ObservedTier == RecordedTier;
		}
	}
}
