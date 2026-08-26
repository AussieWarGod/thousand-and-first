namespace ThousandAndFirst
{

	/// <summary>Wave-1 migration input. Runtime Wave 2 supplies the load tick and the old
	/// city-carried pending crop tuple. All legacy clocks are deliberately restamped to Now;
	/// none of their elapsed pre-transactional time becomes backlog.</summary>
	public sealed class KingdomGrowthMigrationInput
	{
		public bool HasNow;
		public long Now;
		public int PendingCrop;
		public string PendingCropBlueprint;
		public string PendingCropZoneId;
		public bool OptionEnabled;
		public bool ScarcityEnabled;
		public bool Healthy;
		public long ArrivalIntervalTicks;
	}
}
