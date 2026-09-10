namespace ThousandAndFirst
{
	internal static class KingdomSealPendingRules
	{
		internal static bool RoadlessEntrance(bool PublicEntranceFault, bool NoEntry, int StreetCount)
			=> PublicEntranceFault && NoEntry && StreetCount == 0;
	}
}
