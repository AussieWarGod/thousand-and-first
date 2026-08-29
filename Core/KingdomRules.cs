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

	public static partial class KingdomRules
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
			long scaled = (long)RepDelta * SpilloverPercent(Stage) / 100L;
			if (scaled > int.MaxValue) return int.MaxValue;
			if (scaled < int.MinValue) return int.MinValue;
			return (int)scaled;
		}

		public const int DramsPerArrival = 2;

		public const int MaxArrivalsPerVisit = 3;

		public const int DryIntervalsToEmigrate = 2;

		public const int DryIntervalsToWither = 3;

		public const int LoyalCoreSettlers = 2;

		public const int MaxPopulation = 60;

		/// <summary>Retired flat cap. Binary compatibility only; no live caller reads it.</summary>
		[System.Obsolete("Retired before public release; use MaxBuildingsForStage(stage).", true)]
		public const int MaxBuildings = 40;

		/// <summary>
		/// How much a settlement may build on one zone, by what it has become.
		/// <para>
		/// The old flat 40 was never chosen: the project's own fact-check records that the number
		/// could not be justified, and it was quietly setting the game's ceiling. It measured the
		/// wrong thing, too - a zone is 80 by 25, so forty objects is two per cent of the ground,
		/// and a settlement that spans zones got the same allowance on each one regardless of
		/// what it was.
		/// </para>
		/// <para>
		/// The real limits are the ones already in play: what a building costs in water, whether
		/// there are hands to man it, and whether there is empty ground to put it on. This is a
		/// safety rail against pathological object counts, not a design constraint, so it is set
		/// where a settlement stops being readable rather than where it stops being cheap.
		/// </para>
		/// <para>
		/// It counts plots, never furniture: contents of a plot (a cask rack, a bunk, a bench) are
		/// populated the way vanilla populates a hut and are not separately capped &mdash;
		/// <c>KingdomPlot2.CountBuilt</c> skips them.
		/// </para>
		/// </summary>
		public static int MaxBuildingsForStage(GrowthStage Stage)
		{
			switch (Stage)
			{
			case GrowthStage.Camp:
				return 40;
			case GrowthStage.Steading:
				return 70;
			case GrowthStage.Village:
				return 110;
			case GrowthStage.Town:
				return 160;
			default:
				return 220;
			}
		}

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
		/// True worst civic-container envelope on one zone. A City may commission 220 plot roots;
		/// every root may itself be one vessel/larder. A founder can also dedicate the 24+8 manual
		/// allowances before those roots are raised. Plot furnishings are not separate civic stores:
		/// current authored components never carried the marks, and legacy population furnishings
		/// are normalized back to personal vessels by <c>KingdomSurvey</c>.
		/// </summary>
		public static readonly int MaxCivicContainersPerZone =
			MaxBuildingsForStage(GrowthStage.City) + MaxDedicatedVessels + MaxDedicatedLarders;

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


	}
}
