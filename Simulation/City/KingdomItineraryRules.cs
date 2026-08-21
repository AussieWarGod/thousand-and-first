namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Where a carrier is, in the only terms the model has an opinion about.</summary>
	internal enum KingdomItineraryPhase : byte
	{
		/// <summary>Before the first leg departs.</summary>
		Pending = 0,

		/// <summary>Somewhere along a leg.</summary>
		EnRoute = 1,

		/// <summary>Between two legs: the edge handoff.</summary>
		Handoff = 2,

		/// <summary>Past the last leg's arrival.</summary>
		Delivered = 3
	}

	/// <summary>
	/// One leg of a job: which zone it crosses, where it enters and leaves, how long the crossing
	/// is, and the two ticks that date it.
	/// <para>
	/// The endpoints and the length are model truth; the in-between is a redrawing that may differ
	/// by a cell or two if the ground changed (LIVING-CITY-ARCHITECTURE &sect;3.7). Storing a full
	/// cell path would be up to eighty entries a leg and would make a wall raised across the route
	/// a contradiction rather than a detour.
	/// </para>
	/// </summary>
	internal readonly struct KingdomLeg
	{
		internal readonly string ZoneId;

		internal readonly short EnterX;

		internal readonly short EnterY;

		internal readonly short ExitX;

		internal readonly short ExitY;

		/// <summary>Cells to walk. At creation this is an estimate; at render the real length is
		/// measured once and the leg re-projects.</summary>
		internal readonly int PathLength;

		internal readonly long DepartTick;

		internal readonly long ArriveTick;

		internal KingdomLeg(string zoneId, short enterX, short enterY, short exitX, short exitY, int pathLength, long departTick, long arriveTick)
		{
			ZoneId = zoneId;
			EnterX = enterX;
			EnterY = enterY;
			ExitX = exitX;
			ExitY = exitY;
			PathLength = pathLength;
			DepartTick = departTick;
			ArriveTick = arriveTick;
		}

		internal KingdomLeg WithTicks(long departTick, long arriveTick)
		{
			return new KingdomLeg(ZoneId, EnterX, EnterY, ExitX, ExitY, PathLength, departTick, arriveTick);
		}
	}

	/// <summary>The one answer the model gives to "where is this carrier".</summary>
	internal readonly struct KingdomItineraryFix
	{
		internal readonly KingdomItineraryPhase Phase;

		/// <summary>Which leg the tick fell in, or -1 before the first and after the last.</summary>
		internal readonly int LegIndex;

		internal readonly string ZoneId;

		internal readonly short X;

		internal readonly short Y;

		/// <summary>Cells walked along this leg: <c>floor(progress x PathLength)</c>.</summary>
		internal readonly int StepsTaken;

		internal KingdomItineraryFix(KingdomItineraryPhase phase, int legIndex, string zoneId, short x, short y, int stepsTaken)
		{
			Phase = phase;
			LegIndex = legIndex;
			ZoneId = zoneId;
			X = x;
			Y = y;
			StepsTaken = stepsTaken;
		}
	}

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
	internal static class KingdomItineraryRules
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

		/// <summary>
		/// Shifts an itinerary after a live delay or a corrected length.
		/// <para>
		/// LIVING-CITY-ARCHITECTURE &sect;3.7, the re-projection rule: <i>only the unstarted
		/// remainder of an itinerary may move.</i> A leg already begun keeps its
		/// <c>DepartTick</c>; the current leg's <c>ArriveTick</c> and every later leg shift by the
		/// same signed delta. So a porter body-blocked for ten turns arrives ten turns later and
		/// everything downstream shifts by ten — no rubber-banding, no catch-up sprint.
		/// </para>
		/// <para>
		/// Copy-on-write: the input array is never touched, and a refusal publishes nothing.
		/// </para>
		/// </summary>
		internal static bool TryReproject(KingdomLeg[] legs, int count, int currentLegIndex, long deltaTicks, out KingdomLeg[] next, out KingdomCityFault fault)
		{
			next = null;
			if (!TryValidate(legs, count, out fault))
			{
				return false;
			}
			if (currentLegIndex < 0 || currentLegIndex >= count)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			KingdomLeg[] shifted = new KingdomLeg[count];
			for (int i = 0; i < count; i++)
			{
				KingdomLeg leg = legs[i];
				if (i < currentLegIndex)
				{
					shifted[i] = leg;
					continue;
				}
				long depart = (i == currentLegIndex) ? leg.DepartTick : leg.DepartTick + deltaTicks;
				long arrive = leg.ArriveTick + deltaTicks;
				if (depart < 0L || arrive < depart)
				{
					fault = KingdomCityFault.InvalidLegOrder;
					return false;
				}
				shifted[i] = leg.WithTicks(depart, arrive);
			}
			if (!TryValidate(shifted, count, out fault))
			{
				return false;
			}
			next = shifted;
			fault = KingdomCityFault.None;
			return true;
		}

		/// <summary>
		/// Whether a job has outlived twice its projected duration and must fail rather than be
		/// re-projected again. LIVING-CITY-ARCHITECTURE &sect;3.7.
		/// </summary>
		internal static bool TryHasOverrun(KingdomLeg[] legs, int count, long nowTick, out bool overrun, out KingdomCityFault fault)
		{
			overrun = false;
			if (!TryValidate(legs, count, out fault))
			{
				return false;
			}
			if (count == 0)
			{
				fault = KingdomCityFault.OutsideItinerary;
				return false;
			}
			long start = legs[0].DepartTick;
			if (nowTick < start)
			{
				fault = KingdomCityFault.InvalidTick;
				return false;
			}
			long projected = legs[count - 1].ArriveTick - start;
			if (projected > long.MaxValue / FailAtProjectedDurationMultiple)
			{
				fault = KingdomCityFault.ArithmeticOverflow;
				return false;
			}
			overrun = (nowTick - start) > (projected * FailAtProjectedDurationMultiple);
			fault = KingdomCityFault.None;
			return true;
		}

		private static short Interpolate(short from, short to, long elapsed, long duration)
		{
			if (duration <= 0L)
			{
				return to;
			}
			long moved = from + (((long)to - from) * elapsed / duration);
			if (moved < short.MinValue)
			{
				moved = short.MinValue;
			}
			if (moved > short.MaxValue)
			{
				moved = short.MaxValue;
			}
			return (short)moved;
		}
	}
}
