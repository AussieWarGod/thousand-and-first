using System;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>What a node does on its network. LIVING-CITY-ARCHITECTURE &sect;3.11.</summary>
	internal enum KingdomNetworkRole : byte
	{
		/// <summary>Makes the thing. A power work, a well head, a condenser.</summary>
		Source = 0,

		/// <summary>Spends it. A mill, a charging post, a bath house.</summary>
		Sink = 1,

		/// <summary>Holds it between the making and the spending. A salt bed, a cistern.</summary>
		Store = 2
	}
}
