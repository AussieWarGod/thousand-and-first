namespace ThousandAndFirst
{
	public static partial class KingdomRules
	{
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

	}
}
