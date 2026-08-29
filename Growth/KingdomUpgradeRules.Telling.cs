namespace ThousandAndFirst
{
	public static partial class KingdomUpgradeRules
	{
		/// <summary>Reported when a blueprint declares no liquid capacity, which is different
		/// from declaring a capacity of nothing. Negative because Qud's own open pools use a
		/// negative MaxVolume for "unbounded", and neither case is a reason to refuse.</summary>
		public const int UnknownCapacity = -1;

	}
}
