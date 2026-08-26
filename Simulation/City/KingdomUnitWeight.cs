using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// How heavy one unit of reification is. LIVING-CITY-ARCHITECTURE &sect;0.0(b): units are
	/// weighted because they are not the same size, and the light tier is not a convenience — it
	/// is forced by a home farm standing eighty plant objects.
	/// </summary>
	internal enum KingdomUnitWeight : byte
	{
		/// <summary>Mint or move a body: <c>GameObject.Create</c> plus a population table plus a
		/// name, the heaviest unit by an order of magnitude. At most four a turn.</summary>
		Heavy = 0,

		/// <summary>One item stack into one container, or one work row reconciled.</summary>
		Medium = 1,

		/// <summary>One plant or prop object into a cell: exactly <c>ZoneRepair</c>'s own unit.</summary>
		Light = 2
	}
}
