using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// The brownout ladder, and it is a design statement rather than an ordering convenience.
	/// <b>Lower stops first.</b>
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;3.11 states the order —
	/// <i>industry &rarr; refining &rarr; amenity &rarr; food &rarr; water &rarr; defence and
	/// watch</i> — and says where it comes from: it is the mod's existing <i>stop at the loyal
	/// core</i> discipline, the thirst ladder's <i>"empty casks and one rung of the ladder, never an
	/// empty town"</i> (DECISIONS: <i>"Failure has a floor"</i>), applied to charge instead of
	/// drams. A city gives up what it is doing before it gives up what it is.
	/// </para>
	/// <para>
	/// <b>Where lodging sits, stated because it is the question the order is judged on.</b> Lodging
	/// is <see cref="Amenity"/> — the middle of the ladder, not the top and not the bottom. A roof
	/// needs no charge to keep the rain off; what a dwelling draws power for is comfort, and the
	/// closeness ladder and the roof brink are what actually govern a household
	/// (<c>KingdomLodgingRules</c>, <c>KingdomBrinkRules</c>). Putting lodging <i>last</i> would
	/// make a brownout able to condemn a home, which is the roof brink's business and nothing
	/// else's; putting it <i>first</i> would say a settlement stops housing people before it stops
	/// smelting, which is the opposite of everything the mod says about a city. So the forge and the
	/// smelter go quiet first, comfort third, and the things a city dies without — food, water, the
	/// watch — are the last to be given up, in that order, because a dark hungry city recovers and a
	/// city whose watch went dark on the night raiders came does not.
	/// </para>
	/// </summary>
	internal enum KingdomWorkTier : byte
	{
		Industry = 0,
		Refining = 1,
		Amenity = 2,
		Food = 3,
		Water = 4,
		Watch = 5
	}
}
