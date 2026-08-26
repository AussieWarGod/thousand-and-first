using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Where a person's day puts them. Derived from job and standing policy, never
	/// authored per settler, and holding no times. LIVING-CITY-ARCHITECTURE &sect;1.2(d).</summary>
	internal enum KingdomDayShape : byte
	{
		Hearth = 0,
		Field = 1,
		Yard = 2,
		Market = 3,
		Craft = 4,
		Watch = 5,
		Shrine = 6
	}
}
