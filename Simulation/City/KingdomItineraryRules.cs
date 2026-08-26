namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// A job is a timed itinerary, computed once, at creation — and one pure function answers
	/// everything about it.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.7, invariant I5: for any <c>TimeTicks</c> the model gives
	/// ONE answer to where a carrier is, and every zone renders that same answer. Consistent
	/// re-rendering is indistinguishable from following, and costs a fraction of what following
	/// would — which is why the body never has to literally traverse anything.
	/// </para>
	/// <para>
	/// Pure and engine-free, and deliberately so: creation happens at reckon, over a frozen model,
	/// where most route zones are on disk and no zone may be touched. Path length at creation is
	/// estimated from the endpoints, never pathfound.
	/// </para>
	/// </summary>
	internal static partial class KingdomItineraryRules
	{
		/// <summary>LIVING-CITY-ARCHITECTURE §3.7: a nine-zone city's diameter is four or five zone
		/// steps, and a job that wants more than six legs is refused at planning and told.</summary>
		internal const int MaxLegs = KingdomCityMemoryRules.MaxLegs;

		/// <summary>Sinuosity over open ground, as a percent. LIVING-CITY-ARCHITECTURE §3.7.</summary>
		internal const int SinuosityOpenPercent = 125;

		/// <summary>Sinuosity through built-up ground, as a percent. LIVING-CITY-ARCHITECTURE §3.7.</summary>
		internal const int SinuosityBuiltPercent = 160;

		/// <summary>A paved leg costs 0.6 of the same distance unpaved, applied identically to the
		/// estimate and to the measured length so a road cannot make the two disagree.
		/// LIVING-CITY-ARCHITECTURE §3.10(3).</summary>
		internal const int RoadDiscountPercent = 60;

		/// <summary>No discount. Named so a caller never writes a bare 100.</summary>
		internal const int NoRoadDiscountPercent = 100;

		/// <summary>
		/// At Speed 100 an actor covers exactly one cell per tick — carrier and founder alike —
		/// so a founder walking beside a porter neither outpaces them nor falls behind.
		/// LIVING-CITY-ARCHITECTURE §3.7.
		/// </summary>
		internal const int WalkTicksPerCellDefault = 1;

		/// <summary>A job whose elapsed exceeds twice its projected duration fails and is told, so
		/// a founder who blocks a doorway forever produces a story and not an unbounded job set.
		/// LIVING-CITY-ARCHITECTURE §3.7.</summary>
		internal const int FailAtProjectedDurationMultiple = 2;

		/// <summary>Chebyshev distance: the cell count a diagonal walker actually pays.</summary>
		internal static bool TryChebyshev(int fromX, int fromY, int toX, int toY, out int cells, out KingdomCityFault fault)
		{
			cells = 0;
			long dx = (long)toX - fromX;
			long dy = (long)toY - fromY;
			if (dx < 0L)
			{
				dx = -dx;
			}
			if (dy < 0L)
			{
				dy = -dy;
			}
			long longest = (dx > dy) ? dx : dy;
			if (longest > int.MaxValue)
			{
				fault = KingdomCityFault.ArithmeticOverflow;
				return false;
			}
			cells = (int)longest;
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// A leg's length at creation: Chebyshev times sinuosity times the road discount, in
		/// integer percent, and zero zone access. That is the cost bound, and it is absolute.
		/// LIVING-CITY-ARCHITECTURE &sect;3.7.
		/// </summary>
		internal static bool TryEstimatePathLength(int chebyshevCells, int sinuosityPercent, int roadDiscountPercent, out int cells, out KingdomCityFault fault)
		{
			cells = 0;
			if (chebyshevCells < 0 || sinuosityPercent <= 0 || roadDiscountPercent <= 0 || roadDiscountPercent > 100)
			{
				fault = KingdomCityFault.InvalidRate;
				return false;
			}
			long scaled = (long)chebyshevCells * sinuosityPercent;
			scaled = scaled / 100L;
			scaled = (scaled * roadDiscountPercent) / 100L;
			if (scaled > int.MaxValue)
			{
				fault = KingdomCityFault.ArithmeticOverflow;
				return false;
			}
			cells = (int)scaled;
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// Whether a leg list is a journey at all: bounded, dated forward, and contiguous. A leg
		/// that arrives before it departs, or a leg that departs before the one before it arrived,
		/// is a corrupt itinerary and is refused whole.
		/// </summary>
		internal static bool TryValidate(KingdomLeg[] legs, int count, out KingdomCityFault fault)
		{
			if (legs == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (count < 0 || count > legs.Length || count > MaxLegs)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			for (int i = 0; i < count; i++)
			{
				KingdomLeg leg = legs[i];
				if (leg.DepartTick < 0L || leg.ArriveTick < leg.DepartTick || leg.PathLength < 0)
				{
					fault = KingdomCityFault.InvalidLegOrder;
					return false;
				}
				if (i > 0 && leg.DepartTick < legs[i - 1].ArriveTick)
				{
					fault = KingdomCityFault.InvalidLegOrder;
					return false;
				}
			}
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// Where the carrier is at a tick: a linear scan of at most six legs for the one containing
		/// it, then interpolation along that leg. One answer, and every zone renders that same
		/// answer — invariant I5.
		/// <para>
		/// A tick between two legs reports <see cref="KingdomItineraryPhase.Handoff"/> standing at
		/// the previous leg's exit cell, which is exactly where the engine's own zone connection
		/// maps the next leg's entry from, so the handoff needs no draw and cannot disagree with
		/// where the founder comes out.
		/// </para>
		/// </summary>
		internal static bool TryAt(KingdomLeg[] legs, int count, long tick, out KingdomItineraryFix fix, out KingdomCityFault fault)
		{
			fix = default(KingdomItineraryFix);
			if (!TryValidate(legs, count, out fault))
			{
				return false;
			}
			if (tick < 0L)
			{
				fault = KingdomCityFault.InvalidTick;
				return false;
			}
			if (count == 0)
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			KingdomLeg first = legs[0];
			if (tick < first.DepartTick)
			{
				fix = new KingdomItineraryFix(KingdomItineraryPhase.Pending, -1, first.ZoneId, first.EnterX, first.EnterY, 0);
				return true;
			}
			KingdomLeg last = legs[count - 1];
			if (tick >= last.ArriveTick)
			{
				fix = new KingdomItineraryFix(KingdomItineraryPhase.Delivered, -1, last.ZoneId, last.ExitX, last.ExitY, last.PathLength);
				return true;
			}
			for (int i = 0; i < count; i++)
			{
				KingdomLeg leg = legs[i];
				if (tick < leg.DepartTick)
				{
					KingdomLeg previous = legs[i - 1];
					fix = new KingdomItineraryFix(KingdomItineraryPhase.Handoff, i - 1, previous.ZoneId, previous.ExitX, previous.ExitY, previous.PathLength);
					return true;
				}
				if (tick >= leg.ArriveTick)
				{
					continue;
				}
				long duration = leg.ArriveTick - leg.DepartTick;
				long elapsed = tick - leg.DepartTick;
				int steps = (duration <= 0L) ? leg.PathLength : (int)((long)leg.PathLength * elapsed / duration);
				short x = Interpolate(leg.EnterX, leg.ExitX, elapsed, duration);
				short y = Interpolate(leg.EnterY, leg.ExitY, elapsed, duration);
				fix = new KingdomItineraryFix(KingdomItineraryPhase.EnRoute, i, leg.ZoneId, x, y, steps);
				return true;
			}
			fault = KingdomCityFault.OutsideItinerary;
			return false;
		}
	}
}
