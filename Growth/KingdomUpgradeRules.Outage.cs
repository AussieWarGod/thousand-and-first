using System.Collections.Generic;

namespace ThousandAndFirst
{
	public static partial class KingdomUpgradeRules
	{
		/// <summary>
		/// Days of labour a build is, rounded UP: a build that runs into a day costs that day's
		/// output. This is the build's own duration &mdash; a property of the design, authored in
		/// <c>Ticks</c> &mdash; and never the clock. Nothing improves because time passed.
		/// </summary>
		public static int BuildDays(long BuildTicks)
		{
			if (BuildTicks <= 0L)
			{
				return 0;
			}
			long days = (BuildTicks + KingdomRules.TicksPerDay - 1L) / KingdomRules.TicksPerDay;
			return (days > int.MaxValue) ? int.MaxValue : (int)days;
		}

		/// <summary>
		/// What the settlement goes without while a work is being rebuilt: the sustained output
		/// the work contributes, for every day the labour takes. Denominated in drams, because one
		/// point of <c>water</c> carried is one dram a day sustained
		/// (<c>KingdomCatalogueRules</c>) and drams are what the reserve is counted in. A work
		/// that sustains nothing loses nothing and is never held.
		/// </summary>
		/// <param name="SupportPerDay">The <c>water</c> amount in the work's <c>Carries</c>.
		/// </param>
		/// <param name="BuildTicks">The improvement's own build time.</param>
		public static int OutputLost(int SupportPerDay, long BuildTicks)
		{
			if (SupportPerDay <= 0)
			{
				return 0;
			}
			long lost = (long)SupportPerDay * BuildDays(BuildTicks);
			return (lost > int.MaxValue) ? int.MaxValue : (int)lost;
		}

		/// <summary>
		/// Drams the stores would still hold above the reserve once the improvement is paid for
		/// AND the output it takes offline has been gone without for the whole build. Negative is
		/// the dip the founder is shown before they may force it.
		/// </summary>
		public static int AbsorptionMargin(int StoredWater, int Cost, int Reserve, int OutputLost)
		{
			return StoredWater - Cost - Reserve - OutputLost;
		}

		/// <summary>
		/// Whether the settlement can go without this work's output for as long as the work takes.
		/// Exactly covering it is covering it: the law asks that the reserve be kept, not that it
		/// be kept with something to spare.
		/// </summary>
		public static bool CoversOutage(int StoredWater, int Cost, int Reserve, int OutputLost)
		{
			return AbsorptionMargin(StoredWater, Cost, Reserve, OutputLost) >= 0;
		}

		/// <summary>
		/// Everything the absorption law needs to know about one work, measured by the engine half
		/// and judged here. A caller that has measured nothing passes <see cref="None"/> and gets
		/// exactly the behaviour that shipped before this law existed.
		/// </summary>
		public struct AbsorptionDemand
		{
			/// <summary>Whether this is housing, which is judged by displacement rather than by
			/// the output margin: its output IS the roof over the people being moved.</summary>
			public bool IsHousing;

			/// <summary>People the house holds &mdash; the <c>roof</c> it carries.</summary>
			public int Residents;

			/// <summary>Unused roof standing elsewhere in the settlement.</summary>
			public int SpareLodging;

			/// <summary>Best shelter rank among that spare lodging.</summary>
			public int OfferedShelter;

			/// <summary>Shelter rank of the roof the residents live under now.</summary>
			public int CurrentShelter;

			/// <summary>The <c>luxury</c> the design lifts by, which decides whose standard the
			/// lodging is judged against.</summary>
			public int LuxuryCarried;

			/// <summary>Sustained output the work contributes, in drams a day.</summary>
			public int SupportPerDay;

			/// <summary>The improvement's own build time, in ticks of labour.</summary>
			public long BuildTicks;

			/// <summary>Whether the stockpiles cover the improvement's material.</summary>
			public bool MaterialsInHand;

			/// <summary>Whether the settlement's craft and learning reach the successor.</summary>
			public bool CraftMet;

			/// <summary>
			/// Whether one of the residents would refuse the quarters on offer outright, judged by
			/// the quality-of-life vocabulary (<see cref="QuartersRefused"/>) rather than by
			/// shelter rank. Addendum 4 re-bases "tolerable" onto that vocabulary; the rank ladder
			/// goes on deciding how GOOD the lodging must be, and this decides whether it is
			/// lodging for these people at all.
			/// <para>
			/// Phrased as a refusal rather than as an acceptance so that <c>false</c> &mdash; the
			/// value of an unset struct and of <see cref="None"/> &mdash; means "nothing was
			/// measured, so nobody refused", which is what every caller that has not measured
			/// needs it to mean.
			/// </para>
			/// </summary>
			public bool QuartersRefused;

			/// <summary>A work nothing has been measured for: material and craft granted, no
			/// residents to move, and no output to go without. Every check below passes, so a
			/// caller that does not measure gets the pre-Addendum-3 behaviour exactly.</summary>
			public static AbsorptionDemand None
			{
				get
				{
					AbsorptionDemand none = default(AbsorptionDemand);
					none.MaterialsInHand = true;
					none.CraftMet = true;
					return none;
				}
			}
		}

		/// <summary>Whether a verdict is the settlement OFFERING a work rather than refusing it:
		/// everything is in hand and the settlement will not act alone. The only verdict the
		/// founder can override.</summary>
		public static bool IsOffer(UpgradeVerdict Verdict)
		{
			return Verdict == UpgradeVerdict.HeldOffer;
		}

	}
}
