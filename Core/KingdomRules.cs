namespace ThousandAndFirst
{
	public enum GrowthStage
	{
		Camp = 0,
		Steading = 1,
		Village = 2,
		Town = 3,
		City = 4
	}

	public static class KingdomRules
	{
		public static int SpilloverPercent(GrowthStage Stage)
		{
			switch (Stage)
			{
			case GrowthStage.Camp:
				return 50;
			case GrowthStage.Steading:
				return 40;
			case GrowthStage.Village:
				return 30;
			case GrowthStage.Town:
				return 20;
			default:
				return 10;
			}
		}

		public static int SpilloverDelta(int RepDelta, GrowthStage Stage)
		{
			return RepDelta * SpilloverPercent(Stage) / 100;
		}

		public const int DramsPerArrival = 2;

		public const int MaxArrivalsPerVisit = 3;

		public const int DryIntervalsToEmigrate = 2;

		public const int FoundingCostDrams = 8;

		public const int FetchDramsPerSettler = 2;

		public static readonly string[] Origins = new string[6] { "the salt marshes", "the desert canyons", "the hills", "the flower fields", "the rust wells", "the banana grove" };

		public static int UpkeepDrams(int Population)
		{
			return Population / 4;
		}

		public static int FetchableDrams(int Population, int OpenWater, int StorageSpace)
		{
			int num = Population * FetchDramsPerSettler;
			if (OpenWater < num)
			{
				num = OpenWater;
			}
			if (StorageSpace < num)
			{
				num = StorageSpace;
			}
			if (num >= 0)
			{
				return num;
			}
			return 0;
		}

		public static long ArrivalIntervalTicks(int Population)
		{
			return 3600 + 600L * Population;
		}

		public static GrowthStage StageFor(int Population, int StoredDrams)
		{
			if (Population >= 50 && StoredDrams >= 1024)
			{
				return GrowthStage.City;
			}
			if (Population >= 25 && StoredDrams >= 256)
			{
				return GrowthStage.Town;
			}
			if (Population >= 12 && StoredDrams >= 64)
			{
				return GrowthStage.Village;
			}
			if (Population >= 5 && StoredDrams >= 16)
			{
				return GrowthStage.Steading;
			}
			return GrowthStage.Camp;
		}

		public static bool TryParseFactionAmount(string Parameter, out string FactionName, out int Amount)
		{
			FactionName = null;
			Amount = 0;
			if (string.IsNullOrEmpty(Parameter))
			{
				return false;
			}
			int num = Parameter.LastIndexOf(':');
			if (num <= 0 || num >= Parameter.Length - 1)
			{
				return false;
			}
			if (!int.TryParse(Parameter.Substring(num + 1).Trim(), out Amount))
			{
				return false;
			}
			FactionName = Parameter.Substring(0, num).Trim();
			return FactionName.Length > 0;
		}
	}
}
