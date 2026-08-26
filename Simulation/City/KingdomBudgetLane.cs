using System;
using System.Globalization;
using System.Text;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// A row of the performance constitution. One lane, one budget, one place the numbers live.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;0.0 is the table; this enum is its index. Nothing in the city
	/// may time or count itself against a figure that is not a row here, because a budget quoted at
	/// a call site is a budget nobody can find when it moves.
	/// </para>
	/// </summary>
	internal enum KingdomBudgetLane : byte
	{
		/// <summary>One city, one pass. LIVING-CITY-ARCHITECTURE &sect;2.1.</summary>
		Reckon = 0,

		/// <summary>One turn's amortised spend while a debt stands. LIVING-CITY-ARCHITECTURE &sect;3.5.</summary>
		Reify = 1,

		/// <summary>One micro-reckon slice. LIVING-CITY-ARCHITECTURE &sect;3.6.</summary>
		Heartbeat = 2,

		/// <summary>The heartbeat's per-turn amortisation. LIVING-CITY-ARCHITECTURE &sect;3.6.</summary>
		HeartbeatAmortised = 3,

		/// <summary>Turns to drain the worst backlog. LIVING-CITY-ARCHITECTURE &sect;3.5.</summary>
		CatchUpDrain = 4,

		/// <summary>Model plus registry plus itineraries plus matrix, in RAM. LIVING-CITY-ARCHITECTURE &sect;0.0(c).</summary>
		ModelBytes = 5,

		/// <summary>The same, serialized on the write path. LIVING-CITY-ARCHITECTURE &sect;6.5.</summary>
		SaveBytes = 6,

		/// <summary>One route plan, per slice. LIVING-CITY-ARCHITECTURE &sect;3.10.</summary>
		RoutePlan = 7,

		/// <summary>One network flow solve, per city, per reckon. LIVING-CITY-ARCHITECTURE &sect;3.11.</summary>
		NetworkSolve = 8,

		/// <summary>Zones held resident beyond the seated one. LIVING-CITY-ARCHITECTURE &sect;6.4.</summary>
		ResidentZones = 9,

		/// <summary>One prefetch thaw. Timed, never budgeted. LIVING-CITY-ARCHITECTURE &sect;6.5.</summary>
		Thaw = 10
	}
}
