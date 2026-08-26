using System;
using System.Diagnostics;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// Why the seam refused or abandoned a computation. Distinct from
	/// <see cref="KingdomCityFault"/>, which is what a rule says about its own arithmetic: this is
	/// what the boundary says about the job.
	/// </summary>
	internal enum KingdomComputeRefusal : byte
	{
		None = 0,
		NullJob = 1,
		NullClock = 2,

		/// <summary>The job threw. It stalls itself, never the city and never the turn.</summary>
		Threw = 3,

		/// <summary>A type from a Qud assembly appears in the boundary's type closure.</summary>
		EngineTypeAtBoundary = 4,

		/// <summary>A boundary type carries a field that is not <c>readonly</c>.</summary>
		MutableField = 5,

		/// <summary>A boundary type carries a static that is not <c>readonly</c> or <c>const</c>.</summary>
		MutableStatic = 6,

		/// <summary>The closure is deeper or wider than the walker will follow, which is a refusal
		/// rather than a pass: an unwalkable boundary has not been shown to be clean.</summary>
		ClosureTooLarge = 7
	}
}
