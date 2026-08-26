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
	/// <summary>
	/// One waypoint pair the planner turns into a leg: which ground, in by which cell, out by which
	/// cell, and how sinuous and how paved the ground between them is.
	/// </summary>
	internal readonly struct KingdomLegPlan
	{
		internal readonly string ZoneId;

		internal readonly short EnterX;

		internal readonly short EnterY;

		internal readonly short ExitX;

		internal readonly short ExitY;

		/// <summary>From <c>KingdomItineraryRules.SinuosityOpenPercent</c> or
		/// <c>SinuosityBuiltPercent</c>, by district.</summary>
		internal readonly int SinuosityPercent;

		/// <summary><c>KingdomItineraryRules.RoadDiscountPercent</c> where a road is laid along
		/// this leg, <c>NoRoadDiscountPercent</c> where none is.</summary>
		internal readonly int RoadDiscountPercent;

		internal KingdomLegPlan(string zoneId, short enterX, short enterY, short exitX, short exitY, int sinuosityPercent, int roadDiscountPercent)
		{
			ZoneId = zoneId;
			EnterX = enterX;
			EnterY = enterY;
			ExitX = exitX;
			ExitY = exitY;
			SinuosityPercent = sinuosityPercent;
			RoadDiscountPercent = roadDiscountPercent;
		}
	}
}
