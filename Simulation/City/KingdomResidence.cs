using System.Collections.Generic;

namespace ThousandAndFirst.Simulation.City
{
	internal readonly struct KingdomResidence
	{
		internal readonly string ZoneId, PlotId, BedId, Creed;
		internal readonly IReadOnlyList<string> Needs, Refuses, SelfTags;
		internal bool HasHome => !string.IsNullOrEmpty(ZoneId);

		internal KingdomResidence(string zone, string plot, string bed, string creed,
			IReadOnlyList<string> needs, IReadOnlyList<string> refuses, IReadOnlyList<string> selfTags)
		{
			ZoneId = zone; PlotId = plot; BedId = bed; Creed = creed;
			Needs = needs; Refuses = refuses; SelfTags = selfTags;
		}
	}
}
