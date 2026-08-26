using System;
using System.Diagnostics;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>How a submitted computation ended.</summary>
	internal enum KingdomComputeStatus : byte
	{
		/// <summary>Ran, stayed inside its budget, and published a value.</summary>
		Ok = 0,

		/// <summary>The seam would not run it. Nothing was invoked.</summary>
		Refused = 1,

		/// <summary>Ran and returned false, or threw. Nothing is published.</summary>
		Faulted = 2,

		/// <summary>Ran and exceeded its lane's budget. Nothing is published.</summary>
		OverBudget = 3
	}
}
