using System.Text;

using ThousandAndFirst.Simulation.City;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>
	/// Engine-free rules for everything the settlement grows: what a field cycles through, what
	/// commits it, how long a crop stands before it is ripe, what a row is worth when it is
	/// gathered, and what seed goes with what crop. The engine-coupled halves are
	/// <see cref="ThousandAndFirst.KingdomCrops"/> (sowing, rows, delivery) and
	/// <see cref="XRL.World.Parts.r_KingdomPlot"/> (the state a field carries), both in this
	/// folder.
	/// <para>
	/// <b>Seeds are ours end to end.</b> The survey of the shipped game
	/// (<c>_notes/VANILLA-PRODUCTION-TRUTH.md</c> &sect;2.1) is unambiguous: twenty-eight
	/// blueprints read as seed, spore, tuber or cutting and <b>not one of them is plantable</b>
	/// &mdash; zero blueprints carry a planting part, zero grow into another object, and there is
	/// no <c>AddAction("Plant"&hellip;)</c> anywhere in the engine. So the seed item, the sowing
	/// verb and the cycle are authored here, in vanilla's idiom and against vanilla's own parts
	/// (<c>Harvestable</c> on the standing rows, <c>Food</c> on what they yield), which is
	/// Addendum 11(c)'s third preference exactly: fill in only where the survey shows vanilla has
	/// nothing.
	/// </para>
	/// <para>
	/// <b>The cycle is stamped, never simulated.</b> A suspended zone runs no clocks
	/// (<c>ActionManager</c>; every vanilla <c>Harvestable.RegenTimer</c> is dead for exactly
	/// that reason), so a field records the tick its crop comes ripe and every question is asked
	/// against that stamp. One reckoning resolves however many cycles actually elapsed.
	/// </para>
	/// </summary>
	public static partial class KingdomCropRules
	{
		/// <summary>
		/// A field's place in its own cycle.
		/// <para>
		/// <see cref="PlotStage.Dormant"/> is <b>uncommitted ground</b> and nothing else: a field
		/// nobody has put seed in. It is where every field starts and where withdrawing the seed
		/// puts it back. A committed field is never Dormant again until the founder takes their
		/// seed out of it, which is why the gate below can read this one value and be right.
		/// </para>
		/// </summary>
		public enum PlotStage
		{
			Dormant = 0,
			Growing = 1,
			Ripe = 2
		}

		/// <summary>Drams a sowing draws from the settlement's dedicated stores, once, at the
		/// moment the founder commits the seed. Not per cycle: what recurs is the crop's own
		/// growing, and the settlement pays for that in the hands the design asks for.</summary>
		public const int PlantWaterCostDrams = 3;

		/// <summary>
		/// Days a committed crop stands before it is ripe. Six &mdash; near enough a Qud week that
		/// a founder who leaves on an errand comes back to a harvest, far enough from a day that
		/// standing over the field is never the way to farm it.
		/// </summary>
		public const int CropDays = 6;

		/// <summary>Ticks of the same. The cycle length, and the only clock a field keeps.</summary>
		public const long GrowTicks = KingdomRules.TicksPerDay * (long)CropDays;

		/// <summary>
		/// Servings one standing row yields when it is gathered. With <see cref="CropDays"/> this
		/// is the whole of the food economy's denomination: a design that stands
		/// <c>Rows</c> rows makes <c>Rows &times; YieldPerRow / CropDays</c> servings a day, and
		/// that figure is what its <c>Carries="food:N"</c> must equal. <c>_notes/balance-sim.py</c>
		/// re-derives every food design's declared carry from the rows on its own blueprint and
		/// fails the run if one has drifted, exactly as it re-derives every water design's from
		/// the <c>LiquidProducer</c> on its.
		/// </summary>
		public const int YieldPerRow = 3;

		/// <summary>
		/// Grace between a crop coming ripe and the settlement's own hands gathering it in. One
		/// day, and it is the founder's day: a ripe row carries vanilla <c>Harvestable</c>, so
		/// anybody standing in the field can gather it themselves, and whatever they take is not
		/// there for the settlement to count. The window exists so that is a real choice rather
		/// than a race nobody can win.
		/// </summary>
		public const long GatherDelayTicks = KingdomRules.TicksPerDay;

		/// <summary>
		/// Cycles one reckoning will resolve. A hundred and twenty of them is nearly two years of
		/// six-day crops, so this is a bound on arithmetic rather than a forgiveness: what caps
		/// what a long absence actually banks is larder room, which the harvest overflows into
		/// loss exactly as a full cask overflows into the ground.
		/// </summary>
		public const int MaxCyclesPerVisit = 120;

		/// <summary>Chance, per cycle, that a gathering also returns sowable seed. Generous
		/// enough that a farm feeds its own next planting most seasons, and low enough that the
		/// first seed still has to be found or bought.</summary>
		public const int SeedReturnChancePercent = 20;

		/// <summary>Seed one reckoning may return, however many cycles it resolved. Two: an
		/// absence brings back a harvest, never a seed merchant's stock.</summary>
		public const int MaxSeedsPerResolve = 2;

	}
}
