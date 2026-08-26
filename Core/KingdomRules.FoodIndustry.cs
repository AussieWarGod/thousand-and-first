namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{
		// --- Industry: what the mill does with a harvest ------------------------------------
		//
		// Addendum 11(b)'s other half - food "used by industry to produce things" - and per
		// VANILLA-PRODUCTION-TRUTH 3 the whole transformation surface in the game is four parts.
		// The one that fits a settlement's harvest is `Mill` (D/XRL/World/Parts/Mill.cs:9), whose
		// blank-target path runs Campfire.PerformPreserve (:82-101) - which is exactly what
		// vanilla's own `Millstone` does: Vinewafer in, Vinewafer Sheaf x3 out, automatically,
		// while mechanically powered (B/…/Furniture.xml:1015-1043).

		/// <summary>
		/// Preserved units one raw crop binds into. Vanilla's own <c>Vinewafer</c> &rarr;
		/// <c>Vinewafer Sheaf</c> figure (<c>B/ObjectBlueprints/Foods.xml:424</c>), and it is the
		/// LEAST of the three vanilla numbers our crops carry (starapple gives five, plump
		/// mushroom ten), so the settlement never books more than the thinnest preserve in the
		/// game actually gives.
		/// <para>
		/// <b>Flat across styles on purpose</b>, for exactly the reason
		/// <c>KingdomCropRules.CropDaysForStyle</c> is flat: a design's <c>Carries</c> is one
		/// number and the ground a settlement is founded on is not chosen by the founder, so a
		/// mill that ground faster in a marsh than on a flower field would make the same building
		/// worth different amounts for a reason nobody picked and nothing states.
		/// </para>
		/// </summary>
		public const int PreserveMultiple = 3;

		/// <summary>
		/// Raw crops one mill's day of grinding takes off the larder shelves. Two, and the number
		/// is not free: two crops at <see cref="PreserveMultiple"/> is six staples back, a net of
		/// four servings, which is exactly the <c>food:4</c> the grinding mill declares in
		/// <c>KingdomBuildings.xml</c>. <c>_notes/balance-sim.py</c> asserts that identity, so a
		/// retune of either end is caught at once.
		/// </summary>
		public const int MillCropsPerDay = 2;

		/// <summary>Servings a day's grinding adds to the settlement: what came back minus what
		/// went in. Never negative &mdash; <see cref="PreserveMultiple"/> is at least one.</summary>
		public static int MilledGain(int Crops)
		{
			return (Crops <= 0) ? 0 : (Crops * (PreserveMultiple - 1));
		}

		/// <summary>Raw crops a mill must grind to gain <paramref name="Gain"/> servings, rounded
		/// up so the gain is never quietly short. The inverse of <see cref="MilledGain"/>.</summary>
		public static int CropsForGain(int Gain)
		{
			if (Gain <= 0 || PreserveMultiple <= 1)
			{
				return 0;
			}
			int per = PreserveMultiple - 1;
			return (Gain + per - 1) / per;
		}

		/// <summary>
		/// How much of the larder the mill is allowed to touch: everything above one day's
		/// rations for everybody living here.
		/// <para>
		/// <b>Industry never eats before the residents do.</b> The pass draws the day's rations
		/// first and grinds afterwards, and even then it grinds only what is left above the next
		/// day's bill &mdash; so a settlement can never wake up hungry because its mill was busy.
		/// This is the food half of the standing order the water lane already keeps, where the
		/// plot spends only what upkeep and arrivals left in the stores.
		/// </para>
		/// </summary>
		/// <param name="FoodStored">Servings in the dedicated larders right now.</param>
		/// <param name="Population">Living settlers.</param>
		/// <returns>Servings the mill may take, never negative.</returns>
		public static int MillableStock(int FoodStored, int Population)
		{
			int reserve = RationsPerDay(Population);
			int free = FoodStored - reserve;
			return (free > 0) ? free : 0;
		}

		// --- Where the settlement keeps its food -------------------------------------------
		//
		// Water's capacity is physical and lives on the blueprint (LiquidVolume MaxVolume), never
		// in the catalogue: a design's Carries says what it adds to the sustainable LEVEL, and
		// how much the vessel holds is a fact about the vessel. Food is given exactly the same
		// shape - a tag on the blueprint, read by KingdomSurvey - so that a third party's own
		// pantry declares its size the same way a third party's own cistern does.

		/// <summary>
		/// Blueprint tag naming how much food a dedicated container holds, mirroring
		/// <c>LiquidVolume MaxVolume</c> on the water side. Absent reads as
		/// <see cref="DefaultLarderCapacity"/>, which is what an ordinary chest the founder
		/// dedicated by hand gets.
		/// </summary>
		public const string LarderCapacityTag = "r_KingdomLarderCapacity";

		/// <summary>What a container with no declared capacity holds. A chest the founder walked
		/// up to and dedicated, sized like a small vessel rather than like a granary.</summary>
		public const int DefaultLarderCapacity = 32;

		/// <summary>
		/// A declared larder capacity, read back safely. Zero, absent, or negative is a container
		/// that never said, and gets <see cref="DefaultLarderCapacity"/> &mdash; never zero,
		/// because a dedicated larder that can hold nothing is a silent black hole for a harvest
		/// and there is no way for the founder to see it.
		/// </summary>
		public static int LarderCapacity(int Declared)
		{
			return (Declared > 0) ? Declared : DefaultLarderCapacity;
		}

		/// <summary>
		/// The blueprints a finished, commissioned work dedicates itself to the settlement's food
		/// stores on completion &mdash; STANDARDS 7's "commissioned storage auto-flags", which is
		/// the food half of the same clause that auto-flags a commissioned cask rack.
		/// <para>
		/// Named rather than inferred, exactly as <c>r_KingdomScaffold.LarderBlueprint</c> named
		/// the first of them: "has an Inventory and no LiquidVolume" would sweep up the charging
		/// post, which carries a Container/Inventory pair and is not a pantry.
		/// </para>
		/// </summary>
		public static readonly string[] CivicLarderBlueprints = new string[2] { "r_KingdomLarder", "r_KingdomGranary" };

		/// <summary>Whether a finished work's blueprint is one the settlement keeps its food in.</summary>
		public static bool IsCivicLarderBlueprint(string Blueprint)
		{
			if (string.IsNullOrEmpty(Blueprint))
			{
				return false;
			}
			for (int i = 0; i < CivicLarderBlueprints.Length; i++)
			{
				if (CivicLarderBlueprints[i] == Blueprint)
				{
					return true;
				}
			}
			return false;
		}

		// ==================================================================================
		// FOOD AS A FLOW (Wave B). The water lane's mirror, function for function, and where it
		// deliberately parts company said out loud rather than left to be inferred.
		//
		//   water                                  food
		//   -----                                  ----
		//   UpkeepDrams(pop, stage)                RationsPerDay(pop)          -- NO stage term
		//   PolicyUpkeepForElapsed(...)            RationsForElapsed(...)      -- no policy term
		//   FetchableDrams(hands, pool, room, d)   ForagedRations(hands, days) -- ceiling, not a pool
		//   ResolveThirst(streak, stage, pop)      ResolveHunger(streak, stage, pop)
		//   DryIntervalsToEmigrate / ToWither      HungryIntervalsToEmigrate / ToFamine
		//
		// THE TWO DIVERGENCES, AND WHY.
		//
		// (1) NO STAGE RATE. Water is billed 100/120/150/180/220 per hundred by stage and the
		//     catalogue's `water` Carries are divided back out by that same percentage
		//     (KingdomSubsidenceRules.LevelFromWater), so a cistern carrying eight settlers at
		//     camp carries three in a city. Food is not, and the catalogue says so in its own
		//     voice: "a dinner and a bed are both counted in people, and neither is divided by
		//     the settlement's own thirst the way a dram is" (KingdomBuildings.xml, the big-plot
		//     note). So one point of `food` is one settler fed for one day, flat, at every rung.
		//     That is what makes the whole lane check out on its face: a settlement standing at
		//     its own equilibrium eats exactly what its fields make, because the food arm of
		//     KingdomCatalogueRules.Equilibrium IS the daily ration bill.
		//
		//     Putting a stage rate on food would not be a tuning knob, it would be a rewrite of
		//     every food figure in the catalogue and of the level arithmetic that reads them.
		//
		// (2) NO STORES POLICY. Thrift discounts the daily draw by a quarter and its own blurb
		//     names what it is doing - "the water-keepers ration". It is a water lever, tuned
		//     against the water economy, and letting it silently halve the food bill as well
		//     would make one menu choice strictly better than the other for a reason nobody
		//     wrote. The agrarian district's upkeep discount is left on the water side for the
		//     same reason: it is already spent there, and spending it twice is a double count.
		// ==================================================================================

		/// <summary>
		/// Food the settlement eats in a day: one ration per settler, at every rung.
		/// <para>
		/// Deliberately NOT the mirror of <see cref="UpkeepDrams(int, GrowthStage)"/>'s stage
		/// scaling &mdash; see the divergence note above. The flatness is load-bearing rather
		/// than lazy: it is what makes a settlement standing at its own supported level exactly
		/// food-neutral, because <c>KingdomCatalogueRules.Equilibrium</c>'s food arm is
		/// denominated in settlers fed and is handed through undivided.
		/// </para>
		/// </summary>
		/// <param name="Population">Living settlers. Zero or fewer eats nothing.</param>
		public static int RationsPerDay(int Population)
		{
			return (Population > 0) ? Population : 0;
		}

		/// <summary>
		/// What the settlement ate over a stretch of elapsed time, uncapped (Addendum 8 clause 1)
		/// exactly as <see cref="PolicyUpkeepForElapsed"/> is: people go on eating whether or not
		/// anyone is watching.
		/// <para>
		/// A BILL and not a debt, for the same reason the water one is: the caller draws it
		/// against what is actually foraged and stored, both of which floor at zero, and what a
		/// settlement could not pay it simply did not eat. Saturates rather than wrapping.
		/// </para>
		/// </summary>
		public static int RationsForElapsed(int Population, long ElapsedTicks)
		{
			if (Population <= 0 || ElapsedTicks <= 0)
			{
				return 0;
			}
			return SaturateToInt(ElapsedDays(ElapsedTicks) * (long)RationsPerDay(Population));
		}

		/// <summary>
		/// Rations one pair of free hands brings in off the land in a day, before the ceiling.
		/// The same figure as <see cref="FetchDramsPerSettler"/>, and for the same reason: a
		/// settler spending a day on the settlement's own supply brings back a day's worth for
		/// two.
		/// </summary>
		public const int ForageRationsPerHand = 2;

		/// <summary>
		/// The most the ground around a settlement will give up in a day, however many people
		/// walk it. This is foraging's <c>OpenWater</c> &mdash; the real thing that bounds the
		/// haul &mdash; except that the wild does not care how many baskets you bring, so the
		/// bound is a flat ceiling rather than a pool that drains.
		/// <para>
		/// Four, deliberately: the same figure as <c>KingdomCatalogueRules.FloorLevel</c> and the
		/// same figure as the population ceiling of the Camp rung (<see cref="StageFor"/> opens
		/// Steading at five). So a Camp feeds itself off the parasang and nothing above a Camp
		/// does &mdash; which is exactly the shape the water lane already has, where a camp's
		/// bill is covered by putting half its people on the detail and a Town's is not. Pinned
		/// against both figures by test rather than by a code dependency, because
		/// <c>KingdomCatalogueRules</c> reads this file and not the other way round.
		/// </para>
		/// </summary>
		public const int MaxForagedRationsPerDay = 4;

		/// <summary>
		/// Rations the settlement's free hands bring in off the land over a stretch of days.
		/// Foraging is hand-to-mouth: the caller pays the day's ration bill from this FIRST and
		/// only then draws the shortfall out of the larders, which is why a camp with no larder
		/// dedicated is not a camp that starves.
		/// <para>
		/// The rate is clamped BEFORE the days are multiplied, exactly as
		/// <c>PolicyUpkeep</c> is applied to the daily rate before
		/// <see cref="PolicyUpkeepForElapsed"/> multiplies it out: what the ground gives is a
		/// daily fact, and a long absence is more days of it and never a bigger day.
		/// </para>
		/// </summary>
		/// <param name="Hands">Settlers on neither the water detail nor a work
		/// (<c>KingdomMaterialRules.FreeHands</c>). Hands are spent once here as everywhere:
		/// a settler turning a mill is not also out on the ridge with a basket.</param>
		/// <param name="Days">Whole world days since the last reckoning. Uncapped, for the reason
		/// fetch is.</param>
		public static int ForagedRations(int Hands, int Days)
		{
			if (Hands <= 0 || Days <= 0)
			{
				return 0;
			}
			long rate = (long)Hands * ForageRationsPerHand;
			if (rate > MaxForagedRationsPerDay)
			{
				rate = MaxForagedRationsPerDay;
			}
			return SaturateToInt(rate * Days);
		}
	}
}
