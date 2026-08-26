using System;
using System.Diagnostics;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>
	/// One piece of model computation, in the only shape the seam accepts.
	/// <para>
	/// LIVING-CITY-ARCHITECTURE &sect;2.5: immutable in, immutable out; no engine type crosses;
	/// budget and timeout belong to the seam, not the job; and <b>a job may not read the clock</b> —
	/// <c>nowTick</c> is an input, never an ambient read, which is also what makes a job replayable
	/// in a test.
	/// </para>
	/// <para>
	/// A structural interface, never a base class. Third-party computations implement the same one
	/// and inherit the same budget, timeout and isolation.
	/// </para>
	/// </summary>
	internal interface IKingdomComputation<TIn, TOut>
	{
		/// <summary>What this job is, for the receipt line. Never null.</summary>
		string Label { get; }

		/// <summary>Which row of the performance constitution this job answers to.</summary>
		KingdomBudgetLane Lane { get; }

		/// <summary>
		/// Runs over the frozen input and produces a new frozen value.
		/// <para>
		/// Total over representable input: returns false with a fault and publishes nothing rather
		/// than throwing. The seam catches a throw anyway, because a third-party job is not
		/// obliged to keep a promise the seam can enforce.
		/// </para>
		/// </summary>
		bool TryRun(TIn input, out TOut output, out KingdomComputeCounters counters, out KingdomCityFault fault);
	}
}
