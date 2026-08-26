using System;
using System.Diagnostics;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>The live clock. Monotonic, and never the game's tick clock — an elapsed
	/// wall-measurement is not world time and must never be mistaken for it.</summary>
	internal sealed class KingdomStopwatchClock : IKingdomComputeClock
	{
		private static readonly double MicrosecondsPerTimestamp = 1000000.0 / Stopwatch.Frequency;

		public long NowMicroseconds()
		{
			return (long)(Stopwatch.GetTimestamp() * MicrosecondsPerTimestamp);
		}
	}
}
