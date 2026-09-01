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
			if (Crops <= 0)
			{
				return 0;
			}
			long gain = (long)Crops * (PreserveMultiple - 1);
			return (gain >= int.MaxValue) ? int.MaxValue : (int)gain;
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
			return (int)(((long)Gain + per - 1L) / per);
		}

		/// <summary>
		/// How much raw crop stock an operating mill may consider for its disclosed physical
		/// transformation.
		/// <para>
		/// Food is not billed as passive upkeep. Therefore no invisible household reserve is
		/// subtracted here: the caller still proves an operating mill, names the crop, and can take
		/// no more real crop items than the larders contain. The legacy population parameter stays
		/// in the public signature for source compatibility and has no economic meaning.
		/// </para>
		/// </summary>
		/// <param name="FoodStored">Servings in the dedicated larders right now.</param>
		/// <param name="Population">Living settlers.</param>
		/// <returns>Servings the mill may take, never negative.</returns>
		public static int MillableStock(int FoodStored, int Population)
		{
			return (FoodStored > 0) ? FoodStored : 0;
		}

		// --- Where the settlement keeps its food -------------------------------------------
		//
		// Storage capacity is physical and lives on the blueprint. A larder's tag is occupancy
		// metadata read by KingdomSurvey, so a third party's pantry declares its size like a
		// cistern does. Unlike water, food capacity never contributes population support.

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
		public static readonly string[] CivicLarderBlueprints = new string[3]
		{
			"r_KingdomLarder", "r_KingdomGranary", "r_KingdomRealmGranary"
		};

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

		// Legacy API seam. Pre-alpha builds mirrored water with an abstract daily ration and
		// hand-to-mouth foraging. The ruling record rejects that model: food stays as physical items
		// until a player-authorized meal, recipe, industry, or trade transaction names an exact debit.
		// Keep these symbols neutral so old source and lifecycle rows still decode without ever
		// charging a save on upgrade.

		/// <summary>Legacy compatibility projection. Passive daily food upkeep is retired.</summary>
		public static int RationsPerDay(int Population)
		{
			return 0;
		}

		/// <summary>Legacy compatibility projection. Elapsed time never creates a food bill.</summary>
		public static int RationsForElapsed(int Population, long ElapsedTicks)
		{
			return 0;
		}

		/// <summary>Legacy compatibility constant. Abstract foraging credit is retired.</summary>
		public const int ForageRationsPerHand = 0;

		/// <summary>Legacy compatibility constant. Abstract foraging credit is retired.</summary>
		public const int MaxForagedRationsPerDay = 0;

		/// <summary>Legacy compatibility projection. Only physical gathered items count as food.</summary>
		public static int ForagedRations(int Hands, int Days)
		{
			return 0;
		}
	}
}
