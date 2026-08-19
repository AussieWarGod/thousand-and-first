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

		public const int DryIntervalsToWither = 3;

		public const int LoyalCoreSettlers = 2;

		public const int MaxPopulation = 60;

		public const int MaxBuildings = 40;

		public const int MaxCharters = 8;

		public const int MaxDedicatedVessels = 24;

		public const int FoundingCostDrams = 8;

		public const int FetchDramsPerSettler = 2;

		public static readonly string[] Origins = new string[6] { "the salt marshes", "the desert canyons", "the hills", "the flower fields", "the rust wells", "the banana grove" };

		public const long TicksPerDay = 1200L;

		public const int MaxUpkeepDaysCharged = 3;

		public static int UpkeepDrams(int Population)
		{
			return Population / 4;
		}

		/// <summary>
		/// Days of settlement life to resolve on arrival. Absence is forgiven beyond the cap:
		/// a season away costs the same as three days, so leaving is never punished.
		/// </summary>
		/// <param name="ElapsedTicks">Ticks since the last heartbeat.</param>
		/// <returns>Whole days to run, 0 to <see cref="MaxUpkeepDaysCharged"/>.</returns>
		public static int HeartbeatDays(long ElapsedTicks)
		{
			if (ElapsedTicks <= 0)
			{
				return 0;
			}
			long days = ElapsedTicks / TicksPerDay;
			if (days > MaxUpkeepDaysCharged)
			{
				days = MaxUpkeepDaysCharged;
			}
			return (int)days;
		}

		/// <summary>Stock tier a settlement's shops carry at a given growth stage.</summary>
		public static int ShopTierForStage(GrowthStage Stage)
		{
			switch (Stage)
			{
			case GrowthStage.Camp:
				return 1;
			case GrowthStage.Steading:
				return 2;
			case GrowthStage.Village:
				return 3;
			case GrowthStage.Town:
				return 5;
			default:
				return 7;
			}
		}

		/// <summary>
		/// Whether the settlement has a bed free for one more settler. Nobody arrives to
		/// sleep in the dirt; housing is the real ceiling on population.
		/// </summary>
		public static bool HasRoomToHouse(int Population, int Beds)
		{
			return Population < Beds;
		}

		/// <summary>
		/// Allocates citizens to works in priority order. Works declare how they respond to
		/// short crew: a <b>scaled</b> work runs at whatever fraction it has hands for (one
		/// person on a two-person crank still turns it, half as fast), while a
		/// <b>threshold</b> work needs its full crew or nothing at all &mdash; there is no such
		/// thing as half a shopkeeper.
		/// </summary>
		/// <param name="Citizens">Citizens available for work.</param>
		/// <param name="Demands">Staff each work requires, in priority order.</param>
		/// <param name="Thresholds">True where a work is all-or-nothing. Null means all scaled.</param>
		/// <returns>Parallel array of citizens actually assigned to each work.</returns>
		public static int[] AssignCrew(int Citizens, int[] Demands, bool[] Thresholds = null)
		{
			int[] result = new int[(Demands != null) ? Demands.Length : 0];
			if (Demands == null)
			{
				return result;
			}
			int remaining = (Citizens > 0) ? Citizens : 0;
			for (int i = 0; i < Demands.Length; i++)
			{
				int need = (Demands[i] > 0) ? Demands[i] : 0;
				if (need == 0)
				{
					continue;
				}
				bool threshold = Thresholds != null && i < Thresholds.Length && Thresholds[i];
				int give = (need <= remaining) ? need : (threshold ? 0 : remaining);
				result[i] = give;
				remaining -= give;
			}
			return result;
		}

		/// <summary>Fraction of full output a work produces with the crew it has, 0 to 100.</summary>
		public static int CrewEffectiveness(int Assigned, int Needed)
		{
			if (Needed <= 0)
			{
				return 100;
			}
			if (Assigned <= 0)
			{
				return 0;
			}
			if (Assigned >= Needed)
			{
				return 100;
			}
			return Assigned * 100 / Needed;
		}

		public static bool IsThresholdManning(string Manning)
		{
			return Manning == "threshold";
		}

		public static int UpkeepForElapsed(int Population, long ElapsedTicks)
		{
			if (Population <= 0 || ElapsedTicks <= 0)
			{
				return 0;
			}
			long days = ElapsedTicks / TicksPerDay;
			if (days > MaxUpkeepDaysCharged)
			{
				days = MaxUpkeepDaysCharged;
			}
			return (int)(days * (long)UpkeepDrams(Population));
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

		public static GrowthStage StageFor(int Population, int StorageCapacity)
		{
			if (Population >= 50 && StorageCapacity >= 1024)
			{
				return GrowthStage.City;
			}
			if (Population >= 25 && StorageCapacity >= 256)
			{
				return GrowthStage.Town;
			}
			if (Population >= 12 && StorageCapacity >= 64)
			{
				return GrowthStage.Village;
			}
			if (Population >= 5 && StorageCapacity >= 16)
			{
				return GrowthStage.Steading;
			}
			return GrowthStage.Camp;
		}

		public static readonly string[] Districts = new string[6] { "agrarian", "market", "craft", "shrine", "garrison", "academy" };

		public static readonly string[] DistrictNames = new string[6] { "vinelands", "bazaar", "forgeworks", "sacred ground", "watch", "scriptorium" };

		public static string DistrictName(string District)
		{
			for (int i = 0; i < Districts.Length; i++)
			{
				if (Districts[i] == District)
				{
					return DistrictNames[i];
				}
			}
			return District;
		}

		public enum ThirstOutcome
		{
			Sustained,
			Warned,
			Emigration,
			Withering
		}

		public static ThirstOutcome ResolveThirst(int DryStreak, GrowthStage Stage, int Population)
		{
			if (DryStreak <= 0)
			{
				return ThirstOutcome.Sustained;
			}
			if (DryStreak >= DryIntervalsToWither && Stage > GrowthStage.Camp)
			{
				return ThirstOutcome.Withering;
			}
			if (DryStreak >= DryIntervalsToEmigrate && Population > LoyalCoreSettlers)
			{
				return ThirstOutcome.Emigration;
			}
			return ThirstOutcome.Warned;
		}

		public static string ToThirdPerson(string Text, string FounderName)
		{
			if (string.IsNullOrEmpty(Text))
			{
				return Text;
			}
			string text = Text.Replace("your ", FounderName + "'s ").Replace("Your ", FounderName + "'s ");
			text = text.Replace("you poured", FounderName + " poured").Replace("You poured", FounderName + " poured");
			text = text.Replace("you ", FounderName + " ").Replace("You ", FounderName + " ");
			return text;
		}

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

		public class BuildEntry
		{
			public string Key;

			public string DisplayName;

			public string Blueprint;

			public int CostDrams;

			public long BuildTicks;

			public string Styles = "common";

			public string Category = "civic";

			public GrowthStage MinStage;

			public int Staff;

			public string Manning = "scaled";

			public string ShortName;

			public string Name => ShortName ?? DisplayName;
		}

		public static string StripParenthetical(string Text)
		{
			if (string.IsNullOrEmpty(Text))
			{
				return Text;
			}
			int num = Text.IndexOf(" (");
			if (num <= 0)
			{
				return Text;
			}
			return Text.Substring(0, num);
		}

		public static bool TryParseBuildAttributes(string Key, string DisplayName, string Blueprint, string Cost, string Ticks, string Styles, string Category, string MinStage, string Staff, string Manning, out BuildEntry Entry, out string Error)
		{
			Entry = null;
			Error = null;
			if (string.IsNullOrEmpty(Key) || string.IsNullOrEmpty(DisplayName) || string.IsNullOrEmpty(Blueprint))
			{
				Error = "building needs Key, DisplayName, and Blueprint";
				return false;
			}
			if (!int.TryParse(Cost, out var costDrams) || costDrams < 0)
			{
				Error = "building " + Key + " has a bad Cost";
				return false;
			}
			if (!long.TryParse(Ticks, out var buildTicks) || buildTicks <= 0)
			{
				Error = "building " + Key + " has a bad Ticks";
				return false;
			}
			int staff = 0;
			if (!string.IsNullOrEmpty(Staff) && (!int.TryParse(Staff, out staff) || staff < 0))
			{
				Error = "building " + Key + " has a bad Staff";
				return false;
			}
			GrowthStage minStage = GrowthStage.Camp;
			if (!string.IsNullOrEmpty(MinStage) && !System.Enum.TryParse<GrowthStage>(MinStage, ignoreCase: true, out minStage))
			{
				Error = "building " + Key + " has a bad MinStage";
				return false;
			}
			Entry = new BuildEntry
			{
				Key = Key,
				DisplayName = DisplayName,
				Blueprint = Blueprint,
				CostDrams = costDrams,
				BuildTicks = buildTicks,
				Styles = (string.IsNullOrEmpty(Styles) ? "common" : Styles),
				Category = (string.IsNullOrEmpty(Category) ? "civic" : Category),
				MinStage = minStage,
				Staff = staff,
				Manning = (string.IsNullOrEmpty(Manning) ? "scaled" : Manning),
				ShortName = StripParenthetical(DisplayName)
			};
			return true;
		}

		public static bool StyleAllows(string EntryStyles, string CityStyle)
		{
			if (string.IsNullOrEmpty(EntryStyles) || EntryStyles == "all")
			{
				return true;
			}
			string[] array = EntryStyles.Split(',');
			for (int i = 0; i < array.Length; i++)
			{
				string text = array[i].Trim();
				if (text == "all" || text == CityStyle)
				{
					return true;
				}
			}
			return false;
		}

		public const int DealTrickleStanding = 2;

		public class DealEntry
		{
			public string Key;

			public string DisplayName;

			public int MinStanding;

			public int IncomeDrams;

			public long IntervalTicks;

			public string CaravanBlueprint;
		}

		public static bool TryParseDealAttributes(string Key, string DisplayName, string MinStanding, string Income, string Interval, string Caravan, out DealEntry Entry, out string Error)
		{
			Entry = null;
			Error = null;
			if (string.IsNullOrEmpty(Key) || string.IsNullOrEmpty(DisplayName))
			{
				Error = "deal needs Key and DisplayName";
				return false;
			}
			if (!int.TryParse(MinStanding, out var minStanding))
			{
				Error = "deal " + Key + " has a bad MinStanding";
				return false;
			}
			if (!int.TryParse(Income, out var income) || income < 0)
			{
				Error = "deal " + Key + " has a bad Income";
				return false;
			}
			if (!long.TryParse(Interval, out var interval) || interval <= 0)
			{
				Error = "deal " + Key + " has a bad Interval";
				return false;
			}
			Entry = new DealEntry
			{
				Key = Key,
				DisplayName = DisplayName,
				MinStanding = minStanding,
				IncomeDrams = income,
				IntervalTicks = interval,
				CaravanBlueprint = (string.IsNullOrEmpty(Caravan) ? "DromadTrader1" : Caravan)
			};
			return true;
		}

		public const int RaidStandingThreshold = -250;

		public const int RaidTributeDrams = 6;

		public const int RaidPlunderDrams = 24;

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

		public static readonly string[] OutsiderTails = new string[6] { ", though the tellers disagree on the year", ", and the water in the telling is always sweeter", ", or so the version sold at the Stilt goes", ", which is a lie, or was one", ", and no two who tell it agree who was there", "" };

		public static string ComposeOutsider(string Text, int Roll)
		{
			int lead = Roll % OutsiderLeads.Length;
			if (lead < 0)
			{
				lead += OutsiderLeads.Length;
			}
			int tail = (Roll / OutsiderLeads.Length) % OutsiderTails.Length;
			if (tail < 0)
			{
				tail += OutsiderTails.Length;
			}
			return OutsiderLeads[lead] + Text + OutsiderTails[tail] + ".";
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

		/// <summary>
		/// Chebyshev adjacency between two zones in global zone coordinates. Engine-free so
		/// it stays unit-testable; callers inside the game should obtain coordinates from
		/// <c>XRL.World.ZoneID.Parse</c>, which also understands instanced zone IDs.
		/// </summary>
		/// <returns>True if the zones touch (including diagonally) on the same stratum, and
		/// are not the same zone.</returns>
		public static bool CoordsAdjacent(string WorldA, int GXA, int GYA, int ZA, string WorldB, int GXB, int GYB, int ZB)
		{
			if (WorldA != WorldB || ZA != ZB)
			{
				return false;
			}
			int dx = (GXA > GXB) ? (GXA - GXB) : (GXB - GXA);
			int dy = (GYA > GYB) ? (GYA - GYB) : (GYB - GYA);
			if (dx <= 1 && dy <= 1)
			{
				return dx + dy > 0;
			}
			return false;
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
