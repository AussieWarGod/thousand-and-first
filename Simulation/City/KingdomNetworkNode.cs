using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One node of a network: which work it is, what it does there, and the two figures the solve
	/// reads off it.
	/// <para>
	/// Fourteen declared bytes against the sixteen LIVING-CITY-ARCHITECTURE &sect;0.0(c) budgets.
	/// <b>What is deliberately not here is a level.</b> A store's contents are the ground's, read
	/// back at check-in and landed at reify in dedication order (&sect;3.9,
	/// <c>KingdomDrainRules</c>); the model keeps one aggregate per network and never a second copy
	/// of a number it would then have to hold in step with a container.
	/// </para>
	/// </summary>
	internal readonly struct KingdomNetworkNode
	{
		internal readonly int WorkId;

		internal readonly KingdomNetworkRole Role;

		/// <summary>The brownout ladder's rung for this node. Read only for a
		/// <see cref="KingdomNetworkRole.Sink"/>; carried on every node so the row has one shape.</summary>
		internal readonly KingdomWorkTier Tier;

		/// <summary>What a store can hold. Zero for a source or a sink.</summary>
		internal readonly int Capacity;

		/// <summary>A source's output a day, a sink's demand a day, a store's throughput a day.
		/// Never negative: which direction it points is the role's business, not the number's.</summary>
		internal readonly int RatePerDay;

		internal KingdomNetworkNode(int workId, KingdomNetworkRole role, KingdomWorkTier tier, int capacity, int ratePerDay)
		{
			WorkId = workId;
			Role = role;
			Tier = tier;
			Capacity = (capacity > 0) ? capacity : 0;
			RatePerDay = (ratePerDay > 0) ? ratePerDay : 0;
		}
	}
}
