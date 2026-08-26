using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One span of model time, frozen, as the reckon job receives it.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;2.5: a job may not read the clock, so the span arrives as two
	/// ticks. Every field is <c>readonly</c> and every type in the closure is ours or the
	/// framework's, which is what <c>KingdomComputeSeam</c> checks before this crosses.
	/// </para>
	/// </summary>
	internal readonly struct KingdomReckonInput
	{
		internal readonly KingdomCityState State;

		internal readonly long ToTick;

		internal KingdomReckonInput(KingdomCityState state, long toTick)
		{
			State = state;
			ToTick = toTick;
		}
	}
}
