using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Which way one claimed zone lies from another. Six, because the engine's own
	/// topology gives at most six neighbours &mdash; four orthogonal, plus the stratum above and
	/// below (LIVING-CITY-ARCHITECTURE &sect;0.0(f), &sect;3.10(2)).</summary>
	internal enum KingdomZoneStep : byte
	{
		North = 0,
		South = 1,
		East = 2,
		West = 3,
		Up = 4,
		Down = 5,

		/// <summary>Not a direction. The two zones are the same, or not neighbours at all.</summary>
		None = 6
	}
}
