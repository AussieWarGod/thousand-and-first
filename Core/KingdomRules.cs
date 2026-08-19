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

		/// <summary>
		/// Advances a heartbeat without losing a partial day. Time beyond the absence cap is
		/// forgiven by starting a fresh checkpoint at the current tick.
		/// </summary>
		public static long HeartbeatCheckpoint(long PreviousTick, long CurrentTick)
		{
			if (PreviousTick <= 0 || CurrentTick <= PreviousTick)
			{
				return CurrentTick;
			}
			long days = (CurrentTick - PreviousTick) / TicksPerDay;
			if (days <= 0)
			{
				return PreviousTick;
			}
			if (days > MaxUpkeepDaysCharged)
			{
				return CurrentTick;
			}
			return PreviousTick + days * TicksPerDay;
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

		/// <summary>
		/// Standing policies: the founder sets intent once and the settlement acts on it.
		/// Every policy trades one good thing for another, so there is no correct answer.
		/// </summary>
		public enum GatePolicy
		{
			Open,
			Guarded
		}

		public enum StoresPolicy
		{
			Plenty,
			Thrift
		}

		public static readonly string[] GatePolicyNames = new string[2] { "open gates", "guarded gates" };

		public static readonly string[] GatePolicyBlurbs = new string[2]
		{
			"Word travels and strangers are welcome. Settlers come sooner; so does trouble.",
			"The watch turns away what it does not know. Fewer settlers, fewer raids."
		};

		public static readonly string[] StoresPolicyNames = new string[2] { "open stores", "thrift" };

		public static readonly string[] StoresPolicyBlurbs = new string[2]
		{
			"Everyone drinks their fill. The settlement grows as fast as the water allows.",
			"The water-keepers ration. Upkeep falls by a quarter, and newcomers are made to wait."
		};

		/// <summary>Arrival interval after standing policy, in ticks.</summary>
		public static long PolicyInterval(long BaseInterval, GatePolicy Gate, StoresPolicy Stores)
		{
			long num = BaseInterval;
			if (Gate == GatePolicy.Guarded)
			{
				num = num * 140 / 100;
			}
			if (Stores == StoresPolicy.Thrift)
			{
				num = num * 130 / 100;
			}
			return num;
		}

		/// <summary>Daily upkeep after standing policy.</summary>
		public static int PolicyUpkeep(int BaseUpkeep, StoresPolicy Stores)
		{
			if (Stores != StoresPolicy.Thrift)
			{
				return BaseUpkeep;
			}
			return BaseUpkeep * 75 / 100;
		}

		/// <summary>Raid cooldown after standing policy; guarded gates buy quiet.</summary>
		public static long PolicyRaidCooldown(long BaseCooldown, GatePolicy Gate)
		{
			if (Gate != GatePolicy.Guarded)
			{
				return BaseCooldown;
			}
			return BaseCooldown * 160 / 100;
		}

		public const int TributeEscalationPercent = 50;

		/// <summary>
		/// What tribute costs now. A demand ignored once is a demand that has grown: deferring
		/// is a real choice with a real price, not a free delay.
		/// </summary>
		/// <param name="BaseDrams">The opening demand.</param>
		/// <param name="TimesDeferred">How many times this demand has been let pass.</param>
		public static int TributeDemand(int BaseDrams, int TimesDeferred)
		{
			int num = BaseDrams;
			for (int i = 0; i < TimesDeferred && i < 4; i++)
			{
				num = num * (100 + TributeEscalationPercent) / 100;
			}
			return num;
		}

		public const int DiplomacyStandingRequired = 250;

		/// <summary>
		/// Whether a standing offer of friendship can turn a raid aside without payment &mdash;
		/// the third exit. Kenshi's lesson: tribute that ignores earned goodwill feels wrong.
		/// </summary>
		public static bool CanTalkDown(int Standing, int TimesDeferred)
		{
			if (Standing >= DiplomacyStandingRequired)
			{
				return TimesDeferred == 0;
			}
			return false;
		}

		/// <summary>
		/// What a settler is asking the founder for. Every kind is generated from a condition
		/// the settlement is actually in, and every kind is met by a thing the player can see
		/// change &mdash; never a fetch quest invented from nothing.
		/// </summary>
		public enum PetitionKind
		{
			None,
			Thirst,
			Shelter,
			Craft,
			Peace,
			Memorial
		}

		public const long PetitionCooldownTicks = 3600L;

		public const long PetitionLifetimeTicks = 24000L;

		/// <summary>
		/// Chooses the petition the settlement would actually raise, in order of how badly it
		/// wants it. Returns None when the settlement is content &mdash; silence is a valid
		/// answer, and the reason there is no petition board.
		/// </summary>
		/// <param name="StoredWater">Drams in the dedicated stores.</param>
		/// <param name="Population">Living settlers.</param>
		/// <param name="Beds">Beds built.</param>
		/// <param name="IdleWorks">Works standing unmanned.</param>
		/// <param name="WorstStanding">Lowest standing with any faction that knows the kingdom.</param>
		/// <param name="HasShrine">Whether a place of remembrance exists.</param>
		/// <param name="Dead">Settlers lost since the settlement was founded.</param>
		public static PetitionKind ChoosePetition(int StoredWater, int Population, int Beds, int IdleWorks, int WorstStanding, bool HasShrine, int Dead)
		{
			if (Population <= 0)
			{
				return PetitionKind.None;
			}
			if (StoredWater < UpkeepDrams(Population) * 3)
			{
				return PetitionKind.Thirst;
			}
			if (Beds <= Population)
			{
				return PetitionKind.Shelter;
			}
			if (Dead > 0 && !HasShrine)
			{
				return PetitionKind.Memorial;
			}
			if (WorstStanding <= -250)
			{
				return PetitionKind.Peace;
			}
			if (IdleWorks > 0)
			{
				return PetitionKind.Craft;
			}
			return PetitionKind.None;
		}

		/// <summary>
		/// How a raid resolves against what the settlement built. Fortification is the fourth
		/// answer to a threat, beside paying, talking, and meeting it in the field &mdash; and
		/// unlike the other three it works while the founder is elsewhere.
		/// </summary>
		public enum RaidOutcome
		{
			Overrun,
			Plundered,
			Repelled
		}

		public const int DefenceToRepel = 12;

		/// <summary>
		/// Resolves a raid against the settlement's defence. Works only count while crewed, so
		/// a watchtower with nobody in it defends nothing.
		/// </summary>
		/// <param name="Defence">Sum of crewed defensive works.</param>
		/// <param name="RaidSize">Number of raiders.</param>
		public static RaidOutcome ResolveRaid(int Defence, int RaidSize)
		{
			int pressure = RaidSize * 3;
			if (Defence >= DefenceToRepel && Defence >= pressure)
			{
				return RaidOutcome.Repelled;
			}
			if (Defence <= 0)
			{
				return RaidOutcome.Overrun;
			}
			return RaidOutcome.Plundered;
		}

		/// <summary>
		/// Drams a raid carries off. Defence buys down the loss proportionally, and a repelled
		/// raid takes nothing &mdash; but walls never make a settlement free, only expensive.
		/// </summary>
		public static int RaidPlunder(int BaseDrams, int Defence, RaidOutcome Outcome)
		{
			if (Outcome == RaidOutcome.Repelled)
			{
				return 0;
			}
			if (Defence <= 0)
			{
				return BaseDrams;
			}
			int reduction = Defence * 6;
			if (reduction > 80)
			{
				reduction = 80;
			}
			return BaseDrams * (100 - reduction) / 100;
		}

		/// <summary>Percent chance a raid in the founder's absence costs a life.</summary>
		public static int RaidCasualtyChance(int Defence, RaidOutcome Outcome)
		{
			if (Outcome == RaidOutcome.Repelled)
			{
				return 0;
			}
			int num = 35 - Defence * 3;
			return (num > 5) ? num : 5;
		}

		/// <summary>The drams a thirst petition asks the stores to reach.</summary>
		public static int ThirstPetitionTarget(int Population)
		{
			int num = UpkeepDrams(Population) * 8;
			if (num < 16)
			{
				num = 16;
			}
			return num;
		}

		/// <summary>Whether an open petition has been answered by the settlement's own state.</summary>
		public static bool IsPetitionMet(PetitionKind Kind, int Target, int StoredWater, int Population, int Beds, int IdleWorks, int Standing, bool HasShrine)
		{
			switch (Kind)
			{
			case PetitionKind.Thirst:
				return StoredWater >= Target;
			case PetitionKind.Shelter:
				return Beds > Population;
			case PetitionKind.Memorial:
				return HasShrine;
			case PetitionKind.Peace:
				return Standing >= Target;
			case PetitionKind.Craft:
				return IdleWorks == 0;
			default:
				return false;
			}
		}

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

		/// <summary>
		/// Whole-day upkeep after stores policy. Apply policy to the daily rate before
		/// multiplying so cost does not change with activation cadence.
		/// </summary>
		public static int PolicyUpkeepForElapsed(int Population, long ElapsedTicks, StoresPolicy Stores)
		{
			return PolicyUpkeep(UpkeepDrams(Population), Stores) * HeartbeatDays(ElapsedTicks);
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

		/// <summary>
		/// What a settlement has become by the time a later life finds it.
		/// <para>
		/// A settlement never languishes because the founder is away. A living save is never
		/// decayed, visits reset no hidden clock, and wall-clock time carries no authority. What
		/// happens instead is that the end of a run &mdash; death, or a deliberate retirement
		/// &mdash; <b>seals</b> the settlement's condition as one immutable number, and a later
		/// save applies exactly one fictional intergenerational transition to it.
		/// </para>
		/// <para>
		/// So the question this answers is not "how long was it left" but "how well was it left,
		/// and how did the years between treat it".
		/// </para>
		/// </summary>
		public enum InheritedState
		{
			/// <summary>An autonomous polity carrying on without you. Not automatically yours.</summary>
			Held = 0,
			/// <summary>Thinner, some works derelict, stores low, but still lived in.</summary>
			Faded = 1,
			/// <summary>Intact and derelict. Everything standing, nobody home.</summary>
			Abandoned = 2,
			/// <summary>The street plan still legible under the collapse. Something else lives here.</summary>
			Ruins = 3
		}

		public static readonly string[] InheritedStateNames = new string[4] { "held", "faded", "abandoned", "ruined" };

		/// <summary>Ceiling on a sealed settlement's vigour, so the scale is readable as a percentage.</summary>
		public const int MaxSealedVigour = 100;

		public const int VigourPerStage = 10;

		public const int VigourFromPopulationCap = 25;

		public const int VigourFromDefenceCap = 20;

		public const int VigourFromWaterCap = 15;

		/// <summary>Drams of stored water per point of vigour; a full point cap is 120 drams.</summary>
		public const int VigourWaterPerPoint = 8;

		/// <summary>
		/// A settlement sealed while withering keeps half its vigour.
		/// <para>
		/// It was a quarter, which made the arithmetic collapse: the ceiling on a withered seal
		/// was 25, and no fate that low can reach <see cref="InheritedState.Faded"/>, so every
		/// withered settlement that had ever existed resolved to Abandoned or Ruins and the
		/// ladder lost a rung. Halving leaves a large withered city able to be found thinned but
		/// still lived in, which is the honest outcome &mdash; a thirsting town whose water
		/// returns after the founder is gone should be able to recover.
		/// </para>
		/// <para>
		/// It still cannot be found <see cref="InheritedState.Held"/>: half of the ceiling is 50
		/// and holding needs 55. That is an invariant of the arithmetic rather than a special
		/// case, and it is tested as one.
		/// </para>
		/// </summary>
		public const int WitheredVigourDivisor = 2;

		/// <summary>
		/// The one number that crosses runs: how well the settlement stood at the moment it was
		/// sealed, from 0 to <see cref="MaxSealedVigour"/>.
		/// <para>
		/// This is deliberately a summary and not a save. It carries no items, no charge, no
		/// water, no object state &mdash; only how much settlement there was to lose. Everything
		/// it is built from is already bounded, and each term is capped again here, so no amount
		/// of hoarding in a final run can buy a stronger inheritance than a well-run town.
		/// </para>
		/// <para>
		/// Each term only ever adds, so a founder can never improve the inheritance by tearing
		/// something down before the end.
		/// </para>
		/// </summary>
		public static int SealedVigour(GrowthStage Stage, int Population, int Defence, int StoredWater, bool Withered)
		{
			int population = (Population > 0) ? Population : 0;
			int defence = (Defence > 0) ? Defence : 0;
			int stored = (StoredWater > 0) ? StoredWater : 0;

			// GrowthStage is an enum over an int and nothing stops a caller casting an arbitrary
			// value into it. Out-of-domain contributes nothing rather than clamping: clamping high
			// values to City hands garbage the best case in the table, which is precisely the
			// outcome the guard exists to prevent. Unrecognised means unproven, and unproven earns
			// a Camp's standing.
			int stage = (int)Stage;
			int fromStage = (stage >= (int)GrowthStage.Camp && stage <= (int)GrowthStage.City) ? (stage * VigourPerStage) : 0;
			int fromPeople = (population < VigourFromPopulationCap) ? population : VigourFromPopulationCap;
			int fromWalls = (defence < VigourFromDefenceCap / 2) ? (defence * 2) : VigourFromDefenceCap;

			// Absolute stores, not days of supply. Measuring days divides water by population,
			// which made the seal fall as settlers arrived - so a founder could have improved
			// their own inheritance by letting people die before the end. Every term here must
			// only ever add, and the monotonicity test exists to keep it that way.
			int fromWater = stored / VigourWaterPerPoint;
			if (fromWater > VigourFromWaterCap)
			{
				fromWater = VigourFromWaterCap;
			}

			int vigour = fromStage + fromPeople + fromWalls + fromWater;
			if (Withered)
			{
				vigour /= WitheredVigourDivisor;
			}
			if (vigour > MaxSealedVigour)
			{
				vigour = MaxSealedVigour;
			}
			return (vigour > 0) ? vigour : 0;
		}

		/// <summary>
		/// The one draw of fortune between one life and the next, from 0 to 99.
		/// <para>
		/// Deterministic on purpose. A legacy draws its fate once, at promotion, and that fate is
		/// then fixed for good: retrying generation any number of times must reproduce it. It is
		/// also why this is a hash rather than the engine's random, which would both consume
		/// world-generation entropy and give the player a stream to reroll.
		/// </para>
		/// <para>
		/// Seed it <b>only</b> from immutable legacy data: lineage, origin, generation, revision.
		/// Never a destination's seed, the calendar, system time, or any stream a player can turn
		/// over again. An earlier version of this comment said to mix the legacy with the seed of
		/// wherever it landed, which would have handed back precisely the reroll it claimed to
		/// prevent.
		/// </para>
		/// <para>
		/// The consequence is that a legacy's fate is fixed the moment it is promoted, not when
		/// it is placed. It arrives in every world the same way, and retrying generation must
		/// reproduce it byte for byte.
		/// </para>
		/// </summary>
		public static int InterregnumRoll(long Seed)
		{
			ulong state = (ulong)Seed + 0x9E3779B97F4A7C15UL;
			state ^= state >> 30;
			state *= 0xBF58476D1CE4E5B9UL;
			state ^= state >> 27;
			state *= 0x94D049BB133111EBUL;
			state ^= state >> 31;
			return (int)(state % 100UL);
		}

		/// <summary>
		/// How much of the outcome the interregnum draw is allowed to move, in vigour points.
		/// <para>
		/// Bounded deliberately. The draw applied at full weight let one bad roll take a
		/// settlement sealed at perfect vigour all the way down to
		/// <see cref="InheritedState.Abandoned"/>, which makes the interregnum the author of the
		/// story and the founder's work irrelevant. At forty it moves the outcome by a band or
		/// two &mdash; enough that a seal is not its whole fate, not enough to overrule how the
		/// place was left.
		/// </para>
		/// <para>
		/// The scale divides by 99, not 100, so the worst draw costs exactly this many points
		/// rather than one short of it. A constant named forty that can only ever take
		/// thirty-nine is a small lie that every later reader has to re-derive.
		/// </para>
		/// </summary>
		public const int InterregnumSwing = 40;

		public const int HoldsAt = 55;

		public const int FadesAt = 35;

		public const int EmptiesAt = 15;

		/// <summary>
		/// Resolves the sealed condition and the interregnum draw into the state the settlement is
		/// found in.
		/// <para>
		/// Fortune shifts the outcome but never decides it alone: a settlement sealed at full
		/// vigour survives the worst draw there is, and a dying camp survives none of them.
		/// Between those two ends the draw is what makes the story.
		/// </para>
		/// <para>
		/// One floor overrides the arithmetic: a settlement sealed with nobody in it is never
		/// found inhabited. Withering needs no floor and takes no parameter here &mdash; it is
		/// already sealed into the vigour, which is capped low enough that
		/// <see cref="InheritedState.Held"/> is unreachable for a withered settlement as a
		/// property of the arithmetic.
		/// </para>
		/// </summary>
		/// <param name="Vigour">The sealed condition, from <see cref="SealedVigour"/>.</param>
		/// <param name="Roll">The interregnum draw, from <see cref="InterregnumRoll"/>.</param>
		/// <param name="Population">Population at sealing, for the empty-settlement floor.</param>
		public static InheritedState ResolveInheritedState(int Vigour, int Roll, int Population)
		{
			int vigour = (Vigour > 0) ? Vigour : 0;
			if (vigour > MaxSealedVigour)
			{
				vigour = MaxSealedVigour;
			}
			int roll = Roll;
			if (roll < 0)
			{
				roll = 0;
			}
			if (roll > 99)
			{
				roll = 99;
			}

			int fate = vigour - roll * InterregnumSwing / 99;
			InheritedState state;
			if (fate >= HoldsAt)
			{
				state = InheritedState.Held;
			}
			else if (fate >= FadesAt)
			{
				state = InheritedState.Faded;
			}
			else if (fate >= EmptiesAt)
			{
				state = InheritedState.Abandoned;
			}
			else
			{
				state = InheritedState.Ruins;
			}

			// No floor for withering. It reads like one is needed, but a withered seal is capped
			// at half the ceiling and holding needs more than that, so the arithmetic already
			// makes Held unreachable. An if-branch that can never fire is worse than no branch:
			// it claims a guarantee nobody is checking. The invariant is proved by test instead.
			if (Population <= 0 && state < InheritedState.Abandoned)
			{
				state = InheritedState.Abandoned;
			}
			return state;
		}

		/// <summary>
		/// How many people are still there to be found. Nobody remains at
		/// <see cref="InheritedState.Abandoned"/> or below; a faded settlement keeps half, and
		/// never rounds its last inhabitant away.
		/// <para>
		/// These are successors, not the old roll walking around again. The named roll crosses as
		/// history; the people who greet a later founder are their descendants.
		/// </para>
		/// </summary>
		public static int InheritedPopulation(int Population, InheritedState State)
		{
			// An unrecognised state must fail closed. Read naively, a cast-garbage negative is
			// neither >= Abandoned nor == Faded, and would fall through to handing back the whole
			// population of a settlement nobody has established still exists.
			if (!IsKnownState(State))
			{
				return 0;
			}
			if (Population <= 0 || State >= InheritedState.Abandoned)
			{
				return 0;
			}
			if (State == InheritedState.Faded)
			{
				return (Population > 1) ? (Population / 2) : 1;
			}
			return Population;
		}

		/// <summary>Whether a state is one this build defines. Anything else fails closed.</summary>
		public static bool IsKnownState(InheritedState State)
		{
			return State >= InheritedState.Held && State <= InheritedState.Ruins;
		}

		/// <summary>
		/// Whether <b>every</b> work still stands, so the settlement can be reoccupied as it was.
		/// <para>
		/// Deliberately named for all rather than any. This was <c>WorksSurvive</c>, which read as
		/// "anything survives" and flatly contradicted <see cref="StandingPercent"/> telling the
		/// caller that a quarter to three-fifths of a ruin is still up. Ask this when deciding
		/// whether to place the settlement intact; ask <see cref="StandingPercent"/> when deciding
		/// how much of it to place.
		/// </para>
		/// <para>
		/// <see cref="InheritedState.Abandoned"/> answers true: it is intact and derelict, empty
		/// rather than damaged, which is the whole point of it.
		/// </para>
		/// </summary>
		public static bool AllWorksSurvive(InheritedState State)
		{
			// Fails closed for the same reason as InheritedPopulation: a cast-garbage negative
			// compares as less than Ruins and would otherwise promise intact structures.
			return IsKnownState(State) && State < InheritedState.Ruins;
		}

		/// <summary>
		/// Fraction of a ruined settlement's structures left standing, as a percentage.
		/// <para>
		/// Ruination is applied by a deterministic transform of our own onto a fresh
		/// reconstruction canvas. It must never be delegated to the engine's <c>Ruiner</c>, which
		/// detonates explosions across a live zone: that would damage whatever else the new world
		/// had already put there, and would make <see cref="InheritedState.Abandoned"/> destructive
		/// when the whole promise of that state is that everything is still standing.
		/// </para>
		/// <para>
		/// The floor is what makes a ruin readable as a place rather than as rubble.
		/// </para>
		/// </summary>
		public const int RuinStandingFloorPercent = 25;

		public const int RuinStandingCeilingPercent = 60;

		public static int StandingPercent(InheritedState State, int Roll)
		{
			if (!IsKnownState(State))
			{
				return RuinStandingFloorPercent;
			}
			if (State < InheritedState.Ruins)
			{
				return 100;
			}

			// Roll is adversity: a high draw is a hard interregnum. Standing must therefore fall
			// as it rises. The first version ran the other way and left the worst-treated ruins
			// the most intact, which is backwards on its face and was caught in review.
			// Clamp rather than modulo - wrapping would turn an out-of-range 150 into a mild 50.
			int roll = Roll;
			if (roll < 0)
			{
				roll = 0;
			}
			if (roll > 99)
			{
				roll = 99;
			}
			return RuinStandingCeilingPercent - roll * (RuinStandingCeilingPercent - RuinStandingFloorPercent) / 99;
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

			public int Defence;

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

		public static bool TryParseBuildAttributes(string Key, string DisplayName, string Blueprint, string Cost, string Ticks, string Styles, string Category, string MinStage, string Staff, string Manning, string Defence, out BuildEntry Entry, out string Error)
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
			int defence = 0;
			if (!string.IsNullOrEmpty(Defence) && (!int.TryParse(Defence, out defence) || defence < 0))
			{
				Error = "building " + Key + " has a bad Defence";
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
				Defence = defence,
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
