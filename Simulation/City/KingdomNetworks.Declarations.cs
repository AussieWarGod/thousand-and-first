using System;
using System.Collections.Generic;

using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One zone's share of the liquid lines standing in it: what each component carries, which
	/// zone row it feeds, how much it will pass in a day, and which of the zone's four edges it
	/// declares across.
	/// <para>
	/// Derived state, and deliberately not saved. LIVING-CITY-ARCHITECTURE &sect;3.11: <i>"the graph
	/// is rebuilt from the ground the next time that zone renders"</i>. What persists is the
	/// DECLARATION, and it persists where the founder put it &mdash; on the conduit objects
	/// themselves, which the engine saves with the zone. Storing a second copy in the model would
	/// be storing a fact we would then have to hold in step with the ground it came from.
	/// </para>
	/// </summary>
	internal sealed class KingdomZoneLine
	{
		internal readonly string ZoneId;

		internal readonly string Liquid;

		/// <summary>The narrowest segment on this component, in drams a day.</summary>
		internal readonly int CapacityPerDay;

		/// <summary>The worst condition on the component. Addendum 10(b): a cracked main carries
		/// less rather than the same.</summary>
		internal readonly int ConditionPercent;

		/// <summary>Which of this zone's edges the component declares across, as a
		/// <c>KingdomNetworkRules</c> join mask.</summary>
		internal readonly int EdgeMask;

		/// <summary>How many taps bind a vessel on this component. A line with no tap is a line
		/// that reaches nothing, which is legal and does nothing.</summary>
		internal readonly int Taps;

		internal KingdomZoneLine(string zoneId, string liquid, int capacityPerDay, int conditionPercent, int edgeMask, int taps)
		{
			ZoneId = zoneId;
			Liquid = liquid;
			CapacityPerDay = capacityPerDay;
			ConditionPercent = conditionPercent;
			EdgeMask = edgeMask;
			Taps = taps;
		}
	}
}
