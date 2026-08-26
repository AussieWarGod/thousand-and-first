using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>What a work is, for the one small discriminated slot of run-state it carries.
	/// LIVING-CITY-ARCHITECTURE &sect;1.2(c).</summary>
	internal enum KingdomWorkKind : byte
	{
		Other = 0,
		Growing = 1,
		Store = 2,
		Producer = 3,
		Refiner = 4,
		Power = 5,

		/// <summary>Attended-only post for the one real gang raising a plot or scaffold. A work row
		/// publishes this kind and its resident-derived crew; the construction receipt remains the
		/// sole owner of progress.</summary>
		Construction = 6
	}
}
