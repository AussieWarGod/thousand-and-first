using System.Collections.Generic;
using System.Text;

namespace ThousandAndFirst
{
	public static partial class KingdomMaterialRules
	{
		// --- Clearance: what removal earns, and what it costs in crew ------------------------

		/// <summary>Effort one cell costs before its hardness is read, indexed by
		/// <see cref="KingdomStanding"/>.</summary>
		public static readonly int[] StandingEffort = new int[StandingCount] { 1, 4, 8, 20, 30, 40, 40 };

		/// <summary>Units one cell yields, indexed by <see cref="KingdomStanding"/>. Bare ground
		/// yields nothing on its own &mdash; its mud comes from <see cref="GroundMud"/>, which is
		/// counted once for the whole rect rather than once per empty cell.</summary>
		public static readonly int[] StandingYield = new int[StandingCount] { 0, 1, 2, 3, 3, 2, 2 };

		/// <summary>Cells of turned ground that give up one load of mud. Mud is the spoil of
		/// digging, not a thing that stood anywhere, so it is counted against the rect rather
		/// than against what was removed from it.</summary>
		public const int MudPerCells = 4;

		/// <summary>Effort one settler removes in one day of clearing.</summary>
		public const int EffortPerHandPerDay = 10;

		/// <summary>
		/// Settlers who can usefully swing at one clearance at once. A bounded consequence per
		/// visit: a large settlement clears faster than a small one, but never instantly, and a
		/// founder who returns after a long absence still finds work left to watch.
		/// </summary>
		public const int MaxClearingHands = 6;

		/// <summary>What a material costs the settlement in effort to bring down, by hardness.
		/// The hardness bands are read off vanilla's own <c>Hitpoints</c> stat &mdash; canvas 15,
		/// a plain wall 100, shale 200, limestone 1000, marble 6000, granite 26000.</summary>
		public static int HardnessPercent(int Hitpoints)
		{
			if (Hitpoints <= 50)
			{
				return 60;
			}
			if (Hitpoints <= 200)
			{
				return 100;
			}
			if (Hitpoints <= 1000)
			{
				return 140;
			}
			if (Hitpoints <= 6000)
			{
				return 200;
			}
			return 300;
		}

		/// <summary>
		/// Effort one cell costs to clear. Bare ground is a fixed nominal cost regardless of what
		/// hardness is passed, because nothing stands on it to be hard; everything else scales
		/// with <see cref="HardnessPercent"/> and never falls below one, so no cell is ever free.
		/// </summary>
		/// <param name="Standing">What stands on the cell.</param>
		/// <param name="Hitpoints">The standing object's base Hitpoints, or 0 for bare ground.</param>
		public static int ClearanceEffort(KingdomStanding Standing, int Hitpoints)
		{
			int index = (int)Standing;
			if (index < 0 || index >= StandingCount)
			{
				return 0;
			}
			if (Standing == KingdomStanding.Nothing)
			{
				return StandingEffort[index];
			}
			int effort = StandingEffort[index] * HardnessPercent(Hitpoints) / 100;
			return (effort < 1) ? 1 : effort;
		}

		/// <summary>The material one cleared cell yields up.</summary>
		public static KingdomMaterial YieldMaterial(KingdomStanding Standing)
		{
			switch (Standing)
			{
			case KingdomStanding.Brush:
				return KingdomMaterial.Brush;
			case KingdomStanding.Tree:
				return KingdomMaterial.Timber;
			case KingdomStanding.Rubble:
			case KingdomStanding.Rock:
				return KingdomMaterial.Stone;
			case KingdomStanding.Ruin:
				return KingdomMaterial.Scrap;
			case KingdomStanding.MarbleSeam:
				return KingdomMaterial.Marble;
			default:
				return KingdomMaterial.Mud;
			}
		}

		/// <summary>Units one cleared cell yields of <see cref="YieldMaterial"/>.</summary>
		public static int YieldUnits(KingdomStanding Standing)
		{
			int index = (int)Standing;
			if (index < 0 || index >= StandingCount)
			{
				return 0;
			}
			return StandingYield[index];
		}

