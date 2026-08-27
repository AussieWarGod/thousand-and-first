using System;
using System.Collections.Generic;

using XRL;
using XRL.World;
using XRL.World.AI.GoalHandlers;
using XRL.World.Parts;

using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The porter, at the engine's edge: minted at the edge of the zone the founder is standing in,
	/// walked by vanilla, putting real goods into a real container, and gone.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.7, and it is nearly free because every piece of it already
	/// stands. <b>Total cost: two reify units and a walk vanilla was going to do anyway.</b>
	/// </para>
	/// <para>
	/// Engine-coupled by design and paired with <see cref="KingdomJobRules"/> the way
	/// <c>KingdomCity</c> is paired with <c>KingdomCityRules</c>: nothing here decides anything. It
	/// reads the ground, asks the rules, obeys the binding registry, and applies the answer.
	/// </para>
	/// <para>
	/// <b>What W3 ships is one flow.</b> &sect;7.4 gives W6 nearest-holder sourcing and
	/// capacity-bound batching, <i>"because both only bite once many jobs compete over many
	/// holders"</i>. So there is one job kind here &mdash; the harvest already in flight, whose
	/// model credit G2 shipped &mdash; and the planner is the itinerary, not a 2-opt over an empty
	/// room.
	/// </para>
	/// </summary>
	public static partial class KingdomPorters
	{
	}
}
