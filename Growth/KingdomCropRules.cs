namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free rules for the settlement's growing plot: what state it cycles through, what
	/// each transition costs or yields, how much of a long absence one visit is allowed to
	/// resolve, and what the ground under it grows. The engine-coupled part that stores and
	/// drives this state is <see cref="ThousandAndFirst.r_KingdomPlot"/>, in the same folder.
	/// </summary>
	public static class KingdomCropRules
	{
		/// <summary>
		/// A plot's place in its own cycle. Dormant is also its resting state whenever it lacks
		/// the water to plant &mdash; there is no separate "starved" state, because a plot with
		/// no water simply waits here, exactly as it would the moment after the settlement was
		/// founded and had never been planted at all.
		/// </summary>
		public enum PlotStage
		{
			Dormant = 0,
			Growing = 1,
			Ripe = 2
		}

		/// <summary>Drams a planting draws from the settlement's dedicated stores, once, at the
		/// moment a plot leaves <see cref="PlotStage.Dormant"/>.</summary>
		public const int PlantWaterCostDrams = 3;

		/// <summary>
		/// Ticks a planted crop spends <see cref="PlotStage.Growing"/> before it ripens. Three
		/// days: long enough that a founder checking in daily watches it happen in stages, short
		/// enough that almost any visit worth making finds one either ripe or freshly gathered.
		/// </summary>
		public const long GrowTicks = KingdomRules.TicksPerDay * 3L;

		/// <summary>Food units one ripe plot delivers to the larders in a single harvest.</summary>
		public const int YieldPerHarvest = 4;

		/// <summary>
		/// Stage transitions one plot may resolve in a single settlement pass: exactly enough
		/// for one full Dormant-Growing-Ripe-Dormant cycle, never more. A planting is always
		/// anchored to the tick it is resolved at (there is no historical record of when, during
		/// an absence, the stores last happened to hold enough water to plant), so a longer
		/// absence can only ever guarantee that one cycle finished, not that several did; a
		/// higher cap here would not earn a second harvest, only let the loop spin once it is
		/// already done. This is the same compromise <see cref="KingdomRules.MaxArrivalsPerVisit"/>
		/// makes for settlers: the news one telling can carry is capped, never the absence
		/// itself, and the next visit picks up exactly where this one left off, from the plot's
		/// own stored tick stamp.
		/// </summary>
		public const int MaxCyclesPerVisit = 3;

		/// <summary>
		/// Whether the settlement can spare <see cref="PlantWaterCostDrams"/> for a planting
		/// without touching the water the heaviest possible single upkeep charge could still
		/// need this same visit. The plot is never the reason a dry streak starts: it may only
		/// spend what is left over once <see cref="KingdomRules.ReserveDays"/> days of
		/// upkeep, at the settlement's current population, are set aside untouched.
		/// </summary>
		/// <param name="StoredWater">Drams currently in the dedicated stores, read after the
		/// same pass's own upkeep draw has already happened.</param>
		/// <param name="Population">Living settlers, for the upkeep reserve.</param>
		public static bool CanAffordPlanting(int StoredWater, int Population)
		{
			int reserve = KingdomRules.UpkeepDrams(Population) * KingdomRules.ReserveDays;
			return StoredWater - PlantWaterCostDrams >= reserve;
		}

		/// <summary>Whether a growing crop has stood long enough to ripen.</summary>
		public static bool HasRipened(long NextStageTick, long TimeTicks)
		{
			return TimeTicks >= NextStageTick;
		}

		/// <summary>The tick a crop planted now will ripen at.</summary>
		public static long RipenTick(long PlantedTick)
		{
			return PlantedTick + GrowTicks;
		}

		/// <summary>
		/// Resolves the ground a settlement stands on to what it grows there. Mirrors
		/// <see cref="KingdomRules.StyleForSite"/>'s total fallback: an unknown, renamed, or
		/// empty style still grows something, because the ground under a plot is never the
		/// reason a founder goes hungry.
		/// </summary>
		/// <param name="Style">The settlement's <see cref="KingdomSystem.Style"/>, already
		/// resolved once at founding from the terrain the rite read. Never re-derived here from
		/// terrain directly &mdash; that evidence was gathered once, and a second reading could
		/// only disagree with it.</param>
		/// <returns>A vanilla food item blueprint name, never null or empty.</returns>
		public static string CropBlueprintForStyle(string Style)
		{
			switch (Style)
			{
			case "verdant":
				return "Vinewafer";
			case "fungal":
				return "Plump Mushroom";
			case "gyre":
				return "Godshroom Cap";
			case "eater":
				return "Dreadroot Tuber";
			default:
				return "Starapple";
			}
		}
	}
}
