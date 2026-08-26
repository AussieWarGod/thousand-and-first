using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>The named clocks, consolidated off the settlement's loose longs and given an
	/// ordinal, which is what makes their draws reproducible. LIVING-CITY-ARCHITECTURE &sect;1.2(e).</summary>
	internal enum KingdomClockKind : byte
	{
		Harvest = 0,
		Arrival = 1,
		Guest = 2,
		NotableGuest = 3,
		Festival = 4,
		MarketDay = 5,
		Delivery = 6,
		Raid = 7
	}
}
