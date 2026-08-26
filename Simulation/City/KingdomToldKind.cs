using System;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// What a told-log line is a line about. LIVING-CITY-ARCHITECTURE &sect;1.2(f).
	/// <para>
	/// Values are appended and never reordered: the ring is serialized as plain ints by
	/// <c>KingdomCityBook</c>, so an older save's <c>10</c> must go on meaning <c>Ceremony</c>
	/// forever.
	/// </para>
	/// </summary>
	internal enum KingdomToldKind : byte
	{
		None = 0,
		Harvest = 1,
		Delivery = 2,
		Arrival = 3,
		Departure = 4,
		Breakdown = 5,
		Mending = 6,
		Raising = 7,
		Shortfall = 8,
		Raid = 9,
		Ceremony = 10,

		/// <summary>W4. Two rows who shared a roof, married. LIVING-CITY-ARCHITECTURE
		/// &sect;7.4.</summary>
		Wedding = 11,

		/// <summary>W4. A row that went <c>Dead</c>, and the rite the city gave it. Written by the
		/// same call that announces the death, never by a second one.</summary>
		Funeral = 12,

		/// <summary>W4. A feast kept on a day of Qud's own calendar &mdash; the Ides, or the
		/// festival of Ut yara Ux. Never an invented holiday.</summary>
		Festival = 13,

		/// <summary>W7. A work stopped because its network could not feed it. The subject is the
		/// work id, and the outcome is the tier it stopped on
		/// (<c>KingdomWorkTier</c>) &mdash; so the ring remembers not only that the lights went
		/// down but how far down the ladder the city had to go. LIVING-CITY-ARCHITECTURE
		/// &sect;3.11.</summary>
		Brownout = 14
	}
}
