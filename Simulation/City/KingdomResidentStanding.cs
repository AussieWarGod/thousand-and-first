using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What the roll says about one settler. LIVING-CITY-ARCHITECTURE &sect;1.2(d) and &sect;8.3.
	/// <para>
	/// Standing is the durable labour/identity fact. Expedition is distinct from <c>Abroad</c>:
	/// both are on the roll and contribute no labour, but an expedition remains deliberately bound
	/// to the one commissioned body and has a dated realm job that must bring that body home.
	/// </para>
	/// </summary>
	internal enum KingdomResidentStanding : byte
	{
		Resident = 0,
		Abroad = 1,
		Dead = 2,

		/// <summary>Commissioned away under a bounded, durable expedition job.</summary>
		Expedition = 3
	}
}
