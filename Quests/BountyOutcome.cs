using System.Collections.Generic;
using System.Text;
using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst
{
	/// <summary>What one attended pass did with a standing notice.</summary>
	public enum BountyOutcome
	{
		/// <summary>Nobody came to read it, or there was nobody to come.</summary>
		NobodyTried = 0,

		/// <summary>Somebody read it and walked away. Free, and remembered.</summary>
		Refused = 1,

		/// <summary>Somebody took it.</summary>
		Taken = 2
	}
}
