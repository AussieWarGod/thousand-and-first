using System;
using System.Collections.Generic;
#if TAF_TESTS
using System.IO;
using System.Text;
#endif

using ThousandAndFirst.Simulation.Kernel;
#if !TAF_TESTS
using XRL.World;
#endif

namespace ThousandAndFirst.Simulation.City
{
	internal static partial class KingdomJobRules
	{
		/// <summary>
		/// Turns a run of waypoints into a dated itinerary.
		/// <para>
		/// Each leg's length is <c>Chebyshev &times; Sinuosity &times; RoadDiscount</c> in integer
		/// percent, with <b>zero zone access</b> &mdash; that is &sect;3.7's absolute cost bound,
		/// and the reason the estimate is a prior that reality corrects rather than a pathfind.
		/// A leg of zero cells still costs one tick, because a carrier that arrives on the tick it
		/// departs has not walked.
		/// </para>
		/// </summary>
		internal static bool TryBuildLegs(KingdomLegPlan[] plans, int count, long startTick, int walkTicksPerCell, out KingdomLeg[] legs, out KingdomCityFault fault)
		{
			legs = null;
			if (plans == null)
			{
				fault = KingdomCityFault.NullArgument;
				return false;
			}
			if (count < 0 || count > plans.Length)
			{
				fault = KingdomCityFault.InvalidIndex;
				return false;
			}
			if (count > KingdomItineraryRules.MaxLegs)
			{
				// §3.7: a job that wants more than six legs is refused at planning and told. It is
				// never truncated, because a truncated route is a carrier arriving somewhere else.
				fault = KingdomCityFault.RowCapExceeded;
				return false;
			}
			if (startTick < 0L || walkTicksPerCell <= 0)
			{
				fault = KingdomCityFault.InvalidTick;
				return false;
			}
			KingdomLeg[] built = new KingdomLeg[count];
			long depart = startTick;
			for (int i = 0; i < count; i++)
			{
				KingdomLegPlan plan = plans[i];
				if (string.IsNullOrEmpty(plan.ZoneId))
				{
					fault = KingdomCityFault.NullArgument;
					return false;
				}
				int chebyshev;
				if (!KingdomItineraryRules.TryChebyshev(plan.EnterX, plan.EnterY, plan.ExitX, plan.ExitY, out chebyshev, out fault))
				{
					return false;
				}
				int length;
				if (!KingdomItineraryRules.TryEstimatePathLength(chebyshev, plan.SinuosityPercent, plan.RoadDiscountPercent, out length, out fault))
				{
					return false;
				}
				long walk = (long)length * walkTicksPerCell;
				if (walk < 1L)
				{
					walk = 1L;
				}
				long arrive = depart + walk;
				if (arrive < depart)
				{
					fault = KingdomCityFault.ArithmeticOverflow;
					return false;
				}
				built[i] = new KingdomLeg(plan.ZoneId, plan.EnterX, plan.EnterY, plan.ExitX, plan.ExitY, length, depart, arrive);
				depart = arrive;
			}
			if (!KingdomItineraryRules.TryValidate(built, count, out fault))
			{
				return false;
			}
			legs = built;
			return true;
		}

		/// <summary>
		/// What is still on the carrier's back at this fix.
		/// <para>
		/// The one-event-two-renderings invariant in arithmetic: the cargo is on them until the
		/// deposit leg is finished and gone afterwards. <b>The stores were credited at the dated
		/// tick either way</b> &mdash; the porter is carrying goods that are already the city's, so
		/// this figure is what a zone DRAWS, never what the city OWNS.
		/// </para>
		/// </summary>
		internal static int CargoAt(KingdomJobRow job, KingdomItineraryFix fix)
		{
			if (job.CargoAmount <= 0)
			{
				return 0;
			}
			if (fix.Phase == KingdomItineraryPhase.Delivered)
			{
				return 0;
			}
			if (fix.Phase == KingdomItineraryPhase.Pending)
			{
				return job.CargoAmount;
			}
			// Handoff reports the PREVIOUS leg's exit, so a handoff at the deposit leg's end is a
			// carrier who has just put the load down.
			if (fix.LegIndex > job.DepositLegIndex)
			{
				return 0;
			}
			if (fix.LegIndex == job.DepositLegIndex && fix.Phase == KingdomItineraryPhase.Handoff)
			{
				return 0;
			}
			return job.CargoAmount;
		}

		/// <summary>Whether the carrier has finished the leg the load lands at the end of.</summary>
		internal static bool Deposited(KingdomJobRow job, long nowTick)
		{
			KingdomLeg leg;
			if (!job.TryLeg(job.DepositLegIndex, out leg))
			{
				return false;
			}
			return nowTick >= leg.ArriveTick;
		}
	}
}
