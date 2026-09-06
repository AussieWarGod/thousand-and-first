namespace ThousandAndFirst
{
	public static partial class KingdomLodgingRules
	{
		/// <summary>A home ceases to be a roof at the best-preserved inherited ruin's condition.</summary>
		public const int CondemnedWearPercent = 100 - KingdomRules.RuinStandingCeilingPercent;

		/// <summary>The threshold is the first condemned wear value, not the last tolerable one.</summary>
		public static bool IsCondemned(int Wear) => Wear >= CondemnedWearPercent;
	}
}
