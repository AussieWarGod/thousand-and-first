using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What one zone owes the ground, split by direction. Both halves are non-negative
	/// magnitudes in weighted thirds; the single signed figure the zone row persists is
	/// <see cref="Net"/>, and the split is recomputed at check-in from the stock rows, which is
	/// exactly what I1 (<c>model total == ground total + counter-owed, per stock kind</c>) is a
	/// statement about.
	/// </summary>
	internal readonly struct KingdomCatchUpCounter
	{
		internal readonly int LandThirds;

		internal readonly int DrawThirds;

		internal KingdomCatchUpCounter(int landThirds, int drawThirds)
		{
			LandThirds = landThirds;
			DrawThirds = drawThirds;
		}

		/// <summary>Everything still owed, in weighted thirds, regardless of direction. This is
		/// the figure the drain budget divides and the receipt reports as <c>owed</c>.</summary>
		internal int OwedThirds
		{
			get { return LandThirds + DrawThirds; }
		}

		/// <summary>The single signed figure the zone row carries.</summary>
		internal int Net
		{
			get { return LandThirds - DrawThirds; }
		}

		internal bool IsSettled
		{
			get { return LandThirds == 0 && DrawThirds == 0; }
		}
	}
}
