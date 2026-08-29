using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>One named clock: kind, when it next falls, and where in its lane it sits.
	/// Sixteen bytes at &sect;0.0(c).</summary>
	internal readonly struct KingdomClockRow
	{
		internal readonly KingdomClockKind Kind;

		internal readonly long NextDueTick;

		/// <summary>The occurrence index within this clock's stream. The whole trick of
		/// LIVING-CITY-ARCHITECTURE &sect;2.4: the seventh harvest of field 3 draws the same numbers
		/// whether it is resolved on the day it fell or six cycles later inside one reckoning.</summary>
		internal readonly int Ordinal;

		internal KingdomClockRow(KingdomClockKind kind, long nextDueTick, int ordinal)
		{
			Kind = kind;
			NextDueTick = nextDueTick;
			Ordinal = ordinal;
		}
	}
}
