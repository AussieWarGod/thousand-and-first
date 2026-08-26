using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Which civic stock a figure speaks for. LIVING-CITY-ARCHITECTURE &sect;1.2(a).</summary>
	internal enum KingdomStockKind : byte
	{
		Water = 0,
		Food = 1,
		Materials = 2,

		/// <summary>Route-only exact whole-object manifest. Not a civic scalar; its opaque owner
		/// defines every object and callback semantic.</summary>
		OpaqueManifest = 3
	}
}
