namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{

		public const int MaxBankedCycles = 3;

		/// <summary>
		/// How many charter deliveries an absence earns. Missed cycles bank rather than
		/// vanish &mdash; absence accrues gifts &mdash; but only up to a cap, so a year away is
		/// not a windfall.
		/// </summary>
		/// <param name="Now">Current tick.</param>
		/// <param name="DueTick">When the next delivery was due.</param>
		/// <param name="IntervalTicks">Ticks between deliveries.</param>
		/// <returns>Deliveries owed, 0 if none are due yet.</returns>
		public static int BankedCycles(long Now, long DueTick, long IntervalTicks)
		{
			if (Now < DueTick || IntervalTicks <= 0)
			{
				return 0;
			}
			long cycles = (Now - DueTick) / IntervalTicks + 1;
			if (cycles > MaxBankedCycles)
			{
				cycles = MaxBankedCycles;
			}
			return (int)cycles;
		}

		public const long DeedMemoryTicks = 12000L;

		/// <summary>
		/// Phrases why a settler came. Growth that names the deed that caused it reads as a
		/// reward; growth that names nothing reads as a timer.
		/// </summary>
		/// <param name="Deed">The kingdom's most recent notable act, or null.</param>
		/// <param name="DeedAge">Ticks since that act.</param>
		/// <param name="Origin">Where the settler walked from.</param>
		public static string ArrivalReason(string Deed, long DeedAge, string Origin)
		{
			if (!string.IsNullOrEmpty(Deed) && DeedAge <= DeedMemoryTicks)
			{
				return "word of " + Deed + " reached " + Origin;
			}
			return "word of shared water reached " + Origin;
		}

		/// <summary>
		/// What the settlement drank over a stretch of elapsed time, uncapped (Addendum 8
		/// clause 1): people go on drinking whether or not anyone is watching.
		/// <para>
		/// This is a BILL, not a debt. Nothing here can go negative and nothing carries over -
		/// the caller draws it against real stores, which floor at zero, and what a settlement
		/// could not pay it simply did not drink. Saturates rather than wrapping, so a corrupt
		/// stamp asks for "more than everything" instead of quietly asking for a negative amount.
		/// </para>
		/// </summary>
		public static int UpkeepForElapsed(int Population, long ElapsedTicks)
		{
			if (Population <= 0 || ElapsedTicks <= 0)
			{
				return 0;
			}
			return SaturateToInt(ElapsedDays(ElapsedTicks) * (long)UpkeepDrams(Population));
		}

		/// <summary>Clamps a whole-day total to what an int can hold. A settlement's stores are
		/// int-denominated, so a bill past that ceiling and a bill at it draw exactly the same
		/// thing: everything there is.</summary>
		private static int SaturateToInt(long Value)
		{
			if (Value <= 0L)
			{
				return 0;
			}
			return (Value > int.MaxValue) ? int.MaxValue : (int)Value;
		}

		/// <summary>
		/// Whole-day upkeep after stores policy. Apply policy to the daily rate before
		/// multiplying so cost does not change with activation cadence.
		/// </summary>
		public static int PolicyUpkeepForElapsed(int Population, long ElapsedTicks, StoresPolicy Stores)
		{
			return PolicyUpkeepForElapsed(Population, ElapsedTicks, Stores, GrowthStage.Camp);
		}

		/// <summary>Whole-day upkeep after stores policy, at the settlement's own stage.
		/// Uncapped, for the reason <see cref="UpkeepForElapsed"/> is.</summary>
		public static int PolicyUpkeepForElapsed(int Population, long ElapsedTicks, StoresPolicy Stores, GrowthStage Stage)
		{
			return SaturateToInt(PolicyUpkeep(UpkeepDrams(Population, Stage), Stores) * (long)ElapsedDays(ElapsedTicks));
		}

		/// <summary>
		/// Drams the settlement's own people carry in from open water.
		/// <para>
		/// This is a RATE, and it has to be. It used to be charged once per zone activation with
		/// no clock at all, while upkeep was charged per elapsed day - so a founder could step out
		/// of the zone and back in to fetch again, without limit, and the water economy could
		/// never bind on any site near a pool. Fetch is now paid per day like everything else,
		/// over the same uncapped elapsed the upkeep bill is drawn against - the detail keeps
		/// walking to the river through an absence exactly as the settlement keeps drinking
		/// through it, so the two net honestly against each other instead of both stopping at
		/// three days.
		/// </para>
		/// <para>
		/// It is also drawn by HANDS, not by heads, and the hands are named: only settlers the
		/// founder put on the water detail walk to the water. That is what makes staffing a real
		/// choice - every settler on the detail is a settler not on a mill, and a settlement with
		/// an empty detail drinks only what the founder pours in.
		/// </para>
		/// </summary>
		/// <param name="Hands">Settlers the founder put on the water detail
		/// (<c>KingdomSystem.WaterCrew</c>), clamped to population by the caller. Zero means
		/// nobody walks to the river.</param>
		/// <param name="OpenWater">Fresh water visible in pools.</param>
		/// <param name="StorageSpace">Room left in dedicated stores.</param>
		/// <param name="Days">Whole days since the last fetch. Uncapped now that upkeep is: the
		/// detail walked to the river every one of those days, and what actually bounds the haul
		/// is the two real things beside it - how much open water is standing there and how much
		/// room is left in the stores.</param>
		public static int FetchableDrams(int Hands, int OpenWater, int StorageSpace, int Days)
		{
			if (Days <= 0 || Hands <= 0)
			{
				return 0;
			}
			long num = (long)Hands * FetchDramsPerSettler * Days;
			if (OpenWater < num)
			{
				num = OpenWater;
			}
			if (StorageSpace < num)
			{
				num = StorageSpace;
			}
			return (num > 0L) ? (int)num : 0;
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

	}
}
