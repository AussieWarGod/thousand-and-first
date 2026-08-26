using ThousandAndFirst.Simulation.Kernel;

namespace ThousandAndFirst.Simulation.City
{
	/// <summary>What one advancement did, in counts the receipt can check.</summary>
	internal readonly struct KingdomAdvanceOutcome<TState>
	{
		internal readonly TState State;

		/// <summary>Passes spent. One pass is one propose plus one apply.</summary>
		internal readonly int Steps;

		/// <summary>Steps x 2R. LIVING-CITY-ARCHITECTURE &sect;0.0(a).</summary>
		internal readonly long RowVisits;

		internal readonly long ProcessedThroughTick;

		/// <summary>Whether the step budget ran out and the model jumped to its fixed point.</summary>
		internal readonly bool Overflowed;

		internal KingdomAdvanceOutcome(TState state, int steps, long rowVisits, long processedThroughTick, bool overflowed)
		{
			State = state;
			Steps = steps;
			RowVisits = rowVisits;
			ProcessedThroughTick = processedThroughTick;
			Overflowed = overflowed;
		}
	}
}
