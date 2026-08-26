namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomItineraryRules
	{
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
