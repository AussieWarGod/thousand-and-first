namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{
		/// <summary>Ruin remains a readable place rather than rubble.</summary>
		public const int RuinStandingFloorPercent = 25;
		/// <summary>Best-preserved ruin condition; also the lodging condemnation boundary.</summary>
		public const int RuinStandingCeilingPercent = 60;

		internal static int RuinedStandingPercent(int roll)
		{
			if (roll < 0) roll = 0;
			if (roll > 99) roll = 99;
			return RuinStandingCeilingPercent - roll * (RuinStandingCeilingPercent - RuinStandingFloorPercent) / 99;
		}
	}
}
