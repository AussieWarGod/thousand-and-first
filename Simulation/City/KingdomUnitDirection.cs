using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// Which way a catch-up unit moves goods. LIVING-CITY-ARCHITECTURE &sect;3.9: the counter is
	/// signed, because a season's drinking has to come out of the vessels it was actually drunk
	/// from, not out of a ledger note.
	/// </summary>
	internal enum KingdomUnitDirection : byte
	{
		Land = 0,
		Draw = 1
	}
}
