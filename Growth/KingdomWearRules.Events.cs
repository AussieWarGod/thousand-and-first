using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomWearRules
	{
		// ==================================================================================
		// The three causes. Named, never anonymous: every damage event says which of the three
		// it was, both in the moment (the message this file composes) and later (LastCause on
		// the part), because "the mill is worn" is a different sentence from "the mill was
		// raided" and the founder is owed the difference.
		// ==================================================================================

		public enum WearCause
		{
			/// <summary>Sentinel for "nothing has ever damaged this work". Never itself the
			/// subject of a message.</summary>
			None = 0,

			/// <summary>Raiders who got past the wall.</summary>
			Raid = 1,

			/// <summary>Crewed at full stretch for many consecutive attended passes.</summary>
			HardRunning = 2,

			/// <summary>Certified salvage, acting up on use.</summary>
			TemperamentalTech = 3,
		}

		/// <summary>The clause a damage line names the cause by.</summary>
		public static string CauseVerb(WearCause Cause)
		{
			switch (Cause)
			{
			case WearCause.Raid:
				return "broken into by raiders who got past the wall";
			case WearCause.HardRunning:
				return "run past what it was built to bear, crewed at full stretch too long";
			case WearCause.TemperamentalTech:
				return "acted up under its own certified hands";
			default:
				return "damaged";
			}
		}

		public const int RaidDamageIncrement = 15;

		public const int HardRunDamageIncrement = 10;

		public const int TemperamentalDamageIncrement = 20;

		/// <summary>The wear one instance of a cause adds, for
		/// <see cref="KingdomMaterialRules.AddWear"/> to clamp at
		/// <see cref="KingdomMaterialRules.MaxWearPercent"/>. Never called for
		/// <see cref="WearCause.None"/>; returns zero for it rather than throwing, so a defensive
		/// caller reads "no damage" instead of crashing on a sentinel.</summary>
		public static int IncrementFor(WearCause Cause)
		{
			switch (Cause)
			{
			case WearCause.Raid:
				return RaidDamageIncrement;
			case WearCause.HardRunning:
				return HardRunDamageIncrement;
			case WearCause.TemperamentalTech:
				return TemperamentalDamageIncrement;
			default:
				return 0;
			}
		}

		/// <summary>
		/// What a work actually manages this pass: this pass's crew stretch (0-100, from
		/// headcount and capability) reduced again by how worn the work itself is
		/// (<see cref="KingdomMaterialRules.ConditionPercent"/>). Two independent reasons a work
		/// can run under full, combined by multiplying their fractions rather than by picking the
		/// worse of the two: a fully-crewed but half-wrecked mill and a well-kept mill crewed at
		/// half strength are not the same settlement, and should not read as one.
		/// </summary>
		public static int CombinedEffectiveness(int CrewStretch, int Wear)
		{
			int stretch = (CrewStretch < 0) ? 0 : ((CrewStretch > 100) ? 100 : CrewStretch);
			return stretch * KingdomMaterialRules.ConditionPercent(Wear) / 100;
		}

		/// <summary>
		/// What ANY finished work is worth this pass, crewed or not (Addendum 10(b): "wear reduces
		/// every work's level contribution, staffed or not"). One rule with two arms, and the
		/// second arm is the ruling:
		/// <list type="bullet">
		/// <item>a work that asks for crew runs at <see cref="CombinedEffectiveness"/> &mdash; the
		/// staffing pass's stretch, reduced again by its own condition;</item>
		/// <item>a work that asks for nobody runs at its CONDITION alone. A cistern holds water
		/// whoever is home, and a holed cistern holds less of it.</item>
		/// </list>
		/// <para>
		/// The arm that was wrong was the second: a staffless work was handed a flat 100, so a
		/// ruined reservoir carried its full twenty-six drams and only crewed works ever felt
		/// damage. Both arms return 100 for a sound work, which is what makes this a strict
		/// refinement of the ternary it replaces rather than a new tax.
		/// </para>
		/// </summary>
		/// <param name="StaffNeeded">The design's <c>KingdomStaffNeeded</c>. Zero or less means
		/// the work asks for nobody.</param>
		/// <param name="CrewStretch">The staffing pass's own 0-100 stamp, read BEFORE any wear is
		/// folded into it. Ignored entirely for a staffless work, which never carries one.</param>
		/// <param name="Wear">The work's own wear, 0 to
		/// <see cref="KingdomMaterialRules.MaxWearPercent"/>.</param>
		public static int WorkEffectiveness(int StaffNeeded, int CrewStretch, int Wear)
		{
			return (StaffNeeded > 0)
				? CombinedEffectiveness(CrewStretch, Wear)
				: KingdomMaterialRules.ConditionPercent(Wear);
		}

		// ==================================================================================
		// Hard running: a streak of consecutive full-stretch ATTENDED passes, re-eligible for a
		// draw once per whole further streak of the same length &mdash; the same
		// milestone-and-reroll shape KingdomConversionRules.AtMilestone/Milestone use, so a
		// question the kernel already answered "no" for this stretch is not asked again until a
		// whole further stretch has been run.
		// ==================================================================================

		/// <summary>Consecutive full-stretch attended passes before a hard-running work is
		/// eligible to be asked whether it wore. Generous: an occasional day at full output is
		/// ordinary work, not the mill run mercilessly.</summary>
		public const int HardRunStreakThreshold = 8;

		/// <summary>Whether this streak has reached a milestone and the draw is owed.</summary>
		public static bool AtHardRunMilestone(int Streak)
		{
			return Streak >= HardRunStreakThreshold;
		}

		/// <summary>Which milestone this streak stands at &mdash; the kernel ordinal the draw is
		/// keyed on, exactly as <see cref="KingdomConversionRules.Milestone"/> keys conversion.
		/// Zero below the first milestone, where nothing calls it.</summary>
		public static ulong HardRunMilestone(int Streak)
		{
			return (Streak < HardRunStreakThreshold) ? 0uL : (ulong)(Streak / HardRunStreakThreshold);
		}

		public const int HardRunChancePercent = 15;

		public const int TemperamentalChancePercent = 4;

		public const int RaidDamageChancePercent = 35;

		/// <summary>Works one raid may damage, at most. Bounded: a large raid still damages at
		/// most a couple of works, never sweeps the settlement.</summary>
		public const int MaxWorksDamagedPerRaid = 2;

		/// <summary>How many works one raid may damage, before the per-work chance is even asked.
		/// Grows gently with the size of the party that got past the wall, and never past
		/// <see cref="MaxWorksDamagedPerRaid"/>.</summary>
		public static int WorksToDamage(int RaidersThrough)
		{
			if (RaidersThrough <= 0)
			{
				return 0;
			}
			int count = 1 + RaidersThrough / 4;
			return (count > MaxWorksDamagedPerRaid) ? MaxWorksDamagedPerRaid : count;
		}
	}
}
