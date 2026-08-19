namespace ThousandAndFirst
{
	internal static class KingdomChargingRules
	{
		internal const int FullChargeRate = 150;

		internal static int Output(int Effectiveness)
		{
			if (Effectiveness <= 0)
			{
				return 0;
			}
			if (Effectiveness >= 100)
			{
				return FullChargeRate;
			}
			return FullChargeRate * Effectiveness / 100;
		}
	}
}