		/// <summary>Loads of mud a rect of the given size gives up as the ground is turned over.
		/// Zero for a rect small enough that nobody would call it digging.</summary>
		public static int GroundMud(int CellsCleared)
		{
			if (CellsCleared <= 0)
			{
				return 0;
			}
			return CellsCleared / MudPerCells;
		}

		/// <summary>
		/// Settlers free to clear: everyone the settlement has who is not already carrying water
		/// or crewing a work. Hands are spent once, and this is the third and last claim on them.
		/// </summary>
		/// <param name="Population">The settlement's people.</param>
		/// <param name="AssignedCrew">Citizens the staffing pass already spent, water detail
		/// included &mdash; <c>KingdomSystem.AssignedCrew</c> counts both.</param>
		public static int FreeHands(int Population, int AssignedCrew)
		{
			int free = Population - AssignedCrew;
			return (free > 0) ? free : 0;
		}

		/// <summary>
		/// Days one pair of hands would need to work off the given effort, rounded up, so a job
		/// worth any effort at all is never reported as taking no time. The unit the founder is
		/// quoted in: effort points mean nothing to anybody standing in a field.
		/// </summary>
		public static int DaysForOneHand(int Effort)
		{
			if (Effort <= 0)
			{
				return 0;
			}
			return (Effort + EffortPerHandPerDay - 1) / EffortPerHandPerDay;
		}

		/// <summary>
		/// Effort a gang removes over the days since it was last worked: hands times days times
		/// <see cref="EffortPerHandPerDay"/>, with the gang clamped at
		/// <see cref="MaxClearingHands"/>.
		/// <para>
		/// Days come from <c>KingdomRules.ElapsedDays</c>, uncapped (Addendum 8 clause 1): a
		/// staked plot is dug through an absence exactly as it is dug through a fortnight of
		/// visits. The bound is HANDS, not the calendar &mdash; zero free hands is zero effort
		/// however long the stretch, which is clause 2, and is why every caller reads its hands
		/// gate before it spends its days.
		/// </para>
		/// </summary>
		public static int EffortWorked(int FreeHands, int Days)
		{
			if (FreeHands <= 0 || Days <= 0)
			{
				return 0;
			}
			long hands = (FreeHands > MaxClearingHands) ? MaxClearingHands : FreeHands;
			// Saturating rather than wrapping: an uncapped day count on a stamp nobody has
			// resolved since the world was made would otherwise come back negative, and negative
			// effort would ADD to the work left rather than take from it.
			long effort = hands * Days * EffortPerHandPerDay;
			return (effort > int.MaxValue) ? int.MaxValue : (int)effort;
		}

		// --- Striking: what comes down, and what comes back ----------------------------------

		/// <summary>Effort even the flimsiest building costs to take down honestly.</summary>
		public const int StrikeBaseEffort = 20;

		/// <summary>Extra effort per unit of material the building was raised from.</summary>
		public const int StrikeEffortPerUnit = 3;

		/// <summary>Drams of the original commission that add one more point of strike effort.
		/// A costly building is a large building, whether or not it was built of anything.</summary>
		public const int StrikeDramsPerEffort = 10;

		/// <summary>
		/// Share of a building's material cost its striking returns. Half, and deliberately less
		/// than all: taking a thing down carefully is still taking it down, and nothing about
		/// striking is a refund. No water is ever returned.
		/// </summary>
		public const int StrikeSalvagePercent = 50;

		/// <summary>
		/// Effort taking a building down costs, from what it was made of and what it cost to
		/// commission. Negative inputs are clamped rather than paying the settlement to demolish.
		/// </summary>
		public static int StrikeEffort(int MaterialUnits, int CostDrams)
		{
			int units = (MaterialUnits > 0) ? MaterialUnits : 0;
			int drams = (CostDrams > 0) ? CostDrams : 0;
			return StrikeBaseEffort + units * StrikeEffortPerUnit + drams / StrikeDramsPerEffort;
		}

		/// <summary>
		/// What striking a building of the given material cost returns to the stockpiles. A
		/// design that cost no materials returns none, and says so rather than inventing timber
		/// out of a water-only hut.
		/// </summary>
		public static KingdomMaterialTally StrikeSalvage(KingdomMaterialTally Cost)
		{
			if (Cost == null)
			{
				return new KingdomMaterialTally();
			}
			return Cost.Scaled(StrikeSalvagePercent);
		}

	}
}
