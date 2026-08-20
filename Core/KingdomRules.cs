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

		/// <summary>
		/// Settlers to a commissioned bunk. One apiece made the City stage arithmetically
		/// impossible: fifty settlers needed fifty bunks plus four cisterns, against a
		/// forty-building cap, so the top of the ladder could never be reached at all.
		/// </summary>
		public const int BedsPerBunk = 4;

		public const int MaxCharters = 8;

		public const int MaxDedicatedVessels = 24;

		/// <summary>
		/// Larders &mdash; dedicated food stores &mdash; are capped separately from water vessels.
		/// A shared cap would let a founder spend the settlement's water accounting on chests,
		/// and the two are accounted by different people for different reasons.
		/// </summary>
		public const int MaxDedicatedLarders = 8;

		/// <summary>
		/// A ladder, not a bar. The Status report reads a settlement's dedicated food this way,
		/// and the shared meal (<see cref="MealCost"/>) spends against the same ladder, so what
		/// the Charter offers to serve is never a different number than what the founder can
		/// already see is stored.
		/// </summary>
		public enum PantryTier
		{
			Empty = 0,
			Scant = 1,
			Modest = 2,
			Ample = 3
		}

		/// <summary>Lower-case display name for each <see cref="PantryTier"/>, Qud style.</summary>
		public static readonly string[] PantryTierNames = new string[4] { "empty", "scant", "modest", "ample" };

		/// <summary>Food count at or above which the pantry reads as merely Scant.</summary>
		public const int PantryScantThreshold = 1;

		/// <summary>Food count at or above which the pantry reads as Modest.</summary>
		public const int PantryModestThreshold = 10;

		/// <summary>Food count at or above which the pantry reads as Ample.</summary>
		public const int PantryAmpleThreshold = 30;

		/// <summary>Coarse abundance tier for a raw food count, as counted into
		/// <c>KingdomSurvey.FoodStored</c>.</summary>
		public static PantryTier ClassifyPantry(int FoodCount)
		{
			if (FoodCount >= PantryAmpleThreshold)
			{
				return PantryTier.Ample;
			}
			if (FoodCount >= PantryModestThreshold)
			{
				return PantryTier.Modest;
			}
			if (FoodCount >= PantryScantThreshold)
			{
				return PantryTier.Scant;
			}
			return PantryTier.Empty;
		}

		/// <summary>
		/// Food a shared meal spends at the Scant tier: exactly <see cref="PantryScantThreshold"/>,
		/// the least a Scant larder can hold, so this cost is affordable the instant the tier is
		/// reached.
		/// </summary>
		public const int MealCostScant = 1;

		/// <summary>Food a shared meal spends at the Modest tier. Stays under
		/// <see cref="PantryModestThreshold"/>, for the same reason as <see cref="MealCostScant"/>.</summary>
		public const int MealCostModest = 8;

		/// <summary>Food a shared meal spends at the Ample tier. Stays under
		/// <see cref="PantryAmpleThreshold"/>, for the same reason as <see cref="MealCostScant"/>.</summary>
		public const int MealCostAmple = 20;

		/// <summary>
		/// What a shared meal costs from the dedicated larders at a given pantry reading. Zero at
		/// <see cref="PantryTier.Empty"/>: there is nothing to spend, and the Charter must never
		/// ask for it.
		/// </summary>
		public static int MealCost(PantryTier Tier)
		{
			switch (Tier)
			{
			case PantryTier.Scant:
				return MealCostScant;
			case PantryTier.Modest:
				return MealCostModest;
			case PantryTier.Ample:
				return MealCostAmple;
			default:
				return 0;
			}
		}

		/// <summary>
		/// Whether the dedicated larders can feed a shared meal at all. This is the one gate the
		/// Charter checks before offering the action: an empty larder must cost the founder
		/// nothing, so declining here is silent and free, exactly like standing on ground with no
		/// larder dedicated at all.
		/// </summary>
		/// <param name="FoodStored">Food counted in the dedicated larders this pass.</param>
		/// <param name="Population">Living settlers &mdash; there is no one to sit at an empty
		/// table.</param>
		public static bool CanHoldSharedMeal(int FoodStored, int Population)
		{
			return Population > 0 && ClassifyPantry(FoodStored) != PantryTier.Empty;
		}

		/// <summary>
		/// Food a shared meal actually spends against the given stock: the honest cost for the
		/// tier that stock reads as, clamped so a survey that has gone stale by the time the
		/// founder acts on it can never spend more than the larders hold.
		/// </summary>
		public static int MealServingsSpent(int FoodStored)
		{
			int cost = MealCost(ClassifyPantry(FoodStored));
			return (cost < FoodStored) ? cost : FoodStored;
		}

		/// <summary>
		/// What the founder calls the meal, graduated with the same tier the Status report shows.
		/// "Choose the tier honestly" means this and the larder reading never disagree: the size
		/// named here is always earned by the stock that paid for it.
		/// </summary>
		public static string MealSizeName(PantryTier Tier)
		{
			switch (Tier)
			{
			case PantryTier.Scant:
				return "a plain meal";
			case PantryTier.Modest:
				return "a hearty meal";
			case PantryTier.Ample:
				return "a feast";
			default:
				return null;
			}
		}

		/// <summary>What a settler says after eating, in their own mouth. Graduated with
		/// <see cref="MealSizeName"/> so the words never oversell what was actually on the
		/// table.</summary>
		public static string MealSpeech(PantryTier Tier)
		{
			switch (Tier)
			{
			case PantryTier.Scant:
				return "It wasn't much. But it was shared, and I noticed that.";
			case PantryTier.Modest:
				return "Nobody left hungry tonight. Some nights that is the whole victory.";
			case PantryTier.Ample:
				return "I don't remember the last time this table had too much on it. Thank you for that.";
			default:
				return null;
			}
		}

		public const int FoundingCostDrams = 8;

		public const int FetchDramsPerSettler = 2;

		public static readonly string[] Origins = new string[6] { "the salt marshes", "the desert canyons", "the hills", "the flower fields", "the rust wells", "the banana grove" };

		public const long TicksPerDay = 1200L;

		public const int MaxUpkeepDaysCharged = 3;

		/// <summary>Daily draw per settler, per hundred, by stage. A camp lives thin; a city
		/// drinks like a city. Prosperity-scaled COST is the loved half of the pattern - what
		/// players resent is prosperity-scaled THREAT, which is a different rule and is keyed off
		/// provocation elsewhere in this file.</summary>
		public static readonly int[] StageUpkeepPercent = new int[5] { 100, 120, 150, 180, 220 };

		/// <summary>
		/// Water the settlement drinks in a day. Scales with population AND with what the
		/// settlement has become: a City asks more of each settler than a Camp does, so growing
		/// is a decision with a bill attached rather than a free ratchet.
		/// </summary>
		public static int UpkeepDrams(int Population, GrowthStage Stage)
		{
			if (Population <= 0)
			{
				return 0;
			}
			int percent = StageUpkeepPercent[(int)Stage];
			return Population * percent / 100;
		}

		/// <summary>Camp-rate upkeep, for callers that have no stage to hand.</summary>
		public static int UpkeepDrams(int Population)
		{
			return UpkeepDrams(Population, GrowthStage.Camp);
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

		/// <summary>Percent of a raiding band turned back at the perimeter, per point of defence.</summary>
		public const int RaidTurnedBackPercentPerDefence = 6;

		/// <summary>
		/// Ceiling on how much of a band the walls turn back. Walls are a perimeter, not an
		/// answer: past this, someone still climbs over and has to be met.
		/// </summary>
		public const int MaxRaidersTurnedBackPercent = 60;

		/// <summary>
		/// How many raiders actually reach the settlement's ground. Defence is a perimeter
		/// rather than a damage number: crewed works and a garrison district turn part of the
		/// band back before any fighting starts, and a wall strong enough to
		/// <see cref="RaidOutcome.Repelled"/> turns all of it back. A band that does get through
		/// is never fewer than one &mdash; being well-walled is not the same as being spared.
		/// </summary>
		/// <param name="RaidSize">Raiders who set out.</param>
		/// <param name="Defence">Sum of crewed defensive works plus district bonus.</param>
		/// <param name="Outcome">Result of <see cref="ResolveRaid"/> for this defence and size.</param>
		public static int RaidingPartySize(int RaidSize, int Defence, RaidOutcome Outcome)
		{
			if (RaidSize <= 0 || Outcome == RaidOutcome.Repelled)
			{
				return 0;
			}
			if (Defence <= 0)
			{
				return RaidSize;
			}
			int turned = Defence * RaidTurnedBackPercentPerDefence;
			if (turned > MaxRaidersTurnedBackPercent)
			{
				turned = MaxRaidersTurnedBackPercent;
			}
			int through = RaidSize * (100 - turned) / 100;
			return (through < 1) ? 1 : through;
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

		/// <summary>
		/// Defence a wall gains from ground already broken into worked stone: exactly
		/// <see cref="RuinTerrainBlueprints"/>'s two blueprints, plus the wider "Ruins" region tag
		/// shared by <c>TerrainGritGate</c>, <c>TerrainRustWell</c>, <c>TerrainGolgotha</c>, and
		/// <c>TerrainRustedArchway</c> (StreamingAssets/Base/ObjectBlueprints/WorldTerrain.xml).
		/// The region match is deliberately wider than <see cref="IsRuinSite"/>'s: that gate
		/// decides whether the founding rite itself restores a ruin, and stays exact so an
		/// ordinary founding never mistakenly reads as one; this one only asks whether there is
		/// cut stone lying around to build with, which any of those sites offer.
		/// </summary>
		private static readonly string[] WorkedStoneGround = new string[1] { "Ruins" };

		/// <summary>
		/// Defence a wall gains from ground with rock underfoot that nobody has cut yet:
		/// <c>TerrainMountains</c>/<c>TerrainBethesdaSusa</c> (region "Mountains") and
		/// <c>TerrainHills</c>/<c>TerrainAsphaltMines</c>/<c>TerrainCraters</c> (region "Hills").
		/// Worth less than <see cref="WorkedStoneGround"/>: the stone is there, but the
		/// settlement's own masons have to quarry it first.
		/// </summary>
		private static readonly string[] QuarriableGround = new string[2] { "Mountains", "Hills" };

		/// <summary>Defence a wall built on <see cref="WorkedStoneGround"/> gains.</summary>
		public const int WorkedStoneWallBonus = 2;

		/// <summary>Defence a wall built on <see cref="QuarriableGround"/> gains.</summary>
		public const int QuarriableWallBonus = 1;

		/// <summary>
		/// Extra defence a wall draws from what the settlement's ground offers to build with.
		/// Reads the exact blueprint first and the region tag only if the blueprint names
		/// nothing recognised &mdash; the same order <see cref="StyleForSite"/> reads ground in,
		/// for the same reason: a ruin sitting inside some other region is still a ruin. Ground
		/// that offers nothing to quarry (a salt flat, a jungle floor, a spore mat) answers zero,
		/// never a negative number &mdash; this is a bonus ladder, and its floor is the wall a
		/// settlement builds today.
		/// </summary>
		/// <param name="TerrainBlueprint">Founding site's terrain blueprint
		/// (<see cref="KingdomSystem.FoundingTerrainBlueprint"/>), or null.</param>
		/// <param name="RegionName">Founding site's terrain region
		/// (<see cref="KingdomSystem.FoundingRegionName"/>), or null.</param>
		public static int GroundWallBonus(string TerrainBlueprint, string RegionName)
		{
			if (GroundMatches(TerrainBlueprint, WorkedStoneGround) || GroundMatches(RegionName, WorkedStoneGround))
			{
				return WorkedStoneWallBonus;
			}
			if (GroundMatches(TerrainBlueprint, QuarriableGround) || GroundMatches(RegionName, QuarriableGround))
			{
				return QuarriableWallBonus;
			}
			return 0;
		}

		/// <summary>As <see cref="ContainsAny"/>, but null- and empty-safe: <see cref="StyleForGround"/>
		/// guards this at its one call site, and this ladder needs the same guard at two.</summary>
		private static bool GroundMatches(string Ground, string[] Needles)
		{
			return !string.IsNullOrEmpty(Ground) && ContainsAny(Ground, Needles);
		}

		/// <summary>Defence a wall gains from a founder who knows the Tinkering skill at all
		/// &mdash; enough to keep a joint sound, even without a schematic for it.</summary>
		public const int TinkeringWallBonus = 1;

		/// <summary>
		/// Defence a wall gains, on top of <see cref="TinkeringWallBonus"/>, from a founder who
		/// has gone on to Tinker I (<c>Tinkering_Tinker1</c>): the point the skill actually
		/// teaches building from a schematic rather than only examining and repairing one.
		/// </summary>
		public const int AdvancedTinkeringWallBonus = 1;

		/// <summary>
		/// Extra defence a wall draws from what the founder standing at the commission actually
		/// knows how to build. Purely additive over the two skill checks, and never negative: an
		/// unskilled founder answers zero, the wall a settlement builds today.
		/// </summary>
		/// <param name="HasTinkering">Whether the founder holds the base Tinkering skill.</param>
		/// <param name="HasAdvancedTinkering">Whether the founder holds Tinker I.</param>
		public static int KnowledgeWallBonus(bool HasTinkering, bool HasAdvancedTinkering)
		{
			int bonus = 0;
			if (HasTinkering)
			{
				bonus += TinkeringWallBonus;
			}
			if (HasAdvancedTinkering)
			{
				bonus += AdvancedTinkeringWallBonus;
			}
			return bonus;
		}

		/// <summary>
		/// The defence a commissioned wall actually rises with: its design's own
		/// <see cref="BuildEntry.Defence"/>, plus what the ground offers to quarry
		/// (<see cref="GroundWallBonus"/>) and what the founder knows how to build
		/// (<see cref="KnowledgeWallBonus"/>). Purely additive and gated on the design already
		/// being defensive &mdash; a design with no <c>Defence</c> of its own (an ordinary
		/// building) never gains any, because this ladder makes walls stronger, it does not make
		/// anything into a wall.
		/// </summary>
		/// <param name="BaseDefence">The commissioned design's own <c>Defence</c> attribute.
		/// Zero or negative for anything that is not a defensive work.</param>
		/// <param name="TerrainBlueprint">Founding site's terrain blueprint, or null.</param>
		/// <param name="RegionName">Founding site's terrain region, or null.</param>
		/// <param name="HasTinkering">Whether the founder holds the base Tinkering skill.</param>
		/// <param name="HasAdvancedTinkering">Whether the founder holds Tinker I.</param>
		/// <returns><paramref name="BaseDefence"/> unchanged for a non-defensive design, poor
		/// ground, and an unskilled founder alike &mdash; the bonus only ever adds.</returns>
		public static int WallDefence(int BaseDefence, string TerrainBlueprint, string RegionName, bool HasTinkering, bool HasAdvancedTinkering)
		{
			if (BaseDefence <= 0)
			{
				return BaseDefence;
			}
			return BaseDefence + GroundWallBonus(TerrainBlueprint, RegionName) + KnowledgeWallBonus(HasTinkering, HasAdvancedTinkering);
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
			return PolicyUpkeepForElapsed(Population, ElapsedTicks, Stores, GrowthStage.Camp);
		}

		/// <summary>Whole-day upkeep after stores policy, at the settlement's own stage.</summary>
		public static int PolicyUpkeepForElapsed(int Population, long ElapsedTicks, StoresPolicy Stores, GrowthStage Stage)
		{
			return PolicyUpkeep(UpkeepDrams(Population, Stage), Stores) * HeartbeatDays(ElapsedTicks);
		}

		/// <summary>
		/// Drams the settlement's own people carry in from open water.
		/// <para>
		/// This is a RATE, and it has to be. It used to be charged once per zone activation with
		/// no clock at all, while upkeep was charged per elapsed day - so a founder could step out
		/// of the zone and back in to fetch again, without limit, and the water economy could
		/// never bind on any site near a pool. Fetch is now paid per day like everything else,
		/// forgiven past the same absence cap so time away is still never a debt.
		/// </para>
		/// <para>
		/// It is also drawn by HANDS, not by heads: only citizens not already crewing a work walk
		/// to the water. That is what makes staffing a real choice - every settler put on a mill
		/// is a settler not carrying a bucket.
		/// </para>
		/// </summary>
		/// <param name="Hands">Citizens free to fetch: population minus assigned crew.</param>
		/// <param name="OpenWater">Fresh water visible in pools.</param>
		/// <param name="StorageSpace">Room left in dedicated stores.</param>
		/// <param name="Days">Whole days since the last fetch, capped by the caller.</param>
		public static int FetchableDrams(int Hands, int OpenWater, int StorageSpace, int Days)
		{
			if (Days <= 0)
			{
				return 0;
			}
			int num = Hands * FetchDramsPerSettler * Days;
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
				num = num * DistrictMarketArrivalPercent / 100;
			}
			return num;
		}

		/// <summary>
		/// Defence a garrison district contributes for every claimed zone that declares it.
		/// Deliberately small: a watch is a militia turning out, not a wall, and
		/// <see cref="DefenceToRepel"/> still wants real works behind it.
		/// </summary>
		public const int DistrictGarrisonDefence = 2;

		/// <summary>Upkeep under an agrarian district, as a percent of the ordinary bill.</summary>
		public const int DistrictAgrarianUpkeepPercent = 90;

		/// <summary>Stock tiers a market district adds to what the growth stage already carries.</summary>
		public const int DistrictMarketShopTier = 1;

		/// <summary>Arrival interval under a market district, as a percent of the ordinary wait.</summary>
		public const int DistrictMarketArrivalPercent = 90;

		/// <summary>Build time under a craft district, as a percent of the ordinary time.</summary>
		public const int DistrictCraftBuildPercent = 80;

		/// <summary>Wait between petitions under a shrine district, as a percent of the ordinary wait.</summary>
		public const int DistrictShrinePetitionPercent = 75;

		/// <summary>
		/// Reachable outsider drift under an academy district, as a percent of the ordinary
		/// range: the scriptorium keeps the record, so fewer retellings wander from it.
		/// </summary>
		public const int DistrictAcademyDriftPercent = 50;

		/// <summary>A district with nothing to say about a quantity leaves that quantity whole.</summary>
		public const int DistrictNeutralPercent = 100;

		/// <summary>
		/// Hard floor under every aggregated district percent. Districts are registry data that
		/// third parties may extend (STANDARDS 6), so without a floor one careless entry could
		/// drive an interval, a bill, or a drift range to zero. Nothing a district does may cut a
		/// quantity by more than half.
		/// </summary>
		public const int DistrictPercentFloor = 50;

		/// <summary>Defence the watch adds where a garrison district is declared.</summary>
		/// <param name="District">A district key, or any unknown string. Null is tolerated.</param>
		/// <returns><see cref="DistrictGarrisonDefence"/> for "garrison", otherwise 0. A district
		/// never subtracts.</returns>
		public static int DistrictDefenceBonus(string District)
		{
			if (District == "garrison")
			{
				return DistrictGarrisonDefence;
			}
			return 0;
		}

		/// <summary>Upkeep the vinelands charge, as a percent of the ordinary bill.</summary>
		/// <param name="District">A district key, or any unknown string. Null is tolerated.</param>
		/// <returns><see cref="DistrictAgrarianUpkeepPercent"/> for "agrarian", otherwise
		/// <see cref="DistrictNeutralPercent"/>.</returns>
		public static int DistrictUpkeepPercent(string District)
		{
			if (District == "agrarian")
			{
				return DistrictAgrarianUpkeepPercent;
			}
			return DistrictNeutralPercent;
		}

		/// <summary>Stock tiers the bazaar adds on top of the growth stage's own tier.</summary>
		/// <param name="District">A district key, or any unknown string. Null is tolerated.</param>
		/// <returns><see cref="DistrictMarketShopTier"/> for "market", otherwise 0.</returns>
		public static int DistrictShopTierBonus(string District)
		{
			if (District == "market")
			{
				return DistrictMarketShopTier;
			}
			return 0;
		}

		/// <summary>Build time the forgeworks charge, as a percent of the ordinary time.</summary>
		/// <param name="District">A district key, or any unknown string. Null is tolerated.</param>
		/// <returns><see cref="DistrictCraftBuildPercent"/> for "craft", otherwise
		/// <see cref="DistrictNeutralPercent"/>.</returns>
		public static int DistrictBuildPercent(string District)
		{
			if (District == "craft")
			{
				return DistrictCraftBuildPercent;
			}
			return DistrictNeutralPercent;
		}

		/// <summary>Wait between petitions on sacred ground, as a percent of the ordinary wait.</summary>
		/// <param name="District">A district key, or any unknown string. Null is tolerated.</param>
		/// <returns><see cref="DistrictShrinePetitionPercent"/> for "shrine", otherwise
		/// <see cref="DistrictNeutralPercent"/>.</returns>
		public static int DistrictPetitionIntervalPercent(string District)
		{
			if (District == "shrine")
			{
				return DistrictShrinePetitionPercent;
			}
			return DistrictNeutralPercent;
		}

		/// <summary>Reachable outsider drift under the scriptorium, as a percent of the ordinary range.</summary>
		/// <param name="District">A district key, or any unknown string. Null is tolerated.</param>
		/// <returns><see cref="DistrictAcademyDriftPercent"/> for "academy", otherwise
		/// <see cref="DistrictNeutralPercent"/>.</returns>
		public static int DistrictDriftPercent(string District)
		{
			if (District == "academy")
			{
				return DistrictAcademyDriftPercent;
			}
			return DistrictNeutralPercent;
		}

		/// <summary>
		/// The aggregation law for district percent effects: <b>best wins, and nothing stacks.</b>
		/// The strongest single district of a kind sets the number for the whole settlement, so a
		/// second vinelands feeds the same city rather than feeding it twice, and the result is
		/// clamped into <see cref="DistrictPercentFloor"/>..<see cref="DistrictNeutralPercent"/>.
		/// <para>
		/// Stacking was rejected on both pillars. Multiplied percents make the sixth claimed zone
		/// worth more than the first and converge on zero, which turns a flavour choice into a
		/// mandatory one; and a settlement that declared its districts early would be punished for
		/// not declaring more of them. Additive aggregation is kept only for defence, where the
		/// fiction is bodies on a wall and more of them plainly is more.
		/// </para>
		/// </summary>
		/// <param name="Districts">District key of every claimed zone. Nulls, blanks, unknown
		/// keys, and duplicates are all tolerated and contribute nothing.</param>
		/// <param name="Effect">Percent this district charges for the quantity being aggregated.</param>
		/// <returns><see cref="DistrictNeutralPercent"/> for a null or empty sequence.</returns>
		private static int BestDistrictPercent(System.Collections.Generic.IEnumerable<string> Districts, System.Func<string, int> Effect)
		{
			int best = DistrictNeutralPercent;
			if (Districts != null)
			{
				foreach (string district in Districts)
				{
					int percent = Effect(district);
					if (percent < best)
					{
						best = percent;
					}
				}
			}
			if (best < DistrictPercentFloor)
			{
				return DistrictPercentFloor;
			}
			if (best > DistrictNeutralPercent)
			{
				return DistrictNeutralPercent;
			}
			return best;
		}

		/// <summary>
		/// Defence every garrison district in the realm musters. Additive by the law documented
		/// on <see cref="BestDistrictPercent"/>, and uncapped: claiming and declaring six zones is
		/// six zones of real investment, and the answer feeds <see cref="ResolveRaid"/> beside the
		/// works that must still be crewed.
		/// </summary>
		/// <param name="Districts">District key of every claimed zone; nulls and unknowns ignored.</param>
		/// <returns>0 for a null or empty sequence.</returns>
		public static int DistrictsDefenceBonus(System.Collections.Generic.IEnumerable<string> Districts)
		{
			int total = 0;
			if (Districts == null)
			{
				return total;
			}
			foreach (string district in Districts)
			{
				total += DistrictDefenceBonus(district);
			}
			return total;
		}

		/// <summary>Upkeep the realm's vinelands charge, by the law on <see cref="BestDistrictPercent"/>.</summary>
		/// <param name="Districts">District key of every claimed zone; nulls and unknowns ignored.</param>
		public static int DistrictsUpkeepPercent(System.Collections.Generic.IEnumerable<string> Districts)
		{
			return BestDistrictPercent(Districts, DistrictUpkeepPercent);
		}

		/// <summary>
		/// Stock tiers the realm's bazaars add. Best-wins like the percent effects: a second
		/// market is a second place to shop, not deeper stock in both.
		/// </summary>
		/// <param name="Districts">District key of every claimed zone; nulls and unknowns ignored.</param>
		/// <returns>0 for a null or empty sequence.</returns>
		public static int DistrictsShopTierBonus(System.Collections.Generic.IEnumerable<string> Districts)
		{
			int best = 0;
			if (Districts == null)
			{
				return best;
			}
			foreach (string district in Districts)
			{
				int bonus = DistrictShopTierBonus(district);
				if (bonus > best)
				{
					best = bonus;
				}
			}
			return best;
		}

		/// <summary>Build time the realm's forgeworks charge, by the law on <see cref="BestDistrictPercent"/>.</summary>
		/// <param name="Districts">District key of every claimed zone; nulls and unknowns ignored.</param>
		public static int DistrictsBuildPercent(System.Collections.Generic.IEnumerable<string> Districts)
		{
			return BestDistrictPercent(Districts, DistrictBuildPercent);
		}

		/// <summary>Petition wait under the realm's sacred ground, by the law on <see cref="BestDistrictPercent"/>.</summary>
		/// <param name="Districts">District key of every claimed zone; nulls and unknowns ignored.</param>
		public static int DistrictsPetitionIntervalPercent(System.Collections.Generic.IEnumerable<string> Districts)
		{
			return BestDistrictPercent(Districts, DistrictPetitionIntervalPercent);
		}

		/// <summary>Outsider drift under the realm's scriptoria, by the law on <see cref="BestDistrictPercent"/>.</summary>
		/// <param name="Districts">District key of every claimed zone; nulls and unknowns ignored.</param>
		public static int DistrictsDriftPercent(System.Collections.Generic.IEnumerable<string> Districts)
		{
			return BestDistrictPercent(Districts, DistrictDriftPercent);
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

		/// <summary>
		/// The city styles the rules themselves can resolve: a style themes what a settlement may
		/// build (<see cref="StyleAllows"/>) and how its founding reads on the page. Third-party
		/// building entries may name styles beyond these &mdash; <c>KingdomData.Styles</c> is the
		/// registry's live union &mdash; but only a style in this array is ever chosen by
		/// <see cref="StyleForSite"/>.
		/// </summary>
		public static readonly string[] Styles = new string[5] { "common", "verdant", "fungal", "gyre", "eater" };

		/// <summary>Whether a string names a style these rules resolve. Case-sensitive: style keys
		/// are data, not prose. Null and empty are not known styles.</summary>
		public static bool IsKnownStyle(string Style)
		{
			for (int i = 0; i < Styles.Length; i++)
			{
				if (Styles[i] == Style)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// The surface stratum. This is the engine's own number, not ours:
		/// <c>XRL.World.Zone.GetTerrainDisplayName</c> answers "the deep underground" for any
		/// <c>Z &gt; 10</c>, so 10 is the surface and larger is deeper.
		/// </summary>
		public const int SurfaceZLevel = 10;

		// Ground-to-style matching, against Caves of Qud 2.0.211.51,
		// StreamingAssets/Base/ObjectBlueprints/WorldTerrain.xml. A zone reports its ground two
		// ways: GetTerrainObject().Blueprint gives the exact blueprint ("TerrainSaltmarsh2",
		// "TerrainFungalOuterGw", "TerrainBaroqueRuins"), and GetTerrainRegion() gives that
		// blueprint's Terrain tag ("Saltmarsh", "Fungal", "Ruins", "Jungle"). Matching is by
		// substring because the game splits one region across dozens of variants, and the tag is
		// the variants' shared stem. A game update that renames these loses the match silently and
		// every site falls back to "common", which is the designed failure rather than a defect.
		private static readonly string[] FungalGround = new string[1] { "Fungal" };

		// TerrainRuins, TerrainBaroqueRuins, TerrainJoppaRuins by blueprint; TerrainGritGate and
		// TerrainRustWell carry Terrain="Ruins". "TheSpindle" and not "Spindle", or
		// TerrainMountainsSpindleShadow - a mountain that merely stands in the Spindle's shadow -
		// would read as the ancients' own chrome.
		private static readonly string[] EaterGround = new string[4] { "Ruins", "BethesdaSusa", "GritGate", "TheSpindle" };

		// The Moon Stair climbs to Brightsheol, where the Girsh are: chitin, bone, and sacrament.
		private static readonly string[] GyreGround = new string[2] { "Brightsheol", "MoonStair" };

		// TerrainWatervine and TerrainJoppaRuins both carry Terrain="Saltmarsh"; "Jungle" catches
		// TerrainJungle, TerrainDeepJungle, and Kyakukya, which carries Terrain="Jungle".
		private static readonly string[] VerdantGround = new string[5] { "Watervine", "Saltmarsh", "Flowerfields", "BananaGrove", "Jungle" };

		/// <summary>
		/// Resolves the ground a settlement stands on to a city style. The blueprint is read
		/// first and the region only if the blueprint says nothing, so a ruin in the marshes
		/// founds an "eater" city rather than a "verdant" one.
		/// <para>
		/// Below the surface only the two styles whose material is actually down there survive:
		/// spore-lit caverns and the ancients' works. Nobody thatches a roof with watervine in a
		/// cave, so a deep site of any other ground falls back to "common".
		/// </para>
		/// <para>
		/// The fallback is total. Unmapped ground, a renamed blueprint, null, and empty all answer
		/// "common", which is the one style every base building design allows.
		/// </para>
		/// </summary>
		/// <param name="TerrainBlueprint">The zone's terrain blueprint
		/// (<c>Zone.GetTerrainObject()?.Blueprint</c>), or null if it could not be read.</param>
		/// <param name="RegionName">The zone's terrain region (<c>Zone.GetTerrainRegion()</c>),
		/// or null if it could not be read.</param>
		/// <param name="ZLevel">The zone's stratum; see <see cref="SurfaceZLevel"/>.</param>
		/// <returns>A member of <see cref="Styles"/>. Never null.</returns>
		public static string StyleForSite(string TerrainBlueprint, string RegionName, int ZLevel)
		{
			string style = StyleForGround(TerrainBlueprint);
			if (style == null)
			{
				style = StyleForGround(RegionName);
			}
			if (style == null)
			{
				return "common";
			}
			if (ZLevel > SurfaceZLevel && style != "fungal" && style != "eater")
			{
				return "common";
			}
			return style;
		}

		/// <summary>
		/// First match wins, in the declared order. No two shipped grounds match two lists, but
		/// third-party terrain can: a "FungalRuins" is fungal, and that precedence is the contract.
		/// </summary>
		/// <returns>Null where the ground is unmapped, so the caller can try the other reading.</returns>
		private static string StyleForGround(string Ground)
		{
			if (string.IsNullOrEmpty(Ground))
			{
				return null;
			}
			if (ContainsAny(Ground, FungalGround))
			{
				return "fungal";
			}
			if (ContainsAny(Ground, EaterGround))
			{
				return "eater";
			}
			if (ContainsAny(Ground, GyreGround))
			{
				return "gyre";
			}
			if (ContainsAny(Ground, VerdantGround))
			{
				return "verdant";
			}
			return null;
		}

		private static bool ContainsAny(string Text, string[] Needles)
		{
			for (int i = 0; i < Needles.Length; i++)
			{
				if (Text.IndexOf(Needles[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Overland terrain blueprints that mark a world tile as an ordinary, unclaimed ruin
		/// (StreamingAssets/Base/ObjectBlueprints/WorldTerrain.xml: <c>TerrainRuins</c> and
		/// <c>TerrainBaroqueRuins</c>, the two objects the vanilla <c>Ruins</c> zone builder is
		/// wired to in <c>Worlds.xml</c>). Exact match, not the substring test
		/// <see cref="StyleForGround"/> uses: that match is deliberately loose because it is only
		/// choosing a building theme, and it is why Grit Gate, Golgotha, and Bethesda Susa all
		/// read as "eater" style even though none of them is a ruin to restore. This one gates an
		/// actual founding path, so a site is a ruin only if the ground the engine reports is
		/// exactly one of these two &mdash; <c>TerrainJoppaRuins</c>, despite its name, is a salt
		/// marsh (<c>Terrain="Saltmarsh"</c>) and never matches.
		/// </summary>
		public static readonly string[] RuinTerrainBlueprints = new string[2] { "TerrainRuins", "TerrainBaroqueRuins" };

		/// <summary>
		/// Whether the founding site is ground the world already built, rather than merely empty:
		/// a founding rite poured here restores instead of raising from nothing. See
		/// <see cref="RuinTerrainBlueprints"/> for exactly what counts and why.
		/// </summary>
		/// <param name="TerrainBlueprint">The founding zone's terrain blueprint
		/// (<c>Zone.GetTerrainObject()?.Blueprint</c>), or null if it could not be read.</param>
		/// <returns>False for null, empty, or any ground not in <see cref="RuinTerrainBlueprints"/>
		/// &mdash; an unresolvable site degrades to an ordinary founding, never a ruin one.</returns>
		public static bool IsRuinSite(string TerrainBlueprint)
		{
			if (string.IsNullOrEmpty(TerrainBlueprint))
			{
				return false;
			}
			for (int i = 0; i < RuinTerrainBlueprints.Length; i++)
			{
				if (RuinTerrainBlueprints[i] == TerrainBlueprint)
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Founder-facing clause naming how many already-standing structures a ruin founding
		/// credited to the settlement. Composes cleanly onto the end of the founding chronicle
		/// line whether or not the ruin happened to have anything worth keeping standing &mdash;
		/// most ruin decoration is rubble and puddles, not furniture, and that is not a failure of
		/// the rite.
		/// </summary>
		/// <param name="StructuresRestored">Count from the restoration pass. Zero or negative
		/// (defensive; a caller error, never produced by the pass itself) yields the empty
		/// clause.</param>
		/// <returns>A trailing clause starting with ", " or "" for nothing to report.</returns>
		public static string RuinRestorationClause(int StructuresRestored)
		{
			if (StructuresRestored <= 0)
			{
				return "";
			}
			if (StructuresRestored == 1)
			{
				return ", and one of its standing works is the settlement's now";
			}
			return ", and " + StructuresRestored + " of its standing works are the settlement's now";
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

		/// <summary>
		/// Every faction that will actually come for the stores. A faction is provokable only
		/// because <see cref="RaiderTableFor"/> can field a war party for it, so this array and
		/// those tables are one contract, indexed together: <c>KingdomRaids.FindProvokedFaction</c>
		/// skips any standing whose faction has no table, and a name here with no table would be a
		/// threat that never arrives.
		/// </summary>
		public static readonly string[] ProvokableFactions = new string[5] { "Snapjaws", "Baboons", "Goatfolk", "Cannibals", "Issachari" };

		// Parallel to ProvokableFactions, and verified against Caves of Qud 2.0.211.51: creature
		// names against StreamingAssets/Base/ObjectBlueprints/Creatures.xml, faction keys against
		// Factions.xml (the key is "Issachari"; "Issachari tribe" is only its DisplayName). A
		// misspelling on either side creates nothing and fails silently at raid time, so the two
		// arrays are walked in both directions by the tests.
		//
		// Raiders are drawn one per body at random, so a doubled entry is weight: each party is
		// two parts scavenger to one part fighter. Raids take drams and leave scars; they are not
		// meant to field a faction's best.
		private static readonly string[][] RaiderTables = new string[5][]
		{
			new string[3] { "Snapjaw Scavenger", "Snapjaw Scavenger", "Snapjaw Hunter" },
			new string[3] { "Baboon", "Baboon", "Hulking Baboon" },
			new string[3] { "Goatfolk Bully", "Goatfolk Bully", "Goatfolk Hornblower" },
			new string[3] { "Cannibal", "Cannibal", "Juicing Cannibal" },
			new string[3] { "Issachari Raider", "Issachari Raider", "Issachari Rifler" }
		};

		/// <summary>
		/// The war party a provoked faction sends. Blueprint names, drawn one per raider.
		/// </summary>
		/// <param name="FactionName">A faction key as it appears in the settlement's standings
		/// (Qud's <c>Factions.xml</c> Name, not its DisplayName). Null is tolerated.</param>
		/// <returns>The faction's table, or null where the faction cannot raid &mdash; which is
		/// how a standing is judged unprovokable. The returned array is shared: read it, never
		/// write it.</returns>
		public static string[] RaiderTableFor(string FactionName)
		{
			for (int i = 0; i < ProvokableFactions.Length; i++)
			{
				if (ProvokableFactions[i] == FactionName)
				{
					// A faction listed with no table is a wiring error, and the tests fail loudly
					// on it; inside a raid it degrades to "cannot raid" rather than throwing out
					// of the engine's event dispatch.
					if (i >= RaiderTables.Length)
					{
						return null;
					}
					return RaiderTables[i];
				}
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
		/// Chebyshev adjacency between two zones in global zone coordinates, with an optional
		/// vertical neighbour on top of it. Engine-free so it stays unit-testable; callers inside
		/// the game should obtain coordinates from <c>XRL.World.ZoneID.Parse</c>, which also
		/// understands instanced zone IDs.
		/// <para>
		/// <paramref name="IncludeVertical"/> defaults false so every existing caller of this
		/// overload &mdash; and every existing test of it &mdash; keeps exactly the answer it
		/// always got. Territorial claiming is the one caller that opts in
		/// (<c>KingdomFounding.ZonesAdjacent</c>), because a cellar directly below a held zone or
		/// a tower directly above it is the settlement's own ground, not a neighbour's.
		/// </para>
		/// </summary>
		/// <returns>True if the zones touch (including diagonally) on the same stratum and are
		/// not the same zone, or &mdash; only when <paramref name="IncludeVertical"/> is true and
		/// only in the same column (no diagonal-and-different-stratum case counts) &mdash; are
		/// exactly one stratum apart.</returns>
		public static bool CoordsAdjacent(string WorldA, int GXA, int GYA, int ZA, string WorldB, int GXB, int GYB, int ZB, bool IncludeVertical = false)
		{
			if (WorldA != WorldB)
			{
				return false;
			}
			int dx = (GXA > GXB) ? (GXA - GXB) : (GXB - GXA);
			int dy = (GYA > GYB) ? (GYA - GYB) : (GYB - GYA);
			if (ZA == ZB)
			{
				if (dx <= 1 && dy <= 1)
				{
					return dx + dy > 0;
				}
				return false;
			}
			if (!IncludeVertical)
			{
				return false;
			}
			int dz = (ZA > ZB) ? (ZA - ZB) : (ZB - ZA);
			return dx == 0 && dy == 0 && dz == 1;
		}

		public static bool ZonesAdjacent(string A, string B)
		{
			return ZonesAdjacent(A, B, IncludeVertical: false);
		}

		/// <summary>
		/// As <see cref="ZonesAdjacent(string, string)"/>, but a zone directly above or below the
		/// other &mdash; same world column, one stratum apart &mdash; counts as adjacent too when
		/// <paramref name="IncludeVertical"/> is true. A malformed ID on either side refuses the
		/// claim rather than guessing: see <see cref="TryParseZoneID"/>.
		/// </summary>
		public static bool ZonesAdjacent(string A, string B, bool IncludeVertical)
		{
			if (!TryParseZoneID(A, out var worldA, out var gxA, out var gyA, out var zA) || !TryParseZoneID(B, out var worldB, out var gxB, out var gyB, out var zB))
			{
				return false;
			}
			return CoordsAdjacent(worldA, gxA, gyA, zA, worldB, gxB, gyB, zB, IncludeVertical);
		}

		/// <summary>True if <paramref name="ZoneFaction"/> names a faction other than the
		/// kingdom's own and isn't empty &mdash; ground someone else already answers to. Null and
		/// empty read as unclaimed, which is what an ordinary wilderness zone's faction property
		/// actually is.</summary>
		public static bool GroundIsForeignFaction(string ZoneFaction, string KingdomFactionName)
		{
			return !string.IsNullOrEmpty(ZoneFaction) && ZoneFaction != KingdomFactionName;
		}

		/// <summary>
		/// What the founding rite should do about ground another faction already answers to,
		/// before any water is measured. <see cref="Unclaimed"/> is the overwhelming common case
		/// (empty wilderness, or the kingdom's own ground) and changes nothing about how founding
		/// already worked. The other two verdicts exist because <c>ClaimZone</c> used to write the
		/// kingdom's faction over whatever a zone already had unconditionally &mdash; a live
		/// hazard the ecosystem-compat audit found &mdash; and a village is not a lair: it is
		/// someone's home, asked into a covenant rather than annexed.
		/// </summary>
		public enum GroundClaimVerdict
		{
			Unclaimed,
			ForeignVillage,
			ForeignOther
		}

		/// <param name="ZoneFaction">The zone's <c>faction</c> property.</param>
		/// <param name="KingdomFactionName">The realm's own faction name, or null/empty if
		/// unfounded.</param>
		/// <param name="ZoneFactionIsVillage">Whether <paramref name="ZoneFaction"/> names a
		/// vanilla village faction (<c>Faction.GetIntProperty("Village") == 1</c>), read by the
		/// caller so this stays engine-free.</param>
		public static GroundClaimVerdict JudgeGroundFaction(string ZoneFaction, string KingdomFactionName, bool ZoneFactionIsVillage)
		{
			if (!GroundIsForeignFaction(ZoneFaction, KingdomFactionName))
			{
				return GroundClaimVerdict.Unclaimed;
			}
			return ZoneFactionIsVillage ? GroundClaimVerdict.ForeignVillage : GroundClaimVerdict.ForeignOther;
		}

		/// <summary>
		/// The reputation floor a village must already hold the founder at before the charter
		/// rite will ask it anything. 250 mirrors <c>XRL.Rules.RuleSettings.REPUTATION_LIKED</c>
		/// &mdash; liked, not yet loved: a real bar, reachable through ordinary play, restated here
		/// as a plain number so this file stays free of engine references.
		/// </summary>
		public const int VillageCharterReputationThreshold = 250;

		/// <summary>
		/// The standing a sealed charter raises a village to (see
		/// <c>KingdomFounding.CharterVillage</c>), and the floor <see cref="JudgeVillageCharter"/>
		/// reads as "already chartered" so asking again cannot spend water for nothing. 600
		/// mirrors <c>XRL.Rules.RuleSettings.REPUTATION_LOVED</c>.
		/// </summary>
		public const int VillageCharterSealedStanding = 600;

		/// <summary>Why the village charter rite may not proceed, or
		/// <see cref="Allowed"/>.</summary>
		public enum VillageCharterVerdict
		{
			Allowed,
			RealmNotFounded,
			AlreadyChartered,
			OpinionTooLow
		}

		/// <summary>
		/// Judges whether a village will hear the charter rite: it asks, so a realm has to exist
		/// to ask in the name of; a village already standing at
		/// <see cref="VillageCharterSealedStanding"/> has nothing left to ask for; and otherwise
		/// the village has to already trust the founder personally
		/// (<see cref="VillageCharterReputationThreshold"/>) &mdash; opinion of the founder, not of
		/// the realm, because the founder is who is standing there with the basin.
		/// </summary>
		/// <param name="AlreadyChartered">Whether the realm's standing with this village is
		/// already at or above <see cref="VillageCharterSealedStanding"/>.</param>
		public static VillageCharterVerdict JudgeVillageCharter(bool Founded, bool AlreadyChartered, int PlayerReputation)
		{
			if (!Founded)
			{
				return VillageCharterVerdict.RealmNotFounded;
			}
			if (AlreadyChartered)
			{
				return VillageCharterVerdict.AlreadyChartered;
			}
			if (PlayerReputation < VillageCharterReputationThreshold)
			{
				return VillageCharterVerdict.OpinionTooLow;
			}
			return VillageCharterVerdict.Allowed;
		}

		/// <summary>What the founder is told when the charter rite will not proceed. Written as
		/// the water-keepers would say it, not as a rule; empty for <see cref="VillageCharterVerdict.Allowed"/>.</summary>
		/// <param name="Verdict">The refusal.</param>
		/// <param name="VillageDisplayName">The village faction's display name.</param>
		public static string VillageCharterRefusal(VillageCharterVerdict Verdict, string VillageDisplayName)
		{
			string village = string.IsNullOrEmpty(VillageDisplayName) ? "this village" : ("{{C|" + VillageDisplayName + "}}");
			switch (Verdict)
			{
			case VillageCharterVerdict.RealmNotFounded:
				return "There is no realm yet to speak this covenant in the name of. Found your own ground first, then come back and ask.";
			case VillageCharterVerdict.AlreadyChartered:
				return "This covenant is already sealed between " + village + " and the realm. Nothing more is asked, or owed.";
			case VillageCharterVerdict.OpinionTooLow:
				return "The water-right with " + village + " is not yet strong enough for this to be asked. Earn it, and ask again.";
			default:
				return "";
			}
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
