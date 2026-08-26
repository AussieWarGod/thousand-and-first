using System;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What one submitted computation actually did. Counts, not only milliseconds: a timing is
	/// hardware and a count is a contract (LIVING-CITY-ARCHITECTURE &sect;6.5), which is what lets a
	/// tester on a slow machine still prove that a ninety-day reckoning did the same row-visits as
	/// a one-day one.
	/// </summary>
	internal readonly struct KingdomComputeCounters
	{
		internal readonly int BreakpointSteps;

		internal readonly long RowVisits;

		internal readonly int Draws;

		/// <summary>Reify units spent, weighted in thirds (LIVING-CITY-ARCHITECTURE &sect;0.0(b)).</summary>
		internal readonly int UnitThirds;

		internal readonly long Bytes;

		internal KingdomComputeCounters(int breakpointSteps, long rowVisits, int draws, int unitThirds, long bytes)
		{
			BreakpointSteps = breakpointSteps;
			RowVisits = rowVisits;
			Draws = draws;
			UnitThirds = unitThirds;
			Bytes = bytes;
		}

		internal static KingdomComputeCounters None
		{
			get { return new KingdomComputeCounters(0, 0L, 0, 0, 0L); }
		}
	}
}
