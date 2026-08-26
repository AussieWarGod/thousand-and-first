using System.Text;

using ThousandAndFirst.Simulation.City;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	public static partial class KingdomCropRules
	{
		/// <summary>
		/// Whether the settlement can spare <see cref="PlantWaterCostDrams"/> for a sowing
		/// without touching the water the heaviest possible single upkeep charge could still
		/// need. A field is never the reason a dry streak starts: it may only spend what is left
		/// once <see cref="KingdomRules.ReserveDays"/> days of upkeep, at the settlement's
		/// current population, are set aside untouched.
		/// </summary>
		/// <param name="StoredWater">Drams currently in the dedicated stores.</param>
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

		/// <summary>The tick a crop sown now comes ripe at.</summary>
		public static long RipenTick(long PlantedTick)
		{
			return PlantedTick + GrowTicks;
		}

		/// <summary>Whether the settlement's own hands have waited out
		/// <see cref="GatherDelayTicks"/> and may gather a field that came ripe at
		/// <paramref name="RipeTick"/>.</summary>
		public static bool MayGather(long RipeTick, long TimeTicks)
		{
			return TimeTicks >= RipeTick + GatherDelayTicks;
		}

		/// <summary>
		/// How many ripenings are due at once, closed form. A field that has stood through a long
		/// absence resolves every cycle it actually completed in one reckoning &mdash; no loop
		/// walks the calendar, and nothing is forgiven.
		/// </summary>
		/// <param name="NextRipeTick">The stamp the field is carrying: the tick its NEXT crop
		/// comes ripe.</param>
		/// <param name="TimeTicks">Now.</param>
		/// <returns>Zero before the first ripening is due; otherwise the number of cycles that
		/// completed, clamped to <see cref="MaxCyclesPerVisit"/>.</returns>
		public static int CyclesDue(long NextRipeTick, long TimeTicks)
		{
			if (TimeTicks < NextRipeTick)
			{
				return 0;
			}
			long over = TimeTicks - NextRipeTick;
			long cycles = 1L + over / GrowTicks;
			return (cycles >= MaxCyclesPerVisit) ? MaxCyclesPerVisit : (int)cycles;
		}

		/// <summary>The tick the last of <paramref name="Cycles"/> ripenings came due at, which is
		/// what dates a harvest resolved out of an absence.</summary>
		public static long LastRipeTick(long NextRipeTick, int Cycles)
		{
			return (Cycles <= 0) ? NextRipeTick : (NextRipeTick + ((long)Cycles - 1L) * GrowTicks);
		}

		/// <summary>Where the field's stamp lands after a reckoning cashed
		/// <paramref name="Cycles"/> of them. Restamped from the harvest, never from now, so a
		/// part-cycle already grown is kept rather than thrown away.</summary>
		public static long RestampedRipeTick(long NextRipeTick, int Cycles)
		{
			return NextRipeTick + ((Cycles < 1) ? 1L : (long)Cycles) * GrowTicks;
		}

		/// <summary>
		/// How many ripenings the settlement's own hands may gather this reckoning, and whether
		/// the newest one is being left standing for the founder.
		/// <para>
		/// The whole of the founder's-day rule in one place, because getting it wrong the other way
		/// round is silent and expensive: unripening a crop that has not been credited loses a
		/// harvest, and gathering one inside the founder's day takes a crop they were promised
		/// first call on.
		/// </para>
		/// </summary>
		/// <param name="NextRipeTick">The field's stamp.</param>
		/// <param name="TimeTicks">Now.</param>
		/// <param name="HoldsLast">True when the newest ripening is still inside
		/// <see cref="GatherDelayTicks"/> and is being left where it stands.</param>
		/// <returns>Cycles to gather now. Zero means nothing is owed yet.</returns>
		public static int GatherableCycles(long NextRipeTick, long TimeTicks, out bool HoldsLast)
		{
			HoldsLast = false;
			int due = CyclesDue(NextRipeTick, TimeTicks);
			if (due <= 0)
			{
				return 0;
			}
			if (MayGather(LastRipeTick(NextRipeTick, due), TimeTicks))
			{
				return due;
			}
			HoldsLast = true;
			return due - 1;
		}

		/// <summary>
		/// Servings a whole reckoning brings in.
		/// <para>
		/// Every cycle but one is credited at what STANDS: nobody can have taken a row off a crop
		/// that ripened and was gathered inside the same reckoning. The exception is the one crop
		/// a founder actually stood in front of, which is credited at what stands RIPE &mdash; a
		/// row they gathered by hand is a row the settlement does not also get.
		/// </para>
		/// </summary>
		/// <param name="StandingRows">Rows still in the ground.</param>
		/// <param name="RipeRows">Of those, the ones standing ripe.</param>
		/// <param name="Cycles">Ripenings being gathered.</param>
		/// <param name="CountsRipeLast">Whether the last of them is the crop the founder was
		/// looking at. False for a reckoning resolved out of an absence, and for one whose newest
		/// crop is being held.</param>
		/// <param name="EffectivenessPercent">What the field is running at.</param>
		public static int GatheredYield(int StandingRows, int RipeRows, int Cycles, bool CountsRipeLast, int EffectivenessPercent)
		{
			return GatheredYield(StandingRows, RipeRows, Cycles, CountsRipeLast, EffectivenessPercent,
				KingdomProductionRules.BaselineMethodPercent);
		}

		/// <summary>
		/// The same reckoning for a realm whose keepers have worked something out.
		/// </summary>
		/// <param name="MethodPercent">The realm's method, from <c>KingdomResearch.MethodPercent</c>.
		/// The baseline is what a realm that has researched nothing carries, and it changes no
		/// number here.</param>
		public static int GatheredYield(int StandingRows, int RipeRows, int Cycles, bool CountsRipeLast, int EffectivenessPercent, int MethodPercent)
		{
			if (Cycles <= 0)
			{
				return 0;
			}
			int last = CountsRipeLast ? RipeRows : StandingRows;
			long yield = (long)HarvestYield(StandingRows, EffectivenessPercent, MethodPercent) * (Cycles - 1)
				+ HarvestYield(last, EffectivenessPercent, MethodPercent);
			return (yield > int.MaxValue) ? int.MaxValue : (int)yield;
		}

		/// <summary>
		/// Ticks one pulse of irrigation is worth. Ten &mdash; a turn's own growing, so a field
		/// standing inside a running <c>Hydraulic Irrigator</c> comes ripe in half the time.
		/// <para>
		/// This is Addendum 11(c)'s SECOND preference exactly: the vanilla machine keeps its own
		/// behaviour (<c>RadiusEventSender Event="AccelerateRipening" Radius="10" ChargeUse="5"</c>
		/// fires on its own turn, at its own radius, off its own charge) and our clock answers it.
		/// The survey found this event fires from DATA and is answered by <c>Harvestable.Ripen</c>,
		/// which returns immediately on every shipped blueprint because none arms <c>RegenTime</c>
		/// &mdash; so the irrigator currently does nothing to any plant in the game. It does
		/// something to ours.
		/// </para>
		/// </summary>
		public const long IrrigationTicksPerPulse = 10L;

		/// <summary>Where one pulse of irrigation leaves a field's stamp. Never before now: a
		/// machine may shorten a wait and may not conjure a harvest out of the past, and ripening
		/// itself stays the tick-stamped cycle's own business.</summary>
		public static long IrrigatedRipeTick(long NextRipeTick, long TimeTicks)
		{
			long pulled = NextRipeTick - IrrigationTicksPerPulse;
			return (pulled < TimeTicks) ? TimeTicks : pulled;
		}

		/// <summary>
		/// Servings a gathering brings in. Rows actually standing, times what a row is worth,
		/// times what the field is running at &mdash; the same effectiveness
		/// <c>KingdomSubsidence.Supports</c> folds its <c>Carries</c> by, so a shorthanded or
		/// half-wrecked field gathers exactly what it was counted for.
		/// </summary>
		/// <param name="Rows">Rows standing in the ground. Zero or fewer yields nothing.</param>
		/// <param name="EffectivenessPercent">0-100, from <c>KingdomWear.EffectivenessOf</c>.</param>
		public static int HarvestYield(int Rows, int EffectivenessPercent)
		{
			return HarvestYield(Rows, EffectivenessPercent, KingdomProductionRules.BaselineMethodPercent);
		}

		/// <summary>
		/// The same gathering, with the keepers' method on it.
		/// <para>
		/// RESEARCH-SYSTEM-DESIGN &sect;8.2: <i>output = base &times; crew &times; wear &times;
		/// method</i>, and method is a THIRD factor. It is applied AFTER the effectiveness clamp
		/// and is deliberately not folded into it &mdash; a field may not run above what its hands
		/// and its condition manage, and what better husbandry buys is a heavier row off the same
		/// hands rather than a hundred-and-fifty-percent-staffed field. A field nobody works still
		/// gathers nothing, because a zero effectiveness returns before this multiplies.
		/// </para>
		/// </summary>
		/// <param name="MethodPercent">The realm's method, from <c>KingdomResearch.MethodPercent</c>.
		/// Anything under the baseline is read as the baseline, so the tree is a bonus lane and
		/// never a tax.</param>
		public static int HarvestYield(int Rows, int EffectivenessPercent, int MethodPercent)
		{
			if (Rows <= 0 || EffectivenessPercent <= 0)
			{
				return 0;
			}
			int percent = (EffectivenessPercent > 100) ? 100 : EffectivenessPercent;
			long yield = (long)Rows * YieldPerRow * percent / 100L;
			return KingdomProductionRules.Methoded((yield > int.MaxValue) ? int.MaxValue : (int)yield, MethodPercent);
		}

		/// <summary>
		/// The daily food a design standing <paramref name="Rows"/> rows honestly makes, which is
		/// what its catalogue <c>Carries="food:N"</c> is required to equal. The food lane's answer
		/// to the water lane's <c>TicksPerDay / mean(VariableRate)</c>: a number that comes off
		/// what the object visibly does rather than out of an author's head.
		/// </summary>
		public static int FoodPerDayForRows(int Rows)
		{
			return (Rows <= 0) ? 0 : (Rows * YieldPerRow / CropDays);
		}

		/// <summary>The rows a design must stand to carry <paramref name="Food"/> a day. The
		/// inverse of <see cref="FoodPerDayForRows"/>, and what the blueprint's own
		/// <c>r_KingdomCropRows</c> tag is required to say.</summary>
		public static int RowsForFoodPerDay(int Food)
		{
			return (Food <= 0) ? 0 : (Food * CropDays / YieldPerRow);
		}

	}
}
