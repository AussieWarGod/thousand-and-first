using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>Everything the confirmation prompt promises, in exact physical units and world
	/// ticks. It is copied into the realm job row at dispatch, so a reload never requotes it.</summary>
	internal readonly struct KingdomExpeditionQuote
	{
		internal readonly int DistanceCells;
		internal readonly int DurationDays;
		internal readonly long DueTick;
		internal readonly int WaterDrams;
		internal readonly int Provisions;

		internal KingdomExpeditionQuote(int distanceCells, int durationDays, long dueTick,
			int waterDrams, int provisions)
		{
			DistanceCells = distanceCells;
			DurationDays = durationDays;
			DueTick = dueTick;
			WaterDrams = waterDrams;
			Provisions = provisions;
		}
	}
}
