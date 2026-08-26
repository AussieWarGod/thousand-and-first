using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>What a posted notice asks for. Four tasks, each grounded in a system that already
	/// exists: clearance, the stockpiles, the works, and the claim's own edge.</summary>
	public enum BountyTask
	{
		/// <summary>Clear a staked rect. Pays twice: the price, and the clearance yield.</summary>
		Clearance = 0,

		/// <summary>Carry a marked pile into the settlement's stockpiles.</summary>
		Fetch = 1,

		/// <summary>Man one idle work for a season.</summary>
		Manning = 2,

		/// <summary>Walk the frontier edge and bring back a report of the ground beyond.</summary>
		Scouting = 3
	}
}
