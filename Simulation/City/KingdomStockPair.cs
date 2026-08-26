using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>One stock and the ceiling it fills toward. Two longs, sixteen bytes.</summary>
	internal readonly struct KingdomStockPair
	{
		internal readonly long Level;

		internal readonly long Capacity;

		internal KingdomStockPair(long level, long capacity)
		{
			Level = level;
			Capacity = capacity;
		}
	}
}
