namespace ThousandAndFirst
{
	public static partial class KingdomMaterialRules
	{
		/// <summary>Maximum event damage. A damaged work remains standing and mendable.</summary>
		public const int MaxWearPercent = 60;

		/// <summary>Wear after an event adds damage, clamped to the shared ceiling.</summary>
		public static int AddWear(int Wear, int Added)
		{
			long total = (long)((Wear > 0) ? Wear : 0) + ((Added > 0) ? Added : 0);
			return total > MaxWearPercent ? MaxWearPercent : (int)total;
		}

		/// <summary>Remaining function percentage. Never less than 100 minus the wear ceiling.</summary>
		public static int ConditionPercent(int Wear)
		{
			int wear = (Wear > 0) ? Wear : 0;
			if (wear > MaxWearPercent) wear = MaxWearPercent;
			return 100 - wear;
		}
	}
}
