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

		public static readonly string[] Districts = new string[6] { "agrarian", "market", "craft", "shrine", "garrison", "academy" };

		public static bool IsValidDistrict(string District)
		{
			for (int i = 0; i < Districts.Length; i++)
			{
				if (Districts[i] == District)
				{
					return true;
				}
			}
			return false;
		}

		public static long ArrivalIntervalTicks(int Population, string District)
		{
			long num = ArrivalIntervalTicks(Population);
			if (District == "market")
			{
				num = num * 90 / 100;
			}
			return num;
		}

		public struct BuildEntry
		{
			public string Key;

			public string DisplayName;

			public string Blueprint;

			public int CostDrams;

			public long BuildTicks;

			public BuildEntry(string Key, string DisplayName, string Blueprint, int CostDrams, long BuildTicks)
			{
				this.Key = Key;
				this.DisplayName = DisplayName;
				this.Blueprint = Blueprint;
				this.CostDrams = CostDrams;
				this.BuildTicks = BuildTicks;
			}
		}

		public static readonly BuildEntry[] BuildCatalog = new BuildEntry[5]
		{
			new BuildEntry("caskrack", "cask rack (holds 64 drams)", "r_KingdomCaskRack", 4, 1200L),
			new BuildEntry("cistern", "great cistern (holds 256 drams)", "r_KingdomGreatCistern", 16, 3600L),
			new BuildEntry("bunk", "communal bunk", "r_KingdomBunk", 4, 1200L),
			new BuildEntry("shrine", "shrine stone", "r_KingdomShrine", 8, 2400L),
			new BuildEntry("fire", "communal fire", "Campfire", 2, 600L)
		};

		public static bool TryGetBuildEntry(string Key, out BuildEntry Entry)
		{
			for (int i = 0; i < BuildCatalog.Length; i++)
			{
				if (BuildCatalog[i].Key == Key)
				{
					Entry = BuildCatalog[i];
					return true;
				}
			}
			Entry = default(BuildEntry);
			return false;
		}

		public const int RaidStandingThreshold = -250;

		public const int RaidTributeDrams = 12;

		public const long RaidCooldownTicks = 8400L;

		public const long RaidWarningLeadTicks = 1200L;

		public static int RaidSize(GrowthStage Stage)
		{
			switch (Stage)
			{
			case GrowthStage.Camp:
				return 0;
			case GrowthStage.Steading:
				return 2;
			case GrowthStage.Village:
				return 3;
			case GrowthStage.Town:
				return 4;
			default:
				return 5;
			}
		}

		public static string[] RaiderTableFor(string FactionName)
		{
			if (FactionName == "Snapjaws")
			{
				return new string[3] { "Snapjaw Scavenger", "Snapjaw Scavenger", "Snapjaw Hunter" };
			}
			return null;
		}

		public static readonly string[] OutsiderLeads = new string[6] { "It is said that ", "Travelers claim that ", "The dromads tell that ", "A rumor holds that ", "The cults mutter that ", "Some deny that " };

		public static string ComposeOutsider(string Text, int Roll)
		{
			int num = Roll % OutsiderLeads.Length;
			if (num < 0)
			{
				num += OutsiderLeads.Length;
			}
			return OutsiderLeads[num] + Text + ".";
		}

		public static bool TryParseZoneID(string ZoneID, out string World, out int GX, out int GY, out int Z)
		{
			World = null;
			GX = 0;
			GY = 0;
			Z = 0;
			if (string.IsNullOrEmpty(ZoneID))
			{
				return false;
			}
			string[] array = ZoneID.Split('.');
			if (array.Length != 6)
			{
				return false;
			}
			if (!int.TryParse(array[1], out var wx) || !int.TryParse(array[2], out var wy) || !int.TryParse(array[3], out var zx) || !int.TryParse(array[4], out var zy) || !int.TryParse(array[5], out Z))
			{
				return false;
			}
			World = array[0];
			GX = wx * 3 + zx;
			GY = wy * 3 + zy;
			return true;
		}

		public static bool ZonesAdjacent(string A, string B)
		{
			if (!TryParseZoneID(A, out var worldA, out var gxA, out var gyA, out var zA) || !TryParseZoneID(B, out var worldB, out var gxB, out var gyB, out var zB))
			{
				return false;
			}
			if (worldA != worldB || zA != zB)
			{
				return false;
			}
			int dx = (gxA > gxB) ? (gxA - gxB) : (gxB - gxA);
			int dy = (gyA > gyB) ? (gyA - gyB) : (gyB - gyA);
			if (dx <= 1 && dy <= 1)
			{
				return dx + dy > 0;
			}
			return false;
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
